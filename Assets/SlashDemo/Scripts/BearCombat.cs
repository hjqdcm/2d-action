using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    // This is the bear's complete attack controller; SlashEnemy only supplies health/hit reactions.
    public sealed class BearCombat : MonoBehaviour
    {
        public enum State { Pursuit, ChargeWindup, Charging, ClawWindup, ClawSwing, Recovery }
        public float chargeWindup = 1f, chargeSpeed = 10, chargeDistance = 8.5f;
        public float chargeCooldown = 3.5f, clawCooldown = 1.8f;
        public const float ClawSwingDuration = .32f;
        public State Current { get; private set; }
        public Vector2 LockedAim { get; private set; }
        public bool IsChasing => Current == State.Pursuit;
        public bool IsUninterruptible => enemy != null && enemy.Alive;
        public bool SkillLocked => Current != State.Pursuit;
        public float Progress { get; private set; }
        public int ChargeCasts { get; private set; }
        public int ClawCasts { get; private set; }
        public int DamageEvents { get; private set; }
        SlashEnemy enemy;
        SlashArena arena;
        BearDemon bear;
        BearPackSteering steering;
        BearWalkMesh mesh;
        ObliqueActor actor;
        Rigidbody2D body;
        Transform warning;
        LineRenderer arc, lane, swipe;
        BearChargeWind wind;
        BeastAttackParticles particles;
        float particleTimer;
        float age, chargeTravel, nextCharge, nextClaw;
        bool hit;
        bool recoverClaw;
        sealed class AllyCollision
        {
            public Collider2D a,b;
            public bool originallyIgnored;
            public int users;
            public (Collider2D,Collider2D) key;
        }
        static readonly Dictionary<(Collider2D,Collider2D),AllyCollision> allyCollisions = new Dictionary<(Collider2D,Collider2D),AllyCollision>();
        readonly List<AllyCollision> ownedAllyCollisions = new List<AllyCollision>();
        Vector2 lastSafeChargePosition;

        public void Initialize(SlashEnemy owner, SlashArena world, ObliqueActor presentation, Transform groundWarning)
        {
            enemy = owner; arena = world; actor = presentation; warning = groundWarning;
            bear = GetComponent<BearDemon>(); mesh = GetComponent<BearWalkMesh>(); body = GetComponent<Rigidbody2D>();
            steering = GetComponent<BearPackSteering>();
            arc = warning.GetComponentInChildren<LineRenderer>(true);
            lane = DemoArt.Line("Charge corridor", warning, new Vector3[5], DemoArt.AttackWarningColor, DemoArt.BeastAttackWarningWidth, -69);
            lane.loop = true;
            swipe = DemoArt.Line("Three claw trails", warning, new Vector3[2], new Color(1,.85f,.5f,.9f), .065f, -68);
            warning.gameObject.SetActive(false);
            nextCharge = Time.time + 2;
            wind = gameObject.AddComponent<BearChargeWind>();
            wind.Initialize(world);
            particles=BeastAttackParticles.For(world,transform);
        }

        public void Tick(float dt)
        {
            if (arena.Paused || dt <= 0) return;
            if (!enemy.Alive || arena.Player == null || !arena.Player.IsAlive) { Stop(); return; }
            age += dt;
            Vector2 toPlayer = (Vector2)arena.Player.transform.position - body.position;
            switch (Current)
            {
                case State.Pursuit:
                    mesh.SetSkillPose(0,-1,0,Vector2.zero);
                    actor.SetPose(Vector2.zero,0,0);
                    if (toPlayer.magnitude <= enemy.attackRange && Time.time >= nextClaw
                        && (steering == null || steering.HasRoom(body.position)))
                        Begin(State.ClawWindup, toPlayer);
                    else if (toPlayer.magnitude >= 2.8f && toPlayer.magnitude <= chargeDistance
                        && Time.time >= nextCharge && ClearLane(toPlayer))
                        Begin(State.ChargeWindup, toPlayer);
                    break;
                case State.ChargeWindup:
                    Progress = Mathf.Clamp01(age / chargeWindup);
                    mesh.SetSkillPose(0,-1,Progress,LockedAim);
                    actor.SetPose(-LockedAim * (.15f * Progress), -.08f * Progress, -5 * LockedAim.x * Progress);
                    if (age >= chargeWindup)
                    {
                        Current = State.Charging; age = chargeTravel = 0; particleTimer=0;
                        lastSafeChargePosition = body.position;
                        IgnoreAlliesDuringCharge();
                        nextCharge = Time.time + chargeCooldown;
                        lane.enabled = false; warning.gameObject.SetActive(false);
                    }
                    break;
                case State.Charging:
                    mesh.SetSkillPose(0,-1,.7f,LockedAim);
                    actor.SetPose(LockedAim * .13f,-.05f,-9 * LockedAim.x);
                    if (age >= chargeDistance / chargeSpeed + .15f) Recover();
                    break;
                case State.ClawWindup:
                    Progress = Mathf.Clamp01(age / enemy.windupTime);
                    mesh.SetSkillPose(Mathf.SmoothStep(0,1,Progress),-1,0,LockedAim);
                    actor.SetPose(-LockedAim * (.08f * Progress),.04f * Progress,6 * LockedAim.x * Progress);
                    if (age >= enemy.windupTime)
                    {
                        Current = State.ClawSwing; age = 0;
                        nextClaw = Time.time + clawCooldown;
                        arc.enabled = false; swipe.enabled = true;
                    }
                    break;
                case State.ClawSwing:
                    Progress = Mathf.Clamp01(age / ClawSwingDuration);
                    mesh.SetSkillPose(1-Progress,Progress,0,LockedAim);
                    actor.SetPose(LockedAim * (Mathf.Sin(Progress * Mathf.PI) * .16f),0,-8 * LockedAim.x);
                    DrawSwipe(Progress);
                    if (!hit && Progress >= .45f)
                    {
                        hit = true;
                        if (SlashPlayer.InsideArc(body.position,LockedAim,arena.Player.transform.position,enemy.attackRange,105,.29f))
                            Damage(enemy.damage);
                        arena.Effects.Burst(body.position + LockedAim * .9f,DemoArt.Gold,10,.5f);
                        particles.BearClaw(body.position,LockedAim,enemy.attackRange);
                    }
                    if (Progress >= 1) Recover();
                    break;
                case State.Recovery:
                    mesh.SetSkillPose(0,recoverClaw ? Mathf.Lerp(1,38f/60f,Mathf.Clamp01(age/.35f)) : -1,0,LockedAim);
                    actor.SetPose(Vector2.zero,0,0);
                    if (age >= .85f) { Current = State.Pursuit; age = 0; }
                    break;
            }
            wind.Present(Current == State.Charging,LockedAim,age);
            if(Current==State.ChargeWindup || Current==State.Charging)
            {
                particleTimer-=dt;
                if(particleTimer<=0) { particles.ChargeTrail(body.position,LockedAim,Current==State.Charging); particleTimer=Current==State.Charging?.05f:.13f; }
            }
            else particleTimer=0;
        }

        void Begin(State state, Vector2 aim)
        {
            Current = state; age = 0; hit = false; Progress = 0;
            LockedAim = aim.sqrMagnitude > .001f ? aim.normalized : Vector2.right;
            body.linearVelocity = Vector2.zero;
            warning.gameObject.SetActive(true); warning.localScale = Vector3.one;
            warning.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(LockedAim.y,LockedAim.x)*Mathf.Rad2Deg);
            arc.enabled = state == State.ClawWindup; lane.enabled = state == State.ChargeWindup; swipe.enabled = false;
            if (state == State.ChargeWindup)
            {
                ChargeCasts++;
                float half = enemy.Radius + .15f;
                lane.SetPositions(new[] { new Vector3(0,-half),new Vector3(chargeDistance,-half),
                    new Vector3(chargeDistance,half),new Vector3(0,half),new Vector3(0,-half) });
            }
            else
            {
                ClawCasts++;
                arc.transform.localScale = Vector3.one * (enemy.attackRange / 1.15f);
            }
        }

        bool ClearLane(Vector2 target)
        {
            foreach (var cast in Physics2D.CircleCastAll(body.position,enemy.Radius,target.normalized,target.magnitude))
                if (!cast.collider.isTrigger && !cast.transform.IsChildOf(transform)
                    && !IsAlly(cast.collider) && cast.collider.GetComponent<SlashPlayer>() == null) return false;
            return true;
        }

        void DrawSwipe(float t)
        {
            // Three separated claw scratches sweep across the ground with the forepaw.
            const int points = 20;
            swipe.positionCount = points * 3;
            for (int claw = 0; claw < 3; claw++) for (int i = 0; i < points; i++)
            {
                float angle = Mathf.Lerp(52,-52,Mathf.Clamp01(t + (i / (float)(points-1) - 1) * .28f));
                Vector2 p = DemoArt.Direction(angle) * (enemy.attackRange * (.70f + claw * .11f));
                swipe.SetPosition(claw * points + i,p);
            }
        }

        public void FixedTick(Vector2 knockback)
        {
            if (!enemy.Alive || arena.Paused || arena.Player == null || !arena.Player.IsAlive)
            { body.linearVelocity = Vector2.zero; return; }
            if (Current == State.Charging)
            {
                IgnoreAlliesDuringCharge();
                if (!OverlapsAlly(body.position)) lastSafeChargePosition = body.position;
                float distance = Mathf.Min(chargeSpeed * Time.fixedDeltaTime, chargeDistance - chargeTravel);
                float nearest = distance + .02f;
                Collider2D obstacle = null;
                foreach (var cast in Physics2D.CircleCastAll(body.position,enemy.Radius,LockedAim,distance + .02f))
                    if (!cast.collider.isTrigger && !cast.transform.IsChildOf(transform) && !IsAlly(cast.collider) && cast.distance < nearest)
                    { nearest = cast.distance; obstacle = cast.collider; }
                if (obstacle != null)
                {
                    if (!hit && obstacle.GetComponent<SlashPlayer>() == arena.Player)
                    { hit = true; Damage(Mathf.RoundToInt(enemy.damage * 1.5f)); }
                    arena.Effects.Burst(body.position + LockedAim * enemy.Radius,DemoArt.Gold,14,.8f);
                    particles.BearImpact(body.position+LockedAim*enemy.Radius,LockedAim);
                    Recover(); return;
                }
                body.linearVelocity = LockedAim * (distance / Time.fixedDeltaTime);
                chargeTravel += distance;
                if (chargeTravel >= chargeDistance) Recover();
                return;
            }
            Vector2 velocity = knockback;
            if (Current == State.Pursuit)
            {
                if (steering != null) velocity += steering.Velocity(body.position,arena.Player.transform.position);
                else if (Vector2.Distance(body.position,arena.Player.transform.position) > enemy.attackRange * .9f)
                    velocity += bear.ChaseDirection(body.position,arena.Player.transform.position) * enemy.moveSpeed;
            }
            body.linearVelocity = bear.ConstrainVelocity(velocity,Time.fixedDeltaTime);
        }

        void Damage(int amount)
        { if (arena.Player.TakeDamage(amount,LockedAim)) DamageEvents++; }

        void Recover()
        {
            if (Current == State.Charging) FinishChargeCollisions(true);
            recoverClaw = Current == State.ClawSwing;
            Current = State.Recovery; age = 0; body.linearVelocity = Vector2.zero;
            warning.gameObject.SetActive(false);
            wind.Hide();
        }
        public void Interrupt(float duration)
        {
            // Damage cannot cancel any phase. Stop remains available for death/scene cleanup.
        }
        public void Stop()
        {
            FinishChargeCollisions(enemy != null && enemy.Alive && gameObject.activeInHierarchy);
            if (wind != null) wind.Hide();
            if (warning != null) warning.gameObject.SetActive(false);
            if (mesh != null) mesh.SetSkillPose(0,-1,0,Vector2.zero);
            if (body != null) body.linearVelocity = Vector2.zero;
            Current = State.Pursuit; age = 0;
        }
        void OnDisable() { Stop(); }

        bool IsAlly(Collider2D collider)
        {
            var other=collider.GetComponentInParent<SlashEnemy>();
            return other!=null && other!=enemy && other.rockBear;
        }

        void IgnoreAlliesDuringCharge()
        {
            var own=GetComponent<CircleCollider2D>();
            foreach(var other in arena.Enemies)
            {
                if(other==null || other==enemy || !other.rockBear || !other.Alive || !other.gameObject.activeInHierarchy) continue;
                var collider=other.GetComponent<CircleCollider2D>();
                if(collider==null || !collider.enabled) continue;
                (Collider2D,Collider2D) key=(own,collider);
                if(!allyCollisions.TryGetValue(key,out var pair) && !allyCollisions.TryGetValue((collider,own),out pair))
                {
                    pair=new AllyCollision { a=own,b=collider,key=key,originallyIgnored=Physics2D.GetIgnoreCollision(own,collider) };
                    allyCollisions.Add(key,pair);
                }
                if(ownedAllyCollisions.Contains(pair)) continue;
                ownedAllyCollisions.Add(pair); pair.users++;
                Physics2D.IgnoreCollision(own,collider,true);
            }
        }

        bool OverlapsAlly(Vector2 point)
        {
            foreach(var collider in Physics2D.OverlapCircleAll(point,enemy.Radius+.025f))
                if(!collider.isTrigger && IsAlly(collider)) return true;
            return false;
        }

        bool FreeLanding(Vector2 point)
        {
            foreach(var collider in Physics2D.OverlapCircleAll(point,enemy.Radius+.025f))
                if(!collider.isTrigger && !collider.transform.IsChildOf(transform)) return false;
            Vector2 delta=point-body.position;
            foreach(var cast in Physics2D.CircleCastAll(body.position,enemy.Radius,delta.normalized,delta.magnitude))
                if(!cast.collider.isTrigger && !cast.transform.IsChildOf(transform) && !IsAlly(cast.collider)) return false;
            return true;
        }

        void FinishChargeCollisions(bool resolveOverlap)
        {
            if(ownedAllyCollisions.Count==0) return;
            if(resolveOverlap && OverlapsAlly(body.position))
            {
                bool found=false;
                // Find a nearby free landing without crossing walls or the player.
                for(int ring=1;ring<=12 && !found;ring++) for(int spoke=0;spoke<16;spoke++)
                {
                    Vector2 candidate=body.position+DemoArt.Direction(spoke*22.5f)*(ring*.25f);
                    if(!FreeLanding(candidate)) continue;
                    body.position=candidate; found=true; break;
                }
                if(!found && FreeLanding(lastSafeChargePosition)) body.position=lastSafeChargePosition;
                Physics2D.SyncTransforms();
            }
            foreach(var pair in ownedAllyCollisions)
            {
                if(--pair.users>0) continue;
                if(pair.a!=null && pair.b!=null && pair.a.enabled && pair.b.enabled
                    && pair.a.gameObject.activeInHierarchy && pair.b.gameObject.activeInHierarchy)
                    Physics2D.IgnoreCollision(pair.a,pair.b,pair.originallyIgnored);
                allyCollisions.Remove(pair.key);
            }
            ownedAllyCollisions.Clear();
        }
    }
}
