using System.Collections.Generic;
using UnityEngine;
using Path = SlashDemo.AscensionCultivator.Path;
using Shape = SlashDemo.AscensionCultivator.Shape;
using Particle = SlashDemo.BeastAttackParticles.SkillParticle;

namespace SlashDemo
{
    sealed class AscensionSkillEffects
    {
        const int TraceBudget = 36;
        const float FrostBeamHeight = .28f;
        enum Form { Crescent, Ring, Blade, Streak, Meteor, Seal, Beam }

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
        static readonly Color[] PassiveColors =
        {
            new Color(1, .14f, .10f), new Color(.29f, .73f, 1),
            new Color(.70f, .50f, 1), new Color(1, .68f, .18f)
        };
        static readonly AnimationCurve Taper = new AnimationCurve(
            new Keyframe(0, .12f), new Keyframe(.35f, 1), new Keyframe(.73f, .78f), new Keyframe(1, .08f));
        static readonly AnimationCurve Even = AnimationCurve.Constant(0, 1, 1);
        static readonly Color FrostBeamCore = new Color(.91f, .86f, 1);
        static readonly Color FrostBeamGlow = new Color(.48f, .30f, 1);

        readonly List<Trace> traces = new List<Trace>(TraceBudget);
        readonly ObliqueView view;
        readonly BeastAttackParticles particles;
        readonly Transform root;
        readonly Material light;
        readonly Path path;
        readonly Color tint, glow;
        readonly SpriteRenderer passiveAura;
        readonly Sprite auraSprite;
        readonly Texture2D auraTexture;
        readonly Trace charge, chargeEcho;
        Vector2 caster;
        float chargeClock, moteTimer;
        bool charging, heavyCharge, counterCharge, rushCharge, vortexCharge;
        int guardStacks;
        float passiveClock, passiveFlash, passiveMoteTimer;
        uint random = 0x3c97a0ef;

        public AscensionSkillEffects(SlashArena arena, Transform owner, Transform model, Path path, Color tint)
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
            auraTexture = CreateAuraTexture();
            var aura = new GameObject("Cultivator passive aura");
            aura.transform.SetParent(model, false);
            aura.transform.localPosition = new Vector3(0, .73f, 0);
            passiveAura = aura.AddComponent<SpriteRenderer>();
            auraSprite = Sprite.Create(auraTexture, new Rect(0, 0, auraTexture.width, auraTexture.height),
                new Vector2(.5f, .5f), auraTexture.width, 0, SpriteMeshType.FullRect);
            passiveAura.sprite = auraSprite;
            passiveAura.sharedMaterial = DemoArt.Material;
            passiveAura.sortingOrder = -1;
            passiveAura.enabled = false;
        }

