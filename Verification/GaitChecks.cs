using System;
using SlashDemo;
using UnityEngine;

static class GaitChecks
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    static void Main()
    {
        Check(Mathf.Abs(HeroGaitMath.StrideDistance-.44f)<.000001f,"Preserve the current stride during code cleanup.");
        Check(Mathf.Abs(HeroGaitMath.CycleDistance-1.6f/.9f)<.000001f,"Preserve the current cadence during code cleanup.");
        Check(HeroGaitMath.StanceFraction==.60f,"Restore that version's contact/swing timing.");
        float originalAmplitude=.60f/HeroAppearance.BodyHeight*.5f;
        Check(Mathf.Abs(HeroGaitMath.Sample(0).travel/originalAmplitude-.44f)<.000001f,"Actual foot travel preserves the current amplitude.");
        Check(Mathf.Abs(HeroGaitMath.Sample(.8f*Mathf.PI*2).lift-.110f)<.000001f,"Keep the current lift height.");
        Check(Mathf.Abs(HeroGaitMath.BodyBob(Mathf.PI*.5f)-.024f)<.000001f,"Keep the current body bob amplitude.");
        foreach(float scale in new[]{1f,1.25f,1.6f})
        {
            float previousPhaseStep=.1f/(1.6f*scale)*Mathf.PI*2;
            float phaseStep=HeroGaitMath.AdvancePhase(.7f,.1f,scale,false,.016f)-.7f;
            Check(Mathf.Abs(phaseStep-previousPhaseStep*.9f)<.000001f,"Actual phase advancement preserves the current cadence at the same travel distance.");
            Check(HeroGaitMath.AdvancePhase(.7f,0,scale,false,.016f)==.7f,"Blocked movement freezes phase.");
            Check(HeroGaitMath.AdvancePhase(.7f,.1f,scale,true,.016f)==.7f,"Dash freezes phase.");
            Check(HeroGaitMath.AdvancePhase(.7f,.1f,scale,false,0)==.7f,"Pause freezes phase.");
        }
        for(int direction=0;direction<8;direction++)for(int i=0;i<=60;i++)
        {
            float phase=i/60f*Mathf.PI*2;
            var rig=HeroGaitMath.ForDirection(direction);
            var pose=HeroGaitMath.Prepare(rig,phase,1,new Vector2(.6f,.8f));
            var stop=HeroGaitMath.Prepare(rig,phase,0,new Vector2(.6f,.8f));
            for(int part=1;part<=2;part++)
            {
                var leg=part==1?rig.left:rig.right;var binding=HeroGaitMath.Bind(leg.sole,rig);
                Vector3 a=HeroGaitMath.Deform(leg.sole,new Vector2(.5f,.01f),binding,pose,part);
                Vector3 b=HeroGaitMath.Deform(leg.sole+Vector2.right*.02f,new Vector2(.5f,.01f),binding,pose,part);
                Check(!float.IsNaN(a.x)&&!float.IsNaN(a.y),"Eight-direction pose remains finite.");
                Check(Mathf.Abs(Vector3.Distance(a,b)-.02f)<.00001f,"Restore rigid boot skinning.");
                Check(HeroGaitMath.Deform(leg.sole,Vector2.zero,binding,stop,part)==(Vector3)leg.sole,"Stopping restores the standing pose.");
            }
        }
        Console.WriteLine("GAIT_SHORTER_STRIDE_PASS: "+checks+" assertions");
    }
}
