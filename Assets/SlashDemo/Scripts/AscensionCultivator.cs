using UnityEngine;

namespace SlashDemo
{
    public sealed class AscensionCultivator : MonoBehaviour
    {
        public enum Path { Body, Sword, Spell, Talisman }

        readonly struct Profile
        {
            public readonly string Name, Resource;
            public readonly int Health;
            public readonly float Speed, Damage, Reach, Windup;
            public readonly float DamageReduction, SmallRecovery, UltimateRecovery, UltimateCooldown;
            public readonly Color ImpactColor;

            public Profile(string name, string resource, int health, float speed, float damage, float reach,
                float windup, float damageReduction, float smallRecovery, float ultimateRecovery,
                float ultimateCooldown, Color impactColor)
            {
                Name = name; Resource = resource; Health = health; Speed = speed; Damage = damage;
                Reach = reach; Windup = windup; ImpactColor = impactColor;
                DamageReduction = damageReduction; SmallRecovery = smallRecovery;
                UltimateRecovery = ultimateRecovery; UltimateCooldown = ultimateCooldown;
            }
        }

        static readonly Profile[] Profiles =
        {
            new Profile("体修", "BodyCultivator-v1", 660, 3.10f, 1, 1.25f, 1.16f,
                .15f, .82f, 1.15f, 6, new Color(1, .68f, .33f)),
            new Profile("剑修", "SwordCultivator-v1", 528, 4.50f, 1.08f, 1.58f, 1,
                0, .56f, .94f, 5.5f, new Color(.7f, .84f, 1)),
            new Profile("法修", "SpellCultivator-v1", 495, 3.05f, 1.42f, 2.48f, 1.43f,
                0, .83f, 1.18f, 6.8f, new Color(.74f, .66f, 1)),
            new Profile("符修", "TalismanCultivator-v1", 572, 3.45f, .84f, 3.05f, 1.33f,
                0, .85f, 1.22f, 7.2f, DemoArt.Gold)
        };

        internal enum Shape { Arc, Circle, Lane, Ring }

        readonly struct Zone
        {
            public readonly Shape Shape;
            public readonly float Size, Detail, Angle;
            public readonly bool AtTarget;
            public readonly Vector2 Offset;

            public Zone(Shape shape, float size, float detail = 0, bool atTarget = false,
                Vector2 offset = default, float angle = 0)
            {
                Shape = shape; Size = size; Detail = detail; AtTarget = atTarget; Offset = offset; Angle = angle;
            }
        }

        readonly struct Skill
        {
            public readonly string Name;
            public readonly float Range, Windup, Damage, FollowupWindup;
            public readonly Zone[][] Waves;

            public Skill(string name, float range, float windup, float damage, params Zone[][] waves)
                : this(name, range, windup, damage, 0, waves) { }

            public Skill(string name, float range, float windup, float damage, float followupWindup, params Zone[][] waves)
            {
                Name = name; Range = range; Windup = windup; Damage = damage;
                FollowupWindup = followupWindup; Waves = waves;
            }
        }

