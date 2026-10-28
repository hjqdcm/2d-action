using UnityEngine;

namespace SlashDemo
{
    [System.Serializable]
    public sealed class SlashTuningSettings
    {
        // Project defaults synchronized from the user's saved v3 tuning on 2026-10-27.
        public const int DefaultDummyHealth = 1000;
        public int version = 3;
        public float moveSpeed = 5.4f, dashSpeed = 17, dashDuration = 0.15f, dashCooldown = 0.75f;
        public float comboResetTime = 0.65f;
        public int maxHealth = 100;
        public bool invincible;
        public SlashPlayer.Strike[] strikes = DefaultStrikes();
        public float enemySpeedMultiplier = 1, enemyHealthMultiplier = 1;
        public int enemyDamage = 12, dummyHealth = DefaultDummyHealth;
        public float enemyWindup = 0.58f, enemyRange = 1.15f;
        public float hitStopMultiplier = 1, shakeMultiplier = 1, volume = 0.18f;
        public float trailLifetime = 0.12f;
        public float viewElevation = 45, characterScale = 1.25f, viewZoom = 1;
        // A nested default also migrates earlier v3 saves without replacing unrelated settings.
        public HeavyAttackSettings heavyAttack = new HeavyAttackSettings();
        public BladeFeedbackSettings bladeFeedback = new BladeFeedbackSettings();
        public FlowingCloudSettings flowingCloud = new FlowingCloudSettings();
        public IronSwordSettings ironSword = new IronSwordSettings();

        [System.Serializable]
        public sealed class IronSwordSettings
        {
            public float foundationChargeBonus = 0.20f, empoweredWidthBonus = 0.30f;
            public float scarDuration = 1.5f, scarTickInterval = 0.25f, scarTickDamageRatio = 0.10f;
            public float scarExplosionRatio = 0.50f, scarHeavyBonus = 0.50f;
            public float bodyDuration = 3, bodyCooldown = 10, bodyDamageReduction = 0.50f, bodyChargeBonus = 0.30f;
            public float bodyHeavyBonus = 0.30f;
            public IronSwordSettings Clone() => (IronSwordSettings)MemberwiseClone();
            public float ChargeSpeed(CultivationRealm realm, bool bodyActive)
                => 1 + (realm >= CultivationRealm.FoundationEstablishment ? foundationChargeBonus : 0)
                    + (bodyActive ? bodyChargeBonus : 0);
            public HeavyAttackSettings Release(HeavyAttackSettings source, int stacks, CultivationRealm realm, bool bodyActive = false)
            {
                var wave = source.Clone();
                wave.damage = Mathf.Max(1, Mathf.RoundToInt(wave.damage * IronSwordRules.DamageMultiplier(stacks)
                    * (bodyActive ? 1 + bodyHeavyBonus : 1)));
                wave.width *= IronSwordRules.RangeMultiplier(stacks);
                wave.distance *= IronSwordRules.RangeMultiplier(stacks);
                if (stacks >= IronSwordRules.EmpoweredStacks && realm >= CultivationRealm.FoundationEstablishment)
                    wave.width *= 1 + empoweredWidthBonus;
                wave.recoveryTime = IronSwordRules.Recovery(stacks);
                return wave;
            }
            public void Validate(IronSwordSettings fallback)
            {
                foundationChargeBonus = Safe(foundationChargeBonus, 0, 2, fallback.foundationChargeBonus);
                empoweredWidthBonus = Safe(empoweredWidthBonus, 0, 1, fallback.empoweredWidthBonus);
                scarDuration = Safe(scarDuration, 0.25f, 5, fallback.scarDuration);
                scarTickInterval = Safe(scarTickInterval, 0.05f, 1, fallback.scarTickInterval);
                scarTickDamageRatio = Safe(scarTickDamageRatio, 0.01f, 1, fallback.scarTickDamageRatio);
                scarExplosionRatio = Safe(scarExplosionRatio, 0, 3, fallback.scarExplosionRatio);
                scarHeavyBonus = Safe(scarHeavyBonus, 0, 2, fallback.scarHeavyBonus);
                bodyDuration = Safe(bodyDuration, 0.1f, 5, fallback.bodyDuration);
                bodyCooldown = Safe(bodyCooldown, 0.2f, 30, fallback.bodyCooldown);
                bodyDamageReduction = Safe(bodyDamageReduction, 0, 0.95f, fallback.bodyDamageReduction);
                bodyChargeBonus = Safe(bodyChargeBonus, 0, 2, fallback.bodyChargeBonus);
                bodyHeavyBonus = Safe(bodyHeavyBonus, 0, 2, fallback.bodyHeavyBonus);
            }
        }

