using System;
using SlashDemo;
using UnityEngine;

static class BladeFeedbackChecks
{
    static int count;
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        count++;
    }
    static void Main()
    {
        foreach (float power in new[] { 0.4f, 0.7f, 1f })
        {
            Check(CombatFeedbackMath.TrailCrescent(0, power) == 0
                && CombatFeedbackMath.TrailCrescent(1, power) == 0, "both crescent tips close completely");
            Check(Mathf.Abs(CombatFeedbackMath.TrailCrescent(0.5f, power) - 1) < 0.000001f,
                "crescent is widest in its middle");
            for (int i = 0; i <= 100; i++)
            {
                float u = i / 100f;
                float width = CombatFeedbackMath.TrailCrescent(u, power);
                Check(width >= 0 && width <= 1 && Mathf.Abs(width - CombatFeedbackMath.TrailCrescent(1 - u, power)) < 0.00001f,
                    "crescent is finite, bounded and symmetric at all supported sharpness values");
            }
        }
        Check(CombatFeedbackMath.TrailCrescent(0.1f, 1) < CombatFeedbackMath.TrailCrescent(0.1f, 0.4f),
            "larger exponent makes the tips narrower");
        foreach (float lifetime in new[] { 0.1f, 0.12f, 0.14f })
        {
            float previous = 1;
            for (int i = 0; i <= 100; i++)
            {
                float alpha = CombatFeedbackMath.TrailAlpha(0, lifetime * i / 100, lifetime);
                Check(alpha <= previous && alpha >= 0, "old samples fade monotonically without being relit by new geometry");
                previous = alpha;
            }
        }
        Check(CombatFeedbackMath.PunchZoom(6, 1, 0) == 6, "zero punch tuning restores exact base framing");
        Check(CombatFeedbackMath.PunchZoom(6, 0, 0.08f) == 6, "no impact preserves configured zoom");
        Check(Mathf.Abs(CombatFeedbackMath.PunchZoom(6, 1, 0.04f) - 6 / 1.04f) < 0.000001f,
            "heavy impact applies configured four percent push");
        Check(CombatFeedbackMath.PunchZoom(6, 0.35f, 0.04f) > CombatFeedbackMath.PunchZoom(6, 1, 0.04f),
            "light impacts push less than heavy impacts");
        Check(CombatFeedbackMath.PunchZoom(6, 100, 100) >= 6 / 1.08f, "repeated impacts cannot exceed eight percent push");
        float previousFade = 1;
        for (int i = 0; i <= 100; i++)
        {
            float fade = CombatFeedbackMath.WaveFade(i / 100f);
            Check(fade >= 0 && fade <= previousFade, "traveling wave fades steadily rather than popping at range end");
            previousFade = fade;
        }
        Check(previousFade == 0, "wave disappears transparently at maximum distance");
        var settings = new SlashTuningSettings();
        var copy = settings.Clone();
        copy.bladeFeedback.glowWidth = 0.18f;
        Check(settings.bladeFeedback.glowWidth == 0.10f, "saved defaults and active feedback settings are deep copied");
        copy.bladeFeedback = null;
        copy.moveSpeed = 7;
        copy.Validate(settings);
        Check(copy.bladeFeedback.punchAmount == 0.04f && copy.bladeFeedback.glowWidth == 0.10f && copy.moveSpeed == 7,
            "older saves receive feedback defaults while preserving other tuning");
        copy.bladeFeedback.glowWidth = float.NaN;
        copy.bladeFeedback.crescentPower = 100;
        copy.bladeFeedback.punchAmount = 0;
        copy.Validate(settings);
        Check(copy.bladeFeedback.glowWidth == 0.10f && copy.bladeFeedback.crescentPower == 1
            && copy.bladeFeedback.punchAmount == 0, "invalid feedback settings are bounded and explicit punch disable survives");
        copy.bladeFeedback.hdrIntensity = 0;
        copy.Validate(settings);
        Check(copy.bladeFeedback.hdrIntensity == 2.2f, "older feedback saves receive the HDR core default");
        copy.bladeFeedback.hdrIntensity = float.NaN;
        copy.Validate(settings);
        Check(copy.bladeFeedback.hdrIntensity == 2.2f, "invalid HDR intensity falls back safely");
        copy.bladeFeedback.hdrIntensity = 100;
        copy.Validate(settings);
        Check(copy.bladeFeedback.hdrIntensity == 3 && settings.bladeFeedback.hdrIntensity == 2.2f,
            "HDR tuning is bounded and does not mutate defaults");
        Console.WriteLine("BLADE_FEEDBACK_PASS: " + count + " assertions");
    }
}
