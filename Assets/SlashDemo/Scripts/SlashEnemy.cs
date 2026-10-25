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
        public int Health { get; private set; }
        public bool Alive => Health > 0;
        public float Radius => GetComponent<CircleCollider2D>().radius;

        enum Phase { Chase, Windup, Recover, Stunned, Dead }
        Phase phase;
        SlashArena arena;
        Rigidbody2D body;
        Transform visual, warning;
        ObliqueActor presentation;
        SpriteRenderer[] sprites;
        Color[] colors;
        Vector2 knockback, lockedAim;
        float timer, flash;
        float deathAge;
        const float DeathDuration = 0.26f;
        int baseHealth;
        float baseSpeed;

        void Awake()
        {
            arena = GetComponentInParent<SlashArena>();
            body = GetComponent<Rigidbody2D>();
            visual = transform.Find("Visual");
            Health = maxHealth;
            presentation = GetComponent<ObliqueActor>();
            if (presentation == null) presentation = gameObject.AddComponent<ObliqueActor>();
            presentation.Initialize(arena, trainingDummy);
            warning = presentation.GroundWarning;
            sprites = visual.GetComponentsInChildren<SpriteRenderer>();
            colors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
            if (warning != null) warning.gameObject.SetActive(false);
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
            if (trainingDummy || arena.Player == null || !arena.Player.IsAlive) return;
            Vector2 toPlayer = arena.Player.transform.position - transform.position;
            if (phase == Phase.Stunned || phase == Phase.Recover)
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
            knockback *= Mathf.Exp(-9 * Time.fixedDeltaTime);
            Vector2 velocity = knockback;
            if (!trainingDummy && phase == Phase.Chase && arena.Player != null && arena.Player.IsAlive)
            {
                Vector2 toward = ((Vector2)arena.Player.transform.position - body.position).normalized;
                Vector2 separation = Vector2.zero;
                foreach (var other in arena.Enemies)
                {
                    if (other == this || other == null || !other.Alive) continue;
                    Vector2 away = body.position - (Vector2)other.transform.position;
                    if (away.sqrMagnitude < 1.1f && away.sqrMagnitude > 0.001f) separation += away.normalized * (1.1f - away.magnitude);
                }
                velocity += (toward + separation * 1.8f).normalized * moveSpeed;
            }
            body.linearVelocity = velocity;
        }

        public void TakeHit(int amount, Vector2 direction, float force, bool heavy = false)
        {
            if (!Alive) return;
            Health = Mathf.Max(0, Health - amount);
            flash = 0.10f;
            knockback = direction * (trainingDummy ? force * 0.1f : force);
            phase = Phase.Stunned;
            timer = 0.32f;
            warning.gameObject.SetActive(false); // A hit interrupts a telegraphed attack.
            presentation.SetPose(Vector2.zero, 0, 0);
            arena.Effects.Burst(transform.position, trainingDummy ? DemoArt.Gold : DemoArt.Jade, 10);
            arena.Effects.Number(transform.position, amount.ToString(), amount >= 40 ? DemoArt.Gold : Color.white);
            arena.Effects.PlayHit(heavy, !Alive);
            arena.Effects.Impact(transform.position, heavy, !Alive);
            if (Alive) return;
            phase = Phase.Dead;
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
            int nextMax = trainingDummy ? settings.dummyHealth : Mathf.Max(1, Mathf.RoundToInt(baseHealth * settings.enemyHealthMultiplier));
            if (Health > 0 && maxHealth != nextMax)
                Health = Mathf.Clamp(Mathf.RoundToInt(Health / (float)Mathf.Max(1, maxHealth) * nextMax), 1, nextMax);
            maxHealth = nextMax;
            moveSpeed = baseSpeed * settings.enemySpeedMultiplier;
            windupTime = settings.enemyWindup;
            damage = settings.enemyDamage;
            attackRange = settings.enemyRange;
        }
    }
}