        [System.Serializable]
        public sealed class FlowingCloudSettings
        {
            public float attackSpeedPerStack = FlowingCloudRules.AttackSpeedPerStack, stackBonus = 0.08f;
            public int maxStacks = 5, pursuitDamage = 48;
            public float pursuitReach = 2.1f, pursuitDuration = 0.68f, pursuitKnockback = 7;
            public float activeCooldown = FlowingCloudRules.ActiveCooldown, rushDistance = FlowingCloudRules.RushDistance, rushDuration = FlowingCloudRules.RushDuration;
            public int activeBaseDamage = 28, activeDamagePerRealm = 6;
            public float activeDuration = 0.26f, activeReach = 2.1f, activeKnockback = 4;
            public int activeDamagePerStack = 8;
            public float rushDistancePerStack = 0.25f, activeWidth = 1.1f, activeWidthPerStack = 0.18f;
            public float rushDistanceMultiplier = 2.4f, rushTrailLifetime = 0.8f;
            // Missing in older saves: seed once from their third strike, then tune independently.
            public SlashPlayer.Strike finisherStrike;
            public SlashPlayer.Strike EnsureFinisher(SlashPlayer.Strike thirdStrike = null)
            {
                // JsonUtility can materialize an omitted inline object with all-zero fields.
                if (finisherStrike == null || (finisherStrike.motionVersion == 0 && finisherStrike.damage == 0))
                {
                    finisherStrike = (thirdStrike ?? SwordMotion.Defaults()[2]).Clone();
                    finisherStrike.damage *= 2;
                }
                return finisherStrike;
            }
            public int ActiveDamage(CultivationRealm realm, int stacks = 0)
                => activeBaseDamage + activeDamagePerRealm * (int)realm + activeDamagePerStack * Mathf.Clamp(stacks, 0, FlowingCloudRules.MaxStacks);
            public float RushDistance(int stacks) => (rushDistance + rushDistancePerStack * Mathf.Clamp(stacks, 0, FlowingCloudRules.MaxStacks)) * rushDistanceMultiplier;
            public float ActiveWidth(int stacks) => activeWidth + activeWidthPerStack * Mathf.Clamp(stacks, 0, FlowingCloudRules.MaxStacks);
            public FlowingCloudSettings Clone()
            {
                var copy = (FlowingCloudSettings)MemberwiseClone();
                copy.finisherStrike = finisherStrike?.Clone();
                return copy;
            }
            public void Validate(FlowingCloudSettings fallback, SlashPlayer.Strike thirdStrike = null)
            {
                attackSpeedPerStack = Safe(attackSpeedPerStack, 0, 0.25f, fallback.attackSpeedPerStack);
                stackBonus = Safe(stackBonus, 0, 0.25f, fallback.stackBonus);
                maxStacks = Mathf.Clamp(maxStacks, 1, FlowingCloudRules.MaxStacks);
                pursuitDamage = Mathf.Clamp(pursuitDamage, 1, 200);
                pursuitReach = Safe(pursuitReach, 0.5f, 4.5f, fallback.pursuitReach);
                pursuitDuration = Safe(pursuitDuration, 0.18f, 0.8f, fallback.pursuitDuration);
                pursuitKnockback = Safe(pursuitKnockback, 0, 20, fallback.pursuitKnockback);
                activeCooldown = Safe(activeCooldown, 0.2f, 15, fallback.activeCooldown);
                rushDistance = Safe(rushDistance, 0, 3, fallback.rushDistance);
                rushDuration = Safe(rushDuration, 0.06f, 0.3f, fallback.rushDuration);
                activeBaseDamage = Mathf.Clamp(activeBaseDamage, 1, 200);
                activeDamagePerRealm = Mathf.Clamp(activeDamagePerRealm, 0, 50);
                activeDuration = Safe(activeDuration, 0.16f, 0.8f, fallback.activeDuration);
                activeReach = Safe(activeReach, 0.5f, 4.5f, fallback.activeReach);
                activeKnockback = Safe(activeKnockback, 0, 20, fallback.activeKnockback);
                activeDamagePerStack = Mathf.Clamp(activeDamagePerStack, 1, 50);
                rushDistancePerStack = Safe(rushDistancePerStack, 0.05f, 0.6f, fallback.rushDistancePerStack);
                activeWidth = Safe(activeWidth, 0.4f, 3, fallback.activeWidth);
                activeWidthPerStack = Safe(activeWidthPerStack, 0.05f, 0.4f, fallback.activeWidthPerStack);
                rushDistanceMultiplier = Safe(rushDistanceMultiplier, 1, 5, fallback.rushDistanceMultiplier);
                rushTrailLifetime = Safe(rushTrailLifetime, 0.2f, 2, fallback.rushTrailLifetime);
                var finisherDefault = (thirdStrike ?? SwordMotion.Defaults()[2]).Clone();
                finisherDefault.damage *= 2;
                ValidateStrike(EnsureFinisher(thirdStrike), finisherDefault, 2, 400);
            }
        }

