using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SlashDemo.Editor
{
    public static class SceneTravelValidation
    {
        const string Pending = "SlashDemo.SceneTravelValidation.Pending";
        static int stage, choice, afterFrame, health, swings, expectedFortune;
        static double next;
        static SlashPlayer original;
        static Quaternion rotation;
        static float zoom;
        static Mouse mouse;
        static Keyboard keyboard;
        static KeyboardState? queuedKeys;
        static MouseState? queuedMouse;
        static CultivationRealm OutboundRealm => (CultivationRealm)(choice % (int)CultivationRealm.NascentSoul + 1);
        static TravelMapDefinition Destination => choice < TravelMaps.Choices.Length ? TravelMaps.Choices[choice]
            : TravelMaps.Get(TravelDestination.FinalBattle);

        // Inject edges into gameplay's input update, not the editor's separate state buffer.
        static void InjectGameplayInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            if (queuedKeys.HasValue)
            {
                InputSystem.QueueStateEvent(keyboard, queuedKeys.Value);
                queuedKeys = null;
            }
            if (queuedMouse.HasValue)
            {
                InputSystem.QueueStateEvent(mouse, queuedMouse.Value);
                queuedMouse = null;
            }
        }

        static void QueueKeys(KeyboardState state) { queuedKeys = state; }
        static void QueueMouse(MouseState state) { queuedMouse = state; }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + ".Launch", true);
            Application.runInBackground = true;
            next = EditorApplication.timeSinceStartup + 3;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
            next = EditorApplication.timeSinceStartup + 3;
        }

        static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            stage = -2;
            choice = 0;
            afterFrame = Time.frameCount + 4;
            // Batch mode has no focused Game view; route synthetic devices to gameplay explicitly.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            queuedKeys = null;
            queuedMouse = null;
            InputSystem.onBeforeUpdate -= InjectGameplayInput;
            InputSystem.onBeforeUpdate += InjectGameplayInput;
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        static void Advance(int value)
        {
            stage = value;
            afterFrame = Time.frameCount + 4;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.04;
            if (SessionState.GetBool(Pending + ".Launch", false))
            {
                SessionState.SetBool(Pending + ".Launch", false);
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying || Time.frameCount < afterFrame) return;
            var arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
            if (arena == null || arena.Player == null || arena.Travel == null) return;
            var travel = arena.Travel;
            try
            {
                switch (stage)
                {
                    case -2:
                        original = arena.Player;
                        Check(original.Realm == CultivationRealm.QiRefining && original.RealmDisplayName == "练气",
                            "Player did not start in Qi Refining.");
                        original.SetRealm(CultivationRealm.NascentSoul);
                        travel.ReturnHome();
                        Check(original.Realm == CultivationRealm.QiRefining,
                            "Returning home while already in the manor did not reset the realm.");
                        original.Teleport(ManorEnvironment.SpawnPoint);
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(-1);
                        break;
                    case -1:
                        Check(arena.PauseMenuOpen && arena.Paused && Time.timeScale == 0 && arena.InputBlocked,
                            "Esc did not open the paused menu; paused=" + arena.Paused
                            + ", queued=" + queuedKeys.HasValue + ", key=" + keyboard.escapeKey.isPressed
                            + ", update=" + InputSystem.settings.updateMode);
                        Directory.CreateDirectory("Verification/Artifacts");
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/Artifacts/Pause-menu.png"));
                        QueueKeys(new KeyboardState(Key.W));
                        Advance(-3);
                        break;
                    case -3:
                        Check(Vector2.Distance(original.transform.position, ManorEnvironment.SpawnPoint) < 0.001f,
                            "Movement continued behind the pause menu.");
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(-4);
                        break;
                    case -4:
                        Check(!arena.PauseMenuOpen && !arena.Paused && Time.timeScale > 0,
                            "Second Esc did not resume the game.");
                        QueueKeys(new KeyboardState());
                        Debug.Log("PAUSE_MENU_PASS: Esc opens/resumes, game simulation and movement pause behind menu.");
                        Advance(0);
                        break;
                    case 0:
                        Check(!travel.IsSelectionOpen && travel.Current == TravelDestination.Manor, "Spawn opened the destination picker.");
                        original = arena.Player;
                        Check(original.Realm == CultivationRealm.QiRefining, "Restart did not reset the realm.");
                        Check(arena.Resources.SpiritStones == 50 && arena.Resources.FortunePoints == 15,
                            "A new run must start with 50 spirit stones and 15 fortune points.");
                        Check(arena.Resources.TryChangeSpiritStones(-5) && arena.Resources.TryChangeFortunePoints(2),
                            "Could not set non-default balances for travel validation.");
                        Check(!travel.ShowsDestination(TravelDestination.HealingLodge) && !travel.ShowsDestination(TravelDestination.Market),
                            "Services were visible before spending fortune.");
                        if (SceneTravel.UsesServiceVisit(Destination.destination))
                            Check(arena.Resources.TryChangeFortunePoints(-10) && arena.Resources.TryChangeFortunePoints(10)
                                && arena.Resources.ServiceVisits == 2, "Gross spending did not grant two shared visits.");
                        expectedFortune = arena.Resources.FortunePoints - SceneTravel.FortuneCost(Destination.destination);
                        original.SetRealm(OutboundRealm);
                        original.invincible = false;
                        original.TakeDamage(12, Vector2.right);
                        health = original.Health;
                        arena.Tuning.Settings.moveSpeed = 7.1f;
                        arena.Tuning.ApplyAll();
                        Check(original.Realm == OutboundRealm, "Applying tuning overwrote the current realm.");
                        rotation = arena.WorldCamera.transform.rotation;
                        zoom = arena.View.zoom;
                        QueueKeys(new KeyboardState(Key.R, Key.T));
                        original.Teleport(SceneTravel.ManorGate.center);
                        Advance(1);
                        break;
                    case 1:
                        Check(arena.Player == original && arena.practiceMode, "R or T shortcut is still active.");
                        Check(travel.IsSelectionOpen && arena.Paused && Time.timeScale == 0 && arena.InputBlocked, "Gate picker did not pause and block combat.");
                        travel.CancelSelection();
                        QueueKeys(new KeyboardState());
                        Advance(2);
                        break;
                    case 2:
                        Check(!travel.IsSelectionOpen && !arena.Paused, "Cancel immediately reopened the gate picker.");
                        original.Teleport(ManorEnvironment.Ground(680, 720));
                        Advance(3);
                        break;
                    case 3:
                        original.Teleport(SceneTravel.ManorGate.center);
                        Advance(4);
                        break;
                    case 4:
                        Check(travel.IsSelectionOpen, "Gate picker did not rearm after leaving.");
                        swings = original.TotalSwings;
                        QueueMouse(new MouseState().WithButton(MouseButton.Left));
                        Advance(5);
                        break;
                    case 5:
                        Check(original.TotalSwings == swings, "Clicking while choosing triggered an attack.");
                        travel.SelectDestination(Destination.destination);
                        if (Destination.destination == TravelDestination.FinalBattle) CheckFinalBoss(arena, exerciseSkills: true);
                        Check(!travel.IsSelectionOpen && !arena.Paused, "Destination selection did not close picker.");
                        Check(arena.Resources.SpiritStones == 45 && arena.Resources.FortunePoints == expectedFortune,
                            "Selecting a route charged the wrong balance.");
                        if (SceneTravel.UsesServiceVisit(Destination.destination))
                            Check(arena.Resources.ServiceVisits == 1, "Service entry did not use one shared visit.");
                        Check(arena.Player == original && original.Health == health, "Travel replaced the character or changed health.");
                        Check(original.Realm == OutboundRealm, "Outbound travel changed the realm.");
                        Check(Mathf.Abs(original.moveSpeed - 7.1f) < 0.001f, "Travel lost character tuning.");
                        Check(Vector2.Distance(original.transform.position, Destination.Entry) < 0.001f, "Travel did not arrive at map entrance.");
                        Check(Quaternion.Angle(rotation, arena.WorldCamera.transform.rotation) < 0.001f && arena.View.zoom == zoom, "Travel changed camera projection.");
                        Check(arena.InputBlocked, "Selection click can leak through into combat.");
                        Advance(6);
                        break;
                    case 6:
                        Check(original.TotalSwings == swings, "Held selection click triggered an attack after travel.");
                        QueueMouse(new MouseState());
                        Advance(7);
                        break;
                    case 7:
                        Check(!travel.BlocksGameplayInput, "Input did not resume after releasing the selection click.");
                        foreach (var enemy in arena.Enemies)
                            Check(enemy == null || enemy.transform.IsChildOf(travel.ActiveMap.transform), "Manor enemy remained registered in the new map.");
                        Check(!arena.GetComponentInChildren<ManorEnvironment>(true).gameObject.activeInHierarchy, "Manor colliders remained active.");
                        ValidateMap(travel.ActiveMap, original.GetComponent<CircleCollider2D>().radius);
                        arena.CameraRig.ResetFeedback();
                        ManorValidation.Render(arena, Path.GetFullPath("Verification/Artifacts/Travel-" + travel.Current + ".png"));
                        if (travel.Current == TravelDestination.HealingLodge) HealingLodgeValidation.Run(arena);
                        original.Teleport(travel.ActiveMap.ReservedExit.transform.position);
                        Advance(8);
                        break;
                    case 8:
                        Check(travel.Current == Destination.destination && !travel.IsSelectionOpen, "Reserved exit unexpectedly teleported the player.");
                        Debug.Log("SCENE_TRAVEL_DESTINATION_PASS: " + travel.Current + "; entry, routes, character state, camera, click suppression, inactive reserved exit.");
                        if (travel.Current == TravelDestination.HealingLodge)
                        {
                            Check(travel.Portal == null && !travel.ShowTreatmentPrompt, "Treatment must start at the bed, with no portal yet.");
                            original.Teleport(travel.ActiveMap.Definition.Ground(550, 310));
                            QueueKeys(new KeyboardState(Key.F));
                            Advance(20);
                            break;
                        }
                        if (travel.Current == TravelDestination.Market)
                        {
                            var definition = travel.ActiveMap.Definition;
                            Check(definition.WorldSize == new Vector2(27, 18), "The market must be 27 by 18 world units.");
                            Check(arena.Enemies.Count == 0 && travel.Portal != null &&
                                travel.ActiveMap.GetComponentsInChildren<TeleportFormation>().Length == 1,
                                "The market must start with one portal and no enemies.");
                            var marker = TravelMaps.MarketPortal;
                            Check(Vector2.Distance(travel.Portal.transform.position, definition.Ground(marker.x, marker.y)) < .01f,
                                "The market portal did not fit at the foot of the entrance steps.");
                            original.Teleport(definition.Entry);
                            Check(travel.ShowPortalPrompt, "The market arrival point is out of reach of its portal.");
                            QueueKeys(new KeyboardState(Key.F));
                            Advance(30);
                            break;
                        }
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(9);
                        break;
                    case 30:
                        Check(travel.IsSelectionOpen && arena.Paused, "The market portal did not open the route picker.");
                        travel.CancelSelection();
                        QueueKeys(new KeyboardState());
                        Advance(31);
                        break;
                    case 31:
                        var pillOrb = travel.ActiveMap.transform.Find("丹药摊光球");
                        Check(pillOrb != null && travel.ActiveMap.transform.Find("法器摊光球") != null,
                            "Both market stalls must have an interaction orb.");
                        Check(!travel.ShowShopPrompt, "The shop prompt is visible from the entrance portal.");
                        original.Teleport(pillOrb.position);
                        Check(travel.ShowShopPrompt && !travel.ShowPortalPrompt, "Wrong F prompt at the pill stall.");
                        QueueKeys(new KeyboardState(Key.F));
                        Advance(32);
                        break;
                    case 32:
                        Check(travel.IsShopOpen && arena.Paused && arena.InputBlocked && !arena.PauseMenuOpen,
                            "F at the pill stall did not open the shop.");
                        QueueKeys(new KeyboardState());
                        Advance(33);
                        break;
                    case 33:
                        Check(travel.IsShopOpen, "Releasing F closed the shop.");
                        QueueKeys(new KeyboardState(Key.F));
                        Advance(34);
                        break;
                    case 34:
                        Check(!travel.IsShopOpen && !arena.Paused && arena.InputBlocked,
                            "Pressing F again did not close the shop and consume the held key.");
                        QueueKeys(new KeyboardState());
                        Advance(35);
                        break;
                    case 35:
                        original.Teleport(travel.ActiveMap.transform.Find("法器摊光球").position);
                        Check(travel.ShowShopPrompt, "Missing F prompt at the treasure stall.");
                        QueueKeys(new KeyboardState(Key.F));
                        Advance(36);
                        break;
                    case 36:
                        Check(travel.IsShopOpen && arena.Paused && arena.InputBlocked, "F at the treasure stall did not open the shop.");
                        Check(arena.Resources.SpiritStones == 45, "Browsing the shop changed the spirit stone balance.");
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(37);
                        break;
                    case 37:
                        Check(!travel.IsShopOpen && !arena.Paused, "Esc did not close the treasure stall shop.");
                        QueueKeys(new KeyboardState());
                        Advance(38);
                        break;
                    case 38:
                        Debug.Log("MARKET_SHOP_PASS: F opens at both orbs, F and Esc close the shop, balance is preserved.");
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(9);
                        break;
                    case 20:
                        Check(travel.IsTreatmentOpen && arena.Paused && arena.InputBlocked && !arena.PauseMenuOpen,
                            "F at the bed did not open a modal treatment choice.");
                        Check(SceneTravel.TreatmentCost == 0, "Treatment must be free.");
                        travel.ResolveTreatment(false);
                        Check(original.Health == health && travel.Portal != null && !arena.Paused && !travel.IsTreatmentOpen,
                            "Declining treatment changed health or failed to spawn the portal.");
                        Check(Vector2.Distance(travel.Portal.transform.position, travel.ActiveMap.Definition.Ground(965, 574)) < .01f,
                            "The healing lodge portal did not fit outside the treatment room.");
                        QueueKeys(new KeyboardState());
                        Advance(21);
                        break;
                    case 21:
                        Check(!travel.ShowTreatmentPrompt && !travel.TryOpenTreatment(), "The treatment marker remained usable after resolving.");
                        original.Teleport(travel.Portal.transform.position);
                        QueueKeys(new KeyboardState(Key.F));
                        Advance(22);
                        break;
                    case 22:
                        Check(travel.IsSelectionOpen && arena.Paused, "The lodge portal did not open the standard travel picker.");
                        travel.SelectDestination(TravelDestination.HealingLodge);
                        Check(arena.Resources.ServiceVisits == 0 && travel.ShowsDestination(TravelDestination.Market)
                            && !travel.CanSelectDestination(TravelDestination.Market), "Both services must share the same pool after unlocking.");
                        Check(travel.Portal == null, "Revisiting the lodge reused its previous portal.");
                        original.Teleport(travel.ActiveMap.Definition.Ground(550, 310));
                        original.TakePoisonDamage(original.Health - Mathf.CeilToInt(original.maxHealth * .25f));
                        health = original.Health;
                        QueueKeys(new KeyboardState());
                        Advance(23);
                        break;
                    case 23:
                        Check(travel.TryOpenTreatment(), "The treatment marker did not reappear on a new visit.");
                        travel.ResolveTreatment(true);
                        health += Mathf.CeilToInt(original.maxHealth * .5f);
                        Check(original.Health == health && travel.Portal != null && !travel.HasModal && arena.InputBlocked,
                            "Treatment did not heal half of maximum health and open the portal.");
                        travel.ResolveTreatment(true);
                        Check(original.Health == health, "Treatment could be resolved twice in one visit.");
                        Debug.Log("HEALING_LODGE_TREATMENT_PASS: F interaction, heal and decline, once per visit, courtyard portal and shared travel picker.");
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(9);
                        break;
                    case 9:
                        Check(arena.PauseMenuOpen && arena.Paused, "Esc did not open pause menu while away from home.");
                        swings = original.TotalSwings;
                        QueueMouse(new MouseState().WithButton(MouseButton.Left));
                        arena.ReturnHome();
                        Check(arena.Player == original && original.Health == health, "Return Home reset the player or health.");
                        Check(original.Realm == CultivationRealm.QiRefining && original.RealmDisplayName == "练气",
                            "Return Home did not reset the realm to Qi Refining.");
                        arena.Tuning.ApplyAll();
                        Check(original.Realm == CultivationRealm.QiRefining, "Tuning reapplied the old realm after returning home.");
                        Check(Mathf.Abs(original.moveSpeed - 7.1f) < 0.001f, "Return Home lost character tuning.");
                        Check(travel.Current == TravelDestination.Manor && travel.ActiveMap == null, "Return Home did not unload destination map.");
                        Check(Vector2.Distance(original.transform.position, ManorEnvironment.SpawnPoint) < 0.001f, "Return Home missed the central courtyard.");
                        Check(!arena.PauseMenuOpen && !arena.Paused && Time.timeScale > 0, "Return Home did not resume the game.");
                        QueueKeys(new KeyboardState());
                        Advance(10);
                        break;
                    case 10:
                        Check(original.TotalSwings == swings && arena.InputBlocked, "Return Home click leaked into an attack.");
                        Check(arena.GetComponentInChildren<ManorEnvironment>().gameObject.activeInHierarchy, "Manor was not restored.");
                        foreach (var enemy in arena.Enemies)
                            Check(enemy != null && enemy.gameObject.activeInHierarchy, "Home practice dummies were not restored.");
                        Check(Quaternion.Angle(rotation, arena.WorldCamera.transform.rotation) < 0.001f && arena.View.zoom == zoom,
                            "Return Home changed camera projection.");
                        QueueMouse(new MouseState());
                        Advance(11);
                        break;
                    case 11:
                        Check(!arena.InputBlocked && !travel.IsSelectionOpen, "Controls did not resume after returning home.");
                        Check(arena.Resources.SpiritStones == 45 && arena.Resources.FortunePoints == expectedFortune,
                            "Returning home must preserve both resource balances.");
                        Debug.Log("PAUSE_RETURN_HOME_PASS: " + Destination.destination + "; same player/health/tuning/camera, manor and dummies restored, menu closed, no click-through.");
                        choice++;
                        if (choice <= TravelMaps.Choices.Length)
                        {
                            arena.Restart();
                            Advance(0);
                        }
                        else
                        {
                            arena.Restart();
                            Check(arena.Resources.TryChangeFortunePoints(1 - arena.Resources.FortunePoints), "Could not set up last wilderness trip.");
                            arena.Player.Teleport(SceneTravel.ManorGate.center);
                            Advance(40);
                        }
                        break;
                    case 40:
                        Check(travel.IsSelectionOpen && !travel.CanSelectDestination(TravelDestination.AscensionPlatform),
                            "Picker did not reject the unaffordable ascension trip.");
                        travel.SelectDestination(TravelDestination.Wilderness);
                        Check(arena.Resources.FortunePoints == 0 && travel.Current == TravelDestination.Wilderness,
                            "Last fortune point must pay for the wilderness encounter.");
                        Advance(41);
                        break;
                    case 41:
                        Check(travel.Current == TravelDestination.Wilderness && arena.Enemies.Count > 0,
                            "Exhaustion interrupted an active encounter.");
                        foreach (var enemy in new System.Collections.Generic.List<SlashEnemy>(arena.Enemies))
                            enemy.TakeHit(100000, Vector2.zero, 0);
                        Advance(42);
                        break;
                    case 42:
                        Check(travel.Current == TravelDestination.FinalBattle && travel.ActiveMap != null,
                            "Exhaustion did not send the player to the final arena after combat.");
                        var boss = CheckFinalBoss(arena);
                        boss.TakeHit(100000, Vector2.zero, 0);
                        Check(arena.Enemies.Count == 0 && travel.Portal != null && !boss.GetComponent<ObliqueActor>().GroundWarning.gameObject.activeSelf,
                            "Final boss defeat did not clear combat and create the exit portal.");
                        var exit = travel.Portal;
                        travel.EnemyDefeated(boss);
                        Check(travel.Portal == exit, "Final boss defeat created more than one exit portal.");
                        arena.Player.Teleport(exit.transform.position);
                        Advance(43);
                        break;
                    case 43:
                        Check(travel.TryOpenPortal(), "Victory portal did not reopen travel.");
                        travel.SelectDestination(TravelDestination.FinalBattle);
                        CheckFinalBoss(arena);
                        SessionState.SetBool(Pending, false);
                        InputSystem.onBeforeUpdate -= InjectGameplayInput;
                        InputSystem.RemoveDevice(mouse);
                        InputSystem.RemoveDevice(keyboard);
                        Debug.Log("CULTIVATION_REALM_PASS: starts/restarts in Qi Refining, all three higher realms survive outbound travel and tuning, every return home resets to Qi Refining.");
                        Debug.Log("SCENE_TRAVEL_PASS: all destinations, gate/cancel/rearm, R/T disabled, reserved exits have no destination, market portal and shop access.");
                        Debug.Log("FORTUNE_TRAVEL_PASS: route costs, shared visits, voluntary and automatic final battle travel.");
                        Debug.Log("FINAL_BOSS_PASS: Blood Saber on each entry path, 2000 base health, half-damage ambush, 10x lifesteal, five rotation skills, eight movement facings, and a single victory portal.");
                        EditorApplication.Exit(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                SessionState.SetBool(Pending, false);
                InputSystem.onBeforeUpdate -= InjectGameplayInput;
                Debug.LogError("SCENE_TRAVEL_FAIL: " + exception);
                EditorApplication.Exit(1);
            }
        }

        static SlashEnemy CheckFinalBoss(SlashArena arena, bool exerciseSkills = false)
        {
            var travel = arena.Travel;
            Check(travel.Current == TravelDestination.FinalBattle && travel.Portal == null && arena.Enemies.Count == 1,
                "Final battle must begin with one live opponent and no exit portal.");
            var enemy = arena.Enemies[0];
            var boss = enemy.GetComponent<EvilCultivatorBoss>();
            Check(enemy.Alive && boss != null && boss.CombatStyle == EvilCultivatorBoss.Style.BloodSaber
                && boss.transform.IsChildOf(travel.ActiveMap.transform),
                "The final battle did not spawn Blood Saber in its map.");
            int expectedHealth = Mathf.RoundToInt(2000 * arena.Tuning.Settings.enemyHealthMultiplier);
            Check(enemy.maxHealth == expectedHealth, "Blood Saber did not use 2000 as its base health.");
            var skin = boss.transform.Find("Visual/Boss art").GetComponent<SpriteRenderer>();
            Check(skin.sprite.texture == Resources.Load<Texture2D>("SlashDemo/Cultivators/BloodSaberCultivator-Directions-v1"),
                "The final boss is using the wrong artwork.");
            Check(boss.GetComponent<WildernessNavigation>().Walkable(boss.transform.position),
                "Final boss spawn overlaps the battlefield boundary.");
            if (exerciseSkills)
            {
                CheckBossLocomotion(arena, enemy, skin);
                CheckBossSkills(arena, enemy, boss);
                CheckBloodMist(arena, enemy, boss, skin);
            }
            return enemy;
        }

        static void CheckBloodMist(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss, SpriteRenderer skin)
        {
            var player = arena.Player;
            var body = boss.GetComponent<Rigidbody2D>();
            var navigation = boss.GetComponent<WildernessNavigation>();
            var warning = boss.GetComponent<ObliqueActor>().GroundWarning;
            var immunity = typeof(SlashPlayer).GetField("invulnerability",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var aim = typeof(SlashPlayer).GetProperty(nameof(SlashPlayer.Aim));
            Vector2 start = body.position, previousPlayerPosition = player.transform.position;
            // Stay beyond the regular rotation's range to isolate the ambush timer.
            Vector2 distant = start + Vector2.right * 7f;
            Vector2 previousAim = player.Aim;
            object previousImmunity = immunity.GetValue(player);
            int previousHealth = player.Health, previousDamage = enemy.damage;
            int previousBossHealth = enemy.Health;
            bool invincible = player.invincible;
            GameObject blocker = null;

            void StartAmbush()
            {
                body.position = boss.transform.position = start;
                boss.SendMessage("Stop");
                player.Teleport(distant);
                aim.SetValue(player, Vector2.left);
                Physics2D.SyncTransforms();
                boss.SendMessage("Tick", 4.99f);
                Check(enemy.IsChasing && !warning.gameObject.activeSelf,
                    "Blood Saber used the ambush in the first five seconds.");
                boss.SendMessage("Tick", .02f);
                Check(boss.CastingSkill == "血雾突袭" && warning.gameObject.activeSelf,
                    "Blood Saber did not telegraph the ambush at range.");
                boss.SendMessage("Tick", .30f);
                Check(skin.color.a < .3f && Vector2.Distance(body.position, start) < .01f,
                    "Blood Saber moved before dissolving into mist.");
                boss.SendMessage("Tick", .13f);
                Check(skin.color.a == 1 && navigation.Walkable(body.position) && body.position.x > distant.x + .4f
                    && warning.gameObject.activeSelf && boss.CastingSkill == "血雾突袭",
                    "Blood Saber did not reappear behind the player with a slash warning.");
            }

            try
            {
                player.invincible = false;
                immunity.SetValue(player, 0f);
                enemy.damage = 9;
                enemy.TakeHit(100, Vector2.zero, 0);
                int bossHealthBeforeSlash = enemy.Health;
                StartAmbush();
                Vector2 unobstructed = body.position;
                Check(Mathf.Abs(unobstructed.y - distant.y) < .05f && player.Health == previousHealth,
                    "The open rear landing was offset or dealt damage on arrival.");
                boss.SendMessage("Tick", .39f);
                Check(player.Health == previousHealth && warning.gameObject.activeSelf,
                    "The quick slash resolved before its 0.4-second warning ended.");
                boss.SendMessage("Tick", .02f);
                Check(player.Health == previousHealth - 5 && !warning.gameObject.activeSelf,
                    "The quick slash did not hit once for half damage at the end of its warning.");
                Check(enemy.Health == bossHealthBeforeSlash + 50,
                    "Blood Saber did not heal for 10 times the damage actually dealt.");
                boss.SendMessage("Tick", .51f);
                player.Teleport(start);
                boss.SendMessage("Tick", .04f);
                Check(boss.CastingSkill != "血雾突袭", "Blood Saber repeated the ambush during cooldown.");
                player.Heal(5);
                immunity.SetValue(player, 0f);

                blocker = new GameObject("Blood mist blocked landing");
                blocker.transform.SetParent(arena.Travel.ActiveMap.transform, false);
                blocker.transform.position = unobstructed;
                blocker.AddComponent<CircleCollider2D>().radius = .22f;
                StartAmbush();
                Check(Vector2.Distance(body.position, unobstructed) > .65f,
                    "Blood Saber blinked into the blocked rear landing.");
                player.Teleport(start);
                boss.SendMessage("Tick", .41f);
                Check(player.Health == previousHealth && !warning.gameObject.activeSelf,
                    "The slash tracked the player out of the warned area or failed to resolve.");
            }
            finally
            {
                if (blocker != null) UnityEngine.Object.DestroyImmediate(blocker);
                body.position = boss.transform.position = start;
                boss.SendMessage("Stop");
                player.Heal(previousHealth - player.Health);
                enemy.SendMessage("Heal", previousBossHealth - enemy.Health);
                immunity.SetValue(player, previousImmunity);
                aim.SetValue(player, previousAim);
                player.invincible = invincible;
                enemy.damage = previousDamage;
                player.Teleport(previousPlayerPosition);
                Physics2D.SyncTransforms();
            }
        }

        static void CheckBossSkills(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss)
        {
            var player = arena.Player;
            Vector2 originalPosition = player.transform.position;
            bool invincible = player.invincible;
            bool bell = boss.CombatStyle == EvilCultivatorBoss.Style.SoulBell;
            var skills = bell
                ? new[] { "摄魂铃震", "十字断魄", "三叠追魂", "镇魂回响", "四方锁魂阵" }
                : new[] { "饮血横斩", "贯血刀", "回锋双斩", "裂地血潮", "三途飞刃" };
            int[][] outlines = bell
                ? new[] { new[] { 1 }, new[] { 2 }, new[] { 1, 1, 1 }, new[] { 2 }, new[] { 4, 1 } }
                : new[] { new[] { 1 }, new[] { 1 }, new[] { 1, 1 }, new[] { 1, 1 }, new[] { 3 } };
            var warning = boss.GetComponent<ObliqueActor>().GroundWarning;
            var lines = warning.GetComponentsInChildren<LineRenderer>(true);
            var gait = boss.GetComponentInChildren<HeroWalkMesh>();
            var skin = gait.GetComponent<SpriteRenderer>();
            var model = boss.transform.Find("Visual");
            var map = arena.Travel.ActiveMap;
            var effects = map.transform.Find("Final boss skill effects");
            Check(effects != null, "Final boss did not initialize its skill effects.");
            var weaponGlow = effects.GetChild(0).GetComponent<LineRenderer>();
            var whirlwind = bell ? null : effects.Find("Saber whirlwind").GetComponent<LineRenderer>();
            var particles = map.GetComponent<BeastAttackParticles>();
            var saberLayer = bell ? null : skin.transform.Find("Saber arm");
            Check(bell || saberLayer != null, "Blood Saber is missing its separately rendered sword arm.");
            var armMesh = bell ? gait.DeformedMesh : saberLayer.GetComponent<MeshFilter>().sharedMesh;
            var armRenderer = bell ? null : saberLayer.GetComponent<MeshRenderer>();
            var bladeLayer = bell ? null : skin.transform.Find("Saber blade");
            Check(bell || bladeLayer != null, "Blood Saber is missing its rigid blade.");
            var bladeMesh = bell ? null : bladeLayer.GetComponent<MeshFilter>().sharedMesh;
            var supportLayer = bell ? null : skin.transform.Find("Saber support arm");
            Check(bell || supportLayer != null, "Blood Saber is missing its second arm.");
            var supportRenderer = bell ? null : supportLayer.GetComponent<MeshRenderer>();
            var supportMesh = bell ? null : supportLayer.GetComponent<MeshFilter>().sharedMesh;
            player.invincible = true;
            Vector2 front = (Vector2)boss.transform.position + Vector2.down * 1.3f;
            Vector2 flank = (Vector2)boss.transform.position + Vector2.right * 1.3f;
            float followStep = bell ? .07f : .07f / 1.5f;
            for (int skill = 0; skill < skills.Length; skill++)
            {
                bool spins = !bell && skills[skill] == "裂地血潮";
                bool sweeps = !bell && (skills[skill] == "饮血横斩" || skills[skill] == "回锋双斩");
                player.Teleport(front);
                boss.SendMessage("Tick", .04f);
                Check(boss.CastingSkill == skills[skill], "Final boss skipped " + skills[skill]);
                gait.Refresh();
                Sprite castingFacing = skin.sprite;
                var rest = gait.DeformedMesh.vertices;
                var triangles = gait.DeformedMesh.GetTriangles(0);
                int grip = NearestVertex(rest, bell ? new Vector2(-.199f, .857f) : new Vector2(-.147f, .464f));
                int boot = NearestVertex(rest, new Vector2(0, .033f));
                int bladeTip = bell ? 0 : NearestVertex(bladeMesh.vertices, new Vector2(-.195f, -.288f));
                Vector3 blade = bell ? Vector3.zero : bladeMesh.vertices[bladeTip];
                int supportGrip = bell ? 0 : NearestVertex(supportMesh.vertices, new Vector2(.145f, .446f));
                if (!bell)
                    Check(blade.magnitude > .325f,
                        "Blood Saber did not equip the longer blade.");
                int[] waist = bell ? null : SaberTorsoVertices(rest, 0);
                int[] core = {
                    NearestVertex(rest, new Vector2(-.045f, .66f)), NearestVertex(rest, new Vector2(.045f, .66f)),
                    NearestVertex(rest, new Vector2(-.045f, .86f)), NearestVertex(rest, new Vector2(.045f, .86f)),
                    NearestVertex(rest, new Vector2(0, .93f))
                };
                for (int wave = 0; wave < outlines[skill].Length; wave++)
                {
                    int count = 0;
                    LineRenderer primary = null;
                    foreach (var line in lines)
                    {
                        if (line.name != "Telegraph" || !line.enabled) continue;
                        primary ??= line;
                        count++;
                    }
                    Check(warning.gameObject.activeSelf && count == outlines[skill][wave],
                        "Final boss drew the wrong warning count for " + skills[skill]);
                    Vector3 sweepStart = Vector3.zero, sweepEnd = Vector3.zero;
                    if (sweeps)
                    {
                        Vector3 center = primary.GetPosition(0);
                        sweepStart = primary.GetPosition(1) - center;
                        sweepEnd = primary.GetPosition(primary.positionCount - 1) - center;
                        if (wave == 1) (sweepStart, sweepEnd) = (sweepEnd, sweepStart);
                    }
                    Check(weaponGlow.enabled, "Final boss did not charge its weapon for " + skills[skill]);
                    Vector3 glowStart = weaponGlow.GetPosition(0);
                    gait.Refresh();
                    var start = gait.DeformedMesh.vertices;
                    var armStart = armMesh.vertices;
                    Vector3 tipStart = bell ? Vector3.zero : BladeTip(bladeLayer, blade);
                    Quaternion planted = model.localRotation;
                    float readyStep = .26f;
                    float timeToImpact = 0;
                    if (!bell)
                    {
                        var castTime = typeof(EvilCultivatorBoss).GetField("windup",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        float duration = (float)castTime.GetValue(boss);
                        float readyAge = spins ? duration - .30f : Mathf.Min(.68f * duration, duration - .28f);
                        readyStep = readyAge - (wave > 0 ? followStep : 0);
                        timeToImpact = duration - readyAge;
                    }
                    boss.SendMessage("Tick", readyStep);
                    gait.Refresh();
                    var ready = gait.DeformedMesh.vertices;
                    var armReady = armMesh.vertices;
                    Vector3 gripReady = bell ? Vector3.zero : bladeLayer.localPosition;
                    Vector3 tipReady = bell ? Vector3.zero : BladeTip(bladeLayer, blade);
                    Check(boss.CastingSkill == skills[skill], "Final boss left windup too early during " + skills[skill]);
                    Check(bell || armRenderer.enabled, "Blood Saber did not display its separate arm during windup.");
                    if (wave == 0)
                        Check((armReady[grip] - armStart[grip]).sqrMagnitude > .000001f,
                            "Final boss did not move the painted weapon during " + skills[skill]);
                    Check((ready[boot] - start[boot]).sqrMagnitude < .000001f, "Final boss moved its planted feet.");
                    Check(Quaternion.Angle(planted, model.localRotation) < .1f, "Final boss pitched its entire model during windup.");
                    Check((weaponGlow.GetPosition(0) - glowStart).sqrMagnitude > .000001f,
                        "Final boss weapon glow did not follow its attack animation.");
                    CheckCastingMesh(rest, ready, triangles, skills[skill]);
                    CheckCastingCore(rest, ready, core, skills[skill]);
                    if (!bell)
                    {
                        CheckBladeLength(blade, gripReady, tipReady);
                        CheckCastingCore(rest, ready, waist, skills[skill]);
                        if (sweeps) CheckSaberSweep(bladeLayer.parent, gripReady, tipReady, sweepStart, skills[skill]);
                    }
                    if (wave + 1 < outlines[skill].Length)
                        player.Teleport(wave % 2 == 0 ? flank : front);
                    if (spins) CheckWhirlwind(boss, skin, whirlwind, warning, timeToImpact);
                    if (spins && wave == 1) CheckBloodTideCenter(boss, enemy, player);
                    else boss.SendMessage("Tick", 4f);
                    gait.Refresh();
                    var contact = gait.DeformedMesh.vertices;
                    var armContact = armMesh.vertices;
                    Check(skin.sprite == castingFacing, "Final boss changed its body facing between skill waves.");
                    CheckCastingMesh(rest, contact, triangles, skills[skill]);
                    CheckCastingCore(rest, contact, core, skills[skill]);
                    if (!bell)
                    {
                        Vector3 gripContact = bladeLayer.localPosition;
                        Vector3 tipContact = BladeTip(bladeLayer, blade);
                        CheckBladeLength(blade, gripContact, tipContact);
                        Check(supportRenderer.enabled, "Blood Saber's support arm did not join the attack.");
                        CheckTwoHandedGrip(gripContact, tipContact, supportMesh.vertices[supportGrip], skills[skill]);
                        CheckCastingCore(rest, contact, waist, skills[skill]);
                        if (sweeps) CheckSaberSweep(bladeLayer.parent, gripContact, tipContact, sweepEnd, skills[skill]);
                        Check(Mathf.Max((tipContact - tipReady).sqrMagnitude, (tipReady - tipStart).sqrMagnitude) > .01f,
                            "Blood Saber's blade did not travel through the attack during " + skills[skill]);
                        if (sweeps)
                            Check(Vector3.Angle(tipReady - gripReady, tipContact - gripContact) > 18,
                                "Blood Saber moved its grip without swinging the blade during the sweep.");
                        Check(spins ? !whirlwind.enabled : sweeps || (tipContact - gripContact).y < -.15f,
                            "Blood Saber did not finish its attack animation during " + skills[skill]);
                    }
                    Quaternion bladeContact = bell ? Quaternion.identity : bladeLayer.localRotation;
                    Check((armContact[grip] - armReady[grip]).sqrMagnitude > .0001f,
                        "Final boss did not reach a separate contact pose for " + skills[skill]);
                    Check(Quaternion.Angle(planted, model.localRotation) < .1f, "Final boss pitched its entire model at impact.");
                    int active = CountBossEffectLines(effects);
                    Check(active > (boss.CastingSkill == null ? 0 : 2) && particles.ActiveCount > 0,
                        "Final boss did not release contact effects for " + skills[skill]);
                    boss.SendMessage("Tick", followStep);
                    gait.Refresh();
                    var follow = gait.DeformedMesh.vertices;
                    var armFollow = armMesh.vertices;
                    CheckCastingMesh(rest, follow, triangles, skills[skill]);
                    CheckCastingCore(rest, follow, core, skills[skill]);
                    if (!bell)
                    {
                        Vector3 gripFollow = bladeLayer.localPosition;
                        Vector3 tipFollow = BladeTip(bladeLayer, blade);
                        CheckBladeLength(blade, gripFollow, tipFollow);
                        CheckTwoHandedGrip(gripFollow, tipFollow, supportMesh.vertices[supportGrip], skills[skill]);
                        CheckCastingCore(rest, follow, waist, skills[skill]);
                        Check(Quaternion.Angle(bladeContact, bladeLayer.localRotation) > 1,
                            "Blood Saber did not follow through with its blade during " + skills[skill]);
                    }
                    Check((armFollow[grip] - armContact[grip]).sqrMagnitude > .000001f,
                        "Final boss did not follow through after impact for " + skills[skill]);
                    Check((follow[boot] - start[boot]).sqrMagnitude < .000001f
                        && Quaternion.Angle(planted, model.localRotation) < .1f, "Final boss lost its stance during follow-through.");
                }
                Check(boss.CastingSkill == null && !enemy.IsChasing, "Final boss skipped recovery.");
                boss.SendMessage("Tick", 4f);
                gait.Refresh();
                Check(enemy.IsChasing && !warning.gameObject.activeSelf, "Final boss left a stale impact.");
                Check(bell || (armRenderer.enabled && !supportRenderer.enabled),
                    "Blood Saber did not return to its one-handed carry after recovery.");
                Check(CountBossEffectLines(effects) == 0, "Final boss left stale skill effects.");
            }
            boss.SendMessage("Tick", .04f);
            Check(warning.gameObject.activeSelf, "Final boss stopped using skills after its first rotation.");
            boss.SendMessage("Stop");
            Check(!warning.gameObject.activeSelf && boss.CastingSkill == null, "Final boss retained a warning after stopping.");
            Check(CountBossEffectLines(effects) == 0 && particles.ActiveCount == 0, "Final boss retained skill effects after stopping.");
            if (!bell) CheckSaberTorsoFacings(arena, enemy, boss, skin, gait,
                effects.Find("Saber swing trail").GetComponent<LineRenderer>());
            player.Teleport(originalPosition);
            player.invincible = invincible;
        }

        static void CheckCastingMesh(Vector3[] rest, Vector3[] pose, int[] triangles, string skill)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                float restArea = Vector3.Cross(rest[b] - rest[a], rest[c] - rest[a]).z;
                float posedArea = Vector3.Cross(pose[b] - pose[a], pose[c] - pose[a]).z;
                float ratio = posedArea / restArea;
                if (!(ratio > .25f && ratio < 1.75f))
                    throw new Exception($"Final boss body mesh folded or stretched during {skill} at {rest[a]:F3} (area ratio {ratio:F2}).");
            }
        }

        static void CheckCastingCore(Vector3[] rest, Vector3[] pose, int[] core, string skill)
        {
            Vector3 translation = pose[core[0]] - rest[core[0]];
            foreach (int vertex in core)
                Check((pose[vertex] - rest[vertex] - translation).sqrMagnitude < .00000001f,
                    "Final boss stretched its body or clothing during " + skill);
        }

        static int[] SaberTorsoVertices(Vector3[] rest, int facing)
        {
            // Probe fabric clear of the blade where it crosses the lower silhouette in profile.
            float tunicX = facing == 2 ? -.103f : facing == 3 ? -.025f : facing == 6 ? .055f : 0;
            return new[]
            {
                NearestVertex(rest, new Vector2(0, .86f)),
                NearestVertex(rest, new Vector2(-.05f, .53f)), NearestVertex(rest, new Vector2(.05f, .53f)),
                NearestVertex(rest, new Vector2(-.065f, .59f)), NearestVertex(rest, new Vector2(.065f, .59f)),
                NearestVertex(rest, new Vector2(tunicX - .022f, .39f)),
                NearestVertex(rest, new Vector2(tunicX + .022f, .39f)),
                NearestVertex(rest, new Vector2(tunicX, .325f))
            };
        }

        static void CheckSaberTorsoFacings(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss,
            SpriteRenderer skin, HeroWalkMesh gait, LineRenderer bladeTrail)
        {
            var atlas = skin.sprite.texture;
            var saberMesh = skin.transform.Find("Saber arm").GetComponent<MeshFilter>().sharedMesh;
            var bladeLayer = skin.transform.Find("Saber blade");
            var bladeMesh = bladeLayer.GetComponent<MeshFilter>().sharedMesh;
            float nearArmLength = 0;
            for (int facing = 0; facing < 8; facing++)
            {
                float angle = (facing * 45 - 90) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 ground = ObliqueProjection.UnprojectGround(direction, arena.View.elevation);
                // Stand past the horizontal cut's trigger range to exercise the straight-arm chop.
                arena.Player.Teleport((Vector2)boss.transform.position + ground.normalized * (enemy.attackRange * 1.2f));
                boss.SendMessage("Tick", .04f);
                Check(boss.CastingSkill == "贯血刀" && skin.sprite.rect.x == facing % 4 * atlas.width / 4
                    && skin.sprite.rect.y == (1 - facing / 4) * atlas.height / 2,
                    "Blood Saber did not start casting in facing " + facing);
                gait.Refresh();
                var rest = gait.DeformedMesh.vertices;
                int[] torso = SaberTorsoVertices(rest, facing);
                var triangles = gait.DeformedMesh.GetTriangles(0);
                string casting = "Blood Saber facing " + facing;
                bool profile = facing == 2 || facing == 6;
                var armRest = profile ? saberMesh.vertices : null;
                int hand = profile ? NearestVertex(armRest, facing == 2 ? new Vector2(-.002f, .442f)
                    : new Vector2(-.058f, .460f)) : 0;
                var weaponVertices = bladeMesh.vertices;
                Vector3 blade = profile ? weaponVertices[NearestVertex(weaponVertices,
                    facing == 2 ? new Vector2(.193f, -.320f) : new Vector2(-.143f, -.233f))] : Vector3.zero;
                int shoulder = profile ? NearestVertex(armRest, facing == 2 ? new Vector2(-.009f, .672f)
                    : new Vector2(-.007f, .658f)) : 0;
                int elbow = profile ? NearestVertex(armRest, facing == 2 ? new Vector2(-.011f, .556f)
                    : new Vector2(-.022f, .563f)) : 0;
                if (profile)
                {
                    float armLength = Vector3.Distance(armRest[shoulder], armRest[elbow])
                        + Vector3.Distance(armRest[elbow], armRest[hand]);
                    if (facing == 2) nearArmLength = armLength;
                    else Check(armLength < nearArmLength * .97f, "Blood Saber's far arm was longer than its near arm.");
                }
                bool showedSlash = false, checkedStraightArms = false;
                float peakGrip = float.NegativeInfinity;
                for (int frame = 0; frame < 80 && !enemy.IsChasing; frame++)
                {
                    bool windingUp = boss.CastingSkill != null;
                    boss.SendMessage("Tick", .025f);
                    gait.Refresh();
                    var pose = gait.DeformedMesh.vertices;
                    CheckCastingCore(rest, pose, torso, casting);
                    CheckCastingMesh(rest, pose, triangles, casting);
                    if (profile && windingUp)
                    {
                        Vector3 grip = bladeLayer.localPosition;
                        Vector3 tip = BladeTip(bladeLayer, blade);
                        float forward = facing == 2 ? 1 : -1;
                        peakGrip = Mathf.Max(peakGrip, grip.y);
                        CheckBladeLength(blade, grip, tip);
                        if (bladeTrail.enabled)
                        {
                            var sleeve = saberMesh.vertices;
                            Vector3 upper = sleeve[elbow] - sleeve[shoulder];
                            Vector3 forearm = sleeve[hand] - sleeve[elbow];
                            Check(Vector3.Angle(upper, forearm) < 22,
                                "Blood Saber's arm bent during its straight-arm chop in facing " + facing);
                            checkedStraightArms = true;
                        }
                        if (boss.CastingSkill == null)
                        {
                            Check((tip.x - grip.x) * forward > .1f,
                                "Blood Saber did not chop in front of its torso in profile " + facing);
                            Check(peakGrip > .76f && peakGrip < .84f && grip.y > .52f,
                                $"Blood Saber's grip fell outside its chop height in profile {facing} ({peakGrip:F3}, {grip.y:F3}).");
                            Check((saberMesh.vertices[hand] - grip).sqrMagnitude < .0005f,
                                "Blood Saber's main hand slipped off the handle in profile " + facing);
                        }
                    }
                    showedSlash |= bladeTrail.enabled && bladeTrail.positionCount >= 2;
                }
                Check(enemy.IsChasing, "Blood Saber did not finish casting in facing " + facing);
                Check(!profile || checkedStraightArms, "Blood Saber did not show its extended arms in facing " + facing);
                Check(showedSlash && !bladeTrail.enabled, "Blood Saber's blade trail did not appear and expire in facing " + facing);
                boss.SendMessage("Stop");
            }
        }

        static void CheckBloodTideCenter(EvilCultivatorBoss boss, SlashEnemy enemy, SlashPlayer player)
        {
            var immunity = typeof(SlashPlayer).GetField("invulnerability",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object previousImmunity = immunity.GetValue(player);
            Vector2 previousPosition = player.transform.position;
            int previousHealth = player.Health, previousDamage = enemy.damage;
            bool invincible = player.invincible;
            try
            {
                player.Teleport((Vector2)boss.transform.position + Vector2.down * .5f);
                immunity.SetValue(player, 0f);
                player.invincible = false;
                enemy.damage = 8;
                boss.SendMessage("Tick", 4f);
                Check(player.Health == previousHealth - 5, "Blood Tide's second wave did not hit inside its former inner ring.");
            }
            finally
            {
                enemy.damage = previousDamage;
                player.invincible = invincible;
                immunity.SetValue(player, previousImmunity);
                player.Heal(previousHealth - player.Health);
                player.Teleport(previousPosition);
            }
        }

        static void CheckWhirlwind(EvilCultivatorBoss boss, SpriteRenderer skin, LineRenderer bladeArc,
            Transform warning, float timeToImpact)
        {
            var facings = new System.Collections.Generic.HashSet<Sprite> { skin.sprite };
            bool showedArc = false;
            const float step = .02f;
            while (timeToImpact > step * 2)
            {
                boss.SendMessage("Tick", step);
                timeToImpact -= step;
                facings.Add(skin.sprite);
                showedArc |= bladeArc.enabled;
            }
            Check(facings.Count == 8 && showedArc, "Blood Saber did not finish a full whirlwind with a visible blade arc.");
            Check(warning.gameObject.activeSelf && boss.CastingSkill == "裂地血潮",
                "Blood Saber's circle resolved before the whirlwind completed.");
        }

        static Vector3 BladeTip(Transform bladeLayer, Vector3 blade)
            => bladeLayer.localPosition + bladeLayer.localRotation * Vector3.Scale(blade, bladeLayer.localScale);

        static void CheckBladeLength(Vector3 blade, Vector3 grip, Vector3 tip)
        {
            Check(Mathf.Abs(blade.magnitude - Vector3.Distance(grip, tip)) < .001f,
                "Blood Saber blade stretched during the arm motion.");
        }

        static void CheckSaberSweep(Transform art, Vector3 grip, Vector3 tip, Vector3 edge, string skill)
        {
            Vector3 bladeDirection = art.TransformDirection(tip - grip);
            Check(Vector3.Angle(bladeDirection, edge) < 12,
                $"Blood Saber's blade did not align with the fan edge during {skill} ({bladeDirection:F3}, {edge:F3}).");
        }

        static void CheckTwoHandedGrip(Vector3 grip, Vector3 tip, Vector3 support, string skill)
        {
            Vector3 axis = (tip - grip).normalized;
            Vector3 expected = grip - axis * .043f;
            Check(((Vector2)(support - expected)).sqrMagnitude < .00065f,
                $"Blood Saber's second hand slipped off the handle during {skill} ({support:F3}, expected {expected:F3}).");
        }

        static int CountBossEffectLines(Transform effects)
        {
            int count = 0;
            foreach (var line in effects.GetComponentsInChildren<LineRenderer>())
                if (line.enabled) count++;
            return count;
        }

        static void CheckBossLocomotion(SlashArena arena, SlashEnemy enemy, SpriteRenderer skin)
        {
            const float step = .04f;
            var body = enemy.GetComponent<Rigidbody2D>();
            var gait = skin.GetComponent<HeroWalkMesh>();
            Check(gait != null && gait.WalkingRenderer.enabled && skin.forceRenderingOff,
                "Final boss did not initialize its walking mesh.");
            Vector2 spawn = body.position;
            Vector2 initialVelocity = body.linearVelocity;
            Vector2 playerPosition = arena.Player.transform.position;
            float attackRange = enemy.attackRange;
            bool usesBell = enemy.GetComponent<EvilCultivatorBoss>().CombatStyle == EvilCultivatorBoss.Style.SoulBell;
            var saberLayer = usesBell ? null : skin.transform.Find("Saber arm");
            Check(usesBell || saberLayer != null, "Blood Saber is missing its separate arm for the walking pose.");
            var saberMesh = saberLayer != null ? saberLayer.GetComponent<MeshFilter>().sharedMesh : null;
            var saberRenderer = saberLayer != null ? saberLayer.GetComponent<MeshRenderer>() : null;
            var bladeLayer = usesBell ? null : skin.transform.Find("Saber blade");
            Check(usesBell || bladeLayer != null, "Blood Saber is missing its rigid blade for the walking pose.");
            var bladeRenderer = bladeLayer != null ? bladeLayer.GetComponent<MeshRenderer>() : null;
            var bodyProperties = new MaterialPropertyBlock();
            float frontFootX = usesBell ? .025f : .073f;
            var center = arena.Travel.ActiveMap.Definition.Ground(755, 500);
            enemy.attackRange = 0;
            var atlas = skin.sprite.texture;
            for (int i = 0; i < 8; i++)
            {
                body.position = center;
                float angle = (i * 45 - 90) * Mathf.Deg2Rad;
                var screen = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var ground = ObliqueProjection.UnprojectGround(screen, arena.View.elevation);
                arena.Player.Teleport(center + ground.normalized * 2);
                Physics2D.SyncTransforms();
                enemy.SendMessage("FixedUpdate");
                Check(body.linearVelocity.sqrMagnitude > .01f, "Final boss did not move for direction " + i);
                Check(skin.sprite.texture == atlas && skin.sprite.rect.x == i % 4 * atlas.width / 4
                    && skin.sprite.rect.y == (1 - i / 4) * atlas.height / 2,
                    "Final boss displayed the wrong movement direction " + i);
                enemy.SendMessage("Stop");
                gait.Refresh();
                var resting = gait.DeformedMesh.vertices;
                var restingSaber = saberMesh != null ? saberMesh.vertices : null;
                Vector3 restingGrip = bladeLayer != null ? bladeLayer.localPosition : Vector3.zero;
                bool profile = i == 2 || i == 6;
                bool bellProfile = usesBell && profile;
                float trailingHemX = i == 2 ? -.19f : .19f;
                int hem = NearestVertex(resting, new Vector2(trailingHemX, .065f));
                int drape = NearestVertex(resting, new Vector2(trailingHemX, .22f));
                var farLeg = skin.transform.Find("Far profile leg");
                var farRenderer = farLeg != null ? farLeg.GetComponent<MeshRenderer>() : null;
                var farMesh = profile && farLeg != null ? farLeg.GetComponent<MeshFilter>().sharedMesh : null;
                Check(!profile || farMesh != null, "Final boss is missing its far profile leg.");
                Check(farRenderer == null || !farRenderer.enabled, "Far profile leg is visible in the resting pose.");
                Vector3 farRest = profile ? farMesh.vertices[0] : Vector3.zero;
                Vector2 velocity = body.linearVelocity;
                var triangles = gait.DeformedMesh.GetTriangles(0);
                int leftBoot = NearestVertex(resting, new Vector2(-frontFootX, .05f));
                int rightBoot = NearestVertex(resting, new Vector2(frontFootX, .05f));
                int shin = NearestVertex(resting, new Vector2(0, .25f));
                float leftLift = 0, rightLift = 0, forwardSwing = 0, shinBend = 0;
                float farAhead = 0, farBehind = 0;
                bool tookStep = false;
                for (int frame = 0; frame < 40; frame++)
                {
                    body.position += velocity * step;
                    enemy.SendMessage("Tick", step);
                    gait.Refresh();
                    var walking = gait.DeformedMesh.vertices;
                    if (saberMesh != null)
                    {
                        gait.WalkingRenderer.GetPropertyBlock(bodyProperties);
                        Check(saberRenderer.enabled && bladeRenderer.enabled && bodyProperties.GetFloat("_SaberCutMode") == 1,
                            "Blood Saber did not use its separate knife layer while walking.");
                        var walkingSaber = saberMesh.vertices;
                        CheckRigidSaberCarry(restingSaber, walkingSaber, i);
                        Vector3 carry = walkingSaber[0] - restingSaber[0];
                        Check((bladeLayer.localPosition - restingGrip - carry).sqrMagnitude < .000001f
                            && Quaternion.Angle(bladeLayer.localRotation, Quaternion.identity) < .1f
                            && bladeLayer.localScale == Vector3.one,
                            "Blood Saber's blade changed shape or slipped off the arm while walking in facing " + i);
                    }
                    tookStep |= HasBossStep(resting, walking);
                    Vector3 leftOffset = walking[leftBoot] - resting[leftBoot];
                    Vector3 rightOffset = walking[rightBoot] - resting[rightBoot];
                    leftLift = Mathf.Max(leftLift, leftOffset.y - rightOffset.y);
                    rightLift = Mathf.Max(rightLift, rightOffset.y - leftOffset.y);
                    forwardSwing = Mathf.Max(forwardSwing, Mathf.Abs(leftOffset.x));
                    shinBend = Mathf.Max(shinBend, (leftOffset - (walking[shin] - resting[shin])).magnitude);
                    CheckBossBootShape(resting, walking, triangles, i, bellProfile);
                    if (bellProfile)
                        Check((walking[hem] - resting[hem]).sqrMagnitude < .000225f
                            && (walking[drape] - resting[drape]).sqrMagnitude < .000225f,
                            "Soul Bell's robe is following the leg stride in profile " + i);
                    if (profile)
                    {
                        Check(farRenderer.enabled && farRenderer.sortingOrder < gait.WalkingRenderer.sortingOrder,
                            "Final boss far leg must render behind the walking portrait.");
                        float separation = farMesh.vertices[0].x - farRest.x - leftOffset.x;
                        farAhead = Mathf.Max(farAhead, separation);
                        farBehind = Mathf.Max(farBehind, -separation);
                    }
                }
                Check(gait.Phase > 0 && tookStep,
                    "Final boss did not deform the lower body while moving in direction " + i);
                if (i == 0)
                    Check(leftLift > .045f && rightLift > .045f, "Final boss did not visibly lift each foot in turn.");
                if (profile)
                {
                    Check(forwardSwing > .05f && shinBend > .035f, "Final boss did not swing and bend its visible leg in profile.");
                    Check(farAhead > .08f && farBehind > .08f, "Final boss profile legs did not pass and visibly separate.");
                }

                float phase = gait.Phase;
                for (int frame = 0; frame < 60; frame++) enemy.SendMessage("Tick", step);
                gait.Refresh();
                Check(gait.Phase == phase && gait.Bob == 0 && !HasBossStep(resting, gait.DeformedMesh.vertices),
                    "Final boss gait did not settle after movement stopped.");
                Check(farRenderer == null || !farRenderer.enabled, "Far profile leg did not settle into the resting portrait.");
                if (saberRenderer != null)
                {
                    gait.WalkingRenderer.GetPropertyBlock(bodyProperties);
                    Check(saberRenderer.enabled && bodyProperties.GetFloat("_SaberCutMode") == 1,
                        "Blood Saber changed back to the shorter baked blade after stopping.");
                }
            }

            var last = skin.sprite;
            body.position = center;
            arena.Player.Teleport(center);
            Physics2D.SyncTransforms();
            enemy.SendMessage("FixedUpdate");
            Check(body.linearVelocity == Vector2.zero && skin.sprite == last,
                "Final boss did not keep its facing when movement stopped.");

            enemy.attackRange = attackRange;
            enemy.SendMessage("Tick", step);
            Check(!enemy.IsChasing && gait.Phase == 0 && gait.Bob == 0,
                "Final boss attack did not reset its walking animation.");

            body.position = spawn;
            body.linearVelocity = initialVelocity;
            arena.Player.Teleport(playerPosition);
            Physics2D.SyncTransforms();
            enemy.SendMessage("Stop");
        }

        static void CheckRigidSaberCarry(Vector3[] rest, Vector3[] walking, int facing)
        {
            Vector3 translation = walking[0] - rest[0];
            for (int i = 1; i < rest.Length; i++)
                if ((walking[i] - rest[i] - translation).sqrMagnitude > .00000001f)
                    throw new Exception("Blood Saber's sleeve stretched while walking in facing " + facing);
        }

        static bool HasBossStep(Vector3[] rest, Vector3[] pose)
        {
            for (int i = 0; i < rest.Length; i++)
                if (rest[i].y >= 0 && rest[i].y < .16f && Mathf.Abs(rest[i].x) < .14f
                    && Vector3.SqrMagnitude(rest[i] - pose[i]) > .000001f) return true;
            return false;
        }

        static int NearestVertex(Vector3[] vertices, Vector2 point)
        {
            int best = 0;
            for (int i = 1; i < vertices.Length; i++)
                if (((Vector2)vertices[i] - point).sqrMagnitude < ((Vector2)vertices[best] - point).sqrMagnitude) best = i;
            return best;
        }

        static void CheckBossBootShape(Vector3[] rest, Vector3[] pose, int[] triangles, int facing, bool bellProfile)
        {
            bool profile = facing == 2 || facing == 6;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (rest[a].y < -.015f || rest[a].y > .34f || Mathf.Abs(rest[a].x) >= .16f) continue;
                var ab = pose[b] - pose[a];
                var ac = pose[c] - pose[a];
                var restAB = rest[b] - rest[a];
                var restAC = rest[c] - rest[a];
                float areaRatio = Vector3.Cross(ab, ac).z / Vector3.Cross(restAB, restAC).z;
                Check(areaRatio > .1f, "Final boss lower leg mesh folded in facing " + facing);
                if (!IsBootVertex(rest[a], bellProfile) || !IsBootVertex(rest[b], bellProfile)
                    || !IsBootVertex(rest[c], bellProfile)) continue;
                Check(areaRatio > .4f && areaRatio < 1.6f, "Final boss boot mesh pinched or folded in facing " + facing);
                if (profile)
                    Check((ab - restAB).sqrMagnitude < .00000001f && (ac - restAC).sqrMagnitude < .00000001f,
                        "Final boss visible profile boot changed shape during a step.");
            }
        }

        static bool IsBootVertex(Vector3 point, bool bellProfile) => bellProfile
            ? point.y >= -.015f && point.y <= .06f && Mathf.Abs(point.x) <= .03f
            : point.y >= -.015f && point.y <= .14f && Mathf.Abs(point.x) < .16f;

        static void ValidateMap(TravelMapEnvironment map, float radius)
        {
            Check(map != null, "No destination environment.");
            Check(map.ReservedExit != null && map.ReservedExit.isTrigger, "Missing unlinked exit reservation.");
            var entry = map.Definition.entry;
            Vector2[] route;
            switch (map.Definition.destination)
            {
                case TravelDestination.AscensionPlatform:
                    route = new[] { entry, new Vector2(755, 735), new Vector2(755, 500), new Vector2(755, 350) };
                    break;
                case TravelDestination.Wilderness:
                    route = new[] { entry, new Vector2(800, 740), new Vector2(800, 520), new Vector2(800, 270) };
                    break;
                case TravelDestination.FinalBattle:
                    Check(map.Definition.WorldSize == TravelMaps.Get(TravelDestination.Wilderness).WorldSize,
                        "Final arena is not sized like the other battle maps.");
                    route = new[] { entry, new Vector2(755, 500), new Vector2(755, 255) };
                    var center = map.Definition.Ground(755, 500);
                    foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                    {
                        bool blocked = false;
                        foreach (var hit in Physics2D.CircleCastAll(center, radius, direction, map.Definition.WorldSize.x))
                            blocked |= BlocksRoute(hit.collider, map);
                        Check(blocked, "Final arena boundary did not block travel toward " + direction);
                    }
                    break;
                case TravelDestination.Market:
                    route = new[] { entry, new Vector2(758, 313), new Vector2(421, 305),
                        new Vector2(758, 313), new Vector2(758, 612), new Vector2(1130, 612) };
                    break;
                default:
                    route = new[] { entry, new Vector2(1170, 740), new Vector2(1235, 815),
                        new Vector2(1170, 740), new Vector2(1000, 650), new Vector2(930, 520),
                        new Vector2(925, 460), new Vector2(900, 400), new Vector2(800, 360) };
                    break;
            }
            Physics2D.SyncTransforms();
            foreach (var point in route)
                foreach (var collider in Physics2D.OverlapCircleAll(map.Definition.Ground(point.x, point.y), radius))
                    Check(!BlocksRoute(collider, map), "Blocked entrance route point " + point + " by " + collider.name);
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = map.Definition.Ground(route[i - 1].x, route[i - 1].y);
                Vector2 b = map.Definition.Ground(route[i].x, route[i].y);
                foreach (var hit in Physics2D.CircleCastAll(a, radius, b - a, (b - a).magnitude))
                    Check(!BlocksRoute(hit.collider, map), "Blocked entrance route by " + hit.collider.name);
            }
        }

        static bool BlocksRoute(Collider2D collider, TravelMapEnvironment map)
            => !collider.isTrigger && collider.transform.IsChildOf(map.transform) && collider.GetComponentInParent<SlashEnemy>() == null;
    }
}
