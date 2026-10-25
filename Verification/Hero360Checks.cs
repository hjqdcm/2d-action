using System;
using System.Reflection;
using System.Collections.Generic;
using SlashDemo;
using UnityEngine;

static class Hero360Checks
{
    static int count;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
    static void Main()
    {
        foreach (string shapeName in new[] { "sphere", "smallSphere", "cylinder", "skirt", "torso", "head", "hairCap", "armSleeve", "boot", "hand", "collarLeft", "collarRight", "blade", "coatUpper", "coatPanels", "frontLocks", "backLocks" })
        {
            object shape = typeof(Hero360Model).GetField(shapeName, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (shape is Array shapes) { int partIndex = 0; foreach (object entry in shapes) CheckShape(entry, shapeName + "[" + partIndex++ + "]"); }
            else CheckShape(shape, shapeName);
            int faceCount = 0, vertexCount = 0;
            foreach (object part in shape is Array pieces ? pieces : new[] { shape })
            {
                faceCount += ((int[])part.GetType().GetField("indices").GetValue(part)).Length / 3;
                vertexCount += ((Vector3[])part.GetType().GetField("points").GetValue(part)).Length;
            }
            Console.WriteLine("HERO360_PART: " + shapeName + " vertices=" + vertexCount + " triangles=" + faceCount);
            if (shapeName == "head") Check(faceCount >= 6000, "integrated head surface has enough geometry to sculpt facial features");
            if (shapeName == "torso") Check(faceCount >= 1000, "torso has dense axial and radial subdivisions");
            if (shapeName == "armSleeve") Check(faceCount >= 2000, "each continuous sleeve has enough rings to bend across its elbow");
            if (shapeName == "frontLocks" || shapeName == "backLocks") Check(faceCount >= 4000, "hair strands contain smooth longitudinal subdivisions");
        }
        var frameType = typeof(Hero360Model).GetNestedType("FrameState", BindingFlags.NonPublic);
        object Frame()
        {
            object value = Activator.CreateInstance(frameType);
            frameType.GetField("rotation").SetValue(value, new Quaternion(0, 0, 0, 1));
            return value;
        }
        object baseline = Frame();
        bool Matches(object a, object b) => (bool)frameType.GetMethod("Matches").Invoke(a, new[] { b });
        Check(Matches(baseline, Frame()), "unchanged idle geometry can be reused");
        foreach (string field in new[] { "right", "left", "grip", "tip", "socket", "caseAxis" })
        {
            object moved = Frame(); frameType.GetField(field).SetValue(moved, Vector3.right * 0.001f);
            Check(!Matches(baseline, moved), "moving " + field + " invalidates cached geometry");
        }
        foreach (string field in new[] { "deformation", "appearance" })
        {
            object changed = Frame(); frameType.GetField(field).SetValue(changed, new Vector4(0.01f, 0, 0, 0));
            Check(!Matches(baseline, changed), "animation and charge changes invalidate cached geometry");
        }
        object turned = Frame(); frameType.GetField("rotation").SetValue(turned, new Quaternion(0, Mathf.Sin(Mathf.Deg2Rad), 0, Mathf.Cos(Mathf.Deg2Rad)));
        Check(!Matches(baseline, turned), "turning rebuilds view dependent triangle order");
        object extracted = Frame(); frameType.GetField("caseVisible").SetValue(extracted, true);
        Check(!Matches(baseline, extracted), "sword extraction rebuilds the scabbard visibility");
        int sleevePose = 0;
        foreach (Vector3[] path in new[] {
            new[] { new Vector3(0.15f,0.935f,0), new Vector3(0.20f,0.85f,0.015f), new Vector3(0.20f,0.73f,0.01f), new Vector3(0.20f,0.6f,0.005f), new Vector3(0.20f,0.54f,0) },
            new[] { new Vector3(0.15f,0.935f,0), new Vector3(0.23f,0.94f,0.06f), new Vector3(0.28f,0.87f,0.18f), new Vector3(0.31f,0.91f,0.30f), new Vector3(0.33f,0.95f,0.36f) },
            new[] { new Vector3(0.15f,0.935f,0), new Vector3(0.22f,1f,0.03f), new Vector3(0.27f,1.08f,0.10f), new Vector3(0.25f,1.19f,0.14f), new Vector3(0.21f,1.24f,0.15f) },
            new[] { new Vector3(0.15f,0.935f,0), new Vector3(0.26f,0.86f,0.02f), new Vector3(0.3f,0.81f,0.12f), new Vector3(0.2f,0.86f,0.23f), new Vector3(0.06f,0.9f,0.28f) }
        }) CheckBentSleeve(path, sleevePose++);
        for (int elevation = 30; elevation <= 65; elevation += 5)
        {
            Vector3 up = ObliqueProjection.Up(elevation);
            Vector3 previous = Hero360Model.FacingForward(DemoArt.Direction(-1), elevation);
            for (int angle = 0; angle <= 360; angle++)
            {
                Vector2 aim = DemoArt.Direction(angle);
                Vector3 forward = Hero360Model.FacingForward(aim, elevation);
                Check(Mathf.Abs(forward.magnitude - 1) < 0.00001f, "every orientation has a unit forward vector");
                Check(Mathf.Abs(Vector3.Dot(forward, up)) < 0.00001f, "body turns around the standing up axis");
                Check(Vector3.Dot(forward, (Vector3)aim) > 0.4f, "front of the body always points toward aim");
                Check(Vector3.Dot(forward, previous) > 0.998f && Vector3.Distance(forward, previous) > 0.001f,
                    "one degree aim changes produce continuous distinct body directions without eight way plateaus");
                Check(Vector3.Distance(forward, -Hero360Model.FacingForward(-aim, elevation)) < 0.00001f,
                    "opposite aims show opposite sides of the complete 3D model");
                Vector3 right = Vector3.Cross(up, forward);
                Check(Mathf.Abs(right.magnitude - 1) < 0.00001f && Mathf.Abs(Vector3.Dot(right, forward)) < 0.00001f,
                    "anatomical right stays attached to the same shoulder throughout a full turn");
                previous = forward;
            }
            Check(Vector3.Distance(Hero360Model.FacingForward(Vector2.zero, elevation),
                Hero360Model.FacingForward(Vector2.down, elevation)) < 0.00001f,
                "zero aim has a finite front facing fallback");
        }
        Console.WriteLine("HERO360_MATH_PASS: " + count + " assertions (geometry and continuous orientation math, not a render test)");
    }
    static void CheckBentSleeve(Vector3[] path, int pose)
    {
        // Invoke the actual mesh deformation without constructing native Unity scene objects.
        object model = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Hero360Model));
        foreach (var field in typeof(Hero360Model).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                field.SetValue(model, Activator.CreateInstance(field.FieldType));
            else if (field.Name == "sleeveNormalSums")
            {
                object template = typeof(Hero360Model).GetField("armSleeve", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                int count = ((Vector3[])template.GetType().GetField("points").GetValue(template)).Length;
                field.SetValue(model, new Vector3[count]);
            }
        typeof(Hero360Model).GetMethod("AppendSleeve", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(model, new object[] { path });
        object Value(string field) => typeof(Hero360Model).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
        var points = ((List<Vector3>)Value("vertices")).ToArray();
        var normals = ((List<Vector3>)Value("normals")).ToArray();
        var uv = ((List<Vector2>)Value("detailUV")).ToArray();
        var indices = new List<int>();
        foreach (object tri in (System.Collections.IEnumerable)Value("triangles"))
            foreach (string field in new[] { "a", "b", "c" }) indices.Add((int)tri.GetType().GetField(field).GetValue(tri));
        var shapeType = typeof(Hero360Model).GetNestedType("Shape", BindingFlags.NonPublic);
        object shape = Activator.CreateInstance(shapeType);
        shapeType.GetField("points").SetValue(shape, points); shapeType.GetField("normals").SetValue(shape, normals);
        // Atlas coordinates live inside opaque subrectangles of the original painting.
        shapeType.GetField("uv").SetValue(shape, uv); shapeType.GetField("indices").SetValue(shape, indices.ToArray());
        CheckShape(shape, "posed continuous sleeve " + pose);
    }
    static void CheckShape(object shape, string label)
    {
            var type = shape.GetType();
            var vertices = (Vector3[])type.GetField("points").GetValue(shape);
            var normals = (Vector3[])type.GetField("normals").GetValue(shape);
            var indices = (int[])type.GetField("indices").GetValue(shape);
            var uv = (Vector2[])type.GetField("uv").GetValue(shape);
            Check(vertices.Length > 20 && indices.Length > 60 && indices.Length % 3 == 0, "3D primitive has complete triangle geometry");
            Check(normals.Length == vertices.Length, "all 3D primitive vertices have shading normals");
            Check(uv != null && uv.Length == vertices.Length, "all detail geometry has texture coordinates");
            float volume = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                Check(!float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                    && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z), "primitive vertices are finite");
                Check(Mathf.Abs(normals[i].magnitude - 1) < 0.0001f, "primitive normals have unit length");
                Check(uv[i].x >= 0 && uv[i].x <= 1 && uv[i].y >= 0 && uv[i].y <= 1, "fabric and hair UVs stay within their atlas patches");
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                Check(a >= 0 && b >= 0 && c >= 0 && a < vertices.Length && b < vertices.Length && c < vertices.Length,
                    "3D triangles reference existing vertices");
                Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (normal.sqrMagnitude > 0.00000000000001f)
                    Check(Vector3.Dot(normal, normals[a] + normals[b] + normals[c]) > 0,
                        label + " triangle " + (i / 3) + " winding must agree with outward normals");
                volume += Vector3.Dot(vertices[a], Vector3.Cross(vertices[b], vertices[c])) / 6;
            }
            Check(volume > 0.000001f, "body and weapon primitives enclose real positive 3D volume");
            // Caps use separate normal vertices; weld coincident positions only for this connectivity audit.
            var positionRoots = new Dictionary<(int, int, int), int>();
            var parent = new int[vertices.Length];
            int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
            void Join(int a, int b) { a = Root(a); b = Root(b); if (a != b) parent[b] = a; }
            for (int i = 0; i < vertices.Length; i++) parent[i] = i;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                var key = (Mathf.RoundToInt(v.x * 1000000), Mathf.RoundToInt(v.y * 1000000), Mathf.RoundToInt(v.z * 1000000));
                if (positionRoots.TryGetValue(key, out int other)) Join(i, other); else positionRoots.Add(key, i);
            }
            for (int i = 0; i < indices.Length; i += 3) { Join(indices[i], indices[i + 1]); Join(indices[i], indices[i + 2]); }
            int connectedRoot = Root(0);
            for (int i = 1; i < vertices.Length; i++) Check(Root(i) == connectedRoot, label + " is a connected surface instead of detached primitive pieces");
    }
}
