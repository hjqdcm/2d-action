using System.Collections.Generic;
using UnityEngine;
using Path = SlashDemo.AscensionCultivator.Path;
using Shape = SlashDemo.AscensionCultivator.Shape;
using Particle = SlashDemo.BeastAttackParticles.SkillParticle;

namespace SlashDemo
{
    sealed class AscensionSkillEffects
    {
        const int TraceBudget = 24;
        enum Form { Crescent, Ring, Blade, Meteor, Seal }

        sealed class Trace
        {
            public LineRenderer Core, Halo;
            public Form Form;
            public Vector2 Origin, Direction;
            public float Size, Detail, Height, Width, Age, Life;
            public bool Active;
        }

        static readonly Color[] Glows =
        {
            new Color(1, .34f, .07f), new Color(.12f, .59f, 1),
            new Color(.47f, .28f, 1), new Color(1, .51f, .06f)
        };
        static readonly AnimationCurve Taper = new AnimationCurve(
            new Keyframe(0, .12f), new Keyframe(.35f, 1), new Keyframe(.73f, .78f), new Keyframe(1, .08f));
        static readonly AnimationCurve Even = AnimationCurve.Constant(0, 1, 1);

        readonly List<Trace> traces = new List<Trace>(TraceBudget);
        readonly ObliqueView view;
        readonly BeastAttackParticles particles;
        readonly Transform root;
        readonly Material light;
        readonly Path path;
        readonly Color tint, glow;
        readonly Trace charge, chargeEcho;
        Vector2 caster;
        float chargeClock, moteTimer;
        bool charging, heavyCharge;
        uint random = 0x3c97a0ef;

        public AscensionSkillEffects(SlashArena arena, Transform owner, Path path, Color tint)
        {
            view = arena.View;
            particles = BeastAttackParticles.For(arena, owner);
            this.path = path;
            this.tint = tint;
            glow = Glows[(int)path];
            root = new GameObject("Cultivator skill effects").transform;
            root.SetParent(owner.GetComponentInParent<TravelMapEnvironment>().transform, false);
            light = new Material(Resources.Load<Shader>("SlashDemo/BladeTrailAdditive")) { hideFlags = HideFlags.DontSave };
            charge = CreateTrace();
            chargeEcho = CreateTrace();
        }

        Trace CreateTrace()
        {
            var trace = new Trace
            {
                Halo = DemoArt.Line("Qi halo", root, new Vector3[32], glow, .15f, -72),
                Core = DemoArt.Line("Qi core", root, new Vector3[32], tint, .05f, -71)
            };
            trace.Halo.useWorldSpace = trace.Core.useWorldSpace = true;
            trace.Halo.sharedMaterial = trace.Core.sharedMaterial = light;
            Hide(trace);
            return trace;
        }

        static void Hide(Trace trace)
        {
            trace.Active = false;
            if (trace.Core != null) trace.Core.enabled = trace.Halo.enabled = false;
        }

        static void Setup(Trace trace, Form form, Vector2 origin, Vector2 direction, float size,
            float detail, float width, float height, float life, float delay)
        {
            trace.Form = form; trace.Origin = origin; trace.Direction = direction; trace.Size = size;
            trace.Detail = detail; trace.Width = width; trace.Height = height; trace.Life = life;
            trace.Age = -delay; trace.Active = true;
            bool loop = form == Form.Ring || form == Form.Seal;
            int samples = form == Form.Blade || form == Form.Meteor ? 13 : 32;
            trace.Core.loop = trace.Halo.loop = loop;
            trace.Core.positionCount = trace.Halo.positionCount = samples;
            trace.Core.widthCurve = trace.Halo.widthCurve = loop ? Even : Taper;
            bool aboveGround = height > .15f || form == Form.Meteor;
            trace.Core.sortingOrder = aboveGround ? 15002 : -71;
            trace.Halo.sortingOrder = trace.Core.sortingOrder - 1;
        }

