using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SlashDemo.Editor
{
    public static class ApeDemonValidation
    {
        const string Key = "SlashDemo.ApeDemonValidation";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static SlashArena arena;
        static SlashEnemy[] pack;
        static double deadline, next, nextFrame;
        static int stage, frame, checks;
        static Vector2 initial;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        public static void RunBatch()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        static void Hook()
        {
            deadline = EditorApplication.timeSinceStartup + 120;
            next = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        static void Capture(string name, Vector3 focus, float zoom = 4)
        {
            var camera = arena.WorldCamera; var oldPosition = camera.transform.position; float oldZoom = camera.orthographicSize;
            camera.transform.position = focus - ObliqueProjection.Forward(arena.View.elevation) * 20;
            camera.orthographicSize = zoom;
            var image = ManorOcclusionValidation.Capture(camera);
            string path = "Verification/Artifacts/MountainApe-v1/" + name + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image); camera.transform.position = oldPosition; camera.orthographicSize = oldZoom;
        }
        static void VerifyGait()
        {
            var enemy=pack[0]; var gait=enemy.GetComponent<ApeAnimation>();
            var source=enemy.transform.Find("Visual/Mountain ape skin").GetComponent<SpriteRenderer>();
            var renderer=gait.SkinRenderer; var camera=arena.WorldCamera;
            int oldMask=camera.cullingMask,oldLayer=renderer.gameObject.layer;
            Color background=camera.backgroundColor; var clear=camera.clearFlags;
            bool post=camera.GetUniversalAdditionalCameraData().renderPostProcessing,flip=source.flipX;
            var uv=gait.DeformedMesh.uv; float scale=arena.View.characterScale;
            var soleSamples = new[] { new Vector2(.29f, .105f), new Vector2(.73f, .14f) };
            var soleVertices = soleSamples.Select(sole => MeshIndex(uv, sole)).ToArray();
            var paintSamples = new[] { new Vector2(.27f,.10f), new Vector2(.70f,.13f), new Vector2(.22f,.31f), new Vector2(.88f,.22f), new Vector2(.71f,.80f) };
            var paintVertices = paintSamples.Select(point => MeshIndex(uv, point)).ToArray();
            gait.Advance(Vector2.zero,1,false); gait.SetAttackPose(ApeCombat.State.Pursuit, 0); gait.Refresh();
            var rest=gait.DeformedMesh.vertices;
            var soleDistances = soleVertices.Select((vertex, leg) => Vector3.Distance(rest[vertex], gait.FootPosition(leg))).ToArray();
            try
            {
                for(int frame=0;frame<48;frame++)
                {
                    source.flipX=frame>=32;
                    Vector2 direction = source.flipX ? Vector2.left : Vector2.right;
                    gait.Advance(direction*(ApeAnimation.CycleDistance*scale/32),.04f,true); gait.Refresh();
                    var pose=gait.DeformedMesh.vertices;
                    for (int leg = 0; leg < soleVertices.Length; leg++)
                        Check(Mathf.Abs(Vector3.Distance(pose[soleVertices[leg]], gait.FootPosition(leg)) - soleDistances[leg]) < .002f,
                            "The visible sole drifted away from its footfall.");
                    foreach (int a in paintVertices)
                    {
                        int b = a + 1;
                        Check(Mathf.Abs(Vector3.Distance(rest[a],rest[b])-Vector3.Distance(pose[a],pose[b]))<.002f,"Walking distorted hand/foot/head paint.");
                    }
                    Check(gait.DeformedMesh.uv.SequenceEqual(uv),"Walking changed artwork UVs.");
                    Vector3 oldPos=camera.transform.position; float oldZoom=camera.orthographicSize;
                    renderer.gameObject.layer=31;camera.cullingMask=1<<31;camera.backgroundColor=Color.black;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                    camera.transform.position=enemy.transform.position+arena.View.Up*1.1f-ObliqueProjection.Forward(arena.View.elevation)*20;
                    camera.orthographicSize=2.2f;
                    var image=ManorOcclusionValidation.Capture(camera);
                    for(int limb=0;limb<4;limb++)
                    {
                        var p=camera.WorldToViewportPoint(renderer.transform.TransformPoint(gait.JointPosition(limb)));
                        Color color=image.GetPixel(Mathf.Clamp((int)(p.x*image.width),0,image.width-1),Mathf.Clamp((int)(p.y*image.height),0,image.height-1));
                        Check(color.r+color.g+color.b>.06f,"Ape shoulder/hip reveals background at frame "+frame+" limb "+limb);
                    }
                    UnityEngine.Object.Destroy(image);
                    camera.transform.position=oldPos;camera.orthographicSize=oldZoom;renderer.gameObject.layer=oldLayer;
                    camera.cullingMask=oldMask;camera.backgroundColor=background;camera.clearFlags=clear;
                    camera.GetUniversalAdditionalCameraData().renderPostProcessing=post;
                    Capture("Gait/frame-"+frame.ToString("D3"),enemy.transform.position+arena.View.Up*1.1f,2.2f);
                }
                float phase=gait.Phase;gait.Advance(Vector2.right,0,true);Check(gait.Phase==phase,"Frozen step advanced gait.");
                gait.Advance(Vector2.zero,1,false);gait.Refresh();Check(gait.Blend==0,"Stopped ape kept stepping.");
                phase=gait.Phase;gait.Advance(Vector2.right*20,.1f,true);Check(gait.Phase==phase,"Teleport advanced gait phase.");
                Check(ApeAnimation.Footfall(.2f).y==0&&ApeAnimation.Footfall(.8f).y>.1f,"Foot contact and lift phases are missing.");
                Debug.Log("APE_GAIT_PASS: 48 walking/mirrored poses; stable hands/feet/head; continuous joints; fixed UVs; four GPU-covered joints per pose; planted/lifted feet; stop/freeze/teleport.");
            }
            finally
            {
                source.flipX=flip;gait.Advance(Vector2.zero,1,false);gait.Refresh();
                renderer.gameObject.layer=oldLayer;camera.cullingMask=oldMask;camera.backgroundColor=background;camera.clearFlags=clear;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=post;
            }
        }
        static void VerifyWallPursuit(SlashEnemy enemy, Rigidbody2D body)
        {
            var map = arena.Travel.ActiveMap;
            var wall = map.GetComponentInChildren<EdgeCollider2D>();
            var navigation = enemy.GetComponent<ApeDemon>().Navigation;
            Vector2 wallPoint = map.Definition.Ground(820, 1005);
            Vector2 start = wallPoint + Vector2.up * (enemy.Radius + .4f);
            body.position = start;
            body.transform.position = start;
            body.linearVelocity = Vector2.zero;
            arena.Player.Teleport(start + Vector2.up * 5);
            Physics2D.SyncTransforms();

            float dt = Time.fixedDeltaTime;
            var fixedUpdate = typeof(SlashEnemy).GetMethod("FixedUpdate", Private);
            var simulation = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                for (int step = 0; step < 25; step++)
                {
                    body.linearVelocity = navigation.ConstrainVelocity(Vector2.down * enemy.moveSpeed, dt);
                    Physics2D.Simulate(dt);
                }
                Vector2 contact = body.position;
                float clearance = contact.y - wallPoint.y;
                Check(clearance >= enemy.Radius && clearance < enemy.Radius + wall.edgeRadius + .025f,
                    "The ape did not stop against the wall.");
                for (int step = 0; step < 25; step++)
                {
                    fixedUpdate.Invoke(enemy, null);
                    Physics2D.Simulate(dt);
                }
                Check(body.position.y > contact.y + .5f && navigation.Walkable(body.position),
                    "The ape could not leave wall contact to pursue the player.");
            }
            finally { Physics2D.simulationMode = simulation; }
        }

        static void VerifyCombat()
        {
            var enemy = pack[0]; var combat = enemy.GetComponent<ApeCombat>(); var animation = enemy.GetComponent<ApeAnimation>();
            var actor = enemy.GetComponent<ObliqueActor>(); var body = enemy.GetComponent<Rigidbody2D>();
            foreach (var other in pack.Skip(1)) other.gameObject.SetActive(false);
            combat.Stop();
            VerifyWallPursuit(enemy, body);
            body.position = Vector2.zero; body.transform.position = Vector2.zero; body.linearVelocity = Vector2.zero;
            enemy.moveSpeed = 0; combat.Stop(); Set(combat, "nextPound", 1000f);
            arena.Player.invincible = false; Set(arena.Player, "invulnerability", 0f);
            arena.Player.Teleport(Vector2.down * enemy.attackRange * .9f); Physics2D.SyncTransforms();
            int health = arena.Player.Health, events = combat.DamageEvents;
            combat.Tick(.01f);
            Check(combat.Current == ApeCombat.State.PalmWindup, "Palm did not start within reach.");
            var arc = actor.GroundWarning.Find("Ape palm warning").GetComponent<LineRenderer>();
            var sweep = actor.GroundWarning.Find("Ape palm sweep").GetComponent<LineRenderer>();
            Check(Mathf.Abs(arc.GetPosition(1).magnitude * arc.transform.localScale.x - enemy.attackRange) < .001f, "Palm warning radius mismatches damage.");
            Vector2 aim = combat.LockedAim;
            for (int i = 0; i < 16; i++)
            { combat.Tick(enemy.windupTime / 16 + .00001f); animation.Refresh(); Capture("Palm/frame-" + i.ToString("D3"), arena.View.Up * .7f); }
            Check(combat.Current == ApeCombat.State.PalmSwing && combat.DamageEvents == events, "Palm hit before the swing.");
            Vector3 drawnFist = MeshPoint(animation, .88f, .20f);
            for (int i = 0; i < 16; i++)
            {
                combat.Tick(ApeCombat.PalmDuration / 16); animation.Refresh();
                if (i == 8)
                    Check(Vector3.Distance(drawnFist, MeshPoint(animation, .88f, .20f)) > .5f && sweep.enabled && !arc.enabled,
                        "Palm did not sweep its fist and trail during the strike.");
                Capture("Palm/frame-" + (16 + i).ToString("D3"), arena.View.Up * .7f);
            }
            Check(!sweep.enabled, "Palm trail lingered after the swing.");
            Check(combat.DamageEvents == events + 1 && arena.Player.Health == health - enemy.damage, "Palm did not deal exactly one normal hit.");
            Check(arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>().ApePalmEvents == 1, "Palm particles missing.");
            for (int i = 0; i < 16; i++)
            {
                combat.Tick(ApeCombat.RecoveryDuration / 16); animation.Refresh();
                arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>().Advance(ApeCombat.RecoveryDuration / 16);
                Capture("Palm/frame-" + (32 + i).ToString("D3"), arena.View.Up * .7f);
            }
            combat.Stop(); combat.Tick(.01f); aim = combat.LockedAim;
            arena.Player.Teleport(-aim * (enemy.attackRange + 1)); Physics2D.SyncTransforms();
            Set(arena.Player, "invulnerability", 0f);
            combat.Tick(enemy.windupTime + .01f); combat.Tick(ApeCombat.PalmDuration * .6f);
            Check(combat.LockedAim == aim && combat.DamageEvents == events + 1, "Palm followed or hit a dodging target.");

            VerifyPound(enemy, combat, animation, body);
            foreach (var other in pack.Skip(1)) other.gameObject.SetActive(true);
            enemy.TakeHit(100000, Vector2.zero, 0);
            Check(!enemy.Alive && !actor.GroundWarning.gameObject.activeSelf && arena.Travel.Portal == null, "Death cleanup or early portal incorrect.");
            foreach (var other in pack.Skip(1)) other.TakeHit(100000, Vector2.zero, 0);
            Check(arena.Enemies.Count == 0 && arena.Travel.Portal != null, "Ape clear did not spawn a portal.");
            Debug.Log("APE_COMBAT_PASS: locked palm; takeoff target and warning; untargetable in flight; safe landings, dodge, pause, particles and cleanup.");
        }

        static void VerifyPound(SlashEnemy enemy, ApeCombat combat, ApeAnimation animation, Rigidbody2D body)
        {
            var collider = enemy.GetComponent<CircleCollider2D>();
            var ring = arena.View.GroundMarks.Find("Ape ground pound warning").GetComponent<LineRenderer>();
            var impact = arena.View.GroundMarks.Find("Ape landing shockwave").GetComponent<LineRenderer>();
            var particles = arena.Travel.ActiveMap.GetComponent<BeastAttackParticles>();
            var actor = enemy.GetComponent<ObliqueActor>();
            float groundedLabelHeight = actor.LabelHeight;
            combat.Stop(); Set(combat, "nextPound", 0f); Set(arena.Player, "invulnerability", 0f);
            animation.Refresh();
            float leftFistHeight = MeshPoint(animation, .22f, .30f).y;
            float rightFistHeight = MeshPoint(animation, .88f, .20f).y;
            float footHeight = animation.FootPosition(1).y;
            arena.Player.Teleport(Vector2.down * (ApeCombat.PoundRadius * .9f)); Physics2D.SyncTransforms();
            int health = arena.Player.Health, events = combat.DamageEvents;
            combat.Tick(.01f);
            Check(combat.Current == ApeCombat.State.PoundWindup && !ring.enabled && collider.enabled,
                "Pound windup should be grounded and unmarked.");
            int apeHealth = enemy.Health;
            enemy.TakeHit(1, Vector2.right, 30);
            Check(enemy.Health == apeHealth - 1 && combat.Current == ApeCombat.State.PoundWindup, "Windup could not be hit.");
            for (int i = 0; i < ring.positionCount; i++)
                Check(Mathf.Abs(ring.GetPosition(i).magnitude - ApeCombat.PoundRadius) < .001f, "Pound warning mismatches damage radius.");
            Vector2 target = new Vector2(1.25f, -1.75f);
            Vector3 focus = (Vector3)(target * .5f) + arena.View.Up * 1.2f;
            for (int i = 0; i < 20; i++)
            {
                if (i == 10) arena.Player.Teleport(target);
                combat.Tick(ApeCombat.PoundWindup / 20 + .00001f);
                animation.Refresh(); Capture("Pound/frame-" + i.ToString("D3"), focus);
            }
            Check(combat.IsAirborne && ring.enabled && (Vector2)ring.transform.position == target, "Pound did not mark the takeoff target.");
            Check(!collider.enabled && !Physics2D.OverlapPointAll(body.position).Contains(collider), "Airborne ape was available to attack queries.");
            apeHealth = enemy.Health;
            enemy.TakeHit(100000, Vector2.right, 30, true);
            Check(enemy.Health == apeHealth && combat.IsAirborne, "Airborne ape took damage.");
            arena.Player.Teleport(target + Vector2.right * (ApeCombat.PoundRadius - .15f));
            arena.TogglePause();
            Vector2 takeoff = body.position;
            combat.Tick(1);
            Check(body.position == takeoff && combat.Progress == 0 && ring.enabled && arena.Player.Health == health, "Pause advanced the jump.");
            arena.TogglePause();
            for (int i = 0; i < 20; i++)
            {
                combat.Tick(ApeCombat.PoundAirDuration / 20 + .00001f); animation.Refresh();
                particles.Advance(.045f);
                if (i < 19)
                    Check(combat.DamageEvents == events && ring.enabled && (Vector2)ring.transform.position == target,
                        "Pound hit early or moved its warning in flight.");
                if (i == 9)
                {
                    Check(Vector2.Distance(body.position, target) < Vector2.Distance(takeoff, target)
                        && Vector3.Dot(enemy.transform.Find("Visual").localPosition, arena.View.Up) > 1, "Ape did not jump toward the target.");
                    Check(actor.LabelHeight > groundedLabelHeight + 1, "The ape's health label did not follow its jump.");
                    Check(MeshPoint(animation, .22f, .30f).y > leftFistHeight + .25f
                        && MeshPoint(animation, .88f, .20f).y > rightFistHeight + .25f
                        && animation.FootPosition(1).y > footHeight + .2f, "Pound did not raise both fists and tuck its feet.");
                }
                Capture("Pound/frame-" + (20 + i).ToString("D3"), focus);
            }
            Check(combat.DamageEvents == events + 1 && arena.Player.Health == health - Mathf.RoundToInt(enemy.damage * 1.8f), "Pound damage or event count incorrect.");
            Check(!combat.IsAirborne && collider.enabled && !ring.enabled && body.position == target, "Landing did not restore the ape at its target.");
            Check(impact.enabled && (Vector2)impact.transform.position == target && particles.ApeSlamEvents == 1, "Impact effects did not use the warning center.");
            enemy.TakeHit(1, Vector2.right, 0);
            Check(enemy.Health == apeHealth - 1, "Landed ape was still immune.");
            for (int i = 0; i < 16; i++)
            {
                combat.Tick(ApeCombat.RecoveryDuration / 16); animation.Refresh();
                particles.Advance(.05f);
                Capture("Pound/frame-" + (40 + i).ToString("D3"), focus);
            }
            Vector2 dodgeTarget = body.position + Vector2.up * 2;
            StartPound(combat, dodgeTarget);
            arena.Player.Teleport(dodgeTarget + Vector2.down * (ApeCombat.PoundRadius + .29f + .4f));
            Set(arena.Player, "invulnerability", 0f);
            combat.Tick(ApeCombat.PoundAirDuration + .01f);
            Check(combat.DamageEvents == events + 1, "Dodging outside the pound circle still took damage.");

            var map = arena.Travel.ActiveMap.Definition;
            body.position = body.transform.position = map.Ground(820, 925);
            Vector2 edgeTarget = map.Ground(820, 985);
            var navigation = enemy.GetComponent<ApeDemon>().Navigation;
            Check(!navigation.Walkable(edgeTarget), "Edge test did not use a player-only position.");
            StartPound(combat, edgeTarget);
            combat.Tick(ApeCombat.PoundAirDuration + .01f);
            Check(navigation.Walkable(body.position) && Vector2.Distance(body.position, edgeTarget) < 1
                && (Vector2)impact.transform.position == edgeTarget, "Edge landing crossed the wall or moved the impact.");

            var ally = pack[1]; var allyBody = ally.GetComponent<Rigidbody2D>();
            var allyCombat = ally.GetComponent<ApeCombat>();
            body.position = body.transform.position = Vector2.left * 2;
            allyBody.position = allyBody.transform.position = Vector2.right * 2;
            ally.gameObject.SetActive(true);
            StartPound(combat, Vector2.zero); StartPound(allyCombat, Vector2.zero);
            combat.Tick(ApeCombat.PoundAirDuration + .01f); allyCombat.Tick(ApeCombat.PoundAirDuration + .01f);
            Check(Vector2.Distance(body.position, allyBody.position) >= enemy.Radius + ally.Radius, "Two apes landed inside each other.");
            ally.gameObject.SetActive(false);

            Vector2 origin = body.position;
            StartPound(combat, origin + Vector2.down * 2);
            combat.Tick(ApeCombat.PoundAirDuration * .5f);
            enemy.gameObject.SetActive(false);
            Check(!ring.enabled && !impact.enabled && collider.enabled && body.position == origin && combat.IsChasing, "Disable leaked airborne state or effects.");
            enemy.gameObject.SetActive(true);
        }

        static void StartPound(ApeCombat combat, Vector2 target)
        {
            combat.Stop(); Set(combat, "nextPound", 0f);
            arena.Player.Teleport(target); Physics2D.SyncTransforms();
            combat.Tick(.01f);
            combat.Tick(ApeCombat.PoundWindup + .01f);
            Check(combat.IsAirborne, "Pound did not take off.");
        }

        static Vector3 MeshPoint(ApeAnimation animation, float x, float y)
            => animation.DeformedMesh.vertices[MeshIndex(animation.DeformedMesh.uv, new Vector2(x, y))];

        static int MeshIndex(Vector2[] uv, Vector2 sample)
        {
            int closest = 0;
            for (int i = 1; i < uv.Length; i++)
                if ((uv[i] - sample).sqrMagnitude < (uv[closest] - sample).sqrMagnitude) closest = i;
            return closest;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Ape test timed out at " + stage);
                if (stage == 2 && EditorApplication.timeSinceStartup >= nextFrame)
                { nextFrame = EditorApplication.timeSinceStartup + .07; Capture("Walk/frame-" + (frame++).ToString("D3"), pack[0].transform.position + arena.View.Up * .8f, 3); }
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + .35;
                switch (stage)
                {
                    case 0:
                        arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>(); if (arena == null || arena.Travel == null) return;
                        arena.Player.invincible = true; arena.Player.Teleport(SceneTravel.ManorGate.center); stage = 1; break;
                    case 1:
                        WildernessEncounterValidation.Enter(arena, WildernessCreature.Ape); pack = arena.Enemies.ToArray();
                        foreach (var enemy in pack)
                        {
                            var ape = enemy.GetComponent<ApeDemon>();
                            Check(enemy.maxHealth == Mathf.RoundToInt(200 * arena.Tuning.Settings.enemyHealthMultiplier), "Ape health incorrect.");
                            Check(enemy.damage == 10 && Mathf.Abs(enemy.moveSpeed - arena.Player.moveSpeed * .6f) < .001f, "Ape damage or speed incorrect.");
                            Check(ape.Navigation.Walkable(enemy.transform.position), "Ape spawn overlaps map bounds.");
                            Check(!ShaderUtil.ShaderHasError(enemy.GetComponent<ApeAnimation>().SkinRenderer.sharedMaterial.shader), "Ape skin shader error.");
                        }
                        Capture("Formation", (Vector3)arena.Travel.ActiveMap.Definition.Ground(810, 360) + arena.View.Up * .9f, 7);
                        initial = pack[0].transform.position; stage = 2; next = EditorApplication.timeSinceStartup + 1.7; break;
                    case 2:
                        Check(Vector2.Distance(initial, pack[0].transform.position) > .4f && frame > 5 && pack[0].GetComponent<ApeAnimation>().Phase != 0, "Ape failed live pursuit/animation.");
                        VerifyGait(); VerifyCombat(); stage = 3; next = EditorApplication.timeSinceStartup + .6; break;
                    case 3:
                        Check(arena.Travel.ActiveMap.GetComponentsInChildren<SlashEnemy>().Length == 0, "Cleared ape map respawned enemies.");
                        var oldMap = arena.Travel.ActiveMap;
                        arena.TogglePause(); arena.ReturnHome();
                        Check(!oldMap.gameObject.activeInHierarchy && arena.Enemies.Count == 3, "Return home leaked apes or lost manor actors.");
                        arena.Player.Teleport(SceneTravel.ManorGate.center); stage = 4; break;
                    case 4:
                        WildernessEncounterValidation.Enter(arena, WildernessCreature.Ape);
                        Check(arena.Enemies.Count == 3, "Reentry duplicated ape pack.");
                        arena.Restart(); Check(arena.Enemies.Count == 3 && arena.Travel.Current == TravelDestination.Manor, "Restart did not clean up ape map.");
                        Debug.Log("APE_DEMON_PASS: " + checks + " checks; three apes; spawn bounds; actual pursuit/animated render; combat; cleanup; portal; return/reentry/restart.");
                        SessionState.SetBool(Key, false); EditorApplication.Exit(0); break;
                }
            }
            catch (Exception error)
            { Debug.LogError("APE_DEMON_FAIL: stage=" + stage + " " + error); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
