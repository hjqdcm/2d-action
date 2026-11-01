using UnityEngine;

namespace SlashDemo
{
    // Rigid limbs plus overlapping joint covers; claws never stretch.
    [DefaultExecutionOrder(75)]
    [DisallowMultipleComponent]
    public sealed class BearWalkMesh : MonoBehaviour
    {
        public const float CycleDistance = 1.05f;
        const float Stance = .62f, Reach = .30f, PawLift = .26f;
        static readonly Vector2[] Feet = { new Vector2(.105f,.185f), new Vector2(.400f,.220f), new Vector2(.600f,.085f), new Vector2(.905f,.160f) };
        static readonly Vector2[] Joints = { new Vector2(.17f,.355f), new Vector2(.38f,.275f), new Vector2(.54f,.395f), new Vector2(.89f,.395f) };
        static readonly float[] Phases = { 0, .75f, .50f, .25f };
        static readonly int[] DrawOrder = { 1, 3, 0, -1, 2 };
        // Covers bridge the torso and moving limb, behind the matching forearm/paw.
        static readonly int[] RenderOrder = { 0, 6, 1, 8, 2, 5, 3, 4, 7 };
        static readonly Vector2[] CoverSizes = {
            new Vector2(.25f,.21f), new Vector2(.17f,.16f),
            new Vector2(.31f,.25f), new Vector2(.23f,.22f)
        };
        static readonly Vector4[] CoverPaint = {
            new Vector4(.16f,.38f,.18f,.13f), new Vector4(.39f,.32f,.10f,.08f),
            new Vector4(.54f,.42f,.16f,.12f), new Vector4(.90f,.42f,.13f,.12f)
        };
        SpriteRenderer source;
        Rigidbody2D body;
        SlashEnemy enemy;
        SlashArena arena;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Material material;
        MaterialPropertyBlock properties;
        Vector3[] rest, vertices;
        readonly Vector2[] pivots = new Vector2[4], offsets = new Vector2[4], rotations = new Vector2[4];
        Vector2 previousPosition, motion = Vector2.down;
        float phase, blend, smoothedSpeed;
        float clawRaise, clawSwing = -1, brace;
        Vector2 skillDirection;
        public void SetSkillPose(float raise, float swing, float load, Vector2 direction)
        { clawRaise = raise; clawSwing = swing; brace = load; skillDirection = direction; }
        public float Phase => phase;
        public float Blend => blend;
        public Mesh DeformedMesh => mesh;
        public MeshRenderer WalkingRenderer => meshRenderer;

        public void Initialize(SpriteRenderer skin, Rigidbody2D physicsBody, SlashArena owner)
        {
            source = skin; body = physicsBody; arena = owner;
            previousPosition = transform.position;
            mesh = new Mesh { name = "Bear rigid torso and four limbs", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            var child = new GameObject("Rigid four paw animation");
            child.transform.SetParent(source.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = child.AddComponent<MeshRenderer>();
            material = new Material(Resources.Load<Shader>("SlashDemo/BearSkin")) { hideFlags = HideFlags.DontSave };
            material.mainTexture = source.sprite.texture;
            var materials = new Material[RenderOrder.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            meshRenderer.sharedMaterials = materials;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            properties = new MaterialPropertyBlock();
            BuildPieces(); source.forceRenderingOff = true; Refresh();
        }

        Vector2 ArtPoint(Vector2 uv)
        {
            var b = source.sprite.bounds;
            return new Vector2(b.min.x + b.size.x * uv.x, b.min.y + b.size.y * uv.y);
        }

        void BuildPieces()
        {
            rest = new Vector3[36]; vertices = new Vector3[36];
            var uv = new Vector2[36];
            var corners = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            for (int part = 0; part < 5; part++) for (int corner = 0; corner < 4; corner++)
            {
                int i = part * 4 + corner;
                uv[i] = corners[corner]; rest[i] = ArtPoint(corners[corner]);
            }
            for (int leg = 0; leg < 4; leg++) pivots[leg] = ArtPoint(Joints[leg]);
            for (int leg = 0; leg < 4; leg++) for (int corner = 0; corner < 4; corner++)
            {
                int i = (5 + leg) * 4 + corner;
                Vector2 q = corners[corner] - Vector2.one * .5f;
                rest[i] = ArtPoint(Joints[leg] + Vector2.Scale(q,CoverSizes[leg]));
                uv[i] = (Vector2)CoverPaint[leg] + Vector2.Scale(q,new Vector2(CoverPaint[leg].z,CoverPaint[leg].w));
            }
            mesh.vertices = rest; mesh.uv = uv; mesh.subMeshCount = RenderOrder.Length;
            for (int draw = 0; draw < RenderOrder.Length; draw++)
            {
                int i = RenderOrder[draw] * 4;
                mesh.SetTriangles(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }, draw, false);
            }
        }

        public static Vector2 Footfall(float cycle)
        {
            float t = Mathf.Repeat(cycle, 1);
            if (t < Stance) return new Vector2(Mathf.Lerp(Reach, -Reach, t / Stance), 0);
            float swing = (t - Stance) / (1 - Stance);
            return new Vector2(Mathf.Lerp(-Reach, Reach, Mathf.SmoothStep(0, 1, swing)), Mathf.Sin(swing * Mathf.PI) * PawLift);
        }

        public void Advance(Vector2 displacement, float dt, bool walking)
        {
            if (dt <= 0) return;
            float travel = displacement.magnitude;
            if (travel > 1.5f) travel = 0;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, walking ? travel / dt : 0, 1 - Mathf.Exp(-14 * dt));
            float desired = walking && smoothedSpeed > .025f ? Mathf.Clamp01(smoothedSpeed / .65f) : 0;
            blend = Mathf.MoveTowards(blend, desired, dt * 7);
            if (walking && travel > .00001f)
            {
                phase = Mathf.Repeat(phase + travel / (CycleDistance * Mathf.Max(.1f, arena.View.characterScale)), 1);
                motion = ObliqueProjection.ProjectGround(displacement.normalized, arena.View.elevation);
            }
        }

        Vector3 PosePoint(Vector2 point, int leg)
        {
            Vector2 posed = point;
            if (leg >= 0)
            {
                Vector2 d = point - pivots[leg];
                float c = rotations[leg].x, s = rotations[leg].y;
                posed = pivots[leg] + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c) + offsets[leg];
            }
            posed.y += (1 - Mathf.Cos(phase * Mathf.PI * 4)) * .008f * blend;
            if (source.flipX) posed.x = -posed.x;
            return posed;
        }

