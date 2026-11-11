using UnityEngine;

namespace SlashDemo
{
    sealed class AscensionGroundField
    {
        public const float WallThickness = .28f;
        readonly GameObject root;
        readonly LineRenderer inner, vortexBoundary;
        readonly LineRenderer[] vortexArms;
        readonly LineRenderer[] vortexHelices;
        readonly Material vortexMaterial, vortexLight;
        readonly BoxCollider2D wall;
        readonly LineRenderer wallBase;
        readonly Mesh wallMesh;
        readonly Material wallMaterial;
        readonly ObliqueView view;
        readonly Collider2D[] overlaps = new Collider2D[1];
        float remaining, radius, fieldAge;

        public Vector2 Center => root.transform.position;

        public bool Contains(Vector2 point, float margin = 0)
        {
            if (remaining <= 0) return false;
            if (wall != null) return (wall.ClosestPoint(point) - point).sqrMagnitude <= margin * margin;
            return (Center - point).sqrMagnitude <= (radius + margin) * (radius + margin);
        }

        public AscensionGroundField(Transform map, ObliqueView view, bool barrier)
        {
            this.view = view;
            root = new GameObject(barrier ? "Talisman barrier" : "Star vortex");
            root.SetActive(false);
            root.transform.SetParent(map, false);
            if (barrier)
            {
                wall = root.AddComponent<BoxCollider2D>();
                var curtain = new GameObject("Wind curtain");
                curtain.transform.SetParent(root.transform, false);
                wallMesh = new Mesh { name = "Talisman wind curtain" };
                curtain.AddComponent<MeshFilter>().sharedMesh = wallMesh;
                var renderer = curtain.AddComponent<MeshRenderer>();
                wallMaterial = new Material(Resources.Load<Shader>("SlashDemo/TalismanWindWall"));
                renderer.sharedMaterial = wallMaterial;
                renderer.sortingOrder = -48;
                wallBase = DemoArt.Line("Wind at ground level", root.transform, new Vector3[2],
                    new Color(.94f, .81f, .56f, .82f), .065f, -49);
                return;
            }

            Color tint = new Color(.66f, .48f, 1);
            vortexMaterial = new Material(Resources.Load<Shader>("SlashDemo/AscensionStarVortex"));
            vortexLight = new Material(Resources.Load<Shader>("SlashDemo/BladeTrailAdditive"));
            var floor = DemoArt.Shape("Vortex star field", root.transform, "Disk", Vector2.zero, Vector2.one * 2,
                Color.white, -50);
            floor.sharedMaterial = vortexMaterial;
            vortexBoundary = DemoArt.Ring("Field boundary", root.transform, 1, tint, .027f, -49);
            inner = DemoArt.Ring("Vortex core", root.transform, .17f, tint, .042f, -47);
            inner.sharedMaterial = vortexLight;
            vortexArms = new LineRenderer[3];
            vortexHelices = new LineRenderer[2];
            var taper = new AnimationCurve(new Keyframe(0, .1f), new Keyframe(.48f, 1), new Keyframe(1, .25f));
            for (int i = 0; i < vortexArms.Length; i++)
            {
                var arm = DemoArt.Line("Inward star current", root.transform, new Vector3[41], tint, .069f, -48);
                arm.widthCurve = taper;
                arm.widthMultiplier = .069f;
                arm.sharedMaterial = vortexLight;
                vortexArms[i] = arm;
            }
            for (int i = 0; i < vortexHelices.Length; i++)
            {
                var helix = DemoArt.Line("Rising star current", root.transform, new Vector3[37], tint, .052f, -46);
                helix.widthCurve = taper;
                helix.widthMultiplier = .052f;
                helix.sharedMaterial = vortexLight;
                vortexHelices[i] = helix;
            }
        }

        public bool CanPlaceWall(Vector2 center, Vector2 normal, float halfLength)
            => Physics2D.OverlapBox(center, new Vector2(halfLength * 2 + .08f, WallThickness + .08f), WallAngle(normal),
                new ContactFilter2D { useTriggers = false }, overlaps) == 0;

        static float WallAngle(Vector2 normal) => Mathf.Atan2(normal.x, -normal.y) * Mathf.Rad2Deg;

