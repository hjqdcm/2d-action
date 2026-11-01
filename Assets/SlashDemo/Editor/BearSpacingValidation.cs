using System;
using System.Reflection;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class BearSpacingValidation
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        public static void Verify(SlashArena arena)
        {
            var bears=new[] {arena.Enemies[0],arena.Enemies[1],arena.Enemies[2]};
            var bodies=new Rigidbody2D[3]; var combat=new BearCombat[3];
            var original=new Vector2[3]; var interpolation=new RigidbodyInterpolation2D[3];
            Vector2 player=arena.Player.transform.position;
            var mode=Physics2D.simulationMode;
            var nextClaw=typeof(BearCombat).GetField("nextClaw",BindingFlags.NonPublic|BindingFlags.Instance);
            var nextCharge=typeof(BearCombat).GetField("nextCharge",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int i=0;i<3;i++)
            {
                bodies[i]=bears[i].GetComponent<Rigidbody2D>(); combat[i]=bears[i].GetComponent<BearCombat>();
                original[i]=bodies[i].position; interpolation[i]=bodies[i].interpolation;
                combat[i].Stop(); bodies[i].interpolation=RigidbodyInterpolation2D.None;
                nextClaw.SetValue(combat[i],Time.time+1000); nextCharge.SetValue(combat[i],Time.time+1000);
            }
            void Arrange(Vector2 center)
            {
                arena.Player.Teleport(center);
                for(int i=0;i<3;i++)
                {
                    combat[i].Stop(); bodies[i].position=center+new Vector2((i-1)*2.4f,4);
                    bodies[i].linearVelocity=Vector2.zero;
                }
                Physics2D.SyncTransforms();
            }
            void Simulate(int ticks)
            {
                for(int tick=0;tick<ticks;tick++)
                {
                    for(int i=0;i<3;i++) if(bears[i].gameObject.activeInHierarchy) combat[i].FixedTick(Vector2.zero);
                    // Match the player's normal FixedUpdate with no movement input.
                    // Manual physics alone otherwise retains contact velocity indefinitely.
                    arena.Player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                    Physics2D.Simulate(.02f);
                }
            }
            void Spread(string label)
            {
                Vector2 center=arena.Player.transform.position;
                float minimum=float.PositiveInfinity;
                for(int i=0;i<3;i++)
                {
                    Check(Vector2.Distance(bodies[i].position,center)<4.5f,label+": bear failed to approach player: "+bodies[i].position);
                    for(int j=i+1;j<3;j++) minimum=Mathf.Min(minimum,Vector2.Distance(bodies[i].position,bodies[j].position));
                }
                Debug.Log("BEAR_SPACING_CASE: "+label+" min distance="+minimum+" positions="+bodies[0].position+", "+bodies[1].position+", "+bodies[2].position);
                Check(minimum>2.75f,label+": bears remained crowded.");
            }
            try
            {
                Physics2D.simulationMode=SimulationMode2D.Script;
                Arrange(Vector2.zero); Simulate(650); Spread("same-side approach");
                Check(Mathf.Min(bodies[0].position.y,Mathf.Min(bodies[1].position.y,bodies[2].position.y))<-.5f,
                    "No bear moved around to the player's rear flank.");
                arena.Player.Teleport(new Vector2(3,1)); Simulate(500); Spread("player moved");
                Arrange(arena.Travel.ActiveMap.Definition.Entry); Simulate(600);
                Debug.Log("BEAR_ENTRANCE: player="+arena.Player.transform.position+" bodies="+bodies[0].position+","+bodies[1].position+","+bodies[2].position);
                float nearestEntranceBear=float.PositiveInfinity;
                for(int i=0;i<3;i++)
                {
                    float distance=Vector2.Distance(bodies[i].position,arena.Player.transform.position);
                    nearestEntranceBear=Mathf.Min(nearestEntranceBear,distance);
                    Check(distance<5,"Bear "+i+" stalled near entrance at "+bodies[i].position);
                    for(int j=0;j<i;j++)
                        Check(Vector2.Distance(bodies[i].position,bodies[j].position)>=bears[i].Radius+bears[j].Radius-.02f,"Entrance queue overlaps bears.");
                    foreach(var collider in Physics2D.OverlapCircleAll(bodies[i].position,bears[i].Radius-.07f))
                        if(!collider.isTrigger && collider.transform.IsChildOf(arena.Travel.ActiveMap.transform)
                            && collider.GetComponent<SlashEnemy>()==null) throw new Exception("Pack steering crossed map boundary.");
                }
                Check(nearestEntranceBear<bears[0].attackRange+.5f,"No bear reached attack range at the narrow entrance.");
                Arrange(Vector2.zero);
                bodies[0].position=new Vector2(0,2.5f); bodies[1].position=new Vector2(0,4.8f); bodies[2].position=new Vector2(-8,6);
                Physics2D.SyncTransforms(); nextClaw.SetValue(combat[0],-1f); combat[0].Tick(.01f);
                Check(combat[0].Current==BearCombat.State.Pursuit,"Crowded bear locked itself in repeated claw attacks.");
                bodies[1].position=new Vector2(8,6); Physics2D.SyncTransforms(); combat[0].Tick(.01f);
                Check(combat[0].Current==BearCombat.State.ClawWindup,"Roomy bear could no longer claw."); combat[0].Stop();
                bears[1].gameObject.SetActive(false); bears[2].gameObject.SetActive(false);
                bodies[0].position=new Vector2(0,5); Simulate(300);
                Check(Vector2.Distance(bodies[0].position,Vector2.zero)<bears[0].attackRange+.2f,"Lone bear stopped pursuing normally.");
                Debug.Log("BEAR_SPACING_PASS: spread from one side, follow moving player, rear flank, map-edge clearance, crowded claw gating, normal claw and lone survivor pursuit.");
            }
            finally
            {
                for(int i=0;i<3;i++)
                {
                    bears[i].gameObject.SetActive(true); combat[i].Stop(); bodies[i].position=original[i]; bodies[i].linearVelocity=Vector2.zero;
                    bodies[i].interpolation=interpolation[i]; nextClaw.SetValue(combat[i],Time.time); nextCharge.SetValue(combat[i],Time.time+2);
                }
                arena.Player.Teleport(player); Physics2D.SyncTransforms(); Physics2D.simulationMode=mode;
            }
        }
    }
}
