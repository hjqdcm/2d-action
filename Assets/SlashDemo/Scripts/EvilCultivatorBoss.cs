using System;
using UnityEngine;

namespace SlashDemo
{
    [DisallowMultipleComponent]
    public sealed class EvilCultivatorBoss : MonoBehaviour
    {
        public enum Style { BloodSaber, SoulBell }
        enum Phase { Pursuit, BloodMist, Windup, Recovery }
        enum BloodSaberStage { Opening, BloodFrenzy, FiveBlades, ArenaMist }

        const float BodyRadius = .43f, Height = 2.25f;
        const int AtlasColumns = 4, AtlasRows = 2;
        // The packed atlas uses 512px cells with 448px of art height and a 32px foot margin.
        const float ArtHeightRatio = 448f / 512f;
        static readonly Vector2 ArtPivot = new Vector2(.5f, 32f / 512f);
        const int OutlinesPerWave = 5, CurveSegments = 40;
        const int LifestealMultiplier = 10;
        const float BloodSaberDamageScale = .35f;
        const int BloodFrenzyHealthPercent = 10;
        const float BloodFrenzyDuration = 20, BloodFrenzyCooldown = 20;
        const float BloodFrenzyDamage = 1.20f, BloodFrenzyAttackSpeed = 1.25f, BloodFrenzyMoveSpeed = 1.10f;
        const string BloodFrenzyName = "血祭";
        static readonly string BloodFrenzyDescription =
            $"消耗当前气血的 {BloodFrenzyHealthPercent}%，在 {BloodFrenzyDuration:0} 秒内令伤害提高 {BloodFrenzyDamage - 1:P0}、攻速提高 {BloodFrenzyAttackSpeed - 1:P0}、移速提高 {BloodFrenzyMoveSpeed - 1:P0}。冷却 {BloodFrenzyCooldown:0} 秒。";
        static readonly CombatBuffView LifestealBuff = new CombatBuffView("blood-saber-lifesteal", "吸血", 0,
            description: $"常驻。对玩家造成伤害后，恢复实际扣除气血量的 {LifestealMultiplier} 倍，最多恢复到气血上限。");

        internal enum Shape { Arc, Lane, Circle, Ring }
        enum ImpactStyle { Standard, Seal, BloodCut, BloodTide, BloodFrenzy, FlyingCrescents, ArenaMistTransition }

        readonly struct Zone
        {
            public readonly Shape Shape;
            public readonly float Size, Detail, Angle;
            public readonly bool AtTarget;
            public readonly Vector2 Offset;

            public Zone(Shape shape, float size, float detail = 0, float angle = 0,
                bool atTarget = false, Vector2 offset = default)
            {
                Shape = shape; Size = size; Detail = detail; Angle = angle;
                AtTarget = atTarget; Offset = offset;
            }
        }

        readonly struct Wave
        {
            public readonly float Windup, Damage;
            public readonly bool Retarget;
            public readonly ImpactStyle Impact;
            public readonly AttackMotion Motion;
            public readonly Zone[] Zones;

            public Wave(float windup, float damage, bool retarget, AttackMotion motion, params Zone[] zones)
                : this(windup, damage, retarget, motion, ImpactStyle.Standard, zones) { }

            public Wave(float windup, float damage, bool retarget, AttackMotion motion, ImpactStyle impact, params Zone[] zones)
            {
                Windup = windup; Damage = damage; Retarget = retarget; Motion = motion; Impact = impact; Zones = zones;
            }
        }

        readonly struct Skill
        {
            public readonly string Name;
            public readonly float Range, Recovery, Cooldown;
            public readonly Wave[] Waves;

            public Skill(string name, float range, float recovery, float cooldown, params Wave[] waves)
            {
                Name = name; Range = range; Recovery = recovery; Cooldown = cooldown; Waves = waves;
            }
        }

        // Brace moves the torso vertically over the planted feet. The hand reaches independently.
        readonly struct AttackPose
        {
            public readonly float Brace, Out, Lift, Reach;

            public AttackPose(float brace, float handOut, float handLift, float reach) : this()
            {
                Brace = brace; Out = handOut; Lift = handLift; Reach = reach;
            }
        }

        readonly struct AttackMotion
        {
            public readonly AttackPose Ready, Contact, Follow;
            public readonly float Release, FollowTime;
            public readonly Vector2 SwingArc;
            public readonly int SweepDirection;

            public AttackMotion(float release, float followTime, Vector2 swingArc,
                AttackPose ready, AttackPose contact, AttackPose follow) : this()
            {
                Release = release; FollowTime = followTime; SwingArc = swingArc;
                Ready = ready; Contact = contact; Follow = follow;
            }

            public AttackMotion(float release, float followTime, int sweepDirection = 0) : this()
            {
                Release = release; FollowTime = followTime;
                SweepDirection = sweepDirection;
            }
        }

        static readonly AttackMotion DownwardCut = new AttackMotion(.27f, .19f);
        static readonly AttackMotion HorizontalCut = new AttackMotion(.32f, .19f, sweepDirection: 1);
        static readonly AttackMotion GroundStab = new AttackMotion(.42f, .19f);
        static readonly AttackMotion FlyingBladeCast = new AttackMotion(.36f, .22f);
        const float FlyingBladeExtensionEnd = .85f;
        const float GroundStabWindup = 1.45f, GroundStabRecovery = .68f;
        const float ArenaMistPoseBlendTime = .20f;
        const float SaberStepTurnTime = .10f;
        static readonly Skill ArenaMistTransition = new Skill("血雾升腾", 0, GroundStabRecovery, 0,
            new Wave(GroundStabWindup, 0, true, GroundStab, ImpactStyle.ArenaMistTransition));
        static readonly Skill BloodMistCut = new Skill("血雾突袭", 4, .50f, 7,
            new Wave(1, 1.10f, true, HorizontalCut, new Zone(Shape.Arc, .90f, 105)));
        static readonly float[] BloodMistLandingAngles = { 0, 35, -35, 65, -65 };
        const float BloodMistOpeningDelay = 5;
        const float BloodMistWindup = .42f;
        const float BloodMistSlashWindup = .40f;
        const float BloodTideMistDuration = 8;
        const int BloodTideMistDamage = 2;
        const float SaberAnimationSpeed = 1.5f;
        const float SaberReadyAngle = 42;
        const float BloodFrenzyReadyAngle = 60;
        const float SaberSweepFollowAngle = 14;
        const float SaberFinishAngle = -32;

        static readonly AttackMotion BellShake = new AttackMotion(.19f, .15f, new Vector2(.020f, .005f),
            new AttackPose(.005f, .016f, .077f, -.006f),
            new AttackPose(.003f, .020f, -.025f, .005f),
            new AttackPose(.004f, .024f, .012f, 0));
        static readonly AttackMotion BellCross = new AttackMotion(.22f, .16f, new Vector2(0, .039f),
            new AttackPose(-.004f, -.063f, .038f, -.012f),
            new AttackPose(.003f, .067f, .032f, .015f),
            new AttackPose(.003f, .087f, .023f, .010f));
        static readonly AttackMotion BellReturn = new AttackMotion(.17f, .13f, new Vector2(.032f, -.019f),
            new AttackPose(.001f, .050f, -.021f, -.005f),
            new AttackPose(.004f, -.036f, .067f, .012f),
            new AttackPose(.003f, -.043f, .078f, .005f));
        static readonly AttackMotion BellResonance = new AttackMotion(.21f, .17f, new Vector2(.005f, .033f),
            new AttackPose(.008f, -.016f, .117f, -.004f),
            new AttackPose(.009f, .069f, .085f, .012f),
            new AttackPose(.007f, .073f, .073f, .006f));
        static readonly AttackMotion BellOpen = new AttackMotion(.24f, .17f, new Vector2(.024f, .052f),
            new AttackPose(-.003f, -.065f, .012f, -.005f),
            new AttackPose(.009f, .082f, .072f, .010f),
            new AttackPose(.008f, .088f, .076f, .006f));
        static readonly AttackMotion BellSeal = new AttackMotion(.19f, .15f, new Vector2(.018f, -.021f),
            new AttackPose(.003f, .025f, .079f, -.003f),
            new AttackPose(-.008f, -.048f, -.055f, .013f),
            new AttackPose(-.009f, -.054f, -.062f, .008f));

