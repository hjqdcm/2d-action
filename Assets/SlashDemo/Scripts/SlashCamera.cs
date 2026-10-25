using UnityEngine;

namespace SlashDemo
{
    [RequireComponent(typeof(Camera))]
    public sealed class SlashCamera : MonoBehaviour
    {
        const float FeedbackStrength = 0.1f;
        public Transform target;
        [Min(0)] public float shakeMultiplier = 1;
        [Min(1)] public float shakeFrequency = 24;
        [Range(0, 0.08f)] public float punchAmount = 0.04f;
        float shakeAmplitude, shakeElapsed, shakeDirection;
        float punch;
        Vector3 smoothFocus;
        ObliqueView view;
        Camera worldCamera;

        void Awake()
        {
            worldCamera = GetComponent<Camera>();
            view = GetComponentInParent<SlashArena>().View;
            ApplyView(Vector2.zero);
        }

        void ApplyView(Vector2 jitter)
        {
            transform.rotation = view.Rotation;
            transform.position = smoothFocus - ObliqueProjection.Forward(view.elevation) * 20
                + transform.right * jitter.x + transform.up * jitter.y;
            worldCamera.orthographic = true;
            float groundDepth = ObliqueProjection.DepthScale(view.elevation) * 5.6f;
            float baseSize = Mathf.Max(groundDepth + 1.75f, 12 / Mathf.Max(0.1f, worldCamera.aspect)) / view.zoom;
            worldCamera.orthographicSize = CombatFeedbackMath.PunchZoom(baseSize, punch, punchAmount * FeedbackStrength);
        }
        public void Shake(float strength)
        {
            if (strength <= 0 || shakeMultiplier <= 0) return;
            shakeAmplitude = CombatFeedbackMath.AddShake(shakeAmplitude, strength, shakeMultiplier);
            shakeElapsed = 0;
            shakeDirection = Mathf.Repeat(shakeDirection + 137.5f, 360);
        }

        public void Punch(float strength)
        {
            punch = Mathf.Max(punch, Mathf.Clamp01(strength));
        }

        public void ResetFeedback()
        {
            punch = shakeAmplitude = shakeElapsed = 0;
            ApplyView(Vector2.zero);
        }

        void LateUpdate()
        {
            if (target == null) return;
            var desired = (Vector3)((Vector2)target.position * 0.17f);
            desired += view.Up * 0.25f;
            smoothFocus = Vector3.Lerp(smoothFocus, desired, 1 - Mathf.Exp(-5 * Time.unscaledDeltaTime));
            if (shakeMultiplier <= 0) shakeAmplitude = 0;
            if (punchAmount <= 0) punch = 0;
            Vector2 jitter = CombatFeedbackMath.ShakeOffset(shakeAmplitude * FeedbackStrength, shakeElapsed, shakeDirection, shakeFrequency);
            ApplyView(jitter);
            // Render the impact first, then decay. No first-frame amplitude is discarded.
            shakeAmplitude = CombatFeedbackMath.DecayShake(shakeAmplitude, Time.unscaledDeltaTime);
            if (shakeAmplitude < 0.0001f) shakeAmplitude = 0;
            shakeElapsed += Time.unscaledDeltaTime;
            punch = Mathf.MoveTowards(punch, 0, Time.unscaledDeltaTime * 8);
        }
    }
}