        public bool TryActivateWall(Vector2 center, Vector2 normal, float halfLength, float duration)
        {
            if (!CanPlaceWall(center, normal, halfLength)) return false;
            remaining = duration;
            fieldAge = 0;
            root.transform.SetPositionAndRotation(new Vector3(center.x, center.y, -.025f), Quaternion.Euler(0, 0, WallAngle(normal)));
            wall.size = new Vector2(halfLength * 2, WallThickness);
            Vector3 left = Vector3.left * halfLength, right = Vector3.right * halfLength;
            Vector3 lift = root.transform.InverseTransformDirection(view.Up) * 1.55f;
            // Let the top billow sideways so walls parallel to the camera's vertical axis stay visible.
            lift += Vector3.up * (.7f * Mathf.Abs(normal.x));
            wallMesh.vertices = new[] { left, right, left + lift, right + lift };
            wallMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            wallMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            wallMesh.RecalculateBounds();
            wallBase.SetPosition(0, left);
            wallBase.SetPosition(1, right);
            UpdateWallVisual();
            root.SetActive(true);
            return true;
        }

        public void ActivateVortex(Vector2 center, float size, float duration)
        {
            radius = size;
            remaining = duration;
            fieldAge = 0;
            root.transform.position = new Vector3(center.x, center.y, -.025f);
            root.transform.localScale = new Vector3(size, size, 1);
            UpdateVortexVisual();
            root.SetActive(true);
        }

        public bool Advance(float dt)
        {
            if (remaining <= 0) return false;
            remaining -= dt;
            if (remaining <= 0)
            {
                Clear();
                return true;
            }
            fieldAge += dt;
            if (wall != null) UpdateWallVisual();
            else UpdateVortexVisual();
            return false;
        }

        void UpdateWallVisual()
        {
            float fade = Mathf.Lerp(.45f, 1, Mathf.Clamp01(fieldAge * 6)) * Mathf.Clamp01(remaining * 4);
            wallMaterial.SetFloat("_Age", fieldAge);
            wallMaterial.SetFloat("_Fade", fade);
            wallBase.startColor = wallBase.endColor = new Color(.94f, .81f, .56f, .82f * fade);
        }

        void UpdateVortexVisual()
        {
            float opening = Mathf.SmoothStep(.36f, 1, Mathf.Clamp01(fieldAge * 4));
            float collapse = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(remaining / .65f));
            float fade = opening * (1 - collapse);
            float pulse = .5f + .5f * Mathf.Sin(fieldAge * 9);
            vortexMaterial.SetFloat("_Age", fieldAge);
            vortexMaterial.SetFloat("_Fade", fade);
            vortexMaterial.SetFloat("_Collapse", collapse);
            vortexBoundary.startColor = vortexBoundary.endColor = new Color(.66f, .48f, 1, .55f * fade);
            inner.transform.localScale = Vector3.one * Mathf.Lerp(.78f, 1.16f, pulse) * Mathf.Lerp(1, .3f, collapse);
            inner.startColor = inner.endColor = new Color(.81f, .63f, 1, .68f * fade);
            for (int i = 0; i < vortexArms.Length; i++)
            {
                var arm = vortexArms[i];
                float heading = i * 360f / vortexArms.Length - fieldAge * 155;
                arm.startColor = new Color(.44f, .25f, 1, .17f * fade);
                arm.endColor = new Color(.67f, .94f, 1, .69f * fade);
                for (int j = 0; j < arm.positionCount; j++)
                {
                    float u = j / (float)(arm.positionCount - 1);
                    float distance = Mathf.Lerp(.96f, .10f, u) * Mathf.Lerp(opening, .3f, collapse);
                    Vector2 point = DemoArt.Direction(heading + u * 270) * distance;
                    arm.SetPosition(j, new Vector3(point.x, point.y, -.008f));
                }
            }

            Vector3 lift = root.transform.InverseTransformVector(view.Up);
            for (int i = 0; i < vortexHelices.Length; i++)
            {
                var helix = vortexHelices[i];
                helix.startColor = new Color(.37f, .64f, 1, .52f * fade);
                helix.endColor = new Color(.77f, .51f, 1, .08f * fade);
                for (int j = 0; j < helix.positionCount; j++)
                {
                    float u = j / (float)(helix.positionCount - 1);
                    float heading = i * 180 - fieldAge * 210 + u * 390;
                    Vector2 orbit = DemoArt.Direction(heading) * Mathf.Lerp(.58f, .06f, u) * (1 - collapse);
                    float height = .06f + u * 1.8f * opening * (1 - collapse);
                    helix.SetPosition(j, (Vector3)orbit + lift * height);
                }
            }
        }

        public Vector2 Pull(Vector2 position)
        {
            return wall != null || !Contains(position) ? Vector2.zero : Vector2.ClampMagnitude((Center - position) * 2.8f, 3.6f);
        }

        public void Clear()
        {
            remaining = 0;
            if (root != null) root.SetActive(false);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            if (wallMaterial != null) Object.Destroy(wallMaterial);
            if (wallMesh != null) Object.Destroy(wallMesh);
            if (vortexMaterial != null) Object.Destroy(vortexMaterial);
            if (vortexLight != null) Object.Destroy(vortexLight);
        }
    }
}
