using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class BearPackValidation
    {
        const string Key = "SlashDemo.BearPack";
        static SlashArena arena;
        static SlashEnemy[] pack;
        static Vector2[] initial;
        static int stage;
        static double next, deadline;
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }

        public static SlashEnemy[] CheckFormation(SlashArena owner)
        {
            Check(owner.Travel.Current==TravelDestination.Wilderness,"Not in wilderness.");
            Check(owner.Enemies.Count==3,"Expected exactly three wilderness bears.");
            var result = new SlashEnemy[3];
            float minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
            foreach(var p in owner.Travel.ActiveMap.Definition.boundary)
            {
                float y=owner.Travel.ActiveMap.Definition.Ground(p.x,p.y).y;
                minY=Mathf.Min(minY,y); maxY=Mathf.Max(maxY,y);
            }
            for(int i=0;i<3;i++)
            {
                var enemy=owner.Enemies[i]; result[i]=enemy;
                Check(enemy.rockBear && enemy.Alive && enemy.gameObject.activeInHierarchy,"Spawned bear is not active/alive.");
                Check(enemy.GetComponent<BearCombat>()!=null && enemy.GetComponent<BearWalkMesh>()!=null,"Missing bear skills or animation.");
                Vector2 point=enemy.transform.position;
                Check(point.y>(minY+maxY)*.5f,"Bear spawned in the entrance half.");
                Check(Vector2.Distance(point,owner.Travel.ActiveMap.Definition.Entry)>(maxY-minY)*.45f,"Bear spawned too close to entrance.");
                foreach(var collider in Physics2D.OverlapCircleAll(point,enemy.Radius+.06f))
                    if(!collider.isTrigger && collider.transform.IsChildOf(owner.Travel.ActiveMap.transform)
                        && collider.GetComponent<SlashEnemy>()==null)
                        throw new Exception("Bear spawn overlaps map boundary or furniture.");
            }
            Vector2 a=result[0].transform.position,b=result[1].transform.position,c=result[2].transform.position;
            Check(Mathf.Abs(a.y-b.y)<.001f && Mathf.Abs(b.y-c.y)<.001f,"Bears are not on a straight horizontal row.");
            Check(b.x>a.x && c.x>b.x && Mathf.Abs((b.x-a.x)-(c.x-b.x))<.001f,"Bears are not evenly spaced.");
            return result;
        }

        // Individual skill/gait regressions isolate one actor after checking the full formation.
        public static SlashEnemy IsolateMiddleBear(SlashArena owner)
        {
            var all=CheckFormation(owner);
            foreach(var enemy in new[] { all[0],all[2] })
            { enemy.gameObject.SetActive(false); owner.UnregisterEnemy(enemy); }
            return all[1];
        }

        public static void RunBatch()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Key,true); SessionState.SetBool(Key+".Launch",true);
        }
        [InitializeOnLoadMethod]
        static void Hook() { EditorApplication.update-=Tick; EditorApplication.update+=Tick; deadline=EditorApplication.timeSinceStartup+80; }
        static void Advance(int value,double delay=.25) { stage=value; next=EditorApplication.timeSinceStartup+delay; }
        static void Capture()
        {
            var camera=arena.WorldCamera;
            Vector3 old=camera.transform.position; float zoom=camera.orthographicSize;
            camera.transform.position=(pack[0].transform.position+pack[2].transform.position)*.5f
                +arena.View.Up*1.3f-ObliqueProjection.Forward(arena.View.elevation)*20;
            camera.orthographicSize=8.5f;
            var image=ManorOcclusionValidation.Capture(camera);
            Directory.CreateDirectory("Verification/Artifacts");
            File.WriteAllBytes("Verification/Artifacts/Bear-pack-formation.png",image.EncodeToPNG());
            UnityEngine.Object.Destroy(image); camera.transform.position=old; camera.orthographicSize=zoom;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||EditorApplication.isCompiling||EditorApplication.isUpdating) return;
            try
            {
                if(SessionState.GetBool(Key+".Launch",false))
                { SessionState.SetBool(Key+".Launch",false); EditorApplication.isPlaying=true; deadline=EditorApplication.timeSinceStartup+80; Advance(0,2); return; }
                if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Pack validation timed out at "+stage);
                if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next) return;
                switch(stage)
                {
                    case 0:
                        arena=UnityEngine.Object.FindAnyObjectByType<SlashArena>();
                        if(arena==null||arena.Travel==null) return;
                        arena.Player.Teleport(SceneTravel.ManorGate.center); Advance(1); break;
                    case 1:
                        Check(arena.Travel.IsSelectionOpen,"Gate did not open.");
                        WildernessEncounterValidation.Enter(arena, WildernessCreature.Bear);
                        Physics2D.SyncTransforms(); pack=CheckFormation(arena);
                        initial=new Vector2[3]; for(int i=0;i<3;i++) initial[i]=pack[i].transform.position;
                        arena.TogglePause(); Capture(); arena.TogglePause();
                        BearAllyChargeValidation.Verify(arena);
                        BearSpacingValidation.Verify(arena);
                        Advance(2,1.5); break;
                    case 2:
                        for(int i=0;i<3;i++)
                            Check(Vector2.Distance(initial[i],pack[i].transform.position)>.5f,"Bear "+i+" did not chase.");
                        arena.TogglePause(); arena.ReturnHome();
                        Check(arena.Enemies.Count==3,"Manor enemies not restored.");
                        foreach(var enemy in pack) Check(!enemy.gameObject.activeInHierarchy,"Bear leaked into manor.");
                        arena.Player.Teleport(SceneTravel.ManorGate.center); Advance(3); break;
                    case 3:
                        Check(arena.Travel.IsSelectionOpen,"Reentry gate did not open.");
                        WildernessEncounterValidation.Enter(arena, WildernessCreature.Bear); Physics2D.SyncTransforms();
                        pack=CheckFormation(arena);
                        Check(arena.Travel.ActiveMap.GetComponentsInChildren<SlashEnemy>().Length==3,"Reentry duplicated the pack.");
                        arena.Restart();
                        Check(arena.Travel.Current==TravelDestination.Manor && arena.Enemies.Count==3,"Restart did not clean up pack.");
                        SessionState.SetBool(Key,false);
                        Debug.Log("BEAR_PACK_PASS: three living skilled bears; far half; equal spacing on one horizontal line; boundary clearance; all chase; return, fresh reentry and restart cleanup.");
                        EditorApplication.Exit(0); break;
                }
            }
            catch(Exception ex)
            { SessionState.SetBool(Key,false); Debug.LogError("BEAR_PACK_FAIL: stage="+stage+" "+ex); EditorApplication.Exit(1); }
        }
    }
}