        [System.Serializable]
        public sealed class BladeFeedbackSettings
        {
            public float glowWidth = 0.10f, crescentPower = 0.7f, punchAmount = 0.04f, hdrIntensity = 2.2f;
            public BladeFeedbackSettings Clone() => (BladeFeedbackSettings)MemberwiseClone();
            public void Validate(BladeFeedbackSettings fallback)
            {
                glowWidth = Safe(glowWidth, 0.05f, 0.18f, fallback.glowWidth);
                crescentPower = Safe(crescentPower, 0.4f, 1, fallback.crescentPower);
                punchAmount = Safe(punchAmount, 0, 0.08f, fallback.punchAmount);
                if (hdrIntensity <= 0) hdrIntensity = fallback.hdrIntensity;
                hdrIntensity = Safe(hdrIntensity, 1, 3, fallback.hdrIntensity);
            }
        }

        [System.Serializable]
        public sealed class HeavyAttackSettings
        {
            public float chargeTime = 0.8f, recoveryTime = 0.25f;
            public float width = 3.4f, distance = 6, speed = 15.5f, knockback = 8;
            public int damage = 55;
            public HeavyAttackSettings Clone() => (HeavyAttackSettings)MemberwiseClone();
            public void Validate(HeavyAttackSettings fallback)
            {
                chargeTime = Safe(chargeTime, 0.1f, 3, fallback.chargeTime);
                recoveryTime = Safe(recoveryTime, 0, 1.5f, fallback.recoveryTime);
                width = Safe(width, 0.25f, 5, fallback.width);
                distance = Safe(distance, 1, 18, fallback.distance);
                speed = Safe(speed, 3, 30, fallback.speed);
                knockback = Safe(knockback, 0, 20, fallback.knockback);
                damage = Mathf.Clamp(damage, 1, 200);
            }
        }

        static SlashPlayer.Strike[] DefaultStrikes() => SwordMotion.Defaults();

        public SlashTuningSettings Clone()
        {
            var copy = (SlashTuningSettings)MemberwiseClone();
            copy.strikes = CopyStrikes(strikes);
            copy.heavyAttack = (heavyAttack ?? new HeavyAttackSettings()).Clone();
            copy.bladeFeedback = (bladeFeedback ?? new BladeFeedbackSettings()).Clone();
            copy.flowingCloud = (flowingCloud ?? new FlowingCloudSettings()).Clone();
            copy.ironSword = (ironSword ?? new IronSwordSettings()).Clone();
            return copy;
        }

        public static SlashPlayer.Strike[] CopyStrikes(SlashPlayer.Strike[] source)
        {
            if (source == null) return DefaultStrikes();
            var copy = new SlashPlayer.Strike[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var s = source[i];
                if (s != null) copy[i] = s.Clone();
            }
            return copy;
        }

        public static SlashTuningSettings Capture(SlashPlayer player, SlashEffects effects, SlashCamera camera, ObliqueView view)
        {
            return new SlashTuningSettings {
                moveSpeed = player.moveSpeed, dashSpeed = player.dashSpeed,
                dashDuration = player.dashDuration, dashCooldown = player.dashCooldown,
                comboResetTime = player.comboResetTime, maxHealth = player.maxHealth,
                invincible = player.invincible, strikes = CopyStrikes(player.strikes),
                heavyAttack = (player.heavyAttack ?? new HeavyAttackSettings()).Clone(),
                flowingCloud = player.FlowingCloud.Clone(),
                ironSword = player.IronSword.Clone(),
                bladeFeedback = new BladeFeedbackSettings { glowWidth = player.trailGlowWidth,
                    crescentPower = player.trailCrescentPower, punchAmount = camera.punchAmount, hdrIntensity = player.trailHDRIntensity },
                hitStopMultiplier = effects.hitStopMultiplier, shakeMultiplier = camera.shakeMultiplier,
                volume = effects.volume, trailLifetime = player.trailLifetime,
                viewElevation = view.elevation, characterScale = view.characterScale, viewZoom = view.zoom
            };
        }

        static float Safe(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }

