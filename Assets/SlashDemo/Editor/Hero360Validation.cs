using System;
using System.IO;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlashDemo.Editor
{
    public static class Hero360Validation
    {
        static int count;
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); count++; }

        [MenuItem("Tools/Slash Demo/Validate 360 Hero")]
        public static void ValidateAndRender()
        {
            count = 0;
            var root = new GameObject("360 prototype validation") { hideFlags = HideFlags.HideAndDontSave };
            Hero360Model model = null;
            try
            {
                var view = root.AddComponent<ObliqueView>(); view.characterScale = 1;
                model = new Hero360Model(root.transform, view);
                Check(!ShaderUtil.ShaderHasError(model.Renderer.sharedMaterial.shader), "360 toon shader failed to import.");
                Check(model.Renderer.sharedMaterial.GetTexture("_DetailAtlas") == Resources.Load<Texture2D>("SlashDemo/WhiteSwordsmanAtlas"),
                    "Detailed model no longer references the original painted fabric.");
                foreach (float elevation in new[] { 30f, 45f, 65f })
                {
                    view.elevation = elevation;
                    for (int angle = 0; angle < 360; angle += 5)
                    {
                        Vector2 aim = DemoArt.Direction(angle);
                        Build(model, view, aim, SwordMotion.Evaluate(SwordMotion.Defaults()[0], 1), false, 0, true);
                        Check(Vector3.Dot(model.Root.forward, Hero360Model.FacingForward(aim, elevation)) > 0.99999f,
                            "Mesh orientation is quantized instead of following aim continuously.");
                        Check(model.Mesh.vertexCount > 1000 && model.Mesh.bounds.size.z > 0.1f, "Hero has no 3D volume.");
                        Check(model.Mesh.vertexCount < 100000 && model.Mesh.indexFormat == IndexFormat.UInt32 && model.Mesh.uv.Length == model.Mesh.vertexCount
                            && model.Mesh.uv2.Length == model.Mesh.vertexCount, "Detail geometry lost its UVs or exceeded its prototype vertex budget.");
                        foreach (var v in model.Mesh.vertices)
                            Check(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), "Mesh contains nonfinite vertices.");
                    }
                }
                view.elevation = 45;
                foreach (var strike in SwordMotion.Defaults())
                    for (int frame = 0; frame <= 20; frame++)
                    {
                        float time = frame / 20f;
                        float relaxed = HeroArmMath.SingleHandWeight(true, (int)strike.action, false, true, time, strike.windupRatio);
                        Build(model, view, Vector2.down, SwordMotion.Evaluate(strike, time), true, relaxed, false);
                        Check(model.Mesh.triangles.Length > 3000, "Attack pose lost geometry.");
                    }
                model.SetCharge(1);
                Build(model, view, Vector2.down, SwordMotion.HeavyPose(1, false, 0, 0, 0), true, 0, false);
                bool hdr = false;
                foreach (var c in model.Mesh.colors) if (c.b > 1.5f) hdr = true;
                Check(hdr, "Charged 3D blade lost its HDR brightness.");
                model.SetCharge(0);
                foreach (var entry in new[] { ("front", Vector2.down), ("side", Vector2.right), ("back", Vector2.up), ("diagonal", new Vector2(-0.6f, -0.8f)) })
                {
                    Build(model, view, entry.Item2, SwordMotion.Evaluate(SwordMotion.Defaults()[0], 1), false, 0, true);
                    Export(model, "Temp/Hero360-" + entry.Item1 + ".obj");
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Render(model, view, "Temp/Hero360-" + entry.Item1 + ".png");
                }
                Debug.Log("HERO360_NATIVE_PASS: " + count + " checks; OBJ snapshots in Temp/Hero360-*.obj. "
                    + (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ? "No GPU: PNG and actual shader rendering were not tested. " : "Four native PNG previews generated. ")
                    + "No scene was saved or rebuilt.");
            }
            finally
            {
                model?.Dispose(); UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void Build(Hero360Model model, ObliqueView view, Vector2 aim, SwordMotion.Pose pose, bool attack, float relaxed, bool sheathed)
        {
            model.SetFacing(aim, view.elevation);
            Vector2 socket = SwordMotion.CarryGrip;
            if (sheathed) pose = SwordMotion.EquipPose(pose, 0, out socket);
            model.SetBodyPose(pose, attack, view);
            Vector3 lift = view.Up * view.WeaponHeight;
            Vector2 axis = SwordMotion.ToWorld(DemoArt.Direction(pose.angle), aim);
            Vector3 grip = (Vector3)SwordMotion.ToWorld(pose.grip, aim) + lift;
            Vector3 right = grip - (Vector3)axis * 0.04f, left = grip - (Vector3)axis * 0.18f;
            if (sheathed)
            {
                SwordMotion.EquipHands(pose.grip - DemoArt.Direction(pose.angle) * 0.04f, socket, 0, out Vector2 main, out Vector2 off);
                right = (Vector3)SwordMotion.ToWorld(main, aim) + lift; left = (Vector3)SwordMotion.ToWorld(off, aim) + lift;
            }
            left = Vector3.Lerp(left, model.RelaxedLeftHand, relaxed);
            model.BuildFrame(right, left, grip, (Vector3)SwordMotion.ToWorld(pose.tip, aim) + lift,
                (Vector3)SwordMotion.ToWorld(socket, aim) + lift, SwordMotion.ToWorld(DemoArt.Direction(SwordMotion.CarryAngle), aim), sheathed, relaxed);
        }

        static void Export(Hero360Model model, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("# Exact articulated Unity mesh; world-space positions with per-vertex RGB.");
                Vector3[] vertices = model.Mesh.vertices; Color[] colors = model.Mesh.colors;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = model.Root.TransformPoint(vertices[i]); Color c = colors[i];
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2} {3} {4} {5}", p.x, p.y, p.z, c.r, c.g, c.b));
                }
                foreach (var uv in model.Mesh.uv) writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "vt {0} {1}", uv.x, uv.y));
                int[] indices = model.Mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3) writer.WriteLine("f " + (indices[i] + 1) + "/" + (indices[i] + 1) + " "
                    + (indices[i + 1] + 1) + "/" + (indices[i + 1] + 1) + " " + (indices[i + 2] + 1) + "/" + (indices[i + 2] + 1));
            }
        }
        static void Render(Hero360Model model, ObliqueView view, string path)
        {
            var go = new GameObject("360 preview camera") { hideFlags = HideFlags.HideAndDontSave };
            var target = RenderTexture.GetTemporary(1024, 1024, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var camera = go.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 1.05f; camera.aspect = 1;
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 20;
                camera.transform.position = -ObliqueProjection.Forward(view.elevation) * 6 + view.Up * 0.7f;
                camera.transform.rotation = Quaternion.LookRotation(ObliqueProjection.Forward(view.elevation), view.Up);
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target);
                    commands.ClearRenderTarget(true, true, new Color(0.028f, 0.048f, 0.066f, 1));
                    commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
                    commands.DrawMesh(model.Mesh, model.Root.localToWorldMatrix, model.Renderer.sharedMaterial, 0, 0);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target;
                pixels = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); pixels.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, pixels.EncodeToPNG());
                Check(!ShaderUtil.ShaderHasError(model.Renderer.sharedMaterial.shader), "Shader failed during native rendering.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(go);
            }
        }
        public static void RunBatch()
        {
            try { ValidateAndRender(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogError("HERO360_NATIVE_FAIL: " + error); EditorApplication.Exit(1); }
        }
    }
}
