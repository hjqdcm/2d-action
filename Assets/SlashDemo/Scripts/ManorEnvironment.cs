using System;
using System.Collections.Generic;
using UnityEngine;
using static SlashDemo.ManorMaskGeometry;

namespace SlashDemo
{
    // The painted manor is registered to XY, the same ground plane as the existing combat.
    // All art coordinates below use the 1536 x 1024 reference, measured from its top left.
    public sealed class ManorEnvironment : MonoBehaviour
    {
        public const string ResourcePath = "SlashDemo/PrivateManor-v4";
        public const float LayoutScale = 2;
        public static readonly Vector2 WorldSize = new Vector2(24, 16) * LayoutScale;
        public static Vector2 SpawnPoint => Ground(670, 520);
        public static Vector2 TrainingCenter => Ground(1240, 370);
        public static Rect TrainingBounds => GroundRect(new Rect(1080, 200, 322, 365));
        readonly List<Sprite> generatedSprites = new List<Sprite>();
        Material surfaceMaterial;
        ManorOcclusion occlusion;

        public static Vector2 Ground(float x, float y)
            => new Vector2((x / 1536f - 0.5f) * WorldSize.x, (0.5f - y / 1024f) * WorldSize.y);

        public static Rect GroundRect(Rect pixels)
        {
            Vector2 min = Ground(pixels.xMin, pixels.yMax);
            Vector2 max = Ground(pixels.xMax, pixels.yMin);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Vector2 DummyPoint(int index)
        {
            switch (index % 3)
            {
                case 0: return Ground(1160, 390);
                case 1: return Ground(1310, 285);
                default: return Ground(1310, 490);
            }
        }

        public static Vector2 WavePoint(int index, int count, int wave)
        {
            float angle = (index / (float)count * 360 + wave * 33) * Mathf.Deg2Rad;
            return TrainingCenter + new Vector2(Mathf.Cos(angle) * 1.85f, Mathf.Sin(angle) * 2.05f) * LayoutScale;
        }

        public static bool InTraining(Vector2 position) => TrainingBounds.Contains(position);

        public static void Build(Transform environment)
        {
            var manor = new GameObject("Private Manor").AddComponent<ManorEnvironment>();
            manor.transform.SetParent(environment, false);
            manor.Build();
            // PrepareEnvironment must not add the old closed courtyard walls or entrance steps.
            new GameObject("Oblique Architecture").transform.SetParent(environment, false);
        }

        void Build()
        {
            var texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null) throw new InvalidOperationException("Missing private manor texture: " + ResourcePath);
            var surfaceShader = Resources.Load<Shader>("SlashDemo/ManorSurface");
            surfaceMaterial = surfaceShader != null ? new Material(surfaceShader) { name = "Manor detail sampling" } : null;
            if (surfaceMaterial != null) surfaceMaterial.SetInt("_ManorProtectCount", 0);
            occlusion = gameObject.AddComponent<ManorOcclusion>();
            occlusion.Initialize(GetComponentInParent<SlashArena>());
            var art = new GameObject("Manor ground - painted rooms and garden").AddComponent<SpriteRenderer>();
            art.transform.SetParent(transform, false);
            art.sprite = MakeSprite(texture, new Rect(0, 0, 1536, 1024));
            art.sharedMaterial = surfaceMaterial != null ? surfaceMaterial : DemoArt.Material;
            // The enlarged north rooms can have negative actor depth orders.
            art.sortingOrder = -10000;

            var collisions = new GameObject("Walls furniture and pond - ground collisions").transform;
            collisions.SetParent(transform, false);
            ManorCollisionLayout.Build(collisions);

            // Only real silhouettes may occlude actors; never the surrounding floor in a crop.
            var foreground = new GameObject("Foreground - depth sorted architecture").transform;
            foreground.SetParent(transform, false);
            Patch(texture, foreground, "Tea table", new Rect(597, 166, 169, 129), 277, "Tea table",
                Outline(606,194, 758,194, 766,201, 765,252, 761,270, 751,270, 751,258, 621,258, 619,270, 608,270, 606,257, 602,250, 603,201),
                Outline(630,171, 650,172, 656,180, 655,196, 630,197, 625,187, 627,179),
                Outline(718,172, 737,173, 743,180, 741,197, 717,197, 713,186, 715,178),
                Outline(630,254, 658,254, 662,263, 659,287, 632,290, 626,279, 628,263),
                Outline(721,254, 746,254, 750,266, 746,289, 721,289, 716,277, 718,263));
            Patch(texture, foreground, "Open wardrobe", new Rect(315, 411, 113, 98), 507, "Wardrobe",
                Outline(318,415, 423,415, 428,426, 428,501, 418,507, 318,506, 315,499, 315,427));
            Patch(texture, foreground, "Bed", new Rect(166, 436, 113, 156), 586, "Bed",
                Outline(174,440, 266,439, 272,455, 277,571, 271,586, 172,587, 166,578, 167,461));
            Patch(texture, foreground, "Bedroom screen", new Rect(228, 608, 98, 66), 650, "Bedroom screen",
                Outline(234,610, 312,612, 319,620, 319,660, 324,665, 324,670, 315,670, 311,660, 237,658, 234,669, 228,669, 228,665, 232,658, 232,619));
            Patch(texture, foreground, "Alchemy furnace", new Rect(348, 148, 70, 93), 233, "Alchemy furnace",
                Outline(379,153, 384,155, 383,163, 380,173, 384,180, 389,184, 397,183, 406,193, 411,210, 408,219, 401,225, 393,228, 369,228, 359,223, 354,216, 350,213, 351,205, 356,197, 358,190, 365,184, 372,182, 375,174, 375,164, 374,160),
                Outline(358,221, 366,221, 370,235, 365,236), Outline(395,223, 400,221, 405,234, 400,236));
            Patch(texture, foreground, "Passage wall section", new Rect(441, 126, 35, 50), 174, null,
                Outline(447,126, 470,126, 476,135, 474,174, 443,175, 441,168, 442,134));
            Patch(texture, foreground, "Meditation south wall", new Rect(153, 329, 294, 66), 390, null,
                Outline(154,332, 435,331, 447,340, 443,389, 154,390, 153,382));
            Patch(texture, foreground, "Gate lintel", new Rect(510, 677, 321, 76), 789, null,
                Outline(515,694, 543,692, 552,681, 786,681, 800,690, 824,694, 831,743, 813,750, 791,748, 783,741, 560,741, 550,750, 510,750));
            Patch(texture, foreground, "South wall west", new Rect(24, 738, 520, 74), 806, null,
                Outline(24,740, 534,738, 544,751, 543,806, 24,808));
            Patch(texture, foreground, "South wall east", new Rect(812, 738, 670, 74), 806, null,
                Outline(812,739, 1473,740, 1482,751, 1480,810, 812,809));
            Patch(texture, foreground, "Moon gate arch", new Rect(919, 303, 108, 48), 382, null,
                Outline(920,333, 930,323, 946,313, 962,307, 978,304, 994,308, 1010,317, 1027,332,
                    1027,351, 1014,350, 1003,338, 990,333, 978,332, 964,335, 951,343, 939,351, 920,351));
            BuildTrainingSeal();
        }

