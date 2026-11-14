using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    public enum PlayableCharacter { Swordsman }

    sealed class FiveStarSwordSlash
    {
        public const string Name = "五星剑斩";
        public const string IconResource = "SlashDemo/FiveStarSwordSlashIcon";
        public const string ProgressBuffId = "five-star-sword-progress";
        public const float Cooldown = 30;
        public const int FlowingCloudRequired = 3, IronSwordRequired = 12;
        const int SlashCount = 5, DamagePerSlash = 36;
        const float Radius = 2.75f * 1.25f, HalfWidth = .82f;
        const float AnimationTimeScale = 1.35f;
        const float CoreWidth = .11f * 2, HaloWidth = .26f * 2;
        const float Windup = .10f * AnimationTimeScale, Interval = .072f * AnimationTimeScale;
        const float Duration = .55f * AnimationTimeScale, SweepTime = .036f * AnimationTimeScale;
        const float LastCut = Windup + (SlashCount - 1) * Interval;
        static readonly Color Light = new Color(.64f, .85f, 1);
        static readonly Color Blue = new Color(.37f, .65f, 1);
        static readonly Color Gold = new Color(1, .78f, .38f);
        static readonly AnimationCurve Taper = new AnimationCurve(
            new Keyframe(0, .06f), new Keyframe(.24f, .74f), new Keyframe(.72f, 1), new Keyframe(1, .025f));

        readonly SlashPlayer owner;
        readonly SlashArena arena;
        readonly Rigidbody2D body;
        readonly Vector2[] points = new Vector2[SlashCount];
        readonly LineRenderer[] cores = new LineRenderer[SlashCount], halos = new LineRenderer[SlashCount];
        readonly List<SlashEnemy> targets = new List<SlashEnemy>();
        readonly List<RaycastHit2D> sight = new List<RaycastHit2D>();
        readonly List<Vector3> facetVertices = new List<Vector3>(SlashCount * 9);
        readonly List<Color> facetColors = new List<Color>(SlashCount * 9);
        readonly List<Vector2> facetUvs = new List<Vector2>(SlashCount * 9);
        readonly List<int> facetTriangles = new List<int>(SlashCount * 9);
        Material material;
        Mesh facets;
        Transform visuals;
        int emitted;
        float age;

        public int Progress { get; private set; }
        public float CooldownLeft { get; private set; }
        public bool Active { get; private set; }
        public Vector2 Aim { get; private set; }
        public float PoseProgress
        {
            get
            {
                float time = Mathf.Max(0, age - (Windup - SweepTime));
                int cut = Mathf.Min(SlashCount - 1, Mathf.FloorToInt(time / Interval));
                float swing = Mathf.Clamp01((time - cut * Interval) / SweepTime);
                return cut % 2 == 0 ? swing : 1 - swing;
            }
        }
        public float PoseRecovery => Mathf.InverseLerp(LastCut, Duration, age);

        public FiveStarSwordSlash(SlashPlayer owner, SlashArena arena)
        {
            this.owner = owner;
            this.arena = arena;
            body = owner.GetComponent<Rigidbody2D>();
        }

        public int Required => owner.HasIronSword ? IronSwordRequired : FlowingCloudRequired;
        public string Requirement => owner.HasIronSword ? "满蓄力重击" : "满层 Q";
        public bool Ready => !Active && (owner.InfiniteUltimateForDebug || (CooldownLeft <= 0 && Progress >= Required));
        public void RecordQualification() => Progress = Mathf.Min(Required, Progress + 1);
        public void ResetProgress() => Progress = 0;

        public void Begin(Vector2 center, Vector2 aim)
        {
            Active = true;
            Aim = aim;
            emitted = 0;
            age = 0;
            if (!owner.InfiniteUltimateForDebug)
            {
                Progress = 0;
                CooldownLeft = Cooldown;
            }
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int i = 0; i < points.Length; i++)
                points[i] = center + DemoArt.Direction(angle + i * 144) * Radius;
            EnsureLines();
            Draw();
            visuals.gameObject.SetActive(true);
        }

        public void Advance(float dt)
        {
            CooldownLeft = Mathf.Max(0, CooldownLeft - dt);
            if (!Active) return;
            age += dt;
            while (Active && emitted < SlashCount && age >= Windup + emitted * Interval)
            {
                int index = emitted++;
                Slash(points[index], points[(index + 1) % SlashCount], index == SlashCount - 1);
            }
            if (age >= Duration) Cancel();
            else if (Active) Draw();
        }

        void Slash(Vector2 start, Vector2 end, bool last)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 origin = owner.transform.position;
            arena.Effects.PlaySwing(last);
            arena.Effects.Ghost(owner.transform);
            arena.Effects.DrawSwordSlashFlecks(start, end, Light);
            arena.CameraRig.Shake(last ? .075f : .017f);

            targets.Clear();
            targets.AddRange(arena.Enemies);
            foreach (var enemy in targets)
            {
                if (!Active || arena.Paused) break;
                if (enemy == null || !enemy.Alive || !enemy.isActiveAndEnabled) continue;
                Vector2 point = enemy.transform.position;
                if (SwordMotion.DistanceToBlade(point, start, end, out _) > HalfWidth + enemy.Radius
                    || Blocked(origin, point)) continue;
                enemy.TakeHit(DamagePerSlash, direction, 0, last);
            }
            if (arena.Travel != null && arena.Travel.IsChallengeDecisionOpen) Cancel();
        }

        bool Blocked(Vector2 origin, Vector2 target)
        {
            Physics2D.Linecast(origin, target, new ContactFilter2D { useTriggers = false }, sight);
            foreach (var hit in sight)
                if (hit.collider.attachedRigidbody != body && hit.collider.GetComponentInParent<SlashEnemy>() == null)
                    return true;
            return false;
        }

        void EnsureLines()
        {
            if (material != null) return;
            material = new Material(Resources.Load<Shader>("SlashDemo/BladeTrailAdditive")) { hideFlags = HideFlags.DontSave };
            visuals = new GameObject(Name + " · 特效").transform;
            visuals.SetParent(owner.transform, false);
            visuals.gameObject.SetActive(false);
            var facetObject = new GameObject(Name + " · 锋刃");
            facetObject.transform.SetParent(visuals, false);
            facets = new Mesh { name = Name + " · 锋刃", hideFlags = HideFlags.DontSave };
            facets.MarkDynamic();
            facetObject.AddComponent<MeshFilter>().sharedMesh = facets;
            var facetRenderer = facetObject.AddComponent<MeshRenderer>();
            facetRenderer.sharedMaterial = material;
            facetRenderer.sortingOrder = 15001;
            for (int i = 0; i < SlashCount; i++)
            {
                halos[i] = CreateLine("剑芒", HaloWidth, 15001);
                cores[i] = CreateLine("剑光", CoreWidth, 15002);
            }
        }

        LineRenderer CreateLine(string label, float width, int sortingOrder)
        {
            var line = DemoArt.Line(Name + " · " + label, visuals, new Vector3[2], Light, width, sortingOrder);
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.widthCurve = Taper;
            line.widthMultiplier = width;
            return line;
        }

        void Draw()
        {
            facetVertices.Clear();
            facetColors.Clear();
            facetUvs.Clear();
            facetTriangles.Clear();
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(LastCut, Duration, age));
            float finalFlash = age >= LastCut ? 1 - Mathf.Clamp01((age - LastCut) / .10f) : 0;
            for (int i = 0; i < SlashCount; i++)
            {
                float cutAge = age - (Windup + i * Interval);
                float sweep = Mathf.Clamp01((cutAge + SweepTime) / SweepTime);
                bool visible = sweep > 0;
                halos[i].enabled = cores[i].enabled = visible;
                if (!visible) continue;
                float flash = 1 - Mathf.Clamp01(cutAge / (.055f * AnimationTimeScale));
                float height = Mathf.Lerp(.09f, .39f, flash);
                float travel = sweep * (2 - sweep);
                Vector2 destination = points[(i + 1) % SlashCount];
                Vector2 tip = Vector2.Lerp(points[i], destination, travel);
                Vector3 start = arena.View.LiftPoint(points[i], height);
                Vector3 end = arena.View.LiftPoint(tip, height);
                cores[i].SetPosition(0, start); cores[i].SetPosition(1, end);
                halos[i].SetPosition(0, arena.View.LiftPoint(points[i], .065f));
                halos[i].SetPosition(1, arena.View.LiftPoint(tip, .065f));

                bool last = i == SlashCount - 1;
                Color glow = last ? Gold : Color.Lerp(Blue, Gold, finalFlash * .56f);
                Color core = Color.Lerp(last ? Gold : Light, Color.white, .76f);
                glow.a = (.19f + flash * .43f + finalFlash * .31f) * fade;
                core.a = Mathf.Clamp01(.24f + flash * .74f + finalFlash * .54f) * fade;
                cores[i].startColor = cores[i].endColor = core;
                halos[i].startColor = halos[i].endColor = glow;

                float facetFade = 1 - Mathf.Clamp01(cutAge / .12f);
                if (facetFade > 0)
                    DrawBladeFacets(points[i], destination, tip, height, last, facetFade * fade);
            }
            facets.Clear();
            facets.SetVertices(facetVertices);
            facets.SetColors(facetColors);
            facets.SetUVs(0, facetUvs);
            facets.SetTriangles(facetTriangles, 0);
        }

        void DrawBladeFacets(Vector2 start, Vector2 destination, Vector2 tip, float height, bool last, float opacity)
        {
            Vector2 along = (destination - start).normalized;
            Vector2 side = Vector2.Perpendicular(along);
            float length = Mathf.Min(Vector2.Distance(start, tip), last ? 3.6f : 2.85f);
            float width = Mathf.Min(length * .10f, last ? .32f : .22f);
            Vector2 shoulder = tip - along * (length * .34f);
            Vector2 tail = tip - along * length;
            Color edge = last ? Gold : Light;
            Color bodyColor = last ? new Color(.95f, .40f, .10f) : Blue;
            edge.a = .83f * opacity;
            bodyColor.a = .52f * opacity;
            AddFacet(tail, shoulder + side * width, tip, height, bodyColor, edge);
            AddFacet(tail, tip, shoulder - side * (width * .42f), height, edge, bodyColor);
            AddFacet(tail + side * (width * .82f), shoulder + side * (width * 1.31f),
                tip - along * (length * .20f) + side * (width * .80f), height + .08f, bodyColor, edge);
        }

        void AddFacet(Vector2 tail, Vector2 ridge, Vector2 tip, float height, Color tint, Color highlight)
        {
            int first = facetVertices.Count;
            facetVertices.Add(visuals.InverseTransformPoint(arena.View.LiftPoint(tail, height)));
            facetVertices.Add(visuals.InverseTransformPoint(arena.View.LiftPoint(ridge, height)));
            facetVertices.Add(visuals.InverseTransformPoint(arena.View.LiftPoint(tip, height)));
            Color fade = tint;
            fade.a *= .08f;
            facetColors.Add(fade);
            facetColors.Add(tint);
            facetColors.Add(highlight);
            facetUvs.Add(Vector2.zero);
            facetUvs.Add(new Vector2(.45f, .5f));
            facetUvs.Add(Vector2.right);
            facetTriangles.Add(first);
            facetTriangles.Add(first + 1);
            facetTriangles.Add(first + 2);
        }

        public void Cancel()
        {
            Active = false;
            if (visuals != null) visuals.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (material != null) Object.Destroy(material);
            if (facets != null) Object.Destroy(facets);
        }
    }
}
