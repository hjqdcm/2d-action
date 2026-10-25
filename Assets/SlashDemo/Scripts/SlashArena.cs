using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SlashDemo
{
    public sealed class SlashArena : MonoBehaviour
    {
        [Header("Prototype mode")]
        [Tooltip("T toggles between practice dummies and hostile waves.")]
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
        float bannerTimer;
        string banner = "";
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
        public bool InputBlocked => Tuning != null && Tuning.BlocksGameplayInput;
        public IReadOnlyList<SlashEnemy> Enemies => enemies;
        public bool Paused { get; private set; }
        public bool Defeated { get; private set; }
        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public string Banner => bannerTimer > 0 ? banner : "";

        void Start()
        {
            Time.timeScale = 1;
            if (player == null || worldCamera == null) BuildWorld();
            ConfigureBloom();
            View.PrepareEnvironment(transform.Find("Environment"));
            enemies.Clear();
            enemies.AddRange(actors.GetComponentsInChildren<SlashEnemy>());
            CameraRig.target = player.transform;
            Tuning = GetComponent<SlashTuningPanel>();
            if (Tuning == null) Tuning = gameObject.AddComponent<SlashTuningPanel>();
            Tuning.Initialize(this);
            if (practiceMode)
                ShowBanner("试刀 · 按住左键连斩", 3);
            else
            {
                RemoveEnemies();
                nextWave = Time.time + 2;
                ShowBanner("准备迎敌", 2);
            }
        }

        void Update()
        {
            if (DemoInput.RestartPressed) Restart();
            if (DemoInput.PracticePressed) { practiceMode = !practiceMode; Restart(); }
            if (DemoInput.PausePressed && !Defeated) TogglePause();
            if (Paused || Defeated || Time.deltaTime <= 0) return;
            bannerTimer = Mathf.Max(0, bannerTimer - Time.deltaTime);
            if (!practiceMode && nextWave > 0 && Time.time >= nextWave)
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
        }

        public void Restart()
        {
            effects.Clear();
            CameraRig.ResetFeedback();
            Paused = Defeated = false;
            Wave = Kills = 0;
            nextWave = respawnDummies = -1;
            foreach (Transform child in actors)
            { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            enemies.Clear();
            player = CreatePlayer(actors, new Vector2(-1.5f, -0.6f));
            if (Tuning != null) Tuning.ApplyPlayer(player);
            CameraRig.target = player.transform;
            if (practiceMode)
            {
                SpawnDummies();
                ShowBanner("试刀 · 按住左键连斩", 2.5f);
            }
            else
            {
                nextWave = Time.time + 2;
                ShowBanner("准备迎敌", 2);
            }
        }

        public void EnemyKilled(SlashEnemy enemy)
        {
            enemies.Remove(enemy);
            Kills++;
            if (practiceMode)
            {
                if (enemies.Count == 0)
                { respawnDummies = Time.time + 2; ShowBanner("试刀完成 · 木桩即将重置", 2); }
            }
            else if (enemies.Count == 0 && player.IsAlive)
            {
                player.Heal(15);
                nextWave = Time.time + 3;
                ShowBanner("本轮肃清 · 气血 +15", 2.6f);
            }
        }

        public void PlayerDied()
        {
            Defeated = true;
            nextWave = -1;
            effects.ClearSwordWaves();
        }

        void ShowBanner(string value, float duration) { banner = value; bannerTimer = duration; }
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
            enemies.Add(CreateEnemy(actors, new Vector2(1.3f, 0), true));
            enemies.Add(CreateEnemy(actors, new Vector2(3.6f, 1.8f), true));
            enemies.Add(CreateEnemy(actors, new Vector2(3.6f, -1.8f), true));
            if (Tuning != null)
                foreach (var enemy in enemies) Tuning.ApplyEnemy(enemy);
        }

        void SpawnWave()
        {
            Wave++;
            int count = Mathf.Min(3 + Wave, 10);
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count * 360) + Wave * 33;
                Vector2 position = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * 8.3f, Mathf.Sin(a * Mathf.Deg2Rad) * 4.2f);
                if (Vector2.Distance(position, player.transform.position) < 3)
                    position = -position;
                var enemy = CreateEnemy(actors, position, false, Mathf.Min(90, 52 + Wave * 4), Mathf.Min(2.9f, 1.65f + Wave * 0.12f));
                enemies.Add(enemy);
                if (Tuning != null) Tuning.ApplyEnemy(enemy);
                effects.Burst(position, DemoArt.Red, 8, 0.4f);
            }
            ShowBanner("第 " + Wave.ToString("00") + " 波 · 来敌 " + count, 2);
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
            player = CreatePlayer(actors, new Vector2(-1.5f, -0.6f));
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
            enemy.maxHealth = dummy ? 150 : hp;
            enemy.moveSpeed = speed;
            go.AddComponent<ObliqueActor>().Initialize(parent.GetComponentInParent<SlashArena>(), dummy);
            go.SetActive(true);
            return enemy;
        }

        void CreateEnvironment(Transform root)
        {
            DemoArt.Shape("Outer darkness", root, "Square", Vector2.zero, new Vector2(70, 45), new Color(0.027f, 0.047f, 0.064f), -200);
            DemoArt.Shape("Arena foundation", root, "Square", Vector2.zero, arenaHalfSize * 2 + Vector2.one * 0.6f, new Color(0.13f, 0.21f, 0.24f), -190);
            DemoArt.Shape("Stone floor", root, "Square", Vector2.zero, arenaHalfSize * 2, new Color(0.075f, 0.12f, 0.15f), -180);
            for (int y = -5; y <= 5; y++)
            for (int x = -10; x <= 10; x++)
            {
                float shift = y % 2 == 0 ? 0 : 0.48f;
                float tint = ((x * 17 + y * 13 + 300) % 7) * 0.002f;
                DemoArt.Shape("Slate", root, "Square", new Vector2(x + shift, y), new Vector2(0.96f, 0.96f),
                    new Color(0.085f + tint, 0.134f + tint, 0.16f + tint), -170);
            }
            var seal = new GameObject("Courtyard seal").transform;
            seal.SetParent(root, false);
            var ringColor = new Color(0.22f, 0.40f, 0.41f, 0.5f);
            DemoArt.Ring("Outer seal", seal, 3.6f, ringColor, 0.035f, -160);
            DemoArt.Ring("Inner seal", seal, 3.35f, ringColor, 0.02f, -160);
            DemoArt.Ring("Center seal", seal, 0.72f, ringColor, 0.035f, -160);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45;
                DemoArt.Shape("Seal glyph", seal, "Square", DemoArt.Direction(angle) * 3.48f, new Vector2(0.13f, 0.13f), ringColor, -159, angle);
            }
            CreateWall(root, new Vector2(0, arenaHalfSize.y + 0.2f), new Vector2(arenaHalfSize.x * 2 + 0.8f, 0.4f));
            CreateWall(root, new Vector2(0, -arenaHalfSize.y - 0.2f), new Vector2(arenaHalfSize.x * 2 + 0.8f, 0.4f));
            CreateWall(root, new Vector2(arenaHalfSize.x + 0.2f, 0), new Vector2(0.4f, arenaHalfSize.y * 2));
            CreateWall(root, new Vector2(-arenaHalfSize.x - 0.2f, 0), new Vector2(0.4f, arenaHalfSize.y * 2));
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 3 - 1) * 8.7f;
                float y = i < 3 ? 5.4f : -5.4f;
                DemoArt.Shape("Lantern shadow", root, "Disk", new Vector2(x, y), new Vector2(1.8f, 0.8f), new Color(1, 0.62f, 0.24f, 0.035f), -120);
                DemoArt.Shape("Lantern base", root, "Square", new Vector2(x, y), new Vector2(0.35f, 0.5f), new Color(0.23f, 0.28f, 0.29f), -100);
                DemoArt.Shape("Lantern light", root, "Disk", new Vector2(x, y + 0.12f), new Vector2(0.19f, 0.24f), DemoArt.Gold, -99);
            }
            // Deterministic edge debris gives scale without adding obstacles to the first combat test.
            var random = new System.Random(71);
            for (int i = 0; i < 70; i++)
            {
                float x = (float)random.NextDouble() * 20 - 10;
                float y = (float)random.NextDouble() * 10.5f - 5.25f;
                if (Mathf.Abs(y) < 3.7f && Mathf.Abs(x) < 8) continue;
                DemoArt.Shape("Fallen leaf", root, "Blade", new Vector2(x, y), new Vector2(0.13f, 0.05f), new Color(0.46f, 0.47f, 0.33f, 0.45f), -140, (float)random.NextDouble() * 360);
            }
        }

        static void CreateWall(Transform root, Vector2 position, Vector2 size)
        {
            var wall = DemoArt.Shape("Arena boundary", root, "Square", position, size, new Color(0.18f, 0.29f, 0.31f), -130);
            wall.gameObject.AddComponent<BoxCollider2D>();
        }

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
