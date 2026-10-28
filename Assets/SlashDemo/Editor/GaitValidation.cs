using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlashDemo.Editor
{
    public static class GaitValidation
    {
        static int checks;
        static void Check(bool value, string message)
        { if (!value) throw new Exception(message); checks++; }

        public static void RunBatch()
        {
            try
            {
                AssetDatabase.DesiredWorkerCount = 1;
                AssetDatabase.ForceToDesiredWorkerCount();
                HeroAssetImport.ValidateHero();
                ValidateAndRender();
                Debug.Log("GAIT_NATIVE_PASS: " + checks + " checks; eight-facing walking cycles, stable head, shoulder anchors, pause, combat suppression and stop.");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogError("GAIT_NATIVE_FAIL: " + error); EditorApplication.Exit(1); }
        }

        static void ValidateAndRender()
        {
            checks = 0;
            string folder = "Verification/Artifacts/UpperBodyWalk";
            Directory.CreateDirectory(folder);
            var root = new GameObject("Upper body walking validation") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            var cameraObject = new GameObject("Walking preview camera") { hideFlags = HideFlags.HideAndDontSave };
            var target = RenderTexture.GetTemporary(320, 384, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                var view = root.AddComponent<ObliqueView>(); view.characterScale = 1;
                var visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
                var hero = root.AddComponent<HeroAppearance>();
                Check(hero.Initialize(visual) && hero.HasDirectionalArt, "Actual eight-direction sprites initialize.");
                var gait = hero.Locomotion;
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = .59f; camera.aspect = 320f / 384;
                camera.nearClipPlane = .1f; camera.farClipPlane = 20;
                camera.transform.position = new Vector3(0, .50f, -10);
                for (int direction = 0; direction < 8; direction++)
                {
                    Vector2 heading = ObliqueProjection.UnprojectGround(DemoArt.Direction(direction * 45 - 90), 45).normalized;
                    hero.SetFacing(heading, 1.2f); gait.Refresh();
                    for (int i = 0; i < 160; i++) gait.Advance(Vector2.zero, 1f / 60, false, view);
                    gait.Refresh();
                    var rest = gait.DeformedMesh.vertices;
                    var uv = gait.DeformedMesh.uv2;
                    var textureUV = gait.DeformedMesh.uv;
                    for (int i = 0; i < 16; i++) gait.Advance(heading * .05f, 1f / 60, false, view);
                    for (int frame = 0; frame < 16; frame++)
                    {
                        gait.Refresh();
                        var walking = gait.DeformedMesh.vertices;
                        var currentUV = gait.DeformedMesh.uv;
                        float bob = gait.Bob / HeroAppearance.BodyHeight;
                        for (int i = 0; i < walking.Length; i++)
                        {
                            var p = walking[i];
                            Check(float.IsFinite(p.x) && float.IsFinite(p.y), "Walking mesh remains finite.");
                            Check(currentUV[i] == textureUV[i], "Walking preserves the original painted UVs.");
                            if (uv[i].y >= .97f)
                                Check(Vector3.Distance(rest[i], p + Vector3.up * bob) < .00001f, "Crown stays stable in world space.");
                            if (uv[i].y > .59f)
                                Check(Vector3.Distance(gait.PosePoint(rest[i]), p) < .00001f, "Shoulder anchors inherit the walking body pose.");
                        }
                        Render(camera, target, gait, bob, folder + "/direction-" + direction + "-" + frame.ToString("00") + ".png");
                        gait.Advance(heading * (HeroGaitMath.CycleDistance / 16), HeroGaitMath.CycleDistance / 48, false, view);
                    }
                    gait.Refresh();
                    var beforePause = gait.DeformedMesh.vertices;
                    gait.Advance(heading, 0, false, view); gait.Refresh();
                    var paused = gait.DeformedMesh.vertices;
                    for (int i = 0; i < paused.Length; i++) Check(paused[i] == beforePause[i], "Pause freezes the entire upper-body walking pose.");
                    gait.Attacking = true; gait.Refresh();
                    var combat = gait.DeformedMesh.vertices;
                    for (int i = 0; i < combat.Length; i++)
                        if (uv[i].y > .59f) Check(Vector3.Distance(combat[i], rest[i]) < .00001f, "Combat owns the upper body instead of layering walking twist.");
                    gait.Attacking = false;
                    for (int i = 0; i < 160; i++) gait.Advance(Vector2.zero, 1f / 60, false, view);
                    gait.Refresh();
                    var stopped = gait.DeformedMesh.vertices;
                    for (int i = 0; i < stopped.Length; i++)
                        Check(Vector3.Distance(stopped[i], rest[i]) < .00001f, "Stopping smoothly restores the rest mesh.");
                }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void Render(Camera camera, RenderTexture target, HeroWalkMesh gait, float bob, string path)
        {
            using (var commands = new CommandBuffer())
            {
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, new Color(.035f, .060f, .080f, 1));
                commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, false));
                var properties = new MaterialPropertyBlock(); properties.SetFloat("_WalkBlend", 1);
                properties.SetColor("_Color", Color.white);
                for (int part = 0; part < gait.DeformedMesh.subMeshCount; part++)
                    commands.DrawMesh(gait.DeformedMesh, Matrix4x4.Translate(Vector3.up * bob), gait.WalkingRenderer.sharedMaterials[part], part, 0, properties);
                Graphics.ExecuteCommandBuffer(commands);
            }
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }
    }
}
