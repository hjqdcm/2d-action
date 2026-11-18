using System.Collections.Generic;
using UnityEngine;
using Shape = SlashDemo.EvilCultivatorBoss.Shape;
using Particle = SlashDemo.BeastAttackParticles.SkillParticle;

namespace SlashDemo
{
    sealed class FinalBossSkillEffects
    {
        enum Form { Arc, BloodCut, BloodScar, BloodTide, FlyingCrescent, Beam, Ring, Seal }
        const int Samples = 40, TraceBudget = 36;
        const float SaberTrailLife = .09f;
        const int SaberTrailSamples = 10;
        static readonly float[] CrackOffsets = { 0, -.53f, .37f, -.79f, .31f, -.38f, 0 };
        static readonly AnimationCurve StrokeWidth = new AnimationCurve(
            new Keyframe(0, .10f), new Keyframe(.24f, .88f), new Keyframe(.65f, 1), new Keyframe(1, .08f));
        static readonly AnimationCurve CrescentWidth = new AnimationCurve(
            new Keyframe(0, 0), new Keyframe(.25f, .65f), new Keyframe(.52f, 1),
            new Keyframe(.8f, .48f), new Keyframe(1, 0));
        static readonly AnimationCurve EvenWidth = AnimationCurve.Constant(0, 1, 1);
        static readonly Color CrescentCore = new Color(.73f, .022f, .045f);
        static readonly Color CrescentGlow = new Color(.60f, .008f, .024f);
        static readonly Color BloodCutCore = new Color(.93f, .035f, .075f);
        static readonly Color BloodCutGlow = new Color(.78f, .012f, .050f);
        static readonly Color BloodTideCore = new Color(.74f, .037f, .068f);
        static readonly Color BloodTideGlow = new Color(.47f, .014f, .045f);
        static readonly Color TideMistTint = new Color(.27f, .022f, .052f, .46f);
        static readonly Color BloodScarCore = new Color(.43f, .014f, .042f);
        static readonly Color BloodMistTint = new Color(.40f, .025f, .055f, .64f);
        static readonly Color BloodEmberTint = new Color(.95f, .12f, .16f, .92f);
        static readonly int EdgeHighlight = Shader.PropertyToID("_EdgeHighlight");
        static readonly int SoftEdge = Shader.PropertyToID("_SoftEdge");
        const float CrescentStart = .32f;

        sealed class Trace
        {
            public LineRenderer Core, Halo;
            public Form Form;
            public Vector2 Origin, Direction;
            public Vector3 LaunchOffset;
            public float Size, Detail, Height, Width, Age, Duration;
            public bool Active;

            public void Hide()
            {
                Active = false;
                if (Core != null) Core.enabled = false;
                if (Halo != null) Halo.enabled = false;
            }
        }

        readonly bool bell;
        readonly Color coreColor, glowColor;
        readonly ObliqueView view;
        readonly BeastAttackParticles particles;
        readonly Transform root;
        readonly Material material;
        readonly MaterialPropertyBlock traceProperties = new MaterialPropertyBlock();
        readonly List<Trace> traces = new List<Trace>(TraceBudget);
        readonly Trace charge;
        readonly Trace saberTrail;
        readonly SpriteRenderer bloodTideMist;
        readonly Material bloodTideMistMaterial;
        readonly List<(Vector3 point, float time)> saberTrailPoints = new List<(Vector3, float)>(SaberTrailSamples);
        float saberTrailTime;

