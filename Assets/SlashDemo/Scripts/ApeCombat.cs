using UnityEngine;

namespace SlashDemo
{
    public sealed class ApeCombat : MonoBehaviour
    {
        public enum State { Pursuit, PalmWindup, PalmSwing, PoundWindup, PoundAir, Recovery }
        public const float PalmDuration = .28f, PoundWindup = .9f, PoundAirDuration = .9f;
        public const float PoundRadius = 2.45f, PoundCooldown = 6.5f, RecoveryDuration = .8f;
        static readonly Vector3 PalmReady = new Vector3(-.18f, -.09f, 10);
        static readonly Vector3 PalmStrike = new Vector3(.29f, -.035f, -12);
        static readonly Vector3 PalmFollow = new Vector3(.07f, -.025f, -4);
        static readonly Vector3 PoundLanded = new Vector3(0, -.22f, -8);
        public State Current { get; private set; }
        public bool IsChasing => Current == State.Pursuit;
        public bool IsAirborne => Current == State.PoundAir;
        public Vector2 LockedAim { get; private set; } = Vector2.right;
        public float Progress { get; private set; }
        public int PalmCasts { get; private set; }
        public int PoundCasts { get; private set; }
        public int DamageEvents { get; private set; }
        SlashEnemy enemy;
        SlashArena arena;
        ObliqueActor actor;
        ApeAnimation animationMesh;
        BeastAttackParticles particles;
        Rigidbody2D body;
        CircleCollider2D groundCollider;
        WildernessNavigation navigation;
        Transform warning;
        LineRenderer palm, swipe, pound, impact;
        Vector2 poundOrigin, poundTarget, poundLanding, impactPoint;
        float age, clock, nextPound;
        float impactAge = 1;
        bool hit, poundRecovery;

