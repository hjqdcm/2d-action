using UnityEngine;
namespace SlashDemo
{
    // Bear-style footfall timing with rigid extremities and continuous shoulder/hip transitions.
    [DefaultExecutionOrder(80)]
    public sealed class ApeAnimation : MonoBehaviour
    {
        public const float CycleDistance=1.1f;
        const float Stance=.62f, Reach=CycleDistance*Stance*.5f, Lift=.20f;
        static readonly Vector2[] Joints={new Vector2(.49f,.405f),new Vector2(.69f,.34f),new Vector2(.47f,.66f),new Vector2(.79f,.64f)};
        static readonly Vector2[] Ends={new Vector2(.29f,.105f),new Vector2(.73f,.14f)};
        const int Grid=64;
        Vector2[] coordinates;
        Vector4[] weights;
        SpriteRenderer source; SlashArena arena; SlashEnemy enemy; Rigidbody2D body;
        Mesh mesh; MeshRenderer skin; Material material;
        readonly Vector2[] pivots=new Vector2[4],offsets=new Vector2[4],rotations=new Vector2[4];
        readonly float[] angles=new float[4];
        Vector3[] rest,posed;
        Vector2 previous,motion=new Vector2(.7f,-.5f);
        float phase,blend,speed,windup,swing,bob;
        public float Phase=>phase;
        public float Blend=>blend;
        public Mesh DeformedMesh=>mesh;
        public MeshRenderer SkinRenderer=>skin;
        public void Initialize(SpriteRenderer original,SlashArena world)
        {
            source=original; arena=world; body=GetComponent<Rigidbody2D>(); previous=body.position;
            original.forceRenderingOff=true;
            var root=new GameObject("Ape articulated walking skin"); root.transform.SetParent(original.transform,false);
            mesh=new Mesh {name="Ape continuous joints and rigid extremities",hideFlags=HideFlags.DontSave}; mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh=mesh; skin=root.AddComponent<MeshRenderer>();
            material=new Material(Resources.Load<Shader>("SlashDemo/ApeSkin")){hideFlags=HideFlags.DontSave}; material.mainTexture=original.sprite.texture;
            skin.sharedMaterial=material;
            skin.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; skin.receiveShadows=false;
            rest=new Vector3[(Grid+1)*(Grid+1)];posed=new Vector3[rest.Length];coordinates=new Vector2[rest.Length];weights=new Vector4[rest.Length];
            var triangles=new int[Grid*Grid*6];
            for(int limb=0;limb<4;limb++) pivots[limb]=ArtPoint(Joints[limb]);
            for(int y=0;y<=Grid;y++)for(int x=0;x<=Grid;x++)
            {
                int i=y*(Grid+1)+x;var uv=new Vector2(x/(float)Grid,y/(float)Grid);
                coordinates[i]=uv;rest[i]=ArtPoint(uv);weights[i]=Weights(uv);
                if(x<Grid&&y<Grid)
                {
                    int t=(y*Grid+x)*6;triangles[t]=i;triangles[t+1]=i+Grid+2;triangles[t+2]=i+1;
                    triangles[t+3]=i;triangles[t+4]=i+Grid+1;triangles[t+5]=i+Grid+2;
                }
            }
            mesh.vertices=rest;mesh.uv=coordinates;mesh.triangles=triangles;
            Refresh();
        }
        static Vector4 Weights(Vector2 p)
        {
            float tail=(1-Mathf.SmoothStep(.29f,.38f,p.x))*Mathf.SmoothStep(.47f,.60f,p.y);
            float cloth=Mathf.SmoothStep(.48f,.55f,p.x)*(1-Mathf.SmoothStep(.63f,.70f,p.x))*(1-Mathf.SmoothStep(.38f,.49f,p.y));
            float leftArm=(1-Mathf.SmoothStep(.29f,.42f,p.x))*(1-Mathf.SmoothStep(.52f,.72f,p.y))*Mathf.SmoothStep(.18f,.26f,p.y)*(1-tail);
            float rightArm=Mathf.SmoothStep(.75f,.84f,p.x)*(1-Mathf.SmoothStep(.55f,.74f,p.y));
            float leftLeg=(1-Mathf.SmoothStep(.28f,.47f,p.y))*(1-Mathf.SmoothStep(.50f,.63f,p.x))*(1-leftArm)*(1-cloth);
            float rightLeg=(1-Mathf.SmoothStep(.25f,.42f,p.y))*Mathf.SmoothStep(.55f,.65f,p.x)*(1-Mathf.SmoothStep(.81f,.88f,p.x))*(1-rightArm)*(1-cloth);
            Vector4 w=new Vector4(leftLeg,rightLeg,leftArm,rightArm);float total=w.x+w.y+w.z+w.w;
            return total>1?w/total:w;
        }
        Vector2 ArtPoint(Vector2 uv){var b=source.sprite.bounds; return (Vector2)b.min+Vector2.Scale(b.size,uv);}
        public static Vector2 Footfall(float cycle)
        {
            float t=Mathf.Repeat(cycle,1);
            if(t<Stance) return new Vector2(Mathf.Lerp(Reach,-Reach,t/Stance),0);
            float s=(t-Stance)/(1-Stance);
            return new Vector2(Mathf.Lerp(-Reach,Reach,Mathf.SmoothStep(0,1,s)),Mathf.Sin(s*Mathf.PI)*Lift);
        }
        public void Advance(Vector2 displacement,float dt,bool walking)
        {
            if(dt<=0)return;
            float travel=displacement.magnitude;
            if(travel>1.5f){travel=0;walking=false;}
            speed=Mathf.Lerp(speed,walking?travel/dt:0,1-Mathf.Exp(-14*dt));
            blend=Mathf.MoveTowards(blend,walking?Mathf.Clamp01(speed/.65f):0,dt*8);
            if(walking&&travel>.00001f)
            {
                phase=Mathf.Repeat(phase+travel/(CycleDistance*Mathf.Max(.1f,arena.View.characterScale)),1);
                motion=ObliqueProjection.ProjectGround(displacement.normalized,arena.View.elevation);
            }
        }
        public void SetAttack(float preparation,float extension){windup=preparation;swing=extension;}
        static Vector2 Rotate(Vector2 p,float a){float c=Mathf.Cos(a),s=Mathf.Sin(a);return new Vector2(p.x*c-p.y*s,p.x*s+p.y*c);}
        Vector3 Pose(Vector2 point,int limb)
        {
            Vector2 p=point;
            if(limb>=0)
            {
                Vector2 d=point-pivots[limb],r=rotations[limb];
                p=pivots[limb]+new Vector2(d.x*r.x-d.y*r.y,d.x*r.y+d.y*r.x)+offsets[limb];
            }
            p.y+=bob;if(source.flipX)p.x=-p.x;return p;
        }
        Vector3 SkinPoint(Vector2 point,Vector4 w)
        {
            Vector3 p=Pose(point,-1)*(1-w.x-w.y-w.z-w.w);
            for(int limb=0;limb<4;limb++)if(w[limb]>0)p+=Pose(point,limb)*w[limb];return p;
        }
        public Vector3 JointPosition(int limb)=>SkinPoint(pivots[limb],Weights(Joints[limb]));
        public Vector3 FootPosition(int leg)=>Pose(ArtPoint(Ends[leg]),leg);
        public void Refresh()
        {
            if(mesh==null)return;
            Vector2 heading=motion;if(source.flipX)heading.x=-heading.x;
            bob=(1-Mathf.Cos(phase*Mathf.PI*4))*.018f*blend;
            for(int limb=0;limb<4;limb++)
            {
                bool leg=limb<2;float shift=limb==0||limb==3?0:.5f;Vector2 step=Footfall(phase+shift);
                float angle=step.x/Reach*(leg?10:19)*blend;
                if(!leg)angle+=(limb==2?-1:1)*(windup*24-swing*40);
                angles[limb]=angle*Mathf.Deg2Rad;
                rotations[limb]=new Vector2(Mathf.Cos(angles[limb]),Mathf.Sin(angles[limb]));
                if(leg)
                {
                    Vector2 end=ArtPoint(Ends[limb])-pivots[limb];
                    offsets[limb]=(heading*step.x+Vector2.up*step.y)*blend-(Rotate(end,angles[limb])-end)-Vector2.up*bob;
                }
                else offsets[limb]=heading*(step.x*.10f*blend)+Vector2.up*(windup*.04f);
            }
            for(int i=0;i<rest.Length;i++)
            {
                posed[i]=SkinPoint(rest[i],weights[i]);
            }
            material.SetColor("_Color",source.color);
            mesh.vertices=posed;mesh.RecalculateBounds();skin.sortingLayerID=source.sortingLayerID;skin.sortingOrder=source.sortingOrder;skin.enabled=source.enabled;
        }
        void LateUpdate()
        {
            if(source==null)return;if(enemy==null)enemy=GetComponent<SlashEnemy>();Vector2 now=body.position;
            if(!arena.Paused&&enemy.Alive)Advance(now-previous,Time.deltaTime,enemy.IsChasing);
            previous=now;Refresh();
        }
        void OnEnable(){if(source!=null)source.forceRenderingOff=true;previous=transform.position;}
        void OnDisable(){if(source!=null)source.forceRenderingOff=false;if(skin!=null)skin.enabled=false;}
        void OnDestroy(){if(source!=null)source.forceRenderingOff=false;if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
