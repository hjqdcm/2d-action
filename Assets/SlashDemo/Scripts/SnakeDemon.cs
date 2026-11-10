using UnityEngine;

namespace SlashDemo
{
    [DefaultExecutionOrder(70)]
    public sealed class SnakeDemon : MonoBehaviour
    {
        public const string ArtResource = "SlashDemo/JadeSnake-v1";
        public const float BodyScale = .85f;
        public const float Height = 2.7f * BodyScale;
        public const int WildernessCount = 4;
        public const int BaseHealth = 150;
        public const int BaseDamage = 8;
        public const float MoveSpeedScale = .52f;
        public int FormationSlot { get; private set; }
        public const float WindupScale = 1.45f * .8f;
        public const float RangeScale = BearDemon.RangeScale * .9f;
        public const float Radius = 1.12f * BodyScale;
        public WildernessNavigation Navigation { get; private set; }
        SlashArena arena;
        SlashEnemy enemy;
        Sprite art;
        SpriteRenderer skin;

        public static void SpawnPack(SlashArena arena, TravelMapEnvironment map)
        {
            for (int i = 0; i < WildernessCount; i++)
            {
                var enemy = Spawn(arena, map, map.Definition.Ground(450 + i * 240, 360), i);
                enemy.name = "碧鳞蛇妖 " + (i + 1);
            }
        }

        public static SlashEnemy Spawn(SlashArena arena, TravelMapEnvironment map, Vector2 position, int formationSlot = 0)
        {
            var texture = Resources.Load<Texture2D>(ArtResource);
            if (texture == null) throw new System.InvalidOperationException("Missing snake artwork: " + ArtResource);
            var go = new GameObject("碧鳞蛇妖");
            go.SetActive(false);
            go.transform.SetParent(map.transform, false);
            go.transform.position = position;
            var body = DemoArt.Body(go, Radius, 5);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            var snake = go.AddComponent<SnakeDemon>();
            snake.arena = arena;
            snake.FormationSlot = formationSlot;
            snake.Navigation = go.AddComponent<WildernessNavigation>();
            snake.Navigation.Initialize(map, body, Radius);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            DemoArt.Shape("Shadow", visual, "Disk", Vector2.zero, Vector2.one, new Color(0, 0, 0, .32f));
            snake.art = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .04f), texture.height / Height, 0, SpriteMeshType.FullRect);
            snake.skin = new GameObject("Jade snake skin").AddComponent<SpriteRenderer>();
            snake.skin.transform.SetParent(visual, false);
            snake.skin.sprite = snake.art;
            snake.skin.sharedMaterial = DemoArt.Material;
            go.AddComponent<SnakeAnimation>().Initialize(snake.skin, arena);
            DemoArt.Shape("HealthBack", go.transform, "Square", Vector2.zero, Vector2.one, new Color(.035f, .055f, .065f), 500);
            DemoArt.Shape("HealthFill", go.transform, "Square", Vector2.zero, Vector2.one, DemoArt.Red, 501);
            var warning = new GameObject("AttackWarning").transform;
            warning.SetParent(go.transform, false);
            var points = new Vector3[26];
            for (int i = 1; i < points.Length - 1; i++)
                points[i] = DemoArt.Direction(Mathf.Lerp(-52.5f, 52.5f, (i - 1) / 23f)) * 1.15f;
            DemoArt.Line("Bite warning", warning, points, DemoArt.AttackWarningColor, DemoArt.BeastAttackWarningWidth, 10);
            warning.gameObject.SetActive(false);
            var enemy = go.AddComponent<SlashEnemy>();
            enemy.maxHealth = BaseHealth; enemy.moveSpeed = arena.Player.moveSpeed * MoveSpeedScale;
            enemy.windupTime = .58f * WindupScale; enemy.damage = BaseDamage; enemy.attackRange = 1.15f * RangeScale;
            snake.enemy = enemy;
            var actor = go.AddComponent<ObliqueActor>();
            actor.CreatureHeight = Height;
            actor.ShadowScale = BodyScale;
            actor.Initialize(arena);
            go.SetActive(true);
            arena.RegisterEnemy(enemy);
            if (arena.Tuning != null) arena.Tuning.ApplyEnemy(enemy);
            return enemy;
        }

        void LateUpdate()
        {
            if (arena == null || arena.Paused || Time.deltaTime <= 0 || !enemy.Alive) return;
            if (arena.Player != null)
            {
                var combat = GetComponent<SnakeCombat>();
                float dx = combat != null && !combat.IsChasing ? combat.LockedAim.x : arena.Player.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) > .15f) skin.flipX = dx < 0;
            }
        }

        void OnDestroy() { if (art != null) Destroy(art); }
    }
}
