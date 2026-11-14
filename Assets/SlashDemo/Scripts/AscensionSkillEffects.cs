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
        enum Form { Crescent, Ring, Blade, Streak, Meteor, Beam, Fault, Guard, FlyingSword, Orbit, Talisman }

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
        static readonly Vector2[] SwordOutline =
        {
            new Vector2(.68f, 0), new Vector2(.36f, .10f), new Vector2(-.15f, .10f),
            new Vector2(-.15f, .21f), new Vector2(-.26f, .21f), new Vector2(-.26f, .046f),
            new Vector2(-.5f, .046f), new Vector2(-.5f, -.046f), new Vector2(-.26f, -.046f),
            new Vector2(-.26f, -.21f), new Vector2(-.15f, -.21f), new Vector2(-.15f, -.10f),
            new Vector2(.36f, -.10f)
        };
        static readonly Vector2[] TalismanOutline =
        {
            new Vector2(-.25f, 0), new Vector2(.25f, 0), new Vector2(.25f, 1),
            new Vector2(-.25f, 1), new Vector2(-.25f, .15f), new Vector2(.12f, .15f),
            new Vector2(-.08f, .38f), new Vector2(.12f, .57f), new Vector2(-.12f, .57f),
            new Vector2(.12f, .82f), new Vector2(-.25f, .82f)
        };
        static readonly float[] FaultOffsets = { 0, .65f, -.35f, 1, -.72f, .42f, -.46f, 0 };

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
        Vector2 caster, chargeAim;
        float chargeClock, moteTimer;
        bool charging, heavyCharge;
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
            bool loop = form == Form.Ring || form == Form.Guard || form == Form.FlyingSword;
            bool straight = form == Form.Streak || form == Form.Beam;
            int samples = form switch
            {
                Form.Streak or Form.Beam => 2,
                Form.Guard => 6,
                Form.FlyingSword => SwordOutline.Length,
                Form.Talisman => TalismanOutline.Length,
                Form.Fault => FaultOffsets.Length,
                Form.Blade or Form.Meteor => 13,
                _ => 32
            };
            trace.Core.loop = trace.Halo.loop = loop;
            trace.Core.positionCount = trace.Halo.positionCount = samples;
            trace.Core.widthCurve = trace.Halo.widthCurve = loop || straight || form == Form.Talisman ? Even : Taper;
            bool aboveGround = height > .15f || form == Form.Meteor || form == Form.Talisman;
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
            chargeAim = aim;
            chargeClock = moteTimer = 0;
            charging = true; heavyCharge = ultimate;
            guardStacks = 0;
            Hide(chargeEcho);
            switch (path)
            {
                case Path.Body:
                    Setup(charge, mechanic ? Form.Guard : Form.Crescent, origin, aim, ultimate ? 1.02f : .80f,
                        200, .052f, mechanic ? .36f : .065f, 1, 0);
                    if (mechanic || ultimate)
                        Setup(chargeEcho, Form.Guard, origin, aim, 1.06f, 0, .033f, .07f, 1, 0);
                    break;
                case Path.Sword:
                    float length = ultimate || mechanic ? 1.34f : 1.05f;
                    Setup(charge, Form.FlyingSword, origin, aim, length, 0, .032f, .77f, 1, 0);
                    Setup(chargeEcho, Form.FlyingSword, origin, aim, length, 0, .029f, .46f, 1, 0);
                    break;
                case Path.Spell:
                    Vector2 center = mechanic ? focus : origin;
                    Setup(charge, Form.Orbit, center, aim, ultimate || mechanic ? 1.20f : .84f, 275,
                        .040f, .22f, 1, 0);
                    Setup(chargeEcho, Form.Orbit, center, -aim, charge.Size, -275, .027f, .32f, 1, 0);
                    break;
                case Path.Talisman:
                    Setup(charge, Form.Talisman, origin, Vector2.right, ultimate ? .97f : .73f, .48f,
                        .036f, .62f, 1, 0);
                    Setup(chargeEcho, Form.Talisman, focus, Vector2.right, mechanic ? .87f : .52f, 0,
                        .030f, .10f, 1, 0);
                    break;
            }
            Charge(0, 0);
        }

        public void Charge(float dt, float progress)
        {
            chargeClock += dt;
            moteTimer -= dt;
            if (path == Path.Sword)
            {
                Vector2 side = Vector2.Perpendicular(chargeAim);
                float spacing = Mathf.Lerp(1.03f, .36f, progress);
                Vector2 center = caster + chargeAim * Mathf.Lerp(-.34f, .52f, progress);
                charge.Origin = center + side * spacing;
                chargeEcho.Origin = center - side * spacing;
            }
            else if (path == Path.Spell)
            {
                charge.Direction = DemoArt.Direction(chargeClock * 195);
                chargeEcho.Direction = -charge.Direction;
            }
            Draw(charge, progress, .31f + .32f * progress + .09f * guardStacks);
            if (chargeEcho.Active)
                Draw(chargeEcho, progress, .35f + .27f * progress + .09f * guardStacks);
            if (moteTimer > 0) return;
            moteTimer = heavyCharge ? .055f : .085f;
            int count = heavyCharge ? 6 : 3;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = DemoArt.Direction(chargeClock * 270 + 360f * i / count);
                Vector2 side = new Vector2(-direction.y, direction.x);
                float radius = Mathf.Lerp(heavyCharge ? 1.35f : .86f, .32f, progress);
                switch (path)
                {
                    case Path.Body:
                        particles.SkillMote(caster + direction * radius, .055f, direction * Range(.12f, .48f),
                            Range(.45f, 1.35f), Particle.Stone, tint, Range(.076f, .16f), .42f);
                        break;
                    case Path.Sword:
                        Vector2 blade = i % 2 == 0 ? charge.Origin : chargeEcho.Origin;
                        particles.SkillMote(blade - chargeAim * Range(.18f, .52f), i % 2 == 0 ? .77f : .46f,
                            -chargeAim * Range(.72f, 1.8f), 0, Particle.Spark, tint, Range(.064f, .14f), .25f);
                        break;
                    case Path.Spell:
                        particles.SkillMote(charge.Origin + direction * radius, Range(.15f, .65f),
                            (side - direction * .45f) * Range(.7f, 1.35f), .50f, Particle.Star,
                            i % 2 == 0 ? tint : FrostBeamGlow, Range(.065f, .13f), .42f);
                        break;
                    case Path.Talisman:
                        particles.SkillMote(caster + direction * radius, Range(.32f, .85f),
                            side * Range(.18f, .72f), Range(.38f, 1.05f), i % 3 == 0 ? Particle.Paper : Particle.Spark,
                            i % 2 == 0 ? tint : glow, Range(.065f, .14f), .45f);
                        break;
                }
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
            Add(Form.Guard, origin, chargeAim, radius, 0, .055f, .46f, .28f);
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
            Add(Form.Guard, origin, aim, radius * .76f, 0, .05f + .011f * stacks, .11f, .56f);
            for (int i = 0; i < 6 + stacks * 2; i++)
            {
                Vector2 direction = DemoArt.Direction(i * 360f / (6 + stacks * 2));
                Add(Form.Fault, origin + direction * radius * .19f, direction, radius * .77f,
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
            Add(Form.Orbit, center, Vector2.right, radius * .86f, 310, .060f, .13f, .64f, .05f);
            Add(Form.Orbit, center, Vector2.left, radius * .78f, -310, .044f, .31f, .59f, .09f);
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
                Add(Form.Talisman, anchor, normal, .63f, .10f, .045f, .075f, .69f, Mathf.Abs(i) * .04f);
                Add(Form.Streak, anchor, normal, .38f, 0, .046f, .11f, .39f, Mathf.Abs(i) * .06f);
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
            Add(Form.Orbit, origin, aim, halfWidth * .66f, 260, .038f, .26f, .36f);
            Add(Form.Orbit, origin + aim * length, -aim, halfWidth * .74f, -275, .032f, .18f, .38f, .035f);
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
                        Vector2 side = Vector2.Perpendicular(aim);
                        for (int i = -1; i <= 1; i++)
                            Add(Form.Fault, origin + side * (i * size * .12f), (aim + side * (i * .32f)).normalized,
                                size * .81f, size * .062f, .054f, .055f, .50f, Mathf.Abs(i) * .035f);
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
                            float reach = (size - start) * Range(.68f, .98f);
                            Add(Form.Fault, origin + spoke * start, spoke, reach,
                                reach * Range(.09f, .16f), .052f, .04f, .57f, Range(.025f, .085f));
                            particles.DustBurst(origin + spoke * (start + reach * .75f), 3, 1.55f);
                        }
                        if (shape == Shape.Circle) particles.DustBurst(origin, 20, 1.6f);
                    }
                    break;
                case Path.Sword:
                    if (shape == Shape.Arc)
                    {
                        Add(Form.Crescent, origin, aim, size, detail, .115f, .62f, .31f);
                        Vector2 side = Vector2.Perpendicular(aim);
                        for (int i = -1; i <= 1; i++)
                        {
                            float angle = detail * .30f * i * Mathf.Deg2Rad;
                            Vector2 blade = aim * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                            Add(Form.FlyingSword, origin + blade * (size * .30f), blade, size * .27f, size * .47f,
                                .037f, .58f + .19f * Mathf.Abs(i), .35f, Mathf.Abs(i) * .045f);
                        }
                    }
                    else
                    {
                        Vector2 side = new Vector2(-aim.y, aim.x) * detail * .45f;
                        Add(Form.Blade, origin, aim, size, .15f, .095f * weight, .50f, .32f);
                        for (int i = -1; i <= 1; i++)
                        {
                            float delay = (i + 1) * .055f;
                            Add(Form.FlyingSword, origin + aim * (size * .18f) + side * i, aim,
                                size * .30f, size * .58f, .042f, .67f + .18f * i, .36f, delay);
                        }
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
                        Add(Form.Orbit, origin, aim, size * .79f, 295, .058f * weight, .13f, .54f, .045f);
                        Add(Form.Orbit, origin, -aim, size * .69f, -265, .034f, .24f, .51f, .085f);
                        if (ultimate)
                        {
                            Vector2 side = Vector2.Perpendicular(aim);
                            Add(Form.Meteor, origin + side * size * .36f, -aim, 1.7f, .46f, .057f, .08f, .34f, .04f);
                            Add(Form.Meteor, origin - side * size * .36f, side, 1.48f, .32f, .054f, .07f, .32f, .085f);
                        }
                    }
                    break;
                case Path.Talisman:
                    Add(Form.Ring, origin, aim, size, size * .27f, .061f * weight, .045f, .40f);
                    for (int i = 0; i < (ultimate ? 3 : 2); i++)
                    {
                        Vector2 outward = DemoArt.Direction(i * 132 + Range(-22, 22));
                        Add(Form.Talisman, origin + outward * (size * .28f), outward, size * .48f,
                            size * .39f, .045f, .16f, .66f, i * .045f);
                    }
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
                switch (path)
                {
                    case Path.Body:
                        particles.SkillMote(point, .06f, direction * Range(.65f, 2.1f),
                            Range(1.0f, 2.6f), Particle.Stone, Color.Lerp(glow, new Color(.24f, .15f, .09f), Range(.3f, .8f)),
                            Range(.11f, .22f), Range(.44f, .70f));
                        break;
                    case Path.Sword:
                        Vector2 travel = shape == Shape.Lane ? aim : Vector2.Perpendicular(direction);
                        particles.SkillMote(point, Range(.38f, .88f), travel * Range(1.3f, 3.0f),
                            Range(.02f, .35f), Particle.Spark, i % 3 == 0 ? glow : tint,
                            Range(.055f, .13f), Range(.19f, .36f));
                        break;
                    case Path.Spell:
                        bool star = i % 3 == 0;
                        particles.SkillMote(point, star ? Range(.12f, .4f) : Range(.75f, 1.9f),
                            direction * Range(.45f, 1.42f), star ? Range(.55f, 1.4f) : Range(-2.1f, -1.0f),
                            star ? Particle.Star : Particle.Spark, star ? tint : FrostBeamCore,
                            Range(.07f, .16f), Range(.30f, .61f));
                        break;
                    case Path.Talisman:
                        bool paper = i % 3 == 0;
                        particles.SkillMote(point, Range(.08f, .27f),
                            (direction + Vector2.Perpendicular(direction) * .45f) * Range(.25f, 1.1f),
                            Range(.7f, paper ? 1.35f : 2.2f), paper ? Particle.Paper : Particle.Spark,
                            paper ? tint : glow, Range(.07f, paper ? .19f : .12f), Range(.36f, paper ? .84f : .58f));
                        break;
                }
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
            Vector2 side = Vector2.Perpendicular(trace.Direction);
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
                            + side * bow;
                        break;
                    case Form.Streak:
                        offset = trace.Direction * (u * trace.Size);
                        break;
                    case Form.Meteor:
                        offset = trace.Direction * (u * trace.Detail);
                        height += u * trace.Size * Mathf.Lerp(1, .26f, growth);
                        break;
                    case Form.Fault:
                        offset = trace.Direction * (u * trace.Size * Mathf.Lerp(.48f, 1, growth))
                            + side * (FaultOffsets[i] * trace.Detail);
                        break;
                    case Form.Guard:
                        offset = DemoArt.Direction(heading + u * 360) * (trace.Size * Mathf.Lerp(.76f, 1, growth));
                        break;
                    case Form.FlyingSword:
                        Vector2 blade = SwordOutline[i] * trace.Size;
                        offset = trace.Direction * (blade.x + trace.Detail * progress * (2 - progress)) + side * blade.y;
                        break;
                    case Form.Orbit:
                        float angleAround = heading + u * trace.Detail + Mathf.Sign(trace.Detail) * progress * 115;
                        offset = DemoArt.Direction(angleAround) * (trace.Size * Mathf.Lerp(1, .26f, u) * Mathf.Lerp(.94f, .52f, progress));
                        height += u * trace.Size * .36f;
                        break;
                    case Form.Talisman:
                        Vector2 ink = TalismanOutline[i] * trace.Size;
                        offset = trace.Direction * (trace.Detail * progress) + Vector2.right * ink.x;
                        height += ink.y + progress * .48f;
                        break;
                    default:
                        continue;
                }
                Vector3 point = view.LiftPoint(trace.Origin + offset, height);
                trace.Core.SetPosition(i, point);
                trace.Halo.SetPosition(i, point);
            }
            Color core = Color.Lerp(tint, Color.white, trace.Form == Form.Fault ? .22f : .57f);
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
