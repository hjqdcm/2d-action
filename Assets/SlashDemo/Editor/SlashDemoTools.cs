using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlashDemo.Editor
{
    public static class SlashDemoTools
    {
        public const string ScenePath = "Assets/SlashDemo/Scenes/SlashArena.unity";
        const string ResourcePath = "Assets/SlashDemo/Resources/SlashDemo";
        const string NativePreviewKey = "SlashDemo.NativeGamePreview.v1";

        [InitializeOnLoadMethod]
        static void ScheduleGameViewClarity()
        {
            EditorApplication.delayCall += ApplyInitialGameViewClarity;
            EditorApplication.playModeStateChanged -= ApplyGameViewClarityOnPlay;
            EditorApplication.playModeStateChanged += ApplyGameViewClarityOnPlay;
        }

        static void ApplyGameViewClarityOnPlay(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) ApplyInitialGameViewClarity();
        }

        static void ApplyInitialGameViewClarity()
        {
            if (Application.isBatchMode || SessionState.GetBool(NativePreviewKey, false)) return;
            if (SceneManager.GetActiveScene().path != ScenePath && UnityEngine.Object.FindAnyObjectByType<SlashArena>() == null) return;
            if (SetNativeGameView()) SessionState.SetBool(NativePreviewKey, true);
        }

        [MenuItem("Tools/Slash Demo/Improve Game View Clarity")]
        public static void ImproveGameViewClarity()
        {
            if (SetNativeGameView())
            {
                SessionState.SetBool(NativePreviewKey, true);
                Debug.Log("SLASH_GAME_VIEW_CLARITY_OK: low-resolution preview disabled; preview fitted without extra magnification.");
            }
        }

        static bool SetNativeGameView()
        {
            // GameView is internal; use its own setters so DPI, render targets and mouse mapping update together.
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var lowResolution = type?.GetProperty("lowResolutionForAspectRatios", flags);
            var minScale = type?.GetProperty("minScale", flags);
            var snapZoom = type?.GetMethod("SnapZoom", flags, null, new[] { typeof(float) }, null);
            if (lowResolution == null || !lowResolution.CanWrite || minScale == null || snapZoom == null)
            {
                Debug.LogWarning("Game view clarity could not be configured automatically. Disable Low Resolution Aspect Ratios in the Game view and fit its Scale slider.");
                return false;
            }
            bool updated = false;
            foreach (var window in Resources.FindObjectsOfTypeAll(type))
            {
                try
                {
                    lowResolution.SetValue(window, false);
                    snapZoom.Invoke(window, new object[] { Mathf.Min(1, (float)minScale.GetValue(window)) });
                    ((EditorWindow)window).Repaint();
                    updated = true;
                }
                catch (Exception error)
                {
                    Debug.LogWarning("Game view clarity update failed: " + error.GetBaseException().Message);
                }
            }
            return updated;
        }

        [MenuItem("Tools/Slash Demo/Open Arena")]
        public static void OpenArena()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Slash Demo/Rebuild Arena")]
        public static void RebuildArena()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Rebuild Slash Arena", "This replaces the demo scene and its placeholder sprites. Other scenes are unaffected.", "Rebuild", "Cancel")) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildArena();
        }

        public static void BuildArena()
        {
            Directory.CreateDirectory(ResourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            CreateSprite("Square", null, false);
            CreateSprite("Disk", null, true);
            CreateSprite("Blade", new[] { new Vector2(-0.49f, -0.36f), new Vector2(0.26f, -0.28f), new Vector2(0.49f, 0), new Vector2(0.26f, 0.28f), new Vector2(-0.49f, 0.36f) }, false);
            CreateSprite("Hat", new[] { new Vector2(-0.49f, -0.12f), new Vector2(0, 0.49f), new Vector2(0.49f, -0.12f), new Vector2(0, -0.4f) }, false);
            CreateSprite("Robe", new[] { new Vector2(-0.4f, -0.48f), new Vector2(0.4f, -0.48f), new Vector2(0.49f, -0.25f), new Vector2(0.25f, 0.49f), new Vector2(-0.25f, 0.49f), new Vector2(-0.49f, -0.25f) }, false);
            string matPath = ResourcePath + "/Unlit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("URP 2D unlit shader not found.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, matPath);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var arena = new GameObject("Slash Arena").AddComponent<SlashArena>();
            arena.bloomProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            arena.BuildWorld();
            arena.ConfigureBloom();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath))
                throw new IOException("Could not save arena scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("SLASH_DEMO_BUILD_OK: " + ScenePath);
        }

        static void CreateSprite(string name, Vector2[] polygon, bool disk)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                float alpha = disk ? Mathf.Clamp01((0.49f - p.magnitude) * size) : polygon == null || InPolygon(p, polygon) ? 1 : 0;
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            string path = ResourcePath + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        static bool InPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                    point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            return inside;
        }

        public static void RunSmokeTest()
        {
            BuildArena();
            // Persist across the editor's play-mode domain reload.
            SessionState.SetBool("SlashDemo.SmokePending", true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void HookPlayMode()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("SlashDemo.SmokePending", false)) return;
            SessionState.SetBool("SlashDemo.SmokePending", false);
            new GameObject("Slash Demo Smoke Test").AddComponent<SlashSmokeTest>();
        }
    }
}