        void Add(Form form, Vector2 origin, Vector2 direction, float size, float detail,
            float width, float height, float life, float delay = 0)
        {
            var trace = traces.Find(item => !item.Active);
            if (trace == null)
            {
                if (traces.Count == TraceBudget) return;
                trace = CreateTrace();
                traces.Add(trace);
            }
            Setup(trace, form, origin, direction, size, detail, width, height, life, delay);
            if (delay <= 0) Draw(trace, 0, 1);
        }

        public void BeginCharge(Vector2 origin, Vector2 focus, Vector2 aim, bool ultimate)
        {
            caster = origin;
            chargeClock = moteTimer = 0;
            charging = true; heavyCharge = ultimate;
            bool ranged = path == Path.Spell || path == Path.Talisman;
            bool markTarget = ranged && !(path == Path.Spell && ultimate);
            Form form = path == Path.Talisman ? Form.Seal : ranged ? Form.Ring : Form.Crescent;
            float radius = ultimate ? .87f : .57f;
            Setup(charge, form, markTarget ? focus : origin, aim, radius, ranged ? radius * .65f : 230,
                ultimate ? .035f : .024f, .045f, 1, 0);
            if (ultimate)
                Setup(chargeEcho, Form.Seal, origin, aim, 1.15f, 0, .029f, .035f, 1, 0);
            Charge(0, 0);
        }

        public void Charge(float dt, float progress)
        {
            chargeClock += dt;
            moteTimer -= dt;
            charge.Direction = DemoArt.Direction(chargeClock * (heavyCharge ? 185 : 125));
            Draw(charge, progress, .31f + .32f * progress);
            if (heavyCharge)
            {
                chargeEcho.Direction = DemoArt.Direction(-chargeClock * 88);
                Draw(chargeEcho, progress, .35f + .27f * progress);
            }
            if (moteTimer > 0) return;
            moteTimer = heavyCharge ? .055f : .085f;
            int count = heavyCharge ? 6 : 3;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = DemoArt.Direction(chargeClock * 270 + 360f * i / count);
                Vector2 side = new Vector2(-direction.y, direction.x);
                float radius = Mathf.Lerp(heavyCharge ? 1.35f : .86f, .32f, progress);
                var shape = path == Path.Talisman && i == 0 ? Particle.Paper : Particle.Mote;
                particles.SkillMote(caster + direction * radius, Range(.14f, .61f),
                    (-direction + side * .58f) * Range(.8f, 1.9f), Range(.25f, .9f), shape,
                    i % 2 == 0 ? tint : glow, Range(.042f, .09f), heavyCharge ? .54f : .38f);
            }
        }

        public void EndCharge()
        {
            charging = false;
            Hide(charge);
            Hide(chargeEcho);
        }

