using UnityEngine;
using UnityEngine.Rendering;

namespace SlashDemo
{
    public static class ObliqueScenery
    {
        // Actual raised faces and top caps, while the original floor colliders stay in XY.
        public static void Build(Transform environment, ObliqueView view)
        {
            var sprites = environment.GetComponentsInChildren<SpriteRenderer>();
            var root = new GameObject("Oblique Architecture").transform;
            root.SetParent(environment, false);
            foreach (var sprite in sprites)
            {
                if (sprite.name == "Arena boundary")
                {
                    sprite.enabled = false;
                    Vector2 size = sprite.transform.localScale;
                    Vector2 position = sprite.transform.localPosition;
                    if (size.y > size.x)
                    {
                        int pieces = Mathf.CeilToInt(size.y / 1.4f);
                        for (int i = 0; i < pieces; i++)
                            Box(root, position + Vector2.up * (-size.y / 2 + size.y / pieces * (i + 0.5f)),
                                new Vector2(size.x, size.y / pieces - 0.025f), 0.80f, new Color(0.20f, 0.32f, 0.34f));
                    }
                    else Box(root, position, size, 0.80f, new Color(0.20f, 0.32f, 0.34f));
                }
                else if (sprite.name == "Lantern base")
                {
                    sprite.enabled = false;
                    Vector2 position = sprite.transform.localPosition;
                    // Put raised lanterns on the wall footprint, outside the unchanged walkable floor.
                    position.y += Mathf.Sign(position.y) * 0.4f;
                    var lantern = Box(root, position, new Vector2(0.46f, 0.38f), 1.10f, new Color(0.29f, 0.38f, 0.38f));
                    lantern.GetComponent<SortingGroup>().sortingOrder++;
                    var light = DemoArt.Shape("Lantern flame", lantern, "Disk", Vector2.zero, new Vector2(0.20f, 0.28f), DemoArt.Gold, 5);
                    light.transform.localPosition = new Vector3(0, 0, -1.24f);
                    light.transform.localRotation = view.Rotation;
                }
                else if (sprite.name == "Lantern light") sprite.enabled = false;
            }
            // Entrance steps and a pair of stone markers make the floor/height distinction readable.
            Box(root, new Vector2(-1.2f, -5.7f), new Vector2(2.4f, 0.7f), 0.12f, new Color(0.26f, 0.35f, 0.35f));
            Box(root, new Vector2(-1.2f, -6.1f), new Vector2(2.7f, 0.55f), 0.06f, new Color(0.19f, 0.27f, 0.29f));
        }

        static Color Shade(Color color, float factor) => new Color(color.r * factor, color.g * factor, color.b * factor, color.a);

        static Transform Box(Transform parent, Vector2 position, Vector2 size, float height, Color color)
        {
            var root = new GameObject("Raised stone").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.gameObject.AddComponent<SortingGroup>().sortingOrder = ObliqueProjection.DepthOrder(position.y);
            var top = DemoArt.Shape("Top", root, "Square", Vector2.zero, size, color, 2);
            top.transform.localPosition = new Vector3(0, 0, -height);
            var front = DemoArt.Shape("Front face", root, "Square", Vector2.zero, new Vector2(size.x, height), Shade(color, 0.64f), 1);
            front.transform.localPosition = new Vector3(0, -size.y / 2, -height / 2);
            front.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var left = DemoArt.Shape("Side face", root, "Square", Vector2.zero, new Vector2(height, size.y), Shade(color, 0.48f), 0);
            left.transform.localPosition = new Vector3(-size.x / 2, 0, -height / 2);
            left.transform.localRotation = Quaternion.Euler(0, 90, 0);
            // Brighter lip on the cap makes the oblique silhouette visible against the slate floor.
            var lip = DemoArt.Shape("Cap edge", root, "Square", new Vector2(0, -size.y / 2), new Vector2(size.x, 0.045f), Shade(color, 1.2f), 3);
            lip.transform.localPosition = new Vector3(0, -size.y / 2, -height - 0.005f);
            return root;
        }
    }
}
