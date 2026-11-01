using UnityEngine;

namespace SlashDemo
{
    [DefaultExecutionOrder(75)]
    [DisallowMultipleComponent]
    public sealed class SnakeAnimation : MonoBehaviour
    {
        const int Grid = 40;
        public const int PartCount = 3;
        const int PartVertices = (Grid + 1) * (Grid + 1);
        public const float CycleDistance = 1.6f;
        SpriteRenderer source;
        SlashArena arena;
        SlashEnemy enemy;
        Mesh mesh;
        MeshRenderer rendererMesh;
        Material material;
        MaterialPropertyBlock properties;
        Vector3[] rest, posed;
        Vector2[] uv;
        Vector2 previousPosition, aim = Vector2.right;
        float phase, blend, speed, coil, reach;
        public float Phase => phase;
        public float Blend => blend;
        public Mesh DeformedMesh => mesh;
        public MeshRenderer SkinRenderer => rendererMesh;

        public void Initialize(SpriteRenderer skin, SlashArena owner)
        {
            source = skin; arena = owner; previousPosition = transform.position;
            var child = new GameObject("Articulated serpent animation");
            child.transform.SetParent(source.transform, false);
            mesh = new Mesh { name = "Snake overlapping coil, neck and tail", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            rendererMesh = child.AddComponent<MeshRenderer>();
            material = new Material(Resources.Load<Shader>("SlashDemo/SnakeSkin")) { hideFlags = HideFlags.DontSave };
            material.mainTexture = source.sprite.texture;
            rendererMesh.sharedMaterials = new[] { material, material, material };
            rendererMesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rendererMesh.receiveShadows = false;
            properties = new MaterialPropertyBlock();
            rest = new Vector3[PartVertices * PartCount]; posed = new Vector3[rest.Length]; uv = new Vector2[rest.Length];
            var triangles = new int[Grid * Grid * 6];
            var bounds = source.sprite.bounds;
            for (int part = 0; part < PartCount; part++)
            for (int y = 0; y <= Grid; y++) for (int x = 0; x <= Grid; x++)
            {
                int i = part * PartVertices + y * (Grid + 1) + x;
                uv[i] = new Vector2(x / (float)Grid, y / (float)Grid);
                rest[i] = new Vector3(bounds.min.x + bounds.size.x * uv[i].x, bounds.min.y + bounds.size.y * uv[i].y, 0);
                if (x == Grid || y == Grid) continue;
                int t = (y * Grid + x) * 6;
                if (part != 0) continue;
                triangles[t] = i; triangles[t + 1] = i + Grid + 2; triangles[t + 2] = i + 1;
                triangles[t + 3] = i; triangles[t + 4] = i + Grid + 1; triangles[t + 5] = i + Grid + 2;
            }
            mesh.vertices = rest; mesh.uv = uv; mesh.subMeshCount = PartCount;
            for (int part=0;part<PartCount;part++)
            {
                var indices = new int[triangles.Length];
                for(int i=0;i<indices.Length;i++) indices[i]=triangles[i]+part*PartVertices;
                mesh.SetTriangles(indices,part,false);
            }
            source.forceRenderingOff = true; Refresh();
        }

        public void SetAttackPose(float compression, float extension, Vector2 direction)
        { coil = Mathf.Clamp01(compression); reach = Mathf.Clamp01(extension); aim = direction; }

        public void Advance(Vector2 displacement, float dt, bool walking)
        {
            if (dt <= 0) return;
            float travel = displacement.magnitude;
            if (travel > 1.5f) travel = 0; // Ignore teleports.
            speed = Mathf.Lerp(speed, walking ? travel / dt : 0, 1 - Mathf.Exp(-14 * dt));
            blend = Mathf.MoveTowards(blend, walking ? Mathf.Clamp01(speed / .6f) : 0, dt * 6);
            if (walking) phase = Mathf.Repeat(phase + travel / (CycleDistance * Mathf.Max(.1f, arena.View.characterScale)), 1);
        }

        Vector2 ArtPoint(float x,float y)
        {
            var bounds=source.sprite.bounds;
            return new Vector2(bounds.min.x+bounds.size.x*x,bounds.min.y+bounds.size.y*y);
        }
        public Vector3 PartPoint(int part, float x, float y)
            => posed[part*PartVertices+Mathf.RoundToInt(y*Grid)*(Grid+1)+Mathf.RoundToInt(x*Grid)];

        public static float TailMotionWeight(Vector2 point)
        {
            // A radial rotation transition stays continuous and cannot shear the
            // free tail. The painted root is inside the stationary inner disk.
            float radius=Vector2.Distance(point,new Vector2(.91f,.365f));
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.18f,radius));
        }