        // Distances are relative to the challenger's tuned attack range.
        static readonly Skill[][] Skills =
        {
            new[] {
                new Skill("碎岩拳", 1, 1, 1, new[] { new Zone(Shape.Arc, 1, 115) }),
                new Skill("崩山踏", 1.12f, 1.36f, 1.17f, new[] { new Zone(Shape.Circle, 1.15f) }),
                new Skill("撼岳双震", 1.35f, 2.15f, 1.5f, 1.17f,
                    new[] { new Zone(Shape.Circle, 1.05f) }, new[] { new Zone(Shape.Ring, 1.8f, 1.05f) })
            },
            new[] {
                new Skill("回风斩", 1, 1, 1, new[] { new Zone(Shape.Arc, 1, 95) }),
                new Skill("穿云剑", 1.5f, 1.22f, 1.24f, new[] { new Zone(Shape.Lane, 1.55f, .15f) }),
                new Skill("十字剑阵", 1.28f, 2.10f, 1.76f, new[] {
                    new Zone(Shape.Lane, 2, .17f, true), new Zone(Shape.Lane, 2, .17f, true, angle: 90)
                })
            },
            new[] {
                new Skill("落星诀", 1, 1, 1, new[] { new Zone(Shape.Circle, .37f, atTarget: true) }),
                new Skill("玄霜带", 1.2f, 1.20f, 1.18f, new[] { new Zone(Shape.Lane, 1.25f, .27f) }),
                new Skill("三星连落", 1.18f, 1.78f, 1.28f, 1.20f,
                    new[] { new Zone(Shape.Circle, .48f, atTarget: true) },
                    new[] { new Zone(Shape.Circle, .45f, atTarget: true, offset: new Vector2(.47f, .3f)) },
                    new[] { new Zone(Shape.Circle, .55f, atTarget: true, offset: new Vector2(.13f, -.44f)) })
            },
            new[] {
                new Skill("爆炎符", 1, 1, 1, new[] { new Zone(Shape.Circle, .29f, atTarget: true) }),
                new Skill("双生符", 1.08f, 1.25f, 1.30f, new[] {
                    new Zone(Shape.Circle, .34f, atTarget: true, offset: new Vector2(0, -.24f)),
                    new Zone(Shape.Circle, .34f, atTarget: true, offset: new Vector2(0, .24f))
                }),
                new Skill("四门封阵", 1.2f, 2.12f, 1.98f, new[] {
                    new Zone(Shape.Circle, .46f, atTarget: true, offset: new Vector2(.35f, 0)),
                    new Zone(Shape.Circle, .46f, atTarget: true, offset: new Vector2(-.35f, 0)),
                    new Zone(Shape.Circle, .46f, atTarget: true, offset: new Vector2(0, .35f)),
                    new Zone(Shape.Circle, .46f, atTarget: true, offset: new Vector2(0, -.35f))
                })
            }
        };
        static readonly Vector2[] SpawnPixels =
        {
            new Vector2(755, 350), new Vector2(595, 475), new Vector2(945, 475), new Vector2(755, 610)
        };

        // Hip, knee, ankle and sole positions in each portrait, in units of sprite height.
        static readonly HeroGaitMath.Rig[] WalkRigs =
        {
            new HeroGaitMath.Rig(
                Leg(-.065f, .49f, -.085f, .29f, -.140f, .085f, -.145f, -.003f),
                Leg(.040f, .49f, .066f, .29f, .073f, .093f, .104f, .024f), false),
            new HeroGaitMath.Rig(
                Leg(.008f, .47f, -.064f, .26f, -.103f, .067f, -.107f, -.003f),
                Leg(.091f, .48f, .094f, .25f, .103f, .087f, .123f, .020f), false),
            new HeroGaitMath.Rig(
                Leg(-.066f, .47f, -.110f, .26f, -.137f, .053f, -.137f, -.003f),
                Leg(.018f, .48f, .045f, .26f, .070f, .063f, .093f, .014f), false),
            new HeroGaitMath.Rig(
                Leg(-.039f, .46f, -.091f, .255f, -.130f, .067f, -.130f, -.003f),
                Leg(.055f, .47f, .054f, .26f, .069f, .091f, .088f, .029f), false)
        };

        static HeroGaitMath.Leg Leg(float hipX, float hipY, float kneeX, float kneeY,
            float ankleX, float ankleY, float soleX, float soleY)
            => new HeroGaitMath.Leg(new Vector2(hipX, hipY), new Vector2(kneeX, kneeY),
                new Vector2(ankleX, ankleY), new Vector2(soleX, soleY));