        static void ValidateStrike(SlashPlayer.Strike s, SlashPlayer.Strike f, int slot, int maxDamage = 200)
        {
            SwordMotion.UpgradeLegacy(s, slot);
            s.action = (SwordAction)slot;
            s.damage = Mathf.Clamp(s.damage, 1, maxDamage);
            s.reach = Safe(s.reach, 0.5f, 4.5f, f.reach);
            s.arc = Safe(s.arc, 30, 170, f.arc);
            s.duration = Safe(s.duration, 0.2f, 1.2f, f.duration);
            s.knockback = Safe(s.knockback, 0, 20, f.knockback);
            s.windupRatio = Safe(s.windupRatio, 0.1f, 0.45f, f.windupRatio);
            s.activeRatio = Safe(s.activeRatio, 0.15f, Mathf.Min(0.55f, 0.85f - s.windupRatio), f.activeRatio);
            s.stepDistance = 0;
            s.bladeWidth = Safe(s.bladeWidth, 0.04f, 0.4f, f.bladeWidth);
        }

        // Saved values go through the same bounds as the sliders before touching live actors.
        public void Validate(SlashTuningSettings fallback)
        {
            // JsonUtility may omit this object in pre-technique v3 saves.
            if (flowingCloud == null) flowingCloud = (fallback.flowingCloud ?? new FlowingCloudSettings()).Clone();
            if (ironSword == null) ironSword = new IronSwordSettings();
            ironSword.Validate(fallback.ironSword ?? new IronSwordSettings());
            if (heavyAttack == null) heavyAttack = new HeavyAttackSettings();
            heavyAttack.Validate(fallback.heavyAttack ?? new HeavyAttackSettings());
            if (bladeFeedback == null) bladeFeedback = new BladeFeedbackSettings();
            bladeFeedback.Validate(fallback.bladeFeedback ?? new BladeFeedbackSettings());
            moveSpeed = Safe(moveSpeed, 1, 12, fallback.moveSpeed);
            dashSpeed = Safe(dashSpeed, 5, 30, fallback.dashSpeed);
            dashDuration = Safe(dashDuration, 0.06f, 0.4f, fallback.dashDuration);
            dashCooldown = Safe(dashCooldown, 0.15f, 3, fallback.dashCooldown);
            comboResetTime = Safe(comboResetTime, 0.15f, 2, fallback.comboResetTime);
            maxHealth = Mathf.Clamp(maxHealth, 20, 500);
            if (strikes == null || strikes.Length != 3) strikes = CopyStrikes(fallback.strikes);
            for (int i = 0; i < strikes.Length; i++)
            {
                var f = fallback.strikes[i];
                if (strikes[i] == null) strikes[i] = f.Clone();
                ValidateStrike(strikes[i], f, i);
            }
            flowingCloud.Validate(fallback.flowingCloud ?? new FlowingCloudSettings(), strikes[2]);
            version = 3;
            enemySpeedMultiplier = Safe(enemySpeedMultiplier, 0, 3, fallback.enemySpeedMultiplier);
            enemyHealthMultiplier = Safe(enemyHealthMultiplier, 0.2f, 5, fallback.enemyHealthMultiplier);
            enemyDamage = Mathf.Clamp(enemyDamage, 1, 60);
            dummyHealth = Mathf.Clamp(dummyHealth, 50, 2000);
            enemyWindup = Safe(enemyWindup, 0.2f, 2, fallback.enemyWindup);
            enemyRange = Safe(enemyRange, 0.5f, 3, fallback.enemyRange);
            hitStopMultiplier = Safe(hitStopMultiplier, 0, 2.5f, fallback.hitStopMultiplier);
            shakeMultiplier = Safe(shakeMultiplier, 0, 2.5f, fallback.shakeMultiplier);
            volume = Safe(volume, 0, 1, fallback.volume);
            // Earlier v2 saves have no trailLifetime field. Fill it without changing other tuning.
            if (trailLifetime <= 0) trailLifetime = fallback.trailLifetime;
            trailLifetime = Safe(trailLifetime, 0.10f, 0.14f, fallback.trailLifetime);
            if (viewElevation <= 0) viewElevation = fallback.viewElevation;
            if (characterScale <= 0) characterScale = fallback.characterScale;
            if (viewZoom <= 0) viewZoom = fallback.viewZoom;
            viewElevation = Safe(viewElevation, 30, 65, fallback.viewElevation);
            characterScale = Safe(characterScale, 1, 1.6f, fallback.characterScale);
            viewZoom = Safe(viewZoom, 0.85f, 1.25f, fallback.viewZoom);
        }
    }
}