        public FinalBossSkillEffects(SlashArena arena, Transform owner, bool bell)
        {
            view = arena.View;
            this.bell = bell;
            coreColor = bell ? new Color(.64f, 1, .90f) : new Color(1, .52f, .43f);
            glowColor = bell ? new Color(.18f, .95f, .67f) : new Color(1, .07f, .11f);
            particles = BeastAttackParticles.For(arena, owner);
            root = new GameObject("Final boss skill effects").transform;
            root.SetParent(owner.parent, false);
            material = new Material(Resources.Load<Shader>("SlashDemo/BladeTrailAdditive")) { hideFlags = HideFlags.DontSave };
            charge = CreateTrace();
            charge.Core.positionCount = charge.Halo.positionCount = bell ? 20 : 3;
            charge.Core.widthCurve = charge.Halo.widthCurve = StrokeWidth;
            charge.Core.sortingOrder = 15003;
            charge.Halo.sortingOrder = 15002;
            if (!bell)
            {
                saberTrail = CreateTrace();
                saberTrail.Core.name = "Saber swing trail";
                saberTrail.Core.sortingOrder = 15002;
                saberTrail.Halo.sortingOrder = 15001;
                saberTrail.Core.widthCurve = saberTrail.Halo.widthCurve = StrokeWidth;
                bloodTideMistMaterial = new Material(Resources.Load<Shader>("SlashDemo/BloodTideMist"))
                    { hideFlags = HideFlags.DontSave };
                bloodTideMist = DemoArt.Shape("Blood tide mist", root, "Square", Vector2.zero, Vector2.one, Color.white, -65);
                bloodTideMist.sharedMaterial = bloodTideMistMaterial;
                bloodTideMist.enabled = false;
            }
        }

        Trace CreateTrace()
        {
            var points = new Vector3[Samples];
            var trace = new Trace
            {
                Core = DemoArt.Line("Skill core", root, points, coreColor, .05f, -72),
                Halo = DemoArt.Line("Skill glow", root, points, glowColor, .12f, -73)
            };
            trace.Core.useWorldSpace = trace.Halo.useWorldSpace = true;
            trace.Core.sharedMaterial = trace.Halo.sharedMaterial = material;
            trace.Hide();
            return trace;
        }

        public void Charge(Vector3 grip, Vector3 tip, float progress)
        {
            float energy = Mathf.SmoothStep(0, 1, progress);
            int count = charge.Core.positionCount;
            for (int i = 0; i < count; i++)
            {
                float u = i / (count - 1f);
                Vector3 point;
                if (bell)
                {
                    Vector2 orbit = DemoArt.Direction(progress * 450 + u * 275);
                    float radius = Mathf.Lerp(.27f, .16f, energy);
                    point = tip + Vector3.right * (orbit.x * radius)
                        + view.Up * ((orbit.y * .53f + (u - .5f) * .5f) * radius);
                }
                else
                {
                    // Light gathers along the blade from the guard to the tip.
                    point = Vector3.Lerp(grip, tip, Mathf.Lerp(.24f, .46f, energy) + u * Mathf.Lerp(.42f, .54f, energy));
                }
                SetPoint(charge, i, point);
            }
            Style(charge, bell ? Mathf.Lerp(.026f, .054f, energy) : .020f,
                bell ? Mathf.Lerp(.21f, .79f, energy) : Mathf.Lerp(.16f, .37f, energy));
        }

        public void EndCharge() => charge.Hide();

        public void BloodFrenzy(Vector2 origin)
        {
            Add(Form.Ring, origin, Vector2.right, 1.15f, .26f, .12f, .16f, .40f);
            BloodMist(origin, Vector2.zero);
        }

        public void BloodMist(Vector2 origin, Vector2 drift)
        {
            var deepRed = new Color(.28f, .014f, .036f, .59f);
            var red = new Color(.67f, .032f, .065f, .44f);
            for (int i = 0; i < 18; i++)
            {
                float height = Mathf.Lerp(.17f, 2.2f, i / 17f);
                Vector2 outward = DemoArt.Direction(i * 137.5f);
                particles.SkillMote(origin + outward * (.12f + i % 3 * .10f), height,
                    drift + outward * .7f, .30f, Particle.Mist, i % 3 == 0 ? red : deepRed,
                    .25f + i % 4 * .055f, .42f + i % 3 * .09f);
            }
        }

