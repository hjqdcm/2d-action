using System;
using SlashDemo;
using UnityEngine;

static class HeavyMotionChecks
{
    static int count;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.00001f;
    static void Main()
    {
        var clock = new HeavyReleaseState();
        clock.Begin(0.25f);
        Check(!clock.Advance(0.032f) && clock.SweepProgress == 0, "release pauses before any sword sweep");
        float age = clock.Age;
        Check(!clock.Advance(0) && clock.Age == age, "freeze cannot advance release or emit waves");
        Check(!clock.Advance(0.05f), "wave does not emit at release or early sweep");
        Check(clock.Advance(0.05f), "wave emits only when the sword crosses forward");
        Check(!clock.Advance(0.01f), "crossing threshold cannot emit twice");
        clock.Cancel();
        Check(!clock.Active && !clock.Advance(5), "dash cancels pending sweep and recovery");
        clock.Begin(0.25f); clock.Advance(0.02f); clock.Cancel();
        Check(!clock.Advance(1), "dash during release pause prevents the future wave");
        clock.Begin(0.25f);
        Check(clock.Advance(2) && !clock.Active, "long frames emit once and complete the whole action");
        Check(!clock.Advance(1), "completed action cannot fire again");
        clock.Begin(0); Check(clock.Advance(1) && !clock.Active, "zero recovery still preserves pause and sweep");
        var load = SwordMotion.HeavyPose(1, false, 0, 0, 0);
        Check(Near(load.angle, 100) && Near(load.torsoShear, -0.16f) && Near(load.bodyOffset.x, -0.06f)
            && Near(load.sink, -0.10f), "charge coils sword and torso behind the shoulder with weight pressed down");
        var forward = SwordMotion.HeavyPose(1, true, 0.55f, 0, 0);
        Check(Near(forward.angle, 0), "sword points exactly forward at the wave emission time");
        var finish = SwordMotion.HeavyPose(1, true, 1, 0, 0);
        Check(Near(finish.angle, -90) && Near(finish.torsoShear, 0.22f) && Near(finish.sink, -0.18f),
            "heavy finishes its 190 degree sweep while body follows through");
        var rest = SwordMotion.HeavyPose(1, true, 1, 1, 0);
        Check(Near(rest.angle, -25) && Near(rest.torsoShear, 0) && Near(rest.sink, 0)
            && rest.bodyOffset == Vector2.zero, "recovery returns exactly to rest");
        Check(Near(SwordMotion.HeavySweep(0.2f), 0) && SwordMotion.HeavySweep(0.75f) > 0.95f,
            "hips start first and over ninety five percent of sword travel occurs in the fast middle");
        float last = 0;
        for (int i = 0; i <= 200; i++)
        {
            float s = i / 200f;
            float curve = SwordMotion.HeavySweep(s);
            Check(curve >= last && curve <= 1, "sword sweep is monotonic without mid-swing reversal");
            last = curve;
            var p = SwordMotion.HeavyPose(1, true, s, 0, 0);
            Check(p.kinetic && p.headLock && !float.IsNaN(p.tip.x) && p.sink <= -0.099f,
                "sweep retains head stabilization and grounded weight");
            var charge = SwordMotion.HeavyPose(1, false, 0, 0, s);
            Check(Mathf.Abs(charge.angle - 100) <= 0.50001f && charge.bodyOffset == load.bodyOffset
                && charge.torsoShear == load.torsoShear, "full charge only trembles the sword by half a degree while torso stays still");
        }
        var head = new Vector3(0, 1, 0);
        var locked = HeroLayerMath.KineticDeform(head, new Vector2(0.5f, 1), new Vector2(0, 0.52f),
            Vector2.zero, 0.22f, 0.20f, 14, 14, 0, true);
        var lowerHead = new Vector3(0, 0.80f, 0);
        var lockedLower = HeroLayerMath.KineticDeform(lowerHead, new Vector2(0.5f, 0.80f), new Vector2(0, 0.52f),
            Vector2.zero, 0.22f, 0.20f, 14, 14, 0, true);
        Check(Vector3.Distance(locked - lockedLower, head - lowerHead) < 0.000001f,
            "head follows the neck as a rigid piece without tilting or stretching");
        foreach (float direction in new[] { -1f, 1f })
            for (int frame = 0; frame <= 100; frame++)
            {
                foreach (bool release in new[] { false, true })
                {
                    var pose = SwordMotion.HeavyPose(frame / 100f, release, frame / 100f, 0, 0);
                    Vector2 offset = new Vector2(pose.bodyOffset.x, pose.sink);
                    Vector3 shift = HeroLayerMath.KineticDeform(head, new Vector2(0.5f, 1), new Vector2(0, 0.52f),
                        offset, pose.torsoShear * direction, pose.shoulderShear * direction,
                        pose.torsoTurn * direction, pose.shoulderTurn * direction, 0.03f, true) - head;
                    foreach (float y in new[] { 0.78f, 0.82f, 0.9f, 1f })
                        foreach (float x in new[] { 0.2f, 0.5f, 0.8f })
                        {
                            var point = new Vector3(x - 0.5f, y, 0);
                            var result = HeroLayerMath.KineticDeform(point, new Vector2(x, y), new Vector2(0, 0.52f),
                                offset, pose.torsoShear * direction, pose.shoulderShear * direction,
                                pose.torsoTurn * direction, pose.shoulderTurn * direction, 0.03f, true);
                            Check(Vector3.Distance(result - point, shift) < 0.000001f,
                                "entire face and skull preserve proportions through charge and swing in either direction");
                        }
                }
            }
        float traveled = 0, previousStep = 0;
        for (int i = 0; i <= 20; i++)
        {
            float distance = SwordMotion.WaveTravel(i * 0.01f, 12, 6);
            Check(distance >= traveled && distance <= 6, "accelerating wave always moves forward within its configured range");
            if (i > 0)
            {
                float step = distance - traveled;
                Check(step + 0.00001f >= previousStep, "wave speed rises throughout the first twenty percent of distance");
                previousStep = step;
            }
            traveled = distance;
        }
        Check(Near(traveled, 1.2f) && SwordMotion.WaveTravel(10, 12, 6) == 6,
            "acceleration spans first twenty percent and final travel still stops exactly at range");
        Console.WriteLine("HEAVY_MOTION_PASS: " + count + " assertions");
    }
}
