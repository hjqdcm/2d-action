using UnityEngine;
using UnityEngine.Rendering;

namespace SlashDemo
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class ObliqueActor : MonoBehaviour
    {
        [SerializeField] SpriteRenderer groundShadow;
        [SerializeField] Transform groundWarning;
        ObliqueView view;
        Transform model;
        SortingGroup sortingGroup;
        SlashEnemy enemy;
        Transform healthBack, healthFill;
        Vector2 poseOffset;
        float bob, lean;
        bool dummy;
        float deathProgress;
        public Transform GroundWarning => groundWarning;
        public float LabelHeight => (dummy ? 1.13f : 1.48f) * view.characterScale;

        public void Initialize(SlashArena arena, bool isDummy = false)
        {
            view = arena.View;
            dummy = isDummy;
            model = transform.Find("Visual");
            enemy = GetComponent<SlashEnemy>();
            sortingGroup = GetComponent<SortingGroup>();
            if (sortingGroup == null) sortingGroup = gameObject.AddComponent<SortingGroup>();
            if (groundShadow == null && model != null)
            {
                var shadow = model.Find("Shadow");
                if (shadow != null) groundShadow = shadow.GetComponent<SpriteRenderer>();
            }
            if (groundShadow != null)
            {
                groundShadow.transform.SetParent(view.GroundMarks, true);
                groundShadow.sortingOrder = -90;
            }
            if (groundWarning == null) groundWarning = transform.Find("AttackWarning");
            if (groundWarning != null)
            {
                groundWarning.SetParent(view.GroundMarks, true);
                foreach (var line in groundWarning.GetComponentsInChildren<LineRenderer>(true))
                { line.sortingOrder = -70; line.sharedMaterial = DemoArt.LineMaterial; }
            }
            healthBack = transform.Find("HealthBack") ?? transform.Find("Health UI/HealthBack");
            healthFill = transform.Find("HealthFill") ?? transform.Find("Health UI/HealthFill");
            if (healthBack != null && healthFill != null)
            {
                var ui = transform.Find("Health UI");
                if (ui == null)
                {
                    ui = new GameObject("Health UI").transform;
                    ui.SetParent(transform, false);
                }
                var uiGroup = ui.GetComponent<SortingGroup>();
                if (uiGroup == null) uiGroup = ui.gameObject.AddComponent<SortingGroup>();
                uiGroup.sortAtRoot = true;
                uiGroup.sortingOrder = 30000;
                healthBack.SetParent(ui, false);
                healthFill.SetParent(ui, false);
            }
            Refresh();
        }

        public void SetPose(Vector2 offset, float verticalBob, float screenLean)
        {
            poseOffset = offset; bob = verticalBob; lean = screenLean;
            Refresh();
        }
        public void SetDeathProgress(float progress)
        {
            deathProgress = Mathf.Clamp01(progress);
            Refresh();
        }
        void LateUpdate() { Refresh(); }
        void Refresh()
        {
            if (view == null || model == null) return;
            float scale = view.characterScale;
            sortingGroup.sortingOrder = ObliqueProjection.DepthOrder(transform.position.y);
            float lift = (dummy ? 0.50f : 0.44f) * scale;
            model.localPosition = (Vector3)poseOffset + view.Up * (lift + bob);
            model.localRotation = view.Rotation * Quaternion.Euler(0, 0, lean);
            model.localScale = Vector3.one * scale * (1 - deathProgress * 0.45f);
            if (groundShadow != null)
            {
                groundShadow.transform.position = new Vector3(transform.position.x, transform.position.y, -0.015f);
                groundShadow.transform.rotation = Quaternion.identity;
                groundShadow.transform.localScale = new Vector3(0.92f * scale, 0.52f * scale, 1);
                groundShadow.enabled = enemy == null || !Application.isPlaying || enemy.Alive;
            }
            if (groundWarning != null) groundWarning.position = new Vector3(transform.position.x, transform.position.y, -0.025f);
            if (healthBack != null && healthFill != null)
            {
                float hp = enemy != null && Application.isPlaying ? enemy.Health / (float)Mathf.Max(1, enemy.maxHealth) : 1;
                Vector3 height = view.Up * ((dummy ? 0.95f : 1.30f) * scale);
                healthBack.localPosition = height;
                healthFill.localPosition = height + Vector3.right * ((hp - 1) * 0.4f * scale);
                healthBack.localRotation = healthFill.localRotation = view.Rotation;
                healthBack.localScale = new Vector3(0.84f * scale, 0.085f, 1);
                healthFill.localScale = new Vector3(0.80f * scale * hp, 0.055f, 1);
                healthBack.GetComponent<SpriteRenderer>().sortingOrder = 60;
                healthFill.GetComponent<SpriteRenderer>().sortingOrder = 61;
                bool visible = enemy == null || !Application.isPlaying || enemy.Alive;
                healthBack.GetComponent<SpriteRenderer>().enabled = visible;
                healthFill.GetComponent<SpriteRenderer>().enabled = visible;
            }
        }

        void OnEnable() { if (groundShadow != null) groundShadow.gameObject.SetActive(true); }
        void OnDisable()
        {
            if (groundShadow != null) groundShadow.gameObject.SetActive(false);
            if (groundWarning != null) groundWarning.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            if (!Application.isPlaying) return;
            if (groundShadow != null) Destroy(groundShadow.gameObject);
            if (groundWarning != null) Destroy(groundWarning.gameObject);
        }
    }
}
