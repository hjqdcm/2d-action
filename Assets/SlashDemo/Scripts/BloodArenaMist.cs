using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    sealed class BloodArenaMist
    {
        const int MaskWidth = 192, MaskHeight = 128;
        readonly Vector2[] boundary;
        readonly SpriteRenderer renderer;
        readonly Material material;
        readonly Texture2D mask;
        readonly float width;
        float age;

        public BloodArenaMist(TravelMapEnvironment map)
        {
            var definition = map.Definition;
            Vector2 size = definition.WorldSize;
            var points = map.WalkableBoundary;
            int count = points.Count - (points[0] == points[points.Count - 1] ? 1 : 0);
            boundary = new Vector2[count];
            for (int i = 0; i < count; i++) boundary[i] = points[i];

            // Measure inward distance from the walls and the closed arena gate.
            // Equal-area samples let the chosen width cover roughly 40% of the courtyard.
            var distances = new float[MaskWidth * MaskHeight];
            var interior = new List<float>(distances.Length);
            for (int y = 0; y < MaskHeight; y++)
            for (int x = 0; x < MaskWidth; x++)
            {
                var uv = new Vector2((x + .5f) / MaskWidth, (y + .5f) / MaskHeight);
                float distance = DistanceSquaredInside(Vector2.Scale(uv - Vector2.one * .5f, size));
                distances[y * MaskWidth + x] = distance;
                if (distance >= 0) interior.Add(distance);
            }
            interior.Sort();
            width = Mathf.Sqrt(interior[Mathf.FloorToInt(interior.Count * .40f)]);

            var pixels = new Color32[distances.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                if (distances[i] < 0) continue;
                float edge = Mathf.SmoothStep(0, 1, (width - Mathf.Sqrt(distances[i])) / .36f);
                pixels[i] = new Color32((byte)Mathf.RoundToInt(edge * 255), 0, 0, 255);
            }
            mask = new Texture2D(MaskWidth, MaskHeight, TextureFormat.RGBA32, false, true)
            {
                name = "Blood mist boundary mask", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            mask.SetPixels32(pixels);
            mask.Apply(false, true);

            material = new Material(Resources.Load<Shader>("SlashDemo/BloodTideMist")) { hideFlags = HideFlags.DontSave };
            material.SetTexture("_BorderMask", mask);
            material.SetFloat("_UseBorderMask", 1);
            renderer = DemoArt.Shape("Arena blood mist", map.transform, "Square", Vector2.zero, size, Color.white, -66);
            renderer.transform.localPosition = new Vector3(0, 0, -.034f);
            renderer.sharedMaterial = material;
            renderer.enabled = false;
        }

        float DistanceSquaredInside(Vector2 point)
        {
            bool inside = false;
            float distance = float.PositiveInfinity;
            for (int i = 0, j = boundary.Length - 1; i < boundary.Length; j = i++)
            {
                Vector2 a = boundary[j], b = boundary[i];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < a.x + (b.x - a.x) * (point.y - a.y) / (b.y - a.y))
                    inside = !inside;
                Vector2 edge = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
                distance = Mathf.Min(distance, (point - (a + edge * t)).sqrMagnitude);
            }
            return inside ? distance : -1;
        }

        public bool Contains(Vector2 point, float margin)
        {
            if (!renderer.enabled) return false;
            float distance = DistanceSquaredInside(renderer.transform.parent.InverseTransformPoint(point));
            float limit = width + margin;
            return distance >= 0 && distance <= limit * limit;
        }

        public void Show()
        {
            age = 0;
            renderer.enabled = true;
            Advance(0);
        }

        public void Advance(float dt)
        {
            if (!renderer.enabled) return;
            age += dt;
            material.SetFloat("_Age", age);
            material.SetFloat("_Fade", Mathf.SmoothStep(0, 1, age / .85f));
        }

        public void Hide() => renderer.enabled = false;

        public void Dispose()
        {
            if (renderer != null) Object.Destroy(renderer.gameObject);
            Object.Destroy(material);
            Object.Destroy(mask);
        }
    }
}
