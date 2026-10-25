using System;
using SlashDemo;
using UnityEngine;

static class ChecklistChecks
{
    static int count;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        count++;
    }
    static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
    static void Main()
    {
        var waist = new Vector2(0, 0.52f);
        foreach (var strike in SwordMotion.Defaults())
        {
            for (int frame = 0; frame <= 100; frame++)
            {
                var pose = SwordMotion.Evaluate(strike, frame / 100f);
                float shear = Mathf.Clamp(pose.lean * 0.4f, -6, 6);
                float squash = Mathf.Clamp01(Mathf.Abs(pose.lean) / 14) * 0.04f;
                for (int y = 0; y <= 20; y++)
                    for (int x = 0; x <= 10; x++)
                    {
                        Vector2 uv = new Vector2(x / 10f, y / 20f);
                        Vector3 rest = new Vector3(uv.x - 0.5f, uv.y, 0);
                        var baseline = HeroLayerMath.Deform(rest, uv, waist, 0, 0, 0, 0);
                        var weighted = HeroLayerMath.Deform(rest, uv, waist, 0, 0, 0, 0, shear, squash);
                        Check(Finite(weighted), "finite deformation throughout every cut");
                        Check(Mathf.Abs(weighted.y - baseline.y * (1 - squash)) < 0.000001f,
                            "compression remains within the intended four percent");
                        if (y == 0) Check(Vector3.Distance(rest, weighted) < 0.000001f, "foot contact stays fixed");
                        if (frame == 100) Check(Vector3.Distance(rest, weighted) < 0.000001f, "recovery restores the exact standing silhouette");
                    }
            }
            var end = SwordMotion.Evaluate(strike, 1);
            Check(Mathf.Abs(end.lean) < 0.000001f, "no attack deformation leaks into locomotion");
            foreach (Vector2 facing in new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down })
            {
                Vector2 target = facing * 1.2f;
                Check(SwordMotion.SweptHit(strike, 0, 1, Vector2.zero, Vector2.zero, facing, target, 0.2f, out _),
                    "forward hit detection remains independent of presentation deformation");
                Check(!SwordMotion.SweptHit(strike, 0, 1, Vector2.zero, Vector2.zero, facing, -target, 0.2f, out _),
                    "rear targets remain excluded");
            }
        }
        var head = new Vector3(0, 1, 0);
        Check(HeroLayerMath.Deform(head, new Vector2(0.5f, 1), waist, 0, 0, 0, 0, 4, 0.04f).x > head.x,
            "an attack changes the silhouette in the shear direction");
        Console.WriteLine("CHECKLIST_MATH_PASS: " + count + " assertions");
    }
}
