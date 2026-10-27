using System;
using System.Collections.Generic;

namespace SlashDemo
{
    public sealed class TechniqueStage
    {
        public readonly CultivationRealm realm;
        public readonly string name, effect;
        public TechniqueStage(CultivationRealm realm, string name, string effect)
        { this.realm = realm; this.name = name; this.effect = effect; }
    }

    public sealed class TechniqueDefinition
    {
        public readonly string id, name, focus, intent, activeName, activeEffect, cost;
        public readonly IReadOnlyList<TechniqueStage> stages;
        public TechniqueDefinition(string id, string name, string focus, string intent,
            string activeName, string activeEffect, string cost, params TechniqueStage[] stages)
        {
            this.id = id; this.name = name; this.focus = focus; this.intent = intent;
            this.activeName = activeName; this.activeEffect = activeEffect; this.cost = cost;
            this.stages = Array.AsReadOnly(stages);
        }
    }

    public static class TechniqueCatalog
    {
        public static readonly TechniqueDefinition FlowingCloud = new TechniqueDefinition(
            "flowing-cloud-sword", "流云剑诀", "连段", "以快打慢，连而不绝。",
            "流云疾斩", "消耗全部叠浪，向前突进并造成伤害，每层叠浪会额外增加伤害和突进距离，若消耗了五层叠浪，则会额外释放一次斩击，该斩击不会叠加叠浪层数。",
            "闪避扣除两层叠浪，最低为0层；受击、改用重击或传送时清空连击层数；普攻未命中不叠层、不刷新时间，叠浪到期后清空。",
            new TechniqueStage(CultivationRealm.QiRefining, "一式·叠浪", "每次普攻命中后会叠加一层叠浪，每层叠浪会使普攻伤害提升8%，最多叠加5层，持续2秒，普攻命中后重置该时间，当叠浪层数为5层时，额外使普攻伤害提升20%。受击、重击都会清空叠浪层数；闪避会扣除2层叠浪。"),
            new TechniqueStage(CultivationRealm.FoundationEstablishment, "二式·起风", "每层叠浪会额外提升5%的普攻攻速。释放完流云疾斩后，首次达到满层叠浪时，减少1秒流云疾斩的冷却时间，每次释放仅触发一次。"),
            new TechniqueStage(CultivationRealm.GoldenCore, "三式·风舞", "第3段普攻命中后继续普攻可以释放第4段普攻——旋风斩，该次普攻会对周围360度的敌人造成一次伤害，如果旋风斩命中了敌人，则可以叠加两次叠浪。"),
            new TechniqueStage(CultivationRealm.NascentSoul, "四式·剑潮", "旋风斩命中会额外释放一道剑气，剑气伤害视为普攻伤害，若剑气命中敌人，则会减少1秒流云疾斩的冷却时间。"));

        public static IReadOnlyList<TechniqueDefinition> All { get; }
            = Array.AsReadOnly(new[] { FlowingCloud });
    }

    public static class FlowingCloudRules
    {
        public const float AttackSpeedPerStack = 0.05f, StackBonus = 0.08f, StackDuration = 2, ActiveCooldown = 6;
        public const float SwordTideCooldownReduction = 1;
        public const float RebuildCooldownReduction = 1;
        public const float FullStackDamageBonus = 0.20f;
        public const float RushDistance = 1.4f, RushDuration = 0.12f;
        public const int MaxStacks = 5, PursuitDamage = 48, DodgeStackCost = 2;
        public static int ActiveDamage(CultivationRealm realm) => 28 + 6 * (int)realm;
    }

    // One reward per completed active skill, independent of stack loss and individual attack sequences.
    public sealed class FlowingCloudRebuildReward
    {
        public bool Pending { get; private set; }
        public void Arm(CultivationRealm realm) => Pending = realm >= CultivationRealm.FoundationEstablishment;
        public void Clear() => Pending = false;
        public float OnStacksIncreased(int previous, int current, CultivationRealm realm, int maximum)
        {
            if (!Pending || realm < CultivationRealm.FoundationEstablishment
                || previous >= maximum || current < maximum) return 0;
            Pending = false;
            return FlowingCloudRules.RebuildCooldownReduction;
        }
    }

