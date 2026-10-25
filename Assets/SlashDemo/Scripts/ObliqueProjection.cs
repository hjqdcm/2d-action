using UnityEngine;

namespace SlashDemo
{
    // Physics remains in XY. These functions map ground positions and visual height into the view.
    public static class ObliqueProjection
    {
        public static float DepthScale(float elevation) => Mathf.Sin(elevation * Mathf.Deg2Rad);
        public static Vector3 Up(float elevation) => new Vector3(0, DepthScale(elevation), -Mathf.Cos(elevation * Mathf.Deg2Rad));
        public static Vector3 Forward(float elevation) => new Vector3(0, Mathf.Cos(elevation * Mathf.Deg2Rad), DepthScale(elevation));
        public static Vector2 ProjectGround(Vector2 ground, float elevation) => new Vector2(ground.x, ground.y * DepthScale(elevation));
        public static Vector2 UnprojectGround(Vector2 screenPlane, float elevation) => new Vector2(screenPlane.x, screenPlane.y / Mathf.Max(0.1f, DepthScale(elevation)));
        public static Vector2 Movement(Vector2 input, float elevation)
        {
            float magnitude = Mathf.Clamp01(input.magnitude);
            return UnprojectGround(input, elevation).normalized * magnitude;
        }
        public static Vector3 Lift(Vector2 ground, float height, float elevation) => (Vector3)ground + Up(elevation) * height;
        public static int DepthOrder(float groundY) => 1000 - Mathf.RoundToInt(groundY * 100);

        public static bool RayToGround(Ray ray, float elevation, float height, out Vector2 ground)
        {
            ground = Vector2.zero;
            Vector3 lift = Up(elevation) * height;
            if (Mathf.Abs(ray.direction.z) < 0.00001f) return false;
            float distance = (lift.z - ray.origin.z) / ray.direction.z;
            if (distance < 0) return false;
            ground = (Vector2)(ray.origin + ray.direction * distance - lift);
            return true;
        }
    }
}
