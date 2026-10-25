using System;
using SlashDemo;
using UnityEngine;

static class SingleHandChecks
{
    static int count;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
    static void Main()
    {
        for (int combo = 0; combo < 3; combo++)
        {
            var strike = SwordMotion.Defaults()[combo];
            for (int frame = 0; frame <= 40; frame++)
            {
                float t = frame / 40f;
                float w = HeroArmMath.SingleHandWeight(true, combo, false, true, t, strike.windupRatio);
                Check(w >= 0 && w <= 1, "single hand blend stays bounded throughout the cut");
                if (combo == 2 || frame == 0 || frame == 40)
                    Check(w == 0, "third cut remains two handed and transitions join at the shared grip");
                else if (t >= strike.windupRatio && t <= 0.85f)
                    Check(w == 1, "first two active cuts fully detach the support hand");
                Check(HeroArmMath.SingleHandWeight(true, combo, true, true, t, strike.windupRatio) == 0,
                    "heavy charge, sweep and recovery retain both hands on the sword");
                Check(HeroArmMath.SingleHandWeight(true, combo, false, false, t, strike.windupRatio) == 0,
                    "drawing and sheathing keep the support hand on the scabbard");
                Check(HeroArmMath.SingleHandWeight(false, combo, false, true, t, strike.windupRatio) == 0,
                    "idle and dash cannot leave the relaxed off hand pose active");
            }
        }
        foreach (float elevation in new[] { 30f, 45f, 65f })
        foreach (float scale in new[] { 1f, 1.6f })
        for (int facing = 0; facing < 8; facing++)
        {
            bool mainLeft = HeroArmMath.RightHandOnScreenLeft(facing);
            Vector3 center = new Vector3(2, 3, 1);
            Vector3 up = ObliqueProjection.Up(elevation);
            Vector3 shoulder = center + Vector3.right * (mainLeft ? 0.14f : -0.14f) * scale + up * 0.53f * scale;
            for (int combo = 0; combo < 2; combo++)
            {
                Vector3 guard = HeroArmMath.RelaxedOffHand(shoulder, up, !mainLeft, scale);
                Check(Vector3.Distance(guard, shoulder) < 0.41f * scale, "relaxed hand stays within a natural hanging arm reach");
                Check(Vector3.Dot(shoulder - guard, up) > 0.39f * scale, "left hand hangs below the shoulder beside the hip");
                Check((guard.x - center.x) * (mainLeft ? 1 : -1) > 0, "off hand remains on its own side of the torso");
            }
            var orders = HeroArmMath.Sorting(facing >= 3 && facing <= 5, 100, 5);
            if (facing >= 3 && facing <= 5)
                Check(orders.hand + 1 < 100, "relaxed hand remain occluded by the body from behind");
        }
        Console.WriteLine("SINGLE_HAND_PASS: " + count + " assertions");
    }
}
