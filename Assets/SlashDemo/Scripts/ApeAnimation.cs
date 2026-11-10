using UnityEngine;
namespace SlashDemo
{
    // Bear-style footfall timing with rigid extremities and continuous shoulder/hip transitions.
    [DefaultExecutionOrder(80)]
    public sealed class ApeAnimation : MonoBehaviour
    {
        public const float CycleDistance=.96f;
        const float Stance=.62f, Reach=CycleDistance*Stance*.5f, Lift=.20f;
        static readonly Vector2[] Joints={new Vector2(.49f,.405f),new Vector2(.69f,.34f),new Vector2(.47f,.66f),new Vector2(.79f,.64f)};
        static readonly Vector2[] Ends={new Vector2(.29f,.105f),new Vector2(.73f,.14f)};
        static readonly Vector2[] Elbows = { new Vector2(.34f, .51f), new Vector2(.82f, .47f) };
        const int Grid = 96, SplitColumn = Grid / 3, RowWidth = Grid + 2;
        Vector2[] coordinates;
        Vector4[] weights, attackWeights;
        SpriteRenderer source; SlashArena arena; SlashEnemy enemy;
        Mesh mesh; MeshRenderer skin; Material material;
        readonly Vector2[] pivots=new Vector2[4],offsets=new Vector2[4],rotations=new Vector2[4];
        readonly float[] angles=new float[4];
        readonly Vector2[] elbows = new Vector2[2], shoulderTurns = new Vector2[2], elbowTurns = new Vector2[2];
        Vector3[] rest,posed;
        Vector2 previous,motion=new Vector2(.7f,-.5f);
        float phase, blend, speed, bob, tuck;
        bool posingAttack;

        readonly struct AttackPose
        {
            public readonly Vector2 Shoulders, Elbows;
            public readonly float Tuck;

            public AttackPose(float leftShoulder, float rightShoulder, float leftElbow, float rightElbow, float tuck = 0)
            {
                Shoulders = new Vector2(leftShoulder, rightShoulder);
                Elbows = new Vector2(leftElbow, rightElbow);
                Tuck = tuck;
            }

            public static AttackPose Ease(AttackPose from, AttackPose to, float progress)
            {
                float t = Mathf.SmoothStep(0, 1, progress);
                Vector2 shoulders = Vector2.LerpUnclamped(from.Shoulders, to.Shoulders, t);
                Vector2 elbows = Vector2.LerpUnclamped(from.Elbows, to.Elbows, t);
                return new AttackPose(shoulders.x, shoulders.y, elbows.x, elbows.y, Mathf.LerpUnclamped(from.Tuck, to.Tuck, t));
            }
        }

