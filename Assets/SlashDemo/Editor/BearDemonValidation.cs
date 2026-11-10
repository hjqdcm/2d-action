using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class BearDemonValidation
    {
        const string Pending = "SlashDemo.BearDemonValidation";
        static int stage, health;
        static double next, timeout;
        static SlashArena arena;
        static SlashEnemy bear;
        static Vector2 origin;
        static float pausedPhase;
        static int liveFrames, liveAfterFrame;
        static double nextLive;
        static float liveMinBlend = 1;
        static readonly Vector2[] liveMin = new Vector2[4], liveMax = new Vector2[4];
        static readonly Vector2[] Feet = { new Vector2(.105f,.185f), new Vector2(.4f,.22f), new Vector2(.6f,.085f), new Vector2(.905f,.16f) };

        static void CaptureLiveWalking()
        {
            if (stage != 2 || bear == null || Time.frameCount < liveAfterFrame || EditorApplication.timeSinceStartup < nextLive) return;
            nextLive = EditorApplication.timeSinceStartup + .055;
            var gait = bear.GetComponent<BearWalkMesh>();
            if (liveFrames > 2) liveMinBlend = Mathf.Min(liveMinBlend, gait.Blend);
            for (int leg = 0; leg < 4; leg++)
            {
                var p = (Vector2)gait.PawPosition(leg);
                if (liveFrames == 0) liveMin[leg] = liveMax[leg] = p;
                else { liveMin[leg] = Vector2.Min(liveMin[leg], p); liveMax[leg] = Vector2.Max(liveMax[leg], p); }
            }
            Vector3 oldPosition = arena.WorldCamera.transform.position;
            float oldZoom = arena.WorldCamera.orthographicSize;
            arena.WorldCamera.orthographicSize = 5;
            arena.WorldCamera.transform.position = bear.transform.position + arena.View.Up * 1.1f
                - ObliqueProjection.Forward(arena.View.elevation) * 20;
            var image = ManorOcclusionValidation.Capture(arena.WorldCamera);
            Directory.CreateDirectory("Verification/Artifacts/BearWalkLive");
            File.WriteAllBytes("Verification/Artifacts/BearWalkLive/frame-" + liveFrames.ToString("D2") + ".png", image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            arena.WorldCamera.transform.position = oldPosition;
            arena.WorldCamera.orthographicSize = oldZoom;
            liveFrames++;
        }

        static void ValidateWalking()
        {
            var gait = bear.GetComponent<BearWalkMesh>();
            Check(gait != null && gait.WalkingRenderer.enabled, "Missing four paw walking mesh.");
            Check(!ShaderUtil.ShaderHasError(gait.WalkingRenderer.sharedMaterial.shader), "Bear skin shader failed.");
            var skin = bear.transform.Find("Visual/Rock bear skin").GetComponent<SpriteRenderer>();
            var originalUV = gait.DeformedMesh.uv;
            gait.Advance(Vector2.zero, 1, false); gait.Refresh();
            var rest = gait.DeformedMesh.vertices;
            Check(rest.Length == 36 && gait.DeformedMesh.subMeshCount == 9, "Expected five rigid cutouts and four joint covers.");
            bool[] moved = new bool[4], lifted = new bool[4];
            var restFeet = new Vector3[4];
            for (int leg = 0; leg < 4; leg++) restFeet[leg] = gait.PawPosition(leg);
            var cameraPosition = arena.WorldCamera.transform.position;
            float zoom = arena.WorldCamera.orthographicSize;
            arena.WorldCamera.orthographicSize = 2.5f;
            arena.WorldCamera.transform.position = (Vector3)bear.transform.position + arena.View.Up * 1.2f
                - ObliqueProjection.Forward(arena.View.elevation) * 20;
            Directory.CreateDirectory("Verification/Artifacts/BearWalk");
            try
            {
                for (int frame = 0; frame < 16; frame++)
                {
                    gait.Advance(Vector2.down * (BearWalkMesh.CycleDistance * arena.View.characterScale / 16), .08f, true);
                    gait.Refresh();
                    var pose = gait.DeformedMesh.vertices;
                    // All six pair distances on every quad must remain invariant.
                    // This excludes shear, squashing and any texture stretching.
                    for (int part = 0; part < 9; part++)
                        for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++)
                        {
                            int i = part * 4 + a, j = part * 4 + b;
                            Check(Mathf.Abs(Vector3.Distance(pose[i], pose[j]) - Vector3.Distance(rest[i], rest[j])) < .00001f,
                                "Rigid claw/armor cutout changed shape at frame " + frame);
                        }
                    for (int leg = 0; leg < 4; leg++)
                    {
                        Vector3 delta = gait.PawPosition(leg) - restFeet[leg];
                        moved[leg] |= delta.magnitude > .045f;
                        lifted[leg] |= delta.y > .04f;
                    }
                    var image = ManorOcclusionValidation.Capture(arena.WorldCamera);
                    File.WriteAllBytes("Verification/Artifacts/BearWalk/frame-" + frame.ToString("D2") + ".png", image.EncodeToPNG());
                    UnityEngine.Object.Destroy(image);
                }
                for (int leg = 0; leg < 4; leg++) Check(moved[leg] && lifted[leg], "Paw " + leg + " did not swing and lift.");
                var beforeFreeze = gait.DeformedMesh.vertices;
                float phase = gait.Phase;
                gait.Advance(Vector2.one, 0, true); gait.Refresh();
                var frozen = gait.DeformedMesh.vertices;
                Check(gait.Phase == phase, "Frozen time advanced gait phase.");
                for (int i = 0; i < beforeFreeze.Length; i++)
                    Check((beforeFreeze[i] - frozen[i]).sqrMagnitude < .0000001f, "Frozen gait changed pose.");
                gait.Advance(Vector2.zero, 1, false); gait.Refresh();
                Check(gait.Blend == 0, "Stopping did not settle the paws.");
                var settled = gait.DeformedMesh.vertices;
                var currentUV = gait.DeformedMesh.uv;
                for (int i = 0; i < rest.Length; i++)
                {
                    Check((rest[i] - settled[i]).sqrMagnitude < .0000001f, "Stopping did not restore standing pose.");
                    Check(originalUV[i] == currentUV[i], "Gait changed texture coordinates.");
                }
                skin.flipX = !skin.flipX; gait.Refresh();
                // 200 FPS rendering with 50 Hz physics: three zero-distance frames per tick.
                for (int frame = 0; frame < 200; frame++)
                    gait.Advance(frame % 4 == 0 ? Vector2.down * .033f : Vector2.zero, .005f, true);
                Check(gait.Blend > .9f, "High frame rate faded out the paw motion.");
                gait.Advance(Vector2.zero, 1, false); gait.Refresh();
                var flipped = gait.DeformedMesh.vertices;
                for (int i = 0; i < rest.Length; i++)
                    Check(Mathf.Abs(flipped[i].x + rest[i].x) < .00001f && Mathf.Abs(flipped[i].y - rest[i].y) < .00001f,
                        "Turning did not mirror all four paws.");
                skin.flipX = !skin.flipX; gait.Refresh();
                Debug.Log("BEAR_WALK_PASS: five rigid pieces preserve all pair distances through the full cycle; four paws swing/lift; fixed UVs, stop, freeze, mirroring, high FPS and shader.");
            }
            finally
            {
                arena.WorldCamera.transform.position = cameraPosition;
                arena.WorldCamera.orthographicSize = zoom;
            }
        }

        public static void RunBatch()
        {
            // Prior batch exits can leave a Play Mode recovery scene behind.
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + ".Launch", true);
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            next = EditorApplication.timeSinceStartup + 2;
            timeout = EditorApplication.timeSinceStartup + 100;
        }

        static void Advance(int value, double delay = 0.3)
        { stage = value; next = EditorApplication.timeSinceStartup + delay; }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); }
        static void OpenGate()
        { arena.Player.Teleport(SceneTravel.ManorGate.center); }
        static SlashEnemy OnlyBear()
        {
            var enemy = BearPackValidation.IsolateMiddleBear(arena);
            Check(enemy.rockBear && !enemy.trainingDummy && enemy.Alive, "Bear is not a living hostile enemy.");
            return enemy;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            CaptureLiveWalking();
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (SessionState.GetBool(Pending + ".Launch", false))
                {
                    SessionState.SetBool(Pending + ".Launch", false);
                    timeout = EditorApplication.timeSinceStartup + 100;
                    EditorApplication.isPlaying = true;
                    Advance(0, 3); return;
                }
                if (EditorApplication.timeSinceStartup > timeout) throw new Exception("Validation timed out at stage " + stage);
                if (!EditorApplication.isPlaying) return;
                switch (stage)
                {
                    case 0:
                        arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
                        Check(arena != null && arena.Travel != null, "Arena did not initialize.");
                        arena.Player.invincible = false;
                        OpenGate(); Advance(1); break;
                    case 1:
                        Check(arena.Travel.IsSelectionOpen, "Gate did not open travel.");
                        WildernessEncounterValidation.Enter(arena, WildernessCreature.Bear);
                        bear = OnlyBear();
                        origin = bear.transform.position;
                        liveFrames = 0; liveMinBlend = 1; nextLive = 0; liveAfterFrame = Time.frameCount + 4;
                        Advance(2, 1.5); break;
                    case 2:
                        Check(liveFrames >= 8 && liveMinBlend > .7f, "Live chase did not sustain visible paw motion: frames=" + liveFrames + ", blend=" + liveMinBlend);
                        for (int leg = 0; leg < 4; leg++)
                        {
                            float pixels = (liveMax[leg] - liveMin[leg]).magnitude * arena.View.characterScale * 480 / 10;
                            Check(pixels > 6, "Paw " + leg + " moves less than 6 pixels at gameplay zoom: " + pixels);
                            Debug.Log("BEAR_LIVE_PAW: " + leg + ", screen travel=" + pixels + " pixels");
                        }
                        Check(Vector2.Distance(origin, bear.transform.position) > 0.5f, "Bear did not automatically chase.");
                        Check(Vector2.Distance(bear.transform.position, arena.Player.transform.position)
                            < Vector2.Distance(origin, arena.Player.transform.position), "Bear moved away from player.");
                        ValidateWalking();
                        arena.Player.Teleport((Vector2)bear.transform.position + Vector2.down * 1.25f);
                        health = arena.Player.Health;
                        Advance(3, 1.3); break;
                    case 3:
                        Check(arena.Player.Health < health, "Bear attack did not damage the player.");
                        arena.TogglePause(); origin = bear.transform.position; health = arena.Player.Health;
                        pausedPhase = bear.GetComponent<BearWalkMesh>().Phase;
                        Advance(4, 0.7); break;
                    case 4:
                        Check(Vector2.Distance(origin, bear.transform.position) < 0.01f && health == arena.Player.Health,
                            "Paused bear continued moving or dealing damage.");
                        Check(bear.GetComponent<BearWalkMesh>().Phase == pausedPhase, "Pause advanced the paw animation.");
                        arena.TogglePause();
                        // Render a close view of the actual character and enemy in the mountain map.
                        arena.Player.Teleport((Vector2)bear.transform.position + Vector2.down * 2.5f);
                        arena.CameraRig.ResetFeedback(); arena.WorldCamera.orthographicSize = 5;
                        Advance(5, 0.2); break;
                    case 5:
                        Directory.CreateDirectory("Verification/Artifacts");
                        var frame = ManorOcclusionValidation.Capture(arena.WorldCamera);
                        File.WriteAllBytes("Verification/Artifacts/RockBear-wilderness.png", frame.EncodeToPNG());
                        UnityEngine.Object.Destroy(frame);
                        int hp = bear.Health;
                        bear.TakeHit(20, Vector2.up, 1);
                        Check(bear.Health == hp - 20, "Bear did not take combat damage.");
                        bear.TakeHit(100000, Vector2.up, 1, true);
                        Check(!bear.Alive && arena.Enemies.Count == 0, "Bear death did not unregister it.");
                        Advance(6, 2.5); break;
                    case 6:
                        Check(arena.Enemies.Count == 0 && arena.Travel.ActiveMap.GetComponentsInChildren<SlashEnemy>().Length == 0,
                            "Killed bear respawned or leaked.");
                        arena.TogglePause(); arena.ReturnHome();
                        Check(arena.Enemies.Count == 3, "Manor training dummies were not restored.");
                        foreach (var enemy in arena.Enemies)
                            Check(enemy.trainingDummy && enemy.gameObject.activeInHierarchy, "Manor enemy was not restored.");
                        OpenGate(); Advance(7); break;
                    case 7:
                        WildernessEncounterValidation.Enter(arena, WildernessCreature.Bear);
                        bear = OnlyBear();
                        arena.TogglePause(); arena.ReturnHome();
                        Check(!bear.gameObject.activeInHierarchy && arena.Enemies.Count == 3, "Living bear leaked into manor.");
                        OpenGate(); Advance(8); break;
                    case 8:
                        arena.Travel.SelectDestination(TravelDestination.AscensionPlatform);
                        Check(arena.Enemies.Count == 1 && arena.Enemies[0].GetComponent<AscensionCultivator>() != null,
                            "Ascension platform did not isolate its challenger from the bears.");
                        arena.Restart();
                        Check(arena.Travel.Current == TravelDestination.Manor && arena.Enemies.Count == 3,
                            "Restart did not clean up travel enemies.");
                        SessionState.SetBool(Pending, false);
                        Debug.Log("ROCK_BEAR_PASS: one hostile bear on entry, automatic chase, damage, pause, hit/death, no in-map respawn, reentry, return-home cleanup, other-map isolation and restart.");
                        EditorApplication.Exit(0); break;
                }
            }
            catch (Exception exception)
            {
                SessionState.SetBool(Pending, false);
                Debug.LogError("ROCK_BEAR_FAIL: stage=" + stage + " " + exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
