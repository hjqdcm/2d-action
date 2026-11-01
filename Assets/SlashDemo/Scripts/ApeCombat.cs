using UnityEngine;

namespace SlashDemo
{
    public sealed class ApeCombat : MonoBehaviour
    {
        public enum State { Pursuit, PalmWindup, PalmSwing, PoundWindup, PoundAir, Recovery }
        public const float PalmDuration = .28f, PoundWindup = .9f, PoundAirDuration = .44f;
        public const float PoundRadius = 2.45f, PoundCooldown = 6.5f, RecoveryDuration = .8f;
        public State Current { get; private set; }
        public bool IsChasing => Current == State.Pursuit;
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
        Transform warning;
        LineRenderer palm, pound, impact;
        float age, clock, nextPound;
        float impactAge = 1;
        bool hit;

        public void Initialize(SlashEnemy owner, SlashArena world, ObliqueActor presentation, Transform groundWarning)
        {
            enemy = owner; arena = world; actor = presentation; warning = groundWarning;
            animationMesh = GetComponent<ApeAnimation>();
            palm = warning.GetComponentInChildren<LineRenderer>(true);
            pound = DemoArt.Ring("Ape ground pound warning", warning, PoundRadius,
                DemoArt.AttackWarningColor, .055f * DemoArt.AttackWarningWidthScale, -69);
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
                    animationMesh.SetAttack(Progress, 0);
                    actor.SetPose(-LockedAim * (.1f * Progress), -.05f * Progress, 5 * LockedAim.x * Progress);
                    if (Progress >= 1) { Current = State.PalmSwing; age = 0; warning.gameObject.SetActive(false); }
                    break;
                case State.PalmSwing:
                    Progress = Mathf.Clamp01(age / PalmDuration);
                    animationMesh.SetAttack(1 - Progress, Mathf.Sin(Progress * Mathf.PI));
                    actor.SetPose(LockedAim * (.14f * Mathf.Sin(Progress * Mathf.PI)), 0, -8 * LockedAim.x * Progress);
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
                    animationMesh.SetAttack(Progress, 0);
                    actor.SetPose(Vector2.zero, -.16f * Progress, 0);
                    if (Progress >= 1) { Current = State.PoundAir; age = 0; }
                    break;
                case State.PoundAir:
                    Progress = Mathf.Clamp01(age / PoundAirDuration);
                    float lift = Mathf.Sin(Mathf.Clamp01(Progress / .78f) * Mathf.PI) * .85f;
                    animationMesh.SetAttack(1 - Progress, Mathf.SmoothStep(0, 1, Progress));
                    actor.SetPose(Vector2.zero, lift, 0);
                    if (!hit && Progress >= .78f)
                    {
                        hit = true; warning.gameObject.SetActive(false);
                        impactAge = 0; DrawImpact();
                        particles.ApeSlam(transform.position, PoundRadius);
                        if (Vector2.Distance(transform.position, arena.Player.transform.position) <= PoundRadius + .29f)
                            Damage(Mathf.RoundToInt(enemy.damage * 1.8f));
                    }
                    if (Progress >= 1) Recover();
                    break;
                case State.Recovery:
                    Progress = Mathf.Clamp01(age / RecoveryDuration);
                    animationMesh.SetAttack(0, .3f * (1 - Progress));
                    actor.SetPose(Vector2.zero, -.06f * (1 - Progress), 0);
                    if (Progress >= 1) { Current = State.Pursuit; age = Progress = 0; }
                    break;
            }
        }
        void Begin(State state, Vector2 aim)
        {
            Current = state; age = Progress = 0; hit = false;
            LockedAim = aim.sqrMagnitude > .001f ? aim.normalized : Vector2.right;
            GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            warning.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(LockedAim.y, LockedAim.x) * Mathf.Rad2Deg);
            warning.localScale = Vector3.one;
            palm.enabled = state == State.PalmWindup; pound.enabled = state == State.PoundWindup;
            if (state == State.PalmWindup)
            { PalmCasts++; palm.transform.localScale = Vector3.one * (enemy.attackRange / 1.15f); }
            else { PoundCasts++; nextPound = clock + PoundCooldown; }
            warning.gameObject.SetActive(true);
        }
        void Damage(int amount)
        { if (arena.Player.TakeDamage(amount, LockedAim)) DamageEvents++; }
        void DrawImpact()
        {
            float t = Mathf.Clamp01(impactAge / .36f);
            impact.enabled = t < 1;
            impact.transform.position = new Vector3(transform.position.x, transform.position.y, -.03f);
            impact.transform.localScale = Vector3.one * Mathf.Lerp(.35f, PoundRadius, Mathf.Sqrt(t));
            impact.startColor = impact.endColor = new Color(.95f, .84f, .45f, .85f * (1 - t));
        }
        void Recover()
        { Current = State.Recovery; age = Progress = 0; warning.gameObject.SetActive(false); }
        public void Stop()
        {
            Current = State.Pursuit; age = Progress = 0;
            if (warning != null) warning.gameObject.SetActive(false);
            if (animationMesh != null) animationMesh.SetAttack(0, 0);
            if (actor != null) actor.SetPose(Vector2.zero, 0, 0);
            impactAge = 1; if (impact != null) impact.enabled = false;
        }
        void OnDisable() { Stop(); }
        void OnDestroy() { if (impact != null) Destroy(impact.gameObject); }
    }
}
