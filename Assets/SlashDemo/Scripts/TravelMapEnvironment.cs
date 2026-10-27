using System;
using UnityEngine;

namespace SlashDemo
{
    public enum TravelDestination { Manor, AscensionPlatform, Wilderness, HealingLodge }

    // All coordinates use the artwork's 1536 x 1024 reference, like the manor.
    public sealed class TravelMapDefinition
    {
        public readonly TravelDestination destination;
        public readonly string title, resource;
        public readonly Vector2 entry;
        public readonly Rect reservedExit;
        public readonly Vector2[] boundary;
        public readonly Vector2[][] obstacles;

        public TravelMapDefinition(TravelDestination destination, string title, string resource,
            Vector2 entry, Rect reservedExit, Vector2[] boundary, params Vector2[][] obstacles)
        {
            this.destination = destination; this.title = title; this.resource = resource;
            this.entry = entry; this.reservedExit = reservedExit;
            this.boundary = boundary; this.obstacles = obstacles;
        }
        public Vector2 Entry => ManorEnvironment.Ground(entry.x, entry.y);
        public Color MistColor => destination == TravelDestination.Wilderness
            ? new Color(0.48f, 0.53f, 0.44f) : new Color(0.70f, 0.74f, 0.70f);
    }

    public static class TravelMaps
    {
        public static readonly TravelMapDefinition[] Choices = {
            new TravelMapDefinition(TravelDestination.AscensionPlatform, "升仙台", "SlashDemo/AscensionPlatform-v1",
                new Vector2(755, 865), new Rect(665, 955, 170, 40),
                Points(580,150, 995,150, 1235,235, 1315,465, 1160,620, 915,685,
                    860,735, 855,1005, 645,1005, 645,780, 665,695, 405,620,
                    190,480, 225,260, 415,188, 580,150)),
            new TravelMapDefinition(TravelDestination.Wilderness, "山野", "SlashDemo/Wilderness-v1",
                new Vector2(810, 865), new Rect(745, 955, 155, 40),
                Points(300,255, 485,205, 705,175, 735,105, 790,35, 900,35,
                    860,150, 960,180, 1120,215, 1255,300, 1285,440, 1310,560,
                    1260,655, 1145,720, 1015,745, 915,820, 925,1005, 730,1005,
                    730,850, 650,785, 455,745, 300,665, 245,525, 185,410, 300,255)),
            HealingLodge()
        };

        [Serializable]
        sealed class AnnotatedLodge
        {
            public int referenceWidth = 0, referenceHeight = 0;
            public Vector2 entry = Vector2.zero;
            public Vector2[] perimeter = Array.Empty<Vector2>();
        }

        static TravelMapDefinition HealingLodge()
        {
            var source = Resources.Load<TextAsset>("SlashDemo/HealingLodgeCollision-v1");
            if (source == null) throw new InvalidOperationException("Missing annotated healing lodge collision layout.");
            var layout = JsonUtility.FromJson<AnnotatedLodge>(source.text);
            if (layout == null || layout.referenceWidth != 1536 || layout.referenceHeight != 1024
                || layout.perimeter.Length < 4 || layout.perimeter[0] != layout.perimeter[layout.perimeter.Length - 1])
                throw new InvalidOperationException("Invalid healing lodge collision reference coordinates.");
            // Beds, stools, cabinets and walls are excluded by the same traced perimeter.
            return new TravelMapDefinition(TravelDestination.HealingLodge, "疗伤医庐", "SlashDemo/HealingLodge-open-v1",
                layout.entry, new Rect(1320, 970, 125, 35), layout.perimeter);
        }

        public static TravelMapDefinition Get(TravelDestination destination)
        {
            foreach (var map in Choices) if (map.destination == destination) return map;
            throw new ArgumentOutOfRangeException(nameof(destination));
        }

        static Vector2[] Points(params float[] coordinates)
        {
            var points = new Vector2[coordinates.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(coordinates[i * 2], coordinates[i * 2 + 1]);
            return points;
        }
    }

    public sealed class TravelMapEnvironment : MonoBehaviour
    {
        Sprite paint;
        Sprite gateRoof;
        Material material;
        public TravelMapDefinition Definition { get; private set; }
        public BoxCollider2D ReservedExit { get; private set; }

