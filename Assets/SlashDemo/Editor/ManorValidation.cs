using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class ManorValidation
    {
        const string Pending = "SlashDemo.ManorValidation.Pending";
        const string LaunchPending = "SlashDemo.ManorValidation.LaunchPending";
        static int stage;
        static double nextCheck;
        static int initialFrame;

        // Opens the existing scene, so validation covers its runtime migration as well as fresh builds.
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(LaunchPending, true);
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            nextCheck = EditorApplication.timeSinceStartup + 3;
            Debug.Log("MANOR_BATCH_BEGIN: waiting for startup asset imports before entering Play.");
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.update -= UpdateBatch;
            EditorApplication.update += UpdateBatch;
            nextCheck = EditorApplication.timeSinceStartup + 3;
        }

        static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            Debug.Log("MANOR_BATCH_PLAYING");
            stage = 0;
            initialFrame = Time.frameCount;
            nextCheck = EditorApplication.timeSinceStartup + 1;
        }

        static void UpdateBatch()
        {
            if (!SessionState.GetBool(Pending, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            if (SessionState.GetBool(LaunchPending, false))
            {
                SessionState.SetBool(LaunchPending, false);
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying) return;
            var arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
            if (arena == null || arena.Player == null) return;
            try
            {
                switch (stage)
                {
                    case 0:
                        Validate(arena);
                        if (Vector2.Distance(arena.Player.transform.position, ManorEnvironment.SpawnPoint) > 0.01f)
                            throw new Exception("Initial player is not in the center courtyard.");
                        if (arena.Enemies.Count != 3) throw new Exception("Expected three practice dummies, found " + arena.Enemies.Count);
                        foreach (var dummy in arena.Enemies)
                            if (!dummy.trainingDummy || !ManorEnvironment.InTraining(dummy.transform.position))
                                throw new Exception("Practice dummy is not in the training ground.");
                        initialFrame = Time.frameCount;
                        stage = 1;
                        break;
                    case 1:
                        if (Time.frameCount < initialFrame + 45) return;
                        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        {
                            ManorOcclusionValidation.Run(arena, Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/Artifacts")));
                            ManorCollisionPreview.Render(arena, Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/Artifacts/Manor-collision-layout.png")));
                            Render(arena, Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/Artifacts/Manor-in-game.png")));
                            RenderBeforeDetailUpdate(arena, Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/Artifacts/Manor-before-detail.png")));
                        }
                        arena.Restart();
                        if (Vector2.Distance(arena.Player.transform.position, ManorEnvironment.SpawnPoint) > 0.01f)
                            throw new Exception("Restart did not preserve the manor spawn.");
                        stage = 2;
                        nextCheck = EditorApplication.timeSinceStartup + 0.15;
                        break;
                    case 2:
                        if (arena.Enemies.Count != 3) throw new Exception("Restart did not restore practice dummies.");
                        arena.practiceMode = false;
                        arena.Restart();
                        stage = 3;
                        nextCheck = EditorApplication.timeSinceStartup + 2.5;
                        break;
                    case 3:
                        if (arena.Enemies.Count != 0) throw new Exception("Hostile waves started outside the training ground.");
                        arena.Player.GetComponent<Rigidbody2D>().position = ManorEnvironment.TrainingCenter;
                        stage = 4;
                        nextCheck = EditorApplication.timeSinceStartup + 0.15;
                        break;
                    case 4:
                        if (arena.Wave != 1 || arena.Enemies.Count != 4) throw new Exception("Entering training ground did not start the wave.");
                        SessionState.SetBool(Pending, false);
                        Debug.Log("MANOR_PLAY_PASS: legacy scene migration, center spawn, restart, practice and wave entry.");
                        EditorApplication.Exit(0);
                        break;
                }
            }
            catch (Exception error)
            {
                SessionState.SetBool(Pending, false);
                Debug.LogError("MANOR_PLAY_FAIL: " + error);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Slash Demo/Validate Manor Layout")]
        public static void ValidateLayout()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("Enter Play, then run Validate Manor Layout to check the active manor without rebuilding the scene.");
                return;
            }
            Validate(UnityEngine.Object.FindAnyObjectByType<SlashArena>());
        }

        public static void Validate(SlashArena arena)
        {
            if (arena == null || arena.Player == null) throw new Exception("Arena did not initialize.");
            var manor = arena.GetComponentInChildren<ManorEnvironment>();
            if (manor == null) throw new Exception("Old scene was not upgraded to the manor.");
            var surface = Resources.Load<Shader>("SlashDemo/ManorSurface");
            if (surface == null || ShaderUtil.ShaderHasError(surface)) throw new Exception("Manor surface shader failed to import.");
            var paint = Resources.Load<Texture2D>(ManorEnvironment.ResourcePath);
            if (paint == null || paint.width != 3072 || paint.height != 2048)
                throw new Exception("HD manor texture was not imported at its full 3072 x 2048 resolution.");
            Physics2D.SyncTransforms();
            float radius = arena.Player.GetComponent<CircleCollider2D>().radius;
            CheckPoint(manor, ManorEnvironment.SpawnPoint, radius);
            Route(manor, radius, "courtyard to training ground", new[] {
                ManorEnvironment.SpawnPoint, ManorEnvironment.Ground(700, 470),
                ManorEnvironment.Ground(970, 470), ManorEnvironment.Ground(1090, 470),
                ManorEnvironment.TrainingCenter });
            Route(manor, radius, "courtyard to bedroom", new[] {
                ManorEnvironment.SpawnPoint, ManorEnvironment.Ground(600, 548),
                ManorEnvironment.Ground(350, 548), ManorEnvironment.Ground(350, 560) });
            Route(manor, radius, "courtyard to hall and meditation room", new[] {
                ManorEnvironment.SpawnPoint, ManorEnvironment.Ground(680, 340),
                ManorEnvironment.Ground(680, 285), ManorEnvironment.Ground(580, 285),
                ManorEnvironment.Ground(580, 260), ManorEnvironment.Ground(550, 220),
                ManorEnvironment.Ground(520, 229), ManorEnvironment.Ground(500, 233),
                ManorEnvironment.Ground(480, 233), ManorEnvironment.Ground(450, 248),
                ManorEnvironment.Ground(420, 248), ManorEnvironment.Ground(340, 280) });
            BlockedRoute(manor, radius, ManorEnvironment.Ground(340, 300),
                ManorEnvironment.Ground(340, 360), ManorCollisionLayout.PerimeterName);
            Route(manor, radius, "walk across meditation cushion", new[] {
                ManorEnvironment.Ground(340, 255), ManorEnvironment.Ground(310, 255),
                ManorEnvironment.Ground(280, 255) });
            foreach (var test in new[] { (1100f,380f,1050f,380f), (1390f,380f,1450f,380f),
                (1240f,210f,1240f,140f), (1240f,550f,1240f,620f) })
                BlockedRoute(manor, radius, ManorEnvironment.Ground(test.Item1, test.Item2),
                    ManorEnvironment.Ground(test.Item3, test.Item4), ManorCollisionLayout.PerimeterName);
            foreach (var point in new[] { new Vector2(680,280), new Vector2(670,175),
                new Vector2(970,210), new Vector2(380,240), new Vector2(350,550) })
                CheckPoint(manor, ManorEnvironment.Ground(point.x, point.y), radius);
            BlockedRoute(manor, radius, ManorEnvironment.Ground(680, 180),
                ManorEnvironment.Ground(680, 295), "Tea table");
            BlockedRoute(manor, radius, ManorEnvironment.Ground(680, 295),
                ManorEnvironment.Ground(680, 180), "Tea table");
            BlockedRoute(manor, radius, ManorEnvironment.Ground(590, 235),
                ManorEnvironment.Ground(780, 235), "Tea table");
            BlockedRoute(manor, radius, ManorEnvironment.Ground(780, 235),
                ManorEnvironment.Ground(590, 235), "Tea table");
            Route(manor, radius, "courtyard through open gate", new[] {
                ManorEnvironment.SpawnPoint, ManorEnvironment.Ground(680, 660),
                ManorEnvironment.Ground(680, 880), ManorEnvironment.Ground(680, 950) });
            for (int i = 0; i < 3; i++) CheckPoint(manor, ManorEnvironment.DummyPoint(i), 0.34f);
            for (int wave = 1; wave <= 10; wave++)
            {
                int count = Mathf.Min(3 + wave, 10);
                for (int i = 0; i < count; i++)
                {
                    Vector2 point = ManorEnvironment.WavePoint(i, count, wave);
                    if (!ManorEnvironment.InTraining(point)) throw new Exception("Wave spawn outside training ground.");
                    CheckPoint(manor, point, 0.34f);
                }
            }
            if (!arena.WorldCamera.orthographic || Quaternion.Angle(arena.WorldCamera.transform.rotation, arena.View.Rotation) > 0.01f)
                throw new Exception("Existing camera projection or elevation was changed.");
            Debug.Log("MANOR_LAYOUT_PASS: annotated perimeter, passable cushion, table blocking, four walkable routes, former invisible-box positions, full HD import, dummies, ten waves and camera projection.");
        }

        static void BlockedRoute(ManorEnvironment manor, float radius, Vector2 from, Vector2 to, string wallName)
        {
            Vector2 delta = to - from;
            foreach (var hit in Physics2D.CircleCastAll(from, radius, delta.normalized, delta.magnitude))
                if (hit.transform.IsChildOf(manor.transform) && hit.collider.name == wallName) return;
            throw new Exception("The repaired wall does not block passage: " + wallName);
        }

        static void CheckPoint(ManorEnvironment manor, Vector2 point, float radius)
        {
            if (!ManorCollisionLayout.ContainsGround(point)) throw new Exception("Ground point outside annotated walkable region: " + point);
            foreach (var hit in Physics2D.OverlapCircleAll(point, radius))
                if (hit.transform.IsChildOf(manor.transform)) throw new Exception("Blocked ground point: " + point + " by " + hit.name);
        }

        static void Route(ManorEnvironment manor, float radius, string name, Vector2[] points)
        {
            foreach (var point in points) CheckPoint(manor, point, radius);
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 delta = points[i] - points[i - 1];
                foreach (var hit in Physics2D.CircleCastAll(points[i - 1], radius, delta.normalized, delta.magnitude))
                    if (hit.transform.IsChildOf(manor.transform)) throw new Exception(name + " blocked by " + hit.collider.name);
            }
        }

        public static void Render(SlashArena arena, string path)
        {
            var camera = arena.WorldCamera;
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1920, 1080, 24);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("MANOR_RENDER_SAVED: " + path);
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(image);
            }
        }

        static void RenderBeforeDetailUpdate(SlashArena arena, string path)
        {
            var manor = arena.GetComponentInChildren<ManorEnvironment>();
            var oldPaint = Resources.Load<Texture2D>("SlashDemo/PrivateManor-v1");
            var newPaint = Resources.Load<Texture2D>(ManorEnvironment.ResourcePath);
            var originals = new List<(SpriteRenderer renderer, Sprite sprite, Material material)>();
            var temporarySprites = new List<Sprite>();
            try
            {
                foreach (var renderer in manor.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (renderer.sprite == null || renderer.sprite.texture != newPaint) continue;
                    originals.Add((renderer, renderer.sprite, renderer.sharedMaterial));
                    float oldScale = oldPaint.width / (float)newPaint.width;
                    Rect oldRect = renderer.sprite.rect;
                    oldRect.position *= oldScale;
                    oldRect.size *= oldScale;
                    var sprite = Sprite.Create(oldPaint, oldRect, Vector2.one * 0.5f,
                        renderer.sprite.pixelsPerUnit * oldScale, 0, SpriteMeshType.FullRect);
                    var geometry = renderer.sprite.vertices;
                    for (int i = 0; i < geometry.Length; i++) geometry[i] = geometry[i] * sprite.pixelsPerUnit + sprite.pivot;
                    sprite.OverrideGeometry(geometry, renderer.sprite.triangles);
                    temporarySprites.Add(sprite);
                    renderer.sprite = sprite;
                    renderer.sharedMaterial = DemoArt.Material;
                }
                Render(arena, path);
            }
            finally
            {
                foreach (var original in originals)
                {
                    original.renderer.sprite = original.sprite;
                    original.renderer.sharedMaterial = original.material;
                }
                foreach (var sprite in temporarySprites) UnityEngine.Object.Destroy(sprite);
            }
        }
    }

}