        // Zone distances are in units of the tuned enemy attack range (2.875 m by default).
        static Skill FlyingBlades(params Zone[] lanes) => new Skill("三途飞刃", 2.32f, .66f, 5.4f,
            new Wave(1.42f, 1.36f, true, FlyingBladeCast, ImpactStyle.FlyingCrescents, lanes));

        static readonly Skill FiveBladeSkill = FlyingBlades(
            new Zone(Shape.Lane, 2.5f, .19f, -32), new Zone(Shape.Lane, 2.5f, .19f, -16),
            new Zone(Shape.Lane, 2.5f, .19f),
            new Zone(Shape.Lane, 2.5f, .19f, 16), new Zone(Shape.Lane, 2.5f, .19f, 32));

        static readonly Skill[] SaberSkills =
        {
            new Skill("饮血横斩", 1, .42f, 2.1f,
                new Wave(1.0f, 1, true, HorizontalCut, new Zone(Shape.Arc, 1.06f, 150))),
            new Skill("贯血刀", 1.8f, .53f, 3.4f,
                new Wave(1.16f, 1.18f, true, DownwardCut, ImpactStyle.BloodCut, new Zone(Shape.Lane, 1.96f, .22f))),
            new Skill(BloodFrenzyName, 1.24f, .42f, BloodFrenzyCooldown,
                new Wave(.94f, 0, true, DownwardCut, ImpactStyle.BloodFrenzy)),
            new Skill("裂地血潮", 1.24f, GroundStabRecovery, 6.3f,
                new Wave(GroundStabWindup, 1.04f, true, GroundStab, ImpactStyle.BloodTide, new Zone(Shape.Circle, 1.24f))),
            FlyingBlades(new Zone(Shape.Lane, 2.5f, .19f, -32),
                new Zone(Shape.Lane, 2.5f, .19f), new Zone(Shape.Lane, 2.5f, .19f, 32))
        };

        static readonly Skill[] BellSkills =
        {
            new Skill("摄魂铃震", 2.4f, .46f, 2.2f,
                new Wave(1.06f, 1, true, BellShake, new Zone(Shape.Circle, .6f, atTarget: true))),
            new Skill("十字断魄", 2.35f, .56f, 3.7f,
                new Wave(1.24f, 1.18f, true, BellCross, new Zone(Shape.Lane, 3.2f, .23f, atTarget: true),
                    new Zone(Shape.Lane, 3.2f, .23f, 90, atTarget: true))),
            new Skill("三叠追魂", 2.5f, .62f, 6.2f,
                new Wave(1.16f, .76f, true, BellShake, new Zone(Shape.Circle, .54f, atTarget: true)),
                new Wave(1.24f, .84f, true, BellReturn, new Zone(Shape.Circle, .59f, atTarget: true)),
                new Wave(1.32f, 1.02f, true, BellSeal, new Zone(Shape.Circle, .69f, atTarget: true))),
            new Skill("镇魂回响", 2.25f, .57f, 4.8f,
                new Wave(1.39f, 1.3f, true, BellResonance, new Zone(Shape.Ring, 1.56f, .61f, atTarget: true))),
            new Skill("四方锁魂阵", 2.3f, .72f, 6.8f,
                new Wave(1.42f, 1.05f, true, BellOpen, ImpactStyle.Seal,
                    new Zone(Shape.Circle, .63f, atTarget: true, offset: new Vector2(.87f, 0)),
                    new Zone(Shape.Circle, .63f, atTarget: true, offset: new Vector2(-.87f, 0)),
                    new Zone(Shape.Circle, .63f, atTarget: true, offset: new Vector2(0, .87f)),
                    new Zone(Shape.Circle, .63f, atTarget: true, offset: new Vector2(0, -.87f))),
                new Wave(1.37f, 1.36f, false, BellSeal, ImpactStyle.Seal, new Zone(Shape.Circle, .79f, atTarget: true)))
        };

        // Coordinates are read from the top-left of each atlas cell (S, SE, E, NE, N, NW, W, SW).
        readonly struct WeaponRig
        {
            public readonly HeroWalkMesh.PaintedGesture Gesture;
            public readonly Vector2 Tip;
            public readonly float Side;

            public WeaponRig(int x, int y, int tipX, int tipY, float solid, float falloff,
                int headX, int headY, int chestX, bool bell = false)
            {
                var grip = ArtPoint(x, y);
                Tip = ArtPoint(tipX, tipY);
                Gesture = new HeroWalkMesh.PaintedGesture(grip, Tip, solid, falloff,
                    ArtPoint(headX, headY), ArtPoint(chestX, 175));
                Side = Mathf.Sign(bell ? grip.x : Tip.x - grip.x);
            }

            public WeaponRig(int x, int y, int tipX, int tipY, int shoulderX, int shoulderY,
                int elbowX, int elbowY, int patchEdge, int patchBottom, bool behindBody, int facing,
                params int[] outline)
            {
                Vector2 grip = ArtPoint(x, y);
                Vector2 paintedTip = ArtPoint(tipX, tipY);
                Side = Mathf.Sign(paintedTip.x - grip.x);
                var arm = new PaintedSaberArm(ArtPoint(shoulderX, shoulderY), ArtPoint(elbowX, elbowY), grip, paintedTip,
                    Side, outline, patchEdge, patchBottom, behindBody, SaberSupportArms[facing]);
                Tip = arm.ExtendedTip;
                Gesture = new HeroWalkMesh.PaintedGesture(grip, Tip, 0, 0, Vector2.zero, Vector2.zero, arm);
            }

            static Vector2 ArtPoint(int x, int y) => new Vector2(x - 256, 480 - y) / 448;
        }

        static readonly PaintedSaberArm.SupportArm[] SaberSupportArms =
        {
            new PaintedSaberArm.SupportArm(306, 180, 318, 228, 321, 280, 1, 322,
                304, 176, 324, 178, 332, 208, 333, 241, 332, 287, 322, 303, 309, 295, 309, 251, 305, 217),
            new PaintedSaberArm.SupportArm(305, 183, 321, 229, 326, 281, 1, 329,
                301, 179, 322, 181, 333, 208, 340, 242, 342, 290, 331, 305, 317, 301, 314, 248, 308, 222),
            new PaintedSaberArm.SupportArm(268, 178, 273, 224, 284, 267, 1, 0),
            new PaintedSaberArm.SupportArm(258, 180, 253, 224, 260, 273, -1, 0),
            new PaintedSaberArm.SupportArm(208, 180, 199, 226, 204, 276, -1, 0),
            new PaintedSaberArm.SupportArm(211, 184, 196, 238, 185, 280, -1, 195,
                193, 188, 216, 181, 220, 211, 211, 246, 201, 286, 192, 303, 178, 303, 170, 288, 179, 244),
            new PaintedSaberArm.SupportArm(272, 182, 265, 234, 248, 283, -1, 225,
                256, 191, 280, 187, 281, 213, 271, 244, 261, 283, 254, 303, 242, 304, 233, 294, 239, 267, 247, 228),
            new PaintedSaberArm.SupportArm(300, 185, 309, 234, 305, 282, 1, 311,
                291, 180, 315, 181, 321, 214, 322, 252, 321, 287, 311, 306, 296, 300, 292, 285, 297, 243)
        };

