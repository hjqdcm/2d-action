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
                        SessionState.SetBool(Pending, false);
                        InputSystem.onBeforeUpdate -= InjectGameplayInput;
                        InputSystem.RemoveDevice(mouse);
                        InputSystem.RemoveDevice(keyboard);
                        Debug.Log("CULTIVATION_REALM_PASS: starts/restarts in Qi Refining, all three higher realms survive outbound travel and tuning, every return home resets to Qi Refining.");
                        Debug.Log("SCENE_TRAVEL_PASS: all destinations, gate/cancel/rearm, R/T disabled, reserved exits have no destination, market portal and shop access.");
                        Debug.Log("FORTUNE_TRAVEL_PASS: route costs, shared visits, voluntary and automatic final battle travel.");
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
