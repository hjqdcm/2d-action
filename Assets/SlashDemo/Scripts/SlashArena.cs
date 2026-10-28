using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SlashDemo
{
    public sealed class SlashArena : MonoBehaviour
    {
        [Header("Prototype mode")]
        [Tooltip("Enable practice dummies; disable for hostile waves in the training ground.")]
        public bool practiceMode = true;
        public Vector2 arenaHalfSize = new Vector2(10.5f, 5.6f);
        [SerializeField] SlashPlayer player;
        [SerializeField] Camera worldCamera;
        [SerializeField] Transform actors;
        [SerializeField] SlashEffects effects;
        public VolumeProfile bloomProfile;
        VolumeProfile fallbackBloomProfile;
        readonly List<SlashEnemy> enemies = new List<SlashEnemy>();
        float nextWave = -1;
        float respawnDummies = -1;
        bool waitingForMenuRelease;
        int menuReleaseFrame;
        ObliqueView obliqueView;
        public ObliqueView View
        {
            get
            {
                if (obliqueView == null) obliqueView = GetComponent<ObliqueView>();
                if (obliqueView == null) obliqueView = gameObject.AddComponent<ObliqueView>();
                return obliqueView;
            }
        }
        public SlashPlayer Player => player;
        public Camera WorldCamera => worldCamera;
        public SlashCamera CameraRig => worldCamera.GetComponent<SlashCamera>();
        public SlashEffects Effects => effects;
        public SlashTuningPanel Tuning { get; private set; }
        public SceneTravel Travel { get; private set; }
        public TechniqueCodex Codex { get; private set; }
        public CharacterPanel CharacterPanel { get; private set; }
        public bool InputBlocked => Paused || waitingForMenuRelease || (Tuning != null && Tuning.BlocksGameplayInput)
            || (Travel != null && Travel.BlocksGameplayInput);
        public IReadOnlyList<SlashEnemy> Enemies => enemies;
        public bool Paused { get; private set; }
        public bool PauseMenuOpen => Paused && (Travel == null || !Travel.IsSelectionOpen)
            && (Codex == null || !Codex.IsOpen) && (CharacterPanel == null || !CharacterPanel.IsOpen);
        public bool Defeated { get; private set; }
        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public string LocationName => player != null && ManorEnvironment.InTraining(player.transform.position)
            ? "试刀训练场" : "私人洞府";

        void Start()
        {
            Time.timeScale = 1;
            if (player == null || worldCamera == null) BuildWorld();
            EnsureManorEnvironment();
            ConfigureBloom();
            View.PrepareEnvironment(transform.Find("Environment"));
            enemies.Clear();
            enemies.AddRange(actors.GetComponentsInChildren<SlashEnemy>());
            // Existing serialized actors keep their appearance, tuning and components.
            player.GetComponent<Rigidbody2D>().position = ManorEnvironment.SpawnPoint;
            for (int i = 0; i < enemies.Count; i++)
                enemies[i].GetComponent<Rigidbody2D>().position = ManorEnvironment.DummyPoint(i);
            CameraRig.target = player.transform;
            Tuning = GetComponent<SlashTuningPanel>();
            if (Tuning == null) Tuning = gameObject.AddComponent<SlashTuningPanel>();
            Tuning.Initialize(this);
            player.EquipTechnique(TechniqueCatalog.Find(PlayerPrefs.GetString(SlashPlayer.TechniqueSaveKey, TechniqueCatalog.FlowingCloud.id)));
            if (!practiceMode)
            {
                RemoveEnemies();
                nextWave = Time.time + 2;
            }
            Travel = GetComponent<SceneTravel>();
            if (Travel == null) Travel = gameObject.AddComponent<SceneTravel>();
            Travel.Initialize(this);
            Codex = GetComponent<TechniqueCodex>();
            if (Codex == null) Codex = gameObject.AddComponent<TechniqueCodex>();
            Codex.Initialize(this);
            CharacterPanel = GetComponent<CharacterPanel>();
            if (CharacterPanel == null) CharacterPanel = gameObject.AddComponent<CharacterPanel>();
            CharacterPanel.Initialize(this);
        }

        void Update()
        {
            if (waitingForMenuRelease && Time.frameCount > menuReleaseFrame
                && !DemoInput.GameplayHeld)
                waitingForMenuRelease = false;
            if (Codex != null && (Codex.IsOpen || Codex.ConsumedInputThisFrame)) return;
            if (CharacterPanel != null && (CharacterPanel.IsOpen || CharacterPanel.ConsumedInputThisFrame)) return;
            if (Travel != null && Travel.IsSelectionOpen)
            {
                if (DemoInput.PausePressed)
                {
                    Travel.CancelSelection();
                    TogglePause();
                }
                return;
            }
            if (DemoInput.PausePressed && !Defeated) TogglePause();
            if (Paused || Defeated || Time.deltaTime <= 0) return;
            if (Travel != null && Travel.Current != TravelDestination.Manor) return;
            if (!practiceMode && nextWave > 0 && Time.time >= nextWave
                && ManorEnvironment.InTraining(player.transform.position))
            { nextWave = -1; SpawnWave(); }
            if (practiceMode && respawnDummies > 0 && Time.time >= respawnDummies)
            {
                respawnDummies = -1;
                if (enemies.Count == 0) SpawnDummies();
            }
        }

        public void TogglePause()
        {
            effects.RestoreTime();
            Paused = !Paused;
            Time.timeScale = Paused ? 0 : 1;
            if (Paused && player != null) player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            if (!Paused)
            {
                waitingForMenuRelease = true;
                menuReleaseFrame = Time.frameCount;
            }
        }

        public void ResumeGame()
        {
            if (PauseMenuOpen) TogglePause();
        }

        public void ReturnHome()
        {
            if (!PauseMenuOpen) return;
            effects.Clear();
            if (Travel != null) Travel.ReturnHome();
            else
            {
                player.ResetRealm();
                player.Teleport(ManorEnvironment.SpawnPoint);
            }
            CameraRig.ResetFeedback();
            ResumeGame();
        }

        public void Restart()
        {
            var equipped = player != null ? player.Technique : TechniqueCatalog.FlowingCloud;
            if (Codex != null) Codex.Close();
            if (CharacterPanel != null) CharacterPanel.Close();
            if (Travel != null) Travel.ResetForRestart();
            effects.Clear();
            CameraRig.ResetFeedback();
            Paused = Defeated = false;
            Wave = Kills = 0;
            nextWave = respawnDummies = -1;
            foreach (Transform child in actors)
            { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            enemies.Clear();
            player = CreatePlayer(actors, ManorEnvironment.SpawnPoint);
            if (Tuning != null) Tuning.ApplyPlayer(player);
            player.EquipTechnique(equipped);
            CameraRig.target = player.transform;
            if (practiceMode)
            {
                SpawnDummies();
            }
            else
            {
                nextWave = Time.time + 2;
            }
        }

        public void EnemyKilled(SlashEnemy enemy)
        {
            enemies.Remove(enemy);
            Kills++;
            if (practiceMode)
            {
                if (enemies.Count == 0)
                    respawnDummies = Time.time + 2;
            }
            else if (enemies.Count == 0 && player.IsAlive)
            {
                player.Heal(15);
                nextWave = Time.time + 3;
            }
        }

        public void PlayerDied()
        {
            Defeated = true;
            nextWave = -1;
            effects.ClearSwordWaves();
            effects.ClearSwordScars();
        }

        void RemoveEnemies()
        {
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
            enemies.Clear();
        }

        void SpawnDummies()
        {
            for (int i = 0; i < 3; i++)
                enemies.Add(CreateEnemy(actors, ManorEnvironment.DummyPoint(i), true));
            if (Tuning != null)
                foreach (var enemy in enemies) Tuning.ApplyEnemy(enemy);
        }

        void SpawnWave()
        {
            Wave++;
            int count = Mathf.Min(3 + Wave, 10);
            for (int i = 0; i < count; i++)
            {
                Vector2 position = ManorEnvironment.WavePoint(i, count, Wave);
                if (Vector2.Distance(position, player.transform.position) < 1.5f)
                    position = ManorEnvironment.TrainingCenter * 2 - position;
                var enemy = CreateEnemy(actors, position, false, Mathf.Min(90, 52 + Wave * 4), Mathf.Min(2.9f, 1.65f + Wave * 0.12f));
                enemies.Add(enemy);
                if (Tuning != null) Tuning.ApplyEnemy(enemy);
                effects.Burst(position, DemoArt.Red, 8, 0.4f);
            }
        }

        void EnsureManorEnvironment()
        {
            var environment = transform.Find("Environment");
            if (environment != null && environment.GetComponentInChildren<ManorEnvironment>() != null) return;
            if (environment != null)
            {
                // Disable the legacy colliders immediately; Destroy is deferred until frame end.
                environment.name = "Legacy courtyard - replaced";
                environment.gameObject.SetActive(false);
                Destroy(environment.gameObject);
            }
            environment = new GameObject("Environment").transform;
            environment.SetParent(transform, false);
            CreateEnvironment(environment);
        }

        // Called by the editor builder. The scene remains editable and contains real scene objects.
        public void BuildWorld()
        {
            var environment = new GameObject("Environment").transform;
            environment.SetParent(transform, false);
            CreateEnvironment(environment);
            View.PrepareEnvironment(environment);
            actors = new GameObject("Actors").transform;
            actors.SetParent(transform, false);
            effects = new GameObject("Combat Effects").AddComponent<SlashEffects>();
            effects.transform.SetParent(transform, false);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.rotation = View.Rotation;
            cameraObject.transform.position = -ObliqueProjection.Forward(View.elevation) * 20;
            cameraObject.tag = "MainCamera";
            worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 7.5f;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color(0.027f, 0.047f, 0.064f);
            worldCamera.nearClipPlane = 0.1f;
            worldCamera.farClipPlane = 100;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<SlashCamera>();
            player = CreatePlayer(actors, ManorEnvironment.SpawnPoint);
            CameraRig.target = player.transform;
            SpawnDummies();
            gameObject.AddComponent<SlashHUD>();
        }

        public static SlashPlayer CreatePlayer(Transform parent, Vector2 position)
        {
            var go = new GameObject("Player - Swordsman");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            DemoArt.Body(go, 0.29f, 1.4f);
            DemoArt.Character(go.transform, false);
            var pivot = new GameObject("SwordPivot").transform;
            pivot.SetParent(go.transform, false);
            DemoArt.Shape("Hilt", pivot, "Square", new Vector2(0.4f, 0), new Vector2(0.35f, 0.1f), new Color(0.19f, 0.22f, 0.24f), 320);
            DemoArt.Shape("Guard", pivot, "Square", new Vector2(0.55f, 0), new Vector2(0.065f, 0.29f), DemoArt.Gold, 321);
            DemoArt.Shape("Blade", pivot, "Blade", new Vector2(1.03f, 0), new Vector2(0.95f, 0.13f), new Color(0.89f, 1, 0.95f), 322);
            var player = go.AddComponent<SlashPlayer>();
            go.AddComponent<ObliqueActor>().Initialize(parent.GetComponentInParent<SlashArena>());
            go.SetActive(true);
            return player;
        }

        public static SlashEnemy CreateEnemy(Transform parent, Vector2 position, bool dummy, int hp = 60, float speed = 1.9f)
        {
            var go = new GameObject(dummy ? "Training Dummy" : "Enemy - Ash Wanderer");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            DemoArt.Body(go, 0.34f, dummy ? 8 : 1);
            DemoArt.Character(go.transform, true, dummy);
            DemoArt.Shape("HealthBack", go.transform, "Square", new Vector2(0, 0.95f), new Vector2(0.84f, 0.085f), new Color(0.035f, 0.055f, 0.065f), 500);
            DemoArt.Shape("HealthFill", go.transform, "Square", new Vector2(0, 0.95f), new Vector2(0.8f, 0.055f), dummy ? DemoArt.Gold : DemoArt.Red, 501);
            var warning = new GameObject("AttackWarning").transform;
            warning.SetParent(go.transform, false);
            var points = new Vector3[24];
            points[0] = Vector3.zero;
            for (int i = 1; i < points.Length - 1; i++) points[i] = DemoArt.Direction(Mathf.Lerp(-52.5f, 52.5f, (i - 1) / 21f)) * 1.15f;
            points[23] = Vector3.zero;
            DemoArt.Line("DangerArc", warning, points, new Color(1, 0.3f, 0.3f, 0.85f), 0.045f, 10);
            warning.gameObject.SetActive(false);
            var enemy = go.AddComponent<SlashEnemy>();
            enemy.trainingDummy = dummy;
            enemy.maxHealth = dummy ? SlashTuningSettings.DefaultDummyHealth : hp;
            enemy.moveSpeed = speed;
            go.AddComponent<ObliqueActor>().Initialize(parent.GetComponentInParent<SlashArena>(), dummy);
            go.SetActive(true);
            return enemy;
        }

        void CreateEnvironment(Transform root) => ManorEnvironment.Build(root);

        public void ConfigureBloom()
        {
            worldCamera.allowHDR = true;
            var data = worldCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.volumeLayerMask |= 1 << gameObject.layer;
            var root = transform.Find("Combat Bloom");
            if (root == null)
            {
                root = new GameObject("Combat Bloom").transform;
                root.SetParent(transform, false);
            }
            root.gameObject.layer = gameObject.layer;
            var volume = root.GetComponent<Volume>();
            if (volume == null) volume = root.gameObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.weight = 1; volume.priority = 10;
            if (bloomProfile == null)
            {
                if (fallbackBloomProfile == null)
                {
                    fallbackBloomProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                    fallbackBloomProfile.name = "Runtime combat bloom";
                    var bloom = fallbackBloomProfile.Add<Bloom>(true);
                    bloom.threshold.value = 0.9f; bloom.intensity.value = 0.85f;
                    bloom.scatter.value = 0.6f; bloom.tint.value = new Color(0.9f, 0.95f, 1);
                }
                volume.sharedProfile = fallbackBloomProfile;
            }
            else volume.sharedProfile = bloomProfile;
        }

        void OnDisable() { Time.timeScale = 1; }
        void OnDestroy()
        {
            if (fallbackBloomProfile == null) return;
            foreach (var component in fallbackBloomProfile.components)
                if (component != null) Destroy(component);
            Destroy(fallbackBloomProfile);
        }
    }
}