        static Texture2D CreateAuraTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Cultivator passive glow", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var offset = new Vector2(x + .5f, y + .5f) * (2f / size) - Vector2.one;
                float fade = Mathf.Clamp01(1 - offset.sqrMagnitude);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * fade * fade));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public void FlashPassive(Vector2 ground)
        {
            passiveFlash = .55f;
            DrawPassiveAura(.88f);
            Particle kind = path == Path.Spell ? Particle.Star : path == Path.Talisman ? Particle.Paper : Particle.Spark;
            int count = path == Path.Spell ? 14 : 7;
            Color color = PassiveColors[(int)path];
            for (int i = 0; i < count; i++)
            {
                Vector2 outward = DemoArt.Direction(i * 360f / count);
                particles.SkillMote(ground + outward * Range(.10f, .28f), Range(.36f, 1.25f),
                    outward * Range(.25f, 1.05f), Range(.45f, 1.35f), kind, color,
                    Range(.07f, .14f), Range(.35f, .65f));
            }
        }

        public void UpdatePassive(float dt, Vector2 ground, Vector2 travel, int marks, bool slowing)
        {
            passiveClock += dt;
            passiveFlash = Mathf.Max(0, passiveFlash - dt);
            float motion = Mathf.InverseLerp(.1f, 4, travel.magnitude / dt);
            float steady = path switch
            {
                Path.Body => .59f,
                Path.Sword => .32f + .24f * motion,
                Path.Talisman => slowing ? .66f : .25f * marks,
                _ => 0
            };
            float pulse = .92f + .08f * Mathf.Sin(passiveClock * 8);
            DrawPassiveAura(Mathf.Max(steady * pulse, .88f * (passiveFlash / .55f)));
            if (path != Path.Sword || motion <= .15f)
            {
                passiveMoteTimer = 0;
                return;
            }
            passiveMoteTimer -= dt;
            if (passiveMoteTimer > 0) return;
            passiveMoteTimer = .075f;
            Vector2 behind = -travel.normalized;
            Vector2 offset = behind * .28f + Vector2.Perpendicular(behind) * Range(-.22f, .22f);
            particles.SkillMote(ground + offset, Range(.31f, 1.16f), behind * Range(.8f, 2), .22f,
                Particle.Spark, PassiveColors[(int)path], Range(.065f, .115f), .31f);
        }

        void DrawPassiveAura(float alpha)
        {
            passiveAura.enabled = alpha > 0;
            Color color = PassiveColors[(int)path];
            color.a = alpha;
            passiveAura.color = color;
            passiveAura.transform.localScale = new Vector3(1.38f, 1.74f, 1) * (.9f + .22f * alpha);
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
            bool straight = form == Form.Streak || form == Form.Beam;
            int samples = straight ? 2 : form == Form.Blade || form == Form.Meteor ? 13 : 32;
            trace.Core.loop = trace.Halo.loop = loop;
            trace.Core.positionCount = trace.Halo.positionCount = samples;
            trace.Core.widthCurve = trace.Halo.widthCurve = loop || straight ? Even : Taper;
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

        public void BeginCharge(Vector2 origin, Vector2 focus, Vector2 aim, bool ultimate, bool mechanic)
        {
            caster = origin;
            chargeClock = moteTimer = 0;
            charging = true; heavyCharge = ultimate;
            counterCharge = mechanic && path == Path.Body;
            rushCharge = mechanic && path == Path.Sword;
            vortexCharge = mechanic && path == Path.Spell;
            guardStacks = 0;
            bool ranged = path == Path.Spell || path == Path.Talisman;
            bool markTarget = ranged && !(path == Path.Spell && ultimate);
            Form form = counterCharge || path == Path.Talisman ? Form.Seal
                : ranged ? Form.Ring : rushCharge ? Form.Blade : Form.Crescent;
            float radius = ultimate ? .87f : .57f;
            float detail = rushCharge ? .10f : ranged ? radius * .65f : 230;
            Setup(charge, form, markTarget ? focus : origin, aim, rushCharge ? 1.42f : radius, detail,
                ultimate || mechanic ? .035f : .024f, rushCharge ? .48f : .045f, 1, 0);
            if (vortexCharge)
                Setup(chargeEcho, Form.Seal, focus, aim, 1.04f, 0, .031f, .09f, 1, 0);
            else if (ultimate || counterCharge)
                Setup(chargeEcho, Form.Seal, origin, aim, ultimate ? 1.15f : .82f, 0, .029f,
                    counterCharge ? .42f : .035f, 1, 0);
            Charge(0, 0);
        }

        public void Charge(float dt, float progress)
        {
            chargeClock += dt;
            moteTimer -= dt;
            if (!rushCharge) charge.Direction = DemoArt.Direction(chargeClock * (heavyCharge ? 185 : 125));
            Draw(charge, progress, .31f + .32f * progress + .09f * guardStacks);
            if (heavyCharge || counterCharge || vortexCharge)
            {
                chargeEcho.Direction = DemoArt.Direction(-chargeClock * 88);
                Draw(chargeEcho, progress, .35f + .27f * progress + .09f * guardStacks);
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

        public void AbsorbCounterHit(Vector2 origin, int stacks)
        {
            guardStacks = stacks;
            float radius = .70f + .13f * stacks;
            Add(Form.Ring, origin, Vector2.right, radius, radius * .55f, .055f, .46f, .28f);
            for (int i = 0; i < 10; i++)
            {
                Vector2 direction = DemoArt.Direction(i * 36 + stacks * 13);
                particles.SkillMote(origin + direction * radius, .4f, direction * Range(.8f, 2),
                    Range(.35f, 1.15f), Particle.Spark, tint, Range(.06f, .1f), .38f);
            }
        }

        public void ReleaseCounter(Vector2 origin, Vector2 aim, float radius, int stacks)
        {
            Impact(Shape.Circle, origin, aim, radius, 0, false);
            Add(Form.Seal, origin, aim, radius * .76f, 0, .05f + .011f * stacks, .11f, .56f);
            for (int i = 0; i < 6 + stacks * 2; i++)
            {
                Vector2 direction = DemoArt.Direction(i * 360f / (6 + stacks * 2));
                Add(Form.Blade, origin + direction * radius * .19f, direction, radius * .77f,
                    Range(-.18f, .18f), .042f, .055f, .38f + .045f * stacks, Range(0, .075f));
            }
        }

        public void RushTrail(Vector2 origin, Vector2 aim, float distance)
        {
            Vector2 side = Vector2.Perpendicular(aim);
            Add(Form.Streak, origin, aim, distance, 0, .10f, .57f, .16f);
            Add(Form.Streak, origin + side * .27f, aim, distance, 0, .043f, .29f, .13f);
            Add(Form.Streak, origin - side * .26f, aim, distance, 0, .035f, .83f, .12f);
        }

        public void OpenVortex(Vector2 center, float radius)
        {
            Add(Form.Ring, center, Vector2.right, radius, radius * .12f, .084f, .07f, .46f);
            Add(Form.Ring, center, Vector2.right, radius * .16f, radius * .94f, .068f, .16f, .62f, .08f);
            Add(Form.Seal, center, Vector2.right, radius * .70f, 0, .060f, .13f, .64f, .05f);
            Add(Form.Meteor, center, Vector2.zero, 2.75f, 0, .10f, .06f, .38f, .08f);
            for (int i = 0; i < 24; i++)
            {
                Vector2 outward = DemoArt.Direction(i * 15 + Range(-6, 6));
                Vector2 offset = outward * (radius * Range(.62f, .96f));
                float life = Range(.40f, .66f);
                particles.SkillMote(center + offset, Range(.07f, .4f), -offset * (.9f / life),
                    Range(.48f, 1.7f), i % 3 == 0 ? Particle.Star : Particle.Spark,
                    i % 3 == 0 ? new Color(.55f, .91f, 1) : tint, Range(.08f, .16f), life);
            }
        }

        public void RaiseWall(Vector2 center, Vector2 normal, float halfLength)
        {
            Vector2 along = Vector2.Perpendicular(normal);
            for (int i = -2; i <= 2; i++)
            {
                Vector2 anchor = center + along * (halfLength * .43f * i);
                Add(Form.Seal, anchor, normal, .43f, 0, .045f, .075f, .58f, Mathf.Abs(i) * .04f);
                Add(Form.Meteor, anchor, Vector2.zero, 1.40f, 0, .052f, .15f, .4f, Mathf.Abs(i) * .06f);
            }
            for (int i = 0; i < 18; i++)
            {
                Vector2 point = center + along * (halfLength * (i / 17f * 2 - 1));
                particles.SkillMote(point, .16f, along * Range(-.8f, .8f) + normal * Range(-.2f, .2f),
                    Range(.75f, 2.1f), i % 2 == 0 ? Particle.Paper : Particle.Spark,
                    i % 2 == 0 ? tint : glow, Range(.08f, .15f), Range(.45f, .78f));
            }
        }

        void FireFrostBeam(Vector2 origin, Vector2 aim, float length, float halfWidth)
        {
            Add(Form.Beam, origin, aim, length, 0, halfWidth, FrostBeamHeight, .38f);
            particles.SkillMote(origin, FrostBeamHeight, Vector2.zero, 0,
                Particle.Star, FrostBeamCore, halfWidth * .88f, .19f);
            Vector2 side = Vector2.Perpendicular(aim);
            for (int i = 0; i < 16; i++)
            {
                float along = (i + Range(.1f, .9f)) / 16;
                Vector2 point = origin + aim * (length * along) + side * (halfWidth * Range(-.6f, .6f));
                Vector2 velocity = aim * Range(1.3f, 3.2f) + side * Range(-.38f, .38f);
                particles.SkillMote(point, FrostBeamHeight, velocity, Range(.04f, .29f), Particle.Spark,
                    i % 3 == 0 ? FrostBeamCore : FrostBeamGlow, Range(.042f, .10f), Range(.16f, .30f));
            }
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
                        Add(Form.Crescent, origin, aim, size * .83f, detail * .86f, .037f, .64f, .37f, .05f);
                        particles.DustBurst(origin + aim * size * .6f, 12, 1.3f);
                    }
                    else
                    {
                        float inside = shape == Shape.Ring ? detail : size * .24f;
                        Add(Form.Ring, origin, aim, size, inside, .085f * weight, .055f, .43f);
                        Add(Form.Ring, origin, aim, size * .98f, inside, .042f, .12f, .48f, .085f);
                        if (ultimate) Add(Form.Ring, origin, aim, size, inside, .04f, .31f, .56f, .13f);
                        int cracks = ultimate ? 8 : 5;
                        for (int i = 0; i < cracks; i++)
                        {
                            Vector2 spoke = DemoArt.Direction(i * 360f / cracks + Range(-8, 8));
                            float start = Mathf.Lerp(inside, size, .13f);
                            Add(Form.Blade, origin + spoke * start, spoke, (size - start) * Range(.68f, .98f),
                                Range(-.11f, .11f), .035f, .04f, .49f, Range(.025f, .085f));
                        }
                        if (shape == Shape.Circle) particles.DustBurst(origin, 20, 1.6f);
                    }
                    break;
                case Path.Sword:
                    if (shape == Shape.Arc)
                    {
                        Add(Form.Crescent, origin, aim, size, detail, .115f, .62f, .31f);
                        Add(Form.Crescent, origin, aim, size * .95f, detail, .053f, .82f, .41f, .045f);
                        Add(Form.Crescent, origin, aim, size * .92f, detail * .84f, .039f, .36f, .49f, .08f);
                    }
                    else
                    {
                        Vector2 side = new Vector2(-aim.y, aim.x) * detail * .45f;
                        Add(Form.Blade, origin, aim, size, .15f, .095f * weight, .50f, .32f);
                        Add(Form.Blade, origin + side, aim, size * .97f, .095f, .044f, .69f, .49f, .055f);
                        Add(Form.Blade, origin - side, aim, size * .96f, -.11f, .05f, .34f, .49f, .09f);
                        if (ultimate) Add(Form.Ring, origin + aim * size * .5f, aim, detail * 2,
                            detail * .25f, .038f, .51f, .35f, .035f);
                    }
                    break;
                case Path.Spell:
                    if (shape == Shape.Lane)
                    {
                        FireFrostBeam(origin, aim, size, detail);
                        return;
                    }
                    else
                    {
                        Add(Form.Meteor, origin, -aim, (ultimate ? 2.25f : 1.65f), .31f, .12f * weight, .055f, .28f);
                        Add(Form.Ring, origin, aim, size, ultimate ? size : size * .17f, .065f, .085f, .49f);
                        Add(Form.Seal, origin, aim, size * .88f, 0, .035f * weight, .13f, .59f, .055f);
                        Add(Form.Ring, origin, aim, size * .77f, size * .23f, .038f, .38f, .37f, .085f);
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
                    Add(Form.Ring, origin, aim, size * .83f, size * .21f, .038f, .47f, .45f, .08f);
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
            if (trace.Form == Form.Beam)
            {
                DrawFrostBeam(trace, progress);
                return;
            }

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
                    case Form.Streak:
                        offset = trace.Direction * (u * trace.Size);
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

        void DrawFrostBeam(Trace trace, float progress)
        {
            Vector3 start = view.LiftPoint(trace.Origin, trace.Height);
            Vector3 end = view.LiftPoint(trace.Origin + trace.Direction * trace.Size, trace.Height);
            trace.Core.SetPosition(0, start);
            trace.Core.SetPosition(1, end);
            trace.Halo.SetPosition(0, start);
            trace.Halo.SetPosition(1, end);

            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, 1, progress));
            Color core = FrostBeamCore;
            Color halo = FrostBeamGlow;
            core.a = fade;
            halo.a = .34f * fade;
            trace.Core.startColor = trace.Core.endColor = core;
            trace.Halo.startColor = trace.Halo.endColor = halo;
            trace.Core.widthMultiplier = trace.Width * Mathf.Lerp(.06f, .44f, fade);
            trace.Halo.widthMultiplier = trace.Width * Mathf.Lerp(.42f, 1.9f, fade);
            trace.Core.enabled = trace.Halo.enabled = true;
        }

        public void Clear()
        {
            if (charging) EndCharge();
            foreach (var trace in traces) Hide(trace);
            passiveClock = passiveFlash = passiveMoteTimer = 0;
            passiveAura.enabled = false;
            if (particles != null) particles.Clear();
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root.gameObject);
            Object.Destroy(light);
            Object.Destroy(auraSprite);
            Object.Destroy(auraTexture);
        }
    }
}
