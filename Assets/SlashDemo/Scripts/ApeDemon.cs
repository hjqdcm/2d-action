using UnityEngine;

namespace SlashDemo
{
    public sealed class ApeDemon : MonoBehaviour
    {
        public const string ArtResource = "SlashDemo/MountainApe-v1";
        public const int WildernessCount = 3, BaseHealth = 200, BaseDamage = 10;
        public const float Height = 2.5f, Radius = .86f, MoveSpeedScale = .60f;
        public const float RangeScale = BearDemon.RangeScale * .82f, WindupScale = 1.12f;
        public WildernessNavigation Navigation { get; private set; }
        public int FormationSlot { get; private set; }
        Sprite art;
        SpriteRenderer skin;
        SlashArena arena;
        SlashEnemy enemy;
        ApeCombat combat;

        public static void SpawnPack(SlashArena arena, TravelMapEnvironment map)
        {
            for (int i = 0; i < WildernessCount; i++)
            {
                var enemy = Spawn(arena, map, map.Definition.Ground(510 + i * 300, 360), i);
                enemy.name = "山魈猿妖 " + (i + 1);
            }
        }

        public static SlashEnemy Spawn(SlashArena arena, TravelMapEnvironment map, Vector2 position, int slot = 0)
        {
            var texture = Resources.Load<Texture2D>(ArtResource);
            if (texture == null) throw new System.InvalidOperationException("Missing ape artwork: " + ArtResource);
            var go = new GameObject("山魈猿妖"); go.SetActive(false);
            go.transform.SetParent(map.transform, false); go.transform.position = position;
            var body = DemoArt.Body(go, Radius, 5);
            body.bodyType = RigidbodyType2D.Kinematic; body.useFullKinematicContacts = true;
            var ape = go.AddComponent<ApeDemon>(); ape.arena = arena; ape.FormationSlot = slot;
            ape.Navigation = go.AddComponent<WildernessNavigation>(); ape.Navigation.Initialize(map, body, Radius);
            var visual = new GameObject("Visual").transform; visual.SetParent(go.transform, false);
            DemoArt.Shape("Shadow", visual, "Disk", Vector2.zero, Vector2.one, new Color(0, 0, 0, .32f));
            ape.art = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .065f),
                texture.height / Height, 0, SpriteMeshType.FullRect);
            ape.skin = new GameObject("Mountain ape skin").AddComponent<SpriteRenderer>();
            ape.skin.transform.SetParent(visual, false); ape.skin.sprite = ape.art; ape.skin.sharedMaterial = DemoArt.Material;
            go.AddComponent<ApeAnimation>().Initialize(ape.skin, arena);
            DemoArt.Shape("HealthBack", go.transform, "Square", Vector2.zero, Vector2.one, new Color(.035f, .055f, .065f), 500);
            DemoArt.Shape("HealthFill", go.transform, "Square", Vector2.zero, Vector2.one, DemoArt.Red, 501);
            var warning = new GameObject("AttackWarning").transform; warning.SetParent(go.transform, false);
            var points = new Vector3[26];
            for (int i = 1; i < points.Length - 1; i++)
                points[i] = DemoArt.Direction(Mathf.Lerp(-57.5f, 57.5f, (i - 1) / 23f)) * 1.15f;
            DemoArt.Line("Ape palm warning", warning, points, DemoArt.AttackWarningColor, .05f * DemoArt.AttackWarningWidthScale, 10);
            warning.gameObject.SetActive(false);
            var enemy = go.AddComponent<SlashEnemy>();
            enemy.maxHealth = BaseHealth; enemy.damage = BaseDamage; enemy.moveSpeed = arena.Player.moveSpeed * MoveSpeedScale;
            enemy.windupTime = .58f * WindupScale; enemy.attackRange = 1.15f * RangeScale;
            ape.enemy = enemy;
            var actor = go.AddComponent<ObliqueActor>(); actor.CreatureHeight = Height; actor.ShadowScale = .85f; actor.Initialize(arena);
            go.SetActive(true); ape.combat = go.GetComponent<ApeCombat>();
            arena.RegisterEnemy(enemy); if (arena.Tuning != null) arena.Tuning.ApplyEnemy(enemy);
            return enemy;
        }

        void LateUpdate()
        {
            if (arena == null || arena.Paused || enemy == null || !enemy.Alive || arena.Player == null) return;
            float dx = combat != null && !combat.IsChasing ? combat.LockedAim.x
                : arena.Player.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > .15f) skin.flipX = dx < 0;
        }
        void OnDestroy() { if (art != null) Destroy(art); }
    }
}
