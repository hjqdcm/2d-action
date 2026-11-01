using UnityEngine;

namespace SlashDemo
{
    // An original cone pulse inspired by Petrifying Gaze's casting rhythm.
    [DefaultExecutionOrder(90)]
    public sealed class SerpentGazeVfx : MonoBehaviour
    {
        public const float Duration=.48f, ConeAngle=105;
        const int Rings=20, Slices=48;
        SlashArena arena;
        SnakeAnimation animationMesh;
        Transform source, root;
        Mesh fanMesh;
        MeshRenderer fanRenderer;
        Material fanMaterial;
        Vector3[] vertices;
        Vector3 mouth;
        float charge;
        bool charging;
        public bool Visible => root!=null && root.gameObject.activeSelf;
        public bool BlastActive { get; private set; }
        public float Age { get; private set; }
        public Vector2 Origin { get; private set; }
        public Vector2 Aim { get; private set; }
        public float Reach { get; private set; }
        public int Casts { get; private set; }
        public MeshRenderer FanRenderer => fanRenderer;

        public void Initialize(SlashArena owner)
        {
            arena=owner; animationMesh=GetComponent<SnakeAnimation>(); source=transform.Find("Visual/Jade snake skin");
            root=new GameObject("Serpent petrifying gaze VFX").transform;
            root.SetParent(owner.View.GroundMarks,false);
            fanMesh=new Mesh { name="Serpent gaze curved cone",hideFlags=HideFlags.DontSave };
            fanMesh.MarkDynamic(); root.gameObject.AddComponent<MeshFilter>().sharedMesh=fanMesh;
            fanRenderer=root.gameObject.AddComponent<MeshRenderer>();
            fanMaterial=new Material(Resources.Load<Shader>("SlashDemo/SerpentGaze")) { hideFlags=HideFlags.DontSave };
            fanRenderer.sharedMaterial=fanMaterial;
            fanRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; fanRenderer.receiveShadows=false;
            vertices=new Vector3[(Rings+1)*(Slices+1)]; var uv=new Vector2[vertices.Length]; var triangles=new int[Rings*Slices*6];
            for(int r=0;r<=Rings;r++) for(int a=0;a<=Slices;a++)
            {
                int i=r*(Slices+1)+a; uv[i]=new Vector2(r/(float)Rings,a/(float)Slices);
                if(r==Rings||a==Slices) continue;
                int t=(r*Slices+a)*6;
                triangles[t]=i; triangles[t+1]=i+Slices+2; triangles[t+2]=i+1;
                triangles[t+3]=i; triangles[t+4]=i+Slices+1; triangles[t+5]=i+Slices+2;
            }
            fanMesh.vertices=vertices; fanMesh.uv=uv; fanMesh.triangles=triangles;
            Hide();
        }

        Vector3 Mouth() => source.TransformPoint(animationMesh.PartPoint(1,.725f,.725f));

        public void Gather(float progress,Vector2 aim,float reach)
        {
            if(BlastActive) return;
            charging=true; charge=Mathf.Clamp01(progress); Origin=transform.position; Aim=aim.normalized; Reach=reach;
            mouth=Mouth(); root.gameObject.SetActive(true); fanRenderer.enabled=true;
            fanMaterial.SetFloat("_Mode",0); fanMaterial.SetFloat("_Progress",charge);
            Draw();
        }

        public void Fire(Vector2 aim,float reach)
        {
            charging=false; BlastActive=true; Age=0; Casts++;
            Origin=transform.position; Aim=aim.normalized; Reach=reach;
            animationMesh.Refresh(); mouth=Mouth();
            root.gameObject.SetActive(true); fanRenderer.enabled=true;
            fanMaterial.SetFloat("_Mode",1); fanMaterial.SetFloat("_Progress",0);
            Draw();
        }

        public void Advance(float dt)
        {
            if(arena==null||arena.Paused||dt<=0||!BlastActive) return;
            Age+=dt;
            if(Age>=Duration) { Hide(); return; }
            fanMaterial.SetFloat("_Progress",Age/Duration);
        }

        void Draw()
        {
            root.position=Vector3.zero; root.rotation=Quaternion.identity;
            float heading=Mathf.Atan2(Aim.y,Aim.x)*Mathf.Rad2Deg;
            for(int r=0;r<=Rings;r++) for(int a=0;a<=Slices;a++)
            {
                float radius=r/(float)Rings;
                Vector2 point=Origin+DemoArt.Direction(heading+Mathf.Lerp(-ConeAngle/2,ConeAngle/2,a/(float)Slices))*(Reach*radius);
                Vector3 ground=arena.View.LiftPoint(point,.06f);
                vertices[r*(Slices+1)+a]=Vector3.Lerp(mouth,ground,Mathf.SmoothStep(0,1,radius))
                    +arena.View.Up*(Mathf.Sin(radius*Mathf.PI)*.10f);
            }
            fanMesh.vertices=vertices; fanMesh.RecalculateBounds();
            fanRenderer.sortingOrder=ObliqueProjection.DepthOrder(Origin.y)+45;
        }

        public void RefreshPresentation()
        {
            if(arena==null||arena.Paused||!Visible) return;
            if(charging) { mouth=Mouth(); Draw(); }
        }
        void LateUpdate() { RefreshPresentation(); }
        public void Hide()
        { charging=false; BlastActive=false; Age=0; if(root!=null) root.gameObject.SetActive(false); }
        void OnDisable() { Hide(); }
        void OnDestroy()
        {
            if(root!=null) Destroy(root.gameObject);
            if(fanMesh!=null) Destroy(fanMesh);
            if(fanMaterial!=null) Destroy(fanMaterial);
        }
    }
}
