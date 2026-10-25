using UnityEngine;

namespace SlashDemo
{
    [DisallowMultipleComponent]
    public sealed class ObliqueView : MonoBehaviour
    {
        [Range(30, 65)] public float elevation = 45;
        [Range(1, 1.6f)] public float characterScale = 1.25f;
        [Range(0.85f, 1.25f)] public float zoom = 1;
        [SerializeField] Transform groundMarks;
        public Quaternion Rotation => Quaternion.Euler(elevation - 90, 0, 0);
        public Vector3 Up => ObliqueProjection.Up(elevation);
        public float BodyLift => 0.44f * characterScale;
        public float WeaponHeight => 0.84f * characterScale;
        public float LabelHeight => 1.4f * characterScale;
        public Vector3 LiftPoint(Vector2 ground, float height) => ObliqueProjection.Lift(ground, height, elevation);
        public Vector2 Movement(Vector2 input) => ObliqueProjection.Movement(input, elevation);
        public bool MouseGround(Camera camera, Vector2 screen, out Vector2 ground)
            => ObliqueProjection.RayToGround(camera.ScreenPointToRay(screen), elevation, WeaponHeight, out ground);

        public Transform GroundMarks
        {
            get
            {
                if (groundMarks == null)
                {
                    groundMarks = transform.Find("Ground Marks");
                    if (groundMarks == null)
                    {
                        groundMarks = new GameObject("Ground Marks").transform;
                        groundMarks.SetParent(transform, false);
                    }
                }
                return groundMarks;
            }
        }

        public void PrepareEnvironment(Transform environment)
        {
            if (environment == null) return;
            foreach (var line in environment.GetComponentsInChildren<LineRenderer>(true)) line.sharedMaterial = DemoArt.LineMaterial;
            if (environment.Find("Oblique Architecture") == null) ObliqueScenery.Build(environment, this);
        }
    }
}
