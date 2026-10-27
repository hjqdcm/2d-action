using System;
using System.IO;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class HealingLodgeValidation
    {
        public static void Run(SlashArena arena)
        {
            var map = arena.Travel.ActiveMap;
            if (map.Definition.entry != new Vector2(1050, 600)
                || Vector2.Distance(arena.Player.transform.position, ManorEnvironment.Ground(1050, 600)) > 0.001f)
                throw new Exception("Healing lodge did not spawn at annotated point 1.");
            float radius = arena.Player.GetComponent<CircleCollider2D>().radius;
            Route(map, radius, new[] { new Vector2(1050,600), new Vector2(930,520), new Vector2(925,460),
                new Vector2(900,400), new Vector2(800,360), new Vector2(650,320),
                new Vector2(600,315), new Vector2(550,310) });
            Route(map, radius, new[] { new Vector2(1050,600), new Vector2(960,740),
                new Vector2(930,840), new Vector2(960,900), new Vector2(1010,1000) });
            Route(map, radius, new[] { new Vector2(1050,600), new Vector2(1170,740),
                new Vector2(1235,815), new Vector2(1315,910), new Vector2(1400,950) });
            foreach (var barrier in new[] {
                (700f,680f,600f,680f), (600f,430f,600f,530f), (620f,380f,500f,360f),
                (820f,260f,730f,240f), (850f,270f,850f,180f), (1200f,570f,1330f,500f) })
            {
                var a = ManorEnvironment.Ground(barrier.Item1, barrier.Item2);
                var b = ManorEnvironment.Ground(barrier.Item3, barrier.Item4);
                if (!HitsMap(map, radius, a, b)) throw new Exception("Healing lodge wall/furniture does not block: " + barrier);
            }

            var roof = map.transform.Find("Gate roof - overhead occlusion").GetComponent<SpriteRenderer>();
            if (roof.GetComponent<Collider2D>() != null || roof.sprite.vertices.Length <= 4)
                throw new Exception("Gate roof must use a silhouette without blocking the passage.");
            var original = arena.Player.transform.position;
            float zoom = arena.WorldCamera.orthographicSize;
            try
            {
                foreach (var test in new[] { (1215f,755f,true), (1235f,780f,true), (1320f,900f,false), (1050f,600f,false) })
                {
                    arena.Player.Teleport(ManorEnvironment.Ground(test.Item1, test.Item2));
                    arena.Player.GetComponent<ObliqueActor>().SetPose(Vector2.zero, 0, 0);
                    arena.CameraRig.ResetFeedback();
                    arena.WorldCamera.orthographicSize = 2.8f;
                    Physics2D.SyncTransforms();
                    var mask = ManorOcclusionValidation.CaptureActor(arena);
                    var covered = ManorOcclusionValidation.Capture(arena.WorldCamera);
                    roof.enabled = false;
                    var uncovered = ManorOcclusionValidation.Capture(arena.WorldCamera);
                    roof.enabled = true;
                    int difference = ManorOcclusionValidation.Difference(covered, uncovered, mask);
                    Directory.CreateDirectory("Verification/Artifacts");
                    File.WriteAllBytes("Verification/Artifacts/HealingLodge-roof-" + test.Item1 + ".png", covered.EncodeToPNG());
                    if (test.Item3)
                        File.WriteAllBytes("Verification/Artifacts/HealingLodge-roof-" + test.Item1 + "-before.png", uncovered.EncodeToPNG());
                    UnityEngine.Object.Destroy(mask); UnityEngine.Object.Destroy(covered); UnityEngine.Object.Destroy(uncovered);
                    Debug.Log("HEALING_LODGE_ROOF_CASE: " + test + "; covered actor pixels=" + difference);
                    if (test.Item3 ? difference < 20 : difference > 3)
                        throw new Exception("Incorrect gate roof coverage: " + test + "; pixels=" + difference);
                }
                Debug.Log("HEALING_LODGE_PASS: point 1 spawn, bed passage, room doorway, stairs and gate routes; six blocking edges; two overhead and two clear render cases.");
            }
            finally
            {
                roof.enabled = true;
                arena.Player.Teleport(original);
                arena.Player.GetComponent<ObliqueActor>().SetPose(Vector2.zero, 0, 0);
                arena.CameraRig.ResetFeedback();
                arena.WorldCamera.orthographicSize = zoom;
                Physics2D.SyncTransforms();
            }
        }

        static bool HitsMap(TravelMapEnvironment map, float radius, Vector2 a, Vector2 b)
        {
            foreach (var hit in Physics2D.CircleCastAll(a, radius, b - a, (b - a).magnitude))
                if (!hit.collider.isTrigger && hit.transform.IsChildOf(map.transform)) return true;
            return false;
        }

        static void Route(TravelMapEnvironment map, float radius, Vector2[] reference)
        {
            for (int i = 0; i < reference.Length; i++)
            {
                Vector2 point = ManorEnvironment.Ground(reference[i].x, reference[i].y);
                foreach (var collider in Physics2D.OverlapCircleAll(point, radius))
                    if (!collider.isTrigger && collider.transform.IsChildOf(map.transform))
                        throw new Exception("Healing lodge route point blocked: " + reference[i]);
                if (i > 0 && HitsMap(map, radius, ManorEnvironment.Ground(reference[i - 1].x, reference[i - 1].y), point))
                    throw new Exception("Healing lodge route blocked between " + reference[i - 1] + " and " + reference[i]);
            }
        }
    }
}
