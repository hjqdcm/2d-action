using System;
using SlashDemo;
using UnityEngine;

static class HeavyAttackChecks
{
    static int count;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        count++;
    }

    static void Main()
    {
        var state = new ChargedAttackState();
        Check(!state.Busy && !state.Release(), "idle release never fires");
        Check(state.Begin(0.8f, 0.25f), "charge begins");
        state.Advance(0.799f);
        Check(state.Charging && !state.Ready && !state.Release(), "release before threshold cancels");
        Check(!state.Busy && state.Progress == 0, "early cancel has no release recovery");
        Check(state.Begin(0.8f, 0.25f), "charge can restart after cancel");
        state.Advance(0.8f);
        Check(state.Ready && state.Progress == 1 && state.Charging, "full charge waits for release");
        state.Advance(20);
        Check(state.Ready && state.Charging, "holding beyond full never auto fires");
        Check(state.Release() && state.Recovering && !state.Charging, "charged release starts recovery");
        Check(!state.Release() && !state.Begin(0.8f, 0.25f), "one release cannot fire twice or overlap recovery");
        state.Advance(0);
        Check(state.RecoveryProgress == 0, "paused recovery does not advance");
        state.Advance(0.249f);
        Check(state.Recovering, "recovery lasts configured duration");
        state.Advance(0.0011f);
        Check(!state.Busy, "movement lock ends with recovery");

        foreach (float elapsed in new[] { 0f, 0.4f, 0.8f, 2f })
        {
            state.Begin(0.8f, 0.25f);
            state.Advance(elapsed);
            state.Cancel();
            Check(!state.Busy && !state.Ready && !state.Release(), "dash cancellation discards incomplete and full charges");
        }
        state.Begin(0.8f, 0.25f);
        state.Advance(1);
        state.Release();
        state.Cancel();
        Check(!state.Busy, "dash cancellation also removes recovery");
        state.Begin(0.8f, 0);
        state.Advance(1);
        Check(state.Release() && !state.Busy, "zero recovery tuning releases immediately");
        state.Begin(0.8f, 0.25f);
        state.Advance(0.4f);
        float before = state.Progress;
        for (int i = 0; i < 100; i++) state.Advance(0);
        Check(state.Progress == before && !state.Ready, "hit stop and pause cannot charge by wall time");
        state.Advance(-10);
        Check(state.Progress == before, "negative time cannot rewind charge");
        state.Cancel();

        var settings = new SlashTuningSettings();
        var copy = settings.Clone();
        copy.heavyAttack.chargeTime = 1.5f;
        copy.heavyAttack.width = 3;
        Check(settings.heavyAttack.chargeTime == 0.8f && settings.heavyAttack.width == 1.8f,
            "charge snapshots do not mutate live or saved tuning");
        copy.heavyAttack = null;
        copy.Validate(settings);
        Check(copy.heavyAttack.chargeTime == 0.8f && copy.heavyAttack.distance == 6,
            "old saves missing heavy tuning receive defaults");
        copy.heavyAttack.width = float.NaN;
        copy.heavyAttack.distance = float.PositiveInfinity;
        copy.heavyAttack.chargeTime = -1;
        copy.heavyAttack.recoveryTime = 99;
        copy.heavyAttack.speed = 0;
        copy.heavyAttack.damage = -50;
        copy.Validate(settings);
        Check(copy.heavyAttack.width == 1.8f && copy.heavyAttack.distance == 6,
            "nonfinite projectile tuning falls back safely");
        Check(copy.heavyAttack.chargeTime == 0.1f && copy.heavyAttack.recoveryTime == 1.5f
            && copy.heavyAttack.speed == 3 && copy.heavyAttack.damage == 1, "tuning bounds match sliders");

        foreach (float width in new[] { 0.25f, 1.8f, 5f })
        {
            var left = SwordMotion.WaveArcPoint(width, -1);
            var right = SwordMotion.WaveArcPoint(width, 1);
            Check(Mathf.Abs(right.y - left.y - width) < 0.00001f, "arc keeps the configured chord width");
            Check(left.x < 0 && right.x < 0 && SwordMotion.WaveArcPoint(width, 0) == Vector2.zero,
                "leading midpoint bows forward while both tips trail behind");
            for (int i = 0; i <= SwordMotion.WaveArcSegments; i++)
            {
                var point = SwordMotion.WaveArcPoint(width, 2f * i / SwordMotion.WaveArcSegments - 1);
                Check(Mathf.Abs(Vector2.Distance(point, new Vector2(-width, 0)) - width) < 0.00001f,
                    "all drawn samples lie on one true large-radius circle");
            }
        }
        Check(SwordMotion.WaveHit(Vector2.zero, Vector2.right, 0, 5, new Vector2(-0.614f, 2.4f), 0),
            "curved trailing tip hits where the old straight line would miss");
        Check(!SwordMotion.WaveHit(Vector2.zero, Vector2.right, 0, 5, new Vector2(0, 2.4f), 0),
            "empty space ahead of a trailing tip is not filled by rectangular damage");
        Check(!SwordMotion.WaveHit(Vector2.zero, Vector2.right, 0, 5, new Vector2(-0.6f, 0), 0),
            "hollow space behind the leading midpoint is not filled with damage");
        Check(SwordMotion.WaveHit(Vector2.zero, Vector2.right, 0.3f, 5, new Vector2(-0.4f, 2.4f), 0),
            "moving arc sweeps its curved tips continuously");
        Check(!SwordMotion.WaveHit(Vector2.zero, Vector2.right, 0.3f, 5, new Vector2(-0.2f, 2.4f), 0),
            "curved tips cannot hit beyond their actual traveled path");

        // Long steps model low frame rates: a crossed target must still be hit in all aim directions.
        for (int angle = 0; angle < 360; angle += 5)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 facing = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 side = new Vector2(-facing.y, facing.x);
            Vector2 origin = new Vector2(2, -1);
            Check(SwordMotion.WaveHit(origin, facing, 6, 1.8f, origin + facing * 3, 0.2f),
                "swept wave cannot tunnel through center targets");
            Check(SwordMotion.WaveHit(origin, facing, 6, 1.8f, origin + facing * 3 + side * 1.05f, 0.2f),
                "width includes touching target collider edges");
            Check(!SwordMotion.WaveHit(origin, facing, 6, 1.8f, origin + facing * 3 + side * 1.3f, 0.2f),
                "targets outside width are excluded");
            Check(!SwordMotion.WaveHit(origin, facing, 6, 1.8f, origin + facing * 6.5f, 0.2f),
                "width cannot extend flight distance");
            Check(!SwordMotion.WaveHit(origin, facing, 6, 1.8f, origin - facing, 0.2f),
                "rear targets are excluded");
            Check(!SwordMotion.WaveHit(origin, facing, 0.2f, 1.8f, origin + facing * 3, 0.2f),
                "wave damage travels with wave rather than instantly hitting full range");
            Check(SwordMotion.WaveHit(origin, facing, 6, 3, origin + facing * 3 + side * 1.3f, 0.2f),
                "increasing width changes actual damage coverage");
            bool crossed = false;
            for (int frame = 0; frame < 60; frame++)
                crossed |= SwordMotion.WaveHit(origin + facing * frame * 0.1f, facing, 0.1f, 1.8f,
                    origin + facing * 3 + side * 0.4f, 0.2f);
            Check(crossed, "fine and coarse frame steps both hit targets");
        }
        Console.WriteLine("HEAVY_ATTACK_PASS: " + count + " assertions");
    }
}
