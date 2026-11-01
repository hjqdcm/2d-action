using UnityEngine;

namespace SlashDemo
{
    public sealed class TeleportFormation : MonoBehaviour
    {
        public const string ArtResource = "SlashDemo/TeleportFormation-v1";
        public const float InteractionRadius = 1.65f;
        const float VisualRadius = 2.4f;
        Sprite sprite;
        SpriteRenderer visual;
        float age;

        public static TeleportFormation Spawn(TravelMapEnvironment map, Vector2 near)
        {
            var texture = Resources.Load<Texture2D>(ArtResource);
            if (texture == null) throw new System.InvalidOperationException("Missing teleport formation artwork.");
            var portal = new GameObject("清场传送阵").AddComponent<TeleportFormation>();
            portal.transform.SetParent(map.transform, false);
            portal.transform.position = (Vector3)FindPosition(map, near) + Vector3.back * .02f;
            portal.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                Vector2.one * .5f, Mathf.Max(texture.width, texture.height) / (VisualRadius * 2), 0, SpriteMeshType.FullRect);
            portal.visual = portal.gameObject.AddComponent<SpriteRenderer>();
            portal.visual.sprite = portal.sprite;
            portal.visual.sharedMaterial = DemoArt.Material;
            portal.visual.sortingOrder = -80;
            return portal;
        }

        static Vector2 FindPosition(TravelMapEnvironment map, Vector2 near)
        {
            // Keep the whole rune circle inside the map, away from solid obstacles.
            for (int ring = 0; ring <= 12; ring++)
                for (int sample = 0; sample < (ring == 0 ? 1 : 16); sample++)
                {
                    Vector2 point = near + DemoArt.Direction(sample * 22.5f) * (ring * .65f);
                    if (Clear(map, point)) return point;
                }
            return map.Definition.Entry;
        }

        static bool Clear(TravelMapEnvironment map, Vector2 point)
        {
            bool inside = false;
            var boundary = map.Definition.boundary;
            for (int i = 0, j = boundary.Length - 1; i < boundary.Length; j = i++)
            {
                Vector2 a = map.Definition.Ground(boundary[j].x, boundary[j].y);
                Vector2 b = map.Definition.Ground(boundary[i].x, boundary[i].y);
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)
                    inside = !inside;
            }
            if (!inside) return false;
            foreach (var collider in Physics2D.OverlapCircleAll(point, VisualRadius))
                if (!collider.isTrigger && collider.transform.IsChildOf(map.transform)
                    && collider.GetComponentInParent<SlashEnemy>() == null) return false;
            return true;
        }

        void Update()
        {
            if (Time.deltaTime <= 0 || visual == null) return;
            age += Time.deltaTime;
            float appear = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .55f));
            float pulse = .88f + .07f * Mathf.Sin(age * 2);
            visual.color = new Color(1, 1, 1, appear * pulse);
            transform.localScale = Vector3.one * Mathf.Lerp(.8f, 1, appear);
        }

        void OnDestroy() { if (sprite != null) Destroy(sprite); }
    }
}