        public void Impact(Shape shape, Vector2 origin, Vector2 aim, float size, float detail, bool ultimate)
        {
            float weight = ultimate ? 1.35f : 1;
            switch (path)
            {
                case Path.Body:
                    if (shape == Shape.Arc)
                    {
                        Add(Form.Crescent, origin, aim, size, detail, .105f, .37f, .25f);
                        Add(Form.Crescent, origin, aim, size * .93f, detail, .057f, .055f, .34f, .04f);
                        particles.DustBurst(origin + aim * size * .6f, 12, 1.3f);
                    }
                    else
                    {
                        float inside = shape == Shape.Ring ? detail : size * .24f;
                        Add(Form.Ring, origin, aim, size, inside, .085f * weight, .055f, .43f);
                        Add(Form.Ring, origin, aim, size * .98f, inside, .042f, .12f, .48f, .085f);
                        if (ultimate)
                        {
                            Add(Form.Ring, origin, aim, size, inside, .028f, .28f, .56f, .13f);
                            for (int i = 0; i < 8; i++)
                            {
                                Vector2 spoke = DemoArt.Direction(i * 45 + Range(-8, 8));
                                float start = Mathf.Lerp(inside, size, .13f);
                                Add(Form.Blade, origin + spoke * start, spoke, (size - start) * Range(.68f, .98f),
                                    Range(-.11f, .11f), .035f, .04f, .49f, Range(.025f, .085f));
                            }
                        }
                        if (shape == Shape.Circle) particles.DustBurst(origin, 20, 1.6f);
                    }
                    break;
                case Path.Sword:
                    if (shape == Shape.Arc)
                    {
                        Add(Form.Crescent, origin, aim, size, detail, .115f, .62f, .31f);
                        Add(Form.Crescent, origin, aim, size * .95f, detail, .053f, .82f, .41f, .045f);
                    }
                    else
                    {
                        Vector2 side = new Vector2(-aim.y, aim.x) * detail * .45f;
                        Add(Form.Blade, origin, aim, size, .15f, .095f * weight, .50f, .32f);
                        Add(Form.Blade, origin + side, aim, size * .97f, .095f, .044f, .69f, .49f, .055f);
                        if (ultimate) Add(Form.Blade, origin - side, aim, size * .96f, -.11f, .05f, .34f, .49f, .09f);
                    }
                    break;
                case Path.Spell:
                    if (shape == Shape.Lane)
                    {
                        Vector2 side = new Vector2(-aim.y, aim.x) * detail * .67f;
                        Add(Form.Blade, origin, aim, size, .22f, .105f, .23f, .4f);
                        Add(Form.Blade, origin + side, aim, size, -.18f, .061f, .16f, .49f, .035f);
                        Add(Form.Blade, origin - side, aim, size, .18f, .061f, .16f, .49f, .035f);
                    }
                    else
                    {
                        Add(Form.Meteor, origin, -aim, (ultimate ? 2.25f : 1.65f), .31f, .12f * weight, .055f, .28f);
                        Add(Form.Ring, origin, aim, size, ultimate ? size : size * .17f, .065f, .085f, .49f);
                        Add(Form.Seal, origin, aim, size * .88f, 0, .035f * weight, .13f, .59f, .055f);
                        if (ultimate)
                        {
                            Vector2 side = Vector2.Perpendicular(aim);
                            Add(Form.Meteor, origin + side * size * .36f, -aim, 1.7f, .46f, .057f, .08f, .34f, .04f);
                            Add(Form.Meteor, origin - side * size * .36f, side, 1.48f, .32f, .054f, .07f, .32f, .085f);
                            Add(Form.Seal, origin, side, size * .56f, 0, .032f, .18f, .57f, .09f);
                        }
                    }
                    break;
                case Path.Talisman:
                    Add(Form.Seal, origin, aim, size * .94f, 0, .046f * weight, .08f, .61f);
                    Add(Form.Ring, origin, aim, size, size * .36f, .05f, .045f, .42f, .03f);
                    Add(Form.Seal, origin, Vector2.Perpendicular(aim), size * .57f, 0, .035f, .29f, .53f, .075f);
                    if (ultimate) Add(Form.Meteor, origin, Vector2.zero, 1.65f, 0, .073f, .1f, .34f, .045f);
                    break;
            }
            Scatter(shape, origin, aim, size, detail, ultimate);
        }

        void Scatter(Shape shape, Vector2 origin, Vector2 aim, float size, float detail, bool ultimate)
        {
            float heading = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            int count = ultimate ? 37 : 23;
            for (int i = 0; i < count; i++)
            {
                float arc = shape == Shape.Arc ? detail * .5f : 180;
                Vector2 direction = DemoArt.Direction(heading + Range(-arc, arc));
                Vector2 point = shape == Shape.Lane
                    ? origin + aim * Range(.06f, 1) * size + Vector2.Perpendicular(aim) * detail * Range(-.85f, .85f)
                    : origin + direction * Range(shape == Shape.Ring ? detail : size * .22f, size * .97f);
                Particle kind = Particle.Spark;
                if (path == Path.Body && i % 2 == 0) kind = Particle.Stone;
                if (path == Path.Spell && i % 3 != 0) kind = Particle.Star;
                if (path == Path.Talisman && i % 3 == 0) kind = Particle.Paper;
                float particleSize = kind == Particle.Paper || kind == Particle.Star ? Range(.085f, .145f) : Range(.049f, .12f);
                float height = path == Path.Sword ? Range(.23f, .69f) : Range(.06f, .27f);
                Color color = kind == Particle.Stone ? Color.Lerp(glow, new Color(.26f, .17f, .11f), .68f)
                    : i % 3 == 0 ? glow : tint;
                particles.SkillMote(point, height, direction * Range(.4f, ultimate ? 2.3f : 1.6f),
                    Range(.4f, ultimate ? 1.9f : 1.35f), kind, color,
                    particleSize, Range(.32f, ultimate ? .9f : .68f));
            }
        }

