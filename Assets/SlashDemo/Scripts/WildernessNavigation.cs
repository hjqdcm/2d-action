using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    // Shared body-sized pathfinding and swept collision for wilderness creatures.
    public sealed class WildernessNavigation : MonoBehaviour
    {
        TravelMapEnvironment map;
        Rigidbody2D body;
        float Radius;
        const float Cell = .65f;
        float nextRoute;
        readonly List<Vector2> nodes = new List<Vector2>();
        readonly List<int[]> links = new List<int[]>();
        readonly List<Vector2> route = new List<Vector2>();
        public void Initialize(TravelMapEnvironment environment, Rigidbody2D physicsBody, float radius)
        { map = environment; body = physicsBody; Radius = radius; }

        public Vector2 ChaseDirection(Vector2 from, Vector2 target)
        {
            if (Clear(from, target)) { route.Clear(); return (target - from).normalized; }
            if (Time.time >= nextRoute)
            {
                nextRoute = Time.time + 0.6f;
                if (nodes.Count == 0) BuildNavigation();
                FindRoute(from, target);
            }
            while (route.Count > 0 && Vector2.Distance(from, route[0]) < 0.22f) route.RemoveAt(0);
            // Smooth the grid path while keeping the complete body inside the boundary.
            while (route.Count > 1 && Clear(from, route[1])) route.RemoveAt(0);
            return route.Count > 0 ? (route[0] - from).normalized : Vector2.zero;
        }

        // Kinematic bodies need an explicit sweep to remain blocked by walls and other creatures.
        // Slide along contacts instead of stopping all movement when approaching at an angle.
        public Vector2 ConstrainVelocity(Vector2 velocity, float dt)
        {
            if (dt <= 0 || velocity.sqrMagnitude < .000001f) return Vector2.zero;
            const float skin = .015f;
            Vector2 start = body.position, position = start, remaining = velocity * dt;
            var own = GetComponent<CircleCollider2D>();
            for (int step = 0; step < 3 && remaining.sqrMagnitude > .000001f; step++)
            {
                float length = remaining.magnitude, nearest = length;
                Vector2 direction = remaining / length, normal = Vector2.zero;
                foreach (var hit in Physics2D.CircleCastAll(position, Radius, direction, length + skin))
                {
                    var obstacle = hit.collider;
                    if (obstacle.isTrigger || obstacle.transform.IsChildOf(transform)
                        || Physics2D.GetIgnoreCollision(own, obstacle)) continue;
                    Vector2 contactNormal = hit.normal;
                    if (hit.distance == 0)
                    {
                        Vector2 away = position - obstacle.ClosestPoint(position);
                        if (away.sqrMagnitude > .000001f) contactNormal = away.normalized;
                    }
                    if (Vector2.Dot(direction, contactNormal) >= -.001f) continue;
                    float travel = Mathf.Max(0, hit.distance - skin);
                    if (travel >= nearest) continue;
                    nearest = travel; normal = contactNormal;
                }
                position += direction * nearest;
                if (normal == Vector2.zero) break;
                remaining -= direction * nearest;
                remaining -= normal * Mathf.Min(0, Vector2.Dot(remaining, normal));
            }
            return (position - start) / dt;
        }

        internal bool Clear(Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            foreach (var hit in Physics2D.CircleCastAll(a, Radius + 0.04f, delta.normalized, delta.magnitude))
                if (!hit.collider.isTrigger && hit.transform.IsChildOf(map.transform)
                    && hit.collider.GetComponent<SlashEnemy>() == null) return false;
            return true;
        }

        public bool Walkable(Vector2 p)
        {
            var boundary = map.Definition.boundary;
            bool inside = false;
            for (int i = 0, j = boundary.Length - 1; i < boundary.Length; j = i++)
            {
                Vector2 a = map.Definition.Ground(boundary[j].x, boundary[j].y);
                Vector2 b = map.Definition.Ground(boundary[i].x, boundary[i].y);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
                Vector2 edge = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(0.00001f, edge.sqrMagnitude));
                if (Vector2.Distance(p, a + t * edge) < Radius + 0.06f) return false;
            }
            return inside;
        }

        void BuildNavigation()
        {
            int width = Mathf.CeilToInt(map.Definition.WorldSize.x / Cell) + 1;
            int height = Mathf.CeilToInt(map.Definition.WorldSize.y / Cell) + 1;
            var grid = new int[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var p = new Vector2(x * Cell, y * Cell) - map.Definition.WorldSize / 2;
                    grid[x, y] = Walkable(p) ? nodes.Count : -1;
                    if (grid[x, y] >= 0) nodes.Add(p);
                }
            for (int i = 0; i < nodes.Count; i++) links.Add(null);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    int id = grid[x, y];
                    if (id < 0) continue;
                    var adjacent = new List<int>();
                    foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                    {
                        int nx = x + d.x, ny = y + d.y;
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height && grid[nx, ny] >= 0
                            && Clear(nodes[id], nodes[grid[nx, ny]])) adjacent.Add(grid[nx, ny]);
                    }
                    links[id] = adjacent.ToArray();
                }
        }

        int Closest(Vector2 p, bool requireClear = true)
        {
            int best = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < nodes.Count; i++)
            {
                float d = (nodes[i] - p).sqrMagnitude;
                if (d < distance && (!requireClear || Clear(p, nodes[i]))) { distance = d; best = i; }
            }
            return best;
        }

        void FindRoute(Vector2 from, Vector2 target)
        {
            route.Clear();
            int start = Closest(from), end = Closest(target, false);
            if (start < 0 || end < 0) return;
            var previous = new int[nodes.Count];
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            var queue = new Queue<int>();
            previous[start] = start; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == end) break;
                foreach (int neighbor in links[current])
                    if (previous[neighbor] < 0) { previous[neighbor] = current; queue.Enqueue(neighbor); }
            }
            if (previous[end] < 0) return;
            for (int i = end; i != start; i = previous[i]) route.Add(nodes[i]);
            route.Add(nodes[start]); route.Reverse();
        }

    }
}
