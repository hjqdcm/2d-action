using System;
using System.Collections.Generic;

namespace SlashDemo
{
    static class PillCatalog
    {
        const int DefaultCombatDurationSeconds = 60;
        internal const int RecoveryIntervalSeconds = 5;
        static readonly Dictionary<AlchemyEntry, ((AlchemyStat Stat, float Amount)[] Stats, float ImmediateHeal, float HealPerTick)> definitions
            = new Dictionary<AlchemyEntry, ((AlchemyStat, float)[], float, float)>();

        public static IReadOnlyList<AlchemyEntry> All { get; } = Array.AsReadOnly(new[]
        {
            RecoveryPill(AlchemyQuality.White, "强身丹", .01f),
            Pill(AlchemyQuality.White, "锐气丹", "造成的伤害 +5%", (AlchemyStat.Damage, .05f)),
            Pill(AlchemyQuality.White, "铁衣散", "受到伤害 −5%", (AlchemyStat.Reduction, .05f)),
            Pill(AlchemyQuality.White, "轻身丸", "移动速度 +6%", (AlchemyStat.Move, .06f)),
            ImmediatePill(AlchemyQuality.White, "回元散", .10f),
            Pill(AlchemyQuality.White, "疾风丹", "普攻速度 +7%", (AlchemyStat.AttackSpeed, .07f)),
            Pill(AlchemyQuality.White, "蓄劲丸", "重击蓄力时间 −8%", (AlchemyStat.ChargeReduction, .08f)),
            Pill(AlchemyQuality.White, "清毒散", "受到的持续伤害 −15%", (AlchemyStat.DotReduction, .15f)),
            Pill(AlchemyQuality.White, "定神丸", "受击硬直时间 −12%", (AlchemyStat.HitRecovery, .12f)),
            Pill(AlchemyQuality.White, "行气丹", "主动技能冷却时间 −5%", (AlchemyStat.TechniqueCooldown, .05f)),
            Pill(AlchemyQuality.White, "凝目丸", "暴击率 +3%", (AlchemyStat.CritChance, .03f)),

            RecoveryPill(AlchemyQuality.Blue, "玉髓丹", .015f),
            Pill(AlchemyQuality.Blue, "振元丹", "造成的伤害 +8%，普攻速度 +5%", (AlchemyStat.Damage, .08f), (AlchemyStat.AttackSpeed, .05f)),
            Pill(AlchemyQuality.Blue, "铁骨丸", "受到伤害 −8%，受击硬直时间 −10%", (AlchemyStat.Reduction, .08f), (AlchemyStat.HitRecovery, .10f)),
            Pill(AlchemyQuality.Blue, "御风散", "移动速度 +8%，冲刺冷却时间 −10%", (AlchemyStat.Move, .08f), (AlchemyStat.DashCooldown, .10f)),
            ImmediatePill(AlchemyQuality.Blue, "生津丹", .16f),
            Pill(AlchemyQuality.Blue, "澄脉丹", "主动技能冷却时间 −8%，主动技能伤害 +6%", (AlchemyStat.TechniqueCooldown, .08f), (AlchemyStat.TechniqueDamage, .06f)),
            Pill(AlchemyQuality.Blue, "壮气丹", "重击伤害 +12%，重击蓄力时间 −8%", (AlchemyStat.HeavyDamage, .12f), (AlchemyStat.ChargeReduction, .08f)),
            Pill(AlchemyQuality.Blue, "涤尘丹", "受到的持续伤害 −25%，受到的治疗量 +10%", (AlchemyStat.DotReduction, .25f), (AlchemyStat.Healing, .10f)),
            Pill(AlchemyQuality.Blue, "逐燕丸", "冲刺距离 +12%，移动速度 +5%", (AlchemyStat.DashDistance, .12f), (AlchemyStat.Move, .05f)),
            Pill(AlchemyQuality.Blue, "聚神丹", "大招伤害 +12%，大招冷却时间 −6%", (AlchemyStat.UltimateDamage, .12f), (AlchemyStat.UltimateCooldown, .06f)),
            Pill(AlchemyQuality.Blue, "透骨丹", "暴击伤害 +15%", (AlchemyStat.CritDamage, .15f)),

            Pill(AlchemyQuality.Purple, "紫金壮骨丹", "受到伤害 −10%，受到的治疗量 +18%", (AlchemyStat.Reduction, .10f), (AlchemyStat.Healing, .18f)),
            Pill(AlchemyQuality.Purple, "凝锋丹", "造成的伤害 +12%，普攻速度 +8%", (AlchemyStat.Damage, .12f), (AlchemyStat.AttackSpeed, .08f)),
            Pill(AlchemyQuality.Purple, "净莲丹", "受到的持续伤害 −40%，受到的治疗量 +15%", (AlchemyStat.DotReduction, .40f), (AlchemyStat.Healing, .15f)),
            Pill(AlchemyQuality.Purple, "青冥行气丹", "主动技能冷却时间 −12%，主动技能伤害 +10%", (AlchemyStat.TechniqueCooldown, .12f), (AlchemyStat.TechniqueDamage, .10f)),
            Pill(AlchemyQuality.Purple, "沉岳炼体丹", "重击伤害 +18%，重击蓄力时间 −14%", (AlchemyStat.HeavyDamage, .18f), (AlchemyStat.ChargeReduction, .14f)),
            ImmediatePill(AlchemyQuality.Purple, "回春玉露丹", .24f),
            Pill(AlchemyQuality.Purple, "玄龟护体丹", "受到伤害 −15%", (AlchemyStat.Reduction, .15f)),
            Pill(AlchemyQuality.Purple, "飞鸿丹", "移动速度 +14%，冲刺冷却时间 −12%", (AlchemyStat.Move, .14f), (AlchemyStat.DashCooldown, .12f)),
            Pill(AlchemyQuality.Purple, "丹霞聚神丹", "大招伤害 +20%，大招冷却时间 −10%", (AlchemyStat.UltimateDamage, .20f), (AlchemyStat.UltimateCooldown, .10f)),
            RecoveryPill(AlchemyQuality.Purple, "赤心丹", .02f),
            Pill(AlchemyQuality.Purple, "洞微丹", "暴击率 +8%，暴击伤害 +15%", (AlchemyStat.CritChance, .08f), (AlchemyStat.CritDamage, .15f)),

            Pill(AlchemyQuality.Orange, "九转金身丹", "受到伤害 −20%，受击硬直时间 −22%", (AlchemyStat.Reduction, .20f), (AlchemyStat.HitRecovery, .22f)),
            Pill(AlchemyQuality.Orange, "太一凝神丹", "造成的伤害 +18%，主动技能冷却时间 −12%", (AlchemyStat.Damage, .18f), (AlchemyStat.TechniqueCooldown, .12f)),
            Pill(AlchemyQuality.Orange, "乾元破岳丹", "重击伤害 +28%，重击蓄力时间 −20%", (AlchemyStat.HeavyDamage, .28f), (AlchemyStat.ChargeReduction, .20f)),
            Pill(AlchemyQuality.Orange, "凌虚御风丹", "移动速度 +14%，冲刺距离 +16%，冲刺冷却时间 −12%", (AlchemyStat.Move, .14f), (AlchemyStat.DashDistance, .16f), (AlchemyStat.DashCooldown, .12f)),
            ImmediatePill(AlchemyQuality.Orange, "万象养元丹", .36f),
            Pill(AlchemyQuality.Orange, "开阳醒神丹", "主动技能伤害 +15%，主动技能冷却时间 −18%", (AlchemyStat.TechniqueDamage, .15f), (AlchemyStat.TechniqueCooldown, .18f)),
            Pill(AlchemyQuality.Orange, "天罡护命丹", "受到伤害 −16%，受到的治疗量 +25%", (AlchemyStat.Reduction, .16f), (AlchemyStat.Healing, .25f)),
            Pill(AlchemyQuality.Orange, "赤霄破阵丹", "造成的伤害 +22%，普攻速度 +12%", (AlchemyStat.Damage, .22f), (AlchemyStat.AttackSpeed, .12f)),
            Pill(AlchemyQuality.Orange, "玄元无垢丹", "受到的持续伤害 −55%，受击硬直时间 −30%", (AlchemyStat.DotReduction, .55f), (AlchemyStat.HitRecovery, .30f)),
            RecoveryPill(AlchemyQuality.Orange, "归墟续命丹", .035f),
            Pill(AlchemyQuality.Orange, "破妄明心丹", "暴击率 +12%，暴击伤害 +25%", (AlchemyStat.CritChance, .12f), (AlchemyStat.CritDamage, .25f)),
        });

