using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    // A bounded, single-draw particle batch shared by the map's creatures.
    public sealed class BeastAttackParticles : MonoBehaviour
    {
        public const int MaxParticles=384;
        struct Fleck
        {
            public Vector2 ground,velocity;
            public float height,rise,gravity,age,life,size,angle,spin;
            public int kind;
            public Color color;
        }
        readonly List<Fleck> active=new List<Fleck>(MaxParticles);
        readonly List<Vector3> vertices=new List<Vector3>(MaxParticles*4),uvs=new List<Vector3>(MaxParticles*4);
        readonly List<Color> colors=new List<Color>(MaxParticles*4);
        readonly List<int> triangles=new List<int>(MaxParticles*6);
        SlashArena arena;
        Transform root;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Material material;
        bool dirty;
        uint random=0x725c49a3;
        public int ActiveCount => active.Count;
        public float Clock { get; private set; }
        public int ClawEvents { get; private set; }
        public int ChargeTrailEvents { get; private set; }
        public int ImpactEvents { get; private set; }
        public int GazeEvents { get; private set; }
        public int SprayEvents { get; private set; }
        public int SplashEvents { get; private set; }
        public int MistEvents { get; private set; }
        public int ApePalmEvents { get; private set; }
        public int ApeSlamEvents { get; private set; }
        public MeshRenderer Renderer => meshRenderer;
        public static BeastAttackParticles For(SlashArena arena,Transform owner)
        {
            var map=owner.GetComponentInParent<TravelMapEnvironment>();
            var fx=map.GetComponent<BeastAttackParticles>();
            if(fx==null) { fx=map.gameObject.AddComponent<BeastAttackParticles>(); fx.Initialize(arena); }
            return fx;
        }
        void Initialize(SlashArena owner)
        {
            arena=owner; root=new GameObject("Beast attack particles").transform; root.SetParent(transform,false);
            root.position=Vector3.zero; root.rotation=Quaternion.identity;
            mesh=new Mesh { name="Batched creature dust, shards and venom",hideFlags=HideFlags.DontSave }; mesh.MarkDynamic();
            root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            meshRenderer=root.gameObject.AddComponent<MeshRenderer>();
            material=new Material(Resources.Load<Shader>("SlashDemo/BeastParticles")) { hideFlags=HideFlags.DontSave };
            meshRenderer.sharedMaterial=material; meshRenderer.sortingOrder=15000;
            meshRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; meshRenderer.receiveShadows=false;
            meshRenderer.enabled=false;
        }
        float R(float min,float max)
        { random^=random<<13; random^=random>>17; random^=random<<5; return Mathf.Lerp(min,max,(random&0xffffff)/16777216f); }
        void Emit(Vector2 ground,float height,Vector2 velocity,float rise,int kind,Color color,float size,float life)
        {
            if(active.Count>=MaxParticles) return;
            dirty=true;
            active.Add(new Fleck { ground=ground,height=height,velocity=velocity,rise=rise,
                gravity=kind==1||kind==2?4.5f:0,kind=kind,color=color,size=size,life=life,
                angle=R(0,360),spin=R(-140,140) });
        }
        public void DustBurst(Vector2 point,int count=8,float power=1)
        {
            for(int i=0;i<count && active.Count<MaxParticles;i++)
                Emit(point+DemoArt.Direction(R(0,360))*R(0,.3f),.04f,DemoArt.Direction(R(0,360))*R(.15f,.8f)*power,
                    R(.1f,.35f),0,new Color(.59f,.43f,.25f,.38f),R(.15f,.28f),R(.35f,.7f));
        }
        public void ChargeTrail(Vector2 point,Vector2 direction,bool charging)
        {
            if(charging) ChargeTrailEvents++;
            DustBurst(point-direction*(charging?.7f:.15f),charging?5:3,charging?1.3f:.65f);
            if(charging) Emit(point,.12f,-direction*R(.4f,1.4f),R(.7f,1.3f),1,new Color(.43f,.35f,.26f,.85f),R(.04f,.07f),.5f);
        }
        public void BearClaw(Vector2 point,Vector2 aim,float reach)
        {
            ClawEvents++; float heading=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
            for(int i=0;i<21;i++)
            {
                Vector2 direction=DemoArt.Direction(heading+R(-45,45));
                Emit(point+direction*R(reach*.35f,reach*.8f),R(.05f,.25f),direction*R(.8f,2),R(.3f,1),
                    i%3==0?1:4,i%3==0?new Color(.5f,.4f,.25f,.85f):new Color(1,.76f,.32f,.85f),R(.055f,.12f),R(.28f,.5f));
            }
            DustBurst(point+aim*reach*.6f,9);
        }
        public void BearImpact(Vector2 point,Vector2 aim)
        {
            ImpactEvents++; DustBurst(point,18,1.6f);
            for(int i=0;i<14;i++) Emit(point,.1f,DemoArt.Direction(R(0,360))*R(.5f,2.2f)+aim*.4f,
                R(1,2),1,new Color(.48f,.39f,.25f,.9f),R(.04f,.095f),R(.4f,.7f));
        }
        public void Gaze(Vector2 point,Vector2 aim,float reach)
        {
            GazeEvents++; float heading=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
            for(int i=0;i<48;i++)
            {
                Vector2 direction=DemoArt.Direction(heading+R(-46,46));
                Emit(point+direction*R(.3f,reach*.75f),R(.06f,.4f),direction*R(.3f,1),R(.15f,.65f),i%4==0?4:2,
                    i%4==0?new Color(1,.93f,.53f,.95f):new Color(.38f,.94f,.52f,.85f),R(.055f,.12f),R(.35f,.65f));
            }
            for(int i=0;i<12;i++)
            {
                Vector2 direction=DemoArt.Direction(heading+Mathf.Lerp(-50,50,i/11f));
                Emit(point+direction*(reach*.92f),R(.06f,.18f),direction*R(.12f,.32f),R(.15f,.4f),4,
                    new Color(.92f,1,.65f,.9f),R(.065f,.11f),R(.25f,.42f));
            }
            for(int i=0;i<7;i++) Emit(point+aim*R(.5f,reach*.7f),.1f,aim*.3f,.2f,3,new Color(.24f,.6f,.22f,.22f),.24f,.55f);
        }
        public void ApePalm(Vector2 point, Vector2 aim, float reach)
        {
            ApePalmEvents++; float heading = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 24; i++)
            {
                Vector2 direction = DemoArt.Direction(heading + R(-52, 52));
                Emit(point + direction * R(reach * .35f, reach * .9f), R(.08f, .3f), direction * R(.5f, 1.3f),
                    R(.2f, .6f), 4, new Color(.9f, .86f, .56f, .8f), R(.055f, .11f), R(.2f, .4f));
            }
        }
        public void ApeSlam(Vector2 point, float radius)
        {
            ApeSlamEvents++; DustBurst(point, 24, 1.8f);
            for (int i = 0; i < 40; i++)
            {
                Vector2 direction = DemoArt.Direction(i * 9);
                Emit(point + direction * radius * .8f, .08f, direction * R(.4f, 1), R(.3f, 1), i % 3 == 0 ? 1 : 4,
                    i % 3 == 0 ? new Color(.53f, .42f, .26f, .9f) : new Color(.93f, .84f, .43f, .9f),
                    R(.06f, .13f), R(.3f, .55f));
            }
        }
        public void VenomSpray(Vector3 mouth,Vector2 aim)
        {
            SprayEvents++;
            for(int i=0;i<14;i++) WorldDrop(mouth,aim+DemoArt.Direction(R(0,360))*.55f,R(.5f,1.8f),.035f,.4f);
        }
        void WorldDrop(Vector3 world,Vector2 velocity,float rise,float size,float life)
        {
            float height=world.z/arena.View.Up.z;
            Vector2 ground=world-arena.View.Up*height;
            Emit(ground,Mathf.Max(.02f,height),velocity,rise,2,new Color(.35f,.72f,.11f,.8f),size,life);
        }
        public void VenomTrail(Vector3 world,Vector2 aim)
        { for(int i=0;i<2;i++) WorldDrop(world,-aim*.3f+DemoArt.Direction(R(0,360))*.2f,.1f,.025f,.35f); }
        public void VenomSplash(Vector2 point,float radius)
        {
            SplashEvents++;
            for(int i=0;i<30;i++) Emit(point+DemoArt.Direction(R(0,360))*R(0,radius*.6f),.04f,
                DemoArt.Direction(R(0,360))*R(.3f,1.4f),R(.7f,1.8f),2,new Color(.38f,.78f,.10f,.85f),R(.035f,.075f),R(.35f,.65f));
        }
        public void PoolMist(Vector2 point,float radius)
        {
            MistEvents++;
            for(int i=0;i<2;i++)
            {
                Vector2 p=point+DemoArt.Direction(R(0,360))*R(.1f,radius*.8f);
                Emit(p,.06f,Vector2.right*R(-.08f,.08f),R(.18f,.35f),3,new Color(.3f,.59f,.13f,.23f),R(.12f,.22f),R(.65f,1.05f));
                Emit(p,.05f,Vector2.zero,.24f,5,new Color(.63f,.86f,.23f,.48f),R(.025f,.055f),.65f);
            }
        }
        public void Advance(float dt)
        {
            if(arena==null || arena.Paused || dt<=0) return;
            Clock+=dt;
            for(int i=active.Count-1;i>=0;i--)
            {
                var p=active[i]; p.age+=dt;
                if(p.age>=p.life) { active.RemoveAt(i); continue; }
                p.ground+=p.velocity*dt; p.height=Mathf.Max(.015f,p.height+p.rise*dt); p.rise-=p.gravity*dt;
                p.angle+=p.spin*dt; active[i]=p;
            }
            Refresh();
        }
        public void Refresh()
        {
            dirty=false;
            if(active.Count==0) { meshRenderer.enabled=false; return; }
            vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear();
            foreach(var p in active)
            {
                float t=p.age/p.life;
                float size=p.size*(p.kind==0||p.kind==3?1+t*1.6f:1-t*.25f);
                Color color=p.color; color.a*=Mathf.Pow(1-t,1.4f);
                float angle=p.angle*Mathf.Deg2Rad,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
                Vector3 center=arena.View.LiftPoint(p.ground,p.height); int start=vertices.Count;
                for(int corner=0;corner<4;corner++)
                {
                    float x=corner==0||corner==3?-1:1, y=corner<2?-1:1;
                    float dx=x*size,dy=y*size*(p.kind==4?.4f:1);
                    vertices.Add(center+Vector3.right*(dx*c-dy*s)+arena.View.Up*(dx*s+dy*c));
                    uvs.Add(new Vector3((x+1)*.5f,(y+1)*.5f,p.kind)); colors.Add(color);
                }
                triangles.Add(start); triangles.Add(start+2); triangles.Add(start+1);
                triangles.Add(start); triangles.Add(start+3); triangles.Add(start+2);
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0,uvs); mesh.SetTriangles(triangles,0,false); mesh.RecalculateBounds();
            meshRenderer.enabled=active.Count>0;
        }
        void Update() { Advance(Time.deltaTime); }
        void LateUpdate() { if(dirty) Refresh(); }
        public void Clear() { active.Clear(); dirty=false; if(meshRenderer!=null) meshRenderer.enabled=false; }
        void OnDisable() { Clear(); }
        void OnDestroy() { if(root!=null) Destroy(root.gameObject); if(mesh!=null) Destroy(mesh); if(material!=null) Destroy(material); }
    }
}