        public void Refresh()
        {
            if (mesh == null || source == null) return;
            var bounds = source.sprite.bounds;
            Vector2 pivot = new Vector2(bounds.min.x + bounds.size.x * .52f, bounds.min.y + bounds.size.y * .64f);
            Vector2 strike = ObliqueProjection.ProjectGround(aim, arena.View.elevation);
            if (source.flipX) strike.x = -strike.x;
            float cycle = phase * Mathf.PI * 2;
            // Locomotion preserves each painted section. Only the attack bends the neck.
            float angle = (8 * coil - 9 * reach) * Mathf.Deg2Rad;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            Vector2 headOffset = -strike * (.23f * coil) + Vector2.up * (.08f * coil) + strike * (.78f * reach);
            float walking = blend * (1 - Mathf.Max(coil, reach));
            var pivots = new[] { ArtPoint(.5f,.13f), ArtPoint(.57f,.445f), ArtPoint(.91f,.365f) };
            var angles = new[] { Mathf.Sin(cycle-1.1f)*1.2f, Mathf.Sin(cycle)*5.5f, Mathf.Sin(cycle-1.7f)*10f };
            var shifts = new[] { new Vector2(Mathf.Sin(cycle-1.1f)*.025f,0),
                new Vector2(-Mathf.Sin(cycle)*.035f,Mathf.Cos(cycle)*.012f), Vector2.zero };
            var rotations = new Vector2[PartCount];
            for(int part=0;part<PartCount;part++)
            {
                float turn=angles[part]*walking*Mathf.Deg2Rad;
                rotations[part]=new Vector2(Mathf.Cos(turn),Mathf.Sin(turn));
            }
            for (int i = 0; i < posed.Length; i++)
            {
                Vector2 p = rest[i]; float y = uv[i].y;
                float grounded = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.035f, .18f, y));
                float neck = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, .68f, y));
                float lower = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .65f, y));
                p.x += (p.x - pivot.x) * .035f * coil * lower * grounded;
                p.y -= .055f * coil * lower * grounded;
                Vector2 d = (Vector2)rest[i] - pivot;
                Vector2 rigid = pivot + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c) + headOffset;
                p += (rigid - (Vector2)rest[i]) * neck;
                int part = i / PartVertices;
                if (part == 2)
                {
                    // The cut edge follows the coil exactly. Only the small root
                    // transitions to the tail's own swing, without a second painted layer.
                    Vector2 bodyLocal=p-pivots[0];
                    Vector2 bodyPoint=pivots[0]+new Vector2(bodyLocal.x*rotations[0].x-bodyLocal.y*rotations[0].y,
                        bodyLocal.x*rotations[0].y+bodyLocal.y*rotations[0].x)+shifts[0]*walking;
                    Vector2 rootLocal=pivots[2]-pivots[0];
                    Vector2 root=pivots[0]+new Vector2(rootLocal.x*rotations[0].x-rootLocal.y*rotations[0].y,
                        rootLocal.x*rotations[0].y+rootLocal.y*rotations[0].x)+shifts[0]*walking;
                    float turn=(angles[2]-angles[0])*walking*TailMotionWeight(uv[i])*Mathf.Deg2Rad;
                    float tailC=Mathf.Cos(turn),tailS=Mathf.Sin(turn);
                    Vector2 tailLocal=bodyPoint-root;
                    p=root+new Vector2(tailLocal.x*tailC-tailLocal.y*tailS,tailLocal.x*tailS+tailLocal.y*tailC);
                    if(source.flipX) p.x=-p.x;
                    posed[i]=p;
                    continue;
                }
                float turnC = rotations[part].x, turnS = rotations[part].y;
                Vector2 local = p - pivots[part];
                p = pivots[part] + new Vector2(local.x*turnC-local.y*turnS, local.x*turnS+local.y*turnC) + shifts[part]*walking;
                if (source.flipX) p.x = -p.x;
                posed[i] = p;
            }
            mesh.vertices = posed; mesh.RecalculateBounds();
            for(int part=0;part<PartCount;part++)
            {
                properties.Clear(); properties.SetColor("_Color",source.color); properties.SetFloat("_PartIndex",part);
                rendererMesh.SetPropertyBlock(properties,part);
            }
            rendererMesh.sortingLayerID = source.sortingLayerID; rendererMesh.sortingOrder = source.sortingOrder;
            rendererMesh.enabled = source.enabled;
        }

        void LateUpdate()
        {
            if (source == null) return;
            if (enemy == null) enemy = GetComponent<SlashEnemy>();
            Vector2 now = transform.position;
            if (!arena.Paused && enemy != null && enemy.Alive) Advance(now - previousPosition, Time.deltaTime, enemy.IsChasing);
            previousPosition = now; Refresh();
        }
        void OnEnable() { previousPosition = transform.position; if (source != null) source.forceRenderingOff = true; }
        void OnDisable()
        { if (source != null) source.forceRenderingOff = false; if (rendererMesh != null) rendererMesh.enabled = false; }
        void OnDestroy()
        {
            if (source != null) source.forceRenderingOff = false;
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