    // Hit-confirmed state, independent of frame rate, UI and the current enemy count.
    public sealed class FlowingCloudCombo
    {
        public int Stacks { get; private set; }
        public bool StrikeHit { get; private set; }
        public float StackUntil { get; private set; } = float.NegativeInfinity;
        float attackSpeed = FlowingCloudRules.AttackSpeedPerStack, bonus = FlowingCloudRules.StackBonus;
        int maxStacks = FlowingCloudRules.MaxStacks;
        public void Configure(SlashTuningSettings.FlowingCloudSettings settings)
        {
            attackSpeed = settings.attackSpeedPerStack; bonus = settings.stackBonus; maxStacks = settings.maxStacks;
            Stacks = Math.Min(Stacks, maxStacks);
        }
        public void BeginStrike() { StrikeHit = false; }

        public float StackTimeLeft(float now) => Math.Max(0, StackUntil - now);
        public void ExpireStacks(float now)
        {
            if (now >= StackUntil) ClearStacks();
        }
        public void RefreshStacks(float now)
        {
            ExpireStacks(now);
            if (Stacks > 0) StackUntil = now + FlowingCloudRules.StackDuration;
        }
        public int ConsumeStacks(float now)
        {
            ExpireStacks(now);
            int consumed = Stacks;
            Clear();
            return consumed;
        }
        public void ApplyDodge(float now)
        {
            ExpireStacks(now);
            Stacks = Math.Max(0, Stacks - FlowingCloudRules.DodgeStackCost);
            if (Stacks == 0) ClearStacks();
            // Surviving layers keep their original deadline; dodging does not refresh the buff.
            ResetSequence();
        }
        void ClearStacks() { Stacks = 0; StackUntil = float.NegativeInfinity; }

        // Normal combo timing can restart the sequence while the two-second buff remains active.
        public void ResetSequence() { StrikeHit = false; }

        public float AttackSpeedMultiplier(CultivationRealm realm)
            => realm >= CultivationRealm.FoundationEstablishment ? 1 + attackSpeed * Stacks : 1;

        public float DamageMultiplier(CultivationRealm realm)
            => realm >= CultivationRealm.QiRefining
                ? 1 + bonus * Math.Min(Stacks, maxStacks)
                    + (Stacks == FlowingCloudRules.MaxStacks ? FlowingCloudRules.FullStackDamageBonus : 0) : 1;

        public bool RegisterHit(int segment, float now, CultivationRealm realm)
        {
            if (StrikeHit) return false;
            ExpireStacks(now);
            StrikeHit = true;
            AddStacks(now, realm, segment == 3 && realm >= CultivationRealm.GoldenCore ? 2 : 1);
            return true;
        }

        // Projectile hit confirmation is independent of whichever sword strike is currently playing.
        public void RegisterSwordTideHit(float now, CultivationRealm realm)
        {
            ExpireStacks(now);
            AddStacks(now, realm, 1);
        }

        void AddStacks(float now, CultivationRealm realm, int count)
        {
            if (realm >= CultivationRealm.QiRefining)
            {
                Stacks = Math.Min(Stacks + count, maxStacks);
                StackUntil = now + FlowingCloudRules.StackDuration;
            }
        }

        public int NextSegment(int previous, bool expired, CultivationRealm realm)
        {
            if (expired || previous < 0 || previous >= 3) return 0;
            if (previous == 2)
            {
                return realm >= CultivationRealm.GoldenCore && StrikeHit ? 3 : 0;
            }
            return previous + 1;
        }

        // A miss cannot add layers or restart their timer; only the existing deadline can expire them.
        public void FinishStrike(float now) => ExpireStacks(now);
        public void Clear()
        {
            ClearStacks(); ResetSequence();
        }
    }
}
