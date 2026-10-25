using UnityEngine;

namespace SlashDemo
{
    public static class CombatFeedbackMath
    {
        public static float AddShake(float current, float strength, float multiplier)
        {
            return Mathf.Clamp(current + Mathf.Max(0, strength) * Mathf.Max(0, multiplier) * 2, 0, 0.32f);
        }

        public static Vector2 ShakeOffset(float amplitude, float elapsed, float directionDegrees, float frequency)
        {
            float phase = elapsed * frequency * Mathf.PI * 2;
            // Full signed range, with a nonzero initial kick; it also works while timeScale is zero.
            Vector2 wave = Vector2.ClampMagnitude(new Vector2(Mathf.Cos(phase), Mathf.Sin(phase * 1.31f)), 1);
            return SwordMotion.ToWorld(wave * amplitude, DemoArt.Direction(directionDegrees));
        }

        public static float DecayShake(float amplitude, float unscaledDelta)
        {
            return amplitude * Mathf.Exp(-18 * Mathf.Max(0, unscaledDelta));
        }

        public static float TrailAlpha(float born, float now, float lifetime)
        {
            if (lifetime <= 0) return 0;
            float remaining = Mathf.Clamp01(1 - (now - born) / lifetime);
            return remaining * remaining;
        }

        public static float ProjectLean(float lean, Vector2 facing)
        {
            // Continuous projection: crossing the vertical axis no longer flips the whole pose.
            return lean * facing.x;
        }

        public static float TrailCrescent(float u, float power)
        {
            if (u <= 0 || u >= 1) return 0;
            return Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * Mathf.PI)), Mathf.Clamp(power, 0.4f, 1));
        }

        public static float PunchZoom(float baseSize, float strength, float amount)
        {
            return baseSize / (1 + Mathf.Clamp01(strength) * Mathf.Clamp(amount, 0, 0.08f));
        }

        public static float WaveFade(float progress)
        {
            float t = Mathf.Clamp01(progress);
            return 1 - t * t;
        }
    }
}
