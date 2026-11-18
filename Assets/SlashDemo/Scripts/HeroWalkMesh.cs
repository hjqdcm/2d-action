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
            public Leg left, right; public bool reversePhase; public int direction;
            internal bool keepFeetLevel, preserveBootShape;
            public Rig(Leg left, Leg right, bool reversePhase)
            {
                this.left = left; this.right = right; this.reversePhase = reversePhase;
                direction = 0; keepFeetLevel = preserveBootShape = false;
            }

            internal bool BootsOverlap => direction == 2 || direction == 6;
            internal float BootSeparation => right.sole.x - left.sole.x;
        }
        public struct LegPose { public Leg rest; public Vector2 knee, ankle, sole; public float roll, depth; }
        public struct Frame { public LegPose left, right; public float phase, blend, upperBlend; public Vector2 motion; public int direction; }
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
        // Narrow tail regions in the cropped v5 sprites: keep the painted face and hair crown out of the drag.
        static readonly Vector2[] HairRoots = {
            new Vector2(.50f,.94f), new Vector2(.49f,.94f), new Vector2(.52f,.94f), new Vector2(.52f,.94f),
            new Vector2(.50f,.94f), new Vector2(.46f,.94f), new Vector2(.50f,.94f), new Vector2(.61f,.94f)
        };
        static readonly Vector2[] HairTips = {
            new Vector2(.35f,.80f), new Vector2(.30f,.69f), new Vector2(.30f,.64f), new Vector2(.38f,.61f),
            new Vector2(.50f,.61f), new Vector2(.57f,.61f), new Vector2(.72f,.65f), new Vector2(.79f,.68f)
        };
        public static Rig ForDirection(int direction)
        {
            int index = Mathf.Clamp(direction, 0, 7);
            var rig = Rigs[index]; rig.direction = index; return rig;
        }
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
        static LegPose PoseLeg(Leg leg,Footfall step,Vector2 motion,float bob,float rollDirection)
        {
            float height = leg.hip.y-leg.ankle.y;
            float upper = Mathf.Max(.12f,leg.hip.y-leg.knee.y)*1.65f;
            float lower = Mathf.Max(.12f,leg.knee.y-leg.ankle.y)*1.65f;
            var hip = new Vector2(0,height);
            Vector2 baseKnee = SolveKnee(hip,Vector2.zero,upper,lower);
            Vector2 knee = SolveKnee(hip,new Vector2(step.travel,step.lift-bob),upper,lower);
            Vector2 offset = motion*step.travel+Vector2.up*(step.lift-bob);
            var pose = new LegPose { rest=leg, sole=leg.sole+offset, roll=step.roll*rollDirection,
                depth=leg.sole.y+motion.y*step.travel };
            pose.ankle = pose.sole+Rotate(leg.ankle-leg.sole,pose.roll);
            pose.knee = leg.knee+motion*(knee.x-baseKnee.x)+Vector2.up*(knee.y-baseKnee.y);
            return pose;
        }
        public static Frame Prepare(Rig rig,float phase,float blend,Vector2 motion,float strideScale=1f)
        {
            float leftPhase = phase+(rig.reversePhase?Mathf.PI:0), bob = BodyBob(phase);
            float rollDirection = rig.keepFeetLevel ? 0 : motion.x;
            return new Frame { phase=phase, blend=Mathf.Clamp01(blend), upperBlend=Mathf.Clamp01(blend), motion=motion, direction=rig.direction,
                left=PoseLeg(rig.left,Sample(leftPhase,strideScale),motion,bob,rollDirection),
                right=PoseLeg(rig.right,Sample(leftPhase+Mathf.PI,strideScale),motion,bob,rollDirection) };
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

        static float PaintedLegWeight(float height, float distance, Leg leg)
        {
            float towardHip = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(leg.ankle.y + .05f, leg.hip.y, height));
            float awayFromLeg = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .26f, distance));
            return (1 - towardHip) * (1 - awayFromLeg);
        }

        public static Binding Bind(Vector2 point,Rig rig)
        {
            if (rig.preserveBootShape)
            {
                float center = (rig.left.sole.x + rig.right.sole.x) * .5f;
                float transition = rig.BootSeparation * .36f;
                float rightBoot = rig.BootsOverlap ? (rig.left.sole.y > rig.right.sole.y ? 1 : 0)
                    : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(center - transition, center + transition, point.x));
                // Keep each boot rigid below the shin. Fade the leg motion into the hip.
                float leg = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.14f, .46f, point.y));
                return new Binding { left = (1 - rightBoot) * leg, right = rightBoot * leg };
            }
            float leftDistance=Mathf.Abs(point.x-CenterX(rig.left,point.y));
            float rightDistance=Mathf.Abs(point.x-CenterX(rig.right,point.y));
            if (rig.keepFeetLevel)
            {
                // Keep each shoe rigid and blend the two legs broadly where the painted fabric joins.
                float width = Mathf.Lerp(.085f, .28f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.095f, .34f, point.y)));
                float rightWeight = Mathf.InverseLerp(-width, width, leftDistance - rightDistance);
                return new Binding
                {
                    left = (1 - rightWeight) * PaintedLegWeight(point.y, leftDistance, rig.left),
                    right = rightWeight * PaintedLegWeight(point.y, rightDistance, rig.right)
                };
            }
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
        public static float PonytailWeight(Vector2 uv, int direction)
        {
            int index = Mathf.Clamp(direction, 0, 7);
            Vector2 root = HairRoots[index], tail = HairTips[index] - root;
            float along = Mathf.Clamp01(Vector2.Dot(uv - root, tail) / tail.sqrMagnitude);
            float radius = index == 4 ? .14f : .075f;
            float width = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(radius * .45f, radius,
                Vector2.Distance(uv, root + tail * along)));
            return width * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .9f, along));
        }
        public static Vector3 UpperBodyDeform(Vector3 rest, Vector2 uv, Frame frame)
        {
            if (frame.upperBlend <= 0 || uv.y <= .46f) return rest;
            float torso = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.59f, .72f, uv.y))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.70f, .82f, uv.y)));
            Vector2 waist = new Vector2(0, .52f);
            Vector2 delta = (Rotate((Vector2)rest - waist, -Mathf.Sin(frame.phase) * 1f)
                - ((Vector2)rest - waist)) * torso;
            // Cancel the root's vertical walking bob at the head, without rotating or stretching the face.
            delta.y -= BodyBob(frame.phase) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.56f, .82f, uv.y));
            float hair = PonytailWeight(uv, frame.direction);
            delta += new Vector2(Mathf.Sin(frame.phase * 2 - .8f) * .009f,
                Mathf.Sin(frame.phase * 2 - 1.1f) * .006f) * hair;
            return rest + (Vector3)(delta * frame.upperBlend);
        }
        public static Vector3 Deform(Vector3 rest,Vector2 uv,Binding binding,Frame frame,int part=0)
        {
            if(frame.blend<=0) return rest;
            Vector3 upper = UpperBodyDeform(rest, uv, frame);
            if(uv.y>=.59f) return upper;
            Vector2 point=rest;
            if(part==1) return rest+(Vector3)((SkinLeg(point,frame.left)-point)*frame.blend);
            if(part==2) return rest+(Vector3)((SkinLeg(point,frame.right)-point)*frame.blend);
            float cloth=(1-Mathf.Clamp01(binding.left+binding.right))*Mathf.Clamp01((.55f-uv.y)/.4f)
                *Mathf.Clamp01((Mathf.Abs(uv.x-.5f)-.15f)*5);
            Vector2 delta=frame.motion*(Mathf.Sin(frame.phase*2-uv.y*5-.55f)*.018f*cloth);
            return upper+(Vector3)(delta*frame.blend);
        }

        internal static Vector3 DeformPainted(Vector3 rest, Vector2 uv, Binding binding, Frame frame,
            Vector2 leftStep, Vector2 rightStep)
        {
            Vector3 cloth = Deform(rest, uv, binding, frame);
            Vector2 offset = leftStep * binding.left + rightStep * binding.right;
            return cloth + (Vector3)offset;
        }

        internal static void FitPaintedStride(Rig rig, Frame frame, ref Vector2 leftFoot, ref Vector2 rightFoot,
            out Vector2 leftKnee, out Vector2 rightKnee)
        {
            // Retain the hero's lift and knee bend. Cap the horizontal separation
            // independently so the painted gap cannot pinch as the feet pass.
            float bob = BodyBob(frame.phase);
            const float footLiftScale = .84f;
            leftFoot.y = (leftFoot.y + bob) * footLiftScale - bob;
            rightFoot.y = (rightFoot.y + bob) * footLiftScale - bob;
            leftKnee = (frame.left.knee - rig.left.knee) * .8f;
            rightKnee = (frame.right.knee - rig.right.knee) * .8f;
            if (rig.BootsOverlap) return;

            float sideways = rig.BootSeparation * .14f;
            LimitPaintedSeparation(ref leftFoot, ref rightFoot, sideways, Mathf.Min(.07f, rig.BootSeparation * .8f));
            LimitPaintedSeparation(ref leftKnee, ref rightKnee, sideways, .04f);
        }

        static void LimitPaintedSeparation(ref Vector2 left, ref Vector2 right, float sideways, float vertical)
        {
            Vector2 midpoint = (left + right) * .5f;
            Vector2 separation = (right - left) * .5f;
            separation.x = Mathf.Clamp(separation.x, -sideways, sideways);
            separation.y = Mathf.Clamp(separation.y, -vertical, vertical);
            left = midpoint - separation;
            right = midpoint + separation;
        }

        internal static Vector2 PaintedShinOffset(float height, Vector2 foot, Vector2 knee)
        {
            float bend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.14f, .36f, height));
            return Vector2.Lerp(foot, knee, bend);
        }
    }

    public static class HeroLayerMath
    {
        internal static float PlantedSupport(float height)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .50f, height));

        public static Vector3 KineticDeform(Vector3 position, Vector2 uv, Vector2 waist, Vector2 hipOffset,
            float torsoShear, float shoulderShear, float torsoTurn, float shoulderTurn, float follow, bool headLock = false,
            float bodyHeight = 1)
        {
            Vector3 rest = position;
            float support = PlantedSupport(uv.y);
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

    internal readonly struct PaintedSaberArm
    {
        const float BladeLengthScale = 1.12f;
        internal const float SecondGripSpacing = .043f;
        public readonly int[] Outline;
        public readonly int PatchEdge, PatchBottom;
        public readonly bool BehindBody;
        public readonly float Side;
        readonly Vector2 shoulder, elbow, grip, tip;
        public readonly SupportArm Support;
        public Vector2 Shoulder => shoulder;
        public Vector2 Grip => grip;
        public Vector2 ExtendedTip => grip + (tip - grip) * BladeLengthScale;
        // Follow the steel and guard where the red cloak falls behind the blade.
        static readonly Vector3[] SouthBladeEdges = {
            new Vector3(172, 191, 280), new Vector3(169, 195, 287), new Vector3(168, 195, 297),
            new Vector3(164, 185, 310), new Vector3(154, 176, 330), new Vector3(143, 164, 350),
            new Vector3(128, 149, 370), new Vector3(104, 119, 387)
        };
        static readonly Vector3[] SoutheastBladeEdges = {
            new Vector3(178, 204, 290), new Vector3(173, 207, 300), new Vector3(171, 204, 310),
            new Vector3(163, 190, 330), new Vector3(152, 179, 350), new Vector3(141, 165, 370),
            new Vector3(124, 150, 390), new Vector3(108, 126, 407)
        };
        static readonly Vector3[] SouthwestBladeEdges = {
            new Vector3(174, 201, 289), new Vector3(169, 202, 298), new Vector3(174, 198, 306),
            new Vector3(164, 186, 325), new Vector3(155, 177, 345), new Vector3(143, 164, 365),
            new Vector3(126, 148, 385), new Vector3(113, 131, 399)
        };
        // The east blade is painted over the greaves. Crop the moving pixels more
        // tightly than the body cutout so the exposed cloth can be filled in behind it.
        static readonly Vector3[] EastBladeEdges = {
            new Vector3(251, 281, 298), new Vector3(260, 282, 320), new Vector3(270, 291, 345),
            new Vector3(281, 304, 368), new Vector3(290, 316, 386), new Vector3(303, 330, 401),
            new Vector3(323, 342, 416)
        };

        public PaintedSaberArm(Vector2 shoulder, Vector2 elbow, Vector2 grip, Vector2 tip, float side,
            int[] outline, int patchEdge, int patchBottom, bool behindBody, SupportArm support)
        {
            this.shoulder = shoulder;
            this.elbow = elbow;
            this.grip = grip;
            this.tip = tip;
            Side = side;
            Outline = outline;
            PatchEdge = patchEdge;
            PatchBottom = patchBottom;
            BehindBody = behindBody;
            Support = support;
        }

        public Vector3 Bind(Vector2 point)
            => Bind(point, shoulder, elbow, grip);

        public Vector2 ExtendBlade(Vector2 point)
        {
            Vector2 axis = (tip - grip).normalized;
            return point + axis * (Mathf.Max(0, Vector2.Dot(point - grip, axis)) * (BladeLengthScale - 1));
        }

        static Vector3 Bind(Vector2 point, Vector2 shoulder, Vector2 elbow, Vector2 grip)
        {
            Vector2 axis = (grip - shoulder).normalized;
            float forearm = Mathf.SmoothStep(0, 1,
                Mathf.InverseLerp(-.04f, .04f, Vector2.Dot(point - elbow, axis)));
            float hand = Mathf.SmoothStep(0, 1,
                Mathf.InverseLerp(-.054f, -.019f, Vector2.Dot(point - grip, axis)));
            return new Vector3(1 - forearm, forearm, hand);
        }

        public Texture2D CreateMask(int facing)
        {
            Vector3[] bladeEdges = facing switch
            {
                0 => SouthBladeEdges,
                1 => SoutheastBladeEdges,
                2 => EastBladeEdges,
                7 => SouthwestBladeEdges,
                _ => null
            };
            return Rasterize(Outline, Side, PatchEdge, PatchBottom, bladeEdges,
                bladeY: 480 - grip.y * 448 + 8, shoulderY: 480 - shoulder.y * 448);
        }

        // The cape hides the far sleeve in two views. Fit the full east arm to those joints
        // so the hand stays on the hilt when it comes out from behind the cape.
        static readonly Vector2 EastShoulder = AtlasPoint(252, 179), EastElbow = AtlasPoint(258, 231),
            EastGrip = AtlasPoint(255, 282), EastTip = AtlasPoint(332, 410);

        internal static readonly int[] EastArmOutline = {
            240, 182, 261, 175, 272, 186, 274, 226, 270, 271, 285, 295, 303, 335,
            326, 385, 344, 413, 323, 420, 302, 405, 272, 366, 255, 324, 241, 302, 240, 271, 239, 217
        };

        static Vector2 AtlasPoint(int x, int y) => new Vector2(x - 256, 480 - y) / 448;

        public static Texture2D CreateFarArmMask()
            => Rasterize(EastArmOutline, 1, 0, 0, EastBladeEdges, bladeY: 290);

        public Vector3 FitFarArm(Vector2 point, out Vector3 weights)
        {
            weights = Bind(point, EastShoulder, EastElbow, EastGrip);
            return FitSection(point, EastShoulder, EastElbow, shoulder, elbow, Side, preserveWidth: true) * weights.x
                + FitSection(point, EastElbow, EastGrip, elbow, grip, Side, preserveWidth: true) * weights.y;
        }

        public Vector2 FitFarBlade(Vector2 point) => FitSection(point, EastGrip, EastTip, grip, tip, Side);

        static Vector2 FitSection(Vector2 point, Vector2 source, Vector2 sourceEnd,
            Vector2 target, Vector2 targetEnd, float mirror, bool preserveWidth = false)
        {
            Vector2 sourceAxis = sourceEnd - source;
            Vector2 targetAxis = targetEnd - target;
            Vector2 delta = point - source;
            Vector2 across = Vector2.Perpendicular(targetAxis);
            // Match the joint spacing without widening the painted sleeve as the bones get longer.
            if (preserveWidth) across = across.normalized * sourceAxis.magnitude;
            return target + (targetAxis * Vector2.Dot(delta, sourceAxis)
                + across * (mirror * Vector2.Dot(delta, Vector2.Perpendicular(sourceAxis))))
                / sourceAxis.sqrMagnitude;
        }

        static Texture2D Rasterize(int[] outline, float side, int patchEdge, int patchBottom, Vector3[] movingEdges,
            float bladeY = float.PositiveInfinity, float shoulderY = -512)
        {
            // The red channel extracts the painted arm. Green marks where the torso needs
            // underpainting. Blue selects the sleeve and alpha selects the rigid blade.
            const int size = 256;
            var pixels = new Color32[size * size];
            var intersections = new float[outline.Length / 2];
            for (int row = 0; row < size; row++)
            {
                float y = (size - row - .5f) * 2;
                Vector3 movingSpan = new Vector3(0, 512, y);
                if (movingEdges != null && y >= movingEdges[0].z)
                {
                    int edge = 1;
                    while (edge < movingEdges.Length - 1 && y > movingEdges[edge].z) edge++;
                    var low = movingEdges[edge - 1];
                    var high = movingEdges[edge];
                    movingSpan = Vector3.Lerp(low, high, Mathf.InverseLerp(low.z, high.z, y));
                }
                int count = 0;
                for (int i = 0, j = outline.Length - 2; i < outline.Length; j = i, i += 2)
                {
                    int ay = outline[i + 1], by = outline[j + 1];
                    if ((ay > y) == (by > y)) continue;
                    intersections[count++] = Mathf.Lerp(outline[i], outline[j], (y - ay) / (by - ay));
                }
                System.Array.Sort(intersections, 0, count);
                for (int i = 0; i + 1 < count; i += 2)
                {
                    int from = Mathf.Clamp(Mathf.CeilToInt((intersections[i] - 1) / 2), 0, size);
                    int to = Mathf.Clamp(Mathf.CeilToInt((intersections[i + 1] - 1) / 2), 0, size);
                    for (int column = from; column < to; column++)
                    {
                        float x = column * 2 + 1;
                        float fill = Mathf.InverseLerp(-5, 5, (patchEdge - x) * side)
                            * Mathf.InverseLerp(0, 16, patchBottom + 8 - y);
                        float inside = Mathf.Min(x - movingSpan.x, movingSpan.y - x);
                        float moving = Mathf.InverseLerp(-1, 1, inside) * 255;
                        byte sleeve = y < bladeY ? (byte)Mathf.RoundToInt(moving) : (byte)0;
                        byte blade = y < bladeY ? (byte)0 : (byte)Mathf.RoundToInt(moving);
                        // Keep the painted pauldron on the torso and overlap the moving sleeve beneath it.
                        float cut = Mathf.InverseLerp(shoulderY + 4, shoulderY + 18, y);
                        float sleeveRoot = Mathf.InverseLerp(shoulderY - 9, shoulderY + 8, y);
                        pixels[row * size + column] = new Color32((byte)Mathf.RoundToInt(cut * 255),
                            (byte)Mathf.RoundToInt(cut * fill * 255), (byte)Mathf.RoundToInt(sleeveRoot * sleeve), blade);
                    }
                }
            }
            var mask = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Painted saber arm cutout", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
            };
            mask.SetPixels32(pixels);
            mask.Apply(false, true);
            return mask;
        }

        public Vector2 RaisedGrip(Vector2 axis, out Vector2 bentElbow)
        {
            bentElbow = shoulder + new Vector2(Side * .6f, -.8f) * Vector2.Distance(shoulder, elbow);
            return bentElbow + axis * Vector2.Distance(elbow, grip);
        }

        public Pose Articulate(Vector2 target, Vector3 axis, float blend, Vector2? elbowTarget,
            float elbowBend, bool twoHanded, bool elbowsDown, out Pose supportPose,
            out WeaponPose weaponPose)
        {
            Vector2 restAxis = (tip - grip).normalized;
            float restAngle = Mathf.Atan2(restAxis.y, restAxis.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
            float bladeTurn = Mathf.DeltaAngle(restAngle, targetAngle) * blend;
            float depth = Mathf.Asin(Mathf.Clamp(axis.z, -1, 1)) * Mathf.Rad2Deg * blend;
            Quaternion bladeRotation = Quaternion.Euler(0, 0, restAngle + bladeTurn)
                * Quaternion.Euler(0, -depth, 0) * Quaternion.Euler(0, 0, -restAngle);
            Vector2 gripAxis = bladeRotation * (Vector3)restAxis;
            Vector2 posedGrip = Vector2.Lerp(grip, target, blend);
            Bone upperBone, lowerBone;
            if (elbowTarget is Vector2 raisedElbow)
            {
                Vector2 joint = Vector2.Lerp(elbow, raisedElbow, blend);
                upperBone = new Bone(shoulder, elbow, shoulder, joint);
                lowerBone = new Bone(elbow, grip, joint, posedGrip);
            }
            else SolveArm(shoulder, elbow, grip, posedGrip, blend, Side, elbowBend, elbowsDown, out upperBone, out lowerBone);

            if (twoHanded)
            {
                Vector2 offhand = Vector2.Lerp(Support.Grip, target - gripAxis * SecondGripSpacing, blend);
                supportPose = Support.PoseAt(offhand, gripAxis, blend, elbowBend, elbowsDown);
            }
            else supportPose = Support.Hidden ? default : Support.FreePose(blend);
            weaponPose = new WeaponPose(grip, posedGrip, bladeRotation);
            return new Pose(upperBone, lowerBone, new Bone(grip, posedGrip, bladeTurn));
        }

        internal WeaponPose RollBladeEdge(WeaponPose pose, Quaternion bodyRotation)
        {
            Vector2 restAxis = (tip - grip).normalized;
            Vector2 screenAxis = bodyRotation * (pose.Rotation * (Vector3)restAxis);
            // Pass through an edge-on silhouette when the blade points across the body.
            float alignment = Side * screenAxis.normalized.x;
            float roll = Mathf.SmoothStep(0, 180, Mathf.InverseLerp(.18f, -.18f, alignment));
            return new WeaponPose(grip, pose.Grip, pose.Rotation * Quaternion.AngleAxis(roll, restAxis));
        }

        static void SolveArm(Vector2 shoulder, Vector2 elbow, Vector2 hand, Vector2 target, float extension,
            float side, float bend, bool elbowsDown, out Bone upper, out Bone lower)
        {
            float upperLength = Vector2.Distance(shoulder, elbow);
            float forearmLength = Vector2.Distance(elbow, hand);
            float fraction = upperLength / (upperLength + forearmLength);
            Vector2 posedElbow = Vector2.Lerp(shoulder, target, fraction);
            if (elbowsDown) posedElbow = LoweredElbow(shoulder, target, upperLength, forearmLength);
            else if (bend > 0)
            {
                // Guide the elbow out and down as the hands draw the saber across the body.
                Vector2 bent = shoulder + new Vector2(side * .78f, -.63f) * upperLength
                    + (target - shoulder) * .14f;
                posedElbow = Vector2.Lerp(posedElbow, bent, bend);
            }
            Vector2 joint = Vector2.Lerp(elbow + (target - hand) * fraction, posedElbow, extension);
            upper = new Bone(shoulder, elbow, shoulder, joint);
            lower = new Bone(elbow, hand, joint, target);
        }

        static Vector2 LoweredElbow(Vector2 shoulder, Vector2 target, float upperLength, float forearmLength)
        {
            float armLength = upperLength + forearmLength;
            Vector2 reach = target - shoulder;
            float distance = reach.magnitude;
            if (distance >= armLength) return shoulder + reach * (upperLength / armLength);

            float span = Mathf.Max(distance, Mathf.Max(armLength * .43f, Mathf.Abs(upperLength - forearmLength) + .002f));
            float along = (upperLength * upperLength - forearmLength * forearmLength + span * span) / (2 * span);
            float bend = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector2 direction = reach / Mathf.Max(.001f, distance);
            // Keep the projected elbow below the shoulder. Ease it toward the axis in profile.
            float down = Mathf.Clamp(-direction.x * 4, -1, 1);
            return shoulder + direction * along + Vector2.Perpendicular(direction) * (bend * down);
        }

        internal readonly struct SupportArm
        {
            public readonly Vector2 Shoulder, Grip;
            public readonly int[] Outline;
            public readonly float Side;
            public readonly int PatchEdge;
            readonly Vector2 elbow;

            static readonly Vector2 DonorShoulder = AtlasPoint(206, 178), DonorElbow = AtlasPoint(193, 225),
                DonorGrip = AtlasPoint(190, 272);
            static readonly int[] DonorOutline = {
                188, 179, 207, 171, 222, 180, 212, 219, 207, 248, 204, 275, 196, 282,
                184, 282, 176, 274, 176, 245, 183, 211
            };

            public SupportArm(int shoulderX, int shoulderY, int elbowX, int elbowY, int gripX, int gripY,
                float side, int patchEdge, params int[] outline)
            {
                Shoulder = AtlasPoint(shoulderX, shoulderY);
                elbow = AtlasPoint(elbowX, elbowY);
                Grip = AtlasPoint(gripX, gripY);
                Side = side;
                PatchEdge = patchEdge;
                Outline = outline;
            }

            public bool Hidden => Outline.Length == 0;
            public static Texture2D CreateDonorMask() => Rasterize(DonorOutline, 1, 0, 0, null);
            public Texture2D CreateBodyMask() => Rasterize(Outline, Side, PatchEdge, 310, null,
                shoulderY: 480 - Shoulder.y * 448);

            public Vector3 Fit(Vector2 point, out Vector3 weights)
            {
                weights = Bind(point, DonorShoulder, DonorElbow, DonorGrip);
                float mirror = -Side;
                return Vector2.Lerp(
                    FitSection(point, DonorShoulder, DonorElbow, Shoulder, elbow, mirror, preserveWidth: true),
                    FitSection(point, DonorElbow, DonorGrip, elbow, Grip, mirror, preserveWidth: true), weights.y);
            }

            public Vector3 BindPaintedArm(Vector2 point) => Bind(point, Shoulder, elbow, Grip);

            public Pose FreePose(float blend)
            {
                Vector2 bentElbow = Shoulder + new Vector2(.22f * Side, -.975f) * Vector2.Distance(Shoulder, elbow);
                Vector2 openHand = bentElbow + new Vector2(.91f * Side, .415f) * Vector2.Distance(elbow, Grip);
                Vector2 joint = Vector2.Lerp(elbow, bentElbow, blend);
                Vector2 hand = Vector2.Lerp(Grip, openHand, blend);
                var forearm = new Bone(elbow, Grip, joint, hand);
                float wristTurn = Vector2.SignedAngle(Grip - elbow, hand - joint) * .78f;
                return new Pose(new Bone(Shoulder, elbow, Shoulder, joint), forearm, new Bone(Grip, hand, wristTurn));
            }

            public Pose PoseAt(Vector2 hand, Vector2 bladeAxis, float extension, float elbowBend, bool elbowsDown)
            {
                SolveArm(Shoulder, elbow, Grip, hand, extension, Side, elbowBend, elbowsDown, out Bone upper, out Bone forearm);
                Vector2 donorAxis = AtlasPoint(112, 387) - DonorGrip;
                Vector2 gripAxis = FitSection(DonorGrip + donorAxis, DonorElbow, DonorGrip,
                    elbow, Grip, -Side, preserveWidth: true) - Grip;
                float turn = Vector2.SignedAngle(gripAxis, bladeAxis) * extension;
                return new Pose(upper, forearm, new Bone(Grip, hand, turn));
            }
        }

        internal readonly struct Bone
        {
            readonly Vector2 rest, posed;
            readonly float cosine, sine;
            readonly Vector2 restAxis;
            readonly float stretch;

            public Bone(Vector2 rest, Vector2 posed, float angle) : this()
            {
                this.rest = rest;
                this.posed = posed;
                cosine = Mathf.Cos(angle * Mathf.Deg2Rad);
                sine = Mathf.Sin(angle * Mathf.Deg2Rad);
            }

            public Bone(Vector2 rest, Vector2 restEnd, Vector2 posed, Vector2 posedEnd)
                : this(rest, posed, Vector2.SignedAngle(restEnd - rest, posedEnd - posed))
            {
                restAxis = (restEnd - rest).normalized;
                stretch = Vector2.Distance(posed, posedEnd) / Vector2.Distance(rest, restEnd) - 1;
            }

            public Vector2 Offset(Vector2 point)
            {
                Vector2 delta = point - rest;
                delta += restAxis * (Vector2.Dot(delta, restAxis) * stretch);
                return posed + new Vector2(cosine * delta.x - sine * delta.y,
                    sine * delta.x + cosine * delta.y) - point;
            }
        }

        internal readonly struct Pose
        {
            readonly Bone upper, forearm, hand;

            public Pose(Bone upper, Bone forearm, Bone hand)
            {
                this.upper = upper;
                this.forearm = forearm;
                this.hand = hand;
            }

            public Vector2 Offset(Vector2 point, Vector3 binding)
                => Vector2.Lerp(upper.Offset(point) * binding.x + forearm.Offset(point) * binding.y,
                    hand.Offset(point), binding.z);
        }

        internal readonly struct WeaponPose
        {
            readonly Vector2 restGrip;
            public readonly Quaternion Rotation;
            public readonly Vector2 Grip;

            public WeaponPose(Vector2 grip, Vector2 target, Quaternion rotation)
            {
                restGrip = grip;
                Rotation = rotation;
                Grip = target;
            }

            public Vector3 Point(Vector2 point) => (Vector3)Grip + Rotation * (Vector3)(point - restGrip);
        }
    }

    // Procedural walking with a shorter stride; cadence is controlled independently.
    [DefaultExecutionOrder(70)]
    [DisallowMultipleComponent]
    public sealed class HeroWalkMesh : MonoBehaviour
    {
        internal readonly struct PaintedGesture
        {
            public readonly Vector2 Grip;
            public readonly bool KeepsBodyShape;
            public readonly PaintedSaberArm? SaberArm;
            readonly Vector2 end;
            readonly Vector2 head, chest;
            readonly float solidRadius, falloffRadius;

            public PaintedGesture(Vector2 grip, Vector2 end, float solidRadius, float falloffRadius) : this()
            {
                Grip = grip; this.end = end; this.solidRadius = solidRadius; this.falloffRadius = falloffRadius;
            }

            public PaintedGesture(Vector2 grip, Vector2 end, float solidRadius, float falloffRadius,
                Vector2 head, Vector2 chest, PaintedSaberArm? saberArm = null) : this(grip, end, solidRadius, falloffRadius)
            {
                this.head = head;
                this.chest = chest;
                KeepsBodyShape = true;
                SaberArm = saberArm;
            }

            public float Weight(Vector2 point)
            {
                Vector2 axis = end - Grip;
                float along = Mathf.Clamp01(Vector2.Dot(point - Grip, axis) / Mathf.Max(.0001f, axis.sqrMagnitude));
                float distance = Vector2.Distance(point, Grip + axis * along);
                float weight = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(solidRadius, falloffRadius, distance));
                if (KeepsBodyShape)
                {
                    // Use the tightest limit so overlapping fades cannot compound into a sharp bend.
                    weight = Mathf.Min(weight, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.10f, .27f, point.y)));
                    weight = Mathf.Min(weight, ArmInfluence(point - head, 32f / 448, 47f / 448));
                    weight = Mathf.Min(weight, ArmInfluence(point - chest, 34f / 448, 42f / 448));
                }
                return weight;
            }

            static float ArmInfluence(Vector2 offset, float width, float height)
            {
                var relative = new Vector2(offset.x / width, offset.y / height);
                float distance = (relative.magnitude - 1) * Mathf.Min(width, height);
                return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .125f, distance));
            }

        }

        const int Columns=40, Rows=56;
        SpriteRenderer source;
        Sprite sprite;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Mesh profileLegMesh;
        MeshRenderer profileLegRenderer;
        Vector3[] profileLegRest, profileLegPose;
        Vector3[] visibleProfileLegRows;
        MaterialPropertyBlock profileLegProperties;
        Material material;
        MaterialPropertyBlock properties;
        Vector3[] rest,vertices;
        Vector2[] weights;
        HeroGaitMath.Binding[] legBindings;
        HeroGaitMath.Rig legRig;
        HeroGaitMath.Rig? paintedRig;
        PaintedGesture paintedGesture;
        float[] gestureWeights;
        Vector3[] saberArmBindings;
        Vector3[] saberArmVertices;
        Vector3[] saberArmRest;
        Mesh saberArmMesh;
        MeshRenderer saberArmRenderer;
        MaterialPropertyBlock saberArmProperties;
        Mesh saberBladeMesh;
        MeshRenderer saberBladeRenderer;
        MaterialPropertyBlock saberBladeProperties;
        PaintedSaberArm.WeaponPose saberWeaponPose;
        Texture2D[] saberMasks;
        Texture2D farSaberArmMask;
        PaintedSaberArm.Pose saberArmPose;
        PaintedSaberArm.Pose supportArmPose;
        Mesh supportArmMesh;
        MeshRenderer supportArmRenderer;
        MaterialPropertyBlock supportArmProperties;
        static readonly int SaberBladeGlow = Shader.PropertyToID("_SaberBladeGlow");
        float saberBladeGlow;
        Vector3[] supportArmRest, supportArmVertices, supportArmBindings;
        Vector3[] freeArmBindings;
        Vector2[] supportArmUv, freeArmUv;
        bool supportUsesOwnArt;
        Texture2D supportDonorMask;
        Texture2D[] supportBodyMasks;
        float twoHandedGrip;
        bool saberArmMoving;
        static readonly Vector3 SaberTorsoPivot = new Vector3(0, .34f, 0);
        Vector3 saberTorsoOffset;
        Quaternion saberTorsoRotation = Quaternion.identity;
        const float SaberPlantTime = .16f;
        Vector2 plantLeftFoot, plantRightFoot, plantLeftKnee, plantRightKnee;
        bool saberPlanting, plantLeftSupport;
        float plantAge, plantBob, plantStartBob, plantStance, saberKneeBend;
        float saberPoseBlend = 1;
        float previousSaberKneeBend;
        Vector3 previousSaberTorso, previousSaberCarry;
        Quaternion previousSaberTorsoRotation;
        bool previousSaberArmMoving, previousSupportArmMoving;
        PaintedSaberArm.Pose previousSaberArm, previousSupportArm;
        PaintedSaberArm.WeaponPose previousSaberWeapon;
        Vector2 castTorso, castHand;
        float castAngle;
        int gridVertexCount;
        int[] leftFootInFront,rightFootInFront;
        bool leftInFront;
        Vector2 motion=Vector2.right;
        float characterScale=1;
        bool hasWalkHeading;
        float phase,blend,smoothedSpeed;
        float upperBodyWeight = 1;
        // 普攻时：步幅 = 常态 2/3，步频 = 常态 3/4。
        // 相位按实际位移推进，而普攻移速是 SlashPlayer.AttackMoveMultiplier(0.5)，
        // 所以相位倍率 = (3/4) / 0.5 = 1.5。改普攻移速时必须同步改这里。
        const float AttackStrideScale = 2f/3f, AttackCadenceScale = 1.5f;
        public bool Attacking { get; set; }
        float walkStrideScale = 1, walkCadenceScale = 1;
        float stanceInset;
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
        float BodyHeight => paintedRig.HasValue ? transform.localScale.y : HeroAppearance.BodyHeight;
        public float Bob => (saberPlanting ? plantBob : HeroGaitMath.BodyBob(phase) * blend) * BodyHeight * characterScale;
        internal bool HasSaberStride => blend > .05f;
        public void Initialize(SpriteRenderer body) => Initialize(body, null, default);

        internal void Initialize(SpriteRenderer body, HeroGaitMath.Rig? painted, PaintedGesture gesture, bool fitStance = true)
        {
            source=body;
            paintedRig = painted;
            paintedGesture = gesture;
            mesh=new Mesh { name="Hero walking legs and robe",hideFlags=HideFlags.DontSave };
            mesh.MarkDynamic();
            var renderObject=new GameObject("Walking mesh");
            renderObject.transform.SetParent(transform,false);
            renderObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            meshRenderer=renderObject.AddComponent<MeshRenderer>();
            var shader=Resources.Load<Shader>("SlashDemo/HeroCloth");
            material=new Material(shader!=null?shader:Shader.Find("Sprites/Default")) { hideFlags=HideFlags.DontSave };
            if (paintedRig is HeroGaitMath.Rig rig)
            {
                // Fit to the hero's heel-strike spacing before shortening the painted stride.
                var hero = HeroGaitMath.ForDirection(0);
                float stanceWidth = rig.right.sole.x - rig.left.sole.x;
                float stanceDifference = stanceWidth - (hero.right.sole.x - hero.left.sole.x);
                float heelStrikeTravel = HeroGaitMath.StrideDistance / (2 * HeroAppearance.BodyHeight);
                float fittedStride = fitStance ? Mathf.Clamp(1 - stanceDifference / heelStrikeTravel, .5f, 1) : 1;
                walkStrideScale = fittedStride * (fitStance ? .6f : .8f);
                walkCadenceScale = 1 / fittedStride;
                strideScale = walkStrideScale;
                cadenceScale = walkCadenceScale;
                stanceInset = fitStance ? stanceWidth * .10f : 0;
                material.SetFloat("_PaintedPortrait", 1);
                meshRenderer.sharedMaterial = material;
            }
            else meshRenderer.sharedMaterials = new[] { material, material, material };
            properties=new MaterialPropertyBlock();source.forceRenderingOff=true;Refresh();
        }

        internal void SetPaintedRig(HeroGaitMath.Rig rig, PaintedGesture gesture)
        {
            if (saberPlanting && plantAge > 0 && plantAge < SaberPlantTime)
            {
                // Hold the support boot in place through the eight-direction sprite change.
                if (plantLeftSupport) plantLeftFoot.x += legRig.left.sole.x - rig.left.sole.x;
                else plantRightFoot.x += legRig.right.sole.x - rig.right.sole.x;
            }
            paintedRig = rig;
            paintedGesture = gesture;
            // The new facing needs fresh weights. Keep the walk phase continuous.
            sprite = null;
        }

        internal void SetProfileLeg(Vector3[] rows, bool bindVisibleLeg = false)
        {
            visibleProfileLegRows = bindVisibleLeg ? rows : null;
            if (rows == null)
            {
                profileLegRest = null;
                if (profileLegRenderer != null) profileLegRenderer.enabled = false;
                return;
            }

            if (profileLegMesh == null)
            {
                var layer = new GameObject("Far profile leg");
                layer.transform.SetParent(transform, false);
                profileLegMesh = new Mesh { name = "Painted far leg", hideFlags = HideFlags.DontSave };
                profileLegMesh.MarkDynamic();
                layer.AddComponent<MeshFilter>().sharedMesh = profileLegMesh;
                profileLegRenderer = layer.AddComponent<MeshRenderer>();
                profileLegRenderer.sharedMaterial = material;
                profileLegProperties = new MaterialPropertyBlock();
                profileLegProperties.SetFloat("_ProfileLegMask", 1);
            }

            var frameSprite = source.sprite;
            var texture = frameSprite.texture;
            var center = frameSprite.rect.min + frameSprite.pivot;
            float pixelsPerUnit = frameSprite.pixelsPerUnit;
            int count = rows.Length * 2;
            profileLegRest = new Vector3[count];
            profileLegPose = new Vector3[count];
            var uv = new Vector2[count];
            var colors = new Color[count];
            var triangles = new List<int>((rows.Length - 1) * 6);
            for (int row = 0; row < rows.Length; row++)
            {
                Vector3 span = rows[row];
                float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(rows[rows.Length - 3].z,
                    rows[rows.Length - 1].z, span.z));
                for (int edge = 0; edge < 2; edge++)
                {
                    int vertex = row * 2 + edge;
                    float x = edge == 0 ? span.x : span.y;
                    profileLegRest[vertex] = new Vector3(x, span.z) / pixelsPerUnit * .94f + Vector3.up * .018f;
                    uv[vertex] = new Vector2((center.x + x) / texture.width, (center.y + span.z) / texture.height);
                    colors[vertex] = new Color(1, 1, 1, alpha);
                }
                if (row < rows.Length - 1) AddLegQuad(triangles, row * 2, row * 2 + 2);
            }
            profileLegMesh.Clear();
            profileLegMesh.vertices = profileLegRest;
            profileLegMesh.uv = uv;
            profileLegMesh.colors = colors;
            profileLegMesh.SetTriangles(triangles, 0);
        }

        float VisibleProfileLegWeight(Vector2 point)
        {
            var rows = visibleProfileLegRows;
            float pixelsPerUnit = sprite.pixelsPerUnit;
            float height = point.y * pixelsPerUnit;
            int row = 1;
            while (row < rows.Length - 1 && height > rows[row].z) row++;
            Vector3 lower = rows[row - 1], upper = rows[row];
            Vector3 span = Vector3.Lerp(lower, upper, Mathf.InverseLerp(lower.z, upper.z, height));
            float x = point.x * pixelsPerUnit;
            float outside = Mathf.Max(span.x - x, x - span.y) / pixelsPerUnit;
            float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .16f, outside));
            float cuff = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(rows[rows.Length - 2].z / pixelsPerUnit,
                rows[rows.Length - 1].z / pixelsPerUnit + .12f, point.y));
            return 1 - Mathf.Max(edge, cuff);
        }

        void RefreshProfileLeg(Vector2 foot, Vector2 knee, float opacity)
        {
            profileLegRenderer.enabled = source.enabled && opacity > .002f;
            if (!profileLegRenderer.enabled) return;
            for (int i = 0; i < profileLegRest.Length; i++)
            {
                Vector3 restPoint = profileLegRest[i];
                float height = restPoint.y - .018f;
                profileLegPose[i] = restPoint + (Vector3)HeroGaitMath.PaintedShinOffset(height, foot, knee);
            }
            profileLegMesh.vertices = profileLegPose;
            profileLegMesh.RecalculateBounds();
            profileLegRenderer.sortingLayerID = source.sortingLayerID;
            profileLegRenderer.sortingOrder = source.sortingOrder - 1;
            Color tint = source.color;
            tint.r *= .72f;
            tint.g *= .72f;
            tint.b *= .72f;
            tint.a *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.002f, .25f, opacity));
            profileLegProperties.SetColor("_Color", tint);
            profileLegRenderer.SetPropertyBlock(profileLegProperties);
        }
        public void Advance(Vector2 distance,float dt,bool dashing,ObliqueView view)
        {
            if(dt<=0) return;
            upperBodyWeight = Mathf.MoveTowards(upperBodyWeight, Attacking || dashing ? 0 : 1, dt * 12);
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
            strideScale=Mathf.Lerp(strideScale,Attacking?AttackStrideScale:walkStrideScale,ease);
            cadenceScale=Mathf.Lerp(cadenceScale,Attacking?AttackCadenceScale:walkCadenceScale,ease);
            phase=HeroGaitMath.AdvancePhase(phase,travel,characterScale * (BodyHeight / HeroAppearance.BodyHeight),dashing,dt,cadenceScale);
            if(!dashing && travel>.00001f)
            {
                Vector2 projected=ObliqueProjection.ProjectGround(distance/distance.magnitude,view.elevation);
                motion=hasWalkHeading?Vector2.Lerp(motion,projected,1-Mathf.Exp(-22*dt)):projected;
                hasWalkHeading=true;
            }
        }

        internal void ResetWalk()
        {
            phase = blend = smoothedSpeed = 0;
            hasWalkHeading = false;
            EndSaberPlant();
        }

        internal void BeginSaberPlant()
        {
            if (saberPlanting) return;
            var frame = WalkFrame();
            GetFootOffsets(frame, out plantLeftFoot, out plantRightFoot, out plantLeftKnee, out plantRightKnee);
            float leftPhase = phase + (legRig.reversePhase ? Mathf.PI : 0);
            plantLeftSupport = HeroGaitMath.Sample(leftPhase, strideScale).lift
                <= HeroGaitMath.Sample(leftPhase + Mathf.PI, strideScale).lift;
            plantStartBob = plantBob = HeroGaitMath.BodyBob(phase) * blend;
            plantLeftFoot *= blend;
            plantRightFoot *= blend;
            plantLeftKnee *= blend;
            plantRightKnee *= blend;
            plantAge = 0;
            plantStance = 1;
            saberPlanting = true;
        }

        internal void AdvanceSaberPlant(float dt, float stance)
        {
            if (!saberPlanting) return;
            plantAge += dt;
            blend = Mathf.MoveTowards(blend, 0, dt / SaberPlantTime);
            plantBob = HeroGaitMath.BodyBob(phase) * blend;
            plantStance = stance;
            smoothedSpeed = 0;
            if (plantAge >= SaberPlantTime) hasWalkHeading = false;
        }

        internal void EndSaberPlant()
        {
            saberPlanting = false;
            saberKneeBend = 0;
        }

        internal void CaptureSaberPose()
        {
            previousSaberTorso = saberTorsoOffset;
            previousSaberTorsoRotation = saberTorsoRotation;
            previousSaberKneeBend = saberKneeBend;
            previousSaberArmMoving = saberArmMoving;
            previousSupportArmMoving = saberArmMoving && (twoHandedGrip > 0 || !paintedGesture.SaberArm.Value.Support.Hidden);
            previousSaberCarry = saberArmMoving ? Vector3.zero : SaberCarry(WalkFrame());
            previousSaberArm = saberArmPose;
            previousSupportArm = supportArmPose;
            previousSaberWeapon = saberWeaponPose;
            saberPoseBlend = 0;
        }

        internal void SetSaberPoseBlend(float blend) => saberPoseBlend = blend;

        internal void SetSaberBladeGlow(bool active) => saberBladeGlow = active ? 1 : 0;

        internal void SetPaintedCastPose(Vector2 torso, Vector2 hand, float angle)
        {
            castTorso = torso;
            castHand = hand;
            castAngle = angle * Mathf.Deg2Rad;
            saberArmMoving = false;
            twoHandedGrip = 0;
        }

        internal void SetSaberBodyPose(Vector2 offset, float lean, float kneeBend = 0)
        {
            var rotation = Quaternion.Euler(0, 0, lean);
            saberTorsoOffset = Vector3.Lerp(previousSaberTorso, offset, saberPoseBlend);
            saberTorsoRotation = saberPoseBlend < 1 ? Quaternion.Slerp(previousSaberTorsoRotation, rotation, saberPoseBlend) : rotation;
            saberKneeBend = Mathf.Lerp(previousSaberKneeBend, kneeBend, saberPoseBlend);
        }

        Vector3 SaberBodyPoint(Vector3 point) =>
            SaberTorsoPivot + saberTorsoRotation * (point - SaberTorsoPivot) + saberTorsoOffset;

        internal Vector2 SaberGripBeforeBodyPose(Vector2 point) =>
            SaberTorsoPivot + Quaternion.Inverse(saberTorsoRotation) * ((Vector3)point - SaberTorsoPivot - saberTorsoOffset);

        internal void SetSaberCastPose(Vector2 hand, Vector3 axis, float blend, Vector2? elbowTarget = null,
            float elbowBend = 0, bool twoHanded = true, bool elbowsDown = false)
        {
            twoHandedGrip = twoHanded ? blend : 0;
            saberArmMoving = blend > 0;
            if (saberArmMoving)
                saberArmPose = paintedGesture.SaberArm.Value.Articulate(hand, Quaternion.Inverse(saberTorsoRotation) * axis,
                    blend, elbowTarget, elbowBend, twoHanded, elbowsDown,
                    out supportArmPose, out saberWeaponPose);
        }

        PaintedSaberArm.WeaponPose SaberWeapon(Vector3 carry)
        {
            Vector2 grip = paintedGesture.Grip;
            var target = saberArmMoving ? saberWeaponPose
                : new PaintedSaberArm.WeaponPose(grip, grip + (Vector2)carry, Quaternion.identity);
            if (saberPoseBlend < 1)
            {
                Vector2 fromGrip = previousSaberArmMoving ? previousSaberWeapon.Grip : grip + (Vector2)previousSaberCarry;
                Quaternion fromRotation = previousSaberArmMoving ? previousSaberWeapon.Rotation : Quaternion.identity;
                target = new PaintedSaberArm.WeaponPose(grip, Vector2.Lerp(fromGrip, target.Grip, saberPoseBlend),
                    Quaternion.Slerp(fromRotation, target.Rotation, saberPoseBlend));
            }
            return saberArmMoving ? paintedGesture.SaberArm.Value.RollBladeEdge(target, saberTorsoRotation) : target;
        }

        static float CastTorsoWeight(float height)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.40f, .78f, height));

        internal Vector3 PaintedCastWorldPoint(Vector2 point)
        {
            var bounds = source.sprite.bounds;
            float height = Mathf.InverseLerp(bounds.min.y, bounds.max.y, point.y);
            if (paintedGesture.SaberArm.HasValue)
                return transform.TransformPoint(SaberBodyPoint(SaberWeapon(saberArmMoving ? Vector3.zero : SaberCarry(WalkFrame())).Point(point)));
            float gripWeight = CastTorsoWeight(paintedGesture.Grip.y - bounds.min.y);
            return transform.TransformPoint(point + CastOffset(point, paintedGesture.Weight(point),
                height, gripWeight, Mathf.Sin(castAngle), Mathf.Cos(castAngle)));
        }

        void BuildSaberArm(PaintedSaberArm arm, Vector2[] uv, List<int> triangles)
        {
            if (saberArmMesh == null)
            {
                var layer = new GameObject("Saber arm");
                layer.transform.SetParent(transform, false);
                saberArmMesh = new Mesh { name = "Saber sleeve", hideFlags = HideFlags.DontSave };
                saberArmMesh.MarkDynamic();
                layer.AddComponent<MeshFilter>().sharedMesh = saberArmMesh;
                saberArmRenderer = layer.AddComponent<MeshRenderer>();
                saberArmRenderer.sharedMaterial = material;
                saberArmProperties = new MaterialPropertyBlock();
                saberArmProperties.SetFloat("_SaberCutMode", 2);
                var blade = new GameObject("Saber blade");
                blade.transform.SetParent(transform, false);
                saberBladeMesh = new Mesh { name = "Rigid saber blade", hideFlags = HideFlags.DontSave };
                blade.AddComponent<MeshFilter>().sharedMesh = saberBladeMesh;
                saberBladeRenderer = blade.AddComponent<MeshRenderer>();
                saberBladeRenderer.sharedMaterial = material;
                saberBladeProperties = new MaterialPropertyBlock();
                saberBladeProperties.SetFloat("_SaberCutMode", 3);
                saberMasks = new Texture2D[8];
            }
            int facing = legRig.direction;
            if (saberMasks[facing] == null) saberMasks[facing] = arm.CreateMask(facing);
            properties.SetTexture("_SaberCutMask", saberMasks[facing]);
            properties.SetFloat("_SaberPatchOffset", -arm.Side * 32 / sprite.texture.width);
            var armMask = saberMasks[facing];
            saberArmRest = new Vector3[rest.Length];
            var bladeRest = new Vector3[rest.Length];
            if (arm.BehindBody)
            {
                if (farSaberArmMask == null) farSaberArmMask = PaintedSaberArm.CreateFarArmMask();
                armMask = farSaberArmMask;
                var rect = sprite.rect;
                var atlas = sprite.texture;
                var size = new Vector2(rect.width / atlas.width, rect.height / atlas.height);
                var origin = new Vector2(2 * size.x, size.y);
                uv = new Vector2[rest.Length];
                for (int i = 0; i < rest.Length; i++)
                {
                    uv[i] = origin + Vector2.Scale(weights[i], size);
                    saberArmRest[i] = arm.FitFarArm(rest[i], out saberArmBindings[i]);
                    bladeRest[i] = arm.ExtendBlade(arm.FitFarBlade(rest[i])) - arm.Grip;
                }
            }
            else
            {
                System.Array.Copy(rest, saberArmRest, rest.Length);
                for (int i = 0; i < rest.Length; i++) bladeRest[i] = arm.ExtendBlade(rest[i]) - arm.Grip;
            }
            saberArmProperties.SetTexture("_SaberCutMask", armMask);
            saberBladeProperties.SetTexture("_SaberCutMask", armMask);
            saberArmVertices = new Vector3[rest.Length];
            saberArmMesh.Clear();
            saberArmMesh.vertices = saberArmRest;
            saberArmMesh.uv = uv;
            saberArmMesh.uv2 = weights;
            saberArmMesh.SetTriangles(triangles, 0);
            BuildSaberBlade(arm, bladeRest, uv, triangles);
            saberBladeMesh.RecalculateBounds();
            BuildSupportArm(arm.Support, facing, triangles);
        }

        void BuildSaberBlade(PaintedSaberArm arm, Vector3[] bladeRest, Vector2[] uv, List<int> triangles)
        {
            // Extend the painted leather grip under the second fist when both hands join the blade.
            Vector2 axis = (arm.ExtendedTip - arm.Grip).normalized;
            Vector2 edge = Vector2.Perpendicular(axis) * .013f;
            Vector2 front = axis * .048f;
            Vector2 back = -axis * (PaintedSaberArm.SecondGripSpacing + .023f);
            int first = bladeRest.Length;
            var bladeVertices = new List<Vector3>(bladeRest) { back - edge, back + edge, front - edge, front + edge };
            Vector2 AtlasUv(int x, int y) => new Vector2(x / (float)sprite.texture.width, 1 - y / (float)sprite.texture.height);
            var bladeUv = new List<Vector2>(uv) { AtlasUv(199, 248), AtlasUv(206, 251), AtlasUv(193, 262), AtlasUv(200, 265) };
            var bladeLocalUv = new List<Vector2>(weights);
            for (int i = 0; i < 4; i++) bladeLocalUv.Add(Vector2.left);
            var bladeTriangles = new List<int>(triangles) { first, first + 2, first + 1, first + 1, first + 2, first + 3 };
            saberBladeMesh.Clear();
            saberBladeMesh.SetVertices(bladeVertices);
            saberBladeMesh.SetUVs(0, bladeUv);
            saberBladeMesh.SetUVs(1, bladeLocalUv);
            saberBladeMesh.SetTriangles(bladeTriangles, 0);
        }

        void BuildSupportArm(PaintedSaberArm.SupportArm arm, int facing, List<int> triangles)
        {
            if (supportArmMesh == null)
            {
                var layer = new GameObject("Saber support arm");
                layer.transform.SetParent(transform, false);
                supportArmMesh = new Mesh { name = "Saber support sleeve", hideFlags = HideFlags.DontSave };
                supportArmMesh.MarkDynamic();
                layer.AddComponent<MeshFilter>().sharedMesh = supportArmMesh;
                supportArmRenderer = layer.AddComponent<MeshRenderer>();
                supportArmRenderer.sharedMaterial = material;
                supportArmProperties = new MaterialPropertyBlock();
                supportArmProperties.SetFloat("_SaberCutMode", 2);
                supportDonorMask = PaintedSaberArm.SupportArm.CreateDonorMask();
                supportArmProperties.SetTexture("_SaberCutMask", supportDonorMask);
                supportBodyMasks = new Texture2D[8];
            }
            if (!arm.Hidden)
            {
                if (supportBodyMasks[facing] == null) supportBodyMasks[facing] = arm.CreateBodyMask();
                properties.SetTexture("_SupportCutMask", supportBodyMasks[facing]);
                properties.SetFloat("_SupportPatchOffset", -arm.Side * 32 / sprite.texture.width);
            }
            var size = new Vector2(sprite.rect.width / sprite.texture.width, sprite.rect.height / sprite.texture.height);
            supportArmUv = new Vector2[rest.Length];
            freeArmUv = arm.Hidden ? null : new Vector2[rest.Length];
            var cellOrigin = new Vector2(sprite.rect.x / sprite.texture.width, sprite.rect.y / sprite.texture.height);
            supportArmRest = new Vector3[rest.Length];
            supportArmVertices = new Vector3[rest.Length];
            supportArmBindings = new Vector3[rest.Length];
            freeArmBindings = arm.Hidden ? null : new Vector3[rest.Length];
            for (int i = 0; i < rest.Length; i++)
            {
                // The supporting sleeve is sampled from the first cell of the atlas.
                Vector2 cellUv = Vector2.Scale(weights[i], size);
                supportArmUv[i] = Vector2.up * size.y + cellUv;
                supportArmRest[i] = arm.Fit(rest[i], out supportArmBindings[i]);
                if (!arm.Hidden)
                {
                    freeArmUv[i] = cellOrigin + cellUv;
                    freeArmBindings[i] = arm.BindPaintedArm(rest[i]);
                }
            }
            supportArmMesh.Clear();
            supportArmMesh.vertices = supportArmRest;
            supportArmMesh.uv = supportArmUv;
            supportArmMesh.uv2 = weights;
            supportArmMesh.SetTriangles(triangles, 0);
            supportUsesOwnArt = false;
            supportArmProperties.SetTexture("_SaberCutMask", supportDonorMask);
        }

        void RefreshSaberArm(bool drawArm, bool freeArm)
        {
            if (saberArmRenderer == null) return;
            var arm = paintedGesture.SaberArm;
            bool visible = drawArm && source.enabled;
            properties.SetFloat("_SaberCutMode", visible ? 1 : 0);
            bool supportVisible = visible && (twoHandedGrip > 0 || freeArm);
            if (supportUsesOwnArt != freeArm)
            {
                supportUsesOwnArt = freeArm;
                supportArmMesh.uv = freeArm ? freeArmUv : supportArmUv;
                supportArmProperties.SetTexture("_SaberCutMask", freeArm ? supportBodyMasks[legRig.direction] : supportDonorMask);
            }
            if (!supportVisible && supportArmRenderer.enabled) supportArmMesh.vertices = supportArmRest;
            supportArmRenderer.enabled = supportVisible;
            properties.SetFloat("_SupportCut", supportVisible && !arm.Value.Support.Hidden ? 1 : 0);
            if (!visible && saberArmRenderer.enabled) saberArmMesh.vertices = saberArmRest;
            saberArmRenderer.enabled = saberBladeRenderer.enabled = visible;
            if (!visible) return;
            saberArmMesh.vertices = saberArmVertices;
            saberArmMesh.RecalculateBounds();
            saberArmRenderer.sortingLayerID = source.sortingLayerID;
            saberArmRenderer.sortingOrder = source.sortingOrder + (arm.Value.BehindBody ? -1 : 2);
            saberArmProperties.SetColor("_Color", source.color);
            saberArmRenderer.SetPropertyBlock(saberArmProperties);
            saberBladeRenderer.sortingLayerID = source.sortingLayerID;
            saberBladeRenderer.sortingOrder = saberArmRenderer.sortingOrder - 1;
            saberBladeProperties.SetColor("_Color", source.color);
            saberBladeProperties.SetFloat(SaberBladeGlow, saberBladeGlow);
            saberBladeProperties.SetFloat("_SaberGripExtension", twoHandedGrip);
            saberBladeRenderer.SetPropertyBlock(saberBladeProperties);

            if (!supportVisible) return;
            supportArmMesh.vertices = supportArmVertices;
            supportArmMesh.RecalculateBounds();
            supportArmRenderer.sortingLayerID = source.sortingLayerID;
            // In the northwest view the joining hand reaches in front of the chest, under the cape.
            bool behindCape = arm.Value.Support.Hidden || (legRig.direction == 5 && !freeArm);
            supportArmRenderer.sortingOrder = source.sortingOrder + (behindCape ? -3 : 3);
            supportArmProperties.SetColor("_Color", source.color);
            supportArmRenderer.SetPropertyBlock(supportArmProperties);
        }

        Vector2 CastOffset(Vector2 point, float handWeight, float height, float gripWeight, float sine, float cosine)
        {
            // The head and chest inherit the supported body translation. The arm adds its own motion.
            if (paintedGesture.KeepsBodyShape)
                return castTorso * HeroLayerMath.PlantedSupport(height) + castHand * handWeight;

            Vector2 fromGrip = point - paintedGesture.Grip;
            var rotated = new Vector2(cosine * fromGrip.x - sine * fromGrip.y, sine * fromGrip.x + cosine * fromGrip.y);
            return castTorso * Mathf.Lerp(CastTorsoWeight(height), gripWeight, handWeight) + (castHand + rotated - fromGrip) * handWeight;
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
            if (source == null || source.sprite == null) return position;
            if (sprite != source.sprite) BuildGrid();
            var bounds = source.sprite.bounds;
            Vector2 uv = new Vector2(Mathf.InverseLerp(bounds.min.x, bounds.max.x, position.x),
                Mathf.InverseLerp(bounds.min.y, bounds.max.y, position.y));
            var frame = new HeroGaitMath.Frame { phase = phase, upperBlend = Attacking ? 0 : blend * upperBodyWeight,
                direction = legRig.direction };
            position = HeroGaitMath.UpperBodyDeform(position, uv, frame);
            return layeredMotion ? CombatPoint(position, uv, new Vector2(0, bounds.min.y + bounds.size.y * 0.52f)) : position;
        }

        void BuildGrid()
        {
            sprite=source.sprite;gridVertexCount=(Columns+1)*(Rows+1);
            int parts = paintedRig.HasValue ? 1 : 3;
            int count = paintedRig.HasValue ? gridVertexCount : gridVertexCount * parts + 16;
            rest=new Vector3[count];vertices=new Vector3[count];weights=new Vector2[count];
            legBindings=new HeroGaitMath.Binding[count];
            gestureWeights = paintedRig.HasValue && !paintedGesture.SaberArm.HasValue ? new float[count] : null;
            saberArmBindings = paintedGesture.SaberArm.HasValue ? new Vector3[count] : null;
            int direction=sprite.name=="Hero back"?4:0;
            string[] names={"Hero S","Hero SE","Hero E","Hero NE","Hero N","Hero NW","Hero W","Hero SW"};
            for(int i=0;i<names.Length;i++) if(sprite.name==names[i]) direction=i;
            legRig=paintedRig ?? HeroGaitMath.ForDirection(direction);
            legRig.keepFeetLevel = paintedRig.HasValue;
            var uv=new Vector2[count];var colors=paintedRig.HasValue ? null : new Color[count];
            var rect=sprite.rect;var bounds=sprite.bounds;
            for(int y=0;y<=Rows;y++) for(int x=0;x<=Columns;x++)
            {
                int gridIndex=y*(Columns+1)+x;
                float u=x/(float)Columns,v=y/(float)Rows;
                for(int part=0;part<parts;part++)
                {
                    int i=gridIndex+part*gridVertexCount;
                    rest[i]=new Vector3(bounds.min.x+bounds.size.x*u,bounds.min.y+bounds.size.y*v);
                    weights[i]=new Vector2(u,v);legBindings[i]=HeroGaitMath.Bind(rest[i],legRig);
                    if (visibleProfileLegRows != null)
                    {
                        float limit = VisibleProfileLegWeight(rest[i]);
                        legBindings[i].left = Mathf.Min(legBindings[i].left, limit);
                        legBindings[i].right = Mathf.Min(legBindings[i].right, limit);
                    }
                    if (saberArmBindings != null) saberArmBindings[i] = paintedGesture.SaberArm.Value.Bind(rest[i]);
                    else if (gestureWeights != null) gestureWeights[i] = paintedGesture.Weight(rest[i]);
                    uv[i]=new Vector2((rect.x+rect.width*u)/sprite.texture.width,(rect.y+rect.height*v)/sprite.texture.height);
                    if (colors != null)
                        colors[i]=new Color(legBindings[i].left,legBindings[i].right,part*.5f,HeroGaitMath.SoleMask(rest[i],legBindings[i],legRig));
                }
            }
            if (paintedRig.HasValue)
            {
                var paintedTriangles = new List<int>(Columns * Rows * 6);
                for (int y = 0; y < Rows; y++)
                    for (int x = 0; x < Columns; x++)
                    {
                        int a = y * (Columns + 1) + x;
                        AddLegQuad(paintedTriangles, a, a + Columns + 1);
                    }
                mesh.Clear();
                mesh.vertices = rest;
                mesh.uv = uv;
                mesh.uv2 = weights;
                mesh.SetTriangles(paintedTriangles, 0);
                material.mainTexture = sprite.texture;
                if (paintedGesture.SaberArm is PaintedSaberArm arm) BuildSaberArm(arm, uv, paintedTriangles);
                return;
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
        HeroGaitMath.Frame WalkFrame()
        {
            float mirror = source.flipX ? -1 : 1;
            Vector2 heading = paintedRig.HasValue ? new Vector2(motion.x * mirror, motion.y) : motion;
            var frame = HeroGaitMath.Prepare(legRig, phase, blend, heading, strideScale);
            frame.upperBlend = Attacking ? 0 : blend * upperBodyWeight;
            return frame;
        }

        void GetFootOffsets(HeroGaitMath.Frame frame, out Vector2 leftStep, out Vector2 rightStep,
            out Vector2 leftKnee, out Vector2 rightKnee)
        {
            leftStep = frame.left.sole - frame.left.rest.sole;
            rightStep = frame.right.sole - frame.right.rest.sole;
            leftKnee = rightKnee = Vector2.zero;
            if (legRig.preserveBootShape)
                HeroGaitMath.FitPaintedStride(legRig, frame, ref leftStep, ref rightStep, out leftKnee, out rightKnee);
            else if (paintedRig.HasValue)
            {
                // The gap between the boots is part of the same painted surface.
                float closing = Mathf.Max(0, leftStep.x - rightStep.x) * .5f;
                leftStep.x -= closing;
                rightStep.x += closing;
            }
        }

        Vector2 SettleFoot(Vector2 foot, bool supports, float settle)
            => new Vector2(foot.x * (supports ? plantStance : 1 - settle),
                Mathf.Lerp(foot.y + plantStartBob, 0, settle) - plantBob);

        Vector3 SaberCarry(HeroGaitMath.Frame frame)
        {
            Vector2 shoulder = paintedGesture.SaberArm.Value.Shoulder;
            var bounds = source.sprite.bounds;
            var uv = new Vector2(Mathf.InverseLerp(bounds.min.x, bounds.max.x, shoulder.x),
                Mathf.InverseLerp(bounds.min.y, bounds.max.y, shoulder.y));
            return HeroGaitMath.UpperBodyDeform(shoulder, uv, frame) - (Vector3)shoulder;
        }

        public void Refresh()
        {
            if (source == null || source.sprite == null) return;
            if (mesh == null || meshRenderer == null || material == null) { UseStaticBody(); return; }
            if (properties == null) properties = new MaterialPropertyBlock();
            if (sprite != source.sprite) BuildGrid();
            float mirror = source.flipX ? -1 : 1;
            var frame = WalkFrame();
            GetFootOffsets(frame, out Vector2 leftStep, out Vector2 rightStep, out Vector2 leftKnee, out Vector2 rightKnee);
            if (saberPlanting)
            {
                float settle = Mathf.SmoothStep(0, 1, plantAge / SaberPlantTime);
                leftStep = SettleFoot(plantLeftFoot, plantLeftSupport, settle);
                rightStep = SettleFoot(plantRightFoot, !plantLeftSupport, settle);
                leftKnee = Vector2.Lerp(plantLeftKnee, leftStep * .45f, settle);
                rightKnee = Vector2.Lerp(plantRightKnee, rightStep * .45f, settle);
            }
            else
            {
                leftStep *= frame.blend;
                rightStep *= frame.blend;
                leftKnee *= frame.blend;
                rightKnee *= frame.blend;
            }
            Vector2 waist=new Vector2(0,sprite.bounds.min.y+sprite.bounds.size.y*.52f);
            bool posing = saberArmMoving || castAngle != 0 || castTorso != Vector2.zero || castHand != Vector2.zero;
            // The cutout supplies the same length of saber in the carry and attack poses.
            bool drawSaberArm = saberArmBindings != null;
            bool freeArm = drawSaberArm && saberArmMoving && twoHandedGrip == 0 && freeArmBindings != null;
            Vector3 saberCarry = drawSaberArm && !saberArmMoving ? SaberCarry(frame) : Vector3.zero;
            if (drawSaberArm)
            {
                var blade = saberBladeRenderer.transform;
                var weapon = SaberWeapon(saberCarry);
                blade.localPosition = SaberBodyPoint(weapon.Grip);
                blade.localRotation = saberTorsoRotation * weapon.Rotation;
            }
            float sine = Mathf.Sin(castAngle), cosine = Mathf.Cos(castAngle);
            float gripTorsoWeight = CastTorsoWeight(paintedGesture.Grip.y - sprite.bounds.min.y);
            float hipBlendStart = Mathf.Lerp(.21f, .14f, saberKneeBend);
            for(int i=0;i<vertices.Length;i++)
            {
                var p=rest[i];
                if (paintedRig.HasValue)
                {
                    var binding = legBindings[i];
                    Vector2 leftOffset = leftStep, rightOffset = rightStep;
                    if (legRig.preserveBootShape)
                    {
                        leftOffset = HeroGaitMath.PaintedShinOffset(p.y, leftStep, leftKnee);
                        rightOffset = HeroGaitMath.PaintedShinOffset(p.y, rightStep, rightKnee);
                        if (saberKneeBend > 0)
                        {
                            float knee = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.09f, .24f, p.y))
                                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.27f, .34f, p.y))) * saberKneeBend;
                            leftOffset += new Vector2(-.029f, -.021f) * knee;
                            rightOffset += new Vector2(.029f, -.021f) * knee;
                        }
                    }
                    vertices[i] = HeroGaitMath.DeformPainted(p, weights[i], binding, frame, leftOffset, rightOffset);
                    if (drawSaberArm)
                    {
                        float torsoWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hipBlendStart, .37f, weights[i].y));
                        vertices[i] = Vector3.Lerp(vertices[i], SaberBodyPoint(vertices[i]), torsoWeight);
                        if (saberArmMoving)
                        {
                            Vector3 armPoint = saberArmRest[i];
                            Vector3 offset = saberArmPose.Offset(armPoint, saberArmBindings[i]);
                            if (saberPoseBlend < 1)
                            {
                                Vector3 from = previousSaberArmMoving
                                    ? previousSaberArm.Offset(armPoint, saberArmBindings[i]) : previousSaberCarry;
                                offset = Vector3.Lerp(from, offset, saberPoseBlend);
                            }
                            saberArmVertices[i] = SaberBodyPoint(armPoint + offset);
                            if (twoHandedGrip > 0 || freeArm)
                            {
                                Vector3 supportPoint = freeArm ? rest[i] : supportArmRest[i];
                                Vector3 supportBinding = freeArm ? freeArmBindings[i] : supportArmBindings[i];
                                Vector2 supportOffset = supportArmPose.Offset(supportPoint, supportBinding);
                                if (saberPoseBlend < 1)
                                    supportOffset = Vector2.Lerp(previousSupportArmMoving
                                        ? previousSupportArm.Offset(supportPoint, supportBinding) : Vector2.zero,
                                        supportOffset, saberPoseBlend);
                                supportArmVertices[i] = SaberBodyPoint(supportPoint + (Vector3)supportOffset);
                            }
                        }
                        else saberArmVertices[i] = SaberBodyPoint(saberArmRest[i] + saberCarry);
                    }
                    else if (posing)
                        vertices[i] += (Vector3)CastOffset(p, gestureWeights[i], weights[i].y,
                            gripTorsoWeight, sine, cosine);
                    // Shift each boot inward, fading the offset into the painted hips.
                    vertices[i].x = (vertices[i].x + stanceInset * (binding.left - binding.right)) * mirror;
                    continue;
                }
                if(source.flipX) p.x=-p.x;
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
            if(!paintedRig.HasValue && nearerLeft!=leftInFront)
            {
                leftInFront=nearerLeft;mesh.SetTriangles(leftInFront?leftFootInFront:rightFootInFront,0,false);
            }
            mesh.RecalculateBounds();meshRenderer.sortingLayerID=source.sortingLayerID;
            meshRenderer.sortingOrder=source.sortingOrder;meshRenderer.enabled=source.enabled;
            RefreshSaberArm(drawSaberArm, freeArm);
            properties.SetColor("_Color", source.color);
            properties.SetFloat("_WalkBlend", blend);
            meshRenderer.SetPropertyBlock(properties);
            if (profileLegRest != null)
            {
                bool farIsLeft = legRig.left.sole.y > legRig.right.sole.y;
                RefreshProfileLeg(farIsLeft ? leftStep : rightStep, farIsLeft ? leftKnee : rightKnee, blend);
            }
        }
        void LateUpdate(){Refresh();}
        public void UseStaticBody()
        {
            if(source!=null) source.forceRenderingOff=false;
            if(profileLegRenderer!=null) profileLegRenderer.enabled=false;
            if(saberArmRenderer!=null) saberArmRenderer.enabled=false;
            if(saberBladeRenderer!=null) saberBladeRenderer.enabled=false;
            if(supportArmRenderer!=null) supportArmRenderer.enabled=false;
            if(meshRenderer!=null) meshRenderer.enabled=false;enabled=false;
        }
        void OnDestroy()
        {
            if(source!=null) source.forceRenderingOff=false;Release(mesh);Release(profileLegMesh);Release(saberArmMesh);Release(material);
            if(saberMasks!=null) foreach (var mask in saberMasks) Release(mask);
            Release(farSaberArmMask);
            Release(supportArmMesh); Release(supportDonorMask);
            Release(saberBladeMesh);
            if(supportBodyMasks!=null) foreach (var mask in supportBodyMasks) Release(mask);
        }
        static void Release(Object value)
        {
            if(value==null) return;
            if(Application.isPlaying) Destroy(value);else DestroyImmediate(value);
        }
    }
}
