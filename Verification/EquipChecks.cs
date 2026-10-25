using System;
using SlashDemo;
using UnityEngine;

static class EquipChecks
{
    static int count;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        count++;
    }
    static void Main()
    {
        var state = new SwordEquipState();
        Check(SwordMotion.CarryLength == .72f && SwordMotion.CarryAngle == -122,
            "shortened scabbard retains its steep carry angle");
        Check(state.Sheathed && !state.Ready && state.ScabbardVisible, "idle begins sheathed with a visible scabbard");
        state.Advance(0.1f, true, 0.2f, 0.26f, 0.18f);
        Check(state.Amount > 0 && state.Amount < 1 && !state.Ready, "initial attack waits while drawing");
        float held = state.Amount;
        state.Advance(0, true, 0.2f, 0.26f, 0.18f);
        Check(state.Amount == held, "hit stop cannot advance drawing");
        state.Advance(0.1f, true, 0.2f, 0.26f, 0.18f);
        Check(state.Ready, "draw completes in configured time");
        Check(!state.ScabbardVisible, "extracted sword hides its scabbard");
        state.Advance(0.1f, false, 0.2f, 0.26f, 0.18f);
        Check(state.Ready, "brief combo gap keeps sword drawn");
        state.Advance(0.2f, true, 0.2f, 0.26f, 0.18f);
        Check(state.Ready, "continuous attacks do not redraw");
        state.Advance(0.31f, false, 0.2f, 0.26f, 0.18f);
        Check(Mathf.Abs(state.Amount - 0.5f) < 0.00001f, "sheathing consumes only time after idle delay");
        Check(state.ScabbardVisible, "scabbard reappears during insertion");
        state.Advance(0.05f, true, 0.2f, 0.26f, 0.18f);
        Check(Mathf.Abs(state.Amount - 0.75f) < 0.00001f, "new attack reverses interrupted sheathing continuously");
        Check(!state.ScabbardVisible, "redrawing hides scabbard again once blade clears its mouth");
        state.Advance(0.05f, true, 0.2f, 0.26f, 0.18f);
        Check(state.Ready, "redraw only completes the remaining distance");
        state.Advance(1, false, 0.2f, 0.26f, 0.18f);
        Check(state.Sheathed, "idle returns sword fully into scabbard");
        state.Advance(0.08f, true, 0.2f, 0.26f, 0.18f);
        state.Advance(1, false, 0.2f, 0.26f, 0.18f);
        Check(state.Sheathed, "canceled draw and dash interruption safely stow the partial blade");
        for (int direction = 0; direction < 8; direction++)
        {
            Vector2 facing = DemoArt.Direction(direction * 45 - 90);
            Vector2 anatomicalRight = new Vector2(facing.y, -facing.x);
            SwordMotion.EquipHands(SwordMotion.CarryGrip, SwordMotion.CarryGrip, 0, out Vector2 right, out Vector2 left);
            Vector2 rightWorld = SwordMotion.ToWorld(right, facing), leftWorld = SwordMotion.ToWorld(left, facing);
            Check(Vector2.Dot(rightWorld, anatomicalRight) > 0.24f && Vector2.Dot(leftWorld, anatomicalRight) < -0.24f,
                "idle hands remain on their own anatomical sides in all eight directions");
            Check(Vector2.Distance(rightWorld, SwordMotion.ToWorld(SwordMotion.CarryGrip, facing)) < 0.03f,
                "relaxed right hand stays beside the right-side sword hilt");
            if (Mathf.Abs(anatomicalRight.x) > 0.1f)
                Check(HeroArmMath.RightHandOnScreenLeft(direction) == (anatomicalRight.x < 0),
                    "painted shoulder assignment connects the right hand to its anatomical right shoulder");
        }
        SwordMotion.EquipHands(new Vector2(0.4f, -0.1f), SwordMotion.CarryGrip, 0.18f,
            out Vector2 drawnRight, out Vector2 drawnLeft);
        Check(drawnRight == new Vector2(0.4f, -0.1f) && drawnLeft == SwordMotion.CarryGrip,
            "drawing puts the right hand on the hilt and the left hand on the scabbard");
        foreach (var strike in SwordMotion.Defaults())
        {
            var ready = SwordMotion.Evaluate(strike, 0);
            var stowed = SwordMotion.EquipPose(ready, 0, out Vector2 socket);
            Check(stowed.grip == socket && stowed.grip == SwordMotion.CarryGrip, "stowed sword hilt meets scabbard mouth");
            var drawn = SwordMotion.EquipPose(ready, 1, out socket);
            Check(Vector2.Distance(drawn.grip, ready.grip) < 0.000001f
                && Vector2.Distance(drawn.tip, ready.tip) < 0.00001f, "draw joins the actual attack pose without a jump");
            var extracted = SwordMotion.EquipPose(ready, 0.72f, out socket);
            Check(Vector2.Distance(extracted.tip, socket) < 0.00001f, "blade tip clears the mouth before sword rotates into attack");
            for (int frame = 0; frame <= 100; frame++)
            {
                var pose = SwordMotion.EquipPose(ready, frame / 100f, out socket);
                Check(!float.IsNaN(pose.tip.x) && !float.IsNaN(pose.tip.y) && pose.bladeLength > 0,
                    "draw and reversed sheath geometry remain finite");
                Check(Mathf.Abs(Vector2.Distance(pose.grip, pose.tip) - pose.bladeLength) < 0.00001f,
                    "drawing preserves a rigid blade instead of stretching it");
            }
        }
        foreach (float elevation in new[] { 30f, 45f, 65f })
        for (int direction = 0; direction < 8; direction++)
        {
            Vector2 facing = ObliqueProjection.UnprojectGround(DemoArt.Direction(direction * 45 - 90), elevation).normalized;
            Vector2 axis = SwordMotion.CarryAxisInView(facing, elevation, direction);
            Vector2 projected = ObliqueProjection.ProjectGround(axis, elevation).normalized;
            Check(projected.y < -.80f, "case hangs downward at every facing and elevation");
            Check(HeroArmMath.RightHandOnScreenLeft(direction) ? projected.x < 0 : projected.x > 0,
                "case tilts outward on the sword-side hip");
            Vector2 side = new Vector2(-facing.y, facing.x);
            Vector2 localAxis = new Vector2(Vector2.Dot(axis, facing), Vector2.Dot(axis, side));
            var ready = SwordMotion.Evaluate(SwordMotion.Defaults()[0], 0);
            var extracted = SwordMotion.EquipPose(ready, SwordMotion.ExtractFraction, localAxis, out Vector2 socket);
            Check(Vector2.Distance(extracted.tip, socket) < .00001f, "blade clears the adjusted case mouth before aligning");
            var drawn = SwordMotion.EquipPose(ready, 1, localAxis, out socket);
            Check(Vector2.Distance(drawn.tip, ready.tip) < .00001f && Vector2.Distance(drawn.grip, ready.grip) < .00001f,
                "adjusted carry orientation joins the original attack pose");
        }
        Console.WriteLine("EQUIP_PASS: " + count + " assertions");
    }
}