        static readonly AttackPose PalmDraw = new AttackPose(-16, -24, -12, 36);
        static readonly AttackPose PalmContact = new AttackPose(-10, 42, -6, 4);
        static readonly AttackPose PalmFollow = new AttackPose(6, 17, -3, -11);
        static readonly AttackPose PoundCrouch = new AttackPose(-12, 16, -15, 19);
        static readonly AttackPose PoundRaised = new AttackPose(-40, 40, -32, 38, 1);
        static readonly AttackPose PoundContact = new AttackPose(7, -6, -3, 4);
        public float Phase=>phase;
        public float Blend=>blend;
        public Mesh DeformedMesh=>mesh;
        public MeshRenderer SkinRenderer=>skin;
        public void Initialize(SpriteRenderer original,SlashArena world)
        {
            source=original; arena=world; previous=transform.position;
            original.forceRenderingOff=true;
            var root=new GameObject("Ape articulated walking skin"); root.transform.SetParent(original.transform,false);
            mesh=new Mesh {name="Ape continuous joints and rigid extremities",hideFlags=HideFlags.DontSave}; mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh=mesh; skin=root.AddComponent<MeshRenderer>();
            material=new Material(Resources.Load<Shader>("SlashDemo/ApeSkin")){hideFlags=HideFlags.DontSave}; material.mainTexture=original.sprite.texture;
            skin.sharedMaterial=material;
            skin.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; skin.receiveShadows=false;
            rest=new Vector3[RowWidth*(Grid+1)];posed=new Vector3[rest.Length];coordinates=new Vector2[rest.Length];weights=new Vector4[rest.Length];
            attackWeights = new Vector4[rest.Length];
            var triangles=new int[Grid*Grid*6];
            for(int limb=0;limb<4;limb++) pivots[limb]=ArtPoint(Joints[limb]);
            for (int arm = 0; arm < elbows.Length; arm++) elbows[arm] = ArtPoint(Elbows[arm]);
            int triangle = 0;
            for (int y = 0; y <= Grid; y++)
            {
                float height = y / (float)Grid;
                float seam = LeftArmSeam(height);
                for (int x = 0; x < RowWidth; x++)
                {
                    bool armSide = x <= SplitColumn;
                    float u = armSide ? Mathf.Lerp(0, seam, x / (float)SplitColumn)
                        : Mathf.Lerp(seam, 1, (x - SplitColumn - 1) / (float)(Grid - SplitColumn));
                    int i = y * RowWidth + x;
                    Vector2 uv = new Vector2(u, height);
                    coordinates[i] = uv; rest[i] = ArtPoint(uv); weights[i] = Weights(uv);
                    attackWeights[i] = AttackWeights(uv, armSide);
                    // The two seam vertices coincide at rest. In an attack the left fist
                    // can lift away from the adjacent shin without stretching triangles across it.
                    if (y == Grid || x == SplitColumn || x == RowWidth - 1) continue;
                    triangles[triangle++] = i;
                    triangles[triangle++] = i + RowWidth + 1;
                    triangles[triangle++] = i + 1;
                    triangles[triangle++] = i;
                    triangles[triangle++] = i + RowWidth;
                    triangles[triangle++] = i + RowWidth + 1;
                }
            }
            mesh.vertices=rest;mesh.uv=coordinates;mesh.triangles=triangles;
            Refresh();
        }
        static Vector4 Weights(Vector2 p)
        {
            float tail=(1-Mathf.SmoothStep(.29f,.38f,p.x))*Mathf.SmoothStep(.47f,.60f,p.y);
            float leftArm=(1-Mathf.SmoothStep(.29f,.42f,p.x))*(1-Mathf.SmoothStep(.52f,.72f,p.y))*Mathf.SmoothStep(.18f,.26f,p.y)*(1-tail);
            float rightArm=Mathf.SmoothStep(.75f,.84f,p.x)*(1-Mathf.SmoothStep(.55f,.74f,p.y));

            // Keep the soles rigid, with a gradual blend into the hips. The sloped edges
            // follow the gaps between the shins, hanging fists and loincloth in the sprite.
            float leftFistEdge = .295f + .53f * (p.y - .24f) - .47f * Mathf.Max(0, p.y - .30f);
            float leftShin = Mathf.Lerp(1, Ramp(leftFistEdge, leftFistEdge + .024f, p.x), Ramp(.213f, .238f, p.y));
            float leftLeg = leftShin * (1 - Ramp(.515f, .555f, p.x)) * (1 - Ramp(.27f, .48f, p.y));
            float clothEdge = Mathf.Lerp(.604f, .682f, Ramp(.17f, .30f, p.y));
            float rightFistEdge = RightFistEdge(p.y);
            float rightShin = Ramp(clothEdge, clothEdge + .024f, p.x) * (1 - Ramp(rightFistEdge, rightFistEdge + .015f, p.x));
            float rightLeg = rightShin * (1 - Ramp(.25f, .46f, p.y));
            float upperBody = 1 - leftLeg - rightLeg;
            return new Vector4(leftLeg, rightLeg, leftArm * upperBody, rightArm * upperBody);
        }

        static float RightFistEdge(float y) => Mathf.Max(.84f - 1.5f * Mathf.Max(0, y - .13f),
            .80f + .40f * (y - .20f) - .75f * Mathf.Max(0, y - .28f));

        static float LeftArmSeam(float y) => Mathf.Min(.328f, .292f + .66f * (Mathf.Max(.22f, y) - .23f))
            + .73f * Mathf.Max(0, y - .43f);

        static Vector4 AttackWeights(Vector2 uv, bool armSide)
        {
            // Keep fists with their forearms. Fade the rotations into the shoulders and away from the tail and legs.
            float leftInside = LeftArmSeam(uv.y);
            float edgeBlend = Mathf.Lerp(.014f, .055f, Ramp(.43f, .57f, uv.y));
            float tail = Ramp(.50f, .60f, uv.y) * (1 - Ramp(.32f, .40f, uv.x));
            float shoulderWeight = 1 - Ramp(leftInside - edgeBlend * .5f, leftInside + edgeBlend * .5f, uv.x);
            float left = Mathf.Lerp(armSide ? 1 : 0, shoulderWeight, Ramp(.40f, .52f, uv.y))
                * (1 - Ramp(.54f, .70f, uv.y)) * Ramp(.218f, .227f, uv.y) * (1 - tail);
            float rightInside = Mathf.Max(.835f - 1.5f * Mathf.Max(0, uv.y - .13f),
                .790f + .47f * (uv.y - .20f) - .83f * Mathf.Max(0, uv.y - .28f));
            float right = Ramp(rightInside, rightInside + edgeBlend, uv.x) * (1 - Ramp(.52f, .67f, uv.y));
            return new Vector4(left, right, 1 - Ramp(.43f, .55f, uv.y), 1 - Ramp(.39f, .50f, uv.y));
        }