        static readonly HeroWalkMesh.PaintedGesture[] Gestures =
        {
            new HeroWalkMesh.PaintedGesture(new Vector2(.122f, .786f), new Vector2(.158f, .731f), .04f, .164f),
            new HeroWalkMesh.PaintedGesture(new Vector2(-.127f, .493f), new Vector2(-.309f, .205f), .033f, .19f),
            new HeroWalkMesh.PaintedGesture(new Vector2(.185f, .693f), new Vector2(.182f, .734f), .05f, .165f),
            new HeroWalkMesh.PaintedGesture(new Vector2(.139f, .775f), new Vector2(.148f, .853f), .032f, .158f)
        };

        readonly struct CastPose
        {
            public readonly Vector2 Torso, Hand;
            public readonly float Angle;

            public CastPose(float x, float y, float handX, float handY, float angle = 0)
            {
                Torso = new Vector2(x, y); Hand = new Vector2(handX, handY); Angle = angle;
            }

            public void Apply(HeroWalkMesh mesh, CastPose to, float progress)
            {
                float t = Mathf.SmoothStep(0, 1, progress);
                mesh.SetPaintedCastPose(Vector2.Lerp(Torso, to.Torso, t), Vector2.Lerp(Hand, to.Hand, t),
                    Mathf.Lerp(Angle, to.Angle, t));
            }
        }

        readonly struct CastMotion
        {
            public readonly CastPose Ready, Contact;
            public CastMotion(CastPose ready, CastPose contact) { Ready = ready; Contact = contact; }
        }

        static readonly CastMotion[,] CastMotions =
        {
            {
                new CastMotion(new CastPose(-.035f, -.045f, -.025f, .025f), new CastPose(.064f, -.013f, .056f, -.016f)),
                new CastMotion(new CastPose(-.012f, .031f, -.012f, .039f), new CastPose(.027f, -.063f, .028f, -.034f)),
                new CastMotion(new CastPose(-.035f, .049f, -.016f, .051f), new CastPose(.037f, -.064f, .031f, -.032f))
            },
            {
                new CastMotion(new CastPose(-.028f, -.019f, -.018f, .039f, -9), new CastPose(.051f, -.025f, .028f, -.018f, 9)),
                new CastMotion(new CastPose(-.033f, -.023f, -.026f, .02f, -6), new CastPose(.066f, .004f, .043f, .022f, 5)),
                new CastMotion(new CastPose(-.041f, .023f, -.021f, .036f, -10), new CastPose(.051f, -.042f, .030f, -.013f, 9))
            },
            {
                new CastMotion(new CastPose(-.012f, .029f, -.015f, .042f, -4), new CastPose(.031f, -.013f, .026f, -.03f, 5)),
                new CastMotion(new CastPose(-.022f, .021f, -.028f, .033f, -6), new CastPose(.052f, -.02f, .04f, .011f, 6)),
                new CastMotion(new CastPose(-.018f, .042f, -.018f, .051f, -5), new CastPose(.039f, -.026f, .031f, -.044f, 6))
            },
            {
                new CastMotion(new CastPose(-.024f, -.014f, -.024f, .024f, -7), new CastPose(.041f, .008f, .046f, .002f, 7)),
                new CastMotion(new CastPose(-.019f, .022f, -.022f, .038f, -7), new CastPose(.034f, -.016f, .038f, -.026f, 9)),
                new CastMotion(new CastPose(-.027f, .036f, -.024f, .048f, -8), new CastPose(.044f, -.023f, .045f, -.037f, 9))
            }
        };

