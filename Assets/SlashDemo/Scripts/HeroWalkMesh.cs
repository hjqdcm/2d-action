using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    public static class HeroGaitMath
    {
        public const float CycleDistance = 1.6f / 0.9f, StrideDistance = 0.44f, StanceFraction = 0.60f;
        const float LiftHeight = 0.110f;
        public struct Footfall { public float travel, lift, roll; }
        public struct Leg
        {
            public Vector2 hip, knee, ankle, sole;
            public Leg(Vector2 hip, Vector2 knee, Vector2 ankle, Vector2 sole)
            { this.hip = hip; this.knee = knee; this.ankle = ankle; this.sole = sole; }
        }
        public struct Rig
        {
            public Leg left, right; public bool reversePhase;
            public Rig(Leg left, Leg right, bool reversePhase)
            { this.left = left; this.right = right; this.reversePhase = reversePhase; }
        }
        public struct LegPose { public Leg rest; public Vector2 knee, ankle, sole; public float roll, depth; }
        public struct Frame { public LegPose left, right; public float phase, blend; public Vector2 motion; }
        public struct Binding { public float left, right; }
        static readonly Rig[] Rigs = {
            new Rig(L(-.045f,.44f,-.060f,.26f,-.070f,.10f,-.085f,0), L(.045f,.44f,.052f,.26f,.075f,.10f,.090f,0), false),
            new Rig(L(-.025f,.44f,-.024f,.27f,-.030f,.10f,-.032f,.01f), L(.060f,.45f,.090f,.30f,.100f,.14f,.121f,.045f), false),
            new Rig(L(-.015f,.45f,.018f,.25f,.015f,.10f,.019f,0), L(.040f,.48f,.060f,.33f,.060f,.18f,.072f,.085f), false),
            new Rig(L(-.055f,.44f,-.080f,.23f,-.099f,.115f,-.104f,.045f), L(.040f,.43f,.055f,.20f,.070f,.07f,.075f,0), true),
            new Rig(L(-.045f,.44f,-.065f,.25f,-.080f,.09f,-.085f,0), L(.045f,.44f,.065f,.25f,.080f,.09f,.090f,0), true),
            new Rig(L(-.040f,.43f,-.070f,.22f,-.085f,.09f,-.090f,0), L(.060f,.44f,.080f,.26f,.095f,.13f,.100f,.05f), true),
            new Rig(L(-.020f,.46f,-.055f,.31f,-.080f,.17f,-.090f,.09f), L(.030f,.44f,-.005f,.23f,-.040f,.09f,-.050f,0), true),
            new Rig(L(-.015f,.46f,-.055f,.31f,-.070f,.16f,-.085f,.075f), L(.075f,.44f,.085f,.23f,.100f,.09f,.090f,0), false)
        };
        static Leg L(float hx,float hy,float kx,float ky,float ax,float ay,float fx,float fy)
            => new Leg(new Vector2(hx,hy),new Vector2(kx,ky),new Vector2(ax,ay),new Vector2(fx,fy));
        public static Rig ForDirection(int direction) => Rigs[Mathf.Clamp(direction,0,7)];
        public static Footfall Sample(float phase, float strideScale = 1f)
        {
            float t = Mathf.Repeat(phase / (Mathf.PI * 2),1);
            float span = StrideDistance * Mathf.Max(.05f, strideScale) * StanceFraction / HeroAppearance.BodyHeight;
            if (t < StanceFraction)
            {
                float u = t / StanceFraction;
                return new Footfall { travel = span * (.5f-u),
                    roll = 7 * (1-Mathf.SmoothStep(0,1,u/.18f)) - 12 * Mathf.SmoothStep(0,1,(u-.75f)/.25f) };
            }
            float swing = (t-StanceFraction)/(1-StanceFraction), arch = swing*(1-swing);
            return new Footfall { travel = Mathf.Lerp(-span*.5f,span*.5f,Mathf.SmoothStep(0,1,swing)),
                lift = LiftHeight*16*arch*arch, roll = Mathf.Lerp(-12,7,swing) };
        }
        public static float BodyBob(float phase) => (1-Mathf.Cos(phase*2))*.012f;
        public static float AdvancePhase(float phase,float travel,float scale,bool dashing,float dt,float cadenceScale=1f)
        {
            if(dt<=0 || dashing || travel<=.00001f || travel>1.5f) return phase;
            return Mathf.Repeat(phase+travel/(CycleDistance*Mathf.Max(.1f,scale))*Mathf.Max(.05f,cadenceScale)*Mathf.PI*2,Mathf.PI*2);
        }
        public static Vector2 SolveKnee(Vector2 hip,Vector2 ankle,float upper,float lower)
        {
            Vector2 delta = ankle-hip;
            float distance = Mathf.Clamp(delta.magnitude,.0001f,upper+lower-.0001f);
            Vector2 axis = delta.sqrMagnitude>.000001f ? delta.normalized : Vector2.down;
            float along = (upper*upper-lower*lower+distance*distance)/(2*distance);
            float bend = Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            return hip+axis*along+new Vector2(-axis.y,axis.x)*bend;
        }
        static LegPose PoseLeg(Leg leg,Footfall step,Vector2 motion,float bob)
        {
            float height = leg.hip.y-leg.ankle.y;
            float upper = Mathf.Max(.12f,leg.hip.y-leg.knee.y)*1.65f;
            float lower = Mathf.Max(.12f,leg.knee.y-leg.ankle.y)*1.65f;
            var hip = new Vector2(0,height);
            Vector2 baseKnee = SolveKnee(hip,Vector2.zero,upper,lower);
            Vector2 knee = SolveKnee(hip,new Vector2(step.travel,step.lift-bob),upper,lower);
            Vector2 offset = motion*step.travel+Vector2.up*(step.lift-bob);
            var pose = new LegPose { rest=leg, sole=leg.sole+offset, roll=step.roll*motion.x,
                depth=leg.sole.y+motion.y*step.travel };
            pose.ankle = pose.sole+Rotate(leg.ankle-leg.sole,pose.roll);
            pose.knee = leg.knee+motion*(knee.x-baseKnee.x)+Vector2.up*(knee.y-baseKnee.y);
            return pose;
        }
        public static Frame Prepare(Rig rig,float phase,float blend,Vector2 motion,float strideScale=1f)
        {
            float leftPhase = phase+(rig.reversePhase?Mathf.PI:0), bob = BodyBob(phase);
            return new Frame { phase=phase, blend=Mathf.Clamp01(blend), motion=motion,
                left=PoseLeg(rig.left,Sample(leftPhase,strideScale),motion,bob),
                right=PoseLeg(rig.right,Sample(leftPhase+Mathf.PI,strideScale),motion,bob) };
        }
        static Vector2 Rotate(Vector2 vector,float degrees)
        {
            float angle=degrees*Mathf.Deg2Rad,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            return new Vector2(vector.x*c-vector.y*s,vector.x*s+vector.y*c);
        }
        static Vector2 BonePoint(Vector2 point,Vector2 from,Vector2 to,Vector2 posedFrom,Vector2 posedTo)
        {
            Vector2 bone=to-from,posed=posedTo-posedFrom;
            float length=Mathf.Max(.001f,bone.magnitude);
            Vector2 axis=bone/length,normal=new Vector2(-axis.y,axis.x);
            Vector2 posedAxis=posed.normalized,posedNormal=new Vector2(-posedAxis.y,posedAxis.x);
            Vector2 offset=point-from;
            return posedFrom+posed*(Vector2.Dot(offset,axis)/length)+posedNormal*Vector2.Dot(offset,normal);
        }
        static Vector2 SkinLeg(Vector2 point,LegPose pose)
        {
            Leg leg=pose.rest;
            Vector2 foot=pose.sole+Rotate(point-leg.sole,pose.roll);
            Vector2 shin=BonePoint(point,leg.ankle,leg.knee,pose.ankle,pose.knee);
            Vector2 thigh=BonePoint(point,leg.knee,leg.hip,pose.knee,leg.hip);
            float bootBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(leg.ankle.y+.10f,leg.ankle.y+.14f,point.y));
            float kneeBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(leg.knee.y-.025f,leg.knee.y+.025f,point.y));
            return Vector2.Lerp(Vector2.Lerp(foot,shin,bootBlend),thigh,kneeBlend);
        }
        static float CenterX(Leg leg,float y)
        {
            if(y<leg.ankle.y) return Mathf.Lerp(leg.sole.x,leg.ankle.x,Mathf.InverseLerp(leg.sole.y,leg.ankle.y,y));
            if(y<leg.knee.y) return Mathf.Lerp(leg.ankle.x,leg.knee.x,Mathf.InverseLerp(leg.ankle.y,leg.knee.y,y));
            return Mathf.Lerp(leg.knee.x,leg.hip.x,Mathf.InverseLerp(leg.knee.y,leg.hip.y,y));
        }
        static float LegMask(Vector2 point,Leg leg)
        {
            float width=Mathf.Abs(point.x-CenterX(leg,point.y));
            float top=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(leg.hip.y-.07f,leg.hip.y,point.y));
            float bottom=Mathf.SmoothStep(0,1,Mathf.InverseLerp(leg.sole.y-.045f,leg.sole.y-.01f,point.y));
            float boot=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(leg.ankle.y+.02f,leg.ankle.y+.10f,point.y));
            float inner=Mathf.Lerp(.055f,.085f,boot),outer=Mathf.Lerp(.085f,.12f,boot);
            return (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(inner,outer,width)))*top*bottom;
        }
        public static Binding Bind(Vector2 point,Rig rig)
        {
            float leftDistance=Mathf.Abs(point.x-CenterX(rig.left,point.y));
            float rightDistance=Mathf.Abs(point.x-CenterX(rig.right,point.y));
            if(point.y<rig.left.sole.y-.005f) leftDistance+=1;
            if(point.y<rig.right.sole.y-.005f) rightDistance+=1;
            float right=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.012f,.012f,leftDistance-rightDistance));
            return new Binding { left=(1-right)*LegMask(point,rig.left),right=right*LegMask(point,rig.right) };
        }
        public static float SoleMask(Vector2 point,Binding binding,Rig rig)
        {
            Leg leg=binding.left>=binding.right?rig.left:rig.right;
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(leg.sole.y+.045f,leg.sole.y+.075f,point.y));
        }
        static Vector2 PantsCorner(Vector2 hip,Vector2 knee,Vector2 ankle,int vertex)
        {
            bool shin=vertex>=4; int corner=vertex%4;
            Vector2 from=shin?knee:hip,to=shin?ankle:knee;
            Vector2 axis=(to-from).normalized,normal=new Vector2(-axis.y,axis.x);
            float width=shin?(corner<2?.044f:.031f):(corner<2?.053f:.044f);
            return (corner<2?from-axis*.012f:to+axis*.012f)+normal*(corner%2==0?-width:width);
        }
        public static Vector2 PantsPoint(LegPose pose,int vertex,float blend)
        {
            Vector2 rest=PantsCorner(pose.rest.hip,pose.rest.knee,pose.rest.ankle+Vector2.up*.015f,vertex);
            Vector2 moved=PantsCorner(pose.rest.hip,pose.knee,pose.ankle+Rotate(Vector2.up*.015f,pose.roll),vertex);
            return Vector2.Lerp(rest,moved,blend);
        }
        public static Vector2 PantsUV(int vertex)
        {
            int corner=vertex%4;
            return new Vector2((corner%2==0?220f:236f)/2048,1-(corner<2?350f:455f)/1280);
        }
        public static Vector3 Deform(Vector3 rest,Vector2 uv,Binding binding,Frame frame,int part=0)
        {
            if(frame.blend<=0 || uv.y>=.59f) return rest;
            Vector2 point=rest;
            if(part==1) return rest+(Vector3)((SkinLeg(point,frame.left)-point)*frame.blend);
            if(part==2) return rest+(Vector3)((SkinLeg(point,frame.right)-point)*frame.blend);
            float cloth=(1-Mathf.Clamp01(binding.left+binding.right))*Mathf.Clamp01((.55f-uv.y)/.4f)
                *Mathf.Clamp01((Mathf.Abs(uv.x-.5f)-.15f)*5);
            Vector2 delta=frame.motion*(Mathf.Sin(frame.phase*2-uv.y*5-.55f)*.018f*cloth);
            return rest+(Vector3)(delta*frame.blend);
        }
    }

    public static class HeroLayerMath
    {
        public static Vector3 KineticDeform(Vector3 position, Vector2 uv, Vector2 waist, Vector2 hipOffset,
            float torsoShear, float shoulderShear, float torsoTurn, float shoulderTurn, float follow, bool headLock = false,
            float bodyHeight = 1)
        {
            Vector3 rest = position;
            float support = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.08f, 0.50f, uv.y));
            float shoulder = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.66f, 0.80f, uv.y));
            float upper = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.24f, 0.58f, uv.y));
            float shear = Mathf.Lerp(torsoShear, shoulderShear, shoulder);
            // A shared waist transform parents all painted regions, with planted feet as its support.
            position.x += shear * (position.y - waist.y) * support;
            float angle = -Mathf.Lerp(torsoTurn, shoulderTurn, shoulder) * upper * Mathf.Deg2Rad;
            float sine = Mathf.Sin(angle), cosine = Mathf.Cos(angle);
            Vector2 delta = (Vector2)position - waist;
            position.x = waist.x + delta.x * cosine - delta.y * sine + hipOffset.x * support;
            position.y = waist.y + delta.x * sine + delta.y * cosine + hipOffset.y * support;
            float edge = Mathf.Clamp01((Mathf.Abs(uv.x - 0.5f) - 0.13f) * 4);
            float hair = Mathf.Sin(Mathf.Clamp01((uv.y - 0.58f) / 0.42f) * Mathf.PI) * edge;
            float hem = Mathf.Clamp01((0.56f - uv.y) / 0.56f) * edge;
            float secondary = follow * (hair * 0.7f + hem) * support;
            // Secondary drag is applied in the inherited torso frame, after its main deformation.
            position.x += secondary * cosine;
            position.y += secondary * sine;
            if (headLock && uv.y > 0.66f)
            {
                // Follow one neck anchor as a rigid piece; varying counter-rotation across the face
                // stretches its features. Only the neck blends into the moving torso.
                const float neckUV = 0.74f;
                float neckShoulder = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.66f, 0.80f, neckUV));
                float neckAngle = -Mathf.Lerp(torsoTurn, shoulderTurn, neckShoulder) * Mathf.Deg2Rad;
                float neckShear = Mathf.Lerp(torsoShear, shoulderShear, neckShoulder);
                float height = bodyHeight * (neckUV - 0.52f);
                var neckDelta = new Vector2(neckShear * height, height);
                float sn = Mathf.Sin(neckAngle), cs = Mathf.Cos(neckAngle);
                var translation = new Vector3(neckDelta.x * cs - neckDelta.y * sn + hipOffset.x,
                    neckDelta.x * sn + neckDelta.y * cs - height + hipOffset.y, 0);
                float neckBlend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.66f, 0.78f, uv.y));
                position = Vector3.Lerp(position, rest + translation, neckBlend);
            }
            return position;
        }

        public static Vector3 Deform(Vector3 position, Vector2 uv, Vector2 waist,
            float turn, float follow, float compression, float clock, float shear = 0, float squash = 0)
        {
            float upper = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.48f, 0.75f, uv.y));
            float head = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.76f, 0.94f, uv.y));
            float angle = (turn * upper - turn * 0.45f * head) * Mathf.Deg2Rad;
            Vector2 delta = (Vector2)position - waist;
            float sine = Mathf.Sin(angle), cosine = Mathf.Cos(angle);
            position.x = waist.x + delta.x * cosine - delta.y * sine;
            position.y = waist.y + delta.x * sine + delta.y * cosine - compression * upper;
            float edge = Mathf.Clamp01((Mathf.Abs(uv.x - 0.5f) - 0.13f) * 4);
            float hair = Mathf.Sin(Mathf.Clamp01((uv.y - 0.58f) / 0.42f) * Mathf.PI) * edge;
            float hem = Mathf.Clamp01((0.56f - uv.y) / 0.56f) * edge;
            position.x += follow * (hair * 0.7f + hem) + Mathf.Sin(clock * 3) * 0.0025f * upper;
            // Feet stay planted while the upper silhouette loads into the cut and relaxes on recovery.
            position.x += Mathf.Tan(shear * Mathf.Deg2Rad) * Mathf.Max(0, position.y)
                * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.1f, 0.7f, uv.y));
            position.y *= 1 - Mathf.Clamp(squash, 0, 0.05f);
            return position;
        }
    }

    // Procedural walking with a shorter stride; cadence is controlled independently.
    [DefaultExecutionOrder(70)]
    [DisallowMultipleComponent]
    public sealed class HeroWalkMesh : MonoBehaviour
    {
        const int Columns=40, Rows=56;
        SpriteRenderer source;
        Sprite sprite;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Material material;
        MaterialPropertyBlock properties;
        Vector3[] rest,vertices;
        Vector2[] weights;
        HeroGaitMath.Binding[] legBindings;
        HeroGaitMath.Rig legRig;
        int gridVertexCount;
        int[] leftFootInFront,rightFootInFront;
        bool leftInFront;
        Vector2 motion=Vector2.right;
        float characterScale=1;
        bool hasWalkHeading;
        float phase,blend,smoothedSpeed;
        // 普攻时：步幅 = 常态 2/3，步频 = 常态 3/4。
        // 相位按实际位移推进，而普攻移速是 SlashPlayer.AttackMoveMultiplier(0.5)，
        // 所以相位倍率 = (3/4) / 0.5 = 1.5。改普攻移速时必须同步改这里。
        const float AttackStrideScale = 2f/3f, AttackCadenceScale = 1.5f;
        public bool Attacking { get; set; }
        float strideScale=1, cadenceScale=1;
        float torsoTurn,clothFollow,compression,motionClock;
        float combatShear,combatSquash;
        bool kineticMotion;
        SwordMotion.Pose kineticPose;
        Vector2 kineticHip;
        float kineticDirection;
        bool layeredMotion;
        public float Phase=>phase;
        public Mesh DeformedMesh=>mesh;
        public MeshRenderer WalkingRenderer=>meshRenderer;
        public int LayerCount=>mesh!=null?mesh.subMeshCount:0;
        public float Bob=>HeroGaitMath.BodyBob(phase)*HeroAppearance.BodyHeight*characterScale*blend;
        public void Initialize(SpriteRenderer body)
        {
            source=body;
            mesh=new Mesh { name="Hero walking legs and robe",hideFlags=HideFlags.DontSave };
            mesh.MarkDynamic();
            var renderObject=new GameObject("Walking mesh");
            renderObject.transform.SetParent(transform,false);
            renderObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            meshRenderer=renderObject.AddComponent<MeshRenderer>();
            var shader=Resources.Load<Shader>("SlashDemo/HeroCloth");
            material=new Material(shader!=null?shader:Shader.Find("Sprites/Default")) { hideFlags=HideFlags.DontSave };
            meshRenderer.sharedMaterials=new[]{material,material,material};
            properties=new MaterialPropertyBlock();source.forceRenderingOff=true;Refresh();
        }
        public void Advance(Vector2 distance,float dt,bool dashing,ObliqueView view)
        {
            if(dt<=0) return;
            motionClock+=dt;characterScale=Mathf.Max(.1f,view.characterScale);
            float travel=distance.magnitude;
            if(travel>1.5f) travel=0;
            float speed=dashing?0:travel/dt;
            smoothedSpeed=Mathf.Lerp(smoothedSpeed,speed,1-Mathf.Exp(-12*dt));
            blend=Mathf.Lerp(blend,Mathf.Clamp01(smoothedSpeed/1.5f),1-Mathf.Exp(-18*dt));
            if(smoothedSpeed<.02f && blend<.002f) blend=0;
            if(blend==0) hasWalkHeading=false;
            // 步幅与步频在普攻进出时平滑过渡，避免收招瞬间双脚被拉开。
            float ease=1-Mathf.Exp(-14*dt);
            strideScale=Mathf.Lerp(strideScale,Attacking?AttackStrideScale:1f,ease);
            cadenceScale=Mathf.Lerp(cadenceScale,Attacking?AttackCadenceScale:1f,ease);
            phase=HeroGaitMath.AdvancePhase(phase,travel,characterScale,dashing,dt,cadenceScale);
            if(!dashing && travel>.00001f)
            {
                Vector2 projected=ObliqueProjection.ProjectGround(distance/distance.magnitude,view.elevation);
                motion=hasWalkHeading?Vector2.Lerp(motion,projected,1-Mathf.Exp(-22*dt)):projected;
                hasWalkHeading=true;
            }
        }

        public void SetCombatPose(float lean, float extension, bool dashing, float dt)
        {
            if (dt <= 0) return;
            layeredMotion = true;
            kineticMotion = false;
            torsoTurn = lean * 0.35f;
            clothFollow = Mathf.Lerp(clothFollow, -extension * 0.22f, 1 - Mathf.Exp(-16 * dt));
            compression = dashing ? 0.025f : Mathf.Abs(lean) * 0.0012f;
            combatShear = Mathf.Clamp(lean * 0.4f, -6, 6);
            combatSquash = Mathf.Clamp01(Mathf.Abs(lean) / 14) * 0.04f;
        }

        public void SetCombatPose(SwordMotion.Pose pose, Vector2 hipOffset, float direction, float dt)
        {
            if (dt <= 0) return;
            layeredMotion = kineticMotion = true;
            kineticPose = pose;
            kineticHip = hipOffset;
            kineticDirection = direction;
            float target = -(pose.torsoShear * 0.09f + pose.torsoTurn * 0.0015f) * direction;
            clothFollow = Mathf.Lerp(clothFollow, target, 1 - Mathf.Exp(-16 * dt));
        }

        Vector3 CombatPoint(Vector3 position, Vector2 uv, Vector2 waist)
        {
            return kineticMotion ? HeroLayerMath.KineticDeform(position, uv, waist, kineticHip,
                kineticPose.torsoShear * kineticDirection, kineticPose.shoulderShear * kineticDirection,
                kineticPose.torsoTurn * kineticDirection, kineticPose.shoulderTurn * kineticDirection, clothFollow,
                kineticPose.headLock, source.sprite.bounds.size.y)
                : HeroLayerMath.Deform(position, uv, waist, torsoTurn, clothFollow, compression, motionClock, combatShear, combatSquash);
        }

        public Vector3 PosePoint(Vector3 position)
        {
            if (!layeredMotion || source == null || source.sprite == null) return position;
            var bounds = source.sprite.bounds;
            Vector2 uv = new Vector2(Mathf.InverseLerp(bounds.min.x, bounds.max.x, position.x),
                Mathf.InverseLerp(bounds.min.y, bounds.max.y, position.y));
            return CombatPoint(position, uv, new Vector2(0, bounds.min.y + bounds.size.y * 0.52f));
        }

        void BuildGrid()
        {
            sprite=source.sprite;gridVertexCount=(Columns+1)*(Rows+1);
            int count=gridVertexCount*3+16;
            rest=new Vector3[count];vertices=new Vector3[count];weights=new Vector2[count];
            legBindings=new HeroGaitMath.Binding[count];
            int direction=sprite.name=="Hero back"?4:0;
            string[] names={"Hero S","Hero SE","Hero E","Hero NE","Hero N","Hero NW","Hero W","Hero SW"};
            for(int i=0;i<names.Length;i++) if(sprite.name==names[i]) direction=i;
            legRig=HeroGaitMath.ForDirection(direction);
            var uv=new Vector2[count];var colors=new Color[count];
            var rect=sprite.rect;var bounds=sprite.bounds;
            for(int y=0;y<=Rows;y++) for(int x=0;x<=Columns;x++)
            {
                int gridIndex=y*(Columns+1)+x;
                float u=x/(float)Columns,v=y/(float)Rows;
                for(int part=0;part<3;part++)
                {
                    int i=gridIndex+part*gridVertexCount;
                    rest[i]=new Vector3(bounds.min.x+bounds.size.x*u,bounds.min.y+bounds.size.y*v);
                    weights[i]=new Vector2(u,v);legBindings[i]=HeroGaitMath.Bind(rest[i],legRig);
                    uv[i]=new Vector2((rect.x+rect.width*u)/sprite.texture.width,(rect.y+rect.height*v)/sprite.texture.height);
                    colors[i]=new Color(legBindings[i].left,legBindings[i].right,part*.5f,HeroGaitMath.SoleMask(rest[i],legBindings[i],legRig));
                }
            }
            var standing=HeroGaitMath.Prepare(legRig,0,0,Vector2.zero);
            for(int leg=0;leg<2;leg++) for(int vertex=0;vertex<8;vertex++)
            {
                int i=gridVertexCount*3+leg*8+vertex;
                rest[i]=HeroGaitMath.PantsPoint(leg==0?standing.left:standing.right,vertex,0);
                weights[i]=new Vector2(Mathf.InverseLerp(bounds.min.x,bounds.max.x,rest[i].x),Mathf.InverseLerp(bounds.min.y,bounds.max.y,rest[i].y));
                uv[i]=HeroGaitMath.PantsUV(vertex);colors[i]=new Color(0,0,.25f,0);
            }
            var triangles=new[]{new List<int>(),new List<int>(),new List<int>()};
            var leftLeg=new List<int>();var rightLeg=new List<int>();
            for(int segment=0;segment<2;segment++)
            {
                int a=gridVertexCount*3+segment*4;
                AddLegQuad(leftLeg,a,a+2);AddLegQuad(rightLeg,a+8,a+10);
            }
            for(int y=0;y<Rows;y++) for(int x=0;x<Columns;x++)
            {
                int a=y*(Columns+1)+x,b=a+Columns+1;float v=(y+.5f)/Rows;
                var layer=triangles[v<.55f?0:v<.8f?1:2];
                AddLegQuad(layer,a,b);
                if(v<.50f)
                {
                    float left=Mathf.Max(Mathf.Max(legBindings[a].left,legBindings[b].left),Mathf.Max(legBindings[a+1].left,legBindings[b+1].left));
                    float right=Mathf.Max(Mathf.Max(legBindings[a].right,legBindings[b].right),Mathf.Max(legBindings[a+1].right,legBindings[b+1].right));
                    if(left>.05f) AddLegQuad(leftLeg,a+gridVertexCount,b+gridVertexCount);
                    if(right>.05f) AddLegQuad(rightLeg,a+gridVertexCount*2,b+gridVertexCount*2);
                }
            }
            var lower=new List<int>(triangles[0]);
            triangles[0].Clear();triangles[0].AddRange(rightLeg);triangles[0].AddRange(leftLeg);triangles[0].AddRange(lower);
            leftFootInFront=triangles[0].ToArray();
            triangles[0].Clear();triangles[0].AddRange(leftLeg);triangles[0].AddRange(rightLeg);triangles[0].AddRange(lower);
            rightFootInFront=triangles[0].ToArray();leftInFront=false;
            mesh.Clear();mesh.vertices=rest;mesh.uv=uv;mesh.uv2=weights;mesh.colors=colors;mesh.subMeshCount=3;
            for(int i=0;i<triangles.Length;i++) mesh.SetTriangles(triangles[i],i);
            material.mainTexture=sprite.texture;
        }
        static void AddLegQuad(List<int> triangles,int a,int b)
        {
            triangles.Add(a);triangles.Add(b);triangles.Add(a+1);
            triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
        }
        public void Refresh()
        {
            if(source==null || source.sprite==null) return;
            if(mesh==null || meshRenderer==null || material==null) { UseStaticBody();return; }
            if(properties==null) properties=new MaterialPropertyBlock();
            if(sprite!=source.sprite) BuildGrid();
            var frame=HeroGaitMath.Prepare(legRig,phase,blend,motion,strideScale);
            Vector2 waist=new Vector2(0,sprite.bounds.min.y+sprite.bounds.size.y*.52f);
            for(int i=0;i<vertices.Length;i++)
            {
                var p=rest[i];if(source.flipX) p.x=-p.x;
                if(i>=gridVertexCount*3)
                {
                    int pants=i-gridVertexCount*3;
                    vertices[i]=HeroGaitMath.PantsPoint(pants<8?frame.left:frame.right,pants%8,frame.blend);
                }
                else vertices[i]=HeroGaitMath.Deform(p,weights[i],legBindings[i],frame,i/gridVertexCount);
                if(layeredMotion) vertices[i]=CombatPoint(vertices[i],weights[i],waist);
            }
            mesh.vertices=vertices;
            bool nearerLeft=frame.left.depth<frame.right.depth;
            if(nearerLeft!=leftInFront)
            {
                leftInFront=nearerLeft;mesh.SetTriangles(leftInFront?leftFootInFront:rightFootInFront,0,false);
            }
            mesh.RecalculateBounds();meshRenderer.sortingLayerID=source.sortingLayerID;
            meshRenderer.sortingOrder=source.sortingOrder;meshRenderer.enabled=source.enabled;
            properties.SetColor("_Color",source.color);properties.SetFloat("_WalkBlend",blend);meshRenderer.SetPropertyBlock(properties);
        }
        void LateUpdate(){Refresh();}
        public void UseStaticBody()
        {
            if(source!=null) source.forceRenderingOff=false;
            if(meshRenderer!=null) meshRenderer.enabled=false;enabled=false;
        }
        void OnDestroy()
        {
            if(source!=null) source.forceRenderingOff=false;Release(mesh);Release(material);
        }
        static void Release(Object value)
        {
            if(value==null) return;
            if(Application.isPlaying) Destroy(value);else DestroyImmediate(value);
        }
    }
}
