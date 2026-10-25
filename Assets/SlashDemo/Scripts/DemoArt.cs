using UnityEngine;

namespace SlashDemo
{
    // Small, original geometric placeholders. Replace the sprites without changing combat code.
    public static class DemoArt
    {
        public static readonly Color Jade = new Color(0.39f, 0.94f, 0.83f);
        public static readonly Color Gold = new Color(1f, 0.73f, 0.35f);
        public static readonly Color Red = new Color(0.97f, 0.35f, 0.37f);
        static Material material;
        static Material lineMaterial;
        public static Material LineMaterial
        {
            get
            {
                if (lineMaterial == null)
                {
                    lineMaterial = Resources.Load<Material>("SlashDemo/LineUnlit");
                    if (lineMaterial == null) lineMaterial = new Material(Resources.Load<Shader>("SlashDemo/BladeTrail"));
                }
                return lineMaterial;
            }
        }

        public static Material Material
        {
            get
            {
                if (material == null)
                {
                    material = Resources.Load<Material>("SlashDemo/Unlit");
                    if (material == null)
                        material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
                }
                return material;
            }
        }

        public static SpriteRenderer Shape(string name, Transform parent, string sprite, Vector2 position,
            Vector2 size, Color color, int order = 0, float angle = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            go.transform.localRotation = Quaternion.Euler(0, 0, angle);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Resources.Load<Sprite>("SlashDemo/" + sprite);
            renderer.sharedMaterial = Material;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        public static LineRenderer Line(string name, Transform parent, Vector3[] points, Color color,
            float width, int order, bool loop = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = LineMaterial;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 0;
            line.loop = loop;
            line.sortingOrder = order;
            return line;
        }

        public static LineRenderer Ring(string name, Transform parent, float radius, Color color,
            float width, int order, int segments = 64)
        {
            var points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                points[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return Line(name, parent, points, color, width, order, true);
        }

        public static Rigidbody2D Body(GameObject go, float radius, float mass = 1)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius = radius;
            return body;
        }

        public static Transform Character(Transform parent, bool enemy, bool dummy = false)
        {
            var model = new GameObject("Visual").transform;
            model.SetParent(parent, false);
            Shape("Shadow", model, "Disk", new Vector2(0, -0.32f), new Vector2(0.92f, 0.32f), new Color(0, 0, 0, 0.35f), 0);
            if (dummy)
            {
                Shape("Post", model, "Square", new Vector2(0, -0.1f), new Vector2(0.15f, 0.8f), new Color(0.34f, 0.26f, 0.21f), 1);
                Shape("Arms", model, "Square", new Vector2(0, 0.08f), new Vector2(0.95f, 0.12f), Gold * 0.65f, 2);
                Shape("Body", model, "Robe", Vector2.zero, new Vector2(0.58f, 0.78f), new Color(0.57f, 0.43f, 0.26f), 3);
                Shape("Target", model, "Disk", new Vector2(0, 0.1f), Vector2.one * 0.28f, Gold, 4);
                Shape("TargetCenter", model, "Disk", new Vector2(0, 0.1f), Vector2.one * 0.12f, Red, 5);
                return model;
            }
            Shape("BootL", model, "Square", new Vector2(-0.15f, -0.26f), new Vector2(0.17f, 0.24f), new Color(0.08f, 0.11f, 0.14f), 1, -8);
            Shape("BootR", model, "Square", new Vector2(0.15f, -0.26f), new Vector2(0.17f, 0.24f), new Color(0.08f, 0.11f, 0.14f), 1, 8);
            Shape("Body", model, "Robe", Vector2.zero, new Vector2(0.82f, 0.88f), enemy ? new Color(0.38f, 0.17f, 0.24f) : new Color(0.16f, 0.36f, 0.40f), 2);
            Shape("Chest", model, "Robe", new Vector2(0.02f, 0.06f), new Vector2(0.41f, 0.55f), enemy ? new Color(0.65f, 0.28f, 0.29f) : new Color(0.37f, 0.66f, 0.65f), 3, -8);
            Shape("Belt", model, "Square", new Vector2(0, -0.13f), new Vector2(0.58f, 0.09f), enemy ? Red : Gold, 4);
            Shape("Head", model, "Disk", new Vector2(0, 0.39f), new Vector2(0.44f, 0.44f), new Color(0.09f, 0.13f, 0.17f), 5);
            Shape("Hat", model, "Hat", new Vector2(0, 0.55f), new Vector2(0.88f, 0.35f), enemy ? new Color(0.36f, 0.23f, 0.28f) : new Color(0.65f, 0.73f, 0.67f), 6);
            Shape("Eyes", model, "Square", new Vector2(0, 0.36f), new Vector2(0.22f, 0.045f), enemy ? Red : Jade, 7);
            Shape("Scarf", model, "Blade", new Vector2(-0.27f, 0.18f), new Vector2(0.65f, 0.2f), enemy ? Red : Jade, 4, 155);
            return model;
        }

        public static Vector2 Direction(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
    }
}