        public void TrackSaberTip(Vector3 tip)
        {
            if (saberTrail == null) return;
            int count = saberTrailPoints.Count;
            if (count != 0 && (tip - saberTrailPoints[count - 1].point).sqrMagnitude < .0004f) return;
            if (count == SaberTrailSamples) saberTrailPoints.RemoveAt(0);
            saberTrailPoints.Add((tip, saberTrailTime));
            DrawSaberTrail();
        }

        void DrawSaberTrail()
        {
            int count = saberTrailPoints.Count;
            if (count < 2)
            {
                saberTrail.Hide();
                return;
            }
            saberTrail.Core.positionCount = saberTrail.Halo.positionCount = count;
            for (int i = 0; i < count; i++) SetPoint(saberTrail, i, saberTrailPoints[i].point);
            float age = saberTrailTime - saberTrailPoints[count - 1].time;
            Style(saberTrail, .026f, .58f * (1 - Mathf.Clamp01(age / SaberTrailLife)));
        }

        public void Impact(Shape shape, Vector2 origin, Vector2 direction, float size, float detail, bool seal, float power)
        {
            switch (shape)
            {
                case Shape.Arc:
                    Add(Form.Arc, origin, direction, size, detail, .17f, .60f, .34f);
                    Add(Form.Arc, origin, direction, size * .88f, detail * .86f, .09f, .91f, .26f);
                    break;
                case Shape.Lane:
                    Add(Form.Beam, origin, direction, size, detail, .17f, .22f, .42f);
                    Add(Form.Beam, origin, direction, size * .94f, detail, .07f, .07f, .34f, .035f);
                    break;
                case Shape.Circle:
                case Shape.Ring:
                    float inner = shape == Shape.Ring ? detail : 0;
                    Add(Form.Ring, origin, direction, size, Mathf.Max(inner, size * .2f), .13f, .06f, .49f);
                    Add(Form.Ring, origin, direction, Mathf.Lerp(inner, size, .83f),
                        Mathf.Max(inner, size * .11f), .074f, .14f, .50f, .07f);
                    if (seal)
                        Add(Form.Seal, origin, direction, size * .76f, 0, .061f, .13f, .60f);
                    break;
            }
            Scatter(shape, origin, direction, size, detail, seal, power);
        }

        public void BloodCut(Vector2 origin, Vector2 direction, float length, float halfWidth)
        {
            // The descending edge travels down the center of the same lane the player saw.
            Add(Form.BloodCut, origin, direction, length, halfWidth, halfWidth * .48f, .74f, .28f, outlined: true);
            Add(Form.BloodScar, origin, direction, length, halfWidth * .23f, halfWidth * .18f, .035f, .53f);
            Vector2 side = Vector2.Perpendicular(direction);
            for (int i = 0; i < 18; i++)
            {
                float t = (i + .5f) / 18;
                float lateral = Mathf.Sin(i * 2.4f);
                Vector2 point = origin + direction * (length * t) + side * (lateral * halfWidth * .42f);
                bool mist = i % 3 == 0;
                Vector2 drift = direction * (mist ? .28f : Mathf.Lerp(.8f, 2.2f, t))
                    + side * (lateral * (mist ? .25f : .72f));
                particles.SkillMote(point, mist ? .05f : Mathf.Lerp(.56f, .12f, t), drift, mist ? .30f : .70f,
                    mist ? Particle.Mist : Particle.Spark, mist ? BloodMistTint : BloodEmberTint,
                    mist ? halfWidth * .37f : .045f + t * .035f, mist ? .55f : .24f + t * .1f);
            }
        }

        public void FlyingCrescent(Vector3 launch, Vector2 origin, Vector2 direction, float length, float halfWidth)
        {
            float span = halfWidth * 1.88f;
            float height = span + .14f;
            Vector3 launchOffset = launch - view.LiftPoint(origin + direction * CrescentStart, height);
            Add(Form.FlyingCrescent, origin, direction, length, span, .24f, height, .38f,
                launchOffset: launchOffset, outlined: true);
            Add(Form.FlyingCrescent, origin, direction, length * .98f, span * .92f, .054f, height - .045f,
                .38f, delay: .012f, launchOffset: launchOffset);
            Scatter(Shape.Lane, origin, direction, length * .17f, halfWidth, false, 1.4f);
        }