        // Trace the sleeve, grip and blade in each cell. The two occluded views
        // supply their own joints and borrow the sleeve art from the east-facing frame.
        static readonly WeaponRig[] SaberWeapons =
        {
            new WeaponRig(190, 272, 112, 387, 206, 178, 193, 225, 199, 275, false, 0,
                189, 179, 209, 171, 224, 180, 212, 222, 207, 264, 205, 286, 193, 309,
                172, 355, 147, 385, 113, 395, 99, 389, 125, 360, 145, 328, 169, 278, 176, 247, 183, 211),
            new WeaponRig(193, 282, 120, 409, 218, 179, 199, 230, 201, 285, false, 1,
                200, 182, 221, 172, 234, 184, 217, 237, 212, 278, 211, 305, 180, 365,
                154, 401, 122, 418, 105, 414, 134, 381, 154, 342, 174, 290, 179, 265, 189, 218),
            new WeaponRig(255, 282, 332, 410, 252, 179, 251, 231, 299, 386, false, 2,
                PaintedSaberArm.EastArmOutline),
            new WeaponRig(308, 284, 386, 406, 280, 179, 304, 229, 317, 328, false, 3,
                278, 182, 296, 176, 310, 194, 322, 240, 324, 273, 331, 295, 354, 351,
                380, 394, 394, 408, 381, 412, 358, 398, 331, 363, 306, 313, 292, 293, 284, 264, 278, 223),
            new WeaponRig(336, 280, 390, 400, 307, 178, 334, 229, 340, 330, false, 4,
                315, 224, 339, 221, 349, 255, 352, 287, 360, 313, 376, 362, 397, 400,
                387, 407, 358, 387, 340, 345, 322, 308, 318, 278),
            new WeaponRig(326, 320, 375, 402, 299, 195, 323, 257, 354, 399, true, 5,
                337, 333, 348, 329, 356, 353, 369, 388, 381, 404, 371, 409, 347, 395, 336, 364),
            // Keep the far sleeve slightly shorter in projection than the east-facing near sleeve.
            new WeaponRig(230, 274, 173, 367, 253, 185, 246, 228, 215, 370, true, 6,
                215, 317, 236, 324, 228, 350, 215, 375, 195, 401, 164, 413, 152, 405, 174, 381, 190, 352),
            new WeaponRig(192, 281, 119, 401, 219, 179, 202, 233, 205, 285, false, 7,
                201, 182, 223, 172, 236, 181, 220, 223, 211, 267, 208, 296, 196, 327,
                171, 375, 139, 402, 120, 409, 106, 405, 131, 375, 150, 342, 174, 290, 179, 260, 189, 219)
        };
        static readonly WeaponRig[] BellWeapons =
        {
            new WeaponRig(167, 96, 162, 148, .052f, .22f, 255, 88, 254, bell: true),
            new WeaponRig(173, 116, 171, 168, .053f, .22f, 252, 101, 250, bell: true),
            new WeaponRig(286, 134, 293, 185, .051f, .22f, 250, 103, 245, bell: true),
            new WeaponRig(339, 126, 347, 174, .051f, .21f, 255, 104, 253, bell: true),
            new WeaponRig(326, 130, 337, 175, .052f, .21f, 253, 97, 253, bell: true),
            new WeaponRig(320, 129, 330, 174, .051f, .21f, 255, 100, 257, bell: true),
            new WeaponRig(207, 133, 199, 178, .050f, .22f, 272, 103, 272, bell: true),
            new WeaponRig(162, 123, 154, 171, .052f, .22f, 250, 101, 249, bell: true)
        };

        // Foot centers in artwork-height units, ordered S, SE, E, NE, N, NW, W, SW.
        // Side views and the long bell robes partly hide the far boot.
        static readonly FootPlacement[] SaberFeet =
        {
            new FootPlacement(-.073f, .073f), new FootPlacement(-.085f, .081f),
            new FootPlacement(-.032f, .036f, .025f, 0), new FootPlacement(-.096f, .073f),
            new FootPlacement(-.080f, .087f), new FootPlacement(-.087f, .079f),
            new FootPlacement(-.032f, .035f, 0, .025f), new FootPlacement(-.084f, .061f)
        };
        static readonly FootPlacement[] BellFeet =
        {
            new FootPlacement(-.025f, .024f), new FootPlacement(-.026f, .023f),
            new FootPlacement(-.058f, .032f, .025f, 0), new FootPlacement(-.043f, .045f),
            new FootPlacement(-.048f, .046f), new FootPlacement(-.051f, .038f),
            new FootPlacement(-.043f, .031f, 0, .025f), new FootPlacement(-.025f, .024f)
        };

        // Each row is the left and right edge of the painted shin at a height in atlas
        // pixels, relative to that frame's foot pivot. The crop leaves the robe and saber out.
        static readonly Vector3[] SaberEastLeg =
        {
            new Vector3(-29, 44, -8), new Vector3(-29, 44, 13), new Vector3(-26, 18, 30),
            new Vector3(-23, 10, 57), new Vector3(-28, 19, 88), new Vector3(-30, 26, 120),
            new Vector3(-27, 27, 146)
        };
        static readonly Vector3[] SaberWestLeg =
        {
            new Vector3(-43, 34, -8), new Vector3(-43, 34, 13), new Vector3(-18, 32, 30),
            new Vector3(-7, 31, 57), new Vector3(-13, 34, 88), new Vector3(-23, 37, 120),
            new Vector3(-27, 37, 146)
        };
        static readonly Vector3[] BellEastLeg =
        {
            new Vector3(-17, 31, -8), new Vector3(-17, 31, 13), new Vector3(-10, 21, 27),
            new Vector3(-8, 13, 44), new Vector3(-9, 12, 69), new Vector3(-12, 13, 100)
        };
        static readonly Vector3[] BellWestLeg =
        {
            new Vector3(-33, 19, -8), new Vector3(-33, 19, 13), new Vector3(-24, 19, 27),
            new Vector3(-15, 18, 44), new Vector3(-14, 17, 69), new Vector3(-16, 19, 100)
        };

        readonly struct FootPlacement
        {
            readonly Vector2 left, right;

            public FootPlacement(float leftX, float rightX, float leftY = 0, float rightY = 0)
            {
                left = new Vector2(leftX, leftY);
                right = new Vector2(rightX, rightY);
            }

            public HeroGaitMath.Rig Rig(int facing)
            {
                // Left and right are ordered on screen. Reverse phase at the back so that
                // the anatomical lead leg stays the same while turning.
                return new HeroGaitMath.Rig(Leg(left), Leg(right), facing >= 3 && facing <= 6)
                    { direction = facing, preserveBootShape = true };
            }

            static HeroGaitMath.Leg Leg(Vector2 foot)
            {
                return new HeroGaitMath.Leg(new Vector2(foot.x * .57f, .48f),
                    new Vector2(foot.x * .91f, .27f + foot.y),
                    foot + Vector2.up * .073f, foot);
            }
        }

        public Style CombatStyle { get; private set; }
        internal CombatBuffView? PassiveBuff => UsesBell ? null : LifestealBuff;
        internal CombatBuffView? FrenzyBuff => bloodFrenzyRemaining > 0
            ? new CombatBuffView("blood-saber-blood-frenzy", BloodFrenzyName, 0,
                description: BloodFrenzyDescription, timeLeft: bloodFrenzyRemaining, duration: BloodFrenzyDuration)
            : null;
        internal bool ShowsWorldHealth => UsesBell;
        internal bool IsChasing => phase == Phase.Pursuit;
        public string CastingSkill => phase is Phase.BloodMist or Phase.Windup ? skill.Name : null;
        internal bool IsTransitionInvulnerable => isActiveAndEnabled && (phase is Phase.Windup or Phase.Recovery)
            && skill.Waves[0].Impact == ImpactStyle.ArenaMistTransition;
        bool UsesBell => CombatStyle == Style.SoulBell;
        Skill[] Skills => UsesBell ? BellSkills : SaberSkills;
        Wave CurrentWave => skill.Waves[waveIndex];
        WeaponRig CurrentWeapon => (UsesBell ? BellWeapons : SaberWeapons)[facingIndex];
        float SaberFacingX => DemoArt.Direction(facingIndex * 45 - 90).x;
        float DamageMultiplier => bloodFrenzyRemaining > 0 ? BloodFrenzyDamage : 1;

        Phase phase;
        BloodSaberStage bloodSaberStage;
        BloodArenaMist arenaMist;
        SlashArena arena;
        SlashEnemy enemy;
        ObliqueActor actor;
        Rigidbody2D body;
        WildernessNavigation navigation;
        Transform warning;
        readonly LineRenderer[] warningLines = new LineRenderer[OutlinesPerWave];
        FinalBossSkillEffects skillEffects;
        readonly float[] cooldowns = new float[SaberSkills.Length];
        readonly Collider2D[] landingOverlap = new Collider2D[1];
        SpriteRenderer skin;
        HeroWalkMesh walk;
        Sprite[] frames;
        Skill skill;
        AttackMotion lastMotion;
        Vector2 sweepAngles;
        int facingIndex, nextSkill, waveIndex;
        Vector2 origin, target, previousGround;
        Vector2 aim = Vector2.down, castAim;
        float age, windup, reach, recovery;
        float bloodMistCooldown = BloodMistOpeningDelay;
        float bloodFrenzyRemaining;
        float saberBreathPhase;
        float saberTurnDelay;
        int pendingSaberFacing = -1;
        // XY is the torso offset in art units. Z is the screen-plane lean in degrees.
        Vector3 saberActionPose;
        float saberActionBend;
        Vector2 bloodTideMistCenter;
        float bloodTideMistRadius;
        double bloodTideMistRemaining, bloodMistExposure;
        float playerRadius;