        Sprite MakeSprite(Texture2D texture, Rect referencePixels)
        {
            float scaleX = texture.width / 1536f, scaleY = texture.height / 1024f;
            var pixels = new Rect(referencePixels.x * scaleX, (1024 - referencePixels.yMax) * scaleY,
                referencePixels.width * scaleX, referencePixels.height * scaleY);
            var sprite = Sprite.Create(texture, pixels, Vector2.one * 0.5f, texture.width / WorldSize.x,
                0, SpriteMeshType.FullRect);
            sprite.name = "Manor " + referencePixels;
            generatedSprites.Add(sprite);
            return sprite;
        }

        void Patch(Texture2D texture, Transform root, string name, Rect pixels, float footY,
            string blockerName, params Vector2[][] outlines)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(root, false);
            renderer.transform.localPosition = GroundRect(pixels).center;
            renderer.sprite = MakeSprite(texture, pixels);
            ManorMaskGeometry.Apply(renderer.sprite, pixels, outlines);
            renderer.sharedMaterial = surfaceMaterial != null ? surfaceMaterial : DemoArt.Material;
            renderer.sortingOrder = ObliqueProjection.DepthOrder(Ground(0, footY).y);
            Rect footprint;
            if (blockerName != null)
                footprint = ManorCollisionLayout.FurnitureFootprint(blockerName);
            else
            {
                footprint = GroundRect(pixels);
                footprint.yMin = footprint.yMax = Ground(0, footY).y;
            }
            occlusion.Register(renderer, footprint);
        }

        void BuildTrainingSeal()
        {
            var seal = new GameObject("Training ground - original courtyard seal").transform;
            seal.SetParent(transform, false);
            seal.localPosition = TrainingCenter;
            seal.localScale = Vector3.one * LayoutScale;
            var color = new Color(0.22f, 0.40f, 0.41f, 0.32f);
            DemoArt.Ring("Outer seal", seal, 2.12f, color, 0.025f, -9990);
            DemoArt.Ring("Inner seal", seal, 1.98f, color, 0.015f, -9990);
            DemoArt.Ring("Center seal", seal, 0.43f, color, 0.025f, -9990);
            for (int i = 0; i < 8; i++)
                DemoArt.Shape("Seal glyph", seal, "Square", DemoArt.Direction(i * 45) * 2.05f,
                    Vector2.one * 0.085f, color, -9989, i * 45);
        }

        void OnDestroy()
        {
            if (surfaceMaterial != null)
            {
                if (Application.isPlaying) Destroy(surfaceMaterial); else DestroyImmediate(surfaceMaterial);
            }
            foreach (var sprite in generatedSprites)
            {
                if (sprite == null) continue;
                if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
            }
        }
    }
}
