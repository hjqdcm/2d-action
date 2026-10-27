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
        static int stage, choice, afterFrame, health, swings;
        static double next;
        static SlashPlayer original;
        static Quaternion rotation;
        static float zoom;
        static Mouse mouse;
        static Keyboard keyboard;
        static KeyboardState? queuedKeys;
        static MouseState? queuedMouse;

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
                        original.SetRealm((CultivationRealm)(choice + 1));
                        original.invincible = false;
                        original.TakeDamage(12, Vector2.right);
                        health = original.Health;
                        arena.Tuning.Settings.moveSpeed = 7.1f;
                        arena.Tuning.ApplyAll();
                        Check(original.Realm == (CultivationRealm)(choice + 1), "Applying tuning overwrote the current realm.");
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
                        travel.SelectDestination(TravelMaps.Choices[choice].destination);
                        Check(!travel.IsSelectionOpen && !arena.Paused, "Destination selection did not close picker.");
                        Check(arena.Player == original && original.Health == health, "Travel replaced the character or changed health.");
                        Check(original.Realm == (CultivationRealm)(choice + 1), "Outbound travel changed the realm.");
                        Check(Mathf.Abs(original.moveSpeed - 7.1f) < 0.001f, "Travel lost character tuning.");
                        Check(Vector2.Distance(original.transform.position, TravelMaps.Choices[choice].Entry) < 0.001f, "Travel did not arrive at map entrance.");
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
                            Check(enemy == null || !enemy.gameObject.activeInHierarchy, "Manor enemy remained active in the new map.");
                        Check(!arena.GetComponentInChildren<ManorEnvironment>(true).gameObject.activeInHierarchy, "Manor colliders remained active.");
                        ValidateMap(travel.ActiveMap, original.GetComponent<CircleCollider2D>().radius);
                        arena.CameraRig.ResetFeedback();
                        ManorValidation.Render(arena, Path.GetFullPath("Verification/Artifacts/Travel-" + travel.Current + ".png"));
                        if (travel.Current == TravelDestination.HealingLodge) HealingLodgeValidation.Run(arena);
                        original.Teleport(ManorEnvironment.GroundRect(travel.ActiveMap.Definition.reservedExit).center);
                        Advance(8);
                        break;
                    case 8:
                        Check(travel.Current == TravelMaps.Choices[choice].destination && !travel.IsSelectionOpen, "Reserved exit unexpectedly teleported the player.");
                        Debug.Log("SCENE_TRAVEL_DESTINATION_PASS: " + travel.Current + "; entry, routes, character state, camera, click suppression, inactive reserved exit.");
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
                        Debug.Log("PAUSE_RETURN_HOME_PASS: " + TravelMaps.Choices[choice].destination + "; same player/health/tuning/camera, manor and dummies restored, menu closed, no click-through.");
                        choice++;
                        if (choice < TravelMaps.Choices.Length)
                        {
                            arena.Restart();
                            Advance(0);
                        }
                        else
                        {
                            SessionState.SetBool(Pending, false);
                            InputSystem.onBeforeUpdate -= InjectGameplayInput;
                            InputSystem.RemoveDevice(mouse);
                            InputSystem.RemoveDevice(keyboard);
                            Debug.Log("CULTIVATION_REALM_PASS: starts/restarts in Qi Refining, all three higher realms survive outbound travel and tuning, every return home resets to Qi Refining.");
                            Debug.Log("SCENE_TRAVEL_PASS: all three destinations, gate/cancel/rearm, R/T disabled, reserved exits have no destination.");
                            EditorApplication.Exit(0);
                        }
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
                default:
                    route = new[] { entry, new Vector2(1170, 740), new Vector2(1235, 815),
                        new Vector2(1170, 740), new Vector2(1000, 650), new Vector2(930, 520),
                        new Vector2(925, 460), new Vector2(900, 400), new Vector2(800, 360) };
                    break;
            }
            Physics2D.SyncTransforms();
            foreach (var point in route)
                foreach (var collider in Physics2D.OverlapCircleAll(ManorEnvironment.Ground(point.x, point.y), radius))
                    Check(collider.isTrigger || !collider.transform.IsChildOf(map.transform), "Blocked entrance route point " + point + " by " + collider.name);
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = ManorEnvironment.Ground(route[i - 1].x, route[i - 1].y);
                Vector2 b = ManorEnvironment.Ground(route[i].x, route[i].y);
                foreach (var hit in Physics2D.CircleCastAll(a, radius, b - a, (b - a).magnitude))
                    Check(hit.collider.isTrigger || !hit.transform.IsChildOf(map.transform), "Blocked entrance route by " + hit.collider.name);
            }
        }
    }
}
