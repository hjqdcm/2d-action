using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class SnakeAnimationValidation
    {
        const string Key = "SlashDemo.SnakeAnimationValidation";
        static SlashArena arena;
        static SlashEnemy enemy;
        static SnakeAnimation mesh;
        static SnakeCombat combat;
        static int stage, frames;
        static Vector2 origin;
        static float initialPhase, minBlend = 1;
        static double next, deadline, nextFrame;
        static void Check(bool yes, string message) { if (!yes) throw new Exception(message); }
        public static void RunBatch()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Launch", true);
        }
        [InitializeOnLoadMethod]
        static void Hook()
        { EditorApplication.update -= Tick; EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 100; next = EditorApplication.timeSinceStartup + 2; }

        static void Capture(string folder, int frame)
        {
            if(folder=="Bite") arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>()?.Advance(frame<16?enemy.windupTime/16:frame<28?.02f:.046875f);
            enemy.GetComponent<SerpentGazeVfx>()?.RefreshPresentation();
            var camera = arena.WorldCamera; var old = camera.transform.position; float zoom = camera.orthographicSize;
            camera.transform.position = enemy.transform.position + arena.View.Up * 1.35f - ObliqueProjection.Forward(arena.View.elevation) * 20;
            camera.orthographicSize = 2.75f;
            if(folder=="Bite")
            {
                camera.transform.position=enemy.transform.position+arena.View.Up*.5f-ObliqueProjection.Forward(arena.View.elevation)*20;
                camera.orthographicSize=3.9f;
            }
            var image = ManorOcclusionValidation.Capture(camera);
            string path = "Verification/Artifacts/BeastParticles-v1/Gaze/" + folder;
            Directory.CreateDirectory(path); File.WriteAllBytes(path + "/frame-" + frame.ToString("D3") + ".png", image.EncodeToPNG());
            UnityEngine.Object.Destroy(image); camera.transform.position = old; camera.orthographicSize = zoom;
        }

        static void CaptureTailClose(int index)
        {
            var camera=arena.WorldCamera; var old=camera.transform.position; float zoom=camera.orthographicSize;
            var source=enemy.transform.Find("Visual/Jade snake skin");
            camera.transform.position=source.TransformPoint(mesh.PartPoint(2,.88f,.44f))-ObliqueProjection.Forward(arena.View.elevation)*20;
            camera.orthographicSize=.85f;
            var frame=ManorOcclusionValidation.Capture(camera);
            Directory.CreateDirectory("Verification/Artifacts/BeastParticles-v1/Gaze/TailClose");
            File.WriteAllBytes("Verification/Artifacts/BeastParticles-v1/Gaze/TailClose/frame-"+index.ToString("D3")+".png",frame.EncodeToPNG());
            UnityEngine.Object.Destroy(frame); camera.transform.position=old; camera.orthographicSize=zoom;
        }

        static void VerifyGeometry()
        {
            VerifyTailOwnership();
            var skin = enemy.transform.Find("Visual/Jade snake skin").GetComponent<SpriteRenderer>();
            skin.flipX = false;
            Check(skin.forceRenderingOff && mesh.SkinRenderer.enabled, "Articulated body renderer not active.");
            Check(!ShaderUtil.ShaderHasError(mesh.SkinRenderer.sharedMaterial.shader), "Snake shader failed.");
            mesh.Advance(Vector2.zero, 1, false); mesh.SetAttackPose(0, 0, Vector2.right); mesh.Refresh();
            var rest = mesh.DeformedMesh.vertices; var uv = mesh.DeformedMesh.uv; var triangles = mesh.DeformedMesh.triangles;
            mesh.Advance(Vector2.right*.25f,.2f,true);
            float maxWalkMotion = 0, maxHeadMotion = 0;
            Vector3 neckMin=Vector3.one*100, neckMax=Vector3.one*-100, tailMin=neckMin, tailMax=neckMax;
            for (int sample = 0; sample < 48; sample++)
            {
                mesh.Advance(Vector2.right * .0625f, .05f, true);
                if (sample >= 32)
                {
                    float t = (sample - 32) / 15f;
                    mesh.SetAttackPose(1-t, Mathf.Sin(t*Mathf.PI), Vector2.down);
                }
                mesh.Refresh(); var posed = mesh.DeformedMesh.vertices;
                var currentUV = mesh.DeformedMesh.uv;
                for (int i = 0; i < rest.Length; i++)
                {
                    Check(currentUV[i] == uv[i], "Texture coordinates drifted.");
                    if (i < rest.Length/3 && uv[i].y < .035f) Check(Vector3.Distance(rest[i], posed[i]) < .065f, "Ground contact sways too far.");
                    if (sample < 32) maxWalkMotion = Mathf.Max(maxWalkMotion, Vector3.Distance(rest[i],posed[i]));
                    if (i < rest.Length/3*2 && uv[i].y > .68f)
                    {
                        int j = i % (rest.Length/3) == rest.Length/3-1 ? i : i+1;
                        Check(Mathf.Abs(Vector3.Distance(posed[i],posed[j])-Vector3.Distance(rest[i],rest[j])) < .00002f, "Head/horns stretched.");
                        maxHeadMotion = Mathf.Max(maxHeadMotion,Vector3.Distance(rest[i],posed[i]));
                    }
                }
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector2 a=posed[triangles[i]], b=posed[triangles[i+1]], c=posed[triangles[i+2]];
                    Check((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x) < 0, "Neck mesh folded over.");
                    if (sample < 32)
                    {
                        for (int edge=0;edge<3;edge++)
                        {
                            int aIndex=triangles[i+edge], bIndex=triangles[i+(edge+1)%3];
                            bool rigid=aIndex<rest.Length/3*2 ||
                                (SnakeAnimation.TailMotionWeight(uv[aIndex])>.999f && SnakeAnimation.TailMotionWeight(uv[bIndex])>.999f) ||
                                (SnakeAnimation.TailMotionWeight(uv[aIndex])<.001f && SnakeAnimation.TailMotionWeight(uv[bIndex])<.001f);
                            if(rigid) Check(Mathf.Abs(Vector3.Distance(posed[aIndex],posed[bIndex])-Vector3.Distance(rest[aIndex],rest[bIndex]))<.00001f,
                                "Movement stretched a rigid body section or free tail.");
                        }
                    }
                }
                if (sample < 32)
                {
                    Vector3 neck=mesh.PartPoint(1,.6f,.8f)-mesh.PartPoint(0,.5f,.2f);
                    Vector3 tail=mesh.PartPoint(2,.8f,.52f)-mesh.PartPoint(0,.5f,.2f);
                    neckMin=Vector3.Min(neckMin,neck); neckMax=Vector3.Max(neckMax,neck);
                    tailMin=Vector3.Min(tailMin,tail); tailMax=Vector3.Max(tailMax,tail);
                    foreach(var rootPoint in new[]{new Vector2(.95f,.325f),new Vector2(.925f,.35f),new Vector2(.9f,.375f),new Vector2(.875f,.4f)})
                        Check(Vector3.Distance(mesh.PartPoint(0,rootPoint.x,rootPoint.y),mesh.PartPoint(2,rootPoint.x,rootPoint.y))<.0001f,
                            "Tail root separated from the coil.");
                    Capture("Walk", sample);
                    CaptureTailClose(sample);
                }
            }
            Check(mesh.DeformedMesh.subMeshCount==3,"Missing independently moving coil/neck/tail sections.");
            Check((neckMax-neckMin).magnitude>.16f && (tailMax-tailMin).magnitude>.1f,
                "Neck or tail barely moves relative to the body: neck="+(neckMax-neckMin).magnitude+", tail="+(tailMax-tailMin).magnitude);
            Check(maxWalkMotion > .15f && maxHeadMotion > .35f, "Segmented motion or bite extension is too small.");
            mesh.SetAttackPose(0,0,Vector2.right); mesh.Advance(Vector2.zero,1,false); mesh.Refresh();
            var unflipped = mesh.DeformedMesh.vertices;
            skin.flipX = true; mesh.Refresh(); var flipped = mesh.DeformedMesh.vertices;
            for(int i=0;i<flipped.Length;i++) Check(Mathf.Abs(unflipped[i].x+flipped[i].x)<.00001f && Mathf.Abs(unflipped[i].y-flipped[i].y)<.00001f,"Mirror broke the body.");
            skin.flipX=false;
            float phase=mesh.Phase; mesh.Advance(Vector2.one,0,true); Check(mesh.Phase==phase,"Zero-delta time advanced animation.");
            for(int i=0;i<200;i++) mesh.Advance(i%4==0?Vector2.right*.03f:Vector2.zero,.005f,true);
            Check(mesh.Blend>.9f,"High frame rate faded out movement.");
            mesh.Advance(Vector2.zero,1,false); Check(mesh.Blend==0,"Stopped snake continued swimming.");
            mesh.Refresh();
            Debug.Log("SNAKE_MESH_PASS: independent coil, neck and tail cycle; visible relative movement; body/neck/free-tail rigid; tail cut edge anchored to coil; rigid head/horns; fixed UVs; no folded triangles; mirroring; stop; high FPS and shader.");
        }

        static void VerifyTailOwnership()
        {
            // Render the real shader with white paint to detect retained tail pixels
            // independently of the animation math and the artwork's dark scales.
            var camera=arena.WorldCamera;
            Vector3 oldPosition=camera.transform.position; Quaternion oldRotation=camera.transform.rotation;
            float oldZoom=camera.orthographicSize, oldAspect=camera.aspect;
            int oldMask=camera.cullingMask; Color oldBackground=camera.backgroundColor;
            var oldClear=camera.clearFlags;
            var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool oldPost=data.renderPostProcessing;
            var go=new GameObject("Tail ownership render audit"); go.layer=31;
            var quad=new Mesh();
            quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            quad.triangles=new[]{0,2,1,0,3,2}; quad.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh=quad;
            var renderer=go.AddComponent<MeshRenderer>();
            var material=new Material(Resources.Load<Shader>("SlashDemo/SnakeSkin"));
            material.mainTexture=Texture2D.whiteTexture; renderer.sharedMaterial=material;
            try
            {
                camera.transform.SetPositionAndRotation(new Vector3(0,0,-10),Quaternion.identity);
                camera.orthographicSize=.6f; camera.aspect=4f/3; camera.cullingMask=1<<31;
                camera.backgroundColor=Color.black; camera.clearFlags=CameraClearFlags.SolidColor;
                data.renderPostProcessing=false;
                var layers=new Color32[3][];
                for(int part=0;part<3;part++)
                {
                    material.SetFloat("_PartIndex",part);
                    var frame=ManorOcclusionValidation.Capture(camera);
                    try
                    {
                        layers[part]=frame.GetPixels32();
                        for(int x=0;x<21;x++) for(int y=0;y<12;y++)
                        {
                            Vector2 uv=new Vector2(.747f+x*.01f,.417f+y*.012f);
                            Color pixel=frame.GetPixel(Mathf.RoundToInt(320+(uv.x-.5f)*400),Mathf.RoundToInt(240+(uv.y-.5f)*400));
                            Check(part==2 ? pixel.r>.8f : pixel.r<.05f,
                                "Free tail/tip must render only in tail layer: part="+part+", UV="+uv+", red="+pixel.r);
                        }
                    }
                    finally { UnityEngine.Object.Destroy(frame); }
                }
                int tailPixels=0,duplicates=0;
                for(int i=0;i<layers[2].Length;i++)
                {
                    if(layers[2][i].r<200) continue;
                    tailPixels++;
                    // AA can assign different subpixels of a cut edge to each layer.
                    // Check the interior, where the old overlapping strip was also visible.
                    int x=i%640,y=i/640; bool interior=x>0&&x<639&&y>0&&y<479;
                    for(int dy=-1;dy<=1 && interior;dy++) for(int dx=-1;dx<=1;dx++)
                        if(layers[2][i+dy*640+dx].r<240) interior=false;
                    if(interior && (layers[0][i].r>20 || layers[1][i].r>20)) duplicates++;
                }
                Check(tailPixels>2000 && duplicates==0,"Whole-tail partition failed: painted="+tailPixels+", duplicated="+duplicates);
                Debug.Log("SNAKE_SINGLE_TAIL_PASS: full UV render audit; tail pixels="+tailPixels+", duplicates="+duplicates+"; tip ownership verified.");
            }
            finally
            {
                go.SetActive(false); UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(quad); UnityEngine.Object.Destroy(material);
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
                camera.orthographicSize=oldZoom; camera.aspect=oldAspect; camera.cullingMask=oldMask;
                camera.backgroundColor=oldBackground; camera.clearFlags=oldClear; data.renderPostProcessing=oldPost;
            }
        }

        static void VerifyCombat()
        {
            enemy.moveSpeed=0; combat.Stop();
            var gaze=enemy.GetComponent<SerpentGazeVfx>();
            Check(gaze!=null && !ShaderUtil.ShaderHasError(gaze.FanRenderer.sharedMaterial.shader),"Missing gaze effect or shader error.");
            Check(gaze.FanRenderer.GetComponentsInChildren<Renderer>(true).Length==1,"Eye glow renderer still exists.");
            var settings=arena.Tuning.Settings;
            Check(Mathf.Abs(enemy.attackRange-settings.enemyRange*BearDemon.RangeScale*.9f)<.0001f,"Snake normal attack radius is not 90% of the bear claw radius.");
            Check(Mathf.Abs(enemy.windupTime-settings.enemyWindup*1.45f*.8f)<.0001f,"Snake windup did not decrease by 20%.");
            float range=enemy.attackRange,windup=enemy.windupTime;
            enemy.ApplyTuning(settings); enemy.ApplyTuning(settings);
            Check(Mathf.Abs(enemy.attackRange-range)<.0001f && Mathf.Abs(enemy.windupTime-windup)<.0001f,"Tuning compounded the skill multipliers.");
            int casts=gaze.Casts;
            float targetDistance=enemy.attackRange*.95f;
            Check(targetDistance>enemy.attackRange/1.5f+.29f,"Positive test target is still within the old hit range.");
            arena.Player.Teleport((Vector2)enemy.transform.position + Vector2.down*targetDistance);
            arena.Player.invincible=false; Physics2D.SyncTransforms();
            int health=arena.Player.Health, events=combat.DamageEvents;
            combat.Tick(.01f); Check(combat.Current==SnakeCombat.State.Windup,"Bite did not wind up.");
            Check(gaze.Visible && !gaze.BlastActive,"Cone windup did not start.");
            Vector2 aim=combat.LockedAim;
            for(int i=0;i<16;i++)
            {
                combat.Tick(enemy.windupTime/16); mesh.Refresh(); Capture("Bite",i);
                if(i==14) Check(combat.Current==SnakeCombat.State.Windup,"Windup completed before the new duration.");
            }
            // Floating-point accumulation may leave the final windup infinitesimally short.
            if(combat.Current==SnakeCombat.State.Windup) combat.Tick(.001f);
            Check(combat.Current==SnakeCombat.State.Bite && combat.DamageEvents==events,"Damage occurred before extension.");
            Check(gaze.Casts==casts,"Gaze fired before the damage frame.");
            int hp=enemy.Health; enemy.TakeHit(1,Vector2.right,99,true);
            Check(enemy.Health==hp-1 && combat.Current==SnakeCombat.State.Bite && combat.LockedAim==aim,"Hit interrupted the bite.");
            arena.TogglePause(); float progress=combat.Progress, phase=mesh.Phase;
            combat.Tick(1); Check(combat.Progress==progress && mesh.Phase==phase,"Pause advanced bite."); arena.TogglePause();
            for(int i=0;i<12;i++)
            {
                combat.Tick(SnakeCombat.BiteDuration/12); mesh.Refresh(); Capture("Bite",16+i);
                if(i==9)
                {
                    Check(gaze.BlastActive && gaze.Aim==aim && Mathf.Abs(gaze.Reach-enemy.attackRange)<.001f,"Gaze cone failed to match the locked damage arc.");
                    float gazeAge=gaze.Age; arena.TogglePause(); gaze.Advance(1);
                    Check(gaze.Age==gazeAge,"Pause advanced VFX."); arena.TogglePause();
                    var vertices=gaze.FanRenderer.GetComponent<MeshFilter>().sharedMesh.vertices;
                    for(int j=vertices.Length-49;j<vertices.Length;j++)
                        Check(Mathf.Abs(Vector2.Distance(vertices[j]-arena.View.Up*.06f,gaze.Origin)-enemy.attackRange)<.001f,"VFX extends beyond damage range.");
                    var camera=arena.WorldCamera;
                    var oldPosition=camera.transform.position; float oldZoom=camera.orthographicSize;
                    camera.transform.position=enemy.transform.position+arena.View.Up*.5f-ObliqueProjection.Forward(arena.View.elevation)*20;
                    camera.orthographicSize=3.9f;
                    var withEffect=ManorOcclusionValidation.Capture(camera);
                    gaze.FanRenderer.enabled=false;
                    var withoutEffect=ManorOcclusionValidation.Capture(camera);
                    gaze.FanRenderer.enabled=true;
                    camera.transform.position=oldPosition; camera.orthographicSize=oldZoom;
                    var a=withEffect.GetPixels32(); var b=withoutEffect.GetPixels32(); int changed=0;
                    for(int p=0;p<a.Length;p++)
                        if(Mathf.Abs(a[p].r-b[p].r)+Mathf.Abs(a[p].g-b[p].g)+Mathf.Abs(a[p].b-b[p].b)>24) changed++;
                    UnityEngine.Object.Destroy(withEffect); UnityEngine.Object.Destroy(withoutEffect);
                    Check(changed>200,"Gaze fan is invisible in the actual render: "+changed);
                    Debug.Log("SERPENT_GAZE_RENDER_PASS: cone changes "+changed+" actual pixels.");
                    BeastParticlesValidation.CaptureActual(arena,enemy.transform.position+arena.View.Up*.5f,3.9f,"Verification/Artifacts/BeastParticles-v1/Snake-gaze-impact.png");
                }
            }
            Check(combat.DamageEvents==events+1 && arena.Player.Health<health,"Peak extension did not deal one hit.");
            Check(gaze.Casts==casts+1,"Gaze did not fire exactly once at the damage frame.");
            Check(arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>().GazeEvents==1,"Actual gaze attack emitted no venom motes.");
            Debug.Log("GAZE_PARTICLES_PASS: actual gaze damage frame emits venom motes and gold flecks without eye glow.");
            for(int i=0;i<16;i++) { combat.Tick(SnakeCombat.RecoveryDuration/16); mesh.Refresh(); Capture("Bite",28+i); }
            Check(combat.DamageEvents==events+1,"Bite dealt repeat damage.");
            Check(!gaze.Visible,"Gaze did not fade after recovery.");
            combat.Stop(); combat.Tick(.01f); aim=combat.LockedAim;
            arena.Player.Teleport((Vector2)enemy.transform.position - aim*1.65f); Physics2D.SyncTransforms();
            combat.Tick(enemy.windupTime+.01f); combat.Tick(SnakeCombat.BiteDuration*.6f);
            Check(combat.LockedAim==aim && combat.DamageEvents==events+1,"Locked bite tracked or hit a dodging player.");
            Check(gaze.Casts==casts+2 && gaze.BlastActive,"A missed attack should still show its cone.");
            combat.Stop();
            arena.Player.Teleport((Vector2)enemy.transform.position+Vector2.down*enemy.attackRange*.9f);
            combat.Tick(.01f); aim=combat.LockedAim;
            float oldRange=enemy.attackRange/.8f;
            arena.Player.Teleport((Vector2)enemy.transform.position+aim*(oldRange*.95f)); Physics2D.SyncTransforms();
            Check(oldRange*.95f>enemy.attackRange+.29f,"Range-shrink miss test is inside the new collision allowance.");
            combat.Tick(enemy.windupTime+.01f); combat.Tick(SnakeCombat.BiteDuration*.6f);
            Check(combat.DamageEvents==events+1,"A target only inside the previous 20% larger radius was still hit.");
            enemy.gameObject.SetActive(false); Check(combat.Current==SnakeCombat.State.Pursuit,"Disable did not clean up bite.");
            Check(!gaze.Visible,"Disable leaked the gaze effect.");
            enemy.gameObject.SetActive(true); combat.Stop();
            enemy.TakeHit(100000,Vector2.zero,0); combat.Tick(1);
            Check(!enemy.Alive && combat.DamageEvents==events+1 && !enemy.GetComponent<ObliqueActor>().GroundWarning.gameObject.activeSelf,"Death left an active bite.");
            Check(!gaze.Visible,"Death leaked the gaze effect.");
            Debug.Log("SERPENT_GAZE_PASS: no eye glow; 20% smaller attack radius with inside hit and former-range miss; 20% shorter windup; stable tuning multipliers; single burst on damage frame, correct locked cone/range, pause, fade, visible miss and disable/death cleanup.");
            Debug.Log("SNAKE_BITE_PASS: windup, animated extension and single synchronized hit; recovery; locked aim/dodge; pause; hit immunity; disable and death cleanup.");
        }

        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||EditorApplication.isCompiling||EditorApplication.isUpdating) return;
            try
            {
                if(SessionState.GetBool(Key+".Launch",false))
                { SessionState.SetBool(Key+".Launch",false); EditorApplication.isPlaying=true; return; }
                if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Timeout at "+stage);
                if(!EditorApplication.isPlaying) return;
                if(stage==2 && enemy!=null && EditorApplication.timeSinceStartup>=nextFrame)
                {
                    nextFrame=EditorApplication.timeSinceStartup+.055;
                    if(frames>2) minBlend=Mathf.Min(minBlend,mesh.Blend);
                    Capture("Live",frames++);
                }
                if(EditorApplication.timeSinceStartup<next) return;
                switch(stage)
                {
                    case 0:
                        arena=UnityEngine.Object.FindAnyObjectByType<SlashArena>(); if(arena==null||arena.Travel==null) return;
                        arena.Player.Teleport(SceneTravel.ManorGate.center); stage=1; next=EditorApplication.timeSinceStartup+.3; break;
                    case 1:
                        WildernessEncounterValidation.Enter(arena,WildernessCreature.Snake);
                        enemy=arena.Enemies[1];
                        foreach(var other in new[]{arena.Enemies[0],arena.Enemies[2],arena.Enemies[3]}) { other.gameObject.SetActive(false); arena.UnregisterEnemy(other); }
                        mesh=enemy.GetComponent<SnakeAnimation>(); combat=enemy.GetComponent<SnakeCombat>();
                        Check(mesh!=null&&combat!=null,"Missing snake animation/combat.");
                        origin=enemy.transform.position; initialPhase=mesh.Phase;
                        stage=2; next=EditorApplication.timeSinceStartup+1.6; break;
                    case 2:
                        Check(Vector2.Distance(origin,enemy.transform.position)>.5f&&frames>=8&&minBlend>.7f&&mesh.Phase!=initialPhase,"Live pursuit did not sustain animation.");
                        arena.TogglePause(); VerifyGeometry(); arena.TogglePause(); VerifyCombat();
                        stage=3; next=EditorApplication.timeSinceStartup+.7; break;
                    case 3:
                        Check(enemy==null,"Death did not release the animated mesh.");
                        arena.Restart(); Check(arena.Enemies.Count==3,"Restart did not restore manor.");
                        SessionState.SetBool(Key,false); Debug.Log("SNAKE_ANIMATION_PASS: geometry, actual pursuit, bite timing/dodge, pause, death and cleanup verified; rendered walk/bite sequences.");
                        EditorApplication.Exit(0); break;
                }
            }
            catch(Exception ex)
            { SessionState.SetBool(Key,false); Debug.LogError("SNAKE_ANIMATION_FAIL: stage="+stage+" "+ex); EditorApplication.Exit(1); }
        }
    }
}
