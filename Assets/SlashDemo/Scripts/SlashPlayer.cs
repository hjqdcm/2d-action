using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    public enum CultivationRealm
    {
        QiRefining,
        FoundationEstablishment,
        GoldenCore,
        NascentSoul
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class SlashPlayer : MonoBehaviour
    {
        const float AttackMoveMultiplier = 0.5f;
        const float ComboRecoveryDuration = 0.1f;
        float comboRecoveryLeft;
        public bool IsComboRecovering => comboRecoveryLeft > 0;

        [System.Serializable]
        public class Strike
        {
            public int damage;
            public float reach, arc, duration, knockback;
            public SwordAction action;
            public float windupRatio, activeRatio, bladeWidth;
            [HideInInspector] public float stepDistance; // Retained only to read older serialized tuning.
            public int motionVersion;
            public Strike(int damage, float reach, float arc, float duration, float knockback)
            { this.damage = damage; this.reach = reach; this.arc = arc; this.duration = duration; this.knockback = knockback; }
            public Strike(int damage, float reach, float arc, float duration, float knockback,
                SwordAction action, float windupRatio, float activeRatio, float stepDistance, float bladeWidth)
                : this(damage, reach, arc, duration, knockback)
            {
                this.action = action; this.windupRatio = windupRatio; this.activeRatio = activeRatio;
                this.stepDistance = stepDistance; this.bladeWidth = bladeWidth; motionVersion = 5;
            }
            public Strike Clone() => (Strike)MemberwiseClone();
        }

        [Header("Movement")]
        [Min(0.1f)] public float moveSpeed = 5.4f;
        [Min(0.01f)] public float dashSpeed = 17;
        [Min(0.01f)] public float dashDuration = 0.15f;
        [Min(0.01f)] public float dashCooldown = 0.75f;
        [Header("Combat")]
        public int maxHealth = 100;
        public bool invincible;
        public float comboResetTime = 0.65f;
        [Range(0.10f, 0.14f)] public float trailLifetime = 0.12f;
        [Range(0.05f, 0.18f)] public float trailGlowWidth = 0.10f;
        [Range(0.4f, 1)] public float trailCrescentPower = 0.7f;
        [Range(1, 3)] public float trailHDRIntensity = 2.2f;
        public Strike[] strikes = SwordMotion.Defaults();
        [Header("Character appearance")]
        [Tooltip("进入 Play 前选择。开启使用真正连续转向的立体原型；关闭恢复八方向贴图。")]
        public bool useContinuous3D = false;
        [Header("Drawing and sheathing")]
        [Min(0.01f)] public float drawDuration = 0.20f, sheathDuration = 0.26f;
        [Min(0)] public float sheathDelay = 0.18f;
        readonly SwordEquipState equipment = new SwordEquipState();
        public bool SwordSheathed => equipment.Sheathed;
        public bool ScabbardVisible => equipment.ScabbardVisible;
        public bool IsDrawingSword => !equipment.Ready && (IsAttacking || IsHeavyBusy);
        [Header("Charged heavy attack")]
        public SlashTuningSettings.HeavyAttackSettings heavyAttack = new SlashTuningSettings.HeavyAttackSettings();
        readonly ChargedAttackState heavyState = new ChargedAttackState();
        readonly HeavyReleaseState heavyRelease = new HeavyReleaseState();
        SlashTuningSettings.HeavyAttackSettings chargedSettings;
        bool heavyPressedQueued, heavyReleaseQueued, chargeReadyFeedback;
        Vector2 heavyAim;
        public bool IsCharging => heavyState.Charging;
        public bool IsHeavyRecovering => heavyRelease.Active;
        public bool IsHeavyReleasePaused => heavyRelease.Active && heavyRelease.Age < HeavyReleaseState.Pause;
        public bool IsHeavySweeping => heavyRelease.Active && heavyRelease.Age >= HeavyReleaseState.Pause
            && heavyRelease.Age < HeavyReleaseState.Pause + HeavyReleaseState.Sweep;
        public bool IsHeavyBusy => heavyState.Busy || heavyRelease.Active;
        public float ChargeProgress => heavyState.Progress;
        public bool HeavyReady => heavyState.Ready && equipment.Ready;
        public int TotalSwordWaves { get; private set; }
        public int Health { get; private set; }
        public CultivationRealm Realm { get; private set; } = CultivationRealm.QiRefining;
        public string RealmDisplayName => GetRealmName(Realm);
        public TechniqueDefinition Technique => TechniqueCatalog.FlowingCloud;
        public SlashTuningSettings.FlowingCloudSettings FlowingCloud { get; private set; } = new SlashTuningSettings.FlowingCloudSettings();
        readonly FlowingCloudCombo techniqueCombo = new FlowingCloudCombo();
        readonly FlowingCloudRebuildReward rebuildReward = new FlowingCloudRebuildReward();
        public bool RebuildCooldownRewardPending => rebuildReward.Pending;
        public int ComboStacks => techniqueCombo.Stacks;
        public float ComboStackTimeLeft => techniqueCombo.StackTimeLeft(Time.time);
        public float ComboDamageBonus => techniqueCombo.DamageMultiplier(Realm) - 1;
        public float NormalAttackSpeedMultiplier => techniqueCombo.AttackSpeedMultiplier(Realm);
        public void CollectBuffs(CombatBuffBar buffs)
        {
            buffs.Clear();
            if (!IsAlive) return;
            if (ComboStacks > 0 && ComboStackTimeLeft > 0)
            {
                string effects = "每层叠浪使普攻伤害提升" + (FlowingCloud.stackBonus * 100).ToString("0.#") + "%";
                if (Realm >= CultivationRealm.FoundationEstablishment)
                    effects += "、普攻攻速提升" + (FlowingCloud.attackSpeedPerStack * 100).ToString("0.#") + "%";
                effects += "，叠满5层时，额外提升" + (FlowingCloudRules.FullStackDamageBonus * 100).ToString("0.#") + "%的普攻伤害";
                buffs.AddOrUpdate(new CombatBuffView("flowing-cloud-stacks", "叠浪", ComboStacks,
                    "SlashDemo/FlowingCloudBuffIcon", effects, ComboStackTimeLeft, FlowingCloudRules.StackDuration));
            }
            // Register future active buffs here; their screen positions belong only to BuffBarLayout.
        }
        float normalStrikeSpeed = 1;
        public bool IsTechniqueActive { get; private set; }
        public bool IsPursuit => IsAttacking && !IsTechniqueActive && ComboIndex == 3;
        public float TechniqueCooldownLeft => techniqueCooldownLeft;
        public bool TechniqueReady => IsAlive && techniqueCooldownLeft <= 0;
        public int TotalTechniqueCasts { get; private set; }
        public int TotalPursuits { get; private set; }
        public int TechniqueConsumedStacks { get; private set; }
        public int TotalTechniqueFinishers { get; private set; }
        public bool IsTechniqueFinisher { get; private set; }
        Strike skillFinisherStrike;
        SlashTuningSettings.FlowingCloudSettings skillSettings;
        bool skillRushFinished;
        float skillWidth;
        float techniqueCooldownLeft, skillRushRemaining, skillRushSpeed;
        bool skillPressedQueued, whirlwindEmitted, queuedExplicit;
        int queuedSegment = -1;
        public int ComboIndex { get; private set; } = -1;
        public bool IsAttacking { get; private set; }
        public bool IsDashing => dashRemaining > 0;
        public bool IsAlive => Health > 0;
        public float DashReady => Mathf.Clamp01(1 - dashCooldownLeft / Mathf.Max(0.01f, dashCooldown));
        public Vector2 Aim { get; private set; } = Vector2.right;
        public int TotalSwings { get; private set; }

        SlashArena arena;
        Rigidbody2D body;
        Transform visual, swordPivot;
        Transform armRig;
        ObliqueActor presentation;
        HeroAppearance appearance;
        SpriteRenderer[] renderers;
        SpriteRenderer[] weaponRenderers;
        SpriteRenderer upperArmL, forearmL, upperArmR, forearmR, gripHand, supportHand;
        Transform swordBlade;
        Transform scabbardRig;
        SpriteRenderer[] scabbardRenderers;
        Sprite scabbardSprite;
        Strike activeStrike;
        Vector2 previousAttackOrigin;
        float renderedElevation = -1, renderedScale = -1;
        bool swingSoundPlayed;
        struct TrailPoint { public Vector2 tip, inner; public float born, life, intensity, heat; public bool heavy, closed; }
        readonly List<TrailPoint> trail = new List<TrailPoint>(48);
        readonly CombatInputBuffer inputBuffer = new CombatInputBuffer();
        Vector2 queuedAim;
        Mesh trailMesh;
        Material trailMaterial;
        MeshRenderer trailRenderer;
        readonly List<Vector3> trailVertices = new List<Vector3>(400);
        readonly List<Color> trailColors = new List<Color>(400);
        readonly List<Vector2> trailEmission = new List<Vector2>(400);
        readonly List<int> trailTriangles = new List<int>(1896);
        Color[] colors;
        readonly List<Collider2D> overlaps = new List<Collider2D>(24);
        readonly HashSet<SlashEnemy> hitThisSwing = new HashSet<SlashEnemy>();
        readonly List<Collider2D> rushIgnoredColliders = new List<Collider2D>(24);
        Vector2 move, dashDirection, recoil, attackAim;
        float attackAge, lastAttackEnd = -10, dashRemaining, dashCooldownLeft, invulnerability, flash, ghostTimer;
        bool queued;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            visual = transform.Find("Visual");
            swordPivot = transform.Find("SwordPivot");
            arena = GetComponentInParent<SlashArena>();
            presentation = GetComponent<ObliqueActor>();
            if (presentation == null) presentation = gameObject.AddComponent<ObliqueActor>();
            presentation.Initialize(arena);
            appearance = GetComponent<HeroAppearance>();
            if (appearance == null) appearance = gameObject.AddComponent<HeroAppearance>();
            appearance.Initialize(visual, useContinuous3D);
            renderers = visual.GetComponentsInChildren<SpriteRenderer>();
            colors = new Color[renderers.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = renderers[i].color;
            Health = maxHealth;
            for (int i = 0; i < strikes.Length; i++) SwordMotion.UpgradeLegacy(strikes[i], i);
            ConfigureWeapon();
            AnimateSword();
        }

        void Update()
        {
            if (!IsAlive || arena == null || arena.Paused)
            {
                inputBuffer.Clear(); queued = false; move = Vector2.zero;
                if (IsCharging || !IsAlive) CancelHeavyAttack();
                heavyPressedQueued = heavyReleaseQueued = skillPressedQueued = false;
                return;
            }
            float dt = Time.deltaTime;
            bool inputBlocked = arena.InputBlocked;
            move = inputBlocked ? Vector2.zero : arena.View.Movement(DemoInput.Move);
            if (inputBlocked)
            {
                queued = false; inputBuffer.Clear();
                if (IsCharging) CancelHeavyAttack();
                heavyPressedQueued = heavyReleaseQueued = skillPressedQueued = false;
            }
            if (!inputBlocked && DemoInput.MouseAvailable)
            {
                if (arena.View.MouseGround(arena.WorldCamera, DemoInput.Pointer, out Vector2 ground))
                {
                    Vector2 delta = ground - (Vector2)transform.position;
                    if (delta.sqrMagnitude > 0.01f) Aim = delta.normalized;
                }
            }
            bool frozen = dt <= 0 || arena.Effects.Frozen;
            if (!inputBlocked)
            {
                if (DemoInput.AttackPressed && !IsHeavyBusy) inputBuffer.Attack(Aim, Time.time);
                if (DemoInput.SkillPressed) skillPressedQueued = true;
                if (DemoInput.HeavyPressed) heavyPressedQueued = true;
                if (DemoInput.HeavyReleased) heavyReleaseQueued = true;
                if (DemoInput.DashPressed) inputBuffer.Dash(move.sqrMagnitude > 0 ? move : Aim);
            }
            // Poll edges above even at timeScale == 0; only consume once simulation resumes.
            if (frozen) return;
            dashCooldownLeft = Mathf.Max(0, dashCooldownLeft - dt);
            invulnerability = Mathf.Max(0, invulnerability - dt);
            flash = Mathf.Max(0, flash - dt);
            comboRecoveryLeft = Mathf.Max(0, comboRecoveryLeft - dt);
            techniqueCooldownLeft = Mathf.Max(0, techniqueCooldownLeft - dt);
            techniqueCombo.ExpireStacks(Time.time);
            if (!IsAttacking && Time.time - lastAttackEnd > comboResetTime)
            {
                techniqueCombo.ResetSequence();
                ComboIndex = -1;
            }
            if (inputBuffer.HasDash && TryDash(inputBuffer.DashDirection))
                inputBuffer.Clear(); // A dash cancels the attack, including pending attack input.
            if (skillPressedQueued)
            {
                skillPressedQueued = false;
                TryTechniqueSkill(Aim);
            }

            equipment.Advance(dt, IsAttacking || IsHeavyBusy || IsComboRecovering, drawDuration, sheathDuration, sheathDelay);

            if (IsDashing)
            {
                heavyPressedQueued = heavyReleaseQueued = false;
                dashRemaining = Mathf.Max(0, dashRemaining - dt);
                ghostTimer -= dt;
                if (ghostTimer <= 0) { arena.Effects.Ghost(transform); ghostTimer = 0.025f; }
            }
            else
            {
                bool heavyFrame = UpdateHeavyAttack(dt);
                if (!heavyFrame)
                {
                    bool acceptedBufferedAttack = false;
                    if (!IsComboRecovering && !IsAttacking && queued)
                    {
                        queued = false;
                        BeginAttack(queuedAim, queuedSegment);
                        queuedSegment = -1; queuedExplicit = false;
                        inputBuffer.ConsumeAttack();
                        acceptedBufferedAttack = true;
                    }
                    if (!IsComboRecovering && inputBuffer.HasAttack && (!IsAttacking || attackAge > CurrentStrike.duration * 0.4f))
                    {
                        RequestAttack(inputBuffer.AttackDirection, true, inputBuffer.AttackTime);
                        inputBuffer.ConsumeAttack();
                        acceptedBufferedAttack = true;
                    }
                    if (!inputBlocked && !acceptedBufferedAttack && !inputBuffer.HasAttack && DemoInput.AttackHeld) RequestAttack(Aim, false);
                    if (IsAttacking && equipment.Ready)
                    {
                        float previousAge = attackAge;
                        attackAge += dt;
                        float start = CurrentStrike.duration * SwordMotion.SwingStart(CurrentStrike);
                        if (!swingSoundPlayed && attackAge >= start)
                        { arena.Effects.PlaySwing(CurrentStrike.action == SwordAction.WideCleave); swingSoundPlayed = true; }
                        if (IsTechniqueActive && !IsTechniqueFinisher) UpdateTechniqueRush();
                        else
                        {
                            ResolveHits(previousAge, attackAge);
                            RecordTrail(previousAge, attackAge);
                        }
                        previousAttackOrigin = body.position;
                        if (attackAge >= CurrentStrike.duration && (!IsTechniqueActive || skillRushFinished)
                            && (!IsPursuit || !arena.Effects.HasWhirlwind(this)))
                        {
                            IsAttacking = false;
                            if (IsTechniqueActive)
                            {
                                IsTechniqueActive = false;
                                IsTechniqueFinisher = false;
                                rebuildReward.Arm(Realm);
                                skillRushRemaining = 0;
                                lastAttackEnd = Time.time;
                                ComboIndex = -1;
                                body.linearVelocity = Vector2.zero;
                            }
                            else
                            {
                                techniqueCombo.FinishStrike(Time.time);
                                lastAttackEnd = Time.time;
                                if (ComboIndex >= 2) comboRecoveryLeft = ComboRecoveryDuration / normalStrikeSpeed;
                                else if (queued)
                                {
                                    queued = false;
                                    BeginAttack(queuedAim, queuedSegment);
                                    queuedSegment = -1; queuedExplicit = false;
                                }
                            }
                        }
                    }
                }
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = i;
                renderers[i].color = flash > 0 ? Color.white : colors[i];
                if (invulnerability > 0 && !IsDashing && flash <= 0)
                {
                    var c = renderers[i].color;
                    c.a *= 0.55f + 0.45f * Mathf.Sin(Time.time * 55);
                    renderers[i].color = c;
                }
            }
            if (appearance.Continuous3D)
                appearance.ContinuousModel.SetFeedback(flash, invulnerability > 0 && !IsDashing && flash <= 0
                    ? 0.55f + 0.45f * Mathf.Sin(Time.time * 55) : 1);
            // Consume thawed commands before ageing the window, including after a long frame.
            inputBuffer.Advance(Time.unscaledDeltaTime, arena.Effects.Frozen);
            appearance.AdvanceLocomotion(body.position, dt, IsDashing, arena.View);
            if (IsCharging && equipment.Ready)
                AddHeavyTrail(SwordMotion.HeavyPose(ChargeProgress, false, 0, 0, Time.time), Aim, Time.time, 0.26f, 0.3f, 0);
            AnimateSword();
        }

        Strike CurrentStrike => activeStrike ?? strikes[Mathf.Clamp(ComboIndex, 0, strikes.Length - 1)];

        public bool RequestAttack(Vector2 direction, bool explicitPress = true, float pressedAt = -1)
        {
            if (!IsAlive || IsDashing || IsHeavyBusy || IsTechniqueActive || arena.Paused || arena.InputBlocked || strikes.Length == 0) return false;
            if (pressedAt < 0) pressedAt = Time.time;
            int next = techniqueCombo.NextSegment(ComboIndex,
                !IsAttacking && pressedAt - lastAttackEnd > comboResetTime, Realm);
            if (IsComboRecovering)
            {
                QueueAttack(direction, next, explicitPress);
                return false;
            }
            if (IsAttacking)
            {
                if (attackAge > CurrentStrike.duration * 0.4f)
                    QueueAttack(direction, next, explicitPress);
                return false;
            }
            BeginAttack(direction, next);
            return true;
        }

        void QueueAttack(Vector2 direction, int next, bool explicitPress)
        {
            // Holding the button must not replace a deliberate fourth press.
            if (queued && queuedExplicit && !explicitPress) return;
            queued = true; queuedSegment = next; queuedExplicit = explicitPress;
            queuedAim = direction.sqrMagnitude > 0.001f ? direction.normalized : Aim;
        }

        void BeginAttack(Vector2 direction, int segment)
        {
            techniqueCombo.ExpireStacks(Time.time);
            if (Time.time - lastAttackEnd > comboResetTime && !IsAttacking) techniqueCombo.ResetSequence();
            // A buffered fourth input may precede contact. Resolve its hit prerequisite at execution.
            if (ComboIndex == 2 && Realm >= CultivationRealm.GoldenCore)
                segment = techniqueCombo.NextSegment(2, Time.time - lastAttackEnd > comboResetTime, Realm);
            else if (segment == 3) segment = 0;
            ComboIndex = Mathf.Clamp(segment, 0, Realm >= CultivationRealm.GoldenCore ? 3 : 2);
            attackAim = direction.sqrMagnitude < 0.001f ? Aim : direction.normalized;
            attackAge = 0;
            activeStrike = ComboIndex == 3
                ? new Strike(FlowingCloud.pursuitDamage, FlowingCloud.pursuitReach, 210, FlowingCloud.pursuitDuration, FlowingCloud.pursuitKnockback,
                    SwordAction.SpinCleave, 0.16f, 0.46f, 0, 0.12f)
                : strikes[Mathf.Min(ComboIndex, strikes.Length - 1)].Clone();
            activeStrike.damage = Mathf.RoundToInt(activeStrike.damage * techniqueCombo.DamageMultiplier(Realm));
            // Snapshot existing layers at attack start; speed changes the whole normal swing and recovery.
            // The fourth normal strike shares the modifier; Q's copied third strike does not.
            normalStrikeSpeed = NormalAttackSpeedMultiplier;
            activeStrike.duration /= normalStrikeSpeed;
            techniqueCombo.BeginStrike();
            whirlwindEmitted = false;
            if (ComboIndex == 3) TotalPursuits++;
            previousAttackOrigin = body.position;
            // Apply the attack slowdown immediately, including between physics ticks.
            body.linearVelocity = move * moveSpeed * AttackMoveMultiplier + recoil;
            swingSoundPlayed = false;
            ClearTrail();
            IsAttacking = true;
            TotalSwings++;
            hitThisSwing.Clear();
        }

        public bool TryDash(Vector2 direction)
        {
            if (!IsAlive || IsDashing || dashCooldownLeft > 0 || arena.Paused || arena.InputBlocked) return false;
            CancelHeavyAttack();
            InterruptCombo(dodging: true);
            dashDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Aim;
            dashRemaining = dashDuration;
            dashCooldownLeft = dashCooldown;
            invulnerability = Mathf.Max(invulnerability, dashDuration);
            IsAttacking = false;
            comboRecoveryLeft = 0;
            queued = false;
            inputBuffer.Clear();
            lastAttackEnd = -10;
            ClearTrail();
            return true;
        }

        public bool TryTechniqueSkill(Vector2 direction)
        {
            if (!TechniqueReady || IsTechniqueActive || IsDashing || IsHeavyBusy || arena.Paused || arena.InputBlocked) return false;
            rebuildReward.Clear();
            arena.Effects.CancelWhirlwinds(this);
            IgnoreRushEnemyCollisions();
            TechniqueConsumedStacks = techniqueCombo.ConsumeStacks(Time.time);
            skillSettings = FlowingCloud.Clone();
            skillFinisherStrike = strikes[Mathf.Min(2, strikes.Length - 1)].Clone();
            IsTechniqueFinisher = false;
            skillWidth = skillSettings.ActiveWidth(TechniqueConsumedStacks);
            skillRushFinished = false;
            ComboIndex = -1;
            lastAttackEnd = -10;
            attackAim = direction.sqrMagnitude > 0.001f ? direction.normalized : Aim;
            activeStrike = new Strike(skillSettings.ActiveDamage(Realm, TechniqueConsumedStacks), skillSettings.activeReach, 115,
                Mathf.Max(skillSettings.activeDuration, skillSettings.rushDuration), skillSettings.activeKnockback,
                SwordAction.Cleave, 0.10f, 0.50f, 0, 0.12f);
            attackAge = 0; previousAttackOrigin = body.position;
            swingSoundPlayed = false;
            IsAttacking = IsTechniqueActive = true;
            comboRecoveryLeft = 0;
            skillRushRemaining = Mathf.Max(0.01f, skillSettings.rushDuration);
            skillRushSpeed = skillSettings.RushDistance(TechniqueConsumedStacks) / skillRushRemaining;
            techniqueCooldownLeft = FlowingCloud.activeCooldown;
            recoil = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            equipment.Advance(drawDuration, true, drawDuration, sheathDuration, sheathDelay);
            queued = queuedExplicit = false; queuedSegment = -1;
            inputBuffer.Clear(); hitThisSwing.Clear(); ClearTrail();
            TotalTechniqueCasts++;
            arena.Effects.Ghost(transform);
            AnimateSword();
            return true;
        }

        void UpdateTechniqueRush()
        {
            if (skillRushFinished) return;
            Vector2 from = previousAttackOrigin, to = body.position;
            // Sweep actual physics displacement, including the final step and movement at low frame rates.
            float radius = skillWidth * 0.5f;
            Physics2D.OverlapCircle((from + to) * 0.5f, Vector2.Distance(from, to) * 0.5f + radius + 0.7f,
                ContactFilter2D.noFilter, overlaps);
            foreach (var collider in overlaps)
            {
                var enemy = collider.GetComponent<SlashEnemy>();
                if (enemy == null || !enemy.Alive || hitThisSwing.Contains(enemy)
                    || SwordMotion.DistanceToBlade(enemy.transform.position, from, to, out _) > radius + enemy.Radius) continue;
                hitThisSwing.Add(enemy);
                enemy.TakeHit(activeStrike.damage, attackAim, activeStrike.knockback);
            }
            arena.Effects.DrawCloudRush(from, to, attackAim, skillWidth, trailHDRIntensity, skillSettings.rushTrailLifetime);
            if ((to - from).sqrMagnitude > 0.001f)
            {
                ghostTimer -= Time.deltaTime;
                if (ghostTimer <= 0) { arena.Effects.Ghost(transform); ghostTimer = 0.035f; }
            }
            if (skillRushRemaining > 0) return;
            skillRushFinished = true;
            body.linearVelocity = Vector2.zero;
            RestoreRushEnemyCollisions();
            if (TechniqueConsumedStacks < FlowingCloudRules.MaxStacks) return;
            // Run the actual third normal strike, including its animation, sword trail and swept hit timing.
            // Keep it inside Q so it cannot rebuild stacks or unlock a passive follow-up.
            TotalTechniqueFinishers++;
            IsTechniqueFinisher = true;
            activeStrike = skillFinisherStrike;
            attackAge = 0;
            swingSoundPlayed = false;
            hitThisSwing.Clear();
            ClearTrail();
        }

        void IgnoreRushEnemyCollisions()
        {
            var playerCollider = GetComponent<CircleCollider2D>();
            foreach (var enemy in arena.Enemies)
            {
                if (enemy == null || !enemy.Alive) continue;
                foreach (var collider in enemy.GetComponentsInChildren<Collider2D>())
                {
                    if (Physics2D.GetIgnoreCollision(playerCollider, collider)) continue;
                    Physics2D.IgnoreCollision(playerCollider, collider, true);
                    rushIgnoredColliders.Add(collider);
                }
            }
        }

        void RestoreRushEnemyCollisions()
        {
            var playerCollider = GetComponent<CircleCollider2D>();
            foreach (var collider in rushIgnoredColliders)
                if (collider != null) Physics2D.IgnoreCollision(playerCollider, collider, false);
            rushIgnoredColliders.Clear();
        }

        void InterruptCombo(bool dodging = false)
        {
            RestoreRushEnemyCollisions();
            if (arena != null && arena.Effects != null) arena.Effects.CancelWhirlwinds(this);
            if (dodging) techniqueCombo.ApplyDodge(Time.time);
            else techniqueCombo.Clear();
            ComboIndex = -1; lastAttackEnd = -10;
            IsAttacking = IsTechniqueActive = false;
            IsTechniqueFinisher = false;
            skillRushRemaining = comboRecoveryLeft = 0;
            queued = queuedExplicit = skillPressedQueued = false; queuedSegment = -1;
            inputBuffer.Clear();
            activeStrike = null;
            ClearTrail();
        }

        public void ResetTechniqueCombo() => InterruptCombo();
        public void ResetTechniqueCooldown() => techniqueCooldownLeft = 0;
        public void FillTechniqueStacksForDebug()
        {
            InterruptCombo();
            for (int i = 0; i < FlowingCloud.maxStacks; i++)
            {
                techniqueCombo.BeginStrike();
                RegisterTechniqueHit(0);
            }
            techniqueCombo.ResetSequence();
        }

        public bool RequestHeavyAttack()
        {
            if (!IsAlive || IsAttacking || IsComboRecovering || IsDashing || IsHeavyBusy || arena.Paused || arena.InputBlocked) return false;
            InterruptCombo();
            chargedSettings = (heavyAttack ?? new SlashTuningSettings.HeavyAttackSettings()).Clone();
            chargedSettings.Validate(new SlashTuningSettings.HeavyAttackSettings());
            if (!heavyState.Begin(chargedSettings.chargeTime, chargedSettings.recoveryTime)) return false;
            chargeReadyFeedback = false;
            queued = false;
            inputBuffer.ConsumeAttack();
            body.linearVelocity = Vector2.zero;
            ClearTrail();
            return true;
        }

        public bool ReleaseHeavyAttack()
        {
            if (!IsAlive || arena.Paused || arena.Effects.Frozen || !IsCharging) return false;
            arena.Effects.StopChargeGather();
            if (!equipment.Ready) { heavyState.Cancel(); return false; }
            bool fire = heavyState.Release();
            if (fire)
            {
                heavyAim = Aim;
                heavyState.Cancel();
                heavyRelease.Begin(chargedSettings.recoveryTime);
                lastAttackEnd = -10;
            }
            return fire;
        }

        public void CancelHeavyAttack()
        {
            if (arena != null && arena.Effects != null) arena.Effects.StopChargeGather();
            if (appearance != null) appearance.SetWeaponCharge(0);
            heavyState.Cancel();
            heavyRelease.Cancel();
            ClearTrail();
            heavyPressedQueued = heavyReleaseQueued = false;
        }

        bool UpdateHeavyAttack(float dt)
        {
            bool handled = IsHeavyBusy;
            if (heavyPressedQueued)
            {
                heavyPressedQueued = false;
                handled |= RequestHeavyAttack();
            }
            heavyState.Advance(dt);
            if (heavyRelease.Active)
            {
                float previous = heavyRelease.Age;
                bool emit = heavyRelease.Advance(dt);
                float start = Mathf.Max(previous, HeavyReleaseState.Pause);
                float end = Mathf.Min(heavyRelease.Age, HeavyReleaseState.Pause + HeavyReleaseState.Sweep);
                if (end > start)
                {
                    int samples = Mathf.Clamp(Mathf.CeilToInt((end - start) / HeavyReleaseState.Sweep * 40), 1, 40);
                    for (int i = 0; i <= samples; i++)
                    {
                        float age = Mathf.Lerp(start, end, i / (float)samples);
                        float s = (age - HeavyReleaseState.Pause) / HeavyReleaseState.Sweep;
                        float born = Time.time - (previous + dt - age);
                        AddHeavyTrail(SwordMotion.HeavyPose(1, true, s, 0, born), heavyAim, born, 0.18f, 1, s);
                    }
                }
                if (emit)
                {
                    arena.Effects.LaunchSwordWave(body.position + heavyAim * 0.4f, heavyAim, chargedSettings);
                    arena.Effects.PlaySwing(true);
                    TotalSwordWaves++;
                }
            }
            if (IsCharging)
            {
                inputBuffer.ConsumeAttack();
                if (HeavyReady && !chargeReadyFeedback)
                {
                    chargeReadyFeedback = true;
                    arena.Effects.Burst(body.position, new Color(0.45f, 0.85f, 1), 7, 0.4f);
                }
                if (heavyReleaseQueued || !DemoInput.HeavyHeld) ReleaseHeavyAttack();
            }
            heavyReleaseQueued = false;
            return handled;
        }

        public static bool InsideArc(Vector2 origin, Vector2 facing, Vector2 target, float reach, float arc, float targetRadius)
        {
            Vector2 delta = target - origin;
            if (delta.magnitude > reach + targetRadius) return false;
            if (delta.magnitude <= targetRadius) return true;
            float padding = Mathf.Asin(Mathf.Clamp01(targetRadius / delta.magnitude)) * Mathf.Rad2Deg;
            return Vector2.Angle(facing, delta) <= arc * 0.5f + padding;
        }

        void ResolveHits(float previousAge, float currentAge)
        {
            var strike = CurrentStrike;
            if (strike.action == SwordAction.SpinCleave)
            {
                float emitAt = strike.duration * Mathf.Lerp(SwordMotion.SwingStart(strike),
                    strike.windupRatio + strike.activeRatio, SwordMotion.WhirlwindEmissionPoint);
                if (!whirlwindEmitted && currentAge >= emitAt)
                {
                    whirlwindEmitted = true;
                    arena.Effects.LaunchWhirlwind(this, body.position, strike, currentAge - emitAt, OnWhirlwindHit);
                    RecordWhirlwindSwordLight(strike);
                }
                return;
            }
            float previous = previousAge / strike.duration, current = currentAge / strike.duration;
            if (current < SwordMotion.SwingStart(strike) || previous > strike.windupRatio + strike.activeRatio) return;
            var filter = ContactFilter2D.noFilter;
            Vector2 center = (previousAttackOrigin + body.position) * 0.5f;
            float radius = strike.reach + 0.6f + Vector2.Distance(previousAttackOrigin, body.position) * 0.5f;
            Physics2D.OverlapCircle(center, radius, filter, overlaps);
            foreach (var collider in overlaps)
            {
                var enemy = collider.GetComponent<SlashEnemy>();
                if (enemy == null || !enemy.Alive || hitThisSwing.Contains(enemy)) continue;
                if (!SwordMotion.SweptHit(strike, previous, current, previousAttackOrigin, body.position,
                    attackAim, enemy.transform.position, enemy.Radius, out _)) continue;
                hitThisSwing.Add(enemy);
                Vector2 side = new Vector2(-attackAim.y, attackAim.x);
                Vector2 direction = attackAim + side * (strike.action == SwordAction.ReverseCleave ? 0.2f : strike.action == SwordAction.Cleave ? -0.12f : 0);
                enemy.TakeHit(strike.damage, direction.normalized, strike.knockback, strike.action == SwordAction.WideCleave);
                if (!IsTechniqueActive) RegisterTechniqueHit(ComboIndex);
                arena.Effects.HitStop(strike.action == SwordAction.WideCleave ? 0.045f : 0.033f);
                arena.CameraRig.Shake((strike.action == SwordAction.WideCleave ? 0.09f : 0.055f) * 0.2f);
            }
        }

        void OnWhirlwindHit()
        {
            RegisterTechniqueHit(3);
            arena.CameraRig.Shake(0.011f);
            if (Realm >= CultivationRealm.NascentSoul)
            {
                var wave = heavyAttack.Clone();
                wave.damage = Mathf.RoundToInt(wave.damage * techniqueCombo.DamageMultiplier(Realm));
                arena.Effects.LaunchSwordWave(body.position + attackAim * 0.4f, attackAim, wave, this);
                TotalSwordWaves++;
            }
        }

        public void OnSwordTideHit()
        {
            if (!IsAlive) return;
            int previous = ComboStacks;
            techniqueCombo.RegisterSwordTideHit(Time.time, Realm);
            ApplyRebuildReward(previous);
            techniqueCooldownLeft = Mathf.Max(0, techniqueCooldownLeft - FlowingCloudRules.SwordTideCooldownReduction);
        }

        void RegisterTechniqueHit(int segment)
        {
            int previous = ComboStacks;
            if (techniqueCombo.RegisterHit(segment, Time.time, Realm)) ApplyRebuildReward(previous);
        }

        void ApplyRebuildReward(int previous)
        {
            float reduction = rebuildReward.OnStacksIncreased(previous, ComboStacks, Realm, FlowingCloud.maxStacks);
            techniqueCooldownLeft = Mathf.Max(0, techniqueCooldownLeft - reduction);
        }

        void FixedUpdate()
        {
            if (!IsAlive || arena == null || arena.Paused) { body.linearVelocity = Vector2.zero; return; }
            recoil *= Mathf.Exp(-14 * Time.fixedDeltaTime);
            Vector2 velocity = IsDashing ? dashDirection * dashSpeed : IsHeavyBusy ? Vector2.zero
                : move * moveSpeed * (IsAttacking || IsComboRecovering ? AttackMoveMultiplier : 1) + recoil;
            if (IsTechniqueActive)
            {
                float step = Mathf.Min(skillRushRemaining, Time.fixedDeltaTime);
                velocity = attackAim * skillRushSpeed * (step / Time.fixedDeltaTime);
                skillRushRemaining -= step;
            }
            body.linearVelocity = velocity;
        }

        public bool TakeDamage(int damage, Vector2 direction)
        {
            if (!IsAlive || invincible || invulnerability > 0 || IsDashing) return false;
            Health = Mathf.Max(0, Health - damage);
            InterruptCombo();
            CancelHeavyAttack();
            invulnerability = 0.8f;
            flash = 0.1f;
            recoil = direction.normalized * 5;
            arena.Effects.Burst(transform.position, DemoArt.Red, 12);
            arena.Effects.Number(transform.position, "-" + damage, DemoArt.Red);
            arena.CameraRig.Shake(0.16f);
            if (!IsAlive)
            {
                IsAttacking = false;
                comboRecoveryLeft = 0;
                CancelHeavyAttack();
                ClearTrail();
                inputBuffer.Clear();
                body.linearVelocity = Vector2.zero;
                presentation.SetPose(Vector2.zero, 0, -75);
                if (appearance.Continuous3D) appearance.ContinuousModel.Fall();
                arena.PlayerDied();
            }
            return true;
        }

        public void Teleport(Vector2 position)
        {
            InterruptCombo();
            CancelHeavyAttack();
            IsAttacking = false;
            queued = false;
            inputBuffer.Clear();
            comboRecoveryLeft = dashRemaining = 0;
            heavyPressedQueued = heavyReleaseQueued = false;
            move = recoil = Vector2.zero;
            ClearTrail();
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            previousAttackOrigin = position;
            presentation.SetPose(Vector2.zero, 0, 0);
        }

        public static string GetRealmName(CultivationRealm realm) => realm switch
        {
            CultivationRealm.QiRefining => "练气",
            CultivationRealm.FoundationEstablishment => "筑基",
            CultivationRealm.GoldenCore => "金丹",
            CultivationRealm.NascentSoul => "元婴",
            _ => "练气"
        };

        public void SetRealm(CultivationRealm realm)
        {
            var next = (CultivationRealm)Mathf.Clamp((int)realm,
                (int)CultivationRealm.QiRefining, (int)CultivationRealm.NascentSoul);
            if (Realm != next) { rebuildReward.Clear(); InterruptCombo(); }
            Realm = next;
        }

        public void ResetRealm() => SetRealm(CultivationRealm.QiRefining);

        public void Heal(int amount) { if (IsAlive) Health = Mathf.Min(maxHealth, Health + amount); }

        public void ApplyTuning(SlashTuningSettings settings)
        {
            int previousMax = Mathf.Max(1, maxHealth);
            if (Health > 0 && maxHealth != settings.maxHealth)
                Health = Mathf.Clamp(Mathf.RoundToInt(Health / (float)previousMax * settings.maxHealth), 1, settings.maxHealth);
            maxHealth = settings.maxHealth;
            moveSpeed = settings.moveSpeed;
            dashSpeed = settings.dashSpeed;
            dashDuration = settings.dashDuration;
            dashCooldown = settings.dashCooldown;
            dashCooldownLeft = Mathf.Min(dashCooldownLeft, dashCooldown);
            comboResetTime = settings.comboResetTime;
            invincible = settings.invincible;
            trailLifetime = settings.trailLifetime;
            trailGlowWidth = settings.bladeFeedback.glowWidth;
            trailCrescentPower = settings.bladeFeedback.crescentPower;
            trailHDRIntensity = settings.bladeFeedback.hdrIntensity;
            if (trailMaterial != null) trailMaterial.SetFloat("_Intensity", trailHDRIntensity);
            strikes = SlashTuningSettings.CopyStrikes(settings.strikes);
            heavyAttack = settings.heavyAttack.Clone();
            FlowingCloud = (settings.flowingCloud ?? new SlashTuningSettings.FlowingCloudSettings()).Clone();
            techniqueCombo.Configure(FlowingCloud);
            techniqueCooldownLeft = Mathf.Min(techniqueCooldownLeft, FlowingCloud.activeCooldown);
        }

        void ConfigureWeapon()
        {
            // Older saved scenes may still contain the replaced line-based trails.
            foreach (string name in new[] { "SlashOuter", "SlashInner" })
            {
                var legacy = transform.Find(name)?.GetComponent<LineRenderer>();
                if (legacy != null) legacy.enabled = false;
            }
            swordPivot.localScale = Vector3.one;
            var hilt = swordPivot.Find("Hilt");
            hilt.localPosition = new Vector3(-0.10f, 0, 0);
            hilt.localScale = new Vector3(0.24f, 0.10f, 1);
            var guard = swordPivot.Find("Guard");
            guard.localPosition = Vector3.zero;
            guard.localScale = new Vector3(0.055f, 0.26f, 1);
            swordBlade = swordPivot.Find("Blade");
            var rig = new GameObject("Weapon Arms").transform;
            rig.SetParent(transform, false);
            armRig = rig;
            Color sleeve = new Color(0.18f, 0.40f, 0.43f);
            Color hand = new Color(0.94f, 0.87f, 0.83f);
            upperArmL = DemoArt.Shape("Left upper arm", rig, "Square", Vector2.zero, Vector2.one, sleeve);
            forearmL = DemoArt.Shape("Left forearm", rig, "Square", Vector2.zero, Vector2.one, sleeve);
            upperArmR = DemoArt.Shape("Right upper arm", rig, "Square", Vector2.zero, Vector2.one, sleeve);
            forearmR = DemoArt.Shape("Right forearm", rig, "Square", Vector2.zero, Vector2.one, sleeve);
            gripHand = DemoArt.Shape("Grip hand", rig, "Disk", Vector2.zero, Vector2.one * 0.15f, hand);
            supportHand = DemoArt.Shape("Support hand", rig, "Disk", Vector2.zero, Vector2.one * 0.14f, hand);
            appearance.ConfigureWeapon(swordPivot, upperArmL, forearmL, upperArmR, forearmR);
            // Upper arms, forearms and palms receive separate orders after the body's facing is chosen.
            weaponRenderers = swordPivot.GetComponentsInChildren<SpriteRenderer>();
            scabbardRig = new GameObject("Waist scabbard").transform;
            scabbardRig.SetParent(transform, false);
            ConfigureScabbardArt();
            scabbardRenderers = scabbardRig.GetComponentsInChildren<SpriteRenderer>();
            if (appearance.Continuous3D)
            {
                foreach (var sprite in weaponRenderers) sprite.enabled = false;
                foreach (var sprite in scabbardRenderers) sprite.enabled = false;
                foreach (var sprite in new[] { upperArmL, forearmL, upperArmR, forearmR, gripHand, supportHand }) sprite.enabled = false;
            }
            var trailObject = new GameObject("Blade Trail Mesh");
            trailObject.transform.SetParent(transform, false);
            trailMesh = new Mesh { name = "Sword trail - per sample colors" };
            trailMesh.MarkDynamic();
            trailObject.AddComponent<MeshFilter>().sharedMesh = trailMesh;
            trailRenderer = trailObject.AddComponent<MeshRenderer>();
            // A mesh shader reads vertex colors directly, without SpriteRenderer-only uniforms.
            trailMaterial = new Material(Resources.Load<Shader>("SlashDemo/BladeTrailAdditive"));
            trailMaterial.SetFloat("_Intensity", trailHDRIntensity);
            trailRenderer.sharedMaterial = trailMaterial;
            trailRenderer.sortingOrder = 400;
            trailRenderer.enabled = false;
        }

        void ConfigureScabbardArt()
        {
            var texture = Resources.Load<Texture2D>("SlashDemo/ScabbardIceBlue-v1");
            if (texture == null || !texture.isReadable)
            {
                Debug.LogWarning("Scabbard artwork is missing or unreadable; using the plain case.", this);
                DemoArt.Shape("Scabbard fallback", scabbardRig, "Square", new Vector2(SwordMotion.CarryLength * 0.5f, 0),
                    new Vector2(SwordMotion.CarryLength, 0.18f), new Color(0.32f, 0.57f, 0.67f));
                return;
            }
            var pixels = texture.GetPixels32();
            int minX = texture.width, maxX = -1, minY = texture.height, maxY = -1;
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a <= 16) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            if (maxX <= minX || maxY <= minY) return;
            // Anchor to the mouth center, unaffected by the hanging charm below the case.
            int collarTop = minY, collarBottom = maxY;
            int collarEnd = minX + Mathf.Max(1, (maxX - minX) / 12);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= collarEnd; x++)
                    if (pixels[y * texture.width + x].a > 64)
                    { collarTop = Mathf.Max(collarTop, y); collarBottom = Mathf.Min(collarBottom, y); }
            var rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            var pivot = new Vector2(0.015f, ((collarTop + collarBottom) * 0.5f - minY) / rect.height);
            scabbardSprite = Sprite.Create(texture, rect, pivot, rect.width, 0, SpriteMeshType.FullRect);
            scabbardSprite.name = "Ice blue silver filigree scabbard";
            var image = new GameObject("Silver filigree scabbard").AddComponent<SpriteRenderer>();
            image.transform.SetParent(scabbardRig, false);
            image.sprite = scabbardSprite;
            image.sharedMaterial = DemoArt.Material;
            // Keep the original thickness while shortening the case from its fixed mouth pivot.
            image.transform.localScale = new Vector3(SwordMotion.CarryLength, 1.3f, 1) / (1 - pivot.x);
        }

        static void ArmSegment(SpriteRenderer sprite, Vector2 from, Vector2 to, float width)
        {
            Vector2 direction = to - from;
            sprite.transform.localPosition = (from + to) * 0.5f;
            sprite.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            sprite.transform.localScale = new Vector3(direction.magnitude, width, 1);
        }

        void RecordTrail(float previousAge, float currentAge)
        {
            var s = CurrentStrike;
            if (s.action == SwordAction.SpinCleave) return;
            float start = Mathf.Max(previousAge / s.duration, SwordMotion.SwingStart(s));
            float end = Mathf.Min(currentAge / s.duration, s.windupRatio + s.activeRatio);
            if (end < start || previousAge / s.duration > s.windupRatio + s.activeRatio) return;
            int count = Mathf.Clamp(Mathf.CeilToInt((end - start) / Mathf.Max(0.01f, s.windupRatio + s.activeRatio - SwordMotion.SwingStart(s)) * 24), 1, 24);
            for (int i = 0; i <= count; i++)
            {
                float t = Mathf.Lerp(start, end, i / (float)count);
                float born = Time.time - (currentAge - t * s.duration);
                if (trail.Count > 0 && born <= trail[trail.Count - 1].born + 0.0001f) continue;
                var pose = SwordMotion.EvaluateAttack(s, t);
                float rootT = Mathf.InverseLerp(previousAge, currentAge, t * s.duration);
                Vector2 origin = Vector2.Lerp(previousAttackOrigin, body.position, rootT);
                trail.Add(new TrailPoint { tip = origin + SwordMotion.ToWorld(pose.tip, attackAim),
                    inner = origin + SwordMotion.ToWorld(Vector2.Lerp(pose.grip, pose.tip, 0.76f), attackAim),
                    born = born, intensity = 1 });
            }
            if (trail.Count > 80) trail.RemoveRange(0, trail.Count - 80);
        }

        void RecordWhirlwindSwordLight(Strike strike)
        {
            // A complete energy ribbon surrounds the player; the physical sword keeps its front-only motion.
            trail.Clear();
            const int segments = 64;
            Vector2 origin = body.position;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                trail.Add(new TrailPoint { tip = origin + direction * strike.reach,
                    inner = origin + direction * (strike.reach * 0.70f),
                    born = Time.time, life = 0.18f, intensity = 0.85f, closed = true });
            }
        }

        void AddHeavyTrail(SwordMotion.Pose pose, Vector2 facing, float born, float life, float intensity, float heat)
        {
            if (trail.Count > 0 && born <= trail[trail.Count - 1].born + 0.00001f) return;
            trail.Add(new TrailPoint { tip = body.position + SwordMotion.ToWorld(pose.tip, facing),
                inner = body.position + SwordMotion.ToWorld(Vector2.Lerp(pose.grip, pose.tip, 0.72f), facing),
                born = born, life = life, intensity = intensity, heavy = true, heat = heat });
            if (trail.Count > 80) trail.RemoveAt(0);
        }

        void DrawTrail()
        {
            float lifetime = Mathf.Clamp(trailLifetime, 0.10f, 0.14f);
            for (int i = trail.Count - 1; i >= 0; i--)
                if (Time.time - trail[i].born >= (trail[i].life > 0 ? trail[i].life : lifetime)) trail.RemoveAt(i);
            trailRenderer.enabled = trail.Count > 1;
            if (trail.Count <= 1) return;
            Color baseColor = appearance.Ready ? new Color(0.46f, 0.82f, 1f) :
                CurrentStrike.action == SwordAction.WideCleave ? DemoArt.Gold : DemoArt.Jade;
            trailVertices.Clear(); trailColors.Clear(); trailTriangles.Clear(); trailEmission.Clear();
            for (int i = 0; i < trail.Count; i++)
            {
                var point = trail[i];
                float alpha = CombatFeedbackMath.TrailAlpha(point.born, Time.time, point.life > 0 ? point.life : lifetime) * point.intensity;
                Color c = point.heavy ? Color.Lerp(new Color(0.77f, 0.92f, 1), new Color(1, 0.79f, 0.40f),
                    Mathf.InverseLerp(0.65f, 1, point.heat)) : baseColor;
                var view = arena.View;
                Vector3 tip = transform.InverseTransformPoint(view.LiftPoint(point.tip, view.WeaponHeight));
                float u = Mathf.InverseLerp(trail[0].born, trail[trail.Count - 1].born, point.born);
                float crescent = point.closed ? 1 : CombatFeedbackMath.TrailCrescent(u, trailCrescentPower);
                float taper = (0.35f + 0.65f * alpha) * crescent;
                Vector2 groundInner = Vector2.Lerp(point.tip, point.inner, taper);
                Vector3 inner = transform.InverseTransformPoint(view.LiftPoint(groundInner, view.WeaponHeight));
                Vector2 outward = (point.tip - point.inner).normalized;
                Vector3 glow = transform.InverseTransformPoint(view.LiftPoint(point.tip + outward * (trailGlowWidth * taper * (point.heavy ? 1.3f : point.closed ? 1.25f : 1)), view.WeaponHeight));
                // Keep a transparent boundary outside the visible mist to avoid a hard outer edge.
                // Geometry tapers to a point at both ends; sample alpha still depends only on age.
                trailVertices.Add(glow);
                trailVertices.Add(Vector3.Lerp(glow, tip, 0.35f));
                trailVertices.Add(tip);
                trailVertices.Add(Vector3.Lerp(tip, inner, 0.30f));
                trailVertices.Add(inner);
                trailColors.Add(new Color(0.35f, 0.75f, 1, 0));
                trailColors.Add(new Color(0.35f, 0.75f, 1, alpha * 0.25f));
                trailColors.Add(new Color(0.95f, 0.99f, 1, alpha * 0.95f));
                trailColors.Add(new Color(c.r, c.g, c.b, alpha * 0.80f));
                trailColors.Add(new Color(c.r, c.g, c.b, 0));
                trailEmission.Add(new Vector2(0, 0));
                trailEmission.Add(new Vector2(0, 0));
                trailEmission.Add(new Vector2(1, 0));
                trailEmission.Add(new Vector2(0.50f, 0));
                trailEmission.Add(new Vector2(0, 0));
                if (i == 0) continue;
                for (int col = 0; col < 4; col++)
                {
                    int a = (i - 1) * 5 + col, b = i * 5 + col;
                    trailTriangles.Add(a); trailTriangles.Add(b); trailTriangles.Add(a + 1);
                    trailTriangles.Add(a + 1); trailTriangles.Add(b); trailTriangles.Add(b + 1);
                }
            }
            trailMesh.Clear();
            trailMesh.SetVertices(trailVertices);
            trailMesh.SetColors(trailColors);
            trailMesh.SetUVs(0, trailEmission);
            trailMesh.SetTriangles(trailTriangles, 0, false);
            trailMesh.RecalculateBounds();
        }

        void ClearTrail()
        {
            trail.Clear();
            if (trailRenderer != null) trailRenderer.enabled = false;
        }

        void AnimateSword()
        {
            renderedElevation = arena.View.elevation;
            renderedScale = arena.View.characterScale;
            var s = IsAttacking ? CurrentStrike : strikes[0];
            var pose = SwordMotion.Evaluate(s, IsAttacking ? attackAge / s.duration : 1);
            if (IsHeavyBusy)
            {
                pose = SwordMotion.HeavyPose(ChargeProgress, heavyRelease.Active, heavyRelease.SweepProgress,
                    heavyRelease.RecoveryProgress, Time.time);
            }
            Vector2 facing = IsAttacking || IsComboRecovering ? attackAim : IsHeavyRecovering ? heavyAim : Aim;
            Vector2 socket = SwordMotion.CarryGrip;
            Vector2 side = new Vector2(-facing.y, facing.x);
            Vector2 carryAxis = appearance.Ready && !appearance.Continuous3D
                ? SwordMotion.CarryAxisInView(facing, arena.View.elevation, appearance.FacingIndex)
                : SwordMotion.ToWorld(DemoArt.Direction(SwordMotion.CarryAngle), facing);
            Vector2 carryLocal = new Vector2(Vector2.Dot(carryAxis, facing), Vector2.Dot(carryAxis, side));
            bool equipping = !equipment.Ready;
            if (equipping) pose = SwordMotion.EquipPose(pose, equipment.Amount, carryLocal, out socket);
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg + pose.angle;
            Vector2 grip = SwordMotion.ToWorld(pose.grip, facing);
            Vector3 lift = arena.View.Up * arena.View.WeaponHeight;
            swordPivot.localPosition = (Vector3)grip + lift;
            swordPivot.localRotation = Quaternion.Euler(0, 0, angle);
            Vector2 sheathSocket = SwordMotion.ToWorld(socket, facing);
            scabbardRig.localPosition = (Vector3)sheathSocket + lift;
            scabbardRig.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(carryAxis.y, carryAxis.x) * Mathf.Rad2Deg);
            scabbardRig.gameObject.SetActive(equipment.ScabbardVisible);
            swordBlade.localPosition = new Vector3(pose.bladeLength * 0.5f, 0, 0);
            swordBlade.localScale = new Vector3(pose.bladeLength, Mathf.Max(0.08f, s.bladeWidth), 1);
            Vector2 offset = SwordMotion.ToWorld(pose.bodyOffset, facing);
            float bob = appearance.Ready ? appearance.WalkBob :
                move.sqrMagnitude > 0.01f ? Mathf.Sin(Time.time * 18) * 0.035f : 0;
            bool meshCombat = appearance.Ready && appearance.HasDirectionalArt && pose.kinetic
                && appearance.Locomotion != null && appearance.Locomotion.enabled;
            presentation.SetPose(meshCombat ? Vector2.zero : offset, bob + (meshCombat ? 0 : pose.sink),
                meshCombat ? 0 : CombatFeedbackMath.ProjectLean(pose.lean, facing));
            appearance.SetFacing(facing, pose.bladeLength, arena.View.elevation);
            appearance.SetWeaponCharge(IsCharging && equipment.Ready ? ChargeProgress : 0);
            if (IsCharging && equipment.Ready)
                arena.Effects.SetChargeTarget(body.position + SwordMotion.ToWorld(pose.tip, facing), ChargeProgress);
            appearance.SetCombatPose(pose, IsAttacking || IsHeavyBusy, IsDashing, Time.deltaTime, facing, arena.View);
            bool backFacing = appearance.Ready ? appearance.IsBackFacing : facing.y > 0.3f;
            armRig.localPosition = lift;
            Vector2 swordDirection = DemoArt.Direction(angle);
            Vector2 mainHand = grip - swordDirection * 0.04f;
            Vector2 offHand = grip - swordDirection * 0.18f;
            if (equipping)
            {
                SwordMotion.EquipHands(pose.grip - DemoArt.Direction(pose.angle) * 0.04f, socket,
                    equipment.Amount, out Vector2 rightHand, out Vector2 leftHand);
                mainHand = SwordMotion.ToWorld(rightHand, facing);
                offHand = SwordMotion.ToWorld(leftHand, facing);
            }
            Vector2 shoulderL = offset + side * 0.18f, shoulderR = offset - side * 0.18f;
            float scale = arena.View.characterScale;
            int direction = appearance.Ready && appearance.FacingIndex >= 0 ? appearance.FacingIndex : HeroFacingMath.Direction(facing);
            // Painted shoulders are screen-left/right; fallback shoulders are anatomical left/right.
            bool mainOnLeft = !appearance.Continuous3D && appearance.Ready && HeroArmMath.RightHandOnScreenLeft(direction);
            float singleHandWeight = HeroArmMath.SingleHandWeight(IsAttacking, ComboIndex, IsHeavyBusy, equipment.Ready,
                IsAttacking ? attackAge / s.duration : 1, s.windupRatio);
            Vector3 left = appearance.Continuous3D ? appearance.ContinuousModel.LeftShoulder : appearance.Ready ? visual.TransformPoint(appearance.Shoulder(true)) : armRig.TransformPoint(shoulderL);
            Vector3 right = appearance.Continuous3D ? appearance.ContinuousModel.RightShoulder : appearance.Ready ? visual.TransformPoint(appearance.Shoulder(false)) : armRig.TransformPoint(shoulderR);
            Vector3 mainWorld = armRig.TransformPoint(mainHand), supportWorld = armRig.TransformPoint(offHand);
            Vector3 relaxed = appearance.Continuous3D ? appearance.ContinuousModel.RelaxedLeftHand
                : HeroArmMath.RelaxedOffHand(mainOnLeft ? right : left, arena.View.Up, !mainOnLeft, scale);
            supportWorld = Vector3.Lerp(supportWorld, relaxed, singleHandWeight);
            gripHand.transform.position = mainWorld;
            supportHand.transform.position = supportWorld;
            gripHand.transform.localRotation = supportHand.transform.localRotation = arena.View.Rotation;
            supportHand.transform.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.105f * scale, singleHandWeight);
            // A hand keeps its shoulder throughout a cut, regardless of where the sword sweeps.
            Vector3 handL = mainOnLeft ? mainWorld : supportWorld;
            Vector3 handR = mainOnLeft ? supportWorld : mainWorld;
            if (appearance.Ready && !appearance.Continuous3D)
            {
                Vector3 elbowLeft = HeroArmMath.Elbow(left, handL, visual.position, arena.View.Up, true, backFacing, scale);
                Vector3 elbowRight = HeroArmMath.Elbow(right, handR, visual.position, arena.View.Up, false, backFacing, scale);
                if (mainOnLeft) elbowRight = Vector3.Lerp(elbowRight, Vector3.Lerp(right, handR, 0.52f), singleHandWeight);
                else elbowLeft = Vector3.Lerp(elbowLeft, Vector3.Lerp(left, handL, 0.52f), singleHandWeight);
                HeroAppearance.FitSleeve(upperArmL, left, elbowLeft, arena.View, 0.13f * scale, 0.08f * scale);
                HeroAppearance.FitSleeve(forearmL, elbowLeft, handL, arena.View, 0.19f * scale);
                HeroAppearance.FitSleeve(upperArmR, right, elbowRight, arena.View, 0.13f * scale, 0.08f * scale);
                HeroAppearance.FitSleeve(forearmR, elbowRight, handR, arena.View, 0.19f * scale);
            }
            else if (!appearance.Continuous3D)
            {
                Vector2 localL = armRig.InverseTransformPoint(handL), localR = armRig.InverseTransformPoint(handR);
                Vector2 elbowL = Vector2.Lerp(shoulderL, localL, 0.5f) + side * 0.12f;
                Vector2 elbowR = Vector2.Lerp(shoulderR, localR, 0.5f) - side * 0.12f;
                ArmSegment(upperArmL, shoulderL, elbowL, 0.14f);
                ArmSegment(forearmL, elbowL, localL, 0.11f);
                ArmSegment(upperArmR, shoulderR, elbowR, 0.14f);
                ArmSegment(forearmR, elbowR, localR, 0.11f);
            }
            int bodyOrder = appearance.BodySortingOrder;
            if (!appearance.Ready)
                foreach (var renderer in renderers)
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                        bodyOrder = Mathf.Max(bodyOrder, renderer.sortingOrder);
            var orders = HeroArmMath.Sorting(backFacing, bodyOrder, weaponRenderers.Length);
            for (int i = 0; i < weaponRenderers.Length; i++) weaponRenderers[i].sortingOrder = orders.sword + i;
            // Cover the portion of the blade still inside the case, with hands above the rim.
            foreach (var renderer in scabbardRenderers) renderer.sortingOrder = orders.sword + weaponRenderers.Length;
            upperArmL.sortingOrder = orders.upperArm;
            upperArmR.sortingOrder = orders.upperArm + 1;
            forearmL.sortingOrder = orders.forearm;
            forearmR.sortingOrder = orders.forearm + 1;
            gripHand.sortingOrder = orders.hand;
            supportHand.sortingOrder = orders.hand + 1;
            trailRenderer.sortingOrder = orders.trail;
            if (appearance.Continuous3D)
            {
                appearance.ContinuousModel.BuildFrame(mainWorld, supportWorld, swordPivot.position,
                    transform.TransformPoint((Vector3)SwordMotion.ToWorld(pose.tip, facing) + lift),
                    scabbardRig.position, scabbardRig.right, equipment.ScabbardVisible, singleHandWeight);
                // Arms, blade and torso occlude each other inside one depth-sorted 3D mesh.
                trailRenderer.sortingOrder = 30;
            }
            DrawTrail();
        }

        void LateUpdate()
        {
            // Reproject a frozen/paused pose when the view controls change, without advancing combat.
            if (IsAlive && arena != null && (renderedElevation != arena.View.elevation || renderedScale != arena.View.characterScale))
                AnimateSword();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                inputBuffer.Clear(); queued = false; move = Vector2.zero;
                if (IsCharging) CancelHeavyAttack();
                heavyPressedQueued = heavyReleaseQueued = false;
            }
        }
        void OnDisable() { rebuildReward.Clear(); InterruptCombo(); CancelHeavyAttack(); }
        void OnDestroy()
        {
            if (scabbardSprite != null) Destroy(scabbardSprite);
            if (trailMesh != null) Destroy(trailMesh);
            if (trailMaterial != null) Destroy(trailMaterial);
        }
    }
}
