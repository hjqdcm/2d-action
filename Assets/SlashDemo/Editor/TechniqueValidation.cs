using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SlashDemo.Editor
{
    public static class TechniqueValidation
    {
        const string Pending = "SlashDemo.TechniqueValidation";
        static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        static double deadline;
        static Keyboard keyboard;
        static Mouse mouse;
        static KeyboardState? keys;
        static MouseState? pointer;
        static int checks;
        static EditorWindow captureView;
        static bool hadTechniqueSelection;
        static string savedTechniqueSelection;

        public static void RunIronBatch()
        {
            SessionState.SetBool(Pending + ".Iron", true);
            RunBatch();
        }

        public static void RunIronZeroImpactBatch()
        {
            SessionState.SetBool(Pending + ".ZeroImpact", true);
            RunBatch();
        }

        public static void RunBatch()
        {
            AssetDatabase.DesiredWorkerCount = 1;
            AssetDatabase.ForceToDesiredWorkerCount();
            var viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            captureView = ScriptableObject.CreateInstance(viewType) as EditorWindow;
            captureView.ShowUtility();
            captureView.position = new Rect(0, 0, 1600, 940);
            captureView.Focus();
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + ".Launch", true);
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 240;
            Time.captureDeltaTime = 0;
            Time.maximumDeltaTime = 0.05f;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlay; EditorApplication.playModeStateChanged += OnPlay;
        }
        static void Inject()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            if (keys.HasValue)
            {
                InputState.Change(keyboard, keys.Value); keyboard.MakeCurrent(); keys = null;
            }
            if (pointer.HasValue)
            {
                InputState.Change(mouse, pointer.Value); mouse.MakeCurrent(); pointer = null;
            }
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.isPaused = false;
            checks = 0; routines.Clear(); keys = null; pointer = null;
            hadTechniqueSelection = PlayerPrefs.HasKey(SlashPlayer.TechniqueSaveKey);
            savedTechniqueSelection = PlayerPrefs.GetString(SlashPlayer.TechniqueSaveKey);
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.onBeforeUpdate += Inject;
            routines.Push(SessionState.GetBool(Pending + ".ZeroImpact", false) ? ValidateIronZeroImpact()
                : SessionState.GetBool(Pending + ".Iron", false) ? RunIron() : Run());
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(Pending + ".Launch", false))
            {
                SessionState.SetBool(Pending + ".Launch", false); EditorApplication.isPlaying = true; return;
            }
            if (!EditorApplication.isPlaying || routines.Count == 0) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Technique validation timed out.");
                var current = routines.Peek();
                if (!current.MoveNext()) routines.Pop();
                else if (current.Current is IEnumerator child) routines.Push(child);
                if (routines.Count != 0) return;
                if (SessionState.GetBool(Pending + ".ZeroImpact", false)) Debug.Log("IRON_ZERO_IMPACT_PASS: " + checks + " assertions.");
                if (SessionState.GetBool(Pending + ".Iron", false)) Debug.Log("IRON_SWORD_PLAY_PASS: " + checks + " assertions.");
                Debug.Log("TECHNIQUE_PLAY_PASS: " + checks + " assertions; combo passives, consuming rush/path damage/scaling/finisher/collision, codex and input suppression.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogError("TECHNIQUE_PLAY_FAIL: " + exception); Finish(1); }
        }
        static void Finish(int code)
        {
            if (hadTechniqueSelection) PlayerPrefs.SetString(SlashPlayer.TechniqueSaveKey, savedTechniqueSelection);
            else PlayerPrefs.DeleteKey(SlashPlayer.TechniqueSaveKey);
            PlayerPrefs.Save();
            SessionState.SetBool(Pending + ".Iron", false);
            SessionState.SetBool(Pending + ".ZeroImpact", false);
            SessionState.SetBool(Pending, false); InputSystem.onBeforeUpdate -= Inject;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            EditorApplication.Exit(code);
        }

        public static void RunTravelBatch()
        {
            AssetDatabase.DesiredWorkerCount = 1;
            AssetDatabase.ForceToDesiredWorkerCount();
            SceneTravelValidation.RunBatch();
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++; Debug.Log("TECHNIQUE_CHECK: " + message);
        }
        static IEnumerator Wait(float duration)
        {
            float end = Time.time + duration;
            while (Time.time < end) yield return null;
        }
        static IEnumerator Frames(int count = 5)
        {
            int end = Time.frameCount + count;
            while (Time.frameCount < end) yield return null;
        }
        static IEnumerator Idle(SlashPlayer player)
        {
            while (player.IsAttacking || player.IsComboRecovering || player.IsDashing) yield return null;
        }
        static void Place(SlashArena arena, CultivationRealm realm, bool multiple = false)
        {
            arena.Restart();
            arena.Tuning.Settings.dummyHealth = 2000; arena.Tuning.Settings.invincible = false;
            arena.Tuning.ApplyAll(); arena.Effects.hitStopMultiplier = 0;
            arena.Player.SetRealm(realm); arena.Player.Teleport(ManorEnvironment.TrainingCenter);
            for (int i = 0; i < arena.Enemies.Count; i++)
            {
                var body = arena.Enemies[i].GetComponent<Rigidbody2D>();
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                body.position = ManorEnvironment.TrainingCenter + (i == 0 || (multiple && i == 1)
                    ? new Vector2(1.2f, i == 0 ? -0.15f : 0.25f) : new Vector2(-5, 3 + i));
                arena.Enemies[i].transform.position = body.position;
            }
            Physics2D.SyncTransforms();
        }
        static IEnumerator FirstTwo(SlashPlayer player)
        {
            for (int i = 0; i < 2; i++)
            {
                Check(player.RequestAttack(Vector2.right) && player.ComboIndex == i, "ordinary segment " + (i + 1) + " starts");
                yield return Idle(player);
            }
        }
        static IEnumerator Run()
        {
            yield return Frames();
            var arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
            if (arena != null) arena.Player.EquipTechnique(TechniqueCatalog.FlowingCloud);
            Check(arena != null && arena.Codex != null && arena.Player.Technique == TechniqueCatalog.FlowingCloud,
                "technique and codex initialize in the existing scene");
            var activeIcon = Resources.Load<Texture2D>("SlashDemo/FlowingCloudActiveIcon");
            Check(activeIcon != null && ReferenceEquals(activeIcon,
                typeof(SlashHUD).GetField("skillIcon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena.GetComponent<SlashHUD>())),
                "HUD loads the supplied transparent active skill icon");
            arena.Tuning.RestoreDefaults();
            var savedDefaults = JsonUtility.FromJson<SlashTuningSettings>(File.ReadAllText("Verification/Artifacts/SavedTuning-2026-10-27.json"));
            var codeDefaults = new SlashTuningSettings();
            codeDefaults.Validate(new SlashTuningSettings());
            savedDefaults.Validate(codeDefaults);
            CheckSavedDefaults(codeDefaults, savedDefaults, "code defaults");
            CheckSavedDefaults(arena.Tuning.Settings, savedDefaults, "scene restore defaults");
            yield return ValidateCharacterPanel(arena);
            yield return ValidateBuffBar(arena);
            yield return ValidateMissedAttacks(arena);

            var player = arena.Player; var target = arena.Enemies[0];
            int health = 0, swings = 0;
            foreach (CultivationRealm realm in new[] { CultivationRealm.QiRefining, CultivationRealm.FoundationEstablishment })
            {
                Place(arena, realm); yield return Frames();
                player = arena.Player; target = arena.Enemies[0];
                yield return FirstTwo(player);
                health = target.Health;
                Check(player.RequestAttack(Vector2.right), "third segment starts");
                while (target.Health == health) yield return null;
                swings = player.TotalSwings;
                player.RequestAttack(Vector2.right);
                while (player.TotalSwings == swings) yield return null;
                Check(player.ComboIndex == 0, "Qi Refining and Foundation both restart at segment one after the third strike");
                yield return Idle(player);
            }

            yield return ValidateAttackSpeed(arena);
            yield return ValidateRebuildCooldown(arena);
            yield return ValidateDodgeStackPenalty(arena);

            Place(arena, CultivationRealm.QiRefining, true); yield return Frames();
            player = arena.Player; target = arena.Enemies[0];
            for (int i = 0; i < 7; i++)
            {
                health = target.Health;
                bool continuousStarted = player.RequestAttack(Vector2.right, false);
                Check(continuousStarted, "continuous light attack starts" + (continuousStarted ? "" :
                    $" (alive={player.IsAlive}, attacking={player.IsAttacking}, recovering={player.IsComboRecovering}, dash={player.IsDashing}, heavy={player.IsHeavyBusy}, paused={arena.Paused}, blocked={arena.InputBlocked})"));
                int expected = Mathf.RoundToInt(player.strikes[player.ComboIndex].damage * (1 + 0.08f * Mathf.Min(i, 5) + (i >= 5 ? 0.20f : 0)));
                yield return Idle(player);
                Check(health - target.Health == expected, "normal damage uses existing stacks before the hit adds its new layer");
                Check(player.ComboStacks == Mathf.Min(i + 1, 5), "multiple targets grant only one stack, capped at five");
                Check(Mathf.Abs(player.ComboDamageBonus - (Mathf.Min(i + 1, 5) * 0.08f + (i >= 4 ? 0.20f : 0))) < 0.001f,
                    "damage bonus display includes the extra twenty percent exactly at five stacks");
            }
            player.RequestAttack(Vector2.right);
            Check(player.TakeDamage(1, Vector2.left) && player.ComboStacks == 0 && !player.IsAttacking,
                "accepted incoming damage interrupts combat and clears stacks");
            Check(player.RequestAttack(Vector2.right), "attack resumes after interruption"); yield return Idle(player);
            Check(player.ComboStacks == 1 && player.TryDash(Vector2.left) && player.ComboStacks == 0, "dodging with one layer clamps at zero");
            yield return Idle(player);
            // Return to the target without touching player state beyond a deliberate interruption.
            player.Teleport(ManorEnvironment.TrainingCenter);
            Check(player.RequestAttack(Vector2.right), "timeout test starts"); yield return Idle(player);
            yield return Wait(player.comboResetTime + 0.04f);
            Check(player.ComboStacks == 1 && player.ComboIndex == -1 && player.ComboStackTimeLeft > 0,
                "normal combo timeout restarts the sequence but preserves the two-second stack buff");
            float pausedStackTime = player.ComboStackTimeLeft;
            arena.TogglePause(); yield return Frames();
            Check(player.ComboStackTimeLeft == pausedStackTime && player.ComboStacks == 1, "pause freezes the stack buff timer");
            arena.TogglePause(); yield return Frames();
            health = target.Health;
            Check(player.RequestAttack(Vector2.right), "a new sequence can continue the unexpired stack buff");
            yield return Idle(player);
            Check(player.ComboStacks == 2 && health - target.Health == Mathf.RoundToInt(22 * 1.08f)
                && player.ComboStackTimeLeft > 1.5f, "next hit inherits layers and resets the two-second timer");
            Capture("Technique-StackTimer.png");
            yield return Wait(Mathf.Max(0, player.ComboStackTimeLeft - 0.1f));
            Check(player.ComboStacks == 2, "stack bonus remains active just before expiry");
            yield return Wait(0.15f); yield return Frames();
            Check(player.ComboStacks == 0 && player.ComboStackTimeLeft == 0, "stack bonus clears after two seconds without a hit");

            Place(arena, CultivationRealm.GoldenCore); yield return Frames();
            player = arena.Player; target = arena.Enemies[0];
            Vector2 aimPointer = arena.WorldCamera.WorldToScreenPoint((Vector3)(ManorEnvironment.TrainingCenter + Vector2.right * 6));
            pointer = new MouseState { position = aimPointer }.WithButton(MouseButton.Left);
            while (player.TotalPursuits == 0) yield return null;
            pointer = new MouseState { position = aimPointer };
            Check(player.IsPursuit, "holding the real left mouse button automatically chains into pursuit at Golden Core");
            yield return Idle(player);

            Place(arena, CultivationRealm.GoldenCore); yield return Frames();
            player = arena.Player;
            yield return FirstTwo(player);
            foreach (var enemy in arena.Enemies)
            {
                enemy.transform.position = (Vector2)player.transform.position + new Vector2(-6, 4);
                enemy.GetComponent<Rigidbody2D>().position = enemy.transform.position;
            }
            Physics2D.SyncTransforms();
            Check(player.RequestAttack(Vector2.right) && player.ComboIndex == 2, "missed-third test starts the third segment");
            yield return Wait(player.strikes[2].duration * 0.45f);
            swings = player.TotalSwings;
            player.RequestAttack(Vector2.right);
            while (player.TotalSwings == swings) yield return null;
            Check(player.ComboIndex == 0 && player.TotalPursuits == 0 && arena.Effects.ActiveWhirlwindCount == 0,
                "a buffered fourth input after a missed third segment restarts at one without a whirlwind");
            Check(player.ComboStacks == 2, "a missed third segment preserves prior layers without unlocking whirlwind");
            yield return Idle(player);

            foreach (var realm in new[] { CultivationRealm.GoldenCore, CultivationRealm.NascentSoul })
            {
                Place(arena, realm, true); yield return Frames();
                player = arena.Player; target = arena.Enemies[0];
                yield return FirstTwo(player);
                health = target.Health; player.RequestAttack(Vector2.right);
                while (target.Health == health) yield return null;
                swings = player.TotalSwings; player.RequestAttack(Vector2.right);
                while (player.TotalSwings == swings) yield return null;
                Check(player.IsPursuit && player.TotalPursuits == 1, "fourth deliberate press starts an independent pursuit");
                // Place targets behind and beside the player after the third segment has finished.
                var rear = arena.Enemies[1]; var side = arena.Enemies[2];
                rear.transform.position = (Vector2)player.transform.position + Vector2.left * 1.5f;
                rear.GetComponent<Rigidbody2D>().position = rear.transform.position;
                side.transform.position = (Vector2)player.transform.position + Vector2.up * 1.9f;
                side.GetComponent<Rigidbody2D>().position = side.transform.position;
                Physics2D.SyncTransforms();
                int rearHealth = rear.Health, sideHealth = side.Health;
                if (realm == CultivationRealm.NascentSoul)
                {
                    var heavy = arena.Tuning.Settings.heavyAttack;
                    heavy.damage = 73; heavy.width = 2.3f; heavy.distance = 8; heavy.speed = 17; heavy.knockback = 9;
                    arena.Tuning.ApplyAll(); arena.Effects.hitStopMultiplier = 0;
                }
                health = target.Health;
                while (arena.Effects.ActiveWhirlwindCount == 0) yield return null;
                var ring = CurrentWhirlwind(arena);
                int emissionDamage = Mathf.RoundToInt(player.FlowingCloud.pursuitDamage * (1 + 3 * player.FlowingCloud.stackBonus));
                Check(rearHealth - rear.Health == emissionDamage && sideHealth - side.Health == emissionDamage,
                    "rear and edge enemies take full whirlwind damage in the sword-light emission frame");
                const BindingFlags ringFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Check(GameObject.Find("Whirlwind - expanding ice ring") == null
                    && GameObject.Find("Whirlwind - delayed bright inner ring") == null,
                    "whirlwind omits both inner visual rings while retaining radial damage");
                if (realm == CultivationRealm.GoldenCore)
                {
                    while ((float)ring.GetType().GetField("age", ringFields).GetValue(ring) < 0.08f) yield return null;
                    var swordLight = (Mesh)typeof(SlashPlayer).GetField("trailMesh", ringFields).GetValue(player);
                    Check(swordLight.vertexCount == 65 * 5, "single outer sword-light ribbon retains all 64 closed segments");
                    Capture("Whirlwind-Slash.png");
                    float pausedAge = (float)ring.GetType().GetField("age", ringFields).GetValue(ring);
                    int pausedHealth = rear.Health;
                    arena.TogglePause(); yield return Frames();
                    Check((float)ring.GetType().GetField("age", ringFields).GetValue(ring) == pausedAge && rear.Health == pausedHealth,
                        "pause freezes ring expansion and damage");
                    arena.TogglePause();
                }
                while (target.Health == health) yield return null;
                Check(health - target.Health >= Mathf.RoundToInt(48 * 1.24f), "pursuit deals its own damage with the three existing stacks");
                if (realm == CultivationRealm.NascentSoul) CheckWaveSnapshot(arena);
                yield return Idle(player);
                Check(arena.Effects.ActiveWhirlwindCount == 0, "whirlwind radial damage expires after its lifetime");
                int expectedSpinDamage = Mathf.RoundToInt(48 * 1.24f);
                Check(rearHealth - rear.Health == expectedSpinDamage && sideHealth - side.Health == expectedSpinDamage,
                    "360-degree pursuit hits rear and side targets exactly once");
                Check(player.TotalSwordWaves == (realm == CultivationRealm.NascentSoul ? 1 : 0),
                    "only Nascent Soul emits exactly one sword tide for a multi-target pursuit");
                Check(player.ComboStacks == 5, "multi-target whirlwind adds two layers to the existing three, capped at five");
            }

            yield return ValidateWhirlwindTiming(arena);
            yield return ValidateSwordTide(arena);

            Place(arena, CultivationRealm.GoldenCore); yield return Frames();
            player = arena.Player; target = arena.Enemies[0];
            yield return FirstTwo(player);
            health = target.Health; player.RequestAttack(Vector2.right);
            while (target.Health == health) yield return null;
            swings = player.TotalSwings; player.RequestAttack(Vector2.right);
            while (player.TotalSwings == swings || arena.Effects.ActiveWhirlwindCount == 0) yield return null;
            Check(player.TryDash(Vector2.left) && arena.Effects.ActiveWhirlwindCount == 0,
                "dash interruption immediately cancels whirlwind radial damage");
            health = target.Health; yield return Wait(0.4f);
            Check(target.Health == health, "cancelled whirlwind cannot cause delayed damage");

            Place(arena, CultivationRealm.FoundationEstablishment); yield return Frames();
            player = arena.Player; target = arena.Enemies[0];
            player.RequestAttack(Vector2.right); yield return Idle(player);
            yield return Wait(player.comboResetTime - 0.15f);
            health = target.Health;
            Check(player.TryTechniqueSkill(Vector2.right), "Q consumes a near-expired combo");
            Check(player.ComboStacks == 0 && player.ComboStackTimeLeft == 0 && player.TechniqueConsumedStacks == 1,
                "Q immediately consumes the actual Qi Refining damage stack and its timer");
            yield return Idle(player);
            Check(health - target.Health == 45 && player.ComboStacks == 0, "one-layer Q scales its 42 base damage by 1.08 and never rebuilds consumed layers");
            Check(player.TechniqueCooldownLeft > 3 && !player.TryTechniqueSkill(Vector2.right), "Q enforces its four-second cooldown");
            Check(player.RequestAttack(Vector2.right) && player.ComboIndex == 0, "normal attacks restart after consuming the combo");
            yield return Idle(player);
            yield return ValidateConsumingRush(arena);
            Debug.Log("TECHNIQUE_PHASE: keyboard Q rush");

            Place(arena, CultivationRealm.QiRefining); yield return Frames(); player = arena.Player;
            foreach (var enemy in arena.Enemies) enemy.gameObject.SetActive(false);
            player.Teleport(ManorEnvironment.Ground(1000, 480)); yield return Frames();
            Vector2 origin = player.transform.position;
            Vector3 aimScreen = arena.WorldCamera.WorldToScreenPoint(arena.View.LiftPoint(origin + Vector2.right * 12, arena.View.WeaponHeight));
            pointer = new MouseState { position = new Vector2(aimScreen.x, aimScreen.y) }; yield return Frames();
            keys = new KeyboardState(Key.Q);
            yield return AwaitCast(arena);
            keys = new KeyboardState(); yield return Idle(player);
            Check(Mathf.Abs(Vector2.Distance(origin, player.transform.position) - player.FlowingCloud.RushDistance(0)) < 0.15f,
                "real Q input produces the tripled zero-stack rush distance");
            Check(player.ComboStacks == 0 && player.ComboIndex == -1, "missing Q breaks the combo");

            Place(arena, CultivationRealm.QiRefining); yield return Frames(); player = arena.Player;
            Rect table = ManorCollisionLayout.FurnitureFootprint("Tea table");
            player.Teleport(new Vector2(table.center.x, table.yMin - 0.75f)); yield return Frames();
            Check(arena.Codex.ShowPrompt, "tea table approach enables the centered F prompt");
            Capture("Technique-HUD.png");
            keys = new KeyboardState(Key.F);
            while (!arena.Codex.IsOpen) yield return null;
            Check(arena.Paused && !arena.PauseMenuOpen && arena.InputBlocked && !arena.CharacterPanel.IsOpen,
                "F opens the independent codex and pauses combat without the character or pause menus");
            Check(!arena.CharacterPanel.Open(), "the character panel cannot overlap the table codex");
            keys = new KeyboardState(Key.Tab); yield return Frames(3);
            Check(arena.Codex.IsOpen && !arena.CharacterPanel.IsOpen, "Tab cannot replace the table codex with the character panel");
            keys = new KeyboardState(Key.Q); yield return Frames();
            Capture("Technique-Codex.png");
            Check(player.TotalTechniqueCasts == 0 && !player.TryTechniqueSkill(Vector2.right), "Q is blocked while reading the codex");
            keys = new KeyboardState(Key.Escape, Key.Q);
            while (arena.Codex.IsOpen) yield return null;
            yield return Frames();
            Check(!arena.Paused && !arena.PauseMenuOpen && player.TotalTechniqueCasts == 0 && arena.InputBlocked,
                "Esc closes the codex without reopening pause or leaking held Q");
            keys = new KeyboardState(); yield return Frames();
            keys = new KeyboardState(Key.Q);
            yield return AwaitCast(arena);
            Check(player.TotalTechniqueCasts == 1, "a new Q press works after closing and releasing keys");
            keys = new KeyboardState(); yield return Idle(player);
            Check(ManorCollisionLayout.ContainsGround(player.transform.position), "skill rush respects the solid tea table");
            player.Teleport(ManorEnvironment.SpawnPoint); yield return Frames();
            Check(!arena.Codex.ShowPrompt && !arena.Codex.Open(), "codex prompt and interaction disappear away from the table");

            var tuning = arena.Tuning.Settings;
            tuning.flowingCloud.attackSpeedPerStack = 0.07f; tuning.flowingCloud.stackBonus = 0.12f; tuning.flowingCloud.maxStacks = 3;
            tuning.flowingCloud.pursuitDamage = 67; tuning.flowingCloud.activeCooldown = 2.5f;
            tuning.flowingCloud.rushDistance = 1.8f; tuning.flowingCloud.activeBaseDamage = 35;
            tuning.flowingCloud.activeDamagePerRealm = 9;
            tuning.flowingCloud.rushDistanceMultiplier = 2.5f; tuning.flowingCloud.rushTrailLifetime = 1.2f;
            tuning.flowingCloud.finisherStrike.damage = 113;
            tuning.flowingCloud.finisherStrike.reach = 2.7f;
            tuning.flowingCloud.finisherStrike.duration = 0.57f;
            arena.Tuning.ApplyAll();
            Check(player.FlowingCloud.pursuitDamage == 67 && player.FlowingCloud.activeCooldown == 2.5f
                && player.FlowingCloud.ActiveDamage(CultivationRealm.NascentSoul) == 62, "technique debug tuning applies to the live player");
            var restored = JsonUtility.FromJson<SlashTuningSettings>(JsonUtility.ToJson(tuning));
            restored.Validate(new SlashTuningSettings());
            Check(restored.flowingCloud.pursuitDamage == 67 && restored.flowingCloud.stackBonus == 0.12f
                && restored.flowingCloud.rushDistance == 1.8f && restored.flowingCloud.rushDistanceMultiplier == 2.5f
                && restored.flowingCloud.rushTrailLifetime == 1.2f && restored.flowingCloud.attackSpeedPerStack == 0.07f,
                "per-stack attack speed and Q settings survive save serialization");
            Check(restored.flowingCloud.finisherStrike.damage == 113 && restored.flowingCloud.finisherStrike.reach == 2.7f
                && restored.flowingCloud.finisherStrike.duration == 0.57f && restored.strikes[2].damage == 40,
                "independent finisher damage, reach and timing survive serialization without changing normal attacks");
            var oldFinisherSave = JsonUtility.FromJson<SlashTuningSettings>(File.ReadAllText("Verification/Artifacts/SavedTuning-2026-10-27.json"));
            oldFinisherSave.strikes[2].damage = 61; oldFinisherSave.strikes[2].reach = 2.8f;
            oldFinisherSave.Validate(new SlashTuningSettings());
            Check(oldFinisherSave.flowingCloud.finisherStrike.damage == 122 && oldFinisherSave.flowingCloud.finisherStrike.reach == 2.8f
                && oldFinisherSave.strikes[2].damage == 61, "old saves seed an independent finisher from their third strike once");
            var legacy = JsonUtility.FromJson<SlashTuningSettings>("{\"version\":3,\"moveSpeed\":7.2}");
            legacy.Validate(new SlashTuningSettings());
            Check(legacy.flowingCloud.attackSpeedPerStack == 0.05f && legacy.flowingCloud.pursuitDamage == 48
                && legacy.flowingCloud.activeCooldown == 6 && Mathf.Abs(legacy.moveSpeed - 7.2f) < 0.001f,
                "old tuning saves receive technique defaults without losing movement tuning");
            var oldActiveSave = JsonUtility.FromJson<SlashTuningSettings>("{\"version\":3,\"flowingCloud\":{\"rushDistance\":1.8,\"rushDistancePerStack\":0.3}}");
            oldActiveSave.Validate(new SlashTuningSettings());
            Check(Mathf.Abs(oldActiveSave.flowingCloud.RushDistance(5) - 7.92f) < 0.001f
                && oldActiveSave.flowingCloud.rushTrailLifetime == 0.8f, "old Q saves receive the synchronized distance multiplier and trail defaults");
            var oldWindSave = JsonUtility.FromJson<SlashTuningSettings>("{\"version\":3,\"flowingCloud\":{\"windWindow\":0.7,\"stackBonus\":0.12}}");
            oldWindSave.Validate(new SlashTuningSettings());
            Check(oldWindSave.flowingCloud.attackSpeedPerStack == 0.05f && oldWindSave.flowingCloud.stackBonus == 0.12f,
                "old continuation-window saves receive five percent speed without replacing damage tuning");
            tuning.flowingCloud = new SlashTuningSettings.FlowingCloudSettings();
            arena.Tuning.ApplyAll(); player.SetRealm(CultivationRealm.NascentSoul);
            player.ResetTechniqueCooldown();
            Check(player.TechniqueReady, "debug action refreshes Q cooldown");
            player.ResetTechniqueCombo();
            Check(player.ComboStacks == 0 && player.ComboIndex == -1, "debug action clears combo state");
            player.FillTechniqueStacksForDebug();
            Check(player.ComboStacks == 5, "active skill debug action fills existing passive layers to five");
            player.ResetTechniqueCombo();
            keys = new KeyboardState(Key.F1);
            while (!arena.Tuning.IsOpen) yield return null;
            keys = new KeyboardState(); yield return Frames();
            const BindingFlags panelFields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(SlashTuningPanel).GetField("page", panelFields).SetValue(arena.Tuning, 6);
            yield return Frames(); Capture("Technique-Tuning-Passive.png");
            typeof(SlashTuningPanel).GetField("techniquePage", panelFields).SetValue(arena.Tuning, 1);
            yield return Frames(); Capture("Technique-Tuning-Active.png");
            typeof(SlashTuningPanel).GetField("page", panelFields).SetValue(arena.Tuning, 7);
            typeof(SlashTuningPanel).GetField("scroll", panelFields).SetValue(arena.Tuning, Vector2.zero);
            yield return Frames(); Capture("CloudRush-Active-Debug.png");
            typeof(SlashTuningPanel).GetField("scroll", panelFields).SetValue(arena.Tuning, new Vector2(0, 920));
            yield return Frames(); Capture("CloudRush-Finisher-Debug.png");
            typeof(SlashTuningPanel).GetField("scroll", panelFields).SetValue(arena.Tuning, new Vector2(0, 1600));
            yield return Frames(); Capture("CloudRush-Active-Debug-Bottom.png");
            keys = new KeyboardState(Key.F1);
            while (arena.Tuning.IsOpen) yield return null;
            keys = new KeyboardState();
        }

        static IEnumerator ValidateMissedAttacks(SlashArena arena)
        {
            Place(arena, CultivationRealm.FoundationEstablishment); yield return Frames();
            var player = arena.Player;
            var hud = arena.GetComponent<SlashHUD>();
            yield return FirstTwo(player);
            var combo = (FlowingCloudCombo)typeof(SlashPlayer).GetField("techniqueCombo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            float deadline = combo.StackUntil;
            int health = arena.Enemies[0].Health;
            foreach (var enemy in arena.Enemies)
            {
                Vector2 away = (Vector2)player.transform.position + new Vector2(-6, 4);
                enemy.GetComponent<Rigidbody2D>().position = away; enemy.transform.position = away;
            }
            Physics2D.SyncTransforms();
            for (int miss = 0; miss < 3; miss++)
            {
                Check(player.RequestAttack(Vector2.right), "start a real missed attack with two live layers");
                yield return Idle(player);
                Check(player.ComboStacks == 2 && combo.StackUntil == deadline && !combo.StrikeHit,
                    "a real missed attack preserves layers and the last hit deadline without refreshing either");
                Check(hud.VisibleBuffs.Count == 1 && hud.VisibleBuffs[0].stacks == 2
                    && Mathf.Abs(player.NormalAttackSpeedMultiplier - 1.1f) < 0.001f,
                    "missed attacks retain the two-layer buff cell and its Foundation speed bonus");
            }
            Check(arena.Enemies[0].Health == health && player.TotalPursuits == 0,
                "misses grant no damage or whirlwind follow-up while preserving layers");
            Capture("MissedAttack-Preserved-Buff.png");
            yield return Wait(Mathf.Max(0, deadline - Time.time) + 0.05f);
            Check(player.ComboStacks == 0 && hud.VisibleBuffs.Count == 0,
                "layers and the buff cell expire at the original deadline despite repeated missed attacks");
        }

        static IEnumerator ClickCharacterPanel(Vector2 position)
        {
            var queueEvent = typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Check(queueEvent != null, "Game view supports native IMGUI mouse event injection");
            pointer = new MouseState { position = new Vector2(position.x, Screen.height - position.y) }.WithButton(MouseButton.Left);
            queueEvent.Invoke(null, new object[] { new Event { type = EventType.MouseDown, button = 0, mousePosition = position } });
            yield return Frames(3);
            pointer = new MouseState { position = new Vector2(position.x, Screen.height - position.y) };
            queueEvent.Invoke(null, new object[] { new Event { type = EventType.MouseUp, button = 0, mousePosition = position } });
            yield return Frames(3);
        }

        static IEnumerator ValidateCharacterPanel(SlashArena arena)
        {
            Place(arena, CultivationRealm.QiRefining); yield return Frames();
            var player = arena.Player;
            player.Teleport(ManorEnvironment.SpawnPoint); yield return Frames();
            Check(!arena.Codex.NearTable, "Tab test starts away from the codex table");
            keys = new KeyboardState(Key.Tab); yield return Frames(3);
            Check(arena.CharacterPanel.IsOpen && !arena.Codex.IsOpen && arena.CharacterPanel.ActivePage == CharacterPanelPage.Techniques
                && arena.Paused && !arena.PauseMenuOpen && arena.InputBlocked && Time.timeScale == 0,
                "Tab opens the Techniques page anywhere and pauses combat without the pause menu");
            keys = new KeyboardState(Key.W, Key.Q); yield return Frames(3);
            Check(Vector2.Distance(player.transform.position, ManorEnvironment.SpawnPoint) < 0.001f
                && player.TotalTechniqueCasts == 0, "character panel blocks movement and Q");
            Capture("CharacterPanel-Techniques.png");
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            Vector2 panelOrigin = new Vector2(Screen.width / scale / 2 - 540, Screen.height / scale / 2 - 360);
            int swings = player.TotalSwings;
            for (int i = 1; i <= 2; i++)
            {
                yield return ClickCharacterPanel((panelOrigin + new Vector2(118 + i * 192, 45)) * scale);
                Check(arena.CharacterPanel.ActivePage == (CharacterPanelPage)i && arena.Paused && player.TotalSwings == swings,
                    "native top button switches to " + (CharacterPanelPage)i + " without leaking an attack");
                Capture(i == 1 ? "CharacterPanel-Attributes.png" : "CharacterPanel-Items.png");
            }
            yield return ClickCharacterPanel((panelOrigin + new Vector2(118, 45)) * scale);
            Check(arena.CharacterPanel.ActivePage == CharacterPanelPage.Techniques, "native top button switches back to Techniques");
            keys = new KeyboardState(Key.Tab, Key.Q); yield return Frames(3);
            Check(!arena.CharacterPanel.IsOpen && !arena.Paused && !arena.PauseMenuOpen && arena.InputBlocked
                && player.TotalTechniqueCasts == 0, "Tab closes the panel while suppressing held combat input");
            keys = new KeyboardState(); yield return Frames(3);
            keys = new KeyboardState(Key.Tab); yield return Frames(3);
            Check(arena.CharacterPanel.IsOpen && arena.CharacterPanel.ActivePage == CharacterPanelPage.Techniques, "reopening defaults to Techniques");
            keys = new KeyboardState(); yield return Frames(2);
            keys = new KeyboardState(Key.Escape); yield return Frames(3);
            Check(!arena.CharacterPanel.IsOpen && !arena.PauseMenuOpen && !arena.Paused, "Esc closes the panel without opening Pause");
            keys = new KeyboardState(); yield return Frames(2);
            arena.TogglePause();
            keys = new KeyboardState(Key.Tab); yield return Frames(3);
            Check(!arena.CharacterPanel.IsOpen && arena.PauseMenuOpen, "Tab cannot overlap the existing Pause menu");
            keys = new KeyboardState(); arena.TogglePause(); yield return Frames(3);
            Check(arena.CharacterPanel.Open(), "character panel opens again after pause resumes");
            yield return Frames(3);
            yield return ClickCharacterPanel((panelOrigin + new Vector2(960, 45)) * scale);
            Check(!arena.CharacterPanel.IsOpen && !arena.Paused && player.TotalSwings == swings, "native close button resumes without an attack");
            yield return Frames(3);
            Rect table = ManorCollisionLayout.FurnitureFootprint("Tea table");
            player.Teleport(new Vector2(table.center.x, table.yMin - 0.75f)); yield return Frames(3);
            keys = new KeyboardState(Key.Tab); yield return Frames(3);
            Check(arena.CharacterPanel.IsOpen && !arena.Codex.IsOpen, "Tab by the table still opens only the character panel");
            keys = new KeyboardState(Key.F); yield return Frames(3);
            Check(arena.CharacterPanel.IsOpen && !arena.Codex.IsOpen, "F cannot replace or close the independent character panel");
            keys = new KeyboardState(Key.Escape); yield return Frames(3);
            keys = new KeyboardState(); yield return Frames(3);
            keys = new KeyboardState(Key.F); yield return Frames(3);
            Check(arena.Codex.IsOpen && !arena.CharacterPanel.IsOpen, "F by the table opens the original codex after character panel closes");
            Capture("Technique-Codex-Restored.png");
            keys = new KeyboardState(); yield return Frames(2);
            keys = new KeyboardState(Key.F); yield return Frames(3);
            Check(!arena.Codex.IsOpen && !arena.CharacterPanel.IsOpen && !arena.Paused, "F closes only the table codex");
            keys = new KeyboardState();
            yield return Frames(3);
        }

        static IEnumerator ValidateBuffBar(SlashArena arena)
        {
            Place(arena, CultivationRealm.QiRefining); yield return Frames();
            var player = arena.Player;
            var hud = arena.GetComponent<SlashHUD>();
            Check(hud.VisibleBuffs.Count == 0, "buff bar is empty when no buff is active");
            player.RequestAttack(Vector2.right); yield return Idle(player);
            Check(hud.VisibleBuffs.Count == 1 && hud.VisibleBuffs[0].id == "flowing-cloud-stacks"
                && hud.VisibleBuffs[0].name == "叠浪" && hud.VisibleBuffs[0].stacks == 1,
                "a real hit shows one Flowing Cloud buff cell with its current layer count");
            Capture("BuffBar-One-Stack.png");
            var buff = hud.VisibleBuffs[0];
            var icon = Resources.Load<Texture2D>(buff.iconResource);
            Check(buff.iconResource == "SlashDemo/FlowingCloudBuffIcon" && icon != null && icon.width == icon.height,
                "buff cell loads the user's supplied square sword-and-cloud artwork");
            Check(buff.description == "每层叠浪使普攻伤害提升8%，叠满5层时，额外提升20%的普攻伤害",
                "Qi tooltip contains only the requested damage effects without timer text");
            Check(Mathf.Abs(buff.timeLeft - player.ComboStackTimeLeft) < 0.001f && buff.duration == FlowingCloudRules.StackDuration,
                "icon countdown receives the actual buff lifetime");
            using (var mask = new BuffCountdownMask())
            {
                mask.Update(1);
                Check(mask.Texture.GetPixel(24, 72).a == 0 && mask.Texture.GetPixel(72, 72).a == 0,
                    "a refreshed countdown starts with the entire icon unswept");
                mask.Update(0.75f);
                Check(mask.Texture.GetPixel(24, 72).a > 0.6f && mask.Texture.GetPixel(72, 72).a == 0
                    && mask.Texture.GetPixel(24, 24).a == 0,
                    "first quarter sweeps counterclockwise from twelve to nine and leaves remaining time bright");
                mask.Update(0.5f);
                Check(mask.Texture.GetPixel(24, 72).a > 0.6f && mask.Texture.GetPixel(24, 24).a > 0.6f
                    && mask.Texture.GetPixel(72, 24).a == 0,
                    "halfway countdown shades the left half and preserves the right half");
                mask.Update(0);
                Check(mask.Texture.GetPixel(72, 72).a > 0.6f, "expiry sweeps the final quadrant");
                mask.Update(1);
                Check(mask.Texture.GetPixel(72, 72).a == 0 && mask.Texture.GetPixel(24, 24).a == 0,
                    "a new hit clears the previous sweep");
            }
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            Vector2 center = BuffBarLayout.Slot(0, new Rect(50, 141, 282, 0)).center;
            pointer = new MouseState { position = new Vector2(center.x * scale, Screen.height - center.y * scale) };
            yield return Frames(3); Capture("BuffIcon-Hover-One.png");
            Check(hud.HoveredBuffId == "flowing-cloud-stacks", "moving the real pointer over the buff opens its effect tooltip");
            pointer = new MouseState { position = new Vector2(500 * scale, Screen.height - 400 * scale) };
            yield return Frames(2); Capture("BuffIcon-NoHover.png");
            Check(hud.HoveredBuffId == null, "moving the pointer away hides the buff tooltip");
            foreach (var realm in new[] { CultivationRealm.FoundationEstablishment, CultivationRealm.GoldenCore, CultivationRealm.NascentSoul })
            {
                player.SetRealm(realm); player.FillTechniqueStacksForDebug();
                Check(hud.VisibleBuffs[0].description == "每层叠浪使普攻伤害提升8%、普攻攻速提升5%，叠满5层时，额外提升20%的普攻伤害",
                    "Foundation and higher realms show the requested damage and attack-speed sentence");
            }
            player.SetRealm(CultivationRealm.FoundationEstablishment); player.FillTechniqueStacksForDebug();
            pointer = new MouseState { position = new Vector2(center.x * scale, Screen.height - center.y * scale) };
            yield return Wait(0.45f); Capture("BuffCountdown-Quarter.png");
            float quarterRemaining = hud.VisibleBuffs[0].RemainingFraction;
            yield return Wait(0.5f); Capture("BuffCountdown-Half.png");
            Check(hud.VisibleBuffs[0].RemainingFraction < quarterRemaining, "visible countdown advances with the existing buff clock");
            yield return Wait(0.5f); Capture("BuffCountdown-ThreeQuarters.png");
            player.SetRealm(CultivationRealm.QiRefining);
            player.FillTechniqueStacksForDebug(); yield return Frames(2);
            Check(hud.VisibleBuffs.Count == 1 && hud.VisibleBuffs[0].stacks == 5,
                "five layers update the same buff cell instead of creating five cells");
            Capture("BuffBar-Five-Stacks.png");
            pointer = new MouseState { position = new Vector2(center.x * scale, Screen.height - center.y * scale) };
            yield return Frames(2); Capture("BuffIcon-Hover-Five.png");
            Check(hud.HoveredBuffId == "flowing-cloud-stacks" && Mathf.Abs(player.ComboDamageBonus - 0.6f) < 0.001f,
                "five-stack badge and hover match the new sixty-percent total damage bonus");
            Check(player.TryDash(Vector2.left) && hud.VisibleBuffs.Count == 1 && hud.VisibleBuffs[0].stacks == 3,
                "dodge immediately updates the visible buff from five to three layers");
            yield return Idle(player); Capture("BuffBar-Three-Stacks.png");
            Check(player.TryTechniqueSkill(Vector2.right) && hud.VisibleBuffs.Count == 0,
                "Q consuming all layers immediately removes the buff cell");
            yield return Idle(player);
            Check(hud.HoveredBuffId == null, "consuming the buff also dismisses its hover tooltip");
            player.FillTechniqueStacksForDebug();
            Check(player.TakeDamage(1, Vector2.up) && hud.VisibleBuffs.Count == 0, "incoming damage clears the visible buff with its layers");
            player.FillTechniqueStacksForDebug();
            float timeLeft = player.ComboStackTimeLeft;
            arena.TogglePause(); yield return Frames();
            Check(hud.VisibleBuffs.Count == 1 && hud.VisibleBuffs[0].stacks == 5 && player.ComboStackTimeLeft == timeLeft,
                "pause preserves the visible buff and freezes its lifetime");
            Check(hud.VisibleBuffs[0].timeLeft == timeLeft, "paused icon countdown freezes with the gameplay timer");
            arena.TogglePause(); yield return Wait(2.05f);
            Check(hud.VisibleBuffs.Count == 0, "expired layers cannot leave a stale buff cell");
            player.FillTechniqueStacksForDebug(); player.Teleport(ManorEnvironment.TrainingCenter);
            Check(hud.VisibleBuffs.Count == 0, "teleport removes buff cells whose layers were cleared");
            player.FillTechniqueStacksForDebug();
            Check(player.TakeDamage(player.maxHealth, Vector2.up) && !player.IsAlive && hud.VisibleBuffs.Count == 0,
                "a defeated player cannot retain a visible active buff");
        }

        static IEnumerator ValidateRebuildCooldown(SlashArena arena)
        {
            foreach (var realm in new[] { CultivationRealm.QiRefining, CultivationRealm.FoundationEstablishment })
            {
                Place(arena, realm); yield return Frames();
                var player = arena.Player;
                bool unlocked = realm >= CultivationRealm.FoundationEstablishment;
                Check(!player.RebuildCooldownRewardPending, "full-stack refund starts inactive before any Q cast");
                Check(player.TryTechniqueSkill(Vector2.left) && !player.RebuildCooldownRewardPending,
                    "starting Q cannot arm the refund before the skill has finished");
                yield return Idle(player);
                Check(player.RebuildCooldownRewardPending == unlocked, "only a completed Foundation Q arms the one-time rebuild refund");
                player.GetComponent<Rigidbody2D>().position = ManorEnvironment.TrainingCenter;
                player.transform.position = ManorEnvironment.TrainingCenter; Physics2D.SyncTransforms();
                float cooldownDeadline = Time.time + player.TechniqueCooldownLeft;
                for (int hit = 1; hit <= 5; hit++)
                {
                    Check(player.RequestAttack(Vector2.right), "rebuild layers with a real normal hit after Q");
                    yield return Idle(player);
                    Check(player.ComboStacks == hit, "normal hits rebuild one layer at a time after Q");
                    float expectedDeadline = cooldownDeadline - (unlocked && hit == 5 ? 1 : 0);
                    Check(Mathf.Abs(Time.time + player.TechniqueCooldownLeft - expectedDeadline) < 0.06f,
                        "only the first full rebuild shortens Q cooldown by one second");
                }
                Check(!player.RebuildCooldownRewardPending, "full rebuild consumes the refund opportunity");
                float refundedDeadline = Time.time + player.TechniqueCooldownLeft;
                player.ResetTechniqueCombo();
                for (int hit = 0; hit < 5; hit++) { player.RequestAttack(Vector2.right); yield return Idle(player); }
                Check(player.ComboStacks == 5 && Mathf.Abs(Time.time + player.TechniqueCooldownLeft - refundedDeadline) < 0.06f,
                    "losing and rebuilding all layers cannot refund twice for the same cast");
                player.ResetTechniqueCooldown();
                Check(player.TryTechniqueSkill(Vector2.left) && player.TryDash(Vector2.up), "interrupt a new Q before its release finishes");
                yield return Idle(player);
                Check(!player.RebuildCooldownRewardPending, "interrupted Q cannot arm the full-stack refund");
                player.ResetTechniqueCooldown();
                Check(player.TryTechniqueSkill(Vector2.left), "the next Q can start a fresh reward cycle");
                yield return Idle(player);
                Check(player.RebuildCooldownRewardPending == unlocked, "each newly completed Q rearms its own refund");
                var cooldown = typeof(SlashPlayer).GetField("techniqueCooldownLeft", BindingFlags.Instance | BindingFlags.NonPublic);
                cooldown.SetValue(player, 0.25f); player.FillTechniqueStacksForDebug();
                Check(Mathf.Abs(player.TechniqueCooldownLeft - (unlocked ? 0 : 0.25f)) < 0.001f,
                    "debug full-stack rebuild obeys the unlock and never makes cooldown negative");
            }
            Place(arena, CultivationRealm.NascentSoul); yield return Frames();
            var owner = arena.Player;
            owner.TryTechniqueSkill(Vector2.left); yield return Idle(owner);
            owner.GetComponent<Rigidbody2D>().position = ManorEnvironment.TrainingCenter;
            owner.transform.position = ManorEnvironment.TrainingCenter;
            var state = (FlowingCloudCombo)typeof(SlashPlayer).GetField("techniqueCombo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
            for (int i = 0; i < 4; i++) { state.BeginStrike(); state.RegisterHit(0, Time.time, owner.Realm); }
            state.ResetSequence(); Physics2D.SyncTransforms();
            typeof(SlashPlayer).GetField("techniqueCooldownLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, 3f);
            float began = Time.time; int health = arena.Enemies[0].Health;
            arena.Effects.LaunchSwordWave((Vector2)owner.transform.position + Vector2.right * 0.4f, Vector2.right, owner.heavyAttack, owner);
            while (arena.Enemies[0].Health == health) yield return null;
            Check(owner.ComboStacks == 5 && !owner.RebuildCooldownRewardPending
                && Mathf.Abs(owner.TechniqueCooldownLeft - (3 - (Time.time - began) - 2)) < 0.06f,
                "sword tide reaching full grants its own one-second refund plus the one-time Foundation refund");
            arena.Effects.ClearSwordWaves();
        }

        static IEnumerator ValidateWhirlwindTiming(SlashArena arena)
        {
            Place(arena, CultivationRealm.GoldenCore); yield return Frames();
            var player = arena.Player;
            yield return FirstTwo(player);
            player.RequestAttack(Vector2.right); yield return Idle(player);
            int damage = Mathf.RoundToInt(player.FlowingCloud.pursuitDamage * (1 + player.ComboDamageBonus));
            Check(player.RequestAttack(Vector2.right) && player.IsPursuit, "timing check starts a real fourth-strike whirlwind");
            Vector2 origin = player.transform.position;
            var near = arena.Enemies[0]; var edge = arena.Enemies[1]; var late = arena.Enemies[2];
            Vector2[] positions = { origin + Vector2.left * 0.7f,
                origin + Vector2.up * (player.FlowingCloud.pursuitReach - 0.05f),
                origin + Vector2.right * (player.FlowingCloud.pursuitReach + 1) };
            for (int i = 0; i < 3; i++)
            {
                arena.Enemies[i].GetComponent<Rigidbody2D>().position = positions[i];
                arena.Enemies[i].transform.position = positions[i];
            }
            Physics2D.SyncTransforms();
            int nearHealth = near.Health, edgeHealth = edge.Health, lateHealth = late.Health;
            yield return Frames(1);
            Check(near.Health == nearHealth && edge.Health == edgeHealth, "whirlwind windup cannot damage enemies before the sword light");
            while (arena.Effects.ActiveWhirlwindCount == 0) yield return null;
            Check(nearHealth - near.Health == damage && edgeHealth - edge.Health == damage && late.Health == lateHealth,
                "one emitted sword light damages near and edge enemies together and excludes outside targets");
            bool nearNumber = false, edgeNumber = false;
            foreach (var number in arena.Effects.Texts)
            {
                if (number.text != damage.ToString()) continue;
                nearNumber |= Vector2.Distance(number.position, near.transform.position) < 0.01f;
                edgeNumber |= Vector2.Distance(number.position, edge.transform.position) < 0.01f;
            }
            Check(nearNumber && edgeNumber, "near and edge damage numbers are created together with the emitted sword light");
            late.GetComponent<Rigidbody2D>().position = origin + Vector2.right * 1.2f;
            late.transform.position = origin + Vector2.right * 1.2f;
            Physics2D.SyncTransforms();
            yield return Wait(0.12f);
            Check(late.Health == lateHealth && nearHealth - near.Health == damage && edgeHealth - edge.Health == damage,
                "fading sword light cannot deal delayed damage to new arrivals or repeat the original hit");
            yield return Idle(player);
        }

        static void CheckSavedDefaults(object actual, object expected, string path)
        {
            if (expected is float number)
            { Check(actual is float value && Mathf.Abs(value - number) < 0.00001f, path + " matches saved tuning"); return; }
            if (expected.GetType().IsPrimitive || expected.GetType().IsEnum)
            { Check(Equals(actual, expected), path + " matches saved tuning"); return; }
            if (expected is Array array)
            {
                var actualArray = actual as Array;
                Check(actualArray != null && actualArray.Length == array.Length, path + " length matches saved tuning");
                for (int i = 0; i < array.Length; i++) CheckSavedDefaults(actualArray.GetValue(i), array.GetValue(i), path + "[" + i + "]");
                return;
            }
            foreach (var field in expected.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                CheckSavedDefaults(field.GetValue(actual), field.GetValue(expected), path + "." + field.Name);
        }

        static IEnumerator ValidateDodgeStackPenalty(SlashArena arena)
        {
            Place(arena, CultivationRealm.FoundationEstablishment); yield return Frames();
            var player = arena.Player;
            player.FillTechniqueStacksForDebug();
            var combo = (FlowingCloudCombo)typeof(SlashPlayer).GetField("techniqueCombo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            float deadline = combo.StackUntil;
            Check(player.TryDash(Vector2.left) && player.ComboStacks == 3 && player.ComboIndex == -1,
                "successful dodge subtracts two from five layers and resets the normal sequence");
            Check(Mathf.Abs(player.ComboDamageBonus - 0.24f) < 0.001f && Mathf.Abs(player.NormalAttackSpeedMultiplier - 1.15f) < 0.001f,
                "three surviving layers retain twenty-four percent damage and fifteen percent normal speed");
            Check(combo.StackUntil == deadline, "dodge does not refresh the surviving stack timer");
            Check(!player.TryDash(Vector2.up) && player.ComboStacks == 3 && combo.StackUntil == deadline,
                "a rejected repeated dodge cannot spend additional layers");
            while (player.IsDashing || player.DashReady < 1) yield return null;
            Check(player.ComboStacks == 3 && player.TryDash(Vector2.left) && player.ComboStacks == 1,
                "a second successful dodge subtracts two more from surviving layers");
            while (player.IsDashing || player.DashReady < 1) yield return null;
            Check(player.ComboStacks == 1 && player.TryDash(Vector2.left) && player.ComboStacks == 0,
                "a third dodge clamps the last remaining layer at zero");
            yield return Idle(player);
        }

        static IEnumerator ValidateSwordTide(SlashArena arena)
        {
            Place(arena, CultivationRealm.NascentSoul); yield return Frames();
            var player = arena.Player;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var cooldown = typeof(SlashPlayer).GetField("techniqueCooldownLeft", flags);
            var combo = (FlowingCloudCombo)typeof(SlashPlayer).GetField("techniqueCombo", flags).GetValue(player);
            for (int i = 0; i < arena.Enemies.Count; i++)
            {
                Vector2 position = (Vector2)player.transform.position + Vector2.right * (1.2f + i * 0.7f);
                arena.Enemies[i].GetComponent<Rigidbody2D>().position = position; arena.Enemies[i].transform.position = position;
            }
            Physics2D.SyncTransforms();
            var settings = player.heavyAttack.Clone();
            var last = arena.Enemies[2]; int health = last.Health;
            cooldown.SetValue(player, 2.5f); float started = Time.time;
            arena.Effects.LaunchSwordWave((Vector2)player.transform.position + Vector2.right * 0.4f, Vector2.right, settings, player);
            while (last.Health == health) yield return null;
            Check(player.ComboStacks == 1 && !combo.StrikeHit, "one sword tide hitting three enemies adds one layer without confirming a sword strike");
            Check(Mathf.Abs(player.TechniqueCooldownLeft - (2.5f - (Time.time - started) - 1)) < 0.06f,
                "one sword tide reduces Q cooldown by exactly one second for multiple targets");
            Check(health - last.Health == settings.damage, "sword tide hits each target once using its configured damage snapshot");
            arena.Effects.ClearSwordWaves(); player.ResetTechniqueCombo();
            health = last.Health; cooldown.SetValue(player, 2.5f); started = Time.time;
            arena.Effects.LaunchSwordWave((Vector2)player.transform.position + Vector2.right * 0.4f, Vector2.right, settings);
            while (last.Health == health) yield return null;
            Check(player.ComboStacks == 0 && Mathf.Abs(player.TechniqueCooldownLeft - (2.5f - (Time.time - started))) < 0.06f,
                "charged heavy sword waves retain their behavior without granting stacks or reducing Q cooldown");
            arena.Effects.ClearSwordWaves();
            cooldown.SetValue(player, 2.5f); started = Time.time;
            arena.Effects.LaunchSwordWave((Vector2)player.transform.position + Vector2.left * 5, Vector2.up, settings, player);
            yield return Wait(0.7f);
            Check(player.ComboStacks == 0 && Mathf.Abs(player.TechniqueCooldownLeft - (2.5f - (Time.time - started))) < 0.06f,
                "a missed sword tide cannot grant layers or reduce Q cooldown");
            arena.Effects.ClearSwordWaves();
            cooldown.SetValue(player, 0.25f);
            health = arena.Enemies[0].Health;
            arena.Effects.LaunchSwordWave((Vector2)player.transform.position + Vector2.right * 0.4f, Vector2.right, settings, player);
            while (arena.Enemies[0].Health == health) yield return null;
            Check(player.TechniqueCooldownLeft == 0 && player.ComboStacks == 1, "sword tide cooldown reduction clamps at zero");
            arena.Effects.ClearSwordWaves();
        }

        static IEnumerator ValidateAttackSpeed(SlashArena arena)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var currentStrike = typeof(SlashPlayer).GetProperty("CurrentStrike", fields);
            foreach (CultivationRealm realm in new[] { CultivationRealm.QiRefining, CultivationRealm.FoundationEstablishment })
            {
                Place(arena, realm); yield return Frames();
                var player = arena.Player;
                Check(player.NormalAttackSpeedMultiplier == 1, "zero layers grant no attack speed at either realm");
                player.FillTechniqueStacksForDebug();
                float speed = realm >= CultivationRealm.FoundationEstablishment ? 1.25f : 1;
                Check(Mathf.Abs(player.NormalAttackSpeedMultiplier - speed) < 0.001f,
                    "full-stack normal attack speed is twenty-five percent only after Foundation unlock");
                for (int segment = 0; segment < 3; segment++)
                {
                    float baseDuration = player.strikes[segment].duration;
                    Check(player.RequestAttack(Vector2.right) && player.ComboIndex == segment, "speed buff preserves normal segment order");
                    var active = (SlashPlayer.Strike)currentStrike.GetValue(player);
                    Check(Mathf.Abs(active.duration - baseDuration / speed) < 0.0001f
                        && player.strikes[segment].duration == baseDuration, "normal swing duration divides by speed without changing base tuning");
                    yield return Idle(player);
                }
                player.ResetTechniqueCombo();
                Check(player.NormalAttackSpeedMultiplier == 1, "clearing layers clears the speed bonus");
            }
            Place(arena, CultivationRealm.FoundationEstablishment); yield return Frames();
            var foundation = arena.Player;
            foundation.FillTechniqueStacksForDebug(); yield return Wait(2.05f);
            Check(foundation.ComboStacks == 0 && foundation.NormalAttackSpeedMultiplier == 1, "expired layers remove Foundation speed in the real scene");
            foundation.FillTechniqueStacksForDebug();
            Check(foundation.RequestHeavyAttack() && foundation.NormalAttackSpeedMultiplier == 1,
                "heavy attack clears normal speed along with combo layers");
            foundation.CancelHeavyAttack();
            foundation.FillTechniqueStacksForDebug();
            Check(foundation.TryTechniqueSkill(Vector2.right) && foundation.NormalAttackSpeedMultiplier == 1,
                "Q consumes attack speed together with all five damage stacks");
            while (!foundation.IsTechniqueFinisher) yield return null;
            Check(Mathf.Abs(((SlashPlayer.Strike)currentStrike.GetValue(foundation)).duration - foundation.FlowingCloud.finisherStrike.duration) < 0.0001f,
                "Q finisher retains independent base timing without the consumed speed bonus");
            yield return Idle(foundation);
            Check(foundation.ComboStacks == 0 && foundation.NormalAttackSpeedMultiplier == 1,
                "Q contacts cannot recreate attack speed or passive layers");
        }

        static IEnumerator ValidateConsumingRush(SlashArena arena)
        {
            foreach (int stacks in new[] { 0, 1, 4, 5 })
            {
                Place(arena, CultivationRealm.QiRefining); yield return Frames();
                var player = arena.Player;
                for (int i = 0; i < stacks; i++)
                {
                    Check(player.RequestAttack(Vector2.right), "build a real stack before consuming rush");
                    yield return Idle(player);
                }
                Check(player.ComboStacks == stacks, "rush uses existing passive layers");
                Vector2 clearLane = ManorEnvironment.Ground(1280, 480);
                player.GetComponent<Rigidbody2D>().position = clearLane; player.transform.position = clearLane;
                Physics2D.SyncTransforms(); yield return Frames(1);
                Vector2 origin = player.transform.position;
                float distance = player.FlowingCloud.RushDistance(stacks);
                float damageMultiplier = 1 + stacks * player.FlowingCloud.stackBonus + (stacks == 5 ? 0.2f : 0);
                int damage = Mathf.RoundToInt(player.FlowingCloud.ActiveDamage(player.Realm, stacks) * damageMultiplier);
                var finisherSnapshot = player.FlowingCloud.finisherStrike.Clone();
                finisherSnapshot.damage = Mathf.RoundToInt(finisherSnapshot.damage * damageMultiplier);
                if (stacks == 5)
                {
                    player.strikes[2].damage = 13; player.strikes[2].reach = 1;
                    player.strikes[2].arc = 60; player.strikes[2].duration = 0.8f;
                }
                var first = arena.Enemies[0]; var end = arena.Enemies[1]; var side = arena.Enemies[2];
                Vector2[] positions = { origin + Vector2.left * 0.8f,
                    origin + Vector2.left * (distance + 0.75f),
                    origin + Vector2.left * 1.35f + Vector2.up * (player.FlowingCloud.activeWidth * 0.5f + side.Radius + 0.12f) };
                for (int i = 0; i < 3; i++)
                {
                    var body = arena.Enemies[i].GetComponent<Rigidbody2D>();
                    body.position = positions[i]; arena.Enemies[i].transform.position = positions[i];
                }
                Physics2D.SyncTransforms();
                int firstHealth = first.Health, endHealth = end.Health, sideHealth = side.Health;
                int finishers = player.TotalTechniqueFinishers;
                float previousFixedStep = Time.fixedDeltaTime;
                if (stacks == 1) Time.fixedDeltaTime = 0.08f;
                Check(player.TryTechniqueSkill(Vector2.left) && player.ComboStacks == 0
                    && player.TechniqueConsumedStacks == stacks, "accepted rush consumes all layers atomically");
                var rushStrike = (SlashPlayer.Strike)typeof(SlashPlayer).GetProperty("CurrentStrike", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                Check(rushStrike.damage == damage, "rush snapshots consumed-stack normal damage including the five-layer bonus before clearing stacks");
                Check(!player.TryTechniqueSkill(Vector2.up) && player.TechniqueConsumedStacks == stacks,
                    "a repeated Q cannot alter a rush already in flight");
                if (stacks == 5)
                {
                    player.FlowingCloud.stackBonus = 0.25f;
                    player.FlowingCloud.finisherStrike.damage = 147;
                    player.FlowingCloud.finisherStrike.reach = 0.7f;
                    player.FlowingCloud.finisherStrike.duration = 1.1f;
                }
                float until = Time.time + 0.06f;
                while (Time.time < until) yield return null;
                Check(arena.Effects.ActiveCloudLightCount > 0, "rush renders ice-blue sword light");
                Capture("CloudRush-" + stacks + "-Stacks.png");
                if (stacks == 4)
                {
                    Vector2 pausedAt = player.transform.position;
                    int lightCount = arena.Effects.ActiveCloudLightCount;
                    arena.TogglePause(); yield return Frames(8);
                    Check(Vector2.Distance(pausedAt, player.transform.position) < 0.001f
                        && arena.Effects.ActiveCloudLightCount == lightCount, "pause freezes rush displacement and sword-light lifetime");
                    arena.TogglePause();
                }
                if (stacks == 5)
                {
                    while (!player.IsTechniqueFinisher) yield return null;
                    var strike = (SlashPlayer.Strike)typeof(SlashPlayer).GetProperty("CurrentStrike", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                    Check(JsonUtility.ToJson(strike) == JsonUtility.ToJson(finisherSnapshot),
                        "five-stack Q uses independent finisher data snapshotted at cast, unaffected by either tuning change");
                    Check(end.Health == endHealth - damage && player.ComboStacks == 0,
                        "third-strike windup cannot deal damage early or grant passive layers");
                    yield return Wait(strike.duration * (SwordMotion.SwingStart(strike) + strike.activeRatio * 0.6f));
                    Capture("CloudRush-FiveStack-ThirdStrike.png");
                }
                yield return Idle(player);
                Time.fixedDeltaTime = previousFixedStep;
                if (stacks == 5) Capture("CloudRush-FiveStack-Landing.png");
                Check(Mathf.Abs(Vector2.Distance(origin, player.transform.position) - distance) < 0.15f,
                    "stack-scaled rush reaches the configured physics distance: expected " + distance
                    + ", actual " + Vector2.Distance(origin, player.transform.position));
                Check(firstHealth - first.Health == damage, "start of path receives damage exactly once");
                Check(endHealth - end.Health == damage + (stacks == 5 ? finisherSnapshot.damage : 0),
                    "endpoint takes path damage once and only five stacks add independent finisher damage");
                Check(sideHealth - side.Health == (stacks >= 4 ? damage : 0), "wider stacked sword light matches path collision");
                Check(player.TotalTechniqueFinishers - finishers == (stacks == 5 ? 1 : 0)
                    && player.ComboStacks == 0, "finisher occurs exactly once and neither strike grants stacks");
                if (stacks == 0)
                {
                    Check(arena.Effects.ActiveCloudTrailCount > 0, "world-fixed sword light remains after the rush finishes");
                    yield return Wait(0.15f); Capture("CloudRush-Lingering-Path.png");
                    Check(arena.Effects.ActiveCloudTrailCount > 0, "the path gradually fades beyond ordinary attack trail lifetime");
                }
                yield return Wait(player.FlowingCloud.rushTrailLifetime + 0.1f);
                Check(arena.Effects.ActiveCloudLightCount == 0, "all rush and landing visuals clean up after fading");
            }
            foreach (bool dashCancel in new[] { false, true })
            {
                Place(arena, CultivationRealm.QiRefining); yield return Frames();
                var player = arena.Player;
                for (int i = 0; i < 5; i++) { player.RequestAttack(Vector2.right); yield return Idle(player); }
                Check(player.ComboStacks == 5 && player.TryTechniqueSkill(Vector2.left), "start full-stack rush for interruption check");
                int finishers = player.TotalTechniqueFinishers;
                Check(dashCancel ? player.TryDash(Vector2.up) : player.TakeDamage(1, Vector2.up), "rush accepts existing dash and damage interruption");
                yield return Wait(0.4f);
                Check(!player.IsTechniqueActive && player.ComboStacks == 0 && player.TotalTechniqueFinishers == finishers,
                    "interrupted full-stack rush cannot produce a delayed endpoint strike or refund layers");
                var playerCollider = player.GetComponent<CircleCollider2D>();
                foreach (var enemy in arena.Enemies)
                    Check(!Physics2D.GetIgnoreCollision(playerCollider, enemy.GetComponent<CircleCollider2D>()),
                        "interruption restores player/enemy solid collision");
            }
            Place(arena, CultivationRealm.QiRefining); yield return Frames();
            var blockedPlayer = arena.Player;
            for (int i = 0; i < 5; i++) { blockedPlayer.RequestAttack(Vector2.right); yield return Idle(blockedPlayer); }
            // Move with the rigidbody to retain the confirmed stacks for this collision setup.
            Rect table = ManorCollisionLayout.FurnitureFootprint("Tea table");
            Vector2 start = new Vector2(table.center.x, table.yMin - 0.75f);
            blockedPlayer.GetComponent<Rigidbody2D>().position = start; blockedPlayer.transform.position = start;
            foreach (var enemy in arena.Enemies) enemy.gameObject.SetActive(false);
            Physics2D.SyncTransforms(); yield return Frames(1);
            int beforeFinisher = blockedPlayer.TotalTechniqueFinishers;
            Check(blockedPlayer.TryTechniqueSkill(Vector2.up), "full-stack rush starts toward solid furniture");
            while (!blockedPlayer.IsTechniqueFinisher) yield return null;
            Vector2 stoppedAt = blockedPlayer.transform.position;
            yield return Idle(blockedPlayer);
            Check(Vector2.Distance(start, blockedPlayer.transform.position) < 0.65f
                && ManorCollisionLayout.ContainsGround(blockedPlayer.transform.position), "solid table clips the full-stack rush at the actual stop");
            Check(blockedPlayer.TotalTechniqueFinishers == beforeFinisher + 1, "blocked full-stack rush still emits one landing slash");
            Check(Vector2.Distance(stoppedAt, blockedPlayer.transform.position) < 0.05f,
                "third-strike animation stays at the collision stop instead of the unreachable destination");
        }

        static void CheckWaveSnapshot(SlashArena arena)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var waves = (IList)typeof(SlashEffects).GetField("waves", fields).GetValue(arena.Effects);
            Check(waves.Count == 1, "pursuit emits exactly one live sword wave");
            var emitted = (SlashTuningSettings.HeavyAttackSettings)waves[0].GetType().GetField("settings", fields).GetValue(waves[0]);
            var current = arena.Player.heavyAttack;
            Check(emitted.damage == Mathf.RoundToInt(current.damage * (1 + arena.Player.ComboDamageBonus))
                && emitted.width == current.width && emitted.distance == current.distance
                && emitted.speed == current.speed && emitted.knockback == current.knockback
                && !ReferenceEquals(emitted, current), "sword tide retains base parameters and snapshots normal-attack stack damage");
            Check((bool)waves[0].GetType().GetField("normalAttack", fields).GetValue(waves[0]), "sword tide damage is classified as a normal attack");
            Capture("Nascent-SwordWave.png");
        }

        static object CurrentWhirlwind(SlashArena arena)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            return ((IList)typeof(SlashEffects).GetField("whirlwinds", fields).GetValue(arena.Effects))[0];
        }

        static IEnumerator AwaitCast(SlashArena arena)
        {
            float start = Time.unscaledTime;
            while (arena.Player.TotalTechniqueCasts == 0)
            {
                if (Time.unscaledTime - start > 2)
                    throw new Exception("Q input did not cast: keyboard=" + (Keyboard.current == keyboard)
                        + ", held=" + keyboard.qKey.isPressed + ", paused=" + arena.Paused
                        + ", blocked=" + arena.InputBlocked + ", cooldown=" + arena.Player.TechniqueCooldownLeft
                        + ", attacking=" + arena.Player.IsAttacking + ", pending=" + keys.HasValue);
                yield return null;
            }
        }

        static void AimIronTest(SlashArena arena)
        {
            Vector2 origin = arena.Player.transform.position;
            Vector3 screen = arena.WorldCamera.WorldToScreenPoint(arena.View.LiftPoint(origin + Vector2.right * 12, arena.View.WeaponHeight));
            pointer = new MouseState { position = new Vector2(screen.x, screen.y), buttons = 2 };
        }

        static ChargedAttackState IronCharge(SlashPlayer player)
            => (ChargedAttackState)typeof(SlashPlayer).GetField("heavyState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);

        static IEnumerator EmitIron(SlashArena arena, int stacks)
        {
            var player = arena.Player;
            player.CancelHeavyAttack();
            Check(player.RequestHeavyAttack(), "Iron Sword charge begins");
            IronCharge(player).Advance(player.heavyAttack.chargeTime * (stacks == 6 ? 1 : (stacks + 0.03f) / 6));
            Check(player.GangQiStacks == stacks && player.ReleaseHeavyAttack() && player.GangQiStacks == 0,
                "release consumes exactly the expected gangqi tier, including zero");
            int waves = player.TotalSwordWaves;
            while (player.TotalSwordWaves == waves) yield return null;
        }

        static IEnumerator ValidateIronZeroImpact()
        {
            yield return Frames();
            var arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
            arena.Tuning.RestoreDefaults(); arena.Player.EquipTechnique(TechniqueCatalog.IronSword);
            foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
                foreach (float feedback in new[] { 0f, 1f, 2.5f })
                    foreach (bool lethal in new[] { false, true })
                    {
                        Place(arena, realm); AimIronTest(arena); yield return Frames();
                        var player = arena.Player; var target = arena.Enemies[0];
                        player.CancelHeavyAttack();
                        arena.Effects.hitStopMultiplier = feedback;
                        if (lethal) target.TakeHit(target.Health - 33, Vector2.zero, 0, interrupt: false);
                        arena.Effects.Clear();
                        int before = target.Health;
                        Debug.Log("IRON_ZERO_IMPACT_CASE: " + realm + " feedback=" + feedback + " lethal=" + lethal);
                        Check(player.RequestHeavyAttack() && player.GangQiStacks == 0 && player.ReleaseHeavyAttack()
                            && player.LastReleasedGangQi == 0, "immediate zero-tier release succeeds");
                        pointer = new MouseState();
                        double timeout = Time.realtimeSinceStartupAsDouble + 12;
                        while ((target.Health == before || player.IsHeavyBusy || arena.Effects.ActiveSwordWaveCount > 0)
                            && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
                        Check(target.Health == before - 33, "zero-tier sword wave hits once for exactly 60 percent damage");
                        Check(!player.IsHeavyBusy && arena.Effects.ActiveSwordWaveCount == 0 && Time.timeScale == 1 && !arena.Effects.Frozen,
                            "zero-tier hit completes recovery and projectile cleanup with simulation running"
                            + " (busy=" + player.IsHeavyBusy + ", charge=" + player.IsCharging + ", waves=" + arena.Effects.ActiveSwordWaveCount
                            + ", scale=" + Time.timeScale + ", frozen=" + arena.Effects.Frozen + ", paused=" + arena.Paused
                            + ", dt=" + Time.deltaTime + ", releaseAge=" + ((HeavyReleaseState)typeof(SlashPlayer).GetField("heavyRelease",
                                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player)).Age + ")");
                        Check(player.RequestAttack(Vector2.right), "normal attack remains available after zero-tier impact");
                        Check(player.TryDash(Vector2.up), "dash remains available after zero-tier impact");
                        yield return Wait(0.3f);
                    }
        }

        static IEnumerator RunIron()
        {
            yield return Frames();
            var arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
            Check(arena != null && TechniqueCatalog.All.Count == 2, "both techniques register in the existing scene");
            arena.Tuning.RestoreDefaults();
            arena.Player.EquipTechnique(TechniqueCatalog.IronSword);
            AimIronTest(arena); yield return Frames();
            foreach (var enemy in arena.Enemies) enemy.gameObject.SetActive(false);
            foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
                for (int stacks = 0; stacks <= 6; stacks++)
                {
                    var player = arena.Player;
                    player.CancelHeavyAttack(); arena.Effects.Clear(); player.SetRealm(realm);
                    var expected = player.IronSword.Release(player.heavyAttack, stacks, realm);
                    yield return EmitIron(arena, stacks);
                    var live = (SlashTuningSettings.HeavyAttackSettings)typeof(SlashPlayer).GetField("chargedSettings",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                    Check(JsonUtility.ToJson(live) == JsonUtility.ToJson(expected), "all four realms and seven tiers snapshot damage, width, range and recovery correctly");
                    Check(arena.Effects.ActiveSwordScarCount == (realm >= CultivationRealm.GoldenCore && stacks >= 5 ? 1 : 0),
                        "scar unlock requires Golden Core and at least five gangqi");
                }
            arena.Player.CancelHeavyAttack(); arena.Effects.Clear();

            foreach (CultivationRealm realm in new[] { CultivationRealm.QiRefining, CultivationRealm.FoundationEstablishment })
                foreach (bool body in new[] { false, true })
                {
                    var player = arena.Player;
                    AimIronTest(arena); yield return Frames(2);
                    player.SetRealm(realm); player.CancelHeavyAttack(); player.ResetTechniqueCooldown();
                    if (body) Check(player.TryTechniqueSkill(Vector2.right), "Iron Body can activate before charging");
                    float speed = 1 + (realm >= CultivationRealm.FoundationEstablishment ? 0.20f : 0) + (body ? 0.3f : 0);
                    Check(player.RequestHeavyAttack(), "start timed native charge");
                    float began = Time.time;
                    double chargeDeadline = Time.realtimeSinceStartupAsDouble + 8;
                    while (!player.HeavyReady && Time.realtimeSinceStartupAsDouble < chargeDeadline) yield return null;
                    Check(player.HeavyReady, "timed charge reaches ready (realm=" + realm + ", body=" + body
                        + ", charging=" + player.IsCharging + ", progress=" + player.ChargeProgress
                        + ", scale=" + Time.timeScale + ", frozen=" + arena.Effects.Frozen + ", paused=" + arena.Paused + ")");
                    Check(Mathf.Abs(Time.time - began - player.heavyAttack.chargeTime / speed) < 0.08f,
                        "real charge speed obeys Foundation and Iron Body bonuses");
                    player.CancelHeavyAttack();
                    // Switching realm clears any remaining Iron Body before the next timing case.
                    player.SetRealm(realm == CultivationRealm.QiRefining ? CultivationRealm.GoldenCore : CultivationRealm.QiRefining);
                }

            Place(arena, CultivationRealm.GoldenCore); AimIronTest(arena); yield return Frames();
            arena.Player.CancelHeavyAttack();
            var target = arena.Enemies[0];
            var extraCollider = target.gameObject.AddComponent<CircleCollider2D>(); extraCollider.radius = target.Radius;
            int health = target.Health, ticks = arena.Effects.TotalScarTicks, explosions = arena.Effects.TotalScarExplosions;
            yield return EmitIron(arena, 6);
            yield return Wait(0.25f); Capture("IronSword-Scar.png");
            yield return Wait(1.4f);
            Check(arena.Effects.TotalScarTicks - ticks == 6 && arena.Effects.TotalScarExplosions == explosions,
                "Golden Core scar ticks six times without explosion and deduplicates multiple colliders");
            Check(health - target.Health == 66 + 6 * 7, "Golden Core scar deals exactly one wave hit and six ten-percent ticks");
            Check((float)typeof(SlashEnemy).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target) < 0,
                "periodic scar damage does not repeatedly refresh normal hit stun");
            Check(arena.Effects.ActiveSwordScarCount == 0, "expired scar cleans up completely");
            UnityEngine.Object.Destroy(extraCollider);

            Place(arena, CultivationRealm.NascentSoul); AimIronTest(arena); yield return Frames();
            var nascent = arena.Player; nascent.CancelHeavyAttack(); target = arena.Enemies[0];
            health = target.Health; ticks = arena.Effects.TotalScarTicks; explosions = arena.Effects.TotalScarExplosions;
            yield return EmitIron(arena, 6);
            yield return Wait(0.22f);
            Check(arena.Effects.IsOnSwordScar(nascent, target.transform.position, target.Radius), "standing target is inside the actual traveled scar");
            int prior = target.Health, priorTicks = arena.Effects.TotalScarTicks;
            yield return EmitIron(arena, 0);
            yield return Wait(0.25f);
            Check(prior - target.Health == 50 + (arena.Effects.TotalScarTicks - priorTicks) * 7,
                "zero-layer heavy hit on an existing scar receives exactly fifty percent extra damage");
            yield return Wait(1.4f);
            Check(arena.Effects.TotalScarExplosions == explosions + 1 && arena.Effects.TotalScarTicks == ticks + 6,
                "Nascent Soul scar expires with one explosion after exactly six ticks");
            Check(health - target.Health == 66 + 50 + 6 * 7 + 33, "wave, scar bonus, periodic damage and explosion resolve independently");

            Place(arena, CultivationRealm.FoundationEstablishment); AimIronTest(arena); yield return Frames();
            var guarded = arena.Player; guarded.CancelHeavyAttack();
            Check(guarded.RequestHeavyAttack(), "begin charge for Iron Body protection");
            IronCharge(guarded).Advance(0.25f);
            int gangqi = guarded.GangQiStacks;
            Check(guarded.TryTechniqueSkill(Vector2.right) && guarded.GangQiStacks == gangqi
                && guarded.IronBodyActive && guarded.TechniqueCooldownLeft > 9.9f,
                "Q activates during charge, consumes no gangqi and starts its ten-second cooldown");
            health = guarded.Health;
            Check(guarded.TakeDamage(50, Vector2.left) && guarded.Health == health - 25 && guarded.IsCharging,
                "Iron Body reduces accepted damage fifty percent and preserves the ongoing charge");
            yield return Wait(3.05f);
            Check(!guarded.IronBodyActive && guarded.IsCharging && guarded.GangQiStacks == 6,
                "Iron Body expiry removes protection while a held full charge keeps waiting");
            health = guarded.Health;
            Check(guarded.TakeDamage(10, Vector2.left) && guarded.Health == health - 10 && !guarded.IsCharging,
                "ordinary incoming damage interrupts charging after Iron Body expires");
            yield return Wait(0.9f);
            Check(guarded.RequestHeavyAttack() && guarded.TryDash(Vector2.up) && !guarded.IsHeavyBusy,
                "Foundation dash converts charge to an immediate wave without leaving a delayed release");
            yield return Wait(0.3f);

            Place(arena, CultivationRealm.NascentSoul); AimIronTest(arena); yield return Frames();
            var exclusive = arena.Player; exclusive.CancelHeavyAttack();
            exclusive.EquipTechnique(TechniqueCatalog.FlowingCloud); exclusive.FillTechniqueStacksForDebug();
            Check(exclusive.ComboStacks == 5, "Flowing Cloud still builds its own passive layers");
            exclusive.EquipTechnique(TechniqueCatalog.IronSword);
            Check(exclusive.ComboStacks == 0 && exclusive.ComboDamageBonus == 0 && exclusive.NormalAttackSpeedMultiplier == 1,
                "switching to Iron Sword removes all Flowing Cloud passives");
            for (int i = 0; i < 4; i++)
            {
                Check(exclusive.RequestAttack(Vector2.right) && exclusive.ComboIndex == i % 3,
                    "Iron Sword ordinary attacks remain a three-strike sequence without whirlwind");
                yield return Idle(exclusive);
            }
            Check(exclusive.ComboStacks == 0 && exclusive.TotalPursuits == 0, "Iron Sword ordinary hits cannot build Tide layers or activate Flowing Cloud follow-ups");

            Rect table = ManorCollisionLayout.FurnitureFootprint("Tea table");
            exclusive.Teleport(new Vector2(table.center.x, table.yMin - 0.8f));
            pointer = new MouseState(); yield return Frames();
            Check(!arena.Codex.Equip(TechniqueCatalog.FlowingCloud), "equipment selection is unavailable outside the open desk codex");
            Check(arena.Codex.Open() && arena.Codex.Equip(TechniqueCatalog.FlowingCloud)
                && exclusive.Technique == TechniqueCatalog.FlowingCloud, "desk codex equips Flowing Cloud exclusively");
            Check(arena.Codex.Equip(TechniqueCatalog.IronSword) && exclusive.Technique == TechniqueCatalog.IronSword,
                "desk codex equips Iron Sword exclusively");
            Check(PlayerPrefs.GetString(SlashPlayer.TechniqueSaveKey) == TechniqueCatalog.IronSword.id,
                "equipped technique selection persists for later launches");
            typeof(SlashHUD).GetField("codexIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena.GetComponent<SlashHUD>(), 1);
            yield return Frames(); Capture("IronSword-Codex.png");
            arena.Codex.Close(); yield return Frames();
            arena.Restart(); yield return Frames();
            Check(arena.Player.HasIronSword && arena.Player.Realm == CultivationRealm.QiRefining,
                "restart retains the equipped book while resetting realm normally");
            arena.Player.ResetTechniqueCooldown();
            Check(arena.Player.TryTechniqueSkill(Vector2.right), "Iron Body starts on the equipped skill slot");
            float cooldown = arena.Player.TechniqueCooldownLeft;
            arena.Player.EquipTechnique(TechniqueCatalog.FlowingCloud);
            Check(!arena.Player.IronBodyActive, "switching books clears Iron Body");
            arena.Player.EquipTechnique(TechniqueCatalog.IronSword);
            Check(arena.Player.TechniqueCooldownLeft == cooldown, "switching books cannot refresh Iron Body cooldown");

            var settings = arena.Tuning.Settings;
            settings.ironSword.scarTickDamageRatio = 0.17f; settings.ironSword.bodyDuration = 2.1f;
            settings.ironSword.bodyHeavyBonus = 0.47f;
            arena.Tuning.ApplyAll();
            var restored = JsonUtility.FromJson<SlashTuningSettings>(JsonUtility.ToJson(settings)); restored.Validate(new SlashTuningSettings());
            Check(restored.ironSword.scarTickDamageRatio == 0.17f && restored.ironSword.bodyDuration == 2.1f
                && arena.Player.IronSword.bodyDuration == 2.1f && restored.ironSword.bodyHeavyBonus == 0.47f
                && arena.Player.IronSword.bodyHeavyBonus == 0.47f, "Iron Sword tuning applies and survives save serialization");
            var legacy = JsonUtility.FromJson<SlashTuningSettings>(File.ReadAllText("Verification/Artifacts/SavedTuning-2026-10-27.json"));
            legacy.Validate(new SlashTuningSettings());
            Check(legacy.ironSword.bodyCooldown == 10 && legacy.ironSword.scarTickDamageRatio == 0.10f
                && legacy.ironSword.bodyDuration == 3 && legacy.ironSword.bodyDamageReduction == 0.5f
                && legacy.ironSword.bodyHeavyBonus == 0.3f,
                "old tuning saves receive all Iron Sword defaults");
            var existingIronSave = JsonUtility.FromJson<SlashTuningSettings>("{\"ironSword\":{\"bodyDuration\":2,\"bodyChargeBonus\":0.4}}");
            existingIronSave.Validate(new SlashTuningSettings());
            Check(existingIronSave.ironSword.bodyHeavyBonus == 0.3f && existingIronSave.ironSword.bodyDuration == 2
                && existingIronSave.ironSword.bodyChargeBonus == 0.4f,
                "existing Iron Sword saves receive the new heavy-bonus default while retaining their saved tuning");
            arena.Tuning.RestoreDefaults();
            yield return ValidateIronZeroImpact();
            yield return ValidateIronDashRelease(arena);
            yield return ValidateIronBodyDamage(arena);
            yield return ValidateIronHud(arena);
            yield return ValidateIronVisuals(arena);
            keys = new KeyboardState(Key.F1); while (!arena.Tuning.IsOpen) yield return null;
            keys = new KeyboardState();
            typeof(SlashTuningPanel).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena.Tuning, 6);
            yield return Frames(); Capture("IronSword-Tuning.png");
            typeof(SlashTuningPanel).GetField("scroll", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena.Tuning, new Vector2(0, 1000));
            yield return Frames(); Capture("IronSword-Tuning-Bottom.png");
            typeof(SlashTuningPanel).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena.Tuning, 7);
            typeof(SlashTuningPanel).GetField("scroll", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena.Tuning, Vector2.zero);
            yield return Frames(); Capture("IronSword-Active-Tuning.png");
        }

        static IEnumerator ValidateIronDashRelease(SlashArena arena)
        {
            foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
                for (int stacks = 0; stacks <= 6; stacks++)
                {
                    Place(arena, realm); AimIronTest(arena); yield return Frames();
                    var player = arena.Player; var target = arena.Enemies[0];
                    player.CancelHeavyAttack();
                    Check(player.RequestHeavyAttack(), "begin charge for dash release");
                    IronCharge(player).Advance(player.heavyAttack.chargeTime * (stacks == 6 ? 1 : (stacks + 0.03f) / 6));
                    var expected = player.IronSword.Release(player.heavyAttack, stacks, realm);
                    Vector2 origin = player.transform.position, aim = player.Aim;
                    int waves = player.TotalSwordWaves, health = target.Health;
                    bool unlocked = realm >= CultivationRealm.FoundationEstablishment;
                    Check(player.TryDash(Vector2.up) && player.IsDashing && !player.IsHeavyBusy && player.GangQiStacks == 0,
                        "dash clears charge and leaves movement free of heavy recovery");
                    Check(player.TotalSwordWaves == waves + (unlocked ? 1 : 0),
                        "dash releases immediately only from Foundation onward, including zero-tier charge");
                    if (unlocked)
                    {
                        var live = (SlashTuningSettings.HeavyAttackSettings)typeof(SlashPlayer).GetField("chargedSettings",
                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                        Check(player.LastReleasedGangQi == stacks && JsonUtility.ToJson(live) == JsonUtility.ToJson(expected),
                            "dash snapshots the current tier damage, width, distance and recovery without extra charging");
                        var active = (IList)typeof(SlashEffects).GetField("waves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena.Effects);
                        object wave = active[active.Count - 1];
                        Check(Vector2.Distance((Vector2)wave.GetType().GetField("position").GetValue(wave), origin + aim * 0.4f) < 0.0001f
                            && Vector2.Distance((Vector2)wave.GetType().GetField("direction").GetValue(wave), aim) < 0.0001f,
                            "dash wave starts at departure point and follows charge aim independently of dash direction");
                        double timeout = Time.realtimeSinceStartupAsDouble + 8;
                        while (target.Health == health && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
                        Check(health - target.Health == expected.damage, "dash wave applies the snapshotted damage once on first contact");
                    }
                    Check(arena.Effects.ActiveSwordScarCount == (unlocked && realm >= CultivationRealm.GoldenCore && stacks >= 5 ? 1 : 0),
                        "dash release retains the five-tier and realm gates for sword scars");
                    pointer = new MouseState(); yield return Wait(0.60f);
                    Check(player.TotalSwordWaves == waves + (unlocked ? 1 : 0) && !player.IsDashing && !player.IsHeavyBusy,
                        "releasing the button after dash cannot emit a second wave or lock the player");
                }

            Place(arena, CultivationRealm.FoundationEstablishment); AimIronTest(arena); yield return Frames();
            var inputPlayer = arena.Player; inputPlayer.CancelHeavyAttack(); inputPlayer.RequestHeavyAttack();
            IronCharge(inputPlayer).Advance(inputPlayer.heavyAttack.chargeTime * 0.5f);
            int before = inputPlayer.TotalSwordWaves;
            pointer = new MouseState(); keys = new KeyboardState(Key.Space, Key.W);
            yield return Frames(2);
            Check(inputPlayer.TotalSwordWaves == before + 1 && !inputPlayer.IsCharging,
                "real Space input and simultaneous mouse release fire only one dash heavy");
            keys = new KeyboardState(); yield return Wait(0.20f);
            Check(inputPlayer.RequestHeavyAttack(), "a new charge may begin while dash cooldown is running");
            float progress = inputPlayer.ChargeProgress;
            Check(!inputPlayer.TryDash(Vector2.up) && inputPlayer.IsCharging && inputPlayer.ChargeProgress == progress
                && inputPlayer.TotalSwordWaves == before + 1,
                "rejected dash during cooldown neither consumes charge nor fires a wave");
            inputPlayer.CancelHeavyAttack();

            Place(arena, CultivationRealm.NascentSoul);
            arena.Player.EquipTechnique(TechniqueCatalog.FlowingCloud); AimIronTest(arena); yield return Frames();
            var cloud = arena.Player; cloud.CancelHeavyAttack(); cloud.RequestHeavyAttack();
            IronCharge(cloud).Advance(cloud.heavyAttack.chargeTime);
            before = cloud.TotalSwordWaves;
            Check(cloud.TryDash(Vector2.up) && !cloud.IsHeavyBusy && cloud.TotalSwordWaves == before,
                "Flowing Cloud dash continues to cancel even a full charge without releasing it");
            pointer = new MouseState(); yield return Wait(0.35f);
            cloud.EquipTechnique(TechniqueCatalog.IronSword);
        }

        static IEnumerator ValidateIronBodyDamage(SlashArena arena)
        {
            foreach (CultivationRealm realm in new[] { CultivationRealm.FoundationEstablishment, CultivationRealm.NascentSoul })
                foreach (int stacks in new[] { 0, 4, 5, 6 })
                    foreach (bool dash in new[] { false, true })
                    {
                        Place(arena, realm); AimIronTest(arena); yield return Frames();
                        var player = arena.Player; var target = arena.Enemies[0];
                        player.CancelHeavyAttack(); player.ResetTechniqueCooldown();
                        Check(player.RequestHeavyAttack(), "begin charge before activating protection damage bonus");
                        IronCharge(player).Advance(player.heavyAttack.chargeTime * (stacks == 6 ? 1 : (stacks + 0.03f) / 6));
                        Check(player.TryTechniqueSkill(Vector2.right) && player.GangQiStacks == stacks
                            && player.IronBodyTimeLeft == 3, "protection activates mid-charge for three seconds without consuming Gangqi");
                        int health = target.Health;
                        int expected = stacks == 0 ? 43 : stacks == 4 ? 72 : stacks == 5 ? 79 : 86;
                        Check(dash ? player.TryDash(Vector2.up) : player.ReleaseHeavyAttack(), "protected heavy can release normally or through dash");
                        var live = (SlashTuningSettings.HeavyAttackSettings)typeof(SlashPlayer).GetField("chargedSettings",
                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                        Check(live.damage == expected, "normal and dash release snapshot exactly thirty percent extra heavy damage");
                        double timeout = Time.realtimeSinceStartupAsDouble + 8;
                        while (target.Health == health && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
                        Check(health - target.Health == expected, "protected projectile applies the boosted heavy damage only once");
                        if (realm == CultivationRealm.NascentSoul && stacks == 6 && !dash)
                        {
                            yield return Wait(1.65f);
                            Check(health - target.Health == 86 + 6 * 9 + 43,
                                "scar ticks and explosion inherit the boosted heavy without multiplying protection twice");
                        }
                    }

            // A released wave keeps its damage if protection expires before the delayed emission or hit.
            Place(arena, CultivationRealm.FoundationEstablishment); AimIronTest(arena); yield return Frames();
            var snapshot = arena.Player; snapshot.CancelHeavyAttack(); snapshot.ResetTechniqueCooldown();
            snapshot.RequestHeavyAttack(); IronCharge(snapshot).Advance(snapshot.heavyAttack.chargeTime);
            snapshot.TryTechniqueSkill(Vector2.right); snapshot.ReleaseHeavyAttack();
            typeof(SlashPlayer).GetField("ironBodyLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(snapshot, 0.01f);
            var enemy = arena.Enemies[0]; int before = enemy.Health;
            yield return Wait(0.40f);
            Check(!snapshot.IronBodyActive && before - enemy.Health == 86, "expiry after release cannot remove damage already snapshotted into the heavy");

            Place(arena, CultivationRealm.FoundationEstablishment); AimIronTest(arena); yield return Frames();
            snapshot = arena.Player; snapshot.CancelHeavyAttack(); snapshot.ResetTechniqueCooldown();
            snapshot.RequestHeavyAttack(); snapshot.TryTechniqueSkill(Vector2.right);
            typeof(SlashPlayer).GetField("ironBodyLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(snapshot, 0f);
            snapshot.ReleaseHeavyAttack();
            var unprotected = (SlashTuningSettings.HeavyAttackSettings)typeof(SlashPlayer).GetField("chargedSettings",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snapshot);
            Check(unprotected.damage == 33, "protection that ended before release does not grant stale heavy damage");
            snapshot.ResetTechniqueCooldown();
            Check(snapshot.TryTechniqueSkill(Vector2.right), "protection can activate after an unboosted heavy was released");
            yield return Wait(0.40f);
            Check(!snapshot.IsHeavyBusy && arena.Enemies[0].maxHealth - arena.Enemies[0].Health == 33,
                "activating protection after release cannot retroactively boost an existing heavy");
        }

        static IEnumerator ValidateIronHud(SlashArena arena)
        {
            Place(arena, CultivationRealm.NascentSoul); AimIronTest(arena); yield return Frames();
            foreach (var enemy in arena.Enemies) enemy.gameObject.SetActive(false);
            var player = arena.Player;
            var hud = arena.GetComponent<SlashHUD>();
            var gangqi = Resources.Load<Texture2D>("SlashDemo/IronSwordGangQiIcon");
            var active = Resources.Load<Texture2D>("SlashDemo/IronSwordActiveIcon");
            Check(gangqi != null && active != null && gangqi.width == gangqi.height && active.width == active.height,
                "both supplied Iron Sword icon assets load at their original square dimensions");
            Check(ReferenceEquals(active, typeof(SlashHUD).GetField("ironSkillIcon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud))
                && player.Technique.activeName == "剑罡护体",
                "active slot uses the supplied round artwork and renamed Sword Gangqi Protection caption");
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(active));
            Check(importer.alphaIsTransparency && importer.DoesSourceTextureHaveAlpha(), "round active icon preserves its supplied transparent background");
            player.CancelHeavyAttack(); player.ResetTechniqueCooldown();
            Check(player.RequestHeavyAttack(), "begin charge for the Gangqi HUD preview");
            IronCharge(player).Advance(player.heavyAttack.chargeTime * 0.5f);
            Time.timeScale = 0;
            yield return Frames(2);
            Check(hud.VisibleBuffs.Count == 1 && hud.VisibleBuffs[0].id == "iron-sword-gangqi"
                && hud.VisibleBuffs[0].iconResource == "SlashDemo/IronSwordGangQiIcon" && hud.VisibleBuffs[0].stacks == player.GangQiStacks,
                "Gangqi uses the supplied square artwork and live stack badge in the shared buff bar");
            Capture("IronSword-Icons-Charging.png");
            Check(player.TryTechniqueSkill(Vector2.right), "activate Sword Gangqi Protection during the HUD charge preview");
            IronCharge(player).Advance(player.heavyAttack.chargeTime);
            yield return Frames(2);
            Check(hud.VisibleBuffs.Count == 2 && hud.VisibleBuffs[0].stacks == 6
                && hud.VisibleBuffs[1].name == "剑罡护体" && hud.VisibleBuffs[1].iconResource == "SlashDemo/IronSwordActiveIcon"
                && hud.VisibleBuffs[1].duration == player.IronSword.bodyDuration,
                "full Gangqi and timed protection share Flowing Cloud's stack, tooltip and countdown presentation");
            Capture("IronSword-Icons-Protected.png");
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            Vector2 center = BuffBarLayout.Slot(0, new Rect(50, 141, 282, 0)).center;
            pointer = new MouseState { position = new Vector2(center.x * scale, Screen.height - center.y * scale), buttons = 2 };
            yield return Frames(3);
            Check(hud.HoveredBuffId == "iron-sword-gangqi", "new Gangqi icon retains the shared effect tooltip");
            Capture("IronSword-Icons-Gangqi-Tooltip.png");
            player.CancelHeavyAttack();
            Check(hud.VisibleBuffs.Count == 1 && hud.VisibleBuffs[0].id == "iron-sword-body", "ending charge removes Gangqi without hiding the active protection buff");
            pointer = new MouseState(); Time.timeScale = 1;
            yield return Wait(player.IronSword.bodyDuration + 0.05f);
            Check(hud.VisibleBuffs.Count == 0 && player.TechniqueCooldownLeft > 0,
                "protection expiry removes its buff while the active slot continues cooling down");
        }

        static object IronVisualState(SlashArena arena, bool burst)
        {
            var list = (IList)typeof(SlashEffects).GetField(burst ? "swordScarBursts" : "swordScars",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena.Effects);
            return list[0];
        }

        static void CheckIronVisualMesh(SlashArena arena, bool burst)
        {
            object state = IronVisualState(arena, burst);
            object visual = state.GetType().GetField("visual").GetValue(state);
            var mesh = (Mesh)visual.GetType().GetField("mesh").GetValue(visual);
            var renderer = (MeshRenderer)visual.GetType().GetField("renderer").GetValue(visual);
            bool finite = mesh.vertexCount > 0;
            foreach (var vertex in mesh.vertices)
                finite &= !float.IsNaN(vertex.x) && !float.IsInfinity(vertex.x)
                    && !float.IsNaN(vertex.y) && !float.IsInfinity(vertex.y) && !float.IsNaN(vertex.z) && !float.IsInfinity(vertex.z);
            Check(finite && mesh.bounds.size.sqrMagnitude > 0 && renderer.sharedMaterial.shader.name == "SlashDemo/BladeTrailAdditive",
                "scar and eruption use valid finite meshes with the ordinary blade's additive HDR shader");
            bool cool = true;
            foreach (var color in mesh.colors) cool &= color.b >= color.r && color.b >= color.g;
            Check(cool, "scar and eruption retain white cores and ice-blue falloff");
            if (!burst)
            {
                object dark = state.GetType().GetField("dark").GetValue(state);
                var darkMesh = (Mesh)dark.GetType().GetField("mesh").GetValue(dark);
                var darkRenderer = (MeshRenderer)dark.GetType().GetField("renderer").GetValue(dark);
                Check(darkRenderer.sharedMaterial.shader.name == "SlashDemo/BladeTrail"
                    && darkRenderer.sortingOrder == renderer.sortingOrder - 1 && darkMesh.vertexCount < mesh.vertexCount,
                    "fracture has a matching alpha-blended dark bed beneath its additive core");
                bool rangeEdges = true;
                Vector2 origin = (Vector2)state.GetType().GetField("origin").GetValue(state);
                Vector2 direction = (Vector2)state.GetType().GetField("direction").GetValue(state);
                float width = (float)state.GetType().GetField("width").GetValue(state);
                var root = (Transform)visual.GetType().GetField("root").GetValue(visual);
                var allVertices = mesh.vertices;
                int lineVertices = mesh.vertexCount - darkMesh.vertexCount;
                int sections = lineVertices / 14;
                for (int line = 0; line < 2; line++)
                    for (int row = 0; row < sections; row++)
                    {
                        Vector2 delta = (Vector2)root.TransformPoint(allVertices[darkMesh.vertexCount + line * sections * 7 + row * 7 + 3]) - origin;
                        float across = delta.x * -direction.y + delta.y * direction.x;
                        rangeEdges &= Mathf.Abs(across - (line == 0 ? -1 : 1) * width * 0.5f) < 0.0001f;
                    }
                Check(sections > 1 && rangeEdges, "two parallel ground lines mark the actual scar damage width");
                bool grounded = true;
                foreach (var vertex in mesh.vertices) grounded &= Mathf.Abs(vertex.z) < 0.00001f;
                foreach (var vertex in darkMesh.vertices) grounded &= Mathf.Abs(vertex.z) < 0.00001f;
                Check(grounded, "both fracture layers lie on the ground plane, including at 30 degree elevation");
                Check(mesh.vertexCount > 25 * 7, "main fracture includes revealed secondary branches");
                var vertices = mesh.vertices;
                typeof(SlashEffects).GetMethod("DrawSwordScar", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(arena.Effects, new[] { state });
                var repeated = mesh.vertices;
                bool stable = vertices.Length == repeated.Length;
                for (int i = 0; stable && i < vertices.Length; i++) stable &= vertices[i] == repeated[i];
                Check(stable, "fracture knots stay fixed across redraws instead of flickering randomly");
            }
        }

        static IEnumerator ValidateIronVisuals(SlashArena arena)
        {
            foreach (int angle in new[] { 0, 45 })
            {
                Place(arena, CultivationRealm.NascentSoul);
                float elevation = arena.View.elevation;
                if (angle == 45) arena.View.elevation = 30;
                foreach (var enemy in arena.Enemies) enemy.gameObject.SetActive(false);
                Vector2 direction = DemoArt.Direction(angle), origin = arena.Player.transform.position;
                Vector3 screen = arena.WorldCamera.WorldToScreenPoint(arena.View.LiftPoint(origin + direction * 12, arena.View.WeaponHeight));
                pointer = new MouseState { position = new Vector2(screen.x, screen.y), buttons = 2 };
                yield return Frames();
                int explosions = arena.Effects.TotalScarExplosions;
                yield return EmitIron(arena, 6);
                yield return Wait(0.55f);
                CheckIronVisualMesh(arena, false);
                Capture("IronSword-VFX-Scar-" + angle + ".png");
                pointer = new MouseState(); yield return Frames(2);
                object scar = IronVisualState(arena, false);
                float age = (float)scar.GetType().GetField("age").GetValue(scar);
                object dark = scar.GetType().GetField("dark").GetValue(scar);
                var darkRoot = (Transform)dark.GetType().GetField("root").GetValue(dark);
                Time.timeScale = 0;
                var drawScar = typeof(SlashEffects).GetMethod("DrawSwordScar", BindingFlags.Instance | BindingFlags.NonPublic);
                float earlyAlpha = 0, lateAlpha = 0;
                foreach (float sample in new[] { 0.18f, 1.30f })
                {
                    scar.GetType().GetField("age").SetValue(scar, sample);
                    drawScar.Invoke(arena.Effects, new[] { scar });
                    object light = scar.GetType().GetField("visual").GetValue(scar);
                    var sampledMesh = (Mesh)light.GetType().GetField("mesh").GetValue(light);
                    float peak = 0;
                    foreach (var color in sampledMesh.colors) peak = Mathf.Max(peak, color.a);
                    if (sample < 1) earlyAlpha = peak; else lateAlpha = peak;
                    yield return Frames(2);
                    Capture("IronSword-Fracture-" + (sample < 1 ? "Early-" : "Charged-") + angle + ".png");
                }
                Check(lateAlpha > earlyAlpha * 1.4f, "ground fracture visibly accumulates light before erupting");
                scar.GetType().GetField("age").SetValue(scar, age);
                drawScar.Invoke(arena.Effects, new[] { scar });
                Time.timeScale = 1;
                int ticks = arena.Effects.TotalScarTicks;
                arena.TogglePause(); yield return Frames(6);
                Check((float)scar.GetType().GetField("age").GetValue(scar) == age && arena.Effects.TotalScarTicks == ticks,
                    "pause freezes scar animation and damage together");
                arena.TogglePause();
                while (arena.Effects.TotalScarExplosions == explosions) yield return null;
                Check(arena.Effects.ActiveSwordScarCount == 0 && arena.Effects.ActiveSwordScarBurstCount == 1,
                    "damage scar ends exactly once while the visual-only eruption begins");
                yield return null;
                Check(darkRoot == null, "dark fracture bed is destroyed with the damage scar");
                CheckIronVisualMesh(arena, true);
                object burst = IronVisualState(arena, true);
                age = (float)burst.GetType().GetField("age").GetValue(burst);
                arena.TogglePause(); yield return Frames(6);
                Check((float)burst.GetType().GetField("age").GetValue(burst) == age,
                    "pause freezes the eruption instead of aging its fade");
                arena.TogglePause();
                // Sample the brief impact at exact ages; screenshot rendering can span its entire lifetime.
                Time.timeScale = 0;
                yield return Frames(2);
                var drawBurst = typeof(SlashEffects).GetMethod("DrawSwordScarBurst", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (float sample in new[] { 0.025f, 0.08f, 0.17f })
                {
                    burst.GetType().GetField("age").SetValue(burst, sample);
                    drawBurst.Invoke(arena.Effects, new[] { burst });
                    yield return Frames(2);
                    string stage = sample < 0.03f ? "Flash" : sample < 0.10f ? "Crescents" : "Splinters";
                    Capture("IronSword-VFX-" + stage + "-" + angle + ".png");
                }
                Time.timeScale = 1; yield return Wait(0.35f);
                Check(arena.Effects.ActiveSwordScarBurstCount == 0, "eruption meshes clean up after fading");
                arena.View.elevation = elevation;
            }
        }

        static void Capture(string name)
        {
            var viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (viewType == null) return;
            var view = captureView != null ? captureView : EditorWindow.GetWindow(viewType);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo render = null;
            for (Type type = viewType; type != null && render == null; type = type.BaseType)
                render = type.GetMethod("RenderView", flags, null, new[] { typeof(Vector2), typeof(bool) }, null);
            var texture = render?.Invoke(view, new object[] { Vector2.zero, false }) as RenderTexture;
            if (texture == null) { Debug.Log("TECHNIQUE_CAPTURE_UNAVAILABLE: " + name); return; }
            var previous = RenderTexture.active;
            var readable = RenderTexture.GetTemporary(texture.width, texture.height, 0);
            Graphics.Blit(texture, readable, new Vector2(1, -1), new Vector2(0, 1));
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            RenderTexture.active = readable;
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
            Directory.CreateDirectory("Verification/Artifacts");
            File.WriteAllBytes("Verification/Artifacts/" + name, pixels.EncodeToPNG());
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(readable); UnityEngine.Object.Destroy(pixels);
            Debug.Log("TECHNIQUE_CAPTURE_SAVED: " + name);
        }
    }
}
