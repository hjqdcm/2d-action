using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SlashDemo.Editor
{
    public static class BearJointValidation
    {
        // Called by BearSkillValidation in Play Mode, before its normal combat checks.
        public static void Verify(SlashArena arena, SlashEnemy enemy)
        {
            var gait = enemy.GetComponent<BearWalkMesh>();
            var renderer = gait.WalkingRenderer;
            var skin = enemy.transform.Find("Visual/Rock bear skin").GetComponent<SpriteRenderer>();
            var camera = arena.WorldCamera;
            var data = camera.GetUniversalAdditionalCameraData();
            int layer = renderer.gameObject.layer, mask = camera.cullingMask;
            Color background = camera.backgroundColor;
            Vector3 position = camera.transform.position;
            float zoom = camera.orthographicSize;
            bool post = data.renderPostProcessing, flip = skin.flipX;
            int repaired = 0, walkingRepaired = 0;
            arena.TogglePause();
            try
            {
                renderer.gameObject.layer = 31; camera.cullingMask = 1 << 31;
                camera.backgroundColor = Color.black; data.renderPostProcessing = false;
                camera.orthographicSize = 2.8f;
                camera.transform.position = enemy.transform.position + arena.View.Up * 1.1f
                    - ObliqueProjection.Forward(arena.View.elevation)*20;
                Directory.CreateDirectory("Verification/Artifacts/BearJoints");
                gait.SetSkillPose(0,-1,0,Vector2.down);
                gait.Advance(Vector2.zero,1,false);
                gait.Refresh();
                var rest = gait.DeformedMesh.vertices;
                for (int frame = 0; frame < 28; frame++)
                {
                    if (frame < 16)
                        gait.Advance(Vector2.down * (BearWalkMesh.CycleDistance * arena.View.characterScale / 16),.08f,true);
                    else
                    {
                        gait.Advance(Vector2.zero,1,false);
                        if (frame < 24) gait.SetSkillPose((frame-16)/7f,-1,0,Vector2.down);
                        else if(frame < 26) gait.SetSkillPose(0,-1,1,Vector2.down);
                        else { skin.flipX = !flip; gait.SetSkillPose(1,-1,0,Vector2.left); }
                    }
                    gait.Refresh();
                    var pose = gait.DeformedMesh.vertices;
                    for (int part = 0; part < 9; part++)
                        for(int aIndex=0;aIndex<4;aIndex++) for(int bIndex=aIndex+1;bIndex<4;bIndex++)
                        {
                            int i=part*4+aIndex,j=part*4+bIndex;
                            if(Mathf.Abs(Vector3.Distance(rest[i],rest[j])-Vector3.Distance(pose[i],pose[j]))>.00001f)
                                throw new Exception("Joint repair distorted rigid art at pose "+frame);
                        }
                    var filled = ManorOcclusionValidation.Capture(camera);
                    for (int leg = 0; leg < 4; leg++)
                    {
                        Vector3 joint = renderer.transform.TransformPoint(gait.JointPosition(leg));
                        Vector3 uv = camera.WorldToViewportPoint(joint);
                        int x = Mathf.Clamp(Mathf.RoundToInt(uv.x*filled.width),0,filled.width-1);
                        int y = Mathf.Clamp(Mathf.RoundToInt(uv.y*filled.height),0,filled.height-1);
                        Color color = filled.GetPixel(x,y);
                        if(color.r+color.g+color.b < .06f) throw new Exception("Joint " + leg + " exposed background at pose " + frame);
                    }
                    var block = new MaterialPropertyBlock();
                    for (int draw = 0; draw < 9; draw++)
                    {
                        renderer.GetPropertyBlock(block,draw);
                        if(block.GetFloat("_PartIndex") < 3.5f) continue;
                        block.SetFloat("_JointOpacity",0); renderer.SetPropertyBlock(block,draw);
                    }
                    var open = ManorOcclusionValidation.Capture(camera);
                    var a = filled.GetPixels32(); var b = open.GetPixels32();
                    int count = 0;
                    for(int i=0;i<a.Length;i++)
                        if(b[i].r+b[i].g+b[i].b < 10 && a[i].r+a[i].g+a[i].b > 45) count++;
                    repaired += count; if(frame<16) walkingRepaired += count;
                    if(frame==6 || frame==12 || frame==23)
                    {
                        File.WriteAllBytes("Verification/Artifacts/BearJoints/pose-"+frame+"-filled.png",filled.EncodeToPNG());
                        File.WriteAllBytes("Verification/Artifacts/BearJoints/pose-"+frame+"-before.png",open.EncodeToPNG());
                    }
                    UnityEngine.Object.Destroy(filled); UnityEngine.Object.Destroy(open);
                    if(frame < 24)
                    {
                        // Also render the same poses in the real mountain environment.
                        gait.Refresh(); camera.cullingMask=mask; camera.backgroundColor=background; data.renderPostProcessing=post;
                        var world=ManorOcclusionValidation.Capture(camera);
                        File.WriteAllBytes("Verification/Artifacts/BearJoints/frame-"+frame.ToString("D2")+".png",world.EncodeToPNG());
                        UnityEngine.Object.Destroy(world);
                        camera.cullingMask=1<<31; camera.backgroundColor=Color.black; data.renderPostProcessing=false;
                    }
                }
                if(walkingRepaired < 50) throw new Exception("No measurable walking seam coverage: " + walkingRepaired);
                Debug.Log("BEAR_JOINTS_PASS: 28 walk/claw/charge/mirrored poses, four covered joint centers per pose, repaired background pixels="+repaired+", walking="+walkingRepaired);
            }
            finally
            {
                gait.SetSkillPose(0,-1,0,Vector2.zero); gait.Advance(Vector2.zero,1,false);
                skin.flipX = flip; gait.Refresh();
                renderer.gameObject.layer = layer; camera.cullingMask = mask;
                camera.backgroundColor = background; camera.transform.position = position;
                camera.orthographicSize = zoom; data.renderPostProcessing = post;
                arena.TogglePause();
            }
        }
    }
}
