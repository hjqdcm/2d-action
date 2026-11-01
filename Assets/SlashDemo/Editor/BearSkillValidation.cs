using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class BearSkillValidation
    {
        const string Key = "SlashDemo.BearSkills";
        static SlashArena arena;
        static SlashEnemy enemy;
        static BearCombat combat;
        static int stage, health, events, frame;
        static float pausedProgress;
        static double deadline, nextFrame, pauseUntil;
        static Vector2 aim, start;
        static Vector3 paw;
        static GameObject wall;
        static BeastAttackParticles particles;
        static int clawParticles,impactParticles;
        static bool lastCaptureClaw;
        static void WarningStyle(LineRenderer line,float originalWidth)
        {
            // LineRenderer gradients can quantize channels to 8-bit color.
            Check(Vector4.Distance(line.startColor,DemoArt.AttackWarningColor)<.01f && Vector4.Distance(line.endColor,DemoArt.AttackWarningColor)<.01f,
                "Warning is not red: "+line.name+" "+line.startColor);
            Check(Mathf.Abs(line.startWidth-originalWidth*.4f)<.000001f && Mathf.Abs(line.endWidth-originalWidth*.4f)<.000001f,
                "Warning width is not 40% of the original.");
        }
        public static void RunBatch()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Key,true); SessionState.SetBool(Key+".Launch",true);
        }
        [InitializeOnLoadMethod]
        static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 100; }
        static void Check(bool yes,string message) { if (!yes) throw new Exception(message); }
        static void ArrangeCharge()
        {
            // Hold distance while the charge cooldown expires; otherwise the AI
            // correctly closes to claw range and keeps choosing its melee skill.
            enemy.moveSpeed = 0;
            enemy.GetComponent<Rigidbody2D>().position = ManorEnvironment.Ground(810,610);
            enemy.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            arena.Player.Teleport(ManorEnvironment.Ground(810,738));
            health = arena.Player.Health; events = combat.DamageEvents;
            Physics2D.SyncTransforms();
        }
        static void Capture()
        {
            if (arena == null || combat == null || arena.Paused || EditorApplication.timeSinceStartup < nextFrame) return;
            bool claw = combat.Current == BearCombat.State.ClawWindup || combat.Current == BearCombat.State.ClawSwing;
            bool charge = combat.Current == BearCombat.State.ChargeWindup || combat.Current == BearCombat.State.Charging;
            bool recovery=combat.Current==BearCombat.State.Recovery && particles!=null && particles.ActiveCount>0;
            if ((!claw && !charge && !recovery) || frame > 220) return;
            if(claw || charge) lastCaptureClaw=claw;
            if(recovery) claw=lastCaptureClaw;
            nextFrame = EditorApplication.timeSinceStartup + .05;
            var rigid = enemy.GetComponent<BearWalkMesh>().DeformedMesh.vertices;
            for (int part = 0; part < 5; part++)
            {
                int i = part*4;
                Check(Mathf.Abs(Vector3.Distance(rigid[i],rigid[i+1])-BearDemon.Height) < .00001f,
                    "Skill animation stretched the claw image.");
            }
            var camera = arena.WorldCamera; Vector3 old = camera.transform.position; float zoom = camera.orthographicSize;
            camera.orthographicSize = claw ? 3 : 5;
            camera.transform.position = enemy.transform.position + arena.View.Up * 1.1f - ObliqueProjection.Forward(arena.View.elevation)*20;
            string folder = "Verification/Artifacts/BeastParticles-v1/Bear/" + (claw ? "Claw" : "Charge");
            Directory.CreateDirectory(folder);
            var image = ManorOcclusionValidation.Capture(camera);
            File.WriteAllBytes(folder+"/frame-"+(frame++).ToString("D3")+".png",image.EncodeToPNG());
            UnityEngine.Object.Destroy(image); camera.transform.position = old; camera.orthographicSize = zoom;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key,false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (SessionState.GetBool(Key+".Launch",false))
                { SessionState.SetBool(Key+".Launch",false); EditorApplication.isPlaying = true; deadline = EditorApplication.timeSinceStartup+100; return; }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout stage "+stage+" state "+combat?.Current);
                if (!EditorApplication.isPlaying) return;
                Capture();
                switch(stage)
                {
                    case 0:
                        arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
                        if (arena == null || arena.Travel == null) return;
                        arena.Player.invincible = false;
                        foreach(var other in arena.Enemies)
                            foreach(var line in other.GetComponent<ObliqueActor>().GroundWarning.GetComponentsInChildren<LineRenderer>(true))
                                WarningStyle(line,.045f);
                        arena.Player.Teleport(SceneTravel.ManorGate.center); stage++; break;
                    case 1:
                        if (!arena.Travel.IsSelectionOpen) return;
                        WildernessEncounterValidation.Enter(arena, WildernessCreature.Bear);
                        enemy = BearPackValidation.IsolateMiddleBear(arena); combat = enemy.GetComponent<BearCombat>();
                        Check(combat != null,"Bear has no exclusive skill controller.");
                        BearJointValidation.Verify(arena,enemy);
                        EnemyUninterruptibleValidation.Verify(arena,enemy);
                        particles=arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>();
                        BeastParticlesValidation.Verify(arena,enemy.transform.position);
                        clawParticles=particles.ClawEvents;
                        events = combat.DamageEvents;
                        Check(Mathf.Abs(enemy.attackRange - 1.15f*1.55f*1.5f) < .001f,"Claw range is not 150% of the original default.");
                        arena.Player.Teleport((Vector2)enemy.transform.position + Vector2.down*(enemy.attackRange*.8f));
                        health = arena.Player.Health; stage++; break;
                    case 2:
                        if (combat.Current != BearCombat.State.ClawWindup) return;
                        var clawArc=enemy.GetComponent<ObliqueActor>().GroundWarning.Find("Claw attack warning").GetComponent<LineRenderer>();
                        WarningStyle(clawArc,.055f);
                        Check(Mathf.Abs(clawArc.GetPosition(1).magnitude*clawArc.transform.localScale.x-enemy.attackRange)<.001f,
                            "150% claw telegraph does not match damage reach.");
                        paw = enemy.GetComponent<BearWalkMesh>().PawPosition(2); stage++; break;
                    case 3:
                        if (combat.Current != BearCombat.State.ClawWindup || combat.Progress < .65f) return;
                        Check(Vector3.Distance(paw,enemy.GetComponent<BearWalkMesh>().PawPosition(2))>.1f,"Claw windup did not lift the rigid forepaw.");
                        Check(arena.Player.Health == health,"Claw dealt damage during windup."); pauseUntil=0; stage++; break;
                    case 4:
                        if (combat.Current != BearCombat.State.Recovery) return;
                        Check(arena.Player.Health == health-enemy.damage && combat.DamageEvents==events+1,"Claw swing must deal one damage event.");
                        Check(particles.ClawEvents>clawParticles,"Actual claw swing emitted no claw particles.");
                        if(pauseUntil==0)
                        {
                            BeastParticlesValidation.CaptureActual(arena,enemy.transform.position+arena.View.Up*.6f,3,"Verification/Artifacts/BeastParticles-v1/Bear-claw-impact.png");
                            pauseUntil=EditorApplication.timeSinceStartup+.55; return;
                        }
                        if(EditorApplication.timeSinceStartup<pauseUntil) return;
                        impactParticles=particles.ImpactEvents;
                        ArrangeCharge(); stage++; break;
                    case 5:
                        if (combat.Current != BearCombat.State.ChargeWindup) return;
                        var corridor=enemy.GetComponent<ObliqueActor>().GroundWarning.Find("Charge corridor").GetComponent<LineRenderer>();
                        WarningStyle(corridor,.055f);
                        Check(!enemy.GetComponent<BearChargeWind>().Visible,"Charge wind appeared before the dash.");
                        aim = combat.LockedAim; start = enemy.transform.position;
                        Check(arena.Player.Health==health,"Charge windup dealt damage.");
                        pausedProgress = combat.Progress; arena.TogglePause(); pauseUntil = EditorApplication.timeSinceStartup+.35; stage++; break;
                    case 6:
                        if (EditorApplication.timeSinceStartup < pauseUntil) return;
                        Check(combat.Progress==pausedProgress && Vector2.Distance(start,enemy.transform.position)<.001f,"Paused charge continued.");
                        arena.TogglePause(); stage++; break;
                    case 7:
                        if (combat.Current != BearCombat.State.Charging) return;
                        Check(enemy.GetComponent<BearChargeWind>().Visible,"Charge has no wind-break marker.");
                        Check(particles.ChargeTrailEvents>0,"Charge emitted no dust/stone trail.");
                        foreach(var line in enemy.GetComponent<BearChargeWind>().GetComponentsInChildren<LineRenderer>())
                            Check(line.startColor.r > line.startColor.g && line.startColor.g > line.startColor.b,"Charge wind is not ochre.");
                        arena.Player.Teleport((Vector2)arena.Player.transform.position + Vector2.right*3);
                        Check(combat.LockedAim == aim,"Charge retargeted during windup."); stage++; break;
                    case 8:
                        if (combat.Current != BearCombat.State.Recovery) return;
                        Check(!enemy.GetComponent<BearChargeWind>().Visible,"Wind-break marker survived the dash.");
                        Check(arena.Player.Health==health && combat.DamageEvents==events,"Dodged charge still dealt damage.");
                        Check(Vector2.Distance(start,enemy.transform.position)>1,"Charge did not dash.");
                        ArrangeCharge(); stage++; break;
                    case 9:
                        if (combat.Current != BearCombat.State.Charging) return;
                        pauseUntil=0;
                        stage++; break;
                    case 10:
                        if (combat.Current != BearCombat.State.Recovery) return;
                        Check(arena.Player.Health==health-Mathf.RoundToInt(enemy.damage*1.5f) && combat.DamageEvents==events+1,
                            "Charge should hit exactly once while crossing player collider.");
                        Check(particles.ImpactEvents>impactParticles,"Charge contact emitted no impact debris.");
                        if(pauseUntil==0)
                        {
                            BeastParticlesValidation.CaptureActual(arena,enemy.transform.position+arena.View.Up*.6f,3,"Verification/Artifacts/BeastParticles-v1/Bear-charge-impact.png");
                            pauseUntil=EditorApplication.timeSinceStartup+.55; return;
                        }
                        if(EditorApplication.timeSinceStartup<pauseUntil) return;
                        ArrangeCharge(); stage++; break;
                    case 11:
                        if (combat.Current != BearCombat.State.Charging) return;
                        wall = new GameObject("Temporary charge blocker");
                        wall.transform.position = (Vector2)enemy.transform.position + combat.LockedAim*1.1f;
                        wall.AddComponent<BoxCollider2D>().size = Vector2.one*.4f;
                        Physics2D.SyncTransforms(); start = enemy.transform.position; stage++; break;
                    case 12:
                        if (combat.Current != BearCombat.State.Recovery) return;
                        Check(arena.Player.Health==health && Vector2.Distance(start,enemy.transform.position)<1,"Charge crossed a wall.");
                        UnityEngine.Object.Destroy(wall);
                        arena.Player.Teleport((Vector2)enemy.transform.position+Vector2.right*1.25f); health = arena.Player.Health;
                        stage++; break;
                    case 13:
                        if (combat.Current != BearCombat.State.ClawWindup) return;
                        enemy.TakeHit(1,Vector2.zero,0);
                        Check(combat.Current==BearCombat.State.ClawWindup,"Hit cancelled claw skill.");
                        Check(enemy.GetComponent<ObliqueActor>().GroundWarning.gameObject.activeSelf,"Hit hid claw warning.");
                        enemy.TakeHit(100000,Vector2.zero,0);
                        Check(!enemy.Alive && arena.Enemies.Count==0,"Death did not stop skill controller.");
                        arena.TogglePause(); arena.ReturnHome();
                        Check(arena.Travel.Current==TravelDestination.Manor,"Return home failed.");
                        Check(particles.ActiveCount==0 && !particles.Renderer.enabled,"Returning home leaked attack particles.");
                        Debug.Log("BEAR_PARTICLES_PASS: real claw sparks, charge dust/stone trails, contact debris and map cleanup.");
                        SessionState.SetBool(Key,false);
                        Debug.Log("BEAR_SKILLS_PASS: claw at 150% original reach hits beyond old range and matches telegraph; all warnings red at 40% width; ochre charge wind start/stop; exclusive skills, pause, dodge, contact hit once, wall stop, hit immunity, death and cleanup.");
                        EditorApplication.Exit(0); break;
                }
            }
            catch(Exception ex)
            {
                SessionState.SetBool(Key,false); Debug.LogError("BEAR_SKILLS_FAIL: stage="+stage+" "+ex); EditorApplication.Exit(1);
            }
        }
    }
}