        float Range(float min, float max)
        {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return Mathf.Lerp(min, max, (random & 0xffffff) / 16777216f);
        }

        public void Advance(float dt)
        {
            foreach (var trace in traces)
            {
                if (!trace.Active) continue;
                trace.Age += dt;
                if (trace.Age < 0) continue;
                if (trace.Age >= trace.Life) { Hide(trace); continue; }
                float progress = trace.Age / trace.Life;
                Draw(trace, progress, Mathf.Pow(1 - progress, 1.45f));
            }
        }

        void Draw(Trace trace, float progress, float opacity)
        {
            int count = trace.Core.positionCount;
            bool closed = trace.Core.loop;
            float heading = Mathf.Atan2(trace.Direction.y, trace.Direction.x) * Mathf.Rad2Deg;
            float growth = Mathf.Sqrt(progress);
            for (int i = 0; i < count; i++)
            {
                float u = i / (float)(closed ? count : count - 1);
                float height = trace.Height;
                Vector2 offset;
                switch (trace.Form)
                {
                    case Form.Crescent:
                        float angle = heading + trace.Detail * (u - .5f) + progress * 13;
                        offset = DemoArt.Direction(angle) * trace.Size * Mathf.Lerp(.78f, 1, growth);
                        break;
                    case Form.Ring:
                        offset = DemoArt.Direction(u * 360) * Mathf.Lerp(trace.Detail, trace.Size, growth);
                        break;
                    case Form.Blade:
                        float bow = Mathf.Sin(u * Mathf.PI) * trace.Detail;
                        offset = trace.Direction * (u * trace.Size * Mathf.Lerp(.88f, 1, growth))
                            + Vector2.Perpendicular(trace.Direction) * bow;
                        break;
                    case Form.Meteor:
                        offset = trace.Direction * (u * trace.Detail);
                        height += u * trace.Size * Mathf.Lerp(1, .26f, growth);
                        break;
                    default:
                        float tooth = Mathf.Lerp(.64f, 1, 1 - Mathf.Abs(Mathf.Repeat(u * 8, 1) * 2 - 1));
                        offset = DemoArt.Direction(heading + u * 360 + progress * 45)
                            * (trace.Size * tooth * Mathf.Lerp(.64f, 1, growth));
                        break;
                }
                Vector3 point = view.LiftPoint(trace.Origin + offset, height);
                trace.Core.SetPosition(i, point);
                trace.Halo.SetPosition(i, point);
            }
            Color core = Color.Lerp(tint, Color.white, .57f);
            Color halo = glow;
            core.a = .83f * opacity; halo.a = .23f * opacity;
            trace.Core.startColor = trace.Core.endColor = core;
            trace.Halo.startColor = trace.Halo.endColor = halo;
            trace.Core.widthMultiplier = trace.Width;
            trace.Halo.widthMultiplier = trace.Width * 3.5f;
            trace.Core.enabled = trace.Halo.enabled = true;
        }

        public void Clear()
        {
            if (charging) EndCharge();
            foreach (var trace in traces) Hide(trace);
            if (particles != null) particles.Clear();
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root.gameObject);
            Object.Destroy(light);
        }
    }
}
