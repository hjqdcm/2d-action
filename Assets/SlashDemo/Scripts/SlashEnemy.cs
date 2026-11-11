using UnityEngine;

namespace SlashDemo
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class SlashEnemy : MonoBehaviour
    {
        public bool trainingDummy;
        public int maxHealth = 60;
        public float moveSpeed = 1.9f;
        public float windupTime = 0.58f;
        public int damage = 12;
        public float attackRange = 1.15f;
        public bool rockBear;
        public int Health { get; private set; }
        public bool Alive => Health > 0;
        public bool IsChasing => Alive && !trainingDummy && (bearCombat != null ? bearCombat.IsChasing : snakeCombat != null ? snakeCombat.IsChasing : apeCombat != null ? apeCombat.IsChasing : cultivator != null ? cultivator.IsChasing : phase == Phase.Chase);
        public float Radius => GetComponent<CircleCollider2D>().radius;

        enum Phase { Chase, Windup, Recover, Dead }
        Phase phase;
        SlashArena arena;
        Rigidbody2D body;
        Transform visual, warning;
        ObliqueActor presentation;
        SpriteRenderer[] sprites;
        Color[] colors;
        Vector2 lockedAim;
        float timer, flash;
        float deathAge;
        const float DeathDuration = 0.26f;
        int baseHealth;
        float baseSpeed;
        BearDemon bear;
        BearCombat bearCombat;
        SnakeDemon snake;
        SnakeCombat snakeCombat;
        ApeDemon ape;
        ApeCombat apeCombat;
        AscensionCultivator cultivator;

        void Awake()
        {
            arena = GetComponentInParent<SlashArena>();
            body = GetComponent<Rigidbody2D>();
            bear = GetComponent<BearDemon>();
            snake = GetComponent<SnakeDemon>();
            ape = GetComponent<ApeDemon>();
            cultivator = GetComponent<AscensionCultivator>();
            visual = transform.Find("Visual");
            Health = maxHealth;
            presentation = GetComponent<ObliqueActor>();
            if (presentation == null) presentation = gameObject.AddComponent<ObliqueActor>();
            presentation.Initialize(arena, trainingDummy);
            warning = presentation.GroundWarning;
            if (warning != null)
            {
                float baseWidth = rockBear || snake != null || ape != null ? DemoArt.BeastAttackWarningBaseWidth : .045f;
                foreach (var line in warning.GetComponentsInChildren<LineRenderer>(true))
                    DemoArt.StyleAttackWarning(line, baseWidth);
            }
            sprites = visual.GetComponentsInChildren<SpriteRenderer>();
            colors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
            if (warning != null) warning.gameObject.SetActive(false);
            if (rockBear)
            {
                bearCombat = GetComponent<BearCombat>();
                if (bearCombat == null) bearCombat = gameObject.AddComponent<BearCombat>();
                bearCombat.Initialize(this, arena, presentation, warning);
            }
            if (snake != null)
            {
                snakeCombat = GetComponent<SnakeCombat>();
                if (snakeCombat == null) snakeCombat = gameObject.AddComponent<SnakeCombat>();
                snakeCombat.Initialize(this, arena, presentation, warning);
            }
            if (ape != null)
            {
                apeCombat = GetComponent<ApeCombat>();
                if (apeCombat == null) apeCombat = gameObject.AddComponent<ApeCombat>();
                apeCombat.Initialize(this, arena, presentation, warning);
            }
            if (cultivator != null) cultivator.InitializeCombat(arena, presentation, warning);
        }

        void Update()
        {
            if (arena.Paused || Time.deltaTime <= 0) return;
            if (!Alive)
            {
                deathAge += Time.deltaTime;
                float t = Mathf.Clamp01(deathAge / DeathDuration);
                for (int i = 0; i < sprites.Length; i++)
                {
                    Color color = colors[i];
                    color.a *= (1 - t) * (1 - t);
                    sprites[i].color = color;
                }
                presentation.SetPose(Vector2.zero, 0, t * -25);
                presentation.SetDeathProgress(t);
                if (t >= 1) Destroy(gameObject);
                return;
            }
            timer -= Time.deltaTime;
            flash = Mathf.Max(0, flash - Time.deltaTime);
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i].color = flash > 0 ? Color.white : colors[i];
                sprites[i].sortingOrder = i;
            }
            if (bearCombat != null) { bearCombat.Tick(Time.deltaTime); return; }
            if (snakeCombat != null) { snakeCombat.Tick(Time.deltaTime); return; }
            if (apeCombat != null) { apeCombat.Tick(Time.deltaTime); return; }
            if (cultivator != null) { cultivator.Tick(Time.deltaTime); return; }
            if (trainingDummy || arena.Player == null || !arena.Player.IsAlive) return;
            Vector2 toPlayer = arena.Player.transform.position - transform.position;
            if (phase == Phase.Recover)
            {
                float recover = phase == Phase.Recover ? Mathf.Clamp01(timer / 0.75f) : 0;
                presentation.SetPose(lockedAim * (0.15f * recover), 0, -8 * recover * lockedAim.x);
                if (timer <= 0) { phase = Phase.Chase; presentation.SetPose(Vector2.zero, 0, 0); }
                return;
            }
            if (phase == Phase.Windup)
            {
                float t = Mathf.Clamp01(1 - timer / windupTime);
                presentation.SetPose(-lockedAim * (0.10f * t), -0.035f * t, 7 * t * lockedAim.x);
                warning.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, t) * (attackRange / 1.15f);
                if (timer <= 0)
                {
                    warning.gameObject.SetActive(false);
                    if (SlashPlayer.InsideArc(transform.position, lockedAim, arena.Player.transform.position, attackRange, 105, 0.29f))
                        arena.Player.TakeDamage(damage, toPlayer.normalized);
                    arena.Effects.Burst((Vector2)transform.position + lockedAim * 0.65f, DemoArt.Red, 5, 0.5f);
                    if (rockBear) arena.Effects.Burst((Vector2)transform.position + lockedAim * 0.9f, DemoArt.Gold, 12, 0.8f);
                    phase = Phase.Recover;
                    timer = 0.75f;
                    presentation.SetPose(lockedAim * 0.15f, 0, -8 * lockedAim.x);
                }
            }
            else if (toPlayer.magnitude <= attackRange)
            {
                phase = Phase.Windup;
                timer = windupTime;
                lockedAim = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.right;
                warning.gameObject.SetActive(true);
                warning.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(lockedAim.y, lockedAim.x) * Mathf.Rad2Deg);
            }
            else presentation.SetPose(Vector2.zero, 0, 0);
        }

        void FixedUpdate()
        {
            if (!Alive || arena == null || arena.Paused) { body.linearVelocity = Vector2.zero; return; }
            if (bearCombat != null) { bearCombat.FixedTick(Vector2.zero); return; }
            if (cultivator != null) { cultivator.FixedTick(Time.fixedDeltaTime); return; }
            var navigation = snake != null ? snake.Navigation : ape?.Navigation;
            Vector2 velocity = Vector2.zero;
            if (IsChasing && arena.Player != null && arena.Player.IsAlive)
            {
                Vector2 toward = ((Vector2)arena.Player.transform.position - body.position).normalized;
                if (bear != null) toward = bear.ChaseDirection(body.position, arena.Player.transform.position);
                if (navigation != null) toward = navigation.ChaseDirection(body.position, arena.Player.transform.position);
                Vector2 separation = Vector2.zero;
                foreach (var other in arena.Enemies)
                {
                    if (other == this || other == null || !other.Alive || (other.apeCombat != null && other.apeCombat.IsAirborne)) continue;
                    Vector2 away = body.position - (Vector2)other.transform.position;
                    float gap = snake != null || ape != null ? Radius + other.Radius + .65f : 1.1f;
                    if (away.magnitude < gap && away.sqrMagnitude > 0.001f) separation += away.normalized * (gap - away.magnitude);
                }
                velocity += (toward + separation * 1.8f).normalized * moveSpeed;
            }
            body.linearVelocity = navigation != null ? navigation.ConstrainVelocity(velocity, Time.fixedDeltaTime) : velocity;
        }

        public void TakeHit(int amount, Vector2 direction, float force, bool heavy = false, bool interrupt = true)
        {
            if (!Alive || (apeCombat != null && apeCombat.IsAirborne)) return;
            if (amount > 0 && cultivator != null) amount = cultivator.MitigateHit(amount);
            Health = Mathf.Max(0, Health - amount);
            flash = 0.10f;
            // All enemies take damage without resetting attacks, poses or movement.
            // Keep force/interrupt arguments compatible with existing sword and skill callers.
            if (amount > 0)
                arena.Effects.DamageImpact(presentation, direction, DemoArt.Gold, heavy, !Alive);
            arena.Effects.Number(transform.position, amount.ToString(), amount >= 40 ? DemoArt.Gold : Color.white);
            arena.Effects.PlayHit(heavy, !Alive);
            arena.CameraRig.Punch(heavy || !Alive ? 1 : .35f);
            if (Alive)
            {
                if (amount > 0) cultivator?.OnSurvivedHit();
                return;
            }
            warning.gameObject.SetActive(false);
            phase = Phase.Dead;
            if (bearCombat != null) bearCombat.Stop();
            if (snakeCombat != null) snakeCombat.Stop();
            if (apeCombat != null) apeCombat.Stop();
            if (cultivator != null) cultivator.Stop();
            GetComponent<Collider2D>().enabled = false;
            body.linearVelocity = Vector2.zero;
            arena.Effects.Burst(transform.position, DemoArt.Gold, 18, 1.2f);
            arena.EnemyKilled(this);
            deathAge = 0;
            presentation.SetDeathProgress(0);
        }

        public void ApplyTuning(SlashTuningSettings settings)
        {
            // Record each enemy's wave-scaled baseline once, rather than multiplying it repeatedly.
            if (baseHealth == 0) { baseHealth = maxHealth; baseSpeed = moveSpeed; }
            int nextMax = cultivator != null ? cultivator.TunedMaxHealth(settings)
                : trainingDummy ? settings.dummyHealth : Mathf.Max(1, Mathf.RoundToInt(baseHealth * settings.enemyHealthMultiplier));
            if (Health > 0 && maxHealth != nextMax)
                Health = Mathf.Clamp(Mathf.RoundToInt(Health / (float)Mathf.Max(1, maxHealth) * nextMax), 1, nextMax);
            maxHealth = nextMax;
            moveSpeed = snake != null ? settings.moveSpeed * SnakeDemon.MoveSpeedScale
                : ape != null ? settings.moveSpeed * ApeDemon.MoveSpeedScale : baseSpeed * settings.enemySpeedMultiplier;
            if (cultivator != null) { cultivator.ApplyAttackTuning(settings); return; }
            windupTime = settings.enemyWindup * (rockBear ? 1.45f : snake != null ? SnakeDemon.WindupScale : ape != null ? ApeDemon.WindupScale : 1);
            damage = snake != null ? SnakeDemon.BaseDamage : ape != null ? ApeDemon.BaseDamage : settings.enemyDamage;
            attackRange = settings.enemyRange * (rockBear ? BearDemon.RangeScale : snake != null ? SnakeDemon.RangeScale : ape != null ? ApeDemon.RangeScale : 1);
        }
    }
}