        const float VisualScale = 1.1f;
        const float Height = HeroAppearance.BodyHeight * VisualScale;
        const float SkillRangeScale = 1.5f;
        const float BodyRadius = .36f, PlayerRadius = .29f;
        const float RecoveryPause = .25f;
        const int Ultimate = 2;
        enum Phase { Pursuit, Prepare, Windup, Recover }
        Phase phase;
        Profile profile;
        float strength, baseDamage, age, cooldown, castRange, prepareTime, castWindup;
        int nextSmall, smallCasts, skillIndex, wave, strafeSide = 1;
        Vector2 aim, target, origin, previousGround;
        Skill skill;
        CastMotion castMotion;
        SlashEnemy enemy;
        SlashArena arena;
        ObliqueActor actor;
        Sprite art;
        SpriteRenderer skin;
        HeroWalkMesh walk;
        AscensionSkillEffects skillEffects;
        Transform warning;
        Rigidbody2D body, playerBody;
        readonly LineRenderer[] outlines = new LineRenderer[4];
        readonly Vector3[] circlePoints = new Vector3[64];
        LineRenderer expandingOutline;
        Vector2 expandingCenter;
        float expandingRadius;
        public Path CultivationPath { get; private set; }
        public bool IsChasing => phase == Phase.Pursuit;
        public string CastingSkill => phase == Phase.Windup ? skill.Name : null;
        public bool CastingUltimate => phase == Phase.Windup && skillIndex == Ultimate;
        public float DamageReduction => profile.DamageReduction;
        public WildernessNavigation Navigation { get; private set; }
        public static int PathCount => Profiles.Length;
        bool IsMelee => CultivationPath == Path.Body || CultivationPath == Path.Sword;

        internal static Texture2D LoadArt(Path path)
        {
            string resource = "SlashDemo/Cultivators/" + Profiles[(int)path].Resource;
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) throw new System.InvalidOperationException("Missing ascension cultivator art: " + resource);
            return texture;
        }

