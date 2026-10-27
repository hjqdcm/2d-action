using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    // Runs after actor presentation. No character renderer, material or sorting group is changed.
    [DefaultExecutionOrder(200)]
    public sealed class ManorOcclusion : MonoBehaviour
    {
        const int Limit = 16;
        static readonly int CountId = Shader.PropertyToID("_ManorProtectCount");
        static readonly int RectsId = Shader.PropertyToID("_ManorProtectRects");
        static readonly int RightId = Shader.PropertyToID("_ManorProtectRight");
        static readonly int UpId = Shader.PropertyToID("_ManorProtectUp");
        sealed class Occluder
        {
            public SpriteRenderer renderer;
            public Rect footprint;
            public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        }
        struct Actor
        {
            public Vector2 ground;
            public Vector4 projected;
        }
        readonly List<Occluder> occluders = new List<Occluder>();
        readonly List<Actor> actors = new List<Actor>();
        readonly Dictionary<Transform, Renderer[]> visuals = new Dictionary<Transform, Renderer[]>();
        readonly List<Transform> expired = new List<Transform>();
        readonly Vector4[] protectedRects = new Vector4[Limit];
        SlashArena arena;

        public void Initialize(SlashArena owner) { arena = owner; }
        public void Register(SpriteRenderer renderer, Rect localFootprint)
            => occluders.Add(new Occluder { renderer = renderer, footprint = localFootprint });

        public static bool IsBehind(Rect footprint, Vector2 ground)
            => ground.x >= footprint.xMin && ground.x <= footprint.xMax && ground.y > footprint.yMax + 0.005f;

        void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (arena == null || arena.WorldCamera == null) return;
            Vector3 right = arena.WorldCamera.transform.right, up = arena.WorldCamera.transform.up;
            actors.Clear();
            if (arena.Player != null) AddActor(arena.Player.transform, right, up);
            foreach (var enemy in arena.Enemies)
                if (enemy != null && enemy.gameObject.activeInHierarchy) AddActor(enemy.transform, right, up);
            expired.Clear();
            foreach (var entry in visuals)
                if (entry.Key == null || !entry.Key.gameObject.activeInHierarchy) expired.Add(entry.Key);
            foreach (var key in expired) visuals.Remove(key);
            foreach (var occluder in occluders)
            {
                int count = 0;
                Vector4 patch = Project(occluder.renderer.bounds, right, up);
                foreach (var actor in actors)
                {
                    // Protect both front and side positions explicitly, including mesh-based actors.
                    if (IsBehind(occluder.footprint, actor.ground)) continue;
                    if (!Overlaps(patch, actor.projected)) continue;
                    protectedRects[count++] = actor.projected;
                    if (count == Limit) break;
                }
                occluder.renderer.GetPropertyBlock(occluder.block);
                occluder.block.SetInt(CountId, count);
                occluder.block.SetVectorArray(RectsId, protectedRects);
                occluder.block.SetVector(RightId, right);
                occluder.block.SetVector(UpId, up);
                occluder.renderer.SetPropertyBlock(occluder.block);
            }
        }

        void AddActor(Transform actor, Vector3 right, Vector3 up)
        {
            if (!actor.gameObject.activeInHierarchy) return;
            if (!visuals.TryGetValue(actor, out var renderers))
            {
                var visual = actor.Find("Visual");
                renderers = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
                visuals.Add(actor, renderers);
            }
            Vector4 projected = new Vector4(float.PositiveInfinity, float.PositiveInfinity,
                float.NegativeInfinity, float.NegativeInfinity);
            bool visible = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Vector4 bounds = Project(renderer.bounds, right, up);
                projected.x = Mathf.Min(projected.x, bounds.x); projected.y = Mathf.Min(projected.y, bounds.y);
                projected.z = Mathf.Max(projected.z, bounds.z); projected.w = Mathf.Max(projected.w, bounds.w);
                visible = true;
            }
            if (!visible) return;
            // Include sleeves and flowing hems just outside the body mesh's bounds.
            float padding = 0.20f * arena.View.characterScale;
            projected += new Vector4(-padding, -padding, padding, padding);
            actors.Add(new Actor { ground = transform.InverseTransformPoint(actor.position), projected = projected });
        }

        static Vector4 Project(Bounds bounds, Vector3 right, Vector3 up)
        {
            Vector3 center = bounds.center, extent = bounds.extents;
            float x = Vector3.Dot(center, right), y = Vector3.Dot(center, up);
            float width = Mathf.Abs(right.x) * extent.x + Mathf.Abs(right.y) * extent.y + Mathf.Abs(right.z) * extent.z;
            float height = Mathf.Abs(up.x) * extent.x + Mathf.Abs(up.y) * extent.y + Mathf.Abs(up.z) * extent.z;
            return new Vector4(x - width, y - height, x + width, y + height);
        }

        static bool Overlaps(Vector4 a, Vector4 b) => a.x <= b.z && a.z >= b.x && a.y <= b.w && a.w >= b.y;
    }

    // Mesh silhouettes remove the opaque floor and air in a rectangular image crop.
    public static class ManorMaskGeometry
    {
        public static Vector2[] Outline(params float[] coordinates)
        {
            var points = new Vector2[coordinates.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(coordinates[i * 2], coordinates[i * 2 + 1]);
            return points;
        }

        public static void Apply(Sprite sprite, Rect source, params Vector2[][] outlines)
        {
            Vector2 center = ManorEnvironment.GroundRect(source).center;
            var vertices = new List<Vector2>();
            var triangles = new List<ushort>();
            foreach (var outline in outlines)
            {
                int offset = vertices.Count;
                foreach (var p in outline) vertices.Add(ManorEnvironment.Ground(p.x, p.y) - center);
                var remaining = new List<int>();
                float area = 0;
                for (int i = 0; i < outline.Length; i++)
                {
                    remaining.Add(i + offset);
                    area += Cross(vertices[i + offset], vertices[(i + 1) % outline.Length + offset]);
                }
                float orientation = Mathf.Sign(area);
                while (remaining.Count > 3)
                {
                    bool clipped = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                        int b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                        if (Cross(vertices[b] - vertices[a], vertices[c] - vertices[b]) * orientation <= 0.000001f) continue;
                        bool contains = false;
                        foreach (int p in remaining)
                            if (p != a && p != b && p != c && Inside(vertices[p], vertices[a], vertices[b], vertices[c], orientation))
                            { contains = true; break; }
                        if (contains) continue;
                        triangles.Add((ushort)a); triangles.Add((ushort)b); triangles.Add((ushort)c);
                        remaining.RemoveAt(i); clipped = true; break;
                    }
                    if (!clipped) throw new InvalidOperationException("Invalid manor silhouette polygon: " + sprite.name);
                }
                foreach (int index in remaining) triangles.Add((ushort)index);
            }
            // OverrideGeometry accepts sprite-rect pixels, while Sprite.vertices returns local units.
            // https://docs.unity.cn/6000.7/Documentation/ScriptReference/Sprite.OverrideGeometry.html
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i] * sprite.pixelsPerUnit + sprite.pivot;
            sprite.OverrideGeometry(vertices.ToArray(), triangles.ToArray());
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float orientation)
            => Cross(b - a, p - a) * orientation >= -0.000001f
                && Cross(c - b, p - b) * orientation >= -0.000001f
                && Cross(a - c, p - c) * orientation >= -0.000001f;
    }
}
