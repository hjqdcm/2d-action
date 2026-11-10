using UnityEngine;

namespace SlashDemo
{
    // The damage event belongs to the fully extended bite, after the visible windup.
    public sealed class SnakeCombat : MonoBehaviour
    {
        public enum State { Pursuit, Windup, Bite, Recovery, VenomWindup, VenomSpit }
        public const float BiteDuration = .24f, RecoveryDuration = .75f;
        public const float VenomWindupDuration=1, VenomCooldown=6, VenomReach=8.5f;
        public Vector2 LockedTarget { get; private set; }
        public int VenomCasts { get; private set; }
        public State Current { get; private set; }
        public Vector2 LockedAim { get; private set; } = Vector2.right;
        public float Progress { get; private set; }
        public int BiteCasts { get; private set; }
        public int DamageEvents { get; private set; }
        public bool IsChasing => Current == State.Pursuit;
        SlashEnemy enemy;
        SlashArena arena;
        SnakeAnimation animationMesh;
        ObliqueActor actor;
        Transform warning;
        SerpentGazeVfx gaze;
        SerpentVenomField venom;
        BeastAttackParticles particles;
        float clock,nextVenom;
        float age;
        bool hit;

        public void Initialize(SlashEnemy owner, SlashArena world, ObliqueActor presentation, Transform groundWarning)
        {
            enemy = owner; arena = world; actor = presentation; warning = groundWarning;
            animationMesh = GetComponent<SnakeAnimation>();
            gaze=GetComponent<SerpentGazeVfx>();
            if(gaze==null) gaze=gameObject.AddComponent<SerpentGazeVfx>();
            gaze.Initialize(world);
            venom=SerpentVenomField.For(world,transform);
            particles=BeastAttackParticles.For(world,transform);
            nextVenom=2+GetComponent<SnakeDemon>().FormationSlot*SerpentVenomField.GroupInterval;
        }

        public void Tick(float dt)
        {
            if (arena.Paused || dt <= 0) return;
            if (!enemy.Alive || arena.Player == null || !arena.Player.IsAlive) { Stop(); return; }
            gaze.Advance(dt);
            clock+=dt;
            age += dt;
            Vector2 delta = arena.Player.transform.position - transform.position;
            switch (Current)
            {
                case State.Pursuit:
                    animationMesh.SetAttackPose(0, 0, LockedAim);
                    if (delta.magnitude <= enemy.attackRange)
                    {
                        LockedAim = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.right;
                        Current = State.Windup; age = Progress = 0; hit = false; BiteCasts++;
                        warning.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(LockedAim.y, LockedAim.x) * Mathf.Rad2Deg);
                        warning.localScale = Vector3.one * (.25f * enemy.attackRange / 1.15f);
                        warning.gameObject.SetActive(true);
                        gaze.Gather(0,LockedAim,enemy.attackRange);
                    }
                    else if(delta.magnitude<=VenomReach && clock>=nextVenom
                        && GetComponent<SnakeDemon>().Navigation.Clear(transform.position,arena.Player.transform.position)
                        && venom.TryReserve(this,arena.Player.transform.position))
                    {
                        LockedTarget=arena.Player.transform.position; LockedAim=delta.normalized;
                        Current=State.VenomWindup; age=Progress=0; gaze.Hide(); warning.gameObject.SetActive(false);
                        GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                    }
                    break;
                case State.VenomWindup:
                    Progress=Mathf.Clamp01(age/VenomWindupDuration);
                    animationMesh.SetAttackPose(Mathf.SmoothStep(0,1,Progress),0,LockedAim);
                    if(Progress>=1) { Current=State.VenomSpit; age=Progress=0; hit=false; }
                    break;
                case State.VenomSpit:
                    Progress=Mathf.Clamp01(age/BiteDuration);
                    animationMesh.SetAttackPose(1-Progress,Mathf.Sin(Progress*Mathf.PI),LockedAim);
                    if(!hit && Progress>=.35f)
                    {
                        hit=true; animationMesh.Refresh();
                        var skin=transform.Find("Visual/Jade snake skin");
                        venom.Launch(this,skin.TransformPoint(animationMesh.PartPoint(1,.725f,.725f)));
                        VenomCasts++; nextVenom=clock+VenomCooldown;
                    }
                    if(Progress>=1) { Current=State.Recovery; age=Progress=0; }
                    break;
                case State.Windup:
                    Progress = Mathf.Clamp01(age / Mathf.Max(.01f, enemy.windupTime));
                    animationMesh.SetAttackPose(Mathf.SmoothStep(0, 1, Progress), 0, LockedAim);
                    gaze.Gather(Progress,LockedAim,enemy.attackRange);
                    warning.localScale = Vector3.one * (Mathf.Lerp(.25f, 1, Progress) * enemy.attackRange / 1.15f);
                    if (Progress >= 1) { Current = State.Bite; age = Progress = 0; warning.gameObject.SetActive(false); }
                    break;
                case State.Bite:
                    Progress = Mathf.Clamp01(age / BiteDuration);
                    float extension = Mathf.Sin(Progress * Mathf.PI);
                    animationMesh.SetAttackPose(Mathf.Lerp(1, .3f, Progress), extension, LockedAim);
                    if (!hit && Progress >= .5f)
                    {
                        hit = true;
                        gaze.Fire(LockedAim,enemy.attackRange);
                        particles.Gaze(transform.position,LockedAim,enemy.attackRange);
                        if (SlashPlayer.InsideArc(transform.position, LockedAim, arena.Player.transform.position, enemy.attackRange, 105, .29f))
                        {
                            arena.Player.TakeDamage(enemy.damage, LockedAim); DamageEvents++;
                            arena.Effects.Burst((Vector2)transform.position + LockedAim * 1.2f, DemoArt.Jade, 6, .4f);
                            arena.Effects.Burst(arena.Player.transform.position,new Color(.64f,.67f,.48f),7,.45f);
                        }
                    }
                    if (Progress >= 1) { Current = State.Recovery; age = Progress = 0; }
                    break;
                case State.Recovery:
                    Progress = Mathf.Clamp01(age / RecoveryDuration);
                    animationMesh.SetAttackPose(.3f * (1 - Mathf.SmoothStep(0, 1, Progress)), 0, LockedAim);
                    if (Progress >= 1) { Current = State.Pursuit; age = Progress = 0; }
                    break;
            }
        }

        public void Stop()
        {
            Current = State.Pursuit; age = Progress = 0;
            if (warning != null) warning.gameObject.SetActive(false);
            if (animationMesh != null) animationMesh.SetAttackPose(0, 0, LockedAim);
            if(gaze!=null) gaze.Hide();
            if(venom!=null) venom.CancelCharge(this);
            if (actor != null) actor.SetPose(Vector2.zero, 0, 0);
        }
        void OnDisable() { Stop(); }
    }
}
