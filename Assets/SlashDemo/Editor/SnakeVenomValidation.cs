using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class SnakeVenomValidation
    {
        const string Key="SlashDemo.SnakeVenomValidation";
        static SlashArena arena;
        static SlashEnemy caster,keeper;
        static SnakeCombat combat;
        static SerpentVenomField field;
        static Vector2 target;
        static int stage,health,events;
        static float pausedClock,dotStart;
        static double deadline,next;
        static void Check(bool yes,string message) { if(!yes) throw new Exception(message); }
        static void Advance(int value,double delay=.3) { stage=value; next=EditorApplication.timeSinceStartup+delay; }
        public static void RunBatch()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Key,true); SessionState.SetBool(Key+".Launch",true);
        }
        [InitializeOnLoadMethod]
        static void Hook()
        { EditorApplication.update-=Tick; EditorApplication.update+=Tick; deadline=EditorApplication.timeSinceStartup+100; next=EditorApplication.timeSinceStartup+2; }
        static void Capture(string folder,int index,Vector3 focus,float zoom)
        {
            if(folder=="Cast") arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>()?.Advance(index<16?.0625f:index<28?.02f:.03125f);
            var camera=arena.WorldCamera; var old=camera.transform.position; float oldZoom=camera.orthographicSize;
            camera.transform.position=focus-ObliqueProjection.Forward(arena.View.elevation)*20; camera.orthographicSize=zoom;
            var image=ManorOcclusionValidation.Capture(camera);
            string path="Verification/Artifacts/BeastParticles-v1/Snake/"+folder;
            Directory.CreateDirectory(path); File.WriteAllBytes(path+"/frame-"+index.ToString("D3")+".png",image.EncodeToPNG());
            UnityEngine.Object.Destroy(image); camera.transform.position=old; camera.orthographicSize=oldZoom;
        }
        static void Prepare()
        {
            var pack=arena.Enemies.ToArray();
            Check(pack.Length==4,"Expected four snakes.");
            Physics2D.SyncTransforms();
            for(int i=0;i<4;i++)
            {
                Check(pack[i].maxHealth==Mathf.RoundToInt(150*arena.Tuning.Settings.enemyHealthMultiplier),"Snake HP was not lowered to 150.");
                Check(pack[i].GetComponent<SnakeDemon>().Navigation.Walkable(pack[i].transform.position),"Four-snake formation overlaps boundary.");
                Check(Mathf.Abs(pack[i].Radius-1.12f*.85f)<.0001f,"Snake collision radius was not reduced with the artwork.");
                Check(Mathf.Abs(pack[i].GetComponent<ObliqueActor>().CreatureHeight-2.7f*.85f)<.0001f,"Snake visual height did not shrink by 15%.");
                pack[i].moveSpeed=0;
            }
            Capture("Formation",0,(pack[0].transform.position+pack[3].transform.position)*.5f+arena.View.Up*1.3f,9.5f);
            BeastParticlesValidation.Verify(arena,pack[1].transform.position);
            field=arena.Travel.ActiveMap.GetComponent<SerpentVenomField>();
            Check(field!=null,"Missing map-owned venom field.");
            Vector2 reservePoint=Vector2.zero;
            Check(field.TryReserve(pack[0].GetComponent<SnakeCombat>(),reservePoint),"First reservation failed.");
            var landingWarning=field.GetComponentsInChildren<LineRenderer>().First(line=>line.name=="Venom landing warning");
            for(int i=0;i<landingWarning.positionCount;i++)
                Check(Mathf.Abs(landingWarning.GetPosition(i).magnitude-2.15625f)<.0001f,"Venom warning did not grow by another 25%.");
            Check(!field.TryReserve(pack[1].GetComponent<SnakeCombat>(),reservePoint),"Four snakes can charge simultaneously.");
            field.Advance(.91f);
            Check(field.TryReserve(pack[1].GetComponent<SnakeCombat>(),reservePoint),"Staggered reservation failed.");
            field.Advance(.91f);
            Check(!field.TryReserve(pack[2].GetComponent<SnakeCombat>(),reservePoint),"Zone cap ignored charging skills.");
            field.Clear(); field.Advance(1);
            caster=pack[1]; keeper=pack[2]; combat=caster.GetComponent<SnakeCombat>();
            foreach(var other in new[]{pack[0],pack[3]}) { other.gameObject.SetActive(false); arena.UnregisterEnemy(other); }
            keeper.enabled=false; // Keep one living enemy so killing the caster does not clear the map.
            Vector2 start=arena.Travel.ActiveMap.Definition.Ground(810,400);
            caster.GetComponent<Rigidbody2D>().position=start; caster.transform.position=start;
            target=start+Vector2.down*6;
            arena.Player.Teleport(target); arena.Player.invincible=true; Physics2D.SyncTransforms();
            combat.Stop(); combat.Tick(4);
            Check(combat.Current==SnakeCombat.State.VenomWindup && combat.LockedTarget==target,"Venom did not lock the player's starting position.");
            arena.Player.Teleport(target+Vector2.right*3); Physics2D.SyncTransforms();
            var view=(Vector3)(start+target)*.5f+arena.View.Up*.8f;
            var animation=caster.GetComponent<SnakeAnimation>();
            for(int i=0;i<15;i++)
            {
                combat.Tick(1f/16); field.Advance(1f/16); animation.Refresh();
                Capture("Cast",i,view,5.2f);
            }
            Check(combat.Current==SnakeCombat.State.VenomWindup,"Venom launched before a full second.");
            int hp=caster.Health; caster.TakeHit(1,Vector2.right,10,true);
            Check(caster.Health==hp-1 && combat.Current==SnakeCombat.State.VenomWindup,"Damage interrupted venom windup.");
            arena.TogglePause(); float progress=combat.Progress,clock=field.Clock;
            combat.Tick(1); field.Advance(1);
            Check(combat.Progress==progress && field.Clock==clock,"Pause advanced venom charge."); arena.TogglePause();
            combat.Tick(.0635f); field.Advance(.0635f); animation.Refresh(); Capture("Cast",15,view,5.2f);
            Check(combat.Current==SnakeCombat.State.VenomSpit,"Venom windup did not finish.");
            for(int i=0;i<12;i++)
            {
                combat.Tick(.02f); animation.Refresh(); field.Advance(.02f);
                Capture("Cast",16+i,view,5.2f);
            }
            Check(combat.VenomCasts==1,"Spit was not launched exactly once.");
            Check(arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>().SprayEvents==1,"Actual venom spit emitted no droplet spray.");
            for(int i=0;i<16;i++) { field.Advance(.03125f); Capture("Cast",28+i,view,5.2f); }
            Check(field.PoolCount==1 && Vector2.Distance(field.LastLanding,target)<.001f,"Venom followed the moving player or missed the locked position.");
            var particles=arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>();
            Check(particles.SplashEvents==1 && particles.MistEvents>0,"Venom landing emitted no splash/mist/bubbles.");
            combat.Stop(); combat.Tick(.1f);
            Check(combat.Current==SnakeCombat.State.Pursuit && combat.VenomCasts==1,"Venom cooldown did not prevent recasting.");
            var keeperCombat=keeper.GetComponent<SnakeCombat>();
            Check(field.TryReserve(keeperCombat,target),"Second overlap test reservation failed.");
            Check(field.Launch(keeperCombat,arena.View.LiftPoint(target,1)),"Second overlap test launch failed.");
            field.Advance(.5f); Check(field.PoolCount==2,"Expected two overlapping pools.");
            foreach(var pool in field.GetComponentsInChildren<MeshRenderer>())
                if(pool.sharedMaterial.shader.name=="SlashDemo/SerpentVenom")
                    Check(Mathf.Abs(pool.transform.localScale.x-2.15625f)<.0001f,"Visible pool did not match the enlarged damage radius.");
            Check(!field.TryReserve(combat,target),"Third venom zone exceeded the cap.");
            keeper.GetComponent<SnakeCombat>().enabled=false;
            float edgeDistance=2.1f;
            Check(edgeDistance>1.725f+arena.Player.GetComponent<CircleCollider2D>().radius,"Enlarged-pool test is inside the old radius.");
            arena.Player.Teleport(target+Vector2.right*edgeDistance); arena.Player.invincible=false; Physics2D.SyncTransforms();
            Check(arena.Player.TakeDamage(1,Vector2.zero),"Normal damage setup failed.");
            Check(!arena.Player.TakeDamage(1,Vector2.zero),"Normal hurt immunity was changed.");
            int before=arena.Player.Health; int beforeEvents=field.DamageEvents;
            field.Advance(.01f);
            Check(arena.Player.Health==before-3 && field.DamageEvents==beforeEvents+1,
                "Entering overlapping pools did not cause exactly one DOT tick.");
            field.Advance(.1f);
            Check(field.DamageEvents==beforeEvents+1,"Overlapping pools stacked damage.");
            health=arena.Player.Health; events=field.DamageEvents;
            dotStart=field.Clock;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||EditorApplication.isCompiling||EditorApplication.isUpdating) return;
            try
            {
                if(SessionState.GetBool(Key+".Launch",false))
                { SessionState.SetBool(Key+".Launch",false); EditorApplication.isPlaying=true; return; }
                if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Timed out at stage "+stage);
                if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next) return;
                switch(stage)
                {
                    case 0:
                        arena=UnityEngine.Object.FindAnyObjectByType<SlashArena>(); if(arena==null||arena.Travel==null) return;
                        arena.Player.Teleport(SceneTravel.ManorGate.center); Advance(1); break;
                    case 1:
                        WildernessEncounterValidation.Enter(arena,WildernessCreature.Snake); Prepare(); Advance(2,.05); break;
                    case 2:
                        if(field.Clock-dotStart<1.05f) { next=EditorApplication.timeSinceStartup+.03; return; }
                        Check(field.DamageEvents==events+2 && arena.Player.Health==health-6,
                            "Half-second poison mismatch: ticks="+(field.DamageEvents-events)+", damage="+(health-arena.Player.Health)+", elapsed="+(field.Clock-dotStart));
                        arena.Player.Teleport(target+Vector2.right*3); health=arena.Player.Health;
                        Capture("Pool",0,(Vector3)target+arena.View.Up*.7f,3);
                        arena.TogglePause(); pausedClock=field.Clock; Advance(3,.7); break;
                    case 3:
                        Check(field.Clock==pausedClock && arena.Player.Health==health,"Paused pools aged or dealt damage."); arena.TogglePause();
                        caster.TakeHit(100000,Vector2.zero,0);
                        Check(field.PoolCount==2 && arena.Travel.Portal==null,"Caster death incorrectly removed launched venom.");
                        field.Advance(4.1f);
                        Check(field.ActiveCount==0 && arena.Player.Health==health,"Pools failed to expire or damaged player after exit.");
                        var remaining=keeper.GetComponent<SnakeCombat>(); remaining.enabled=true;
                        Check(field.TryReserve(remaining,target),"Expired pools did not release their slots.");
                        field.Launch(remaining,arena.View.LiftPoint(target,1)); field.Advance(.5f);
                        keeper.TakeHit(100000,Vector2.zero,0);
                        Check(field.ActiveCount==0 && arena.Travel.Portal!=null,"Final kill did not clean hazards before portal.");
                        var old=arena.Travel.ActiveMap;
                        arena.TogglePause(); arena.ReturnHome();
                        Check(!old.gameObject.activeSelf && arena.Enemies.Count==3,"Return home leaked the venom map.");
                        Check(old.GetComponent<BeastAttackParticles>().ActiveCount==0,"Returning home leaked venom particles.");
                        Debug.Log("SNAKE_PARTICLES_PASS: actual spit spray, glob trail, splash, pool mist/bubbles and cleanup.");
                        arena.Player.Teleport(SceneTravel.ManorGate.center); Advance(4); break;
                    case 4:
                        WildernessEncounterValidation.Enter(arena,WildernessCreature.Snake);
                        Check(arena.Enemies.Count==4 && arena.Travel.ActiveMap.GetComponent<SerpentVenomField>().ActiveCount==0,"Reentry duplicated enemies or hazards.");
                        arena.Restart(); Check(arena.Enemies.Count==3 && arena.Travel.Current==TravelDestination.Manor,"Restart leaked venom.");
                        SessionState.SetBool(Key,false);
                        Debug.Log("SNAKE_VENOM_PASS: radius another +25% (2.15625); matching warning and pool; damage outside former radius; four 150-HP snakes; smaller art/collision; locked target; 3 damage per 0.5s; no dropped DOT ticks; normal immunity preserved; overlap cap; pause; cooldown; lifetime; clear portal; return/reentry/restart.");
                        EditorApplication.Exit(0); break;
                }
            }
            catch(Exception ex)
            { SessionState.SetBool(Key,false); Debug.LogError("SNAKE_VENOM_FAIL: stage="+stage+" "+ex); EditorApplication.Exit(1); }
        }
    }
}