        public static ((AlchemyStat Stat, float Amount)[] Stats, float ImmediateHeal, float HealPerTick) Definition(AlchemyEntry entry)
            => definitions[entry];

        static AlchemyEntry Pill(AlchemyQuality quality, string name, string effect, params (AlchemyStat, float)[] stats)
            => Register(quality, name, effect, DefaultCombatDurationSeconds, stats);

        static AlchemyEntry ImmediatePill(AlchemyQuality quality, string name, float fraction)
            => Register(quality, name, $"立即回复最大生命的 {fraction * 100:0.#}%", 0, immediateHeal: fraction);

        static AlchemyEntry RecoveryPill(AlchemyQuality quality, string name, float fraction)
            => Register(quality, name, $"每 {RecoveryIntervalSeconds} 秒回复最大生命的 {fraction * 100:0.#}%",
                DefaultCombatDurationSeconds, healPerTick: fraction);

        static AlchemyEntry Register(AlchemyQuality quality, string name, string effect, int duration,
            (AlchemyStat, float)[] stats = null, float immediateHeal = 0, float healPerTick = 0)
        {
            var entry = new AlchemyEntry(quality, name, effect, "SlashDemo/Pills/" + name, duration);
            definitions.Add(entry, (stats ?? Array.Empty<(AlchemyStat, float)>(), immediateHeal, healPerTick));
            return entry;
        }
    }
}
