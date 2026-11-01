using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    // One map owns all reservations, projectiles and pools, plus the shared DOT clock.
    public sealed class SerpentVenomField : MonoBehaviour
    {
        public const float Radius=1.15f*1.5f*1.25f, PoolDuration=4, FlightDuration=.5f, TickInterval=.5f, GroupInterval=.9f;
        public const int TickDamage=3;
        public const int MaxZones=2;
        enum Phase { Charge, Flight, Pool }
        sealed class Zone
        {
            public SnakeCombat caster;
            public Vector2 target;
            public Vector3 launch;
            public Transform root, glob;
            public MeshRenderer renderer;
            public Material material;
            public LineRenderer warning;
            public Phase phase;
            public float age;
            public float moteTimer;
        }
        readonly List<Zone> zones=new List<Zone>();
        SlashArena arena;
        Mesh quad;
        BeastAttackParticles particles;
        float clock,nextReservation,nextDamage;
        public int ActiveCount => zones.Count;
        public int DamageEvents { get; private set; }
        public int Landings { get; private set; }
        public float Clock => clock;
        public int PoolCount { get { int count=0; foreach(var z in zones) if(z.phase==Phase.Pool) count++; return count; } }
        public Vector2 LastLanding { get; private set; }

        public static SerpentVenomField For(SlashArena arena, Transform creature)
        {
            var map=creature.GetComponentInParent<TravelMapEnvironment>();
            var field=map.GetComponent<SerpentVenomField>();
            if(field==null) { field=map.gameObject.AddComponent<SerpentVenomField>(); field.Initialize(arena); }
            return field;
        }
        void Initialize(SlashArena owner)
        {
            arena=owner;
            particles=BeastAttackParticles.For(owner,transform);
            quad=new Mesh { name="Venom billboard and pool",hideFlags=HideFlags.DontSave };
            quad.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
            quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up}; quad.triangles=new[]{0,2,1,0,3,2};
        }
        public bool TryReserve(SnakeCombat caster,Vector2 target)
        {
            if(arena.Paused || caster==null || !caster.isActiveAndEnabled || zones.Count>=MaxZones || clock<nextReservation) return false;
            foreach(var z in zones) if(z.caster==caster && z.phase==Phase.Charge) return false;
            var root=new GameObject("Venom locked landing zone").transform; root.SetParent(transform,false);
            root.position=new Vector3(target.x,target.y,-.035f);
            var warning=DemoArt.Ring("Venom landing warning",root,Radius,DemoArt.AttackWarningColor,.035f*DemoArt.AttackWarningWidthScale,-64);
            zones.Add(new Zone { caster=caster,target=target,root=root,warning=warning });
            nextReservation=clock+GroupInterval;
            return true;
        }
        public void Charge(SnakeCombat caster,float progress)
        {
            foreach(var z in zones) if(z.caster==caster && z.phase==Phase.Charge)
            {
                z.warning.startColor=z.warning.endColor=new Color(1,.06f,.08f,Mathf.Lerp(.4f,.9f,progress));
                return;
            }
        }
        public bool Launch(SnakeCombat caster,Vector3 mouth)
        {
            foreach(var z in zones) if(z.caster==caster && z.phase==Phase.Charge)
            {
                z.phase=Phase.Flight; z.age=0; z.launch=mouth;
                z.glob=new GameObject("Flying venom glob").transform; z.glob.SetParent(z.root,false);
                z.glob.gameObject.AddComponent<MeshFilter>().sharedMesh=quad;
                z.renderer=z.glob.gameObject.AddComponent<MeshRenderer>();
                z.material=new Material(Resources.Load<Shader>("SlashDemo/SerpentVenom")) { hideFlags=HideFlags.DontSave };
                z.material.SetFloat("_Mode",1); z.renderer.sharedMaterial=z.material;
                z.renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                z.glob.position=mouth; z.glob.rotation=arena.View.Rotation; z.glob.localScale=Vector3.one*.20f;
                z.renderer.sortingOrder=ObliqueProjection.DepthOrder(z.target.y)+60;
                particles.VenomSpray(mouth,(z.target-(Vector2)caster.transform.position).normalized);
                return true;
            }
            return false;
        }
        public void CancelCharge(SnakeCombat caster)
        {
            for(int i=zones.Count-1;i>=0;i--) if(zones[i].caster==caster && zones[i].phase==Phase.Charge) Remove(i);
        }
        public void Advance(float dt)
        {
            if(arena==null || arena.Paused || dt<=0) return;
            clock+=dt;
            bool inside=false;
            for(int i=zones.Count-1;i>=0;i--)
            {
                var z=zones[i];
                if(z.phase==Phase.Charge)
                {
                    if(z.caster==null || !z.caster.isActiveAndEnabled || !z.caster.GetComponent<SlashEnemy>().Alive) Remove(i);
                    continue;
                }
                z.age+=dt;
                if(z.phase==Phase.Flight)
                {
                    float t=Mathf.Clamp01(z.age/FlightDuration);
                    z.glob.position=Vector3.Lerp(z.launch,arena.View.LiftPoint(z.target,.035f),t)+arena.View.Up*(Mathf.Sin(t*Mathf.PI)*1.1f);
                    z.moteTimer-=dt;
                    if(z.moteTimer<=0) { particles.VenomTrail(z.glob.position,(z.target-(Vector2)z.launch).normalized); z.moteTimer=.045f; }
                    if(t<1) continue;
                    z.phase=Phase.Pool; z.age=0; z.warning.gameObject.SetActive(false);
                    z.glob.position=new Vector3(z.target.x,z.target.y,-.035f); z.glob.rotation=Quaternion.identity;
                    z.glob.localScale=Vector3.one*Radius;
                    z.material.SetFloat("_Mode",0); z.renderer.sortingOrder=-65;
                    Landings++; LastLanding=z.target;
                    arena.Effects.Burst(z.target,new Color(.45f,.7f,.12f),8,.55f);
                    particles.VenomSplash(z.target,Radius); z.moteTimer=0;
                }
                if(z.age>=PoolDuration) { Remove(i); continue; }
                z.moteTimer-=dt;
                if(z.moteTimer<=0) { particles.PoolMist(z.target,Radius); z.moteTimer=.2f; }
                z.material.SetFloat("_Age",z.age);
                z.material.SetFloat("_Fade",1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(PoolDuration-.6f,PoolDuration,z.age)));
                if(arena.Player!=null && arena.Player.IsAlive)
                {
                    float playerRadius=arena.Player.GetComponent<CircleCollider2D>().radius;
                    if(Vector2.Distance(arena.Player.transform.position,z.target)<=Radius+playerRadius)
                    { inside=true; }
                }
            }
            if(inside && clock>=nextDamage && arena.Player.TakePoisonDamage(TickDamage))
            { DamageEvents++; nextDamage=clock+TickInterval; }
        }
        void Update() { Advance(Time.deltaTime); }
        void Remove(int index)
        {
            var z=zones[index]; z.root.gameObject.SetActive(false); Destroy(z.root.gameObject);
            if(z.material!=null) Destroy(z.material); zones.RemoveAt(index);
        }
        public void Clear() { for(int i=zones.Count-1;i>=0;i--) Remove(i); }
        void OnDisable() { Clear(); }
        void OnDestroy() { Clear(); if(quad!=null) Destroy(quad); }
    }
}