        public void BloodTide(Vector2 origin, float radius)
        {
            const float width = .30f;
            Add(Form.BloodTide, origin, DemoArt.Direction(19), radius, 108, width, .12f, .44f);
            Add(Form.BloodTide, origin, DemoArt.Direction(145), radius * .94f, 85, width * .77f, .09f, .39f, .022f);
            Add(Form.BloodTide, origin, DemoArt.Direction(258), radius * .97f, 112, width * .92f, .14f, .45f, .011f);
            const int fissures = 5;
            for (int i = 0; i < fissures; i++)
            {
                Vector2 ray = DemoArt.Direction(i * 137.5f + 31);
                float start = radius * (.16f + .12f * (i % 4));
                float length = radius * (.26f + .08f * (i % 3));
                Add(Form.BloodScar, origin + ray * start, ray, length, length * .051f,
                    i % 3 == 0 ? .080f : .048f, .035f, .44f, .018f * (i % 4));
            }

            const int count = 42;
            for (int i = 0; i < count; i++)
            {
                // The mist starts inside the crests and spreads across their wake.
                float fraction = i / (float)count;
                Vector2 ray = DemoArt.Direction(i * 137.5f);
                Vector2 spot = origin + ray * (radius * .78f * fraction);
                bool mist = i % 3 != 0;
                Vector2 drift = ray * (mist ? .75f : 2.25f) + Vector2.Perpendicular(ray) * .22f;
                float size = mist ? .57f * (.69f + .12f * (i % 5)) : .045f + .02f * (i % 3);
                particles.SkillMote(spot, mist ? .085f : .16f, drift, mist ? .22f : .68f,
                    mist ? Particle.Mist : Particle.Spark, mist ? TideMistTint : BloodEmberTint,
                    size, mist ? .55f + .07f * (i % 4) : .22f + .045f * (i % 4));
            }
        }

        public void ShowBloodTideMist(Vector2 center, float radius)
        {
            bloodTideMist.transform.position = new Vector3(center.x, center.y, -.035f);
            bloodTideMist.transform.localScale = new Vector3(radius * 2, radius * 2, 1);
            bloodTideMist.enabled = true;
            UpdateBloodTideMist(0, 1);
        }

        public void UpdateBloodTideMist(float age, float fade)
        {
            bloodTideMist.enabled = fade > 0;
            bloodTideMistMaterial.SetFloat("_Age", age);
            bloodTideMistMaterial.SetFloat("_Fade", fade);
        }

        void Scatter(Shape shape, Vector2 origin, Vector2 aim, float size, float detail, bool seal, float power)
        {
            int count = seal ? 9 : 13;
            float heading = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float t = (i + .5f) / count;
                float angle = heading + (t - .5f) * (shape == Shape.Arc ? detail : 360);
                Vector2 outward = DemoArt.Direction(angle);
                float inner = shape == Shape.Ring ? detail : size * .34f;
                Vector2 spot = shape == Shape.Lane
                    ? origin + aim * (size * t) + Vector2.Perpendicular(aim) * (Mathf.Sin(i * 2.4f) * detail * .6f)
                    : origin + outward * Mathf.Lerp(inner, size, i % 2 == 0 ? .68f : .92f);
                Vector2 drift = shape == Shape.Lane ? aim : shape == Shape.Arc ? Vector2.Perpendicular(outward) : outward;
                Particle kind = seal && i % 3 == 0 ? Particle.Paper
                    : bell && i % 2 == 0 ? Particle.Star : Particle.Spark;
                bool airborne = shape == Shape.Arc || shape == Shape.Lane;
                particles.SkillMote(spot, airborne ? .44f : .10f, drift * (.35f + .65f * t) * power,
                    bell ? .72f : .24f, kind, i % 3 == 0 ? coreColor : glowColor,
                    .053f + .038f * t, bell ? .50f : .32f);
            }
        }

