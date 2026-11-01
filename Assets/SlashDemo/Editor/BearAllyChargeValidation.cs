using System;
using System.Reflection;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class BearAllyChargeValidation
    {
        static readonly FieldInfo NextCharge=typeof(BearCombat).GetField("nextCharge",BindingFlags.NonPublic|BindingFlags.Instance);
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }

        public static void Verify(SlashArena arena)
        {
            var bears=new[] { arena.Enemies[0],arena.Enemies[1],arena.Enemies[2] };
            var combat=new BearCombat[3]; var bodies=new Rigidbody2D[3]; var colliders=new CircleCollider2D[3];
            var positions=new Vector2[3]; var speeds=new float[3]; var interpolation=new RigidbodyInterpolation2D[3];
            for(int i=0;i<3;i++)
            {
                combat[i]=bears[i].GetComponent<BearCombat>(); bodies[i]=bears[i].GetComponent<Rigidbody2D>(); colliders[i]=bears[i].GetComponent<CircleCollider2D>();
                positions[i]=bodies[i].position; speeds[i]=bears[i].moveSpeed; interpolation[i]=bodies[i].interpolation;
                bears[i].moveSpeed=0; bodies[i].interpolation=RigidbodyInterpolation2D.None;
            }
            Vector2 playerPosition=arena.Player.transform.position;
            int health=arena.Player.Health;
            bool invincible=arena.Player.invincible;
            var mode=Physics2D.simulationMode;
            GameObject wall=null;
            void Arrange(float start=-5,float ally=0,float target=3)
            {
                for(int i=0;i<3;i++) { combat[i].Stop(); bodies[i].linearVelocity=Vector2.zero; }
                bodies[0].position=new Vector2(start,0); bodies[1].position=new Vector2(ally,0); bodies[2].position=new Vector2(-8,6);
                arena.Player.Teleport(new Vector2(target,0));
                Physics2D.SyncTransforms();
            }
            void Begin(int index)
            {
                NextCharge.SetValue(combat[index],-1f);
                combat[index].Tick(.01f);
                Check(combat[index].Current==BearCombat.State.ChargeWindup,"Ally still blocks selecting charge.");
                combat[index].Tick(combat[index].chargeWindup+.01f);
                Check(combat[index].Current==BearCombat.State.Charging,"Charge did not start.");
            }
            float Run()
            {
                float maximum=bodies[0].position.x;
                for(int tick=0;tick<120 && combat[0].Current==BearCombat.State.Charging;tick++)
                {
                    combat[0].FixedTick(Vector2.zero);
                    Physics2D.Simulate(.02f);
                    maximum=Mathf.Max(maximum,bodies[0].position.x);
                    combat[0].Tick(.02f);
                }
                Check(combat[0].Current==BearCombat.State.Recovery,"Charge did not complete.");
                return maximum;
            }
            void VerifyPlayerCannotPush()
            {
                var playerBody=arena.Player.GetComponent<Rigidbody2D>();
                float clearance=bears[0].Radius+arena.Player.GetComponent<CircleCollider2D>().radius;
                foreach(float speed in new[] { 5f,22f })
                {
                    Arrange(0,7,0);
                    arena.Player.Teleport(Vector2.left*(clearance+.05f));
                    Physics2D.SyncTransforms();
                    for(int tick=0;tick<120;tick++)
                    {
                        bodies[0].linearVelocity=Vector2.zero;
                        playerBody.linearVelocity=Vector2.right*speed;
                        Physics2D.Simulate(.02f);
                    }
                    Check(bodies[0].position.sqrMagnitude<.00000001f,"Player walking/dashing pushed stationary bear.");
                    Check(playerBody.position.x<=-clearance+.03f,"Player passed through stationary bear.");
                }
                Arrange(0,7,0);
                arena.Player.Teleport(Vector2.left*(clearance+.05f));
                Physics2D.SyncTransforms();
                for(int tick=0;tick<80;tick++)
                {
                    Vector2 before=bodies[0].position;
                    bodies[0].linearVelocity=bears[0].GetComponent<BearDemon>().ConstrainVelocity(Vector2.right*1.65f,.02f);
                    Vector2 expected=before+bodies[0].linearVelocity*.02f;
                    playerBody.linearVelocity=Vector2.right*5;
                    Physics2D.Simulate(.02f);
                    Check(Vector2.Distance(expected,bodies[0].position)<.0001f,"Player changed moving bear's AI movement.");
                }
                Check(bodies[0].position.x>2,"Unpushable bear could not move by itself.");
                playerBody.linearVelocity=Vector2.zero;
                Debug.Log("BEAR_PLAYER_PUSH_PASS: sustained walking and dashing cannot move bear or pass through it; moving bear follows only AI velocity.");
            }
            try
            {
                Physics2D.simulationMode=SimulationMode2D.Script; arena.Player.invincible=false;
                VerifyPlayerCannotPush();
                Arrange(); int events=combat[0].DamageEvents; int allyHealth=bears[1].Health;
                Begin(0);
                Check(Physics2D.GetIgnoreCollision(colliders[0],colliders[1]),"Physical ally collisions were not suppressed.");
                int attackerHealth=bears[0].Health;
                bears[0].TakeHit(1,Vector2.left,30,true);
                Check(bears[0].Health==attackerHealth-1,"Uninterruptible dash incorrectly prevented damage.");
                Check(combat[0].Current==BearCombat.State.Charging,"Heavy hit interrupted charge.");
                combat[0].Interrupt(.5f);
                Check(combat[0].Current==BearCombat.State.Charging,"Direct interrupt cancelled charge.");
                Check(Physics2D.GetIgnoreCollision(colliders[0],colliders[1]),"Hit restored ally collision during charge.");
                Check(Run()>.4f,"Charge stopped at the ally instead of passing through.");
                Check(combat[0].DamageEvents==events+1,"Charge did not hit the player beyond its ally exactly once.");
                Check(bears[1].Health==allyHealth && bodies[1].position.magnitude<.02f,"Charge damaged or pushed the ally.");
                Check(Vector2.Distance(bodies[0].position,bodies[1].position)>=bears[0].Radius+bears[1].Radius-.01f,"Charge landed inside the ally.");
                Check(!Physics2D.GetIgnoreCollision(colliders[0],colliders[1]),"Normal ally collision was not restored.");

                Arrange(-5,-1,3); events=combat[0].DamageEvents;
                Begin(0);
                // Add a wall after windup, since a pre-existing wall correctly prevents selecting charge.
                wall=new GameObject("Ally charge test wall"); wall.transform.position=new Vector2(1,0);
                wall.AddComponent<BoxCollider2D>().size=new Vector2(.25f,5);
                Physics2D.SyncTransforms(); Run();
                Check(combat[0].DamageEvents==events,"Charge hit the player through a wall.");
                Check(bodies[0].position.x<1,"Charge crossed the wall.");
                Check(!Physics2D.GetIgnoreCollision(colliders[0],colliders[1]),"Wall stop leaked ignored collisions.");
                UnityEngine.Object.DestroyImmediate(wall); wall=null;

                Arrange(-4,0,4); Begin(0); Begin(1);
                combat[0].Stop();
                Check(Physics2D.GetIgnoreCollision(colliders[0],colliders[1]),"Ending one dash restored collision during the other dash.");
                combat[1].Stop();
                Check(!Physics2D.GetIgnoreCollision(colliders[0],colliders[1]),"Simultaneous dashes did not restore normal collisions.");

                Arrange(); Physics2D.IgnoreCollision(colliders[0],colliders[1],true);
                Begin(0); combat[0].Stop();
                Check(Physics2D.GetIgnoreCollision(colliders[0],colliders[1]),"Charge overwrote a pre-existing collision exclusion.");
                Physics2D.IgnoreCollision(colliders[0],colliders[1],false);

                Arrange(); Begin(0); bears[0].gameObject.SetActive(false); bears[0].gameObject.SetActive(true);
                Check(!Physics2D.GetIgnoreCollision(colliders[0],colliders[1]),"Disable/re-enable leaked ignored collisions.");
                Debug.Log("BEAR_ALLY_CHARGE_PASS: heavy hit damages without stun/knockback or cancelling charge; direct interrupt ignored; teammate pass, player hit, free landing, wall stop, simultaneous leases and cleanup.");
            }
            finally
            {
                if(wall!=null) UnityEngine.Object.DestroyImmediate(wall);
                for(int i=0;i<3;i++)
                {
                    combat[i].Stop(); bodies[i].position=positions[i]; bodies[i].linearVelocity=Vector2.zero;
                    bears[i].moveSpeed=speeds[i]; bodies[i].interpolation=interpolation[i];
                    NextCharge.SetValue(combat[i],Time.time+2);
                }
                arena.Player.Teleport(playerPosition);
                if(arena.Player.Health<health) arena.Player.Heal(health-arena.Player.Health);
                arena.Player.invincible=invincible; Physics2D.SyncTransforms(); Physics2D.simulationMode=mode;
            }
        }
    }
}