        public Vector3 PawPosition(int leg) => PosePoint(ArtPoint(Feet[leg]), leg);
        public Vector3 JointPosition(int leg) => (PosePoint(pivots[leg],-1) + PosePoint(pivots[leg],leg)) * .5f;

        public void Refresh()
        {
            if (mesh == null || source == null) return;
            Vector2 heading = new Vector2(source.flipX ? -motion.x : motion.x, motion.y);
            heading = Vector2.Lerp(new Vector2(.45f, -.38f), heading.normalized, .80f).normalized;
            for (int leg = 0; leg < 4; leg++)
            {
                Vector2 step = Footfall(phase + Phases[leg]);
                float angle = (step.x / Reach * 15 - step.y / PawLift * 7) * blend * Mathf.Deg2Rad;
                offsets[leg] = (heading * step.x * .20f + Vector2.up * step.y * .42f) * blend;
                if (leg >= 2)
                {
                    float degrees = clawSwing >= 0 ? Mathf.Lerp(38, -22, Mathf.SmoothStep(0, 1, clawSwing)) : clawRaise * 38;
                    float weight = leg == 2 ? 1 : .35f;
                    angle += degrees * weight * Mathf.Deg2Rad;
                    Vector2 strike = ObliqueProjection.ProjectGround(skillDirection, arena.View.elevation);
                    if (source.flipX) strike.x = -strike.x;
                    offsets[leg] += (Vector2.up * (clawRaise * .10f) + strike * (clawSwing >= 0 ? Mathf.Sin(clawSwing * Mathf.PI) * .12f : 0)) * weight;
                }
                angle += brace * (leg >= 2 ? -8 : 8) * Mathf.Deg2Rad;
                rotations[leg] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            for (int part = 0; part < 5; part++)
            {
                int leg = DrawOrder[part];
                for (int corner = 0; corner < 4; corner++)
                {
                    int i = part * 4 + corner;
                    vertices[i] = PosePoint(rest[i], leg);
                }
            }
            for (int leg = 0; leg < 4; leg++)
            {
                // A halfway rotation overlaps both rigid pieces without stretching either.
                Vector2 half = (Vector2.right + rotations[leg]).normalized;
                for (int corner = 0; corner < 4; corner++)
                {
                    int i = (5 + leg) * 4 + corner;
                    Vector2 d = (Vector2)rest[i] - pivots[leg];
                    Vector2 p = pivots[leg] + new Vector2(d.x * half.x - d.y * half.y,d.x * half.y + d.y * half.x) + offsets[leg] * .5f;
                    p.y += (1 - Mathf.Cos(phase * Mathf.PI * 4)) * .008f * blend;
                    if (source.flipX) p.x = -p.x;
                    vertices[i] = p;
                }
            }
            for (int draw = 0; draw < RenderOrder.Length; draw++)
            {
                int part = RenderOrder[draw];
                properties.Clear();
                properties.SetColor("_Color", source.color);
                properties.SetFloat("_PartIndex", part < 5 ? DrawOrder[part] : part - 1);
                properties.SetFloat("_JointOpacity",1);
                if (part >= 5) properties.SetVector("_JointRect",CoverPaint[part-5]);
                meshRenderer.SetPropertyBlock(properties,draw);
            }
            mesh.vertices = vertices; mesh.RecalculateBounds();
            meshRenderer.sortingLayerID = source.sortingLayerID;
            meshRenderer.sortingOrder = source.sortingOrder;
            meshRenderer.enabled = source.enabled;
        }

        void LateUpdate()
        {
            if (source == null || body == null) return;
            if (enemy == null) enemy = GetComponent<SlashEnemy>();
            Vector2 now = transform.position;
            if (!arena.Paused && enemy != null && enemy.Alive) Advance(now - previousPosition, Time.deltaTime, enemy.IsChasing);
            previousPosition = now; Refresh();
        }
        void OnDisable()
        {
            if (source != null) source.forceRenderingOff = false;
            if (meshRenderer != null) meshRenderer.enabled = false;
        }
        void OnEnable()
        {
            if (source != null) source.forceRenderingOff = true;
            previousPosition = transform.position;
        }
        void OnDestroy()
        {
            if (source != null) source.forceRenderingOff = false;
            Release(mesh); Release(material);
        }
        static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