        void Add(Form form, Vector2 origin, Vector2 direction, float size, float detail,
            float width, float height, float duration, float delay = 0, Vector3 launchOffset = default, bool outlined = false)
        {
            var trace = traces.Find(item => !item.Active);
            if (trace == null)
            {
                if (traces.Count >= TraceBudget) return;
                trace = CreateTrace();
                traces.Add(trace);
            }
            trace.Form = form;
            trace.Origin = origin;
            trace.Direction = direction;
            trace.LaunchOffset = launchOffset;
            trace.Size = size;
            trace.Detail = detail;
            trace.Width = width;
            trace.Height = height;
            trace.Age = -delay;
            trace.Duration = duration;
            trace.Active = true;
            bool loop = form is Form.Ring or Form.Seal;
            int count = form switch
            {
                Form.Seal => 8,
                Form.BloodScar => CrackOffsets.Length,
                Form.Beam => 3,
                _ => Samples
            };
            trace.Core.loop = trace.Halo.loop = loop;
            trace.Core.positionCount = trace.Halo.positionCount = count;
            trace.Core.widthCurve = trace.Halo.widthCurve = form is Form.FlyingCrescent or Form.BloodTide ? CrescentWidth
                : loop ? EvenWidth : StrokeWidth;
            traceProperties.SetFloat(EdgeHighlight, outlined ? 1 : 0);
            traceProperties.SetFloat(SoftEdge, form == Form.BloodTide ? 1 : 0);
            trace.Core.SetPropertyBlock(traceProperties);
            traceProperties.SetFloat(EdgeHighlight, 0);
            trace.Halo.SetPropertyBlock(traceProperties);
            trace.Core.sortingOrder = height > .18f ? 15002 : -72;
            trace.Halo.sortingOrder = trace.Core.sortingOrder - 1;
            if (delay == 0) Draw(trace);
        }

        public void Advance(float dt)
        {
            if (saberTrailPoints.Count != 0)
            {
                saberTrailTime += dt;
                while (saberTrailPoints.Count != 0 && saberTrailTime - saberTrailPoints[0].time >= SaberTrailLife)
                    saberTrailPoints.RemoveAt(0);
                DrawSaberTrail();
            }
            foreach (var trace in traces)
            {
                if (!trace.Active) continue;
                trace.Age += dt;
                if (trace.Age >= trace.Duration) trace.Hide();
                else if (trace.Age >= 0) Draw(trace);
            }
        }

        void Draw(Trace trace)
        {
            float t = trace.Age / trace.Duration;
            if (trace.Form == Form.FlyingCrescent)
            {
                DrawFlyingCrescent(trace, t);
                return;
            }
            float travel = 1 - (1 - t) * (1 - t);
            float heading = Mathf.Atan2(trace.Direction.y, trace.Direction.x) * Mathf.Rad2Deg;
            Vector2 side = Vector2.Perpendicular(trace.Direction);
            int count = trace.Core.positionCount;
            for (int i = 0; i < count; i++)
            {
                float u = i / (float)(trace.Core.loop ? count : count - 1);
                float height = trace.Height;
                Vector2 offset;
                switch (trace.Form)
                {
                    case Form.Arc:
                        offset = DemoArt.Direction(heading + (u - .5f) * trace.Detail)
                            * (trace.Size * Mathf.Lerp(.77f, 1, travel));
                        break;
                    case Form.BloodCut:
                        offset = trace.Direction * (trace.Size * u * Mathf.Lerp(.76f, 1, Mathf.Clamp01(travel * 1.9f)))
                            + side * (trace.Detail * .22f * Mathf.Sin(u * Mathf.PI));
                        height = Mathf.Lerp(height * Mathf.Lerp(1, .46f, travel), .07f, u * u);
                        break;
                    case Form.Beam:
                        offset = trace.Direction * (trace.Size * Mathf.Lerp(u, .5f, .17f * (1 - travel)));
                        break;
                    case Form.BloodTide:
                        float crest = Mathf.Sin(u * Mathf.PI);
                        float radius = trace.Size * Mathf.Lerp(.33f, .94f, Mathf.Clamp01(travel * 1.6f));
                        float angle = heading + (u - .5f) * trace.Detail + travel * 19;
                        offset = DemoArt.Direction(angle) * (radius * (.80f + .20f * crest + .08f * (u - .5f)));
                        height *= crest * (1 - .58f * t);
                        break;
                    case Form.BloodScar:
                        offset = trace.Direction * (u * trace.Size * Mathf.Lerp(.66f, 1, travel))
                            + side * (trace.Detail * CrackOffsets[i]);
                        break;
                    case Form.Seal:
                        offset = DemoArt.Direction(heading + i * 45) * (trace.Size * (i % 2 == 0 ? 1 : .57f));
                        break;
                    default:
                        offset = DemoArt.Direction(u * 360) * Mathf.Lerp(trace.Detail, trace.Size, travel);
                        break;
                }
                SetPoint(trace, i, view.LiftPoint(trace.Origin + offset, height));
            }
            float opacity = Mathf.Pow(1 - t, 1.25f);
            if (trace.Form == Form.BloodTide) opacity *= Mathf.Lerp(.57f, .90f, Mathf.Clamp01(t * 7));
            Style(trace, trace.Width * Mathf.Lerp(1, .46f, t), opacity);
        }