        internal static SlashEnemy Spawn(SlashArena arena, TravelMapEnvironment map, Path path, Texture2D texture, float strength)
        {
            var profile = Profiles[(int)path];
            Vector2 player = arena.Player.transform.position;
            Vector2 position = map.Definition.Ground(SpawnPixels[0].x, SpawnPixels[0].y);
            foreach (var pixel in SpawnPixels)
            {
                Vector2 candidate = map.Definition.Ground(pixel.x, pixel.y);
                if ((candidate - player).sqrMagnitude > (position - player).sqrMagnitude) position = candidate;
            }

            var go = new GameObject(profile.Name);
            go.SetActive(false);
            go.transform.SetParent(map.transform, false);
            go.transform.position = position;
            var body = DemoArt.Body(go, BodyRadius, 1);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            var cultivator = go.AddComponent<AscensionCultivator>();
            cultivator.CultivationPath = path;
            cultivator.profile = profile;
            cultivator.strength = strength;
            cultivator.body = body;
            cultivator.Navigation = go.AddComponent<WildernessNavigation>();
            cultivator.Navigation.Initialize(map, body, BodyRadius);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            DemoArt.Shape("Shadow", visual, "Disk", Vector2.zero, Vector2.one, new Color(0, 0, 0, .32f));
            cultivator.art = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .013f), texture.height, 0, SpriteMeshType.FullRect);
            cultivator.skin = new GameObject("Cultivator art").AddComponent<SpriteRenderer>();
            cultivator.skin.transform.SetParent(visual, false);
            cultivator.skin.transform.localScale = Vector3.one * Height;
            cultivator.skin.sprite = cultivator.art;
            cultivator.skin.sharedMaterial = DemoArt.Material;
            cultivator.walk = cultivator.skin.gameObject.AddComponent<HeroWalkMesh>();
            cultivator.walk.Initialize(cultivator.skin, WalkRigs[(int)path], Gestures[(int)path]);
            DemoArt.Shape("HealthBack", go.transform, "Square", Vector2.zero, Vector2.one, new Color(.035f, .055f, .065f), 500);
            DemoArt.Shape("HealthFill", go.transform, "Square", Vector2.zero, Vector2.one, DemoArt.Red, 501);
            var warning = new GameObject("AttackWarning").transform;
            warning.SetParent(go.transform, false);
            for (int i = 0; i < cultivator.outlines.Length; i++)
            {
                var line = DemoArt.Line("Skill outline", warning, new Vector3[64],
                    DemoArt.AttackWarningColor, .045f * DemoArt.AttackWarningWidthScale, 10, true);
                line.useWorldSpace = true;
                cultivator.outlines[i] = line;
            }
            warning.gameObject.SetActive(false);

            var enemy = go.AddComponent<SlashEnemy>();
            cultivator.enemy = enemy;
            var settings = arena.Tuning?.Settings ?? new SlashTuningSettings();
            enemy.maxHealth = cultivator.TunedMaxHealth(settings);
            enemy.moveSpeed = profile.Speed;
            cultivator.ApplyAttackTuning(settings);
            var actor = go.AddComponent<ObliqueActor>();
            actor.CreatureHeight = Height;
            actor.ShadowScale = .52f * VisualScale;
            go.SetActive(true);
            if (arena.Tuning != null) arena.Tuning.ApplyEnemy(enemy);
            arena.RegisterEnemy(enemy);
            return enemy;
        }

        internal void InitializeCombat(SlashArena world, ObliqueActor presentation, Transform groundWarning)
        {
            arena = world; actor = presentation; warning = groundWarning;
            skillEffects = new AscensionSkillEffects(world, transform, CultivationPath, profile.ImpactColor);
            playerBody = world.Player.GetComponent<Rigidbody2D>();
            previousGround = transform.position;
        }

        internal void ApplyAttackTuning(SlashTuningSettings settings)
        {
            baseDamage = settings.enemyDamage * profile.Damage;
            enemy.damage = ScaleRoundValue(baseDamage);
            enemy.windupTime = settings.enemyWindup * profile.Windup;
            enemy.attackRange = settings.enemyRange * profile.Reach * SkillRangeScale;
        }

        internal int TunedMaxHealth(SlashTuningSettings settings)
            => ScaleRoundValue(profile.Health * settings.enemyHealthMultiplier);

        int ScaleRoundValue(float value)
        {
            int firstRoundValue = Mathf.Max(1, Mathf.RoundToInt(value));
            return Mathf.Max(1, Mathf.RoundToInt(firstRoundValue * strength));
        }

        internal void Tick(float dt)
        {
            if (dt <= 0 || arena.Paused) return;
            if (arena.Player == null || !arena.Player.IsAlive) { Stop(); return; }
            skillEffects.Advance(dt);
            age += dt;
            cooldown = Mathf.Max(0, cooldown - dt);
            Vector2 ground = transform.position;
            Vector2 travel = ground - previousGround;
            previousGround = ground;
            Vector2 delta = (Vector2)arena.Player.transform.position - ground;
            switch (phase)
            {
                case Phase.Pursuit:
                    if (!TryBeginAttack(delta)) AnimateStep(travel, dt, travel);
                    break;
                case Phase.Prepare:
                    AnimateStep(travel, dt, delta);
                    default(CastPose).Apply(walk, castMotion.Ready, age / prepareTime);
                    if (age >= prepareTime) LockAttack();
                    break;
                case Phase.Windup:
                    float progress = Mathf.Clamp01(age / castWindup);
                    AnimateCast(progress);
                    skillEffects.Charge(dt, progress);
                    if (expandingOutline != null) DrawExpandingOutline(progress);
                    if (progress >= 1) Strike();
                    break;
                case Phase.Recover:
                    float recovery = skillIndex == Ultimate ? profile.UltimateRecovery : profile.SmallRecovery;
                    AnimateStep(travel, dt, delta);
                    castMotion.Contact.Apply(walk, default, age / (recovery * .45f));
                    if (age >= recovery) { phase = Phase.Pursuit; age = 0; }
                    break;
            }
        }

        void AnimateStep(Vector2 travel, float dt, Vector2 facing)
        {
            if (Mathf.Abs(facing.x) > .01f) skin.flipX = facing.x < 0;
            walk.Advance(travel, dt, false, arena.View);
            actor.SetPose(Vector2.zero, walk.Bob, 0);
        }

        internal void FixedTick(float dt)
        {
            Vector2 velocity = Vector2.zero;
            if (arena.Player.IsAlive)
            {
                Vector2 delta = playerBody.position - body.position;
                switch (phase)
                {
                    case Phase.Pursuit:
                        float retreat = Vector2.Dot(PlayerVelocity(), delta.normalized);
                        float boost = CultivationPath == Path.Body ? 1.85f : IsMelee ? 1.42f : 1.25f;
                        float speed = enemy.moveSpeed * Mathf.Lerp(1, boost, Mathf.InverseLerp(.4f, arena.Player.moveSpeed * .75f, retreat));
                        velocity = Navigation.ChaseDirection(body.position, PredictPlayer(.22f)) * speed;
                        break;
                    case Phase.Prepare:
                        velocity = IsMelee ? CloseIn(castRange, 1.65f, dt) : Strafe(delta, .55f);
                        break;
                    case Phase.Recover when age >= RecoveryPause:
                        velocity = IsMelee ? CloseIn(enemy.attackRange, 1, dt) : Strafe(delta, .9f);
                        break;
                }
            }
            body.linearVelocity = Navigation.ConstrainVelocity(velocity, dt);
        }

        Vector2 CloseIn(float range, float speedScale, float dt)
        {
            Vector2 point = PredictPlayer(.22f);
            float gap = Vector2.Distance(body.position, point) - Mathf.Max(.75f, range * .48f);
            if (gap <= 0) return Vector2.zero;
            return Navigation.ChaseDirection(body.position, point) * Mathf.Min(enemy.moveSpeed * speedScale, gap / dt);
        }

        Vector2 Strafe(Vector2 delta, float speedScale)
        {
            Vector2 toward = delta.normalized;
            Vector2 side = new Vector2(-toward.y, toward.x) * strafeSide;
            float spacing = Mathf.Clamp((delta.magnitude - enemy.attackRange * .72f) / enemy.attackRange, -.85f, .6f);
            Vector2 heading = (side + toward * spacing).normalized;
            if (!Navigation.Clear(body.position, body.position + heading * .65f))
            {
                Vector2 reverse = (-side + toward * spacing).normalized;
                if (Navigation.Clear(body.position, body.position + reverse * .65f))
                {
                    strafeSide = -strafeSide;
                    heading = reverse;
                }
            }
            return heading * (enemy.moveSpeed * speedScale);
        }

        Vector2 PlayerVelocity()
            => arena.Player.IsDashing || arena.Player.IsTechniqueActive ? Vector2.zero
                : Vector2.ClampMagnitude(playerBody.linearVelocity, arena.Player.moveSpeed);

        Vector2 PredictPlayer(float seconds)
        {
            Vector2 player = playerBody.position;
            Vector2 predicted = player + PlayerVelocity() * seconds;
            return predicted == player || Navigation.Walkable(predicted) ? predicted : player;
        }

        void AnimateCast(float progress)
        {
            const float Release = .74f;
            if (progress < Release)
            {
                CastPose start = wave > 0 ? castMotion.Contact : prepareTime > 0 ? castMotion.Ready : default;
                start.Apply(walk, castMotion.Ready, progress / Release);
            }
            else castMotion.Ready.Apply(walk, castMotion.Contact, (progress - Release) / (1 - Release));
        }

        bool TryBeginAttack(Vector2 delta)
        {
            var skills = Skills[(int)CultivationPath];
            float distance = delta.magnitude;
            float openingRange = enemy.attackRange * (IsMelee ? .82f : .94f);
            bool canUseUltimate = smallCasts >= 2 && cooldown <= 0 && distance <= openingRange * skills[Ultimate].Range;
            int index = canUseUltimate ? Ultimate : nextSmall;
            if (distance > openingRange * skills[index].Range) return false;
            BeginAttack(index);
            return true;
        }

        void BeginAttack(int index)
        {
            skillIndex = index;
            skill = Skills[(int)CultivationPath][index];
            castMotion = CastMotions[(int)CultivationPath, index];
            if (index == Ultimate) { smallCasts = 0; cooldown = profile.UltimateCooldown; }
            else { nextSmall = 1 - nextSmall; smallCasts++; }
            phase = Phase.Prepare;
            age = 0;
            wave = 0;
            castRange = enemy.attackRange;
            castWindup = enemy.windupTime * skill.Windup;
            // Spend part of the existing windup moving, with a readable window after the aim locks.
            prepareTime = Mathf.Clamp(castWindup * .42f, 0, Mathf.Max(0, castWindup - .32f));
            castWindup -= prepareTime;
            strafeSide = -strafeSide;
            if (prepareTime <= 0) LockAttack();
        }

        void LockAttack()
        {
            phase = Phase.Windup;
            age = 0;
            body.linearVelocity = Vector2.zero;
            walk.ResetWalk();
            actor.SetPose(Vector2.zero, 0, 0);
            origin = body.position;
            AimAttack();
            ShowWarning();
        }

        void AimAttack()
        {
            target = PredictPlayer(Mathf.Min(castWindup * .85f, .48f));
            Vector2 delta = target - origin;
            if (Mathf.Abs(delta.x) > .1f) skin.flipX = delta.x < 0;
            aim = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.right;
        }

        void Place(Zone zone, out Vector2 center, out Vector2 direction)
        {
            Vector2 side = new Vector2(-aim.y, aim.x);
            center = (zone.AtTarget ? target : origin) + castRange * (aim * zone.Offset.x + side * zone.Offset.y);
            Vector2 rotation = DemoArt.Direction(zone.Angle);
            direction = aim * rotation.x + side * rotation.y;
            if (zone.Shape == Shape.Lane && zone.AtTarget) center -= direction * (zone.Size * castRange * .5f);
        }

        void ShowWarning()
        {
            int count = 0;
            expandingOutline = null;
            foreach (var zone in skill.Waves[wave])
            {
                Place(zone, out Vector2 center, out Vector2 direction);
                float size = zone.Size * castRange;
                var line = outlines[count++];
                switch (zone.Shape)
                {
                    case Shape.Arc:
                        line.positionCount = 25;
                        line.SetPosition(0, GroundPoint(center));
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        for (int i = 1; i < line.positionCount; i++)
                        {
                            float edge = Mathf.Lerp(-zone.Detail * .5f, zone.Detail * .5f, (i - 1) / 23f);
                            line.SetPosition(i, GroundPoint(center + DemoArt.Direction(angle + edge) * size));
                        }
                        break;
                    case Shape.Lane:
                        line.positionCount = 4;
                        Vector2 side = new Vector2(-direction.y, direction.x) * (zone.Detail * castRange);
                        Vector2 end = center + direction * size;
                        line.SetPosition(0, GroundPoint(center - side));
                        line.SetPosition(1, GroundPoint(center + side));
                        line.SetPosition(2, GroundPoint(end + side));
                        line.SetPosition(3, GroundPoint(end - side));
                        break;
                    case Shape.Circle:
                        DrawCircle(line, center, size);
                        if (CastingUltimate && CultivationPath == Path.Spell)
                        {
                            expandingOutline = outlines[count++];
                            expandingCenter = center;
                            expandingRadius = size;
                            DrawExpandingOutline(0);
                        }
                        break;
                    case Shape.Ring:
                        DrawCircle(line, center, size);
                        DrawCircle(outlines[count++], center, zone.Detail * castRange);
                        break;
                }
            }
            for (int i = 0; i < outlines.Length; i++)
            {
                outlines[i].enabled = i < count;
                outlines[i].startWidth = outlines[i].endWidth = (CastingUltimate ? .08f : .045f) * DemoArt.AttackWarningWidthScale;
                outlines[i].startColor = outlines[i].endColor = DemoArt.AttackWarningColor;
            }
            if (expandingOutline != null)
                expandingOutline.startColor = expandingOutline.endColor = new Color(1, .32f, .23f, .92f);
            warning.gameObject.SetActive(true);
            Place(skill.Waves[wave][0], out Vector2 focus, out _);
            skillEffects.BeginCharge(origin, focus, aim, CastingUltimate);
        }

        static Vector3 GroundPoint(Vector2 point) => new Vector3(point.x, point.y, -.025f);

        void DrawCircle(LineRenderer line, Vector2 center, float radius)
        {
            line.positionCount = circlePoints.Length;
            for (int i = 0; i < circlePoints.Length; i++)
                circlePoints[i] = GroundPoint(center + DemoArt.Direction(i * 360f / circlePoints.Length) * radius);
            line.SetPositions(circlePoints);
        }

        void DrawExpandingOutline(float progress)
            => DrawCircle(expandingOutline, expandingCenter, expandingRadius * Mathf.Lerp(.08f, 1, progress));

        bool Contains(Zone zone, Vector2 center, Vector2 direction, Vector2 player)
        {
            float size = zone.Size * castRange;
            if (zone.Shape == Shape.Arc)
                return SlashPlayer.InsideArc(center, direction, player, size, zone.Detail, PlayerRadius);
            Vector2 offset = player - center;
            if (zone.Shape == Shape.Lane)
            {
                float along = Vector2.Dot(offset, direction);
                float across = Vector2.Dot(offset, new Vector2(-direction.y, direction.x));
                float halfWidth = zone.Detail * castRange;
                Vector2 closest = new Vector2(Mathf.Clamp(along, 0, size), Mathf.Clamp(across, -halfWidth, halfWidth));
                return (new Vector2(along, across) - closest).sqrMagnitude <= PlayerRadius * PlayerRadius;
            }
            float distance = offset.magnitude;
            return distance <= size + PlayerRadius
                && (zone.Shape != Shape.Ring || distance + PlayerRadius >= zone.Detail * castRange);
        }

        void Strike()
        {
            warning.gameObject.SetActive(false);
            expandingOutline = null;
            skillEffects.EndCharge();
            Vector2 player = arena.Player.transform.position;
            bool hit = false;
            foreach (var zone in skill.Waves[wave])
            {
                Place(zone, out Vector2 center, out Vector2 direction);
                hit |= Contains(zone, center, direction, player);
                float detail = zone.Shape == Shape.Arc ? zone.Detail : zone.Detail * castRange;
                skillEffects.Impact(zone.Shape, center, direction, zone.Size * castRange, detail, CastingUltimate);
            }
            arena.Effects.PlaySwing(CastingUltimate);
            if (hit) arena.Player.TakeChallengeDamage(ScaleRoundValue(baseDamage * skill.Damage), aim, enemy);
            if (!isActiveAndEnabled) return;
            age = 0;
            if (++wave < skill.Waves.Length)
            {
                castWindup = Mathf.Max(.55f, enemy.windupTime * skill.FollowupWindup);
                if (CultivationPath == Path.Spell) AimAttack();
                ShowWarning();
            }
            else phase = Phase.Recover;
        }

        internal void Stop()
        {
            phase = Phase.Pursuit;
            age = 0;
            nextSmall = smallCasts = 0;
            strafeSide = 1;
            cooldown = 0;
            expandingOutline = null;
            if (body != null) body.linearVelocity = Vector2.zero;
            previousGround = transform.position;
            if (walk != null)
            {
                walk.ResetWalk();
                walk.SetPaintedCastPose(Vector2.zero, Vector2.zero, 0);
            }
            if (warning != null) warning.gameObject.SetActive(false);
            skillEffects?.Clear();
        }

        void OnDisable() { Stop(); }
        void OnDestroy()
        {
            skillEffects?.Dispose();
            if (art != null) Destroy(art);
        }
    }
}
