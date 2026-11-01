using UnityEngine;

namespace SlashDemo
{
    // Camera-facing bow waves and trailing air streaks identify the actual dash.
    public sealed class BearChargeWind : MonoBehaviour
    {
        const float LeadingWidth = .12f, SecondaryWidth = .07f, TrailWidth = .075f;
        SlashArena arena;
        Transform visual;
        readonly LineRenderer[] bows = new LineRenderer[2], trails = new LineRenderer[4];
        public bool Visible => visual != null && visual.gameObject.activeSelf;

        public void Initialize(SlashArena owner)
        {
            arena = owner;
            visual = new GameObject("Charge wind break").transform;
            visual.SetParent(transform,false);
            for (int i=0;i<bows.Length;i++)
            {
                bows[i] = DemoArt.Line("Leading wind crescent "+i,visual,new Vector3[19],new Color(.88f,.66f,.30f,.95f),i==0?LeadingWidth:SecondaryWidth,85+i);
                bows[i].widthCurve = new AnimationCurve(new Keyframe(0,.05f),new Keyframe(.5f,1),new Keyframe(1,.05f));
                bows[i].widthMultiplier = i==0?LeadingWidth:SecondaryWidth;
            }
            for (int i=0;i<trails.Length;i++)
            {
                trails[i] = DemoArt.Line("Air streak "+i,visual,new Vector3[7],new Color(.74f,.52f,.23f,.85f),TrailWidth,83);
                trails[i].widthCurve = AnimationCurve.Linear(0,.05f,1,1);
                trails[i].widthMultiplier = TrailWidth;
            }
            Hide();
        }

        public void Present(bool charging,Vector2 aim,float clock)
        {
            if (visual == null) return;
            visual.gameObject.SetActive(charging);
            if (!charging) return;
            visual.position = arena.View.LiftPoint(transform.position,.75f);
            visual.rotation = arena.View.Rotation;
            Vector2 forward = ObliqueProjection.ProjectGround(aim,arena.View.elevation).normalized;
            Vector2 side = new Vector2(-forward.y,forward.x);
            for(int bow=0;bow<bows.Length;bow++) for(int p=0;p<19;p++)
            {
                float t=p/18f*2-1;
                float width=.84f+bow*.14f;
                float nose=.92f-bow*.25f+(1-t*t)*.23f;
                bows[bow].SetPosition(p,forward*nose+side*(t*width));
            }
            for(int trail=0;trail<trails.Length;trail++) for(int p=0;p<7;p++)
            {
                float t=p/6f;
                float offset=(trail-1.5f)*.44f;
                float ripple=Mathf.Sin(clock*24+trail+t*4)*.035f;
                trails[trail].SetPosition(p,forward*Mathf.Lerp(-1.65f,.45f,t)+side*(offset+ripple));
            }
        }

        public void Hide() { if(visual!=null) visual.gameObject.SetActive(false); }
        void OnDisable() { Hide(); }
    }
}
