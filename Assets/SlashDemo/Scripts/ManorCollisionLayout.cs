using System;
using UnityEngine;

namespace SlashDemo
{
    public static class ManorCollisionLayout
    {
        public const string PerimeterName = "Red line walkable perimeter";
        [Serializable]
        sealed class Layout
        {
            public int referenceWidth = 0, referenceHeight = 0;
            public Vector2[] perimeter = Array.Empty<Vector2>(), table = Array.Empty<Vector2>();
        }
        static Layout layout;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { layout = null; }

        static Layout Data
        {
            get
            {
                if (layout != null) return layout;
                var source = Resources.Load<TextAsset>("SlashDemo/ManorCollision-v1");
                if (source == null) throw new InvalidOperationException("Missing annotated manor collision layout.");
                layout = JsonUtility.FromJson<Layout>(source.text);
                if (layout.referenceWidth != 1536 || layout.referenceHeight != 1024
                    || layout.perimeter.Length < 4 || layout.table.Length < 3)
                    throw new InvalidOperationException("Invalid annotated manor collision reference coordinates.");
                return layout;
            }
        }

        public static void Build(Transform parent)
        {
            var edge = new GameObject(PerimeterName).AddComponent<EdgeCollider2D>();
            edge.transform.SetParent(parent, false);
            edge.points = GroundPoints(Data.perimeter);
            edge.edgeRadius = 0.03f;
            Solid(parent, "Tea table", Data.table);
            Box(parent, "Gate left leaf", new Rect(572, 817, 57, 22));
            Box(parent, "Gate right leaf", new Rect(722, 817, 57, 22));
        }

        static Vector2[] GroundPoints(Vector2[] reference)
        {
            var result = new Vector2[reference.Length];
            for (int i = 0; i < result.Length; i++) result[i] = ManorEnvironment.Ground(reference[i].x, reference[i].y);
            return result;
        }

        static void Solid(Transform parent, string name, Vector2[] reference)
        {
            var collider = new GameObject(name).AddComponent<PolygonCollider2D>();
            collider.transform.SetParent(parent, false);
            collider.points = GroundPoints(reference);
        }

        static void Box(Transform parent, string name, Rect reference)
        {
            Rect bounds = ManorEnvironment.GroundRect(reference);
            var collider = new GameObject(name).AddComponent<BoxCollider2D>();
            collider.transform.SetParent(parent, false);
            collider.transform.localPosition = bounds.center;
            collider.size = bounds.size;
        }

        // Rendering footprints are independent of the annotated movement boundary.
        public static Rect FurnitureFootprint(string name)
        {
            switch (name)
            {
                case "Tea table": return ManorEnvironment.GroundRect(ReferenceBounds(Data.table));
                case "Wardrobe": return ManorEnvironment.GroundRect(new Rect(318, 436, 111, 71));
                case "Bed": return ManorEnvironment.GroundRect(new Rect(168, 460, 108, 126));
                case "Bedroom screen": return ManorEnvironment.GroundRect(new Rect(231, 615, 85, 35));
                case "Alchemy furnace": return ManorEnvironment.GroundRect(new Rect(353, 182, 54, 51));
                default: throw new ArgumentException("Unknown furniture footprint: " + name);
            }
        }

        static Rect ReferenceBounds(Vector2[] points)
        {
            Vector2 min = points[0], max = points[0];
            foreach (var p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static bool ContainsGround(Vector2 ground)
        {
            Vector2 reference = new Vector2((ground.x / ManorEnvironment.WorldSize.x + 0.5f) * 1536,
                (0.5f - ground.y / ManorEnvironment.WorldSize.y) * 1024);
            return Inside(Data.perimeter, reference) && !Inside(Data.table, reference);
        }

        static bool Inside(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
