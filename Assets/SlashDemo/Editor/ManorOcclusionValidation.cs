using System;
using System.IO;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class ManorOcclusionValidation
    {
        public static void Run(SlashArena arena, string folder)
        {
            var manor = arena.GetComponentInChildren<ManorEnvironment>();
            var occlusion = manor.GetComponent<ManorOcclusion>();
            var root = manor.transform.Find("Foreground - depth sorted architecture");
            if (occlusion == null) throw new Exception("Missing manor occlusion handling.");
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>())
            {
                var vertices = renderer.sprite.vertices;
                var triangles = renderer.sprite.triangles;
                float area = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector2 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    area += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5f;
                }
                float cropArea = renderer.sprite.rect.width * renderer.sprite.rect.height
                    / (renderer.sprite.pixelsPerUnit * renderer.sprite.pixelsPerUnit);
                if (vertices.Length <= 4 || area <= 0 || area >= cropArea * 0.999f)
                    throw new Exception("Foreground still includes its full opaque crop: " + renderer.name);
                var uv = renderer.sprite.uv;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector2 pixel = vertices[i] * renderer.sprite.pixelsPerUnit + renderer.sprite.pivot + renderer.sprite.rect.position;
                    Vector2 expected = new Vector2(pixel.x / renderer.sprite.texture.width, pixel.y / renderer.sprite.texture.height);
                    if (Vector2.Distance(uv[i], expected) > 0.00001f) throw new Exception("Foreground UV registration shifted: " + renderer.name);
                }
            }

            var player = arena.Player;
            var body = player.GetComponent<Rigidbody2D>();
            Vector3 oldPosition = player.transform.position;
            float oldSize = arena.WorldCamera.orthographicSize;
            int originalFailures = 0, behindVisible = 0;
            try
            {
                foreach (var test in new[] {
                    ("Tea table", 594.5f, 234f, "table-left"), ("Tea table", 773.5f, 234f, "table-right"),
                    ("Tea table", 680f, 291f, "table-front"),
                    ("Alchemy furnace", 343.5f, 205f, "furnace-left"), ("Alchemy furnace", 416.5f, 205f, "furnace-right"),
                    ("Alchemy furnace", 380f, 245f, "furnace-front"),
                    ("Open wardrobe", 306f, 470f, "wardrobe-left"), ("Open wardrobe", 380f, 519f, "wardrobe-front"),
                    ("Bed", 288f, 520f, "bed-right"), ("Bed", 220f, 598f, "bed-front"),
                    ("Bedroom screen", 219f, 620f, "screen-left"), ("Bedroom screen", 328f, 620f, "screen-right") })
                {
                    Position(arena, occlusion, test.Item2, test.Item3);
                    var actorMask = CaptureActor(arena);
                    var renderer = root.Find(test.Item1).GetComponent<SpriteRenderer>();
                    var fixedFrame = Capture(arena.WorldCamera);
                    Texture2D without;
                    renderer.enabled = false;
                    try { without = Capture(arena.WorldCamera); }
                    finally { renderer.enabled = true; }
                    int difference = Difference(fixedFrame, without, actorMask);
                    if (difference > 3)
                    {
                        Directory.CreateDirectory(folder);
                        File.WriteAllBytes(Path.Combine(folder, "Manor-fail-" + test.Item4 + ".png"), fixedFrame.EncodeToPNG());
                        File.WriteAllBytes(Path.Combine(folder, "Manor-fail-" + test.Item4 + "-expected.png"), without.EncodeToPNG());
                        throw new Exception("Clothes still disappear beside " + test.Item1 + ": " + test.Item4 + ", pixels=" + difference);
                    }

                    // Reproduce the old opaque rectangular crop to prove the case exercises the bug.
                    var original = renderer.sprite;
                    var rectangle = Sprite.Create(original.texture, original.rect, Vector2.one * 0.5f,
                        original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    Texture2D legacy;
                    try
                    {
                        renderer.sprite = rectangle;
                        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                        block.SetInt("_ManorProtectCount", 0); renderer.SetPropertyBlock(block);
                        legacy = Capture(arena.WorldCamera);
                    }
                    finally
                    {
                        renderer.sprite = original;
                        UnityEngine.Object.Destroy(rectangle);
                        occlusion.Refresh();
                    }
                    int oldDifference = Difference(legacy, without, actorMask);
                    if (oldDifference > 12) originalFailures++;
                    Debug.Log("MANOR_OCCLUSION_CASE: " + test.Item4 + " fixed=" + difference + " legacy=" + oldDifference);
                    if (test.Item4 == "table-left" || test.Item4 == "furnace-left")
                    {
                        Directory.CreateDirectory(folder);
                        File.WriteAllBytes(Path.Combine(folder, "Manor-" + test.Item4 + ".png"), fixedFrame.EncodeToPNG());
                        File.WriteAllBytes(Path.Combine(folder, "Manor-" + test.Item4 + "-before.png"), legacy.EncodeToPNG());
                    }
                    UnityEngine.Object.Destroy(fixedFrame); UnityEngine.Object.Destroy(without); UnityEngine.Object.Destroy(legacy);
                    UnityEngine.Object.Destroy(actorMask);
                }
                foreach (var test in new[] { ("Alchemy furnace", 380f, 170f), ("Bed", 220f, 448f),
                    ("Open wardrobe", 380f, 424f), ("Tea table", 638f, 183f) })
                {
                    Position(arena, occlusion, test.Item2, test.Item3);
                    var actorMask = CaptureActor(arena);
                    var renderer = root.Find(test.Item1).GetComponent<SpriteRenderer>();
                    var normal = Capture(arena.WorldCamera);
                    Texture2D without;
                    renderer.enabled = false;
                    try { without = Capture(arena.WorldCamera); }
                    finally { renderer.enabled = true; }
                    int difference = Difference(normal, without, actorMask);
                    if (difference > 3) behindVisible++;
                    Debug.Log("MANOR_OCCLUSION_BEHIND: " + test.Item1 + " occluded=" + difference);
                    UnityEngine.Object.Destroy(normal); UnityEngine.Object.Destroy(without);
                    UnityEngine.Object.Destroy(actorMask);
                }
                if (originalFailures < 2) throw new Exception("The render checks did not reproduce the original occlusion bug.");
                if (behindVisible < 2) throw new Exception("Furniture no longer occludes actors standing behind it.");
                Debug.Log("MANOR_OCCLUSION_PASS: all 11 foreground silhouettes, 12 side/front render cases; "
                    + originalFailures + " old failures reproduced, " + behindVisible + " behind-object occlusions retained.");
            }
            finally
            {
                player.transform.position = oldPosition; body.position = oldPosition;
                player.GetComponent<ObliqueActor>().SetPose(Vector2.zero, 0, 0);
                arena.CameraRig.ResetFeedback(); arena.WorldCamera.orthographicSize = oldSize;
                Physics2D.SyncTransforms(); occlusion.Refresh();
            }
        }

        static void Position(SlashArena arena, ManorOcclusion occlusion, float x, float y)
        {
            Vector2 position = ManorEnvironment.Ground(x, y);
            arena.Player.transform.position = position;
            arena.Player.GetComponent<Rigidbody2D>().position = position;
            arena.Player.GetComponent<ObliqueActor>().SetPose(Vector2.zero, 0, 0);
            arena.CameraRig.ResetFeedback(); arena.WorldCamera.orthographicSize = 2.8f;
            Physics2D.SyncTransforms(); occlusion.Refresh();
        }

        internal static Texture2D Capture(Camera camera)
        {
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(640, 480, 24);
            var image = new Texture2D(640, 480, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); image.Apply(); return image;
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        internal static Texture2D CaptureActor(SlashArena arena)
        {
            var renderers = arena.Player.GetComponentsInChildren<Renderer>(true);
            var layers = new int[renderers.Length];
            var camera = arena.WorldCamera;
            int mask = camera.cullingMask; Color background = camera.backgroundColor;
            var data = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool postProcessing = data.renderPostProcessing;
            try
            {
                for (int i = 0; i < renderers.Length; i++)
                { layers[i] = renderers[i].gameObject.layer; renderers[i].gameObject.layer = 31; }
                camera.cullingMask = 1 << 31; camera.backgroundColor = Color.black; data.renderPostProcessing = false;
                return Capture(camera);
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].gameObject.layer = layers[i];
                camera.cullingMask = mask; camera.backgroundColor = background; data.renderPostProcessing = postProcessing;
            }
        }

        internal static int Difference(Texture2D a, Texture2D b, Texture2D actorMask)
        {
            var left = a.GetPixels32(); var right = b.GetPixels32(); var mask = actorMask.GetPixels32(); int count = 0;
            for (int y = 90; y < 390; y++) for (int x = 210; x < 430; x++)
            {
                int i = y * 640 + x;
                if (mask[i].r + mask[i].g + mask[i].b < 100) continue;
                int difference = Mathf.Abs(left[i].r - right[i].r) + Mathf.Abs(left[i].g - right[i].g) + Mathf.Abs(left[i].b - right[i].b);
                if (difference > 24) count++;
            }
            return count;
        }
    }
}