        internal static Texture2D LoadArt(Style style)
        {
            string resource = "SlashDemo/Cultivators/" + (style == Style.SoulBell
                ? "SoulBellCultivator-Directions-v1" : "BloodSaberCultivator-Directions-v1");
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) throw new InvalidOperationException("Missing final boss artwork: " + resource);
            return texture;
        }

        internal static void Spawn(SlashArena arena, TravelMapEnvironment map, Style style, Texture2D texture)
        {
            bool usesBell = style == Style.SoulBell;
            var go = new GameObject(usesBell ? "摄魂铃邪修" : "血刀邪修");
            go.SetActive(false);
            go.transform.SetParent(map.transform, false);
            go.transform.position = map.Definition.Ground(765, 375);
            var boss = go.AddComponent<EvilCultivatorBoss>();
            boss.CombatStyle = style;
            boss.body = DemoArt.Body(go, BodyRadius);
            boss.body.bodyType = RigidbodyType2D.Kinematic;
            boss.body.useFullKinematicContacts = true;
            boss.navigation = go.AddComponent<WildernessNavigation>();
            boss.navigation.Initialize(map, boss.body, BodyRadius);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            DemoArt.Shape("Shadow", visual, "Disk", Vector2.zero, Vector2.one, new Color(0, 0, 0, .32f));
            boss.skin = new GameObject("Boss art").AddComponent<SpriteRenderer>();
            boss.skin.transform.SetParent(visual, false);
            boss.skin.transform.localScale = Vector3.one * Height;
            boss.skin.sharedMaterial = DemoArt.Material;
            boss.frames = CreateFacingSprites(texture);
            boss.skin.sprite = boss.frames[0];
            boss.walk = boss.skin.gameObject.AddComponent<HeroWalkMesh>();
            boss.walk.Initialize(boss.skin, boss.WalkRig(0), boss.CurrentWeapon.Gesture, fitStance: false);
            if (boss.ShowsWorldHealth)
            {
                DemoArt.Shape("HealthBack", go.transform, "Square", Vector2.zero, Vector2.one, new Color(.035f, .055f, .065f), 500);
                DemoArt.Shape("HealthFill", go.transform, "Square", Vector2.zero, Vector2.one, DemoArt.Red, 501);
            }
            var warning = new GameObject("AttackWarning").transform;
            warning.SetParent(go.transform, false);
            for (int i = 0; i < OutlinesPerWave; i++)
                boss.warningLines[i] = CreateOutline("Telegraph", warning, DemoArt.AttackWarningColor, .055f);
            warning.gameObject.SetActive(false);

            boss.enemy = go.AddComponent<SlashEnemy>();
            boss.enemy.maxHealth = usesBell ? 2600 : 2000;
            boss.enemy.moveSpeed = usesBell ? 3.55f : 4.55f;
            var actor = go.AddComponent<ObliqueActor>();
            actor.CreatureHeight = Height;
            actor.ShadowScale = .65f;
            go.SetActive(true);
            if (arena.Tuning != null) arena.Tuning.ApplyEnemy(boss.enemy);
            arena.RegisterEnemy(boss.enemy);
        }

        static Sprite[] CreateFacingSprites(Texture2D texture)
        {
            int width = texture.width / AtlasColumns, height = texture.height / AtlasRows;
            var sprites = new Sprite[AtlasColumns * AtlasRows];
            for (int i = 0; i < sprites.Length; i++)
            {
                var rect = new Rect(i % AtlasColumns * width, (AtlasRows - 1 - i / AtlasColumns) * height, width, height);
                sprites[i] = Sprite.Create(texture, rect, ArtPivot, height * ArtHeightRatio, 0, SpriteMeshType.FullRect);
            }
            return sprites;
        }

        internal void InitializeCombat(SlashArena world, ObliqueActor presentation, Transform groundWarning)
        {
            arena = world;
            actor = presentation;
            warning = groundWarning;
            playerRadius = arena.Player.GetComponent<CircleCollider2D>().radius;
            previousGround = body.position;
            foreach (var line in warningLines)
                DemoArt.StyleAttackWarning(line, .055f);
            skillEffects = new FinalBossSkillEffects(world, transform, UsesBell);
            ApplyAttackTuning(arena.Tuning?.Settings ?? new SlashTuningSettings());
        }

        internal void ApplyAttackTuning(SlashTuningSettings settings)
        {
            // The first-round Spell cultivator has the largest basic hit (1.42 times enemyDamage).
            enemy.damage = Mathf.CeilToInt(settings.enemyDamage * (UsesBell ? 3.7f : 3.85f));
            enemy.windupTime = settings.enemyWindup * (UsesBell ? 1.3f : 1.17f);
            enemy.attackRange = settings.enemyRange * 2.5f;
        }

