using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    // A real, continuously rotated mesh. Triangle depth ordering lets it coexist with sprite scenery
    // without writing character depth into the 2D renderer's shared depth/stencil buffer.
    public sealed class Hero360Model : IDisposable
    {
        public static Vector3 FacingForward(Vector2 facing, float elevation)
        {
            if (facing.sqrMagnitude < 0.00001f) facing = Vector2.down;
            Vector3 up = ObliqueProjection.Up(elevation);
            Vector3 forward = (Vector3)facing - up * Vector3.Dot(facing, up);
            return forward.normalized;
        }
        public static Quaternion FacingRotation(Vector2 facing, float elevation)
            => Quaternion.LookRotation(FacingForward(facing, elevation), ObliqueProjection.Up(elevation));

        public sealed class Echo : IDisposable
        {
            readonly GameObject root;
            readonly Mesh shape;
            readonly Material tint;
            float age;
            public Echo(Hero360Model source, Transform parent)
            {
                root = new GameObject("3D dash afterimage");
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(source.Root.position, source.Root.rotation);
                root.transform.localScale = source.Root.lossyScale;
                shape = UnityEngine.Object.Instantiate(source.mesh);
                tint = new Material(source.material);
                tint.SetColor("_Tint", new Color(0.35f, 0.85f, 1, 0.28f));
                tint.SetFloat("_Flash", 0); tint.SetFloat("_Opacity", 1);
                root.AddComponent<MeshFilter>().sharedMesh = shape;
                var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = tint;
                renderer.sortingOrder = ObliqueProjection.DepthOrder(source.owner.position.y) - 1;
            }
            public bool Advance(float dt)
            {
                age += Mathf.Max(0, dt);
                if (age >= 0.24f || root == null) return false;
                tint.SetFloat("_Opacity", 1 - age / 0.24f);
                return true;
            }
            public void Dispose() { Release(root); Release(shape); Release(tint); }
        }

        sealed class Shape
        {
            public Vector3[] points, normals;
            public Vector2[] uv;
            public bool[] lining;
            public Color[] tint;
            public int[] indices;
        }
        enum Surface { Plain, WhiteCloth, BlueCloth, Hair, Silver }
        struct Triangle : IComparable<Triangle>
        {
            public int a, b, c, index;
            public float depth;
            public int CompareTo(Triangle other)
            {
                int order = other.depth.CompareTo(depth);
                return order != 0 ? order : index.CompareTo(other.index);
            }
        }

        static readonly Shape sphere = Sphere();
        static readonly Shape smallSphere = Sphere(12, 20);
        static readonly Shape cylinder = Profile(new[] { -0.5f, 0.5f }, new[] { 1f, 1f }, new[] { 1f, 1f }, 20, 1);
        static readonly Shape skirt = Profile(new[] { 0.17f, 0.28f, 0.45f, 0.63f },
            new[] { 0.15f, 0.16f, 0.15f, 0.14f }, new[] { 0.11f, 0.115f, 0.11f, 0.10f });
        static readonly Shape torso = Profile(new[] { 0.61f, 0.72f, 0.89f, 0.98f },
            new[] { 0.145f, 0.14f, 0.18f, 0.15f }, new[] { 0.10f, 0.095f, 0.115f, 0.095f });
        static readonly Shape head = SculptHead();
        static readonly Shape hairCap = Profile(new[] { 1.13f, 1.18f, 1.25f, 1.30f, 1.33f },
            new[] { 0.091f, 0.109f, 0.109f, 0.085f, 0.016f },
            new[] { 0.075f, 0.098f, 0.100f, 0.078f, 0.025f });
        static readonly Shape armSleeve = Profile(new[] { 0f, 0.14f, 0.34f, 0.52f, 0.68f, 0.86f, 1f },
            new[] { 0.062f, 0.071f, 0.066f, 0.061f, 0.079f, 0.098f, 0.100f },
            new[] { 0.054f, 0.062f, 0.055f, 0.050f, 0.062f, 0.074f, 0.073f }, 36, 5);
        static readonly Shape boot = Boot();
        static readonly Shape hand = Profile(new[] { -0.039f, -0.022f, 0f, 0.023f, 0.041f },
            new[] { 0.013f, 0.026f, 0.031f, 0.027f, 0.015f },
            new[] { 0.014f, 0.023f, 0.023f, 0.018f, 0.012f }, 32, 4);
        static readonly Shape collarLeft = Swept(new[] { new Vector3(-0.074f, 0.99f, 0.064f), new Vector3(-0.043f, 0.87f, 0.120f), new Vector3(0.052f, 0.67f, 0.112f) },
            new[] { 0.022f, 0.026f, 0.020f }, new[] { 0.003f, 0.004f, 0.003f });
        static readonly Shape collarRight = Swept(new[] { new Vector3(0.075f, 0.99f, 0.067f), new Vector3(0.018f, 0.86f, 0.125f), new Vector3(-0.048f, 0.76f, 0.126f) },
            new[] { 0.016f, 0.019f, 0.016f }, new[] { 0.003f, 0.004f, 0.003f });
        static readonly Shape[] coatPanels = { CoatPanel(55, 140), CoatPanel(140, 220), CoatPanel(220, 305) };
        static readonly Shape coatUpper = CoatPanel(55, 305, true);
        static readonly Shape[] backLocks = HairLocks(false);
        static readonly Shape[] frontLocks = HairLocks(true);
        static readonly Shape blade = Profile(new[] { 0f, 0.82f, 1f },
            new[] { 0.032f, 0.023f, 0f }, new[] { 0.009f, 0.006f, 0f }, 24, 1);
        static readonly Color ivory = new Color(0.94f, 0.96f, 0.99f), hair = new Color(0.90f, 0.92f, 0.99f);
        static readonly Color blue = new Color(0.52f, 0.72f, 0.87f), jade = new Color(0.64f, 0.82f, 0.76f);
        static readonly Color silver = new Color(0.81f, 0.87f, 0.94f), skin = new Color(0.93f, 0.82f, 0.75f);
        static readonly Color trim = new Color(0.28f, 0.44f, 0.59f);
        static readonly float[] caseBands = { 0.025f, 0.12f, 0.63f, 0.98f };
        readonly Transform owner;
        readonly List<Vector3> vertices = new List<Vector3>(22000), normals = new List<Vector3>(22000);
        readonly List<Color> colors = new List<Color>(22000);
        readonly List<Vector2> detailUV = new List<Vector2>(22000), surfaceUV = new List<Vector2>(22000);
        readonly List<Triangle> triangles = new List<Triangle>(35000);
        readonly List<Triangle> visibleTriangles = new List<Triangle>(65000);
        readonly List<int> indices = new List<int>(105000);
        struct SleeveSection { public float t, radiusLimit; public Vector3 center, tangent, across, depth; }
        readonly List<SleeveSection> sleeveSections = new List<SleeveSection>(40);
        readonly Vector3[] sleeveNormalSums = new Vector3[armSleeve.points.Length];
        readonly Mesh mesh;
        readonly Material material;
        SwordMotion.Pose pose;
        ObliqueView view;
        Vector2 facing = Vector2.down, previousGround;
        float phase, walking, charge;
        bool combat;
        struct FrameState
        {
            public Vector3 right, left, grip, tip, socket, caseAxis;
            public Quaternion rotation;
            public Vector4 deformation, appearance;
            public float elevation;
            public bool caseVisible;
            public bool Matches(FrameState b) => (right - b.right).sqrMagnitude < 1e-10f && (left - b.left).sqrMagnitude < 1e-10f
                && (grip - b.grip).sqrMagnitude < 1e-10f && (tip - b.tip).sqrMagnitude < 1e-10f && (socket - b.socket).sqrMagnitude < 1e-10f
                && (caseAxis - b.caseAxis).sqrMagnitude < 1e-10f && Mathf.Abs(Quaternion.Dot(rotation, b.rotation)) > 0.9999999f
                && (deformation - b.deformation).sqrMagnitude < 1e-12f && (appearance - b.appearance).sqrMagnitude < 1e-12f
                && elevation == b.elevation && caseVisible == b.caseVisible;
        }
        FrameState lastFrame;
        bool hasFrame;
        public Transform Root { get; }
        public MeshRenderer Renderer { get; }
        public Mesh Mesh => mesh;
        public float SwordBrightness => Mathf.Lerp(1, 1.8f, charge);
        public float Bob => Mathf.Sin(phase * 2) * 0.008f * walking;
        public Vector3 RightShoulder => Root.TransformPoint(TorsoPoint(new Vector3(0.195f, 0.94f, 0.01f)));
        public Vector3 LeftShoulder => Root.TransformPoint(TorsoPoint(new Vector3(-0.195f, 0.94f, 0.01f)));
        public Vector3 RelaxedLeftHand => LeftShoulder - view.Up * (0.40f * view.characterScale)
            - Root.right * (0.035f * view.characterScale);
        public bool BackFacing => Vector3.Dot(Root.forward, ObliqueProjection.Forward(view.elevation)) > 0.2f;

        public Hero360Model(Transform player, ObliqueView projection)
        {
            owner = player; view = projection; previousGround = player.position;
            Shader shader = Resources.Load<Shader>("SlashDemo/Hero360Toon");
            if (shader == null) throw new InvalidOperationException("Hero360Toon shader is missing.");
            var go = new GameObject("White swordsman 360 mesh");
            Root = go.transform; Root.SetParent(player, false);
            mesh = new Mesh { name = "Continuous swordsman - articulated 3D geometry" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.MarkDynamic();
            material = new Material(shader) { name = "White robe prototype toon", hideFlags = HideFlags.DontSave };
            material.SetTexture("_DetailAtlas", Resources.Load<Texture2D>("SlashDemo/WhiteSwordsmanAtlas"));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = go.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            SetBodyPose(default, false, projection);
        }

        public void SetFacing(Vector2 aim, float elevation)
        {
            if (aim.sqrMagnitude > 0.00001f) facing = aim.normalized;
            Root.rotation = FacingRotation(facing, elevation);
        }
        public void AdvanceWalk(Vector2 ground, float dt, bool dashing)
        {
            float distance = Vector2.Distance(previousGround, ground); previousGround = ground;
            if (dt <= 0) return;
            float target = !dashing && distance > 0.0001f ? 1 : 0;
            walking = Mathf.MoveTowards(walking, target, dt * 12);
            if (!dashing) phase += distance * 15;
        }
        public void SetBodyPose(SwordMotion.Pose motion, bool attacking, ObliqueView projection)
        {
            view = projection; pose = motion; combat = attacking;
            Vector2 shift = attacking ? SwordMotion.ToWorld(pose.bodyOffset, facing) : Vector2.zero;
            Root.position = owner.position + (Vector3)shift + view.Up * ((attacking ? pose.sink : 0) + Bob);
            Root.localScale = Vector3.one * view.characterScale;
            Root.rotation = FacingRotation(facing, view.elevation);
            material.SetVector("_ViewDirection", -ObliqueProjection.Forward(view.elevation));
        }
        public void SetCharge(float progress) { charge = Mathf.Clamp01(progress); }
        public void SetFeedback(float flash, float opacity)
        {
            material.SetFloat("_Flash", flash > 0 ? 1 : 0);
            material.SetFloat("_Opacity", Mathf.Clamp01(opacity));
        }
        public void Fall()
        {
            Root.Rotate(ObliqueProjection.Forward(view.elevation), -75, Space.World);
            Upload();
        }
        Vector3 TorsoPoint(Vector3 p)
        {
            if (!combat) return p;
            Vector3 waist = new Vector3(0, 0.62f, 0);
            p = Quaternion.Euler(0, pose.torsoTurn * 1.5f, 0) * (p - waist) + waist;
            p.x += pose.torsoShear * (p.y - waist.y);
            return p;
        }

        public void BuildFrame(Vector3 rightHand, Vector3 leftHand, Vector3 swordGrip, Vector3 swordTip,
            Vector3 caseSocket, Vector3 caseAxis, bool caseVisible, float relaxedLeft)
        {
            float stride = Mathf.Sin(phase) * walking;
            float lag = Mathf.Sin(phase - 0.7f) * walking * 0.025f + (combat ? pose.torsoShear * 0.06f : 0);
            var frame = new FrameState {
                right = Root.InverseTransformPoint(rightHand), left = Root.InverseTransformPoint(leftHand),
                grip = Root.InverseTransformPoint(swordGrip), tip = Root.InverseTransformPoint(swordTip),
                socket = Root.InverseTransformPoint(caseSocket), caseAxis = Root.InverseTransformDirection(caseAxis).normalized,
                rotation = Root.rotation, deformation = new Vector4(stride, lag, combat ? pose.torsoTurn : 0, combat ? pose.torsoShear : 0),
                appearance = new Vector4(charge, relaxedLeft, view.characterScale, combat ? 1 : 0), elevation = view.elevation, caseVisible = caseVisible
            };
            if (hasFrame && frame.Matches(lastFrame)) return;
            vertices.Clear(); normals.Clear(); colors.Clear(); detailUV.Clear(); surfaceUV.Clear(); triangles.Clear(); indices.Clear();
            for (int side = -1; side <= 1; side += 2)
            {
                float step = side * stride;
                Vector3 ankle = new Vector3(side * 0.095f, 0.075f + Mathf.Max(0, step) * 0.035f, step * 0.07f + 0.025f);
                Segment(new Vector3(side * 0.085f, 0.47f, 0), ankle, 0.050f, ivory, Surface.WhiteCloth);
                Append(boot, ankle, Quaternion.identity, Vector3.one, ivory, false, Surface.WhiteCloth);
                Stroke(new[] { ankle + new Vector3(-0.035f, 0.10f, 0.045f), ankle + new Vector3(0, 0.065f, 0.065f),
                    ankle + new Vector3(0.035f, 0.035f, 0.07f) }, 0.003f, silver);
            }
            Append(skirt, Vector3.zero, Quaternion.Euler(0, stride * 2, 0), Vector3.one, blue, false, Surface.BlueCloth);
            Append(torso, Vector3.zero, Quaternion.identity, Vector3.one, blue, true, Surface.BlueCloth);
            Append(coatUpper, Vector3.zero, Quaternion.identity, Vector3.one, ivory, true, Surface.WhiteCloth);
            for (int i = 0; i < coatPanels.Length; i++)
                Append(coatPanels[i], new Vector3(0, 0, -0.012f * stride), Quaternion.Euler(0, stride * (i - 1) * 2.5f, 0),
                    Vector3.one, ivory, false, Surface.WhiteCloth);
            // Overlapping collars and an engraved silver sash replace the old round chest patches.
            Append(collarLeft, Vector3.zero, Quaternion.identity, Vector3.one, ivory, true, Surface.WhiteCloth);
            Append(collarRight, Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.75f, 0.84f, 0.93f), true, Surface.BlueCloth);
            Append(cylinder, TorsoPoint(new Vector3(0, 0.64f, 0)), Quaternion.identity, new Vector3(0.161f, 0.055f, 0.115f), silver, false, Surface.Silver);
            for (int side = -1; side <= 1; side += 2)
            {
                Stroke(new[] { TorsoPoint(new Vector3(side * 0.145f, 0.665f, 0.05f)), TorsoPoint(new Vector3(side * 0.09f, 0.671f, 0.101f)),
                    TorsoPoint(new Vector3(0, 0.668f, 0.121f)) }, 0.003f, ivory);
            }
            Pendant(stride);

            Vector3 headShift = TorsoPoint(new Vector3(0, 1.04f, 0)) - new Vector3(0, 1.04f, 0);
            Face(headShift);
            Append(hairCap, headShift + new Vector3(0, 0, -0.035f), Quaternion.identity, Vector3.one, hair, false, Surface.Hair);
            foreach (var strand in frontLocks) Append(strand, headShift, Quaternion.identity, Vector3.one, hair, false, Surface.Hair);
            Ellipsoid(new Vector3(0, 1.351f, -0.04f) + headShift, new Vector3(0.046f, 0.052f, 0.045f), hair, Surface.Hair);
            Ellipsoid(new Vector3(0, 1.323f, -0.082f) + headShift, new Vector3(0.047f, 0.014f, 0.034f), silver, Surface.Silver);
            for (int i = 0; i < backLocks.Length; i++)
                Append(backLocks[i], headShift + Vector3.right * (lag * 0.35f), Quaternion.Euler(0, lag * 50, 0), Vector3.one,
                    Color.Lerp(hair, ivory, (i % 3) * 0.15f), false, Surface.Hair);

            Arm(Root.InverseTransformPoint(RightShoulder), frame.right, 1, 0);
            Arm(Root.InverseTransformPoint(LeftShoulder), frame.left, -1, relaxedLeft);
            Sword(frame.grip, frame.tip);
            if (caseVisible) Scabbard(frame.socket, frame.caseAxis);
            Upload();
            lastFrame = frame; hasFrame = true;
        }

        void Face(Vector3 shift)
        {
            Append(head, shift + new Vector3(0, 0, 0.020f), Quaternion.identity, Vector3.one, skin);
            for (int side = -1; side <= 1; side += 2)
            {
                Small(new Vector3(side * 0.091f, 1.162f, 0.008f) + shift, new Vector3(0.017f, 0.034f, 0.017f), skin);
                Small(new Vector3(side * 0.036f, 1.184f, 0.091f) + shift, new Vector3(0.024f, 0.008f, 0.005f), ivory);
                Small(new Vector3(side * 0.035f, 1.184f, 0.096f) + shift, new Vector3(0.007f, 0.007f, 0.003f), new Color(0.24f, 0.41f, 0.54f));
                Small(new Vector3(side * 0.035f, 1.185f, 0.099f) + shift, new Vector3(0.003f, 0.006f, 0.002f), new Color(0.12f, 0.18f, 0.26f));
                Stroke(new[] { new Vector3(side * 0.015f, 1.190f, 0.096f) + shift, new Vector3(side * 0.035f, 1.193f, 0.098f) + shift,
                    new Vector3(side * 0.059f, 1.190f, 0.090f) + shift }, 0.0018f, new Color(0.23f, 0.25f, 0.31f));
                Stroke(new[] { new Vector3(side * 0.018f, 1.210f, 0.094f) + shift, new Vector3(side * 0.041f, 1.216f, 0.093f) + shift,
                    new Vector3(side * 0.062f, 1.212f, 0.084f) + shift }, 0.003f, new Color(0.35f, 0.36f, 0.43f));
            }
        }
        void Pendant(float stride)
        {
            Vector3 at = TorsoPoint(new Vector3(0.095f, 0.61f, 0.120f));
            Vector3 end = new Vector3(0.10f + stride * 0.008f, 0.43f, 0.153f);
            Segment(at, end, 0.0035f, silver);
            Small(Vector3.Lerp(at, end, 0.35f), new Vector3(0.015f, 0.022f, 0.010f), silver);
            Ellipsoid(end, new Vector3(0.025f, 0.033f, 0.011f), jade);
            Small(end + new Vector3(0, 0.002f, 0.012f), new Vector3(0.008f, 0.012f, 0.003f), ivory);
            for (int i = 0; i < 5; i++)
                Segment(end + new Vector3((i - 2) * 0.005f, -0.04f, 0),
                    end + new Vector3((i - 2) * 0.007f + stride * 0.008f, -0.20f, 0.004f), 0.0025f, blue);
            Small(end + new Vector3(0, -0.04f, 0), new Vector3(0.014f, 0.019f, 0.012f), silver);
        }

        void Arm(Vector3 shoulder, Vector3 handPoint, int side, float relaxed)
        {
            Vector3 axis = handPoint - shoulder;
            Vector3 bend = new Vector3(side * 0.7f, -1, -0.15f);
            bend -= axis.normalized * Vector3.Dot(bend, axis.normalized);
            float amount = Mathf.Lerp(0.10f, 0.025f, relaxed);
            Vector3 elbow = Vector3.Lerp(shoulder, handPoint, 0.52f) + bend.normalized * amount;
            Vector3 cuff = Vector3.Lerp(elbow, handPoint, 0.88f);
            Vector3 root = shoulder - new Vector3(side * 0.045f, 0.005f, 0);
            AppendSleeve(new[] { root, Vector3.Lerp(shoulder, elbow, 0.45f), elbow, Vector3.Lerp(elbow, cuff, 0.65f), cuff });
            Append(hand, handPoint, Quaternion.FromToRotation(Vector3.up, (handPoint - elbow).normalized), Vector3.one, skin);
        }
        void Sword(Vector3 grip, Vector3 tip)
        {
            Vector3 axis = (tip - grip).normalized;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis);
            Append(blade, grip, rotation, new Vector3(1, Mathf.Max(0.001f, Vector3.Distance(grip, tip)), 1), new Color(0.68f, 0.86f, 0.96f) * SwordBrightness);
            Segment(grip - axis * 0.23f, grip, 0.022f, trim);
            for (int ring = 0; ring < 7; ring++)
                Segment(grip - axis * (0.02f + ring * 0.03f), grip - axis * (0.031f + ring * 0.03f), 0.024f, ivory);
            Append(sphere, grip, rotation, new Vector3(0.10f, 0.019f, 0.032f), silver);
            Vector3 across = rotation * Vector3.right;
            for (int side = -1; side <= 1; side += 2)
                Stroke(new[] { grip + across * (side * 0.11f) - axis * 0.017f, grip + across * (side * 0.063f) + axis * 0.022f,
                    grip + across * (side * 0.028f) + axis * 0.011f }, 0.011f, silver);
            Ellipsoid(grip - axis * 0.24f, Vector3.one * 0.027f, silver);
        }
        void Scabbard(Vector3 socket, Vector3 axis)
        {
            // EquipPose measures extraction in world units, independent of the character size slider.
            float length = SwordMotion.CarryLength / view.characterScale;
            Segment(socket + axis * 0.015f, socket + axis * length, 0.053f, new Color(0.36f, 0.62f, 0.73f));
            foreach (float t in caseBands)
                Segment(socket + axis * (length * t - 0.012f), socket + axis * (length * t + 0.012f), 0.057f, silver);
            Quaternion orientation = Quaternion.FromToRotation(Vector3.up, axis);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 previous = socket + orientation * new Vector3(0, length * 0.16f, side * 0.054f);
                for (int i = 1; i <= 9; i++)
                {
                    float t = 0.16f + i * 0.075f;
                    Vector3 point = socket + orientation * new Vector3(Mathf.Sin(i * 0.9f) * 0.022f, length * t, side * 0.054f);
                    Segment(previous, point, 0.0028f, silver); previous = point;
                }
            }
            Ellipsoid(socket + axis * (length + 0.03f), new Vector3(0.033f, 0.045f, 0.025f), silver);
        }
        void Ellipsoid(Vector3 center, Vector3 radius, Color color, Surface surface = Surface.Plain)
            => Append(sphere, center, Quaternion.identity, radius, color, false, surface);
        void Small(Vector3 center, Vector3 radius, Color color)
            => Append(smallSphere, center, Quaternion.identity, radius, color);
        void Stroke(Vector3[] points, float radius, Color color, Surface surface = Surface.Plain)
        { for (int i = 1; i < points.Length; i++) Segment(points[i - 1], points[i], radius, color, surface); }
        void AppendSleeve(Vector3[] path)
        {
            int offset = vertices.Count;
            float length = 0;
            for (int i = 1; i < path.Length; i++) length += Vector3.Distance(path[i - 1], path[i]);
            float lastT = -1, radiusLimit = float.PositiveInfinity;
            Vector3 center = default, tangent = default, across = default, depth = default;
            sleeveSections.Clear();
            for (int i = 0; i < armSleeve.points.Length; i++)
            {
                Vector3 v = armSleeve.points[i]; float t = Mathf.Clamp01(v.y);
                // One frame calculation per axial ring, shared by all its circumferential vertices.
                if (t != lastT)
                {
                    SleeveSection section;
                    bool reuseStart = t == 0 && sleeveSections.Count > 0;
                    bool reuseEnd = t == 1 && sleeveSections.Count > 0 && sleeveSections[sleeveSections.Count - 1].t == 1;
                    if (reuseStart || reuseEnd) section = sleeveSections[reuseStart ? 0 : sleeveSections.Count - 1];
                    else
                    {
                        center = Curve(path, t);
                        Vector3 before = center - Curve(path, Mathf.Max(0, t - 0.005f));
                        Vector3 after = Curve(path, Mathf.Min(1, t + 0.005f)) - center;
                        tangent = (before + after).normalized;
                        if (sleeveSections.Count == 0) across = Vector3.Cross(tangent, Mathf.Abs(tangent.z) < 0.94f ? Vector3.forward : Vector3.up).normalized;
                        else
                        {
                            var previous = sleeveSections[sleeveSections.Count - 1];
                            across = previous.across - tangent * Vector3.Dot(previous.across, tangent);
                            if (across.sqrMagnitude < 0.000001f) across = previous.depth - tangent * Vector3.Dot(previous.depth, tangent);
                            across.Normalize();
                        }
                        depth = Vector3.Cross(across, tangent).normalized;
                        float lo = Mathf.Max(0, t - 0.005f), hi = Mathf.Min(1, t + 0.005f);
                        Vector3 tangentBefore = (Curve(path, Mathf.Min(1, lo + 0.001f)) - Curve(path, Mathf.Max(0, lo - 0.001f))).normalized;
                        Vector3 tangentAfter = (Curve(path, Mathf.Min(1, hi + 0.001f)) - Curve(path, Mathf.Max(0, hi - 0.001f))).normalized;
                        float curvature = (tangentAfter - tangentBefore).magnitude / Mathf.Max(0.0001f, Vector3.Distance(Curve(path, lo), Curve(path, hi)));
                        radiusLimit = curvature > 0.001f ? 0.60f / curvature : float.PositiveInfinity;
                        section = new SleeveSection { t = t, center = center, tangent = tangent, across = across, depth = depth, radiusLimit = radiusLimit };
                        sleeveSections.Add(section);
                    }
                    center = section.center; tangent = section.tangent; across = section.across; depth = section.depth; radiusLimit = section.radiusLimit;
                    lastT = t;
                }
                float radial = new Vector2(v.x, v.z).magnitude;
                float radialScale = Mathf.Min(1, radiusLimit / Mathf.Max(0.0001f, radial));
                Vector3 n = armSleeve.normals[i];
                vertices.Add(center + (across * v.x + depth * v.z) * radialScale);
                normals.Add((across * n.x + tangent * (n.y / Mathf.Max(0.05f, length)) + depth * n.z).normalized);
                Surface surface = t > 0.92f ? Surface.BlueCloth : Surface.WhiteCloth;
                colors.Add(surface == Surface.BlueCloth ? blue : ivory);
                detailUV.Add(AtlasUV(armSleeve.uv[i], surface)); surfaceUV.Add(new Vector2(0.65f, (int)surface));
            }
            // Bending and local radius protection change the taper; shade the resulting surface itself.
            Array.Clear(sleeveNormalSums, 0, sleeveNormalSums.Length);
            for (int i = 0; i < armSleeve.indices.Length; i += 3)
            {
                int a = armSleeve.indices[i], b = armSleeve.indices[i + 1], c = armSleeve.indices[i + 2];
                Vector3 normal = Vector3.Cross(vertices[offset + b] - vertices[offset + a], vertices[offset + c] - vertices[offset + a]);
                sleeveNormalSums[a] += normal; sleeveNormalSums[b] += normal; sleeveNormalSums[c] += normal;
            }
            for (int i = 0; i < sleeveNormalSums.Length; i++)
                if (sleeveNormalSums[i].sqrMagnitude > 1e-20f)
                    normals[offset + i] = sleeveNormalSums[i] / Mathf.Sqrt(sleeveNormalSums[i].sqrMagnitude);
            AddIndices(armSleeve, offset);
        }
        void Segment(Vector3 a, Vector3 b, float radius, Color color, Surface surface = Surface.Plain)
        {
            Vector3 axis = b - a;
            Append(cylinder, (a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, axis.normalized),
                new Vector3(radius, Mathf.Max(0.0001f, axis.magnitude), radius), color, false, surface);
        }
        static Vector2 AtlasUV(Vector2 uv, Surface surface)
        {
            // Reuse small opaque fabric patches from the existing painting, not full character views.
            Rect patch = surface == Surface.WhiteCloth ? new Rect(0.347f, 0.45f, 0.060f, 0.13f)
                : surface == Surface.BlueCloth ? new Rect(0.267f, 0.49f, 0.050f, 0.15f)
                : surface == Surface.Hair ? new Rect(0.708f, 0.78f, 0.080f, 0.08f)
                : new Rect(0.26f, 0.755f, 0.047f, 0.02f);
            return new Vector2(patch.x + Mathf.Clamp01(uv.x) * patch.width, patch.y + Mathf.Clamp01(uv.y) * patch.height);
        }
        void Append(Shape shape, Vector3 position, Quaternion rotation, Vector3 scale, Color color, bool deform = false, Surface surface = Surface.Plain)
        {
            int offset = vertices.Count;
            color.a = 1;
            for (int i = 0; i < shape.points.Length; i++)
            {
                Vector3 p = position + rotation * Vector3.Scale(shape.points[i], scale);
                Vector3 n = rotation * new Vector3(shape.normals[i].x / scale.x, shape.normals[i].y / scale.y, shape.normals[i].z / scale.z).normalized;
                if (deform)
                {
                    p = TorsoPoint(p);
                    n = Quaternion.Euler(0, combat ? pose.torsoTurn * 1.5f : 0, 0) * n;
                    if (combat) { n.y -= pose.torsoShear * n.x; n.Normalize(); }
                }
                Surface sampled = shape.lining != null && shape.lining[i] && surface == Surface.WhiteCloth ? Surface.BlueCloth : surface;
                Color vertexColor = sampled != surface ? blue * 0.90f : color;
                if (shape.tint != null) vertexColor *= shape.tint[i];
                vertexColor.a = 1;
                vertices.Add(p); normals.Add(n); colors.Add(vertexColor);
                detailUV.Add(AtlasUV(shape.uv != null ? shape.uv[i] : Vector2.one * 0.5f, sampled));
                surfaceUV.Add(new Vector2(sampled == Surface.Plain ? 0 : sampled == Surface.WhiteCloth ? 0.75f : 0.50f, (int)sampled));
            }
            AddIndices(shape, offset);
        }
        void AddIndices(Shape shape, int offset)
        {
            for (int i = 0; i < shape.indices.Length; i += 3)
                triangles.Add(new Triangle { a = offset + shape.indices[i], b = offset + shape.indices[i + 1], c = offset + shape.indices[i + 2], index = triangles.Count });
        }
        void Upload()
        {
            Vector3 direction = Root.InverseTransformDirection(ObliqueProjection.Forward(view.elevation));
            visibleTriangles.Clear();
            for (int i = 0; i < triangles.Count; i++)
            {
                Triangle t = triangles[i];
                if (Vector3.Dot(Vector3.Cross(vertices[t.b] - vertices[t.a], vertices[t.c] - vertices[t.a]), direction) >= 0) continue;
                t.depth = Vector3.Dot((vertices[t.a] + vertices[t.b] + vertices[t.c]) / 3, direction); visibleTriangles.Add(t);
            }
            visibleTriangles.Sort(); indices.Clear();
            foreach (var t in visibleTriangles) { indices.Add(t.a); indices.Add(t.b); indices.Add(t.c); }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
            mesh.SetUVs(0, detailUV); mesh.SetUVs(1, surfaceUV); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
        }

        static float FaceDepth(Vector3 p)
        {
            float Mask(float x, float y, float width, float height) => Mathf.Exp(-((p.x - x) * (p.x - x) / (width * width)
                + (p.y - y) * (p.y - y) / (height * height)));
            float front = Mathf.SmoothStep(0, 1, Mathf.Clamp01(p.z / 0.06f));
            float bridge = 0.008f * Mask(0, 1.19f, 0.014f, 0.035f), nose = 0.019f * Mask(0, 1.164f, 0.012f, 0.027f);
            float cheeks = 0.004f * (Mask(-0.047f, 1.15f, 0.025f, 0.04f) + Mask(0.047f, 1.15f, 0.025f, 0.04f));
            float sockets = -0.003f * (Mask(-0.035f, 1.184f, 0.018f, 0.014f) + Mask(0.035f, 1.184f, 0.018f, 0.014f));
            float brow = 0.0025f * (Mask(-0.035f, 1.212f, 0.032f, 0.012f) + Mask(0.035f, 1.212f, 0.032f, 0.012f));
            float lips = 0.0025f * Mask(0, 1.112f, 0.024f, 0.006f);
            return (bridge + nose + cheeks + sockets + brow + lips) * front;
        }
        static Shape SculptHead()
        {
            Shape shape = Profile(new[] { 0.985f, 1.018f, 1.035f, 1.06f, 1.10f, 1.112f, 1.16f, 1.22f, 1.27f, 1.295f },
                new[] { 0.034f, 0.033f, 0.026f, 0.053f, 0.076f, 0.080f, 0.088f, 0.090f, 0.077f, 0.045f },
                new[] { 0.034f, 0.036f, 0.030f, 0.048f, 0.064f, 0.067f, 0.074f, 0.075f, 0.061f, 0.039f }, 64, 6);
            shape.tint = new Color[shape.points.Length];
            const float h = 0.0002f;
            for (int i = 0; i < shape.points.Length; i++)
            {
                Vector3 p = shape.points[i], n = shape.normals[i];
                float dx = (FaceDepth(p + Vector3.right * h) - FaceDepth(p - Vector3.right * h)) / (2 * h);
                float dy = (FaceDepth(p + Vector3.up * h) - FaceDepth(p - Vector3.up * h)) / (2 * h);
                float dz = (FaceDepth(p + Vector3.forward * h) - FaceDepth(p - Vector3.forward * h)) / (2 * h);
                float z = n.z / (1 + dz);
                shape.normals[i] = new Vector3(n.x - dx * z, n.y - dy * z, z).normalized;
                shape.points[i] = p + Vector3.forward * FaceDepth(p);
                float lip = Mathf.Exp(-(p.x * p.x / 0.0005f + (p.y - 1.112f) * (p.y - 1.112f) / 0.000012f))
                    * Mathf.SmoothStep(0, 1, p.z / 0.06f);
                shape.tint[i] = Color.Lerp(Color.white, new Color(0.74f, 0.53f, 0.54f), lip * 0.75f);
            }
            return shape;
        }
        static Shape Boot()
        {
            float[] y = { -0.052f, -0.044f, -0.02f, 0.015f, 0.06f, 0.13f, 0.20f };
            float[] forward = { 0.034f, 0.032f, 0.028f, 0.024f, 0.008f, 0f, 0f };
            Shape shape = Profile(y, new[] { 0.041f, 0.059f, 0.060f, 0.057f, 0.050f, 0.050f, 0.048f },
                new[] { 0.075f, 0.106f, 0.110f, 0.083f, 0.052f, 0.048f, 0.047f }, 40, 4);
            shape.tint = new Color[shape.points.Length];
            for (int i = 0; i < shape.points.Length; i++)
            {
                Vector3 p = shape.points[i], n = shape.normals[i];
                float slope = (SmoothValue(y, forward, p.y + 0.0001f) - SmoothValue(y, forward, p.y - 0.0001f)) / 0.0002f;
                p.z += SmoothValue(y, forward, p.y); shape.points[i] = p;
                shape.normals[i] = new Vector3(n.x, n.y - slope * n.z, n.z).normalized;
                float sole = 1 - Mathf.SmoothStep(0, 1, (p.y + 0.045f) / 0.012f);
                shape.tint[i] = Color.Lerp(Color.white, new Color(0.60f, 0.66f, 0.74f), sole);
            }
            return shape;
        }
        static Shape Sphere(int rings = 24, int slices = 40)
        {
            var p = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int ring = 0; ring <= rings; ring++)
            {
                float a = ring * Mathf.PI / rings;
                for (int i = 0; i < slices; i++)
                {
                    float angle = i * Mathf.PI * 2 / slices;
                    Vector3 v = new Vector3(Mathf.Sin(a) * Mathf.Cos(angle), -Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(angle));
                    p.Add(v); n.Add(v); uv.Add(new Vector2(i / (float)slices, ring / (float)rings));
                }
            }
            Connect(rings + 1, slices, t);
            return new Shape { points = p.ToArray(), normals = n.ToArray(), indices = t.ToArray(), uv = uv.ToArray() };
        }
        static float SmoothValue(float[] times, float[] values, float time)
        {
            int last = times.Length - 1;
            if (time <= times[0]) return values[0];
            if (time >= times[last]) return values[last];
            int segment = 0;
            while (segment < last - 1 && time > times[segment + 1]) segment++;
            float Slope(int i)
            {
                if (i == 0) return (values[1] - values[0]) / (times[1] - times[0]);
                if (i == last) return (values[last] - values[last - 1]) / (times[last] - times[last - 1]);
                float a = (values[i] - values[i - 1]) / (times[i] - times[i - 1]);
                float b = (values[i + 1] - values[i]) / (times[i + 1] - times[i]);
                return a * b <= 0 ? 0 : 2 * a * b / (a + b);
            }
            float span = times[segment + 1] - times[segment], u = (time - times[segment]) / span, u2 = u * u, u3 = u2 * u;
            float value = (2 * u3 - 3 * u2 + 1) * values[segment] + (u3 - 2 * u2 + u) * span * Slope(segment)
                + (-2 * u3 + 3 * u2) * values[segment + 1] + (u3 - u2) * span * Slope(segment + 1);
            return Mathf.Clamp(value, Mathf.Min(values[segment], values[segment + 1]), Mathf.Max(values[segment], values[segment + 1]));
        }
        static Vector3 Curve(Vector3[] points, float progress)
        {
            float time = Mathf.Clamp01(progress) * (points.Length - 1);
            int i = Mathf.Min(points.Length - 2, Mathf.FloorToInt(time)); float t = time - i;
            Vector3 a = points[Mathf.Max(0, i - 1)], b = points[i], c = points[i + 1], d = points[Mathf.Min(points.Length - 1, i + 2)];
            return 0.5f * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t);
        }
        static Shape Profile(float[] controls, float[] controlX, float[] controlZ, int slices = 40, int subdivisions = 4)
        {
            int rows = (controls.Length - 1) * subdivisions + 1;
            var heights = new float[rows]; var rx = new float[rows]; var rz = new float[rows];
            for (int i = 0; i < rows; i++)
            {
                int segment = Mathf.Min(controls.Length - 2, i / subdivisions);
                heights[i] = Mathf.Lerp(controls[segment], controls[segment + 1], (i - segment * subdivisions) / (float)subdivisions);
                rx[i] = SmoothValue(controls, controlX, heights[i]); rz[i] = SmoothValue(controls, controlZ, heights[i]);
            }
            var p = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int ring = 0; ring < heights.Length; ring++)
            {
                int lo = Mathf.Max(0, ring - 1), hi = Mathf.Min(heights.Length - 1, ring + 1);
                float dx = (rx[hi] - rx[lo]) / Mathf.Max(0.000001f, heights[hi] - heights[lo]);
                float dz = (rz[hi] - rz[lo]) / Mathf.Max(0.000001f, heights[hi] - heights[lo]);
                for (int i = 0; i < slices; i++)
                {
                    float a = i * Mathf.PI * 2 / slices;
                    p.Add(new Vector3(rx[ring] * Mathf.Cos(a), heights[ring], rz[ring] * Mathf.Sin(a)));
                    Vector3 normal = new Vector3(rz[ring] * Mathf.Cos(a), -(dx * rz[ring] * Mathf.Cos(a) * Mathf.Cos(a)
                        + dz * rx[ring] * Mathf.Sin(a) * Mathf.Sin(a)), rx[ring] * Mathf.Sin(a));
                    n.Add(normal.sqrMagnitude > 0.000000001f ? normal.normalized : new Vector3(Mathf.Cos(a), -(dx + dz) * 0.5f, Mathf.Sin(a)).normalized);
                    uv.Add(new Vector2(i / (float)slices, Mathf.InverseLerp(heights[0], heights[heights.Length - 1], heights[ring])));
                }
            }
            Connect(heights.Length, slices, t);
            for (int cap = 0; cap < 2; cap++)
            {
                int ring = cap == 0 ? 0 : heights.Length - 1, center = p.Count;
                p.Add(new Vector3(0, heights[ring], 0)); n.Add(cap == 0 ? Vector3.down : Vector3.up); uv.Add(Vector2.one * 0.5f);
                int rim = p.Count;
                for (int i = 0; i < slices; i++) { p.Add(p[ring * slices + i]); n.Add(cap == 0 ? Vector3.down : Vector3.up); uv.Add(new Vector2(i / (float)slices, cap)); }
                for (int i = 0; i < slices; i++)
                { t.Add(center); t.Add(rim + (cap == 0 ? i : (i + 1) % slices)); t.Add(rim + (cap == 0 ? (i + 1) % slices : i)); }
            }
            return new Shape { points = p.ToArray(), normals = n.ToArray(), indices = t.ToArray(), uv = uv.ToArray() };
        }
        static Vector3 CoatPoint(float angle, float t, bool upper)
        {
            t = Mathf.Clamp01(t);
            float a = angle * Mathf.Deg2Rad;
            float xRadius = upper ? (t < 0.35f ? Mathf.Lerp(0.162f, 0.145f, t / 0.35f)
                : t < 0.75f ? Mathf.Lerp(0.145f, 0.19f, (t - 0.35f) / 0.4f) : Mathf.Lerp(0.19f, 0.158f, (t - 0.75f) / 0.25f))
                : Mathf.Lerp(0.325f, 0.159f, t);
            float zRadius = upper ? Mathf.Lerp(0.112f, 0.105f, t) + Mathf.Sin(t * Mathf.PI) * 0.012f : Mathf.Lerp(0.205f, 0.112f, t);
            float pleat = (upper ? 0.0025f : 0.008f * (1 - t)) * Mathf.Cos(a * 9 + t * 1.4f);
            float y = upper ? Mathf.Lerp(0.62f, 0.98f, t) : Mathf.Lerp(0.14f, 0.64f, t)
                + Mathf.Pow(1 - t, 5) * (0.018f * Mathf.Cos(a * 3) + 0.01f * Mathf.Sin(a * 5));
            return new Vector3(-(xRadius + pleat) * Mathf.Sin(a), y, (zRadius + pleat) * Mathf.Cos(a));
        }
        static Shape CoatPanel(float start, float end, bool upper = false)
        {
            const int rings = 17;
            int slices = Mathf.CeilToInt((end - start) / 4), columns = slices + 1;
            var p = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var lining = new List<bool>(); var tri = new List<int>();
            for (int inside = 0; inside < 2; inside++) for (int ring = 0; ring < rings; ring++) for (int i = 0; i <= slices; i++)
            {
                float t = ring / (rings - 1f), angle = Mathf.Lerp(start, end, i / (float)slices);
                Vector3 vertical = CoatPoint(angle, t + 0.001f, upper) - CoatPoint(angle, t - 0.001f, upper);
                Vector3 angular = CoatPoint(angle + 0.01f, t, upper) - CoatPoint(angle - 0.01f, t, upper);
                Vector3 normal = Vector3.Cross(vertical.normalized, angular.normalized).normalized;
                p.Add(CoatPoint(angle, t, upper) - normal * (inside * 0.007f));
                n.Add(inside == 0 ? normal : -normal); uv.Add(new Vector2(i / (float)slices, t)); lining.Add(inside != 0);
            }
            int layer = rings * columns;
            for (int inside = 0; inside < 2; inside++) for (int r = 0; r < rings - 1; r++) for (int i = 0; i < slices; i++)
            {
                int a = inside * layer + r * columns + i, b = a + columns, c = a + 1, d = b + 1;
                if (inside == 0) { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(c); tri.Add(b); tri.Add(d); }
                else { tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(c); tri.Add(d); tri.Add(b); }
            }
            void Cap(int a, int b, Vector3 expected)
            {
                Vector3 pa = p[a], pb = p[b], pc = p[a + layer], pd = p[b + layer];
                Vector3 normal = Vector3.Cross(pb - pa, pc - pa).normalized;
                bool reverse = Vector3.Dot(normal, expected) < 0;
                if (reverse) normal = -normal;
                int offset = p.Count;
                foreach (Vector3 v in new[] { pa, pb, pc, pd }) { p.Add(v); n.Add(normal); uv.Add(Vector2.one * 0.5f); lining.Add(true); }
                int[] order = reverse ? new[] { 0, 2, 1, 2, 3, 1 } : new[] { 0, 1, 2, 2, 1, 3 };
                foreach (int index in order) tri.Add(offset + index);
            }
            for (int i = 0; i < slices; i++)
            { Cap(i, i + 1, Vector3.down); Cap((rings - 1) * columns + i, (rings - 1) * columns + i + 1, Vector3.up); }
            for (int r = 0; r < rings - 1; r++)
            {
                Vector3 a = CoatPoint(start + 0.01f, 0.5f, upper) - CoatPoint(start - 0.01f, 0.5f, upper);
                Vector3 b = CoatPoint(end + 0.01f, 0.5f, upper) - CoatPoint(end - 0.01f, 0.5f, upper);
                Cap(r * columns, (r + 1) * columns, -a);
                Cap(r * columns + slices, (r + 1) * columns + slices, b);
            }
            return new Shape { points = p.ToArray(), normals = n.ToArray(), uv = uv.ToArray(), lining = lining.ToArray(), indices = tri.ToArray() };
        }
        static Shape[] HairLocks(bool front)
        {
            var result = new List<Shape>();
            if (front)
            {
                for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 4; i++)
                    result.Add(Swept(new[] {
                        new Vector3(side * (0.006f + i * 0.016f), 1.312f - i * 0.006f, 0.045f + i * 0.008f),
                        new Vector3(side * (0.035f + i * 0.018f), 1.28f, 0.10f),
                        new Vector3(side * (0.065f + i * 0.01f), 1.225f, 0.10f - i * 0.012f),
                        new Vector3(side * (0.085f + i * 0.009f), 1.17f - i * 0.025f, 0.04f - i * 0.012f),
                        new Vector3(side * (0.096f + i * 0.008f), 1.12f - i * 0.04f, 0.015f) },
                        new[] { 0.024f, 0.026f, 0.022f, 0.013f, 0.0008f }, new[] { 0.010f, 0.013f, 0.014f, 0.012f, 0.001f }));
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    float baseX = (i - 3.5f) * 0.008f, midX = (i - 3.5f) * 0.023f;
                    result.Add(Swept(new[] { new Vector3(baseX, 1.325f, -0.085f), new Vector3(baseX * 1.3f, 1.28f, -0.16f),
                        new Vector3(midX * 0.7f, 1.15f, -0.245f), new Vector3(midX, 0.98f, -0.27f),
                        new Vector3(midX * 0.8f + 0.015f * Mathf.Sin(i), 0.81f + 0.018f * Mathf.Cos(i), -0.23f),
                        new Vector3((i - 3.5f) * 0.02f, 0.72f + 0.035f * Mathf.Sin(i), -0.17f) },
                        new[] { 0.016f, 0.022f, 0.031f, 0.027f, 0.018f, 0.001f },
                        new[] { 0.011f, 0.014f, 0.018f, 0.016f, 0.011f, 0.001f }));
                }
            }
            return result.ToArray();
        }
        static Shape Swept(Vector3[] centers, float[] widths, float[] depths)
        {
            const int slices = 20, subdivisions = 4;
            var times = new float[centers.Length];
            for (int i = 0; i < times.Length; i++) times[i] = i;
            int rows = (centers.Length - 1) * subdivisions + 1;
            var refinedCenters = new Vector3[rows]; var refinedWidths = new float[rows]; var refinedDepths = new float[rows];
            for (int i = 0; i < rows; i++)
            {
                refinedCenters[i] = Curve(centers, i / (rows - 1f));
                refinedWidths[i] = SmoothValue(times, widths, i / (float)subdivisions);
                refinedDepths[i] = SmoothValue(times, depths, i / (float)subdivisions);
            }
            centers = refinedCenters; widths = refinedWidths; depths = refinedDepths;
            // Keep offset rings inside the bend radius so a dense curl cannot fold inside out.
            for (int ring = 1; ring < centers.Length - 1; ring++)
            {
                Vector3 before = centers[ring] - centers[ring - 1], after = centers[ring + 1] - centers[ring];
                float curvature = (after.normalized - before.normalized).magnitude / Mathf.Max(0.0001f, (before.magnitude + after.magnitude) * 0.5f);
                float scale = Mathf.Min(1, 0.55f / Mathf.Max(0.0001f, curvature * Mathf.Max(widths[ring], depths[ring])));
                widths[ring] *= scale; depths[ring] *= scale;
            }
            var p = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            Vector3 Tangent(int ring) => (centers[Mathf.Min(centers.Length - 1, ring + 1)] - centers[Mathf.Max(0, ring - 1)]).normalized;
            for (int ring = 0; ring < centers.Length; ring++)
            {
                Vector3 tangent = Tangent(ring), across = Vector3.Cross(tangent, Vector3.forward).normalized;
                Vector3 depth = Vector3.Cross(across, tangent).normalized;
                for (int i = 0; i < slices; i++)
                {
                    float a = i * Mathf.PI * 2 / slices;
                    p.Add(centers[ring] + across * (Mathf.Cos(a) * widths[ring]) + depth * (Mathf.Sin(a) * depths[ring]));
                    n.Add((across * (Mathf.Cos(a) / widths[ring]) + depth * (Mathf.Sin(a) / depths[ring])).normalized);
                    uv.Add(new Vector2(i / (float)slices, 1 - ring / (centers.Length - 1f)));
                }
            }
            // Derive the smooth normal from the actual curved surface, including its taper.
            for (int ring = 0; ring < centers.Length; ring++) for (int i = 0; i < slices; i++)
            {
                Vector3 along = p[Mathf.Min(centers.Length - 1, ring + 1) * slices + i] - p[Mathf.Max(0, ring - 1) * slices + i];
                Vector3 around = p[ring * slices + (i + 1) % slices] - p[ring * slices + (i + slices - 1) % slices];
                Vector3 normal = Vector3.Cross(along.normalized, around.normalized).normalized;
                if (Vector3.Dot(normal, n[ring * slices + i]) < 0) normal = -normal;
                n[ring * slices + i] = normal;
            }
            Connect(centers.Length, slices, tri);
            for (int cap = 0; cap < 2; cap++)
            {
                int ring = cap == 0 ? 0 : centers.Length - 1, center = p.Count;
                Vector3 normal = Tangent(ring) * (cap == 0 ? -1 : 1);
                p.Add(centers[ring]); n.Add(normal); uv.Add(new Vector2(0.5f, 1 - cap));
                int rim = p.Count;
                for (int i = 0; i < slices; i++) { p.Add(p[ring * slices + i]); n.Add(normal); uv.Add(new Vector2(i / (float)slices, 1 - cap)); }
                for (int i = 0; i < slices; i++)
                { tri.Add(center); tri.Add(rim + (cap == 0 ? i : (i + 1) % slices)); tri.Add(rim + (cap == 0 ? (i + 1) % slices : i)); }
            }
            return new Shape { points = p.ToArray(), normals = n.ToArray(), uv = uv.ToArray(), indices = tri.ToArray() };
        }
        static void Connect(int rings, int slices, List<int> triangles)
        {
            for (int y = 0; y < rings - 1; y++) for (int i = 0; i < slices; i++)
            {
                int a = y * slices + i, b = (y + 1) * slices + i, c = y * slices + (i + 1) % slices, d = (y + 1) * slices + (i + 1) % slices;
                triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(c); triangles.Add(b); triangles.Add(d);
            }
        }
        static void Release(UnityEngine.Object obj)
        { if (obj == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj); }
        public void Dispose() { Release(Root != null ? Root.gameObject : null); Release(mesh); Release(material); }
    }
}