        public void Initialize(SlashEnemy owner, SlashArena world, ObliqueActor presentation, Transform groundWarning)
        {
            enemy = owner; arena = world; actor = presentation; warning = groundWarning;
            animationMesh = GetComponent<ApeAnimation>();
            body = GetComponent<Rigidbody2D>();
            groundCollider = GetComponent<CircleCollider2D>();
            navigation = GetComponent<ApeDemon>().Navigation;
            palm = warning.GetComponentInChildren<LineRenderer>(true);
            swipe = DemoArt.Line("Ape palm sweep", warning, new Vector3[24], new Color(1, .81f, .43f), .09f, -68);
            swipe.endWidth = .035f;
            pound = DemoArt.Ring("Ape ground pound warning", world.View.GroundMarks, PoundRadius,
                DemoArt.AttackWarningColor, DemoArt.BeastAttackWarningWidth, -69);
            particles = BeastAttackParticles.For(world, transform);
            impact = DemoArt.Ring("Ape landing shockwave", world.View.GroundMarks, 1,
                new Color(.95f, .84f, .45f, .9f), .065f, -64);
            nextPound = 2 + GetComponent<ApeDemon>().FormationSlot * .7f;
            Stop();
        }
        public void Tick(float dt)
        {
            if (arena.Paused || dt <= 0) return;
            if (!enemy.Alive || arena.Player == null || !arena.Player.IsAlive) { Stop(); return; }
            clock += dt; age += dt;
            if (impactAge < .36f) { impactAge += dt; DrawImpact(); }
            Vector2 delta = arena.Player.transform.position - transform.position;
            switch (Current)
            {
                case State.Pursuit:
                    if (delta.magnitude <= PoundRadius && clock >= nextPound) Begin(State.PoundWindup, delta);
                    else if (delta.magnitude <= enemy.attackRange) Begin(State.PalmWindup, delta);
                    break;
                case State.PalmWindup:
                    Progress = Mathf.Clamp01(age / Mathf.Max(.01f, enemy.windupTime));
                    animationMesh.SetAttackPose(Current, Progress);
                    PresentPose(PalmReady * Mathf.SmoothStep(0, 1, Progress));
                    if (Progress >= 1) { Current = State.PalmSwing; age = 0; palm.enabled = false; }
                    break;
                case State.PalmSwing:
                    Progress = Mathf.Clamp01(age / PalmDuration);
                    animationMesh.SetAttackPose(Current, Progress);
                    PresentPose(Progress < .5f
                        ? Vector3.Lerp(PalmReady, PalmStrike, Mathf.SmoothStep(0, 1, Progress * 2))
                        : Vector3.Lerp(PalmStrike, PalmFollow, Mathf.SmoothStep(0, 1, (Progress - .5f) * 2)));
                    DrawPalmSweep(Progress);
                    if (!hit && Progress >= .5f)
                    {
                        hit = true; particles.ApePalm(transform.position, LockedAim, enemy.attackRange);
                        if (SlashPlayer.InsideArc(transform.position, LockedAim, arena.Player.transform.position, enemy.attackRange, 115, .29f))
                            Damage(enemy.damage);
                    }
                    if (Progress >= 1) Recover();
                    break;
                case State.PoundWindup:
                    Progress = Mathf.Clamp01(age / PoundWindup);
                    animationMesh.SetAttackPose(Current, Progress);
                    actor.SetPose(Vector2.zero, -.22f * Mathf.SmoothStep(0, 1, Progress), 0);
                    if (Progress >= 1) LaunchPound(arena.Player.transform.position);
                    break;
                case State.PoundAir:
                    Progress = Mathf.Clamp01(age / PoundAirDuration);
                    float lift = Mathf.Sin(Progress * Mathf.PI) * 1.55f;
                    float brace = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.70f, 1, Progress));
                    animationMesh.SetAttackPose(Current, Progress);
                    SetGroundPosition(Vector2.Lerp(poundOrigin, poundLanding, Progress * Progress));
                    actor.SetPose(Vector2.zero, lift + PoundLanded.y * brace, PoundLanded.z * LockedAim.x * brace, labelLift: lift);
                    if (Progress >= 1) LandPound();
                    break;
                case State.Recovery:
                    Progress = Mathf.Clamp01(age / RecoveryDuration);
                    animationMesh.SetAttackPose(Current, Progress, poundRecovery);
                    PresentPose((poundRecovery ? PoundLanded : PalmFollow) * (1 - Mathf.SmoothStep(0, 1, Progress)));
                    if (Progress >= 1) { Current = State.Pursuit; age = Progress = 0; }
                    break;
            }
        }

        void PresentPose(Vector3 pose) => actor.SetPose(LockedAim * pose.x, pose.y, LockedAim.x * pose.z);

        void DrawPalmSweep(float progress)
        {
            float head = Mathf.SmoothStep(0, 1, progress);
            float tail = Mathf.Max(0, head - .36f);
            float alpha = Mathf.InverseLerp(.12f, .34f, progress) * (1 - Mathf.InverseLerp(.60f, .93f, progress));
            swipe.enabled = alpha > 0;
            if (!swipe.enabled) return;
            Color color = new Color(1, .81f, .43f, alpha * .9f);
            swipe.startColor = new Color(color.r, color.g, color.b, alpha * .12f);
            swipe.endColor = color;
            for (int i = 0; i < swipe.positionCount; i++)
            {
                float t = i / (float)(swipe.positionCount - 1);
                float angle = Mathf.Lerp(-57.5f, 57.5f, Mathf.Lerp(tail, head, t));
                swipe.SetPosition(i, DemoArt.Direction(angle) * (enemy.attackRange * .86f));
            }
        }

        void Begin(State state, Vector2 aim)
        {
            Current = state; age = Progress = 0; hit = false;
            LockedAim = aim.sqrMagnitude > .001f ? aim.normalized : Vector2.right;
            body.linearVelocity = Vector2.zero;
            warning.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(LockedAim.y, LockedAim.x) * Mathf.Rad2Deg);
            warning.localScale = Vector3.one;
            palm.enabled = state == State.PalmWindup;
            swipe.enabled = false;
            if (state == State.PalmWindup)
            { PalmCasts++; palm.transform.localScale = Vector3.one * (enemy.attackRange / 1.15f); }
            else { PoundCasts++; nextPound = clock + PoundCooldown; }
            warning.gameObject.SetActive(state == State.PalmWindup);
        }

        void LaunchPound(Vector2 target)
        {
            poundOrigin = body.position;
            poundTarget = target;
            poundLanding = FindOpenLanding(ReachableLanding());
            Vector2 aim = target - poundOrigin;
            if (aim.sqrMagnitude > .001f) LockedAim = aim.normalized;
            pound.transform.position = new Vector3(target.x, target.y, -.025f);
            pound.enabled = true;
            groundCollider.enabled = false;
            Current = State.PoundAir;
            age = Progress = 0;
            actor.SetPose(Vector2.zero, 0, 0);
        }

        Vector2 ReachableLanding()
        {
            if (navigation.Walkable(poundTarget) && navigation.Clear(poundOrigin, poundTarget)) return poundTarget;
            float clear = 0, blocked = 1;
            for (int i = 0; i < 10; i++)
            {
                float fraction = (clear + blocked) * .5f;
                Vector2 point = Vector2.Lerp(poundOrigin, poundTarget, fraction);
                if (navigation.Walkable(point) && navigation.Clear(poundOrigin, point)) clear = fraction;
                else blocked = fraction;
            }
            return Vector2.Lerp(poundOrigin, poundTarget, clear);
        }

        Vector2 FindOpenLanding(Vector2 point)
        {
            if (HasRoomToLand(point)) return point;
            // Multiple apes can aim at the same target. Give each body room while keeping the impact on its warning.
            for (int i = 0; i < 8; i++)
            {
                Vector2 nearby = point + DemoArt.Direction(i * 45) * (enemy.Radius * 2 + .05f);
                if (navigation.Walkable(nearby) && navigation.Clear(poundOrigin, nearby) && HasRoomToLand(nearby))
                    return nearby;
            }
            return poundOrigin;
        }

        bool HasRoomToLand(Vector2 point)
        {
            foreach (var other in arena.Enemies)
            {
                if (other == enemy || other == null || !other.Alive || !other.gameObject.activeInHierarchy) continue;
                var combat = other.GetComponent<ApeCombat>();
                Vector2 occupied = combat != null && combat.IsAirborne ? combat.poundLanding : (Vector2)other.transform.position;
                if (Vector2.Distance(point, occupied) < enemy.Radius + other.Radius) return false;
            }
            return true;
        }

        void SetGroundPosition(Vector2 position)
        {
            body.position = position;
            transform.position = position;
        }

        void LandPound()
        {
            SetGroundPosition(FindOpenLanding(poundLanding));
            Recover();
            groundCollider.enabled = true;
            pound.enabled = false;
            impactPoint = poundTarget;
            impactAge = 0;
            DrawImpact();
            particles.ApeSlam(impactPoint, PoundRadius);
            if (Vector2.Distance(impactPoint, arena.Player.transform.position) <= PoundRadius + .29f)
                Damage(Mathf.RoundToInt(enemy.damage * 1.8f));
        }

        void Damage(int amount)
        { if (arena.Player.TakeDamage(amount, LockedAim)) DamageEvents++; }
        void DrawImpact()
        {
            float t = Mathf.Clamp01(impactAge / .36f);
            impact.enabled = t < 1;
            impact.transform.position = new Vector3(impactPoint.x, impactPoint.y, -.03f);
            impact.transform.localScale = Vector3.one * Mathf.Lerp(.35f, PoundRadius, Mathf.Sqrt(t));
            impact.startColor = impact.endColor = new Color(.95f, .84f, .45f, .85f * (1 - t));
        }
        void Recover()
        {
            poundRecovery = Current == State.PoundAir;
            Current = State.Recovery; age = Progress = 0;
            swipe.enabled = false;
            warning.gameObject.SetActive(false);
        }
        public void Stop()
        {
            if (IsAirborne)
            {
                SetGroundPosition(poundOrigin);
                groundCollider.enabled = enemy.Alive;
            }
            Current = State.Pursuit; age = Progress = 0;
            if (warning != null) warning.gameObject.SetActive(false);
            if (swipe != null) swipe.enabled = false;
            if (pound != null) pound.enabled = false;
            if (animationMesh != null) animationMesh.SetAttackPose(State.Pursuit, 0);
            if (actor != null) actor.SetPose(Vector2.zero, 0, 0);
            impactAge = 1; if (impact != null) impact.enabled = false;
        }
        void OnDisable() { Stop(); }
        void OnDestroy()
        {
            if (pound != null) Destroy(pound.gameObject);
            if (impact != null) Destroy(impact.gameObject);
        }
    }
}
