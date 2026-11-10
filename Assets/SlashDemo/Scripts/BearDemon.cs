using UnityEngine;

namespace SlashDemo
{
    // Uses the same damage, hit reaction and death pipeline as every other enemy.
    public sealed class BearDemon : MonoBehaviour
    {
        public const string ArtResource = "SlashDemo/RockBear-v1";
        public const float Height = 2.35f;
        public const float RangeScale = 1.55f * 1.5f;
        public const int WildernessCount = 3;
        public int FormationSlot { get; private set; }
        Sprite art;
        SpriteRenderer skin;
        SlashArena arena;
        WildernessNavigation navigation;
        const float Radius = 1.12f;

        public static void SpawnPack(SlashArena arena, TravelMapEnvironment map)
        {
            // Reference image origin is at the top left; the entrance is at y=865.
            // This horizontal row lies in the northern half of the walkable clearing.
            for (int i = 0; i < WildernessCount; i++)
            {
                var enemy = Spawn(arena, map, map.Definition.Ground(510 + i * 300, 360));
                enemy.GetComponent<BearDemon>().FormationSlot = i;
                enemy.name = "岩甲熊妖 " + (i + 1);
            }
        }

        public static SlashEnemy Spawn(SlashArena arena, TravelMapEnvironment map, Vector2 position)
        {
            var texture = Resources.Load<Texture2D>(ArtResource);
            if (texture == null) throw new System.InvalidOperationException("Missing rock bear artwork: " + ArtResource);
            var go = new GameObject("岩甲熊妖");
            go.SetActive(false);
            go.transform.SetParent(map.transform, false);
            go.transform.position = position;
            var body = DemoArt.Body(go, Radius, 5);
            // AI owns this body's movement; contact impulses from the player cannot push it.
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            var bear = go.AddComponent<BearDemon>();
            bear.arena = arena;
            bear.navigation = go.AddComponent<WildernessNavigation>();
            bear.navigation.Initialize(map, body, Radius);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            DemoArt.Shape("Shadow", visual, "Disk", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.32f));
            bear.art = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.08f), texture.height / Height, 0, SpriteMeshType.FullRect);
            bear.skin = new GameObject("Rock bear skin").AddComponent<SpriteRenderer>();
            bear.skin.transform.SetParent(visual, false);
            bear.skin.sprite = bear.art;
            bear.skin.sharedMaterial = DemoArt.Material;
            go.AddComponent<BearWalkMesh>().Initialize(bear.skin, body, arena);
            DemoArt.Shape("HealthBack", go.transform, "Square", Vector2.zero, Vector2.one, new Color(0.035f, 0.055f, 0.065f), 500);
            DemoArt.Shape("HealthFill", go.transform, "Square", Vector2.zero, Vector2.one, DemoArt.Red, 501);
            var warning = new GameObject("AttackWarning").transform;
            warning.SetParent(go.transform, false);
            var points = new Vector3[26];
            for (int i = 1; i < points.Length - 1; i++)
                points[i] = DemoArt.Direction(Mathf.Lerp(-52.5f, 52.5f, (i - 1) / 23f)) * 1.15f;
            DemoArt.Line("Claw attack warning", warning, points, DemoArt.AttackWarningColor, DemoArt.BeastAttackWarningWidth, 10);
            warning.gameObject.SetActive(false);
            var enemy = go.AddComponent<SlashEnemy>();
            enemy.rockBear = true;
            enemy.maxHealth = 240;
            enemy.moveSpeed = 1.8f;
            enemy.windupTime = 0.84f;
            enemy.damage = 12;
            enemy.attackRange = 1.15f * RangeScale;
            go.AddComponent<BearPackSteering>().Initialize(arena,enemy,bear);
            var presentation = go.AddComponent<ObliqueActor>();
            presentation.CreatureHeight = Height;
            presentation.Initialize(arena);
            go.SetActive(true);
            arena.RegisterEnemy(enemy);
            if (arena.Tuning != null) arena.Tuning.ApplyEnemy(enemy);
            return enemy;
        }

        void LateUpdate()
        {
            if (arena == null || arena.Paused || !GetComponent<SlashEnemy>().Alive) return;
            if (arena.Player != null)
            {
                var combat = GetComponent<BearCombat>();
                float dx = combat != null && combat.SkillLocked ? combat.LockedAim.x
                    : arena.Player.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.15f) skin.flipX = dx < 0;
            }
        }

        public Vector2 ChaseDirection(Vector2 from, Vector2 target) => navigation.ChaseDirection(from, target);
        public Vector2 ConstrainVelocity(Vector2 velocity, float dt) => navigation.ConstrainVelocity(velocity, dt);
        internal bool Clear(Vector2 a, Vector2 b) => navigation.Clear(a, b);
        internal bool Walkable(Vector2 p) => navigation.Walkable(p);

        void OnDestroy() { if (art != null) Destroy(art); }
    }
}
