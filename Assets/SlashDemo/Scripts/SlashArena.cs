using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SlashDemo
{
    public sealed class GameResources
    {
        public const int StartingSpiritStones = 50;
        public const int StartingFortunePoints = 15;
        public const int FortunePerServiceVisit = 5;
        public const int TribulationUnlockSpend = 12;
        public const int PotionSlotCount = 3;
        readonly Dictionary<AlchemyEntry, int> pills = new Dictionary<AlchemyEntry, int>();
        readonly List<AlchemyEntry> pillOrder = new List<AlchemyEntry>();
        readonly AlchemyEntry?[] potionSlots = new AlchemyEntry?[PotionSlotCount];
        readonly Dictionary<AlchemyEntry, int> treasures = new Dictionary<AlchemyEntry, int>();
        long tribulationSpendingBaseline;
        public int SpiritStones { get; private set; } = StartingSpiritStones;
        public int FortunePoints { get; private set; } = StartingFortunePoints;
        public long FortuneSpent { get; private set; }
        public long TribulationFortuneSpent => FortuneSpent - tribulationSpendingBaseline;
        public bool TribulationUnlocked => TribulationFortuneSpent >= TribulationUnlockSpend;
        internal bool FoundationTribulationCleared { get; set; }
        internal bool BloodSaberDefeated { get; private set; }
        internal CultivationRealm? LastScriptureLibraryRealm { get; set; }
        internal ScriptureStudy Scripture { get; } = new ScriptureStudy();
        public int ServiceVisits { get; private set; }
        public bool ServicesUnlocked { get; private set; }
        internal IReadOnlyDictionary<AlchemyEntry, int> Pills => pills;
        internal IReadOnlyList<AlchemyEntry> PillOrder => pillOrder;
        internal IReadOnlyList<AlchemyEntry?> PotionSlots => potionSlots;
        internal IReadOnlyDictionary<AlchemyEntry, int> Treasures => treasures;
        internal bool FinalBattleEnteredOnExhaustion { get; set; }

        internal void CollectLoot(AlchemyEntry item, bool isTreasure)
        {
            var inventory = isTreasure ? treasures : pills;
            if (!inventory.TryGetValue(item, out int count) && !isTreasure) pillOrder.Add(item);
            inventory[item] = count + 1;
        }

        internal bool CanAssignPotion(int slot, AlchemyEntry entry)
        {
            if (!pills.TryGetValue(entry, out int available)) return false;
            for (int i = 0; i < potionSlots.Length; i++)
                if (i != slot && potionSlots[i].HasValue && potionSlots[i].Value.Equals(entry)) available--;
            return available > 0;
        }

        internal bool TryAssignPotion(int slot, AlchemyEntry? entry)
        {
            if (slot < 0 || slot >= potionSlots.Length || (entry.HasValue && !CanAssignPotion(slot, entry.Value))) return false;
            potionSlots[slot] = entry;
            return true;
        }

        internal void RefillPotionSlots()
        {
            for (int i = 0; i < potionSlots.Length; i++)
            {
                if (potionSlots[i].HasValue) continue;
                foreach (var entry in pillOrder)
                    if (TryAssignPotion(i, entry)) break;
            }
        }

        internal bool TryDrinkPotion(int slot, out AlchemyEntry entry)
        {
            entry = default;
            if (slot < 0 || slot >= potionSlots.Length || !potionSlots[slot].HasValue) return false;
            entry = potionSlots[slot].Value;
            if (!pills.TryGetValue(entry, out int count) || count <= 0) return false;
            potionSlots[slot] = null;
            if (count > 1) pills[entry] = count - 1;
            else
            {
                pills.Remove(entry);
                pillOrder.Remove(entry);
            }
            return true;
        }

        public bool TryChangeSpiritStones(int delta)
        {
            if (!TryGetBalance(SpiritStones, delta, out int balance)) return false;
            SpiritStones = balance;
            return true;
        }

        public bool TryChangeFortunePoints(int delta)
        {
            if (!TryGetBalance(FortunePoints, delta, out int balance)) return false;
            FortunePoints = balance;
            if (delta < 0)
            {
                long previousThresholds = FortuneSpent / FortunePerServiceVisit;
                FortuneSpent -= (long)delta;
                long newVisits = FortuneSpent / FortunePerServiceVisit - previousThresholds;
                ServiceVisits = (int)System.Math.Min(int.MaxValue, ServiceVisits + newVisits);
                ServicesUnlocked |= ServiceVisits > 0;
            }
            return true;
        }

        internal void UseServiceVisit() => ServiceVisits--;

        internal void RecordBloodSaberDefeat()
        {
            if (BloodSaberDefeated) return;
            BloodSaberDefeated = true;
            if (FoundationTribulationCleared) tribulationSpendingBaseline = FortuneSpent;
        }

        internal void Reset()
        {
            pills.Clear();
            pillOrder.Clear();
            System.Array.Clear(potionSlots, 0, potionSlots.Length);
            treasures.Clear();
            SpiritStones = StartingSpiritStones;
            FortunePoints = StartingFortunePoints;
            FortuneSpent = ServiceVisits = 0;
            tribulationSpendingBaseline = 0;
            FoundationTribulationCleared = BloodSaberDefeated = false;
            LastScriptureLibraryRealm = null;
            Scripture.Reset();
            ServicesUnlocked = FinalBattleEnteredOnExhaustion = false;
        }

        static bool TryGetBalance(int current, int delta, out int balance)
        {
            long next = (long)current + delta;
            balance = current;
            if (next < 0 || next > int.MaxValue) return false;
            balance = (int)next;
            return true;
        }
    }

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
        bool pauseMenuOverSelection;
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
        public GameResources Resources { get; } = new GameResources();
        public Camera WorldCamera => worldCamera;
        public SlashCamera CameraRig => worldCamera.GetComponent<SlashCamera>();
        public SlashEffects Effects => effects;
        public SlashTuningPanel Tuning { get; private set; }
        public SceneTravel Travel { get; private set; }
        public TechniqueCodex Codex { get; private set; }
        public CharacterPanel CharacterPanel { get; private set; }
        public bool InputBlocked => Defeated || Paused || waitingForMenuRelease || (Tuning != null && Tuning.BlocksGameplayInput)
            || (Travel != null && Travel.BlocksGameplayInput);
        public IReadOnlyList<SlashEnemy> Enemies => enemies;
        public bool Paused { get; private set; }
        public bool PauseMenuOpen => Paused && !Defeated && (pauseMenuOverSelection || Travel == null || !Travel.HasModal)
            && (Codex == null || !Codex.IsOpen) && (CharacterPanel == null || !CharacterPanel.IsOpen);
        public bool Defeated { get; private set; }
        public double CombatTime { get; private set; }
        public bool InCombat => (Travel != null && Travel.IsTribulationActive) || HasLiveCombatEnemy();
        internal bool CanConfigurePotions => !Defeated && player.IsAlive && !InCombat && (Travel == null || !Travel.HasModal);
        internal int PotionConfigSlot { get; set; }
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
            if (CharacterPanel != null && (CharacterPanel.IsOpen || CharacterPanel.ConsumedInputThisFrame))
            {
                int slot = DemoInput.PotionSlotPressed;
                if (slot >= 0 && CharacterPanel.IsOpen && CharacterPanel.ActivePage == CharacterPanelPage.Items
                    && CharacterPanel.ActiveInventoryPage == CharacterInventoryPage.Pills
                    && CanConfigurePotions) PotionConfigSlot = slot;
                return;
            }
            if (Travel != null && Travel.IsShopOpen)
            {
                if (DemoInput.PausePressed) Travel.CloseShop();
                return;
            }
            if (Travel != null && (Travel.IsLootChoiceOpen || Travel.IsChallengeDecisionOpen
                || Travel.IsTribulationDecisionOpen || Travel.IsTreatmentOpen)) return;
            if (DemoInput.PausePressed && !Defeated)
            {
                if (PauseMenuOpen) ResumeGame();
                else if (Travel != null && Travel.IsSelectionOpen) pauseMenuOverSelection = true;
                else TogglePause();
            }
            if (Paused || Defeated) return;
            float dt = Time.deltaTime;
            if (dt > 0 && InCombat) CombatTime += dt;
            player.AdvancePills(CombatTime);
            int potionSlot = DemoInput.PotionSlotPressed;
            if (potionSlot >= 0 && !InputBlocked && player.IsAlive
                && Resources.TryDrinkPotion(potionSlot, out var potion)) player.DrinkPill(potion);
            if (dt <= 0) return;
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

        bool HasLiveCombatEnemy()
        {
            foreach (var enemy in enemies)
                if (enemy != null && enemy.Alive && enemy.isActiveAndEnabled && !enemy.trainingDummy)
                    return true;
            return false;
        }

        public void TogglePause()
        {
            pauseMenuOverSelection = false;
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
            if (!PauseMenuOpen) return;
            if (pauseMenuOverSelection) pauseMenuOverSelection = false;
            else TogglePause();
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
            var character = player != null ? player.Character : PlayableCharacter.Swordsman;
            if (Codex != null) Codex.Close();
            if (CharacterPanel != null) CharacterPanel.Close();
            if (Travel != null) Travel.ResetForRestart();
            effects.Clear();
            CameraRig.ResetFeedback();
            Paused = Defeated = false;
            CombatTime = 0;
            PotionConfigSlot = 0;
            Wave = Kills = 0;
            nextWave = respawnDummies = -1;
            foreach (Transform child in actors)
            { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            enemies.Clear();
            Resources.Reset();
            player = CreatePlayer(actors, ManorEnvironment.SpawnPoint, character: character);
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
            bool wasRegistered = enemies.Remove(enemy);
            Kills++;
            if (wasRegistered && !enemy.trainingDummy && !InCombat && player.IsAlive) FinishPotionBattle();
            if (Travel != null) Travel.EnemyDefeated(enemy);
            if (Travel != null && Travel.Current != TravelDestination.Manor) return;
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

        internal void FinishPotionBattle()
        {
            player.ResetAlchemyBattle();
            Resources.RefillPotionSlots();
        }

        public void PlayerDied()
        {
            if (Defeated) return;
            Defeated = true;
            nextWave = -1;
            effects.ClearSwordWaves();
            effects.ClearSwordScars();
            effects.RestoreTime();
            Paused = true;
            Time.timeScale = 0;
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }

        public void ReviveAtHome()
        {
            if (!Defeated || player == null) return;
            bool continuous3D = player.useContinuous3D;
            if (Codex != null) Codex.Close();
            if (CharacterPanel != null) CharacterPanel.Close();
            effects.Clear();
            if (Travel != null) Travel.ReturnHome();
            var defeatedPlayer = player;
            defeatedPlayer.gameObject.SetActive(false);
            Resources.Reset();
            player = CreatePlayer(actors, ManorEnvironment.SpawnPoint, continuous3D, defeatedPlayer.Character);
            if (Tuning != null) Tuning.ApplyPlayer(player);
            // Death resets progression and possessions unless an explicit retention rule is added.
            player.EquipTechnique(TechniqueCatalog.FlowingCloud, true);
            player.ResetRealm();
            CombatTime = 0;
            PotionConfigSlot = 0;
            player.Heal(player.maxHealth);
            Destroy(defeatedPlayer.gameObject);
            CameraRig.target = player.transform;
            CameraRig.ResetFeedback();
            Defeated = Paused = false;
            Time.timeScale = 1;
            // The resurrection click must be released before it can start a sword attack.
            waitingForMenuRelease = true;
            menuReleaseFrame = Time.frameCount;
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

        public void RegisterEnemy(SlashEnemy enemy)
        {
            if (enemy != null && !enemies.Contains(enemy)) enemies.Add(enemy);
        }

        public void UnregisterEnemy(SlashEnemy enemy) => enemies.Remove(enemy);

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

        public static SlashPlayer CreatePlayer(Transform parent, Vector2 position, bool continuous3D = false,
            PlayableCharacter character = PlayableCharacter.Swordsman)
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
            player.Character = character;
            player.useContinuous3D = continuous3D;
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
            DemoArt.Line("DangerArc", warning, points, DemoArt.AttackWarningColor, .045f * DemoArt.AttackWarningWidthScale, 10);
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