        public static TravelMapEnvironment Build(Transform parent, TravelMapDefinition definition)
        {
            var texture = Resources.Load<Texture2D>(definition.resource);
            if (texture == null) throw new InvalidOperationException("Missing travel background: " + definition.resource);
            var root = new GameObject(definition.title).AddComponent<TravelMapEnvironment>();
            root.transform.SetParent(parent, false);
            root.Definition = definition;
            root.paint = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                Vector2.one * 0.5f, texture.width / ManorEnvironment.WorldSize.x, 0, SpriteMeshType.FullRect);
            var renderer = root.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = root.paint;
            renderer.sortingOrder = -10000;
            var shader = Resources.Load<Shader>("SlashDemo/ManorSurface");
            if (shader != null)
            {
                root.material = new Material(shader) { name = definition.title + " surface" };
                root.material.SetInt("_ManorProtectCount", 0);
                root.material.SetFloat("_SceneEdgeFade", 0.045f);
                root.material.SetColor("_SceneEdgeColor", definition.MistColor);
            }
            renderer.sharedMaterial = root.material != null ? root.material : DemoArt.Material;
            var edge = new GameObject("Walkable boundary").AddComponent<EdgeCollider2D>();
            edge.transform.SetParent(root.transform, false);
            edge.points = ToGround(definition.boundary);
            edge.edgeRadius = 0.03f;
            foreach (var obstacle in definition.obstacles)
            {
                var collider = new GameObject("Furniture footprint").AddComponent<PolygonCollider2D>();
                collider.transform.SetParent(root.transform, false);
                collider.points = ToGround(obstacle);
            }
            // A reserved, deliberately unlinked exit. No handler or destination is attached.
            var exit = new GameObject("Reserved exit - unlinked").AddComponent<BoxCollider2D>();
            exit.transform.SetParent(root.transform, false);
            var bounds = ManorEnvironment.GroundRect(definition.reservedExit);
            exit.transform.localPosition = bounds.center;
            exit.size = bounds.size;
            exit.isTrigger = true;
            root.ReservedExit = exit;
            return root;
        }

        void Start()
        {
            // OverrideGeometry must run in the player loop, including when a
            // validation/editor callback initiates travel.
            if (Definition.destination == TravelDestination.HealingLodge) BuildGateRoof(paint.texture);
        }

        void BuildGateRoof(Texture2D texture)
        {
            // Register a silhouette from the original painting. A full rectangular
            // crop would incorrectly paint courtyard floor over the character.
            var pixels = new Rect(1090, 590, 278, 180);
            float scaleX = texture.width / 1536f, scaleY = texture.height / 1024f;
            gateRoof = Sprite.Create(texture,
                new Rect(pixels.x * scaleX, (1024 - pixels.yMax) * scaleY, pixels.width * scaleX, pixels.height * scaleY),
                Vector2.one * 0.5f, texture.width / ManorEnvironment.WorldSize.x, 0, SpriteMeshType.FullRect);
            gateRoof.name = "Healing lodge gate roof silhouette";
            ManorMaskGeometry.Apply(gateRoof, pixels, ManorMaskGeometry.Outline(
                1094,700, 1108,697, 1108,685, 1121,688, 1130,679, 1155,674,
                1193,656, 1230,637, 1267,616, 1292,604, 1309,596, 1313,605,
                1311,618, 1326,615, 1343,601, 1350,596, 1350,608, 1356,619,
                1353,631, 1352,641, 1354,651, 1352,669, 1339,680, 1326,688,
                1311,696, 1289,706, 1260,723, 1229,740, 1202,754, 1188,758,
                1171,750, 1154,736, 1132,731, 1115,722, 1097,723, 1092,716));
            var roof = new GameObject("Gate roof - overhead occlusion").AddComponent<SpriteRenderer>();
            roof.transform.SetParent(transform, false);
            roof.transform.localPosition = ManorEnvironment.GroundRect(pixels).center;
            roof.sprite = gateRoof;
            roof.sharedMaterial = material != null ? material : DemoArt.Material;
            // The roof is above this passage even when the actor's feet are south
            // of it. Keep it above actor depth sorting, with no ground collider.
            roof.sortingOrder = 10000;
        }

        static Vector2[] ToGround(Vector2[] reference)
        {
            var points = new Vector2[reference.Length];
            for (int i = 0; i < points.Length; i++) points[i] = ManorEnvironment.Ground(reference[i].x, reference[i].y);
            return points;
        }

        void OnDestroy()
        {
            if (paint != null) Destroy(paint);
            if (gateRoof != null) Destroy(gateRoof);
            if (material != null) Destroy(material);
        }
    }
}
