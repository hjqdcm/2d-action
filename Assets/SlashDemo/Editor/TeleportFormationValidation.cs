using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SlashDemo.Editor
{
    public static class TeleportFormationValidation
    {
        const string KeyName = "SlashDemo.TeleportFormationValidation";
        static SlashArena arena;
        static SlashPlayer player;
        static SlashEnemy[] manorEnemies;
        static TravelMapEnvironment oldMap;
        static TeleportFormation portal;
        static Keyboard keyboard;
        static KeyboardState? keys;
        static int stage, choice, afterFrame, health;
        static double next, deadline;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Advance(int value)
        { stage = value; afterFrame = Time.frameCount + 3; next = EditorApplication.timeSinceStartup + .2; }
        static void Inject()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || !keys.HasValue) return;
            InputSystem.QueueStateEvent(keyboard, keys.Value); keys = null;
        }
        public static void RunBatch()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(KeyName, true); SessionState.SetBool(KeyName + ".Launch", true);
        }
        [InitializeOnLoadMethod]
        static void Hook()
        { EditorApplication.update -= Tick; EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 100; }
        static void Capture()
        {
            var camera = arena.WorldCamera;
            Vector3 position = camera.transform.position;
            float zoom = camera.orthographicSize;
            camera.transform.position = portal.transform.position - ObliqueProjection.Forward(arena.View.elevation) * 20;
            camera.orthographicSize = 3.5f;
            var image = ManorOcclusionValidation.Capture(camera);
            Directory.CreateDirectory("Verification/Artifacts");
            File.WriteAllBytes("Verification/Artifacts/Teleport-formation-world.png", image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            camera.transform.position = position; camera.orthographicSize = zoom;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(KeyName, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (SessionState.GetBool(KeyName + ".Launch", false))
                {
                    SessionState.SetBool(KeyName + ".Launch", false);
                    EditorApplication.isPlaying = true; deadline = EditorApplication.timeSinceStartup + 100;
                    return;
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out at stage " + stage);
                if (!EditorApplication.isPlaying || Time.frameCount < afterFrame || EditorApplication.timeSinceStartup < next) return;
                if (arena == null)
                {
                    arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
                    if (arena == null || arena.Travel == null) return;
                    player = arena.Player;
                    manorEnemies = new SlashEnemy[arena.Enemies.Count];
                    for (int i = 0; i < manorEnemies.Length; i++) manorEnemies[i] = arena.Enemies[i];
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.onBeforeUpdate += Inject;
                }
                var travel = arena.Travel;
                switch (stage)
                {
                    case 0:
                        Check(travel.Portal == null, "Portal leaked into manor.");
                        player.Teleport(SceneTravel.ManorGate.center); Advance(1); break;
                    case 1:
                        if (!travel.IsSelectionOpen) return;
                        WildernessEncounterValidation.Enter(arena, WildernessCreature.Bear);
                        Check(travel.Portal == null && arena.Enemies.Count == 3, "Portal appeared before combat clear.");
                        Physics2D.SyncTransforms();
                        BearPackValidation.CheckFormation(arena);
                        Vector3 mapSize = travel.ActiveMap.GetComponent<SpriteRenderer>().bounds.size;
                        Check(Mathf.Abs(mapSize.x - 36) < .01f && Mathf.Abs(mapSize.y - 24) < .01f,
                            "Wilderness artwork size does not match its 36x24 collision coordinates.");
                        oldMap = travel.ActiveMap; health = player.Health;
                        var bears = new[] { arena.Enemies[0], arena.Enemies[1], arena.Enemies[2] };
                        bears[0].TakeHit(100000, Vector2.zero, 0);
                        Check(travel.Portal == null, "First kill spawned portal.");
                        bears[1].TakeHit(100000, Vector2.zero, 0);
                        Check(travel.Portal == null, "Portal appeared with a surviving monster.");
                        bears[2].GetComponent<Rigidbody2D>().position = Vector2.zero;
                        bears[2].transform.position = Vector2.zero;
                        Physics2D.SyncTransforms();
                        bears[2].TakeHit(100000, Vector2.zero, 0);
                        portal = travel.Portal;
                        Check(portal != null && oldMap.GetComponentsInChildren<TeleportFormation>().Length == 1,
                            "Final kill did not spawn exactly one portal.");
                        Check(portal.GetComponent<SpriteRenderer>().sprite.texture == Resources.Load<Texture2D>(TeleportFormation.ArtResource),
                            "Portal does not use the generated artwork.");
                        Check(portal.GetComponent<Collider2D>() == null, "Portal blocks walking.");
                        travel.EnemyDefeated(bears[2]);
                        Check(oldMap.GetComponentsInChildren<TeleportFormation>().Length == 1, "Repeated notification duplicated portal.");
                        player.Teleport((Vector2)portal.transform.position + Vector2.right * 4);
                        keys = new KeyboardState(Key.F); Advance(2); break;
                    case 2:
                        Check(!travel.IsSelectionOpen && !travel.ShowPortalPrompt, "Distant F activated portal.");
                        keys = new KeyboardState(); player.Teleport(portal.transform.position); Advance(3); break;
                    case 3:
                        Check(travel.ShowPortalPrompt && !travel.IsSelectionOpen, "Entering portal should show prompt and wait for F.");
                        if (choice == 0) Capture();
                        keys = new KeyboardState(Key.F); Advance(4); break;
                    case 4:
                        Check(travel.IsSelectionOpen && arena.Paused && arena.InputBlocked, "F did not open a paused destination picker: open="+travel.IsSelectionOpen+" paused="+arena.Paused+" prompt="+travel.ShowPortalPrompt+" blocked="+arena.InputBlocked+" key="+keyboard.fKey.isPressed+" current="+(Keyboard.current==keyboard)+" queued="+keys.HasValue+" update="+InputSystem.settings.updateMode);
                        travel.CancelSelection(); keys = new KeyboardState(); Advance(5); break;
                    case 5:
                        Check(!travel.IsSelectionOpen && !arena.Paused, "Cancel reopened portal automatically.");
                        keys = new KeyboardState(Key.F); Advance(6); break;
                    case 6:
                        Check(travel.IsSelectionOpen, "Portal did not reopen on a new F press.");
                        if (choice == TravelMaps.Choices.Length)
                        {
                            arena.Restart();
                            Check(travel.Current == TravelDestination.Manor && travel.Portal == null && arena.Enemies.Count == 3,
                                "Restart left portal or monsters behind.");
                            SessionState.SetBool(KeyName, false); InputSystem.onBeforeUpdate -= Inject;
                            Debug.Log("TELEPORT_FORMATION_PASS: final kill only, unique generated ground sprite, distant F rejected, enter waits for F, real F input, paused shared picker, cancel/reopen, all gate destinations including fresh wilderness, old map cleanup, character/health preserved, manor enemies restored, restart cleanup.");
                            EditorApplication.Exit(0); return;
                        }
                        var destination = TravelMaps.Choices[choice].destination;
                        travel.SelectDestination(destination);
                        Check(travel.Current == destination && travel.ActiveMap != oldMap && !oldMap.gameObject.activeSelf,
                            "Direct map travel retained old map.");
                        Check(!portal.gameObject.activeInHierarchy && travel.Portal == null, "Old portal survived travel.");
                        Check(arena.Player == player && player.Health == health, "Travel changed character or health.");
                        Check(arena.Enemies.Count == (destination == TravelDestination.Wilderness ? (arena.Enemies[0].rockBear ? BearDemon.WildernessCount : SnakeDemon.WildernessCount) : 0), "Destination has incorrect enemies.");
                        keys = new KeyboardState(); Advance(7); break;
                    case 7:
                        arena.TogglePause(); arena.ReturnHome();
                        Check(travel.Current == TravelDestination.Manor && travel.Portal == null && arena.Enemies.Count == manorEnemies.Length,
                            "Return home lost manor enemies.");
                        foreach (var enemy in manorEnemies)
                            Check(enemy != null && enemy.gameObject.activeInHierarchy, "Manor enemy identity/activity lost after direct map travel.");
                        choice++; Advance(0); break;
                }
            }
            catch (Exception ex)
            {
                SessionState.SetBool(KeyName, false); InputSystem.onBeforeUpdate -= Inject;
                Debug.LogError("TELEPORT_FORMATION_FAIL: choice=" + choice + " stage=" + stage + " " + ex);
                EditorApplication.Exit(1);
            }
        }
    }
}
