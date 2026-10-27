using System.IO;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class ManorCollisionPreview
    {
        public static void Render(SlashArena arena, string path)
        {
            var root = new GameObject("Annotated collision preview");
            var cameraObject = new GameObject("Collision preview camera");
            var texture = Resources.Load<Texture2D>(ManorEnvironment.ResourcePath);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f,
                texture.width / ManorEnvironment.WorldSize.x, 0, SpriteMeshType.FullRect);
            var target = RenderTexture.GetTemporary(1536, 1024, 24);
            var image = new Texture2D(1536, 1024, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            try
            {
                var paint = root.AddComponent<SpriteRenderer>(); paint.sprite = sprite; paint.sharedMaterial = DemoArt.Material;
                root.layer = 30; paint.sortingOrder = -10000;
                var manor = arena.GetComponentInChildren<ManorEnvironment>();
                foreach (var collider in manor.GetComponentsInChildren<Collider2D>())
                {
                    Vector2[] outline;
                    if (collider is EdgeCollider2D edge) outline = edge.points;
                    else if (collider is PolygonCollider2D polygon)
                    {
                        var points = polygon.points; outline = new Vector2[points.Length + 1];
                        points.CopyTo(outline, 0); outline[points.Length] = points[0];
                    }
                    else if (collider is BoxCollider2D box)
                        outline = new[] { -box.size * 0.5f, new Vector2(box.size.x, -box.size.y) * 0.5f,
                            box.size * 0.5f, new Vector2(-box.size.x, box.size.y) * 0.5f, -box.size * 0.5f };
                    else continue;
                    var world = new Vector3[outline.Length];
                    for (int i = 0; i < world.Length; i++) world[i] = collider.transform.TransformPoint(outline[i] + collider.offset);
                    var line = DemoArt.Line(collider.name, root.transform, world, new Color(1, 0.22f, 0.25f), 0.075f, 30000);
                    line.gameObject.layer = 30;
                }
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = ManorEnvironment.WorldSize.y * 0.5f;
                camera.transform.position = new Vector3(0, 0, -20); camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1536, 1024), 0, 0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("MANOR_COLLISION_PREVIEW: " + path);
            }
            finally
            {
                RenderTexture.active = active; RenderTexture.ReleaseTemporary(target);
                Object.Destroy(cameraObject); Object.Destroy(root); Object.Destroy(sprite); Object.Destroy(image);
            }
        }
    }
}