        static float Ramp(float from, float to, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));
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
        public void SetAttackPose(ApeCombat.State state, float progress, bool poundRecovery = false)
        {
            const float jumpApex = .5f;
            AttackPose pose = state switch
            {
                ApeCombat.State.PalmWindup => AttackPose.Ease(default, PalmDraw, progress),
                ApeCombat.State.PalmSwing => progress < .5f
                    ? AttackPose.Ease(PalmDraw, PalmContact, progress * 2)
                    : AttackPose.Ease(PalmContact, PalmFollow, (progress - .5f) * 2),
                ApeCombat.State.PoundWindup => AttackPose.Ease(default, PoundCrouch, progress),
                ApeCombat.State.PoundAir => progress < jumpApex
                    ? AttackPose.Ease(PoundCrouch, PoundRaised, progress / jumpApex)
                    : AttackPose.Ease(PoundRaised, PoundContact, (progress - jumpApex) / (1 - jumpApex)),
                ApeCombat.State.Recovery => AttackPose.Ease(poundRecovery ? PoundContact : PalmFollow, default, progress),
                _ => default
            };
            tuck = pose.Tuck;
            posingAttack = pose.Shoulders != Vector2.zero || pose.Elbows != Vector2.zero;
            if (!posingAttack) return;
            for (int arm = 0; arm < elbows.Length; arm++)
            {
                shoulderTurns[arm] = UnitRotation(pose.Shoulders[arm]);
                elbowTurns[arm] = UnitRotation(pose.Elbows[arm]);
            }
        }

        static Vector2 UnitRotation(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        static Vector2 Rotate(Vector2 point, Vector2 rotation)
            => new Vector2(point.x * rotation.x - point.y * rotation.y, point.x * rotation.y + point.y * rotation.x);

        Vector2 ArmDelta(Vector2 point, int arm, float flex)
        {
            Vector2 elbow = elbows[arm], shoulder = pivots[arm + 2];
            Vector2 bent = elbow + Rotate(point - elbow, elbowTurns[arm]);
            Vector2 articulated = Vector2.LerpUnclamped(point, bent, flex);
            return shoulder + Rotate(articulated - shoulder, shoulderTurns[arm]) - point;
        }

        Vector3 AttackDelta(Vector2 point, Vector4 weight)
        {
            Vector2 delta = Vector2.zero;
            if (weight.x > 0) delta += ArmDelta(point, 0, weight.z) * weight.x;
            if (weight.y > 0) delta += ArmDelta(point, 1, weight.w) * weight.y;
            if (source.flipX) delta.x = -delta.x;
            return delta;
        }

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
                float angle = step.x / Reach * (leg ? 10 : 19) * blend;
                if (leg) angle += (limb == 0 ? -12 : 15) * tuck - step.y / Lift * 6 * blend;
                angles[limb]=angle*Mathf.Deg2Rad;
                rotations[limb]=new Vector2(Mathf.Cos(angles[limb]),Mathf.Sin(angles[limb]));
                if(leg)
                {
                    Vector2 end=ArtPoint(Ends[limb])-pivots[limb];
                    offsets[limb]=(heading*step.x+Vector2.up*step.y)*blend-(Rotate(end,angles[limb])-end)-Vector2.up*bob;
                    offsets[limb] += new Vector2(limb == 0 ? .14f : -.10f, limb == 0 ? .25f : .34f) * tuck;
                }
                else offsets[limb]=heading*(step.x*.10f*blend);
            }
            for(int i=0;i<rest.Length;i++)
            {
                posed[i]=SkinPoint(rest[i],weights[i]);
                if (posingAttack) posed[i] += AttackDelta(rest[i], attackWeights[i]);
            }
            material.SetColor("_Color",source.color);
            mesh.vertices=posed;mesh.RecalculateBounds();skin.sortingLayerID=source.sortingLayerID;skin.sortingOrder=source.sortingOrder;skin.enabled=source.enabled;
        }
        void LateUpdate()
        {
            if(source==null)return;if(enemy==null)enemy=GetComponent<SlashEnemy>();Vector2 now=transform.position;
            if(!arena.Paused&&enemy.Alive)Advance(now-previous,Time.deltaTime,enemy.IsChasing);
            previous=now;Refresh();
        }
        void OnEnable(){if(source!=null)source.forceRenderingOff=true;previous=transform.position;}
        void OnDisable(){if(source!=null)source.forceRenderingOff=false;if(skin!=null)skin.enabled=false;}
        void OnDestroy(){if(source!=null)source.forceRenderingOff=false;if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