        internal void Tick(float dt)
        {
            var player = arena.Player;
            if (player == null || !player.IsAlive) { Stop(); return; }
            AdvanceBloodSaberStage();
            float boostedTime = Mathf.Min(dt, bloodFrenzyRemaining);
            if (boostedTime > 0) SetBloodFrenzy(bloodFrenzyRemaining - boostedTime);
            AdvanceMistDamage(player, dt);
            if (!player.IsAlive) { Stop(); return; }
            for (int i = 0; i < cooldowns.Length; i++)
                cooldowns[i] = Mathf.Max(0, cooldowns[i] - dt);
            bloodMistCooldown = Mathf.Max(0, bloodMistCooldown - dt);
            skillEffects.Advance(dt);
            Vector2 ground = body.position;
            Vector2 travel = ground - previousGround;
            previousGround = ground;
            age += dt + boostedTime * (BloodFrenzyAttackSpeed - 1);
            if (!UsesBell)
            {
                saberBreathPhase = Mathf.Repeat(saberBreathPhase + dt * 2.8f, Mathf.PI * 2);
                if (phase == Phase.Windup && pendingSaberFacing >= 0 && age >= saberTurnDelay)
                {
                    SetFacing(pendingSaberFacing);
                    pendingSaberFacing = -1;
                }
                if (phase != Phase.Pursuit)
                {
                    float stance = phase == Phase.Recovery
                        ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(recovery * .65f, recovery, age)) : 1;
                    walk.AdvanceSaberPlant(dt, stance);
                    actor.SetPose(Vector2.zero, walk.Bob, 0);
                }
                UpdateSaberBodyPose();
            }
            switch (phase)
            {
                case Phase.Pursuit:
                    Vector2 delta = (Vector2)player.transform.position - ground;
                    if (!UsesBell && TryBeginBloodMist(player, delta)) break;
                    if (TryBeginAttack(delta)) break;
                    walk.Advance(travel, dt, false, arena.View);
                    actor.SetPose(Vector2.zero, walk.Bob, 0);
                    break;
                case Phase.BloodMist:
                    AdvanceBloodMist(player);
                    break;
                case Phase.Windup:
                    AnimateWindup();
                    if (age >= windup) Strike(player);
                    break;
                case Phase.Recovery:
                    if (!UsesBell)
                    {
                        if (UsesGroundStab(skill.Waves[0].Impact)) AnimateGroundStabRecovery();
                        else if (lastMotion.SweepDirection != 0) AnimateHorizontalFollow();
                        else
                        {
                            float follow = Mathf.SmoothStep(0, 1, age / lastMotion.FollowTime);
                            float hold = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, recovery, age));
                            if (skill.Waves[0].Impact == ImpactStyle.BloodFrenzy) ApplyBloodFrenzyPose(hold);
                            else if (skill.Waves[0].Impact == ImpactStyle.FlyingCrescents) ApplyFlyingBlades(1, follow, hold);
                            else ApplySaberPose(Mathf.Lerp(SaberFinishAngle, -54, follow), hold);
                        }
                    }
                    else if (age < lastMotion.FollowTime) FollowThrough();
                    else BlendPose(lastMotion.Follow, default, Mathf.InverseLerp(lastMotion.FollowTime, recovery, age));
                    if (age >= recovery)
                    {
                        if (!UsesBell) walk.EndSaberPlant();
                        phase = Phase.Pursuit;
                    }
                    break;
            }
        }

        internal void FixedTick(float dt)
        {
            var player = arena.Player;
            float moveSpeed = bloodFrenzyRemaining > 0 ? enemy.moveSpeed * BloodFrenzyMoveSpeed : enemy.moveSpeed;
            var velocity = IsChasing && player != null && player.IsAlive
                ? navigation.ChaseDirection(body.position, player.transform.position) * moveSpeed : Vector2.zero;
            var constrained = navigation.ConstrainVelocity(velocity, dt);
            body.linearVelocity = constrained;
            if (constrained.sqrMagnitude >= .0001f) Face(constrained, true);
        }

        void Face(Vector2 direction, bool hysteresis) =>
            SetFacing(HeroFacingMath.Direction(direction, hysteresis ? facingIndex : -1, arena.View.elevation));

        void SetFacing(int index)
        {
            if (index == facingIndex) return;
            facingIndex = index;
            skin.sprite = frames[index];
            walk.SetPaintedRig(WalkRig(index), CurrentWeapon.Gesture);
            walk.SetProfileLeg(index == 2 ? (UsesBell ? BellEastLeg : SaberEastLeg)
                : index == 6 ? (UsesBell ? BellWestLeg : SaberWestLeg) : null, bindVisibleLeg: UsesBell);
        }

        HeroGaitMath.Rig WalkRig(int index) => (UsesBell ? BellFeet : SaberFeet)[index].Rig(index);

        void UpdateSaberBodyPose()
        {
            float breath = Mathf.Sin(saberBreathPhase);
            Vector3 pose = new Vector3(breath * .003f, breath * .008f, breath * .65f);
            float brace = 0;
            walk.SetSaberPoseBlend(phase == Phase.Windup && CurrentWave.Impact == ImpactStyle.ArenaMistTransition
                ? Mathf.SmoothStep(0, 1, age / ArenaMistPoseBlendTime) : 1);
            if (phase == Phase.Windup)
            {
                var wave = CurrentWave;
                float side = Mathf.Abs(castAim.x) > .28f ? Mathf.Sign(castAim.x) : CurrentWeapon.Side;
                if (wave.Impact == ImpactStyle.BloodFrenzy)
                {
                    float raise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(saberTurnDelay, windup, age));
                    saberActionPose = new Vector3(-side * .012f, .025f, side * 2.8f) * raise;
                    saberActionBend = .14f * raise;
                }
                else
                {
                    if (wave.Motion.SweepDirection != 0)
                    {
                        float swing = DemoArt.Direction(sweepAngles.y).x - DemoArt.Direction(sweepAngles.x).x;
                        if (Mathf.Abs(swing) > .2f) side = Mathf.Sign(swing);
                    }
                    float releaseStart = AttackReleaseStart(wave.Motion);
                    float charge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(saberTurnDelay, releaseStart, age));
                    float releaseProgress = Mathf.InverseLerp(releaseStart, windup, age);
                    float release = Mathf.SmoothStep(0, 1, releaseProgress);
                    bool groundStab = UsesGroundStab(wave.Impact);
                    bool sweep = wave.Motion.SweepDirection != 0;
                    bool flyingBlades = wave.Impact == ImpactStyle.FlyingCrescents;
                    if (flyingBlades) side = FlyingBladeSide;
                    var ready = groundStab ? new Vector3(0, .021f, 0)
                        : new Vector3(-side * (sweep ? .047f : .030f), .010f, side * (sweep ? 6f : 4f));
                    var contact = groundStab ? new Vector3(side * .012f, -.065f, -side * 2.2f)
                        : new Vector3(side * (sweep ? .043f : .036f), sweep ? -.044f : -.028f, -side * (sweep ? 6f : 4.3f));
                    if (flyingBlades)
                    {
                        ready = new Vector3(-side * .022f, .010f, side * 3f);
                        contact = new Vector3(side * .048f, -.034f, -side * 5f);
                    }
                    float drive = groundStab ? release * release : flyingBlades ? FlyingBladeExtension(releaseProgress) : release;
                    saberActionPose = Vector3.Lerp(ready * charge, contact, drive);
                    saberActionBend = Mathf.Lerp(.20f * charge, groundStab ? 1 : sweep ? .70f : .45f, drive);
                }
                pose += saberActionPose;
                brace = saberActionBend;
            }
            else if (phase == Phase.Recovery)
            {
                float riseStart = UsesGroundStab(skill.Waves[0].Impact) ? .10f : 0;
                float planted = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(riseStart, recovery, age));
                pose += saberActionPose * planted;
                brace = saberActionBend * planted;
            }

            walk.SetSaberBodyPose(pose, pose.z, brace);
        }

        float AttackReleaseStart(AttackMotion motion)
        {
            float releaseTime = Mathf.Min(motion.Release, windup * .58f);
            return windup - (UsesBell ? releaseTime : releaseTime / SaberAnimationSpeed);
        }

        void AnimateWindup()
        {
            var motion = CurrentWave.Motion;
            bool bloodFrenzy = CurrentWave.Impact == ImpactStyle.BloodFrenzy;
            bool groundStab = UsesGroundStab(CurrentWave.Impact);
            float releaseStart = AttackReleaseStart(motion);
            float followTime = waveIndex == 0 ? 0 : lastMotion.FollowTime;
            if (bloodFrenzy) ApplyBloodFrenzyPose(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(saberTurnDelay, windup, age)));
            else if (groundStab)
            {
                float drive = Mathf.InverseLerp(releaseStart, windup, age);
                float raise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(saberTurnDelay, releaseStart * .8f, age));
                ApplyGroundStab(drive * drive, CurrentWave.Impact == ImpactStyle.ArenaMistTransition ? 1 : raise);
            }
            else if (!UsesBell) AnimateSaber(motion, releaseStart);
            else if (age < followTime) FollowThrough();
            else if (age < releaseStart)
            {
                float readyTime = Mathf.Lerp(followTime, releaseStart, .86f);
                BlendPose(waveIndex == 0 ? default : lastMotion.Follow, motion.Ready,
                    Mathf.InverseLerp(followTime, readyTime, age));
            }
            else
            {
                float progress = Mathf.InverseLerp(releaseStart, windup, age);
                float arm = progress * progress * (2 - progress);
                ApplyPose(motion.Ready, motion.Contact, arm, motion.SwingArc * (4 * arm * (1 - arm)));
            }
            if (CurrentWave.Impact == ImpactStyle.ArenaMistTransition) return;
            var weapon = CurrentWeapon;
            Vector3 tip = walk.PaintedCastWorldPoint(weapon.Tip);
            if (bloodFrenzy || UsesBell || age < releaseStart)
                skillEffects.Charge(walk.PaintedCastWorldPoint(weapon.Gesture.Grip), tip, Mathf.Clamp01(age / windup));
            else
            {
                skillEffects.EndCharge();
                if (!groundStab) skillEffects.TrackSaberTip(tip);
            }
        }

        static bool UsesGroundStab(ImpactStyle impact) => impact is ImpactStyle.BloodTide or ImpactStyle.ArenaMistTransition;

        void FollowThrough()
        {
            float progress = Mathf.Clamp01(age / lastMotion.FollowTime);
            float settle = progress * (2 - progress);
            ApplyPose(lastMotion.Contact, lastMotion.Follow, settle, Vector2.zero);
        }

        void BlendPose(AttackPose from, AttackPose to, float progress)
        {
            float t = Mathf.SmoothStep(0, 1, progress);
            ApplyPose(from, to, t, Vector2.zero);
        }

        void ApplyPose(AttackPose from, AttackPose to, float arm, Vector2 arc)
        {
            float side = CurrentWeapon.Side;
            Vector2 torso = Vector2.up * Mathf.Lerp(from.Brace, to.Brace, arm);
            float reachOffset = Mathf.Lerp(from.Reach, to.Reach, arm);
            float outward = Mathf.Lerp(from.Out, to.Out, arm) + arc.x;
            float lift = Mathf.Lerp(from.Lift, to.Lift, arm) + arc.y;
            Vector2 hand = new Vector2(outward * side + castAim.x * reachOffset, lift + castAim.y * reachOffset * .32f);
            walk.SetPaintedCastPose(torso, Vector2.ClampMagnitude(hand * .4f, .052f), 0);
        }

        void AnimateSaber(AttackMotion motion, float releaseStart)
        {
            float windupProgress = Mathf.InverseLerp(saberTurnDelay, releaseStart, age);
            float load = Mathf.SmoothStep(0, 1, windupProgress * SaberAnimationSpeed);
            float release = Mathf.InverseLerp(releaseStart, windup, age);
            if (motion.SweepDirection != 0)
            {
                ApplyHorizontalCut(.5f * release * release, 0, load);
                return;
            }
            float hold = Mathf.SmoothStep(0, 1, load / .35f);
            if (CurrentWave.Impact == ImpactStyle.FlyingCrescents)
            {
                float draw = Mathf.SmoothStep(0, 1, windupProgress / .80f);
                ApplyFlyingBlades(release, 0, draw);
                return;
            }
            ApplySaberPose(Mathf.Lerp(Mathf.Lerp(-60, SaberReadyAngle, load), SaberFinishAngle,
                Mathf.SmoothStep(0, 1, release)), hold);
        }

        void AnimateHorizontalFollow()
        {
            float swingTime = Mathf.Min(recovery * .60f, windup - AttackReleaseStart(lastMotion));
            float remainingSwing = 1 - Mathf.Clamp01(age / swingTime);
            float sweep = 1 - .5f * remainingSwing * remainingSwing;
            float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(swingTime * .65f, recovery, age));
            ApplyHorizontalCut(sweep, settle, 1 - settle);
            if (age <= swingTime)
            {
                var weapon = CurrentWeapon;
                skillEffects.TrackSaberTip(walk.PaintedCastWorldPoint(weapon.Tip));
            }
        }

        void ApplyBloodFrenzyPose(float blend)
        {
            var arm = CurrentWeapon.Gesture.SaberArm.Value;
            Vector2 axis = DemoArt.Direction(BloodFrenzyReadyAngle);
            axis.x *= arm.Side;
            Vector2 grip = arm.RaisedGrip(axis, out Vector2 elbow);
            walk.SetSaberCastPose(grip, axis, blend, elbow, twoHanded: false);
        }

        void ApplyHorizontalCut(float sweep, float settle, float hold)
        {
            // Contact is at the center of the fan. The blade and hands keep moving through it into recovery.
            float overshoot = lastMotion.SweepDirection * SaberSweepFollowAngle * settle;
            float angle = Mathf.Lerp(sweepAngles.x, sweepAngles.y, sweep) + overshoot;
            float armAngle = Mathf.Lerp(sweepAngles.x, sweepAngles.y, Mathf.Clamp01(sweep * 1.35f)) + overshoot;
            float extension = Mathf.Lerp(Mathf.SmoothStep(0, 1, sweep * 2), .35f, settle);
            Vector3 axis = Quaternion.Inverse(arena.View.Rotation) * (Vector3)DemoArt.Direction(angle);
            Vector2 reach = ObliqueProjection.ProjectGround(DemoArt.Direction(armAngle), arena.View.elevation);
            var arm = CurrentWeapon.Gesture.SaberArm.Value;
            Vector2 shoulders = (arm.Shoulder + arm.Support.Shoulder) * .5f;
            Vector2 hand = shoulders + new Vector2(reach.x * Mathf.Lerp(.128f, .185f, extension),
                Mathf.Lerp(-.094f, -.112f, extension) + reach.y * .085f);
            walk.SetSaberCastPose(hand, axis, hold, elbowBend: Mathf.Lerp(.82f, .20f, extension));
        }

        float FlyingBladeSide => Mathf.Abs(SaberFacingX) > .5f ? Mathf.Sign(SaberFacingX) : CurrentWeapon.Side;

        static float FlyingBladeExtension(float release) => Mathf.SmoothStep(0, 1, release / FlyingBladeExtensionEnd);

        void ApplyFlyingBlades(float release, float follow, float hold)
        {
            var arm = CurrentWeapon.Gesture.SaberArm.Value;
            float side = FlyingBladeSide;
            Vector2 shoulders = (arm.Shoulder + arm.Support.Shoulder) * .5f;
            float extension = FlyingBladeExtension(release);
            // Drive the hilt forward from bent elbows before the blade dips into the release.
            Vector2 grip = shoulders + new Vector2(side * (Mathf.Lerp(.099f, .229f, extension) + .016f * follow),
                Mathf.Lerp(-.079f, -.158f, extension) - .028f * follow);
            float tipDip = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.22f, 1, release));
            Vector2 axis = DemoArt.Direction(Mathf.Lerp(27, -46, tipDip) - 11 * follow);
            axis.x *= side;
            walk.SetSaberCastPose(grip, axis, hold, elbowsDown: true);
        }

        void ApplyGroundStab(float drive, float hold)
        {
            var arm = CurrentWeapon.Gesture.SaberArm.Value;
            float forward = SaberFacingX;
            Vector2 axis = new Vector2(Mathf.Lerp(arm.Side * .19f, .32f * Mathf.Sign(forward), Mathf.Abs(forward)), -1).normalized;
            float bladeLength = Vector2.Distance(arm.Grip, arm.ExtendedTip);
            float raisedHeight = Mathf.Min(arm.Shoulder.y, arm.Support.Shoulder.y) - .028f;
            float plantedHeight = -.012f - axis.y * bladeLength;
            // Project the space in front of the body into the eight painted views.
            // The fixed grip line keeps the blade planted even as the shoulders dip.
            float gripX = forward * .205f - axis.x * bladeLength * .5f;
            Vector2 grip = new Vector2(gripX, Mathf.Lerp(raisedHeight, plantedHeight, drive));
            walk.SetSaberCastPose(walk.SaberGripBeforeBodyPose(grip), axis, hold, elbowsDown: true);
        }

        void AnimateGroundStabRecovery()
        {
            float withdraw = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.10f, recovery * .62f, age));
            float lower = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(recovery * .42f, recovery, age));
            ApplyGroundStab(1 - withdraw * .82f, 1 - lower);
        }

        void ApplySaberPose(float angle, float hold)
        {
            var arm = CurrentWeapon.Gesture.SaberArm.Value;
            Vector2 direction = DemoArt.Direction(angle);
            float forward = Mathf.Abs(castAim.x) > .3f ? Mathf.Sign(castAim.x) : CurrentWeapon.Side;
            Vector2 axis = new Vector2(forward * direction.x, direction.y);
            // Both extended arms rotate together about the shoulders, with the blade along the hands.
            Vector2 shoulders = (arm.Shoulder + arm.Support.Shoulder) * .5f;
            Vector2 hand = shoulders + axis * .205f;
            walk.SetSaberCastPose(hand, axis, hold);
        }

        bool TryBeginBloodMist(SlashPlayer player, Vector2 delta)
        {
            if (enemy.attackRange <= 0 || bloodMistCooldown > 0) return false;
            float minRange = Mathf.Max(4, enemy.attackRange * 1.45f);
            float maxRange = Mathf.Max(9, enemy.attackRange * BloodMistCut.Range);
            if (delta.sqrMagnitude < minRange * minRange || delta.sqrMagnitude > maxRange * maxRange
                || !TryBloodMistLanding(player, out _)) return false;

            PrepareAttack(BloodMistCut, Mathf.Max(2.2f, enemy.attackRange));
            Face(delta, false);
            phase = Phase.BloodMist;
            age = 0;
            windup = BloodMistWindup;
            bloodMistCooldown = BloodMistCut.Cooldown;
            DrawCircle(warningLines[0], origin, .8f);
            for (int i = 0; i < warningLines.Length; i++) warningLines[i].enabled = i == 0;
            warning.gameObject.SetActive(true);
            skillEffects.BloodMist(origin, delta.normalized * .6f);
            return true;
        }

        void AdvanceBloodMist(SlashPlayer player)
        {
            if (age < windup)
            {
                float dissolve = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(windup * .22f, windup * .84f, age));
                SetBloodMistOpacity(1 - dissolve);
                return;
            }

            SetBloodMistOpacity(1);
            if (!TryBloodMistLanding(player, out Vector2 landing))
            {
                warning.gameObject.SetActive(false);
                walk.EndSaberPlant();
                phase = Phase.Pursuit;
                return;
            }

            body.linearVelocity = Vector2.zero;
            body.position = landing;
            transform.position = landing;
            origin = previousGround = landing;
            walk.ResetWalk();
            walk.BeginSaberPlant();
            navigation.InvalidateRoute();
            actor.SetPose(Vector2.zero, 0, 0);
            Vector2 playerPosition = player.transform.position;
            skillEffects.BloodMist(landing, (playerPosition - landing).normalized * .6f);
            BeginWave(playerPosition, BloodMistSlashWindup);
        }

        bool TryBloodMistLanding(SlashPlayer player, out Vector2 landing)
        {
            Vector2 position = player.transform.position;
            Vector2 behind = -player.FacingDirection.normalized;
            if (behind == Vector2.zero) behind = (position - body.position).normalized;
            Vector2 side = Vector2.Perpendicular(behind);
            float distance = Mathf.Max(1.35f, BodyRadius + playerRadius + .20f);
            var filter = new ContactFilter2D { useTriggers = false };
            foreach (float angle in BloodMistLandingAngles)
            {
                Vector2 direction = DemoArt.Direction(angle);
                Vector2 point = position + (behind * direction.x + side * direction.y) * distance;
                if (!navigation.Walkable(point)
                    || Physics2D.OverlapCircle(point, BodyRadius + .04f, filter, landingOverlap) > 0
                    || !navigation.Clear(point, position)) continue;
                landing = point;
                return true;
            }
            landing = default;
            return false;
        }

        void SetBloodMistOpacity(float opacity)
        {
            Color color = skin.color;
            color.a = opacity;
            skin.color = color;
        }

        bool TryBeginAttack(Vector2 delta)
        {
            if (enemy.attackRange <= 0) return false;
            for (int offset = 0; offset < Skills.Length; offset++)
            {
                int index = (nextSkill + offset) % Skills.Length;
                var candidate = Skills[index];
                float range = candidate.Range * enemy.attackRange;
                if (cooldowns[index] > 0 || delta.sqrMagnitude > range * range) continue;
                if (candidate.Waves[0].Impact == ImpactStyle.BloodFrenzy
                    && (bloodSaberStage < BloodSaberStage.BloodFrenzy || enemy.Health <= 1 || bloodFrenzyRemaining > 0))
                    continue;
                if (candidate.Waves[0].Impact == ImpactStyle.FlyingCrescents && bloodSaberStage >= BloodSaberStage.FiveBlades)
                    candidate = FiveBladeSkill;
                cooldowns[index] = candidate.Cooldown;
                nextSkill = (index + 1) % Skills.Length;
                PrepareAttack(candidate, enemy.attackRange);
                BeginWave(origin + delta);
                return true;
            }
            return false;
        }

        void PrepareAttack(Skill candidate, float attackReach)
        {
            skill = candidate;
            waveIndex = 0;
            origin = body.position;
            reach = attackReach;
            body.linearVelocity = Vector2.zero;
            if (UsesBell) walk.ResetWalk();
            else walk.BeginSaberPlant();
            actor.SetPose(Vector2.zero, walk.Bob, 0);
        }

        void BeginWave(Vector2 playerPosition, float quickWindup = 0)
        {
            var wave = CurrentWave;
            if (wave.Retarget)
            {
                target = playerPosition;
                Vector2 delta = target - origin;
                if (delta.sqrMagnitude > .001f) aim = delta.normalized;
            }
            // Later waves can re-aim their hit zones while the casting stance stays planted.
            if (waveIndex == 0)
            {
                castAim = aim;
                pendingSaberFacing = -1;
                saberTurnDelay = 0;
                if (wave.Impact != ImpactStyle.ArenaMistTransition)
                {
                    bool midStride = !UsesBell && walk.HasSaberStride;
                    int facing = HeroFacingMath.Direction(castAim, midStride ? facingIndex : -1, arena.View.elevation);
                    if (midStride && facing != facingIndex)
                    {
                        pendingSaberFacing = facing;
                        saberTurnDelay = SaberStepTurnTime;
                    }
                    else SetFacing(facing);
                }
            }
            if (wave.Motion.SweepDirection != 0)
            {
                Zone arc = wave.Zones[0];
                Place(arc, out _, out Vector2 direction);
                float heading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                float halfSweep = arc.Detail * .5f * wave.Motion.SweepDirection;
                sweepAngles = new Vector2(heading - halfSweep, heading + halfSweep);
            }
            phase = Phase.Windup;
            age = 0;
            // Keep successive impacts farther apart than the player's hit immunity.
            windup = quickWindup > 0 ? quickWindup
                : Mathf.Max(waveIndex == 0 ? .52f : .85f, enemy.windupTime * wave.Windup);
            if (!UsesBell) UpdateSaberBodyPose();
            AnimateWindup();
            DrawWave(warningLines);
            warning.gameObject.SetActive(wave.Zones.Length > 0);
        }

        void Strike(SlashPlayer player)
        {
            var wave = CurrentWave;
            lastMotion = wave.Motion;
            Vector2 position = player.transform.position;
            bool hit = false;
            Vector2 knockback = aim;
            warning.gameObject.SetActive(false);
            skillEffects.EndCharge();
            if (wave.Impact == ImpactStyle.ArenaMistTransition)
            {
                arenaMist ??= new BloodArenaMist(GetComponentInParent<TravelMapEnvironment>());
                arenaMist.Show();
                BeginRecovery();
                return;
            }
            if (wave.Impact == ImpactStyle.BloodFrenzy)
            {
                int cost = Mathf.CeilToInt(enemy.Health * (BloodFrenzyHealthPercent / 100f));
                if (enemy.TrySpendHealth(cost))
                {
                    SetBloodFrenzy(BloodFrenzyDuration);
                    skillEffects.BloodFrenzy(origin);
                    arena.Effects.Number(origin, "-" + cost, DemoArt.Red);
                }
                BeginRecovery();
                AdvanceBloodSaberStage();
                return;
            }
            bool flyingCrescents = wave.Impact == ImpactStyle.FlyingCrescents;
            Vector3 launch = flyingCrescents
                ? Vector3.Lerp(walk.PaintedCastWorldPoint(CurrentWeapon.Gesture.Grip),
                    walk.PaintedCastWorldPoint(CurrentWeapon.Tip), .55f)
                : Vector3.zero;
            foreach (var zone in wave.Zones)
            {
                Place(zone, out Vector2 center, out Vector2 direction);
                bool directional = zone.Shape == Shape.Arc || zone.Shape == Shape.Lane;
                if (Contains(zone, center, direction, position))
                {
                    hit = true;
                    Vector2 outward = position - center;
                    knockback = directional || outward.sqrMagnitude <= .001f ? direction : outward.normalized;
                }
                float detail = zone.Shape == Shape.Arc ? zone.Detail : zone.Detail * reach;
                if (flyingCrescents) skillEffects.FlyingCrescent(launch, center, direction, zone.Size * reach, detail);
                else if (wave.Impact == ImpactStyle.BloodCut) skillEffects.BloodCut(center, direction, zone.Size * reach, detail);
                else if (wave.Impact == ImpactStyle.BloodTide)
                {
                    float radius = zone.Size * reach;
                    skillEffects.BloodTide(center, radius);
                    bloodTideMistCenter = center;
                    bloodTideMistRadius = radius;
                    bloodTideMistRemaining = BloodTideMistDuration;
                    skillEffects.ShowBloodTideMist(center, radius);
                }
                else skillEffects.Impact(zone.Shape, center, direction, zone.Size * reach, detail,
                    wave.Impact == ImpactStyle.Seal, wave.Damage);
            }
            arena.Effects.PlaySwing(wave.Damage >= 1.18f);
            if (hit)
            {
                int before = player.Health;
                float damageScale = UsesBell ? 1 : BloodSaberDamageScale;
                int damage = Mathf.Max(1, Mathf.RoundToInt(enemy.damage * wave.Damage * damageScale * DamageMultiplier));
                if (player.TakeDamage(damage, knockback) && !UsesBell)
                    HealFromDamage(before, player);
            }
            if (!player.IsAlive || !enemy.Alive || !isActiveAndEnabled) { Stop(); return; }
            if (++waveIndex < skill.Waves.Length)
            {
                BeginWave(player.transform.position);
                return;
            }
            BeginRecovery();
        }

        void BeginRecovery()
        {
            phase = Phase.Recovery;
            age = 0;
            recovery = skill.Recovery;
        }

        void SetBloodFrenzy(float remaining)
        {
            bloodFrenzyRemaining = remaining;
            walk?.SetSaberBladeGlow(remaining > 0);
        }

        internal void AdvanceBloodSaberStage()
        {
            if (UsesBell || bloodSaberStage == BloodSaberStage.ArenaMist) return;
            long health = (long)enemy.Health * 4;
            long maxHealth = enemy.maxHealth;
            var stage = health < maxHealth ? BloodSaberStage.ArenaMist
                : health < 2 * maxHealth ? BloodSaberStage.FiveBlades
                : health < 3 * maxHealth ? BloodSaberStage.BloodFrenzy
                : BloodSaberStage.Opening;
            if (stage <= bloodSaberStage) return;
            var player = arena.Player;
            if (stage == BloodSaberStage.ArenaMist && (player == null || !player.IsAlive)) return;
            bloodSaberStage = stage;
            if (stage == BloodSaberStage.ArenaMist)
            {
                if (phase == Phase.BloodMist) SetBloodMistOpacity(1);
                skillEffects.EndCharge();
                walk.CaptureSaberPose();
                PrepareAttack(ArenaMistTransition, enemy.attackRange);
                BeginWave(player.transform.position);
            }
        }

        internal int LimitHealthForTransition(int remaining)
        {
            if (remaining > 0 || UsesBell || !isActiveAndEnabled || bloodSaberStage == BloodSaberStage.ArenaMist)
                return remaining;
            var player = arena?.Player;
            return player != null && player.IsAlive ? 1 : remaining;
        }

        void AdvanceMistDamage(SlashPlayer player, float dt)
        {
            Vector2 point = player.transform.position;
            arenaMist?.Advance(dt);
            double exposure = arenaMist != null && arenaMist.Contains(point, playerRadius) ? dt : 0;
            if (bloodTideMistRemaining > 0)
            {
                double elapsed = Math.Min(dt, bloodTideMistRemaining);
                bloodTideMistRemaining -= elapsed;
                float radius = bloodTideMistRadius + playerRadius;
                if ((point - bloodTideMistCenter).sqrMagnitude <= radius * radius)
                    exposure = Math.Max(exposure, elapsed);
                float remaining = (float)bloodTideMistRemaining;
                skillEffects.UpdateBloodTideMist(BloodTideMistDuration - remaining, Mathf.Clamp01(remaining / .65f));
            }

            bloodMistExposure = exposure > 0 ? bloodMistExposure + exposure : 0;
            while (bloodMistExposure >= 1 && player.IsAlive)
            {
                bloodMistExposure -= 1;
                int before = player.Health;
                if (player.TakeBloodMistDamage(Mathf.RoundToInt(BloodTideMistDamage * DamageMultiplier)))
                    HealFromDamage(before, player);
            }
        }

        void HealFromDamage(int previousHealth, SlashPlayer player)
            => enemy.Heal((previousHealth - player.Health) * LifestealMultiplier);

        void Place(Zone zone, out Vector2 center, out Vector2 direction)
        {
            Vector2 side = new Vector2(-aim.y, aim.x);
            Vector2 rotation = DemoArt.Direction(zone.Angle);
            direction = aim * rotation.x + side * rotation.y;
            center = (zone.AtTarget ? target : origin) + reach * (aim * zone.Offset.x + side * zone.Offset.y);
            if (zone.Shape == Shape.Lane && zone.AtTarget) center -= direction * (zone.Size * reach * .5f);
        }

        bool Contains(Zone zone, Vector2 center, Vector2 direction, Vector2 point)
        {
            float size = zone.Size * reach;
            Vector2 relative = point - center;
            switch (zone.Shape)
            {
                case Shape.Arc:
                    return SlashPlayer.InsideArc(center, direction, point, size, zone.Detail, playerRadius);
                case Shape.Lane:
                    float along = Vector2.Dot(relative, direction);
                    float across = Vector2.Dot(relative, new Vector2(-direction.y, direction.x));
                    float width = zone.Detail * reach;
                    var closest = new Vector2(Mathf.Clamp(along, 0, size), Mathf.Clamp(across, -width, width));
                    return (new Vector2(along, across) - closest).sqrMagnitude <= playerRadius * playerRadius;
                default:
                    float distance = relative.magnitude;
                    return distance <= size + playerRadius
                        && (zone.Shape != Shape.Ring || distance + playerRadius >= zone.Detail * reach);
            }
        }

        static LineRenderer CreateOutline(string name, Transform parent, Color color, float width)
        {
            var line = DemoArt.Line(name, parent, new Vector3[CurveSegments], color,
                width * DemoArt.AttackWarningWidthScale, 10, loop: true);
            line.useWorldSpace = true;
            line.enabled = false;
            return line;
        }

        static Vector3 GroundPoint(Vector2 point) => new Vector3(point.x, point.y, -.025f);

        void DrawWave(LineRenderer[] lines)
        {
            int count = 0;
            foreach (var zone in CurrentWave.Zones)
            {
                Place(zone, out Vector2 center, out Vector2 direction);
                var line = lines[count++];
                float size = zone.Size * reach;
                switch (zone.Shape)
                {
                    case Shape.Arc:
                        line.positionCount = CurveSegments + 1;
                        line.SetPosition(0, GroundPoint(center));
                        float heading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        for (int i = 0; i < CurveSegments; i++)
                        {
                            float angle = heading + (i / (CurveSegments - 1f) - .5f) * zone.Detail;
                            line.SetPosition(i + 1, GroundPoint(center + DemoArt.Direction(angle) * size));
                        }
                        break;
                    case Shape.Lane:
                        line.positionCount = 4;
                        Vector2 edge = new Vector2(-direction.y, direction.x) * (zone.Detail * reach);
                        Vector2 end = center + direction * size;
                        line.SetPosition(0, GroundPoint(center - edge));
                        line.SetPosition(1, GroundPoint(center + edge));
                        line.SetPosition(2, GroundPoint(end + edge));
                        line.SetPosition(3, GroundPoint(end - edge));
                        break;
                    default:
                        DrawCircle(line, center, size);
                        if (zone.Shape == Shape.Ring) DrawCircle(lines[count++], center, zone.Detail * reach);
                        break;
                }
            }
            for (int i = 0; i < lines.Length; i++) lines[i].enabled = i < count;
        }

        static void DrawCircle(LineRenderer line, Vector2 center, float radius)
        {
            line.positionCount = CurveSegments;
            for (int i = 0; i < CurveSegments; i++)
                line.SetPosition(i, GroundPoint(center + DemoArt.Direction(i * 360f / CurveSegments) * radius));
        }

        internal void Stop()
        {
            if (phase == Phase.BloodMist) SetBloodMistOpacity(1);
            phase = Phase.Pursuit;
            age = 0;
            aim = Vector2.down;
            nextSkill = 0;
            bloodMistCooldown = BloodMistOpeningDelay;
            SetBloodFrenzy(0);
            bloodTideMistRemaining = 0;
            bloodMistExposure = 0;
            bloodSaberStage = BloodSaberStage.Opening;
            arenaMist?.Hide();
            Array.Clear(cooldowns, 0, cooldowns.Length);
            previousGround = body.position;
            saberBreathPhase = 0;
            saberTurnDelay = 0;
            pendingSaberFacing = -1;
            saberActionPose = Vector3.zero;
            saberActionBend = 0;
            walk.ResetWalk();
            walk.SetSaberPoseBlend(1);
            walk.SetSaberBodyPose(Vector2.zero, 0);
            walk.SetPaintedCastPose(Vector2.zero, Vector2.zero, 0);
            warning.gameObject.SetActive(false);
            skillEffects?.Clear();
            actor.SetPose(Vector2.zero, 0, 0);
        }

        void OnEnable()
        {
            if (bloodSaberStage == BloodSaberStage.ArenaMist && (phase != Phase.Windup || !IsTransitionInvulnerable))
                arenaMist?.Show();
        }

        void OnDisable()
        {
            if (phase == Phase.BloodMist) SetBloodMistOpacity(1);
            SetBloodFrenzy(0);
            bloodTideMistRemaining = 0;
            bloodMistExposure = 0;
            arenaMist?.Hide();
            skillEffects?.Clear();
        }

        void OnDestroy()
        {
            skillEffects?.Dispose();
            arenaMist?.Dispose();
            if (frames == null) return;
            foreach (var sprite in frames) Destroy(sprite);
        }
    }
}
