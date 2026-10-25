using System;
using SlashDemo;
using UnityEngine;

static class KineticChecks
{
    static int count;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        count++;
    }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.00001f;
    static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y)
        && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
    static void Main()
    {
        var first = SwordMotion.Defaults()[0];
        var hipFrame = SwordMotion.Evaluate(first, 5.5f / 60 / first.duration);
        Check(hipFrame.hipPhase > 0 && hipFrame.torsoPhase == 0 && hipFrame.shoulderPhase == 0
            && hipFrame.swordPhase == 0, "hip drive begins first near frame five");
        var torsoFrame = SwordMotion.Evaluate(first, 6.5f / 60 / first.duration);
        Check(torsoFrame.hipPhase > torsoFrame.torsoPhase && torsoFrame.torsoPhase > 0
            && torsoFrame.shoulderPhase == 0 && torsoFrame.swordPhase == 0, "torso begins near frame six after hips");
        var shoulderFrame = SwordMotion.Evaluate(first, 7.5f / 60 / first.duration);
        Check(shoulderFrame.shoulderPhase > 0 && shoulderFrame.torsoPhase > shoulderFrame.shoulderPhase
            && shoulderFrame.swordPhase == 0, "shoulders begin near frame seven before sword");
        Check(SwordMotion.Evaluate(first, 9f / 60 / first.duration).swordPhase > 0,
            "sword drive begins near frame nine");

        var waist = new Vector2(0, 0.52f);
        foreach (var strike in SwordMotion.Defaults())
        {
            float stroke = strike.action == SwordAction.ReverseCleave ? -1 : 1;
            float endTime = strike.windupRatio + strike.activeRatio;
            var load = SwordMotion.Evaluate(strike, strike.windupRatio);
            var end = SwordMotion.Evaluate(strike, endTime);
            float expectedStart = strike.action == SwordAction.Cleave ? 55 : strike.action == SwordAction.ReverseCleave ? -60 : 80;
            Check(Near(load.angle, expectedStart) && Near(end.angle, -expectedStart),
                "restored horizontal cuts use their original 110, 120 and 160 degree arcs");
            Check(Near(load.bodyOffset.x, -0.06f) && Near(load.sink, -0.04f), "windup shifts hips back and sinks");
            Check(Near(load.torsoShear, -0.10f * stroke), "windup twists back around waist");
            Check(Near(end.bodyOffset.x, 0.08f) && Near(end.sink, -0.12f), "release drives hips forward and presses down");
            Check(Near(end.torsoShear, 0.16f * stroke), "release twists through the target");
            Check(Near(end.torsoTurn * stroke - 6, 7), "torso overshoots target by seven degrees");
            var settled = SwordMotion.Evaluate(strike, 1);
            Check(settled.bodyOffset == Vector2.zero && Near(settled.sink, 0) && Near(settled.torsoShear, 0)
                && Near(settled.shoulderShear, 0) && Near(settled.torsoTurn, 0) && Near(settled.shoulderTurn, 0),
                "all primary deformation returns exactly to rest");
            var rebound = SwordMotion.Evaluate(strike, endTime + (1 - endTime) * 0.4f);
            Check(Mathf.Abs(rebound.torsoTurn) < 6 && Mathf.Abs(rebound.shoulderTurn - rebound.torsoTurn) > 0.01f,
                "torso rebounds through target while shoulders follow later");

            foreach (float boundary in new[] { strike.windupRatio, SwordMotion.SwingStart(strike), endTime })
            {
                var before = SwordMotion.Evaluate(strike, boundary - 0.00001f);
                var after = SwordMotion.Evaluate(strike, boundary + 0.00001f);
                Check(Vector2.Distance(before.grip, after.grip) < 0.001f && Mathf.Abs(before.torsoTurn - after.torsoTurn) < 0.01f
                    && Mathf.Abs(before.sink - after.sink) < 0.001f,
                    "phase boundaries have no pose discontinuity");
            }
            for (int frame = 0; frame <= 100; frame++)
            {
                var pose = SwordMotion.Evaluate(strike, frame / 100f);
                Check(pose.hipPhase >= pose.torsoPhase && pose.torsoPhase >= pose.shoulderPhase
                    && pose.shoulderPhase >= pose.swordPhase, "power flows from hips through torso and shoulders to sword");
                for (int y = 0; y <= 20; y++)
                    for (int x = 0; x <= 10; x++)
                    {
                        var uv = new Vector2(x / 10f, y / 20f);
                        var point = new Vector3(uv.x - 0.5f, uv.y);
                        var deformed = HeroLayerMath.KineticDeform(point, uv, waist,
                            new Vector2(pose.bodyOffset.x, pose.sink), pose.torsoShear, pose.shoulderShear,
                            pose.torsoTurn, pose.shoulderTurn, 0);
                        Check(Finite(deformed), "all body layers remain finite throughout cuts");
                        if (y == 0) Check(Vector3.Distance(point, deformed) < 0.000001f, "feet stay planted during weight transfer");
                        if (frame == 100) Check(Vector3.Distance(point, deformed) < 0.000001f, "every vertex restores on recovery");
                    }
            }
            foreach (Vector2 facing in new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down })
            {
                Check(SwordMotion.SweptHit(strike, 0, 1, Vector2.zero, Vector2.zero, facing, facing * 1.2f, 0.2f, out _),
                    "delayed blade still hits forward targets with shared collision pose");
                Check(!SwordMotion.SweptHit(strike, 0, 1, Vector2.zero, Vector2.zero, facing, -facing * 1.2f, 0.2f, out _),
                    "delayed blade excludes targets behind player");
                Check(!SwordMotion.SweptHit(strike, 0, SwordMotion.SwingStart(strike) - 0.001f,
                    Vector2.zero, Vector2.zero, facing, facing, 0.2f, out _), "body loading cannot damage before sword starts");
            }
        }
        var low = new Vector3(0, 0.6f);
        var high = new Vector3(0, 0.75f);
        var lowShear = HeroLayerMath.KineticDeform(low, new Vector2(0.5f, 0.6f), waist, Vector2.zero, 0.16f, 0.16f, 0, 0, 0);
        var highShear = HeroLayerMath.KineticDeform(high, new Vector2(0.5f, 0.75f), waist, Vector2.zero, 0.16f, 0.16f, 0, 0, 0);
        Check(Near(lowShear.x, 0.16f * (low.y - waist.y)) && Near(highShear.x, 0.16f * (high.y - waist.y)),
            "shear uses height relative to waist rather than whole-body translation");
        foreach (Vector2 uv in new[] { new Vector2(0.9f, 0.4f), new Vector2(0.9f, 0.9f) })
        {
            var point = new Vector3(uv.x - 0.5f, uv.y);
            var inherited = HeroLayerMath.KineticDeform(point, uv, waist, new Vector2(0.08f, -0.12f), 0.16f, 0.16f, 13, 13, 0);
            var dragged = HeroLayerMath.KineticDeform(point, uv, waist, new Vector2(0.08f, -0.12f), 0.16f, 0.16f, 13, 13, 0.02f);
            Check(Vector3.Distance(point, inherited) > 0.01f && Vector3.Distance(inherited, dragged) > 0.0001f,
                "hem and hair inherit torso deformation before their secondary drag");
        }
        foreach (float active in new[] { 0.15f, 0.3f, 0.55f })
        {
            var tuned = first.Clone();
            tuned.activeRatio = active;
            Check(SwordMotion.SwingStart(tuned) < tuned.windupRatio + active, "tuning always leaves a valid sword swing window");
        }
        var oldThird = SwordMotion.Defaults()[2];
        oldThird.arc = 190; oldThird.motionVersion = 4;
        SwordMotion.UpgradeLegacy(oldThird, 2);
        Check(oldThird.arc == 160 && oldThird.motionVersion == 5, "discarded combo saves restore the previous 160 degree sweep");
        oldThird.arc = 150;
        SwordMotion.UpgradeLegacy(oldThird, 2);
        Check(oldThird.arc == 150, "newly tuned arcs survive repeated migration");
        var custom = SwordMotion.Defaults()[2]; custom.arc = 145; custom.motionVersion = 4;
        SwordMotion.UpgradeLegacy(custom, 2);
        Check(custom.arc == 145, "old custom arcs remain intact");
        var settings = new SlashTuningSettings();
        settings.Validate(new SlashTuningSettings());
        Check(settings.strikes[2].arc == 160, "tuning validation retains the restored finisher default");
        Console.WriteLine("KINETIC_PASS: " + count + " assertions");
    }
}