        void DrawFlyingCrescent(Trace trace, float t)
        {
            float spread = Mathf.SmoothStep(0, 1, t / .24f);
            float span = trace.Detail * Mathf.Lerp(.32f, 1, spread);
            float distance = Mathf.Lerp(CrescentStart, trace.Size, t * (2 - t));
            Vector2 ground = trace.Origin + trace.Direction * distance;
            Vector3 center = view.LiftPoint(ground, trace.Height) + trace.LaunchOffset * (1 - spread);
            float facing = trace.Direction.x < -.12f ? -1 : 1;
            // Keep the crescent upright and readable when its lane points straight at the camera.
            for (int i = 0; i < Samples; i++)
            {
                float vertical = i * 2f / (Samples - 1) - 1;
                float curve = vertical * (.09f - vertical * .52f);
                Vector3 point = center + view.Up * (vertical * span) + Vector3.right * (curve * facing * span);
                SetPoint(trace, i, point);
            }
            float opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.39f, 1, t));
            Style(trace, trace.Width * Mathf.Lerp(.68f, 1, spread), opacity);
        }

        static void SetPoint(Trace trace, int index, Vector3 point)
        {
            trace.Core.SetPosition(index, point);
            trace.Halo.SetPosition(index, point);
        }

        void Style(Trace trace, float width, float opacity)
        {
            Color core = trace.Form switch
            {
                Form.FlyingCrescent => CrescentCore,
                Form.BloodCut => BloodCutCore,
                Form.BloodTide => BloodTideCore,
                Form.BloodScar => BloodScarCore,
                _ => coreColor
            };
            Color glow = trace.Form switch
            {
                Form.FlyingCrescent => CrescentGlow,
                Form.BloodCut => BloodCutGlow,
                Form.BloodTide => BloodTideGlow,
                Form.BloodScar => BloodScarCore,
                _ => glowColor
            };
            core.a = opacity * .74f;
            glow.a = opacity * .20f;
            trace.Core.startColor = trace.Core.endColor = core;
            trace.Halo.startColor = trace.Halo.endColor = glow;
            trace.Core.widthMultiplier = width;
            trace.Halo.widthMultiplier = width * 3;
            trace.Core.enabled = trace.Halo.enabled = true;
        }

        public void Clear()
        {
            charge.Hide();
            saberTrail?.Hide();
            if (bloodTideMist != null) bloodTideMist.enabled = false;
            saberTrailPoints.Clear();
            foreach (var trace in traces) trace.Hide();
            if (particles != null) particles.Clear();
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root.gameObject);
            Object.Destroy(material);
            if (bloodTideMistMaterial != null) Object.Destroy(bloodTideMistMaterial);
        }
    }
}
