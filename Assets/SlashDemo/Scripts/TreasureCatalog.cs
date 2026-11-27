using System;
using System.Collections.Generic;

namespace SlashDemo
{
    enum AlchemyQuality { White, Blue, Purple, Orange }

    readonly struct AlchemyEntry
    {
        public readonly AlchemyQuality Quality;
        public readonly string Name;
        public readonly string Effect;
        public readonly string IconResource;
        public readonly int CombatDurationSeconds;

        public AlchemyEntry(AlchemyQuality quality, string name, string effect, string iconResource, int combatDurationSeconds = 0)
        {
            Quality = quality;
            Name = name;
            Effect = combatDurationSeconds > 0 ? $"持续 {combatDurationSeconds} 秒战斗时间\n{effect}" : effect;
            IconResource = iconResource;
            CombatDurationSeconds = combatDurationSeconds;
        }
    }

    static class TreasureCatalog
    {
        static readonly Dictionary<AlchemyEntry, ((AlchemyStat Stat, float Amount)[] Stats, TreasureTrigger Trigger)> definitions
            = new Dictionary<AlchemyEntry, ((AlchemyStat, float)[], TreasureTrigger)>();
        public static IReadOnlyList<AlchemyEntry> All { get; } = Array.AsReadOnly(new[]
        {
            Treasure(AlchemyQuality.White, "磨剑石", "普攻伤害 +4%", (AlchemyStat.NormalDamage, .04f)),
            Treasure(AlchemyQuality.White, "铁胎坠", "重击伤害 +5%", (AlchemyStat.HeavyDamage, .05f)),
            Treasure(AlchemyQuality.White, "轻羽扣", "普攻攻速 +4%", (AlchemyStat.AttackSpeed, .04f)),
            Treasure(AlchemyQuality.White, "双鱼玉环", "最大生命 +6%", (AlchemyStat.MaxHealth, .06f)),
            Treasure(AlchemyQuality.White, "细鳞护符", "受到伤害 −3%", (AlchemyStat.Reduction, .03f)),
            Treasure(AlchemyQuality.White, "青竹履", "移动速度 +4%", (AlchemyStat.Move, .04f)),
            Treasure(AlchemyQuality.White, "束锋绳", "重击蓄力所需时间 −5%", (AlchemyStat.ChargeReduction, .05f)),
            Treasure(AlchemyQuality.White, "聚气珠", "Q 冷却时间 −4%", (AlchemyStat.TechniqueCooldown, .04f)),
            Treasure(AlchemyQuality.White, "刻星牌", "R 伤害 +5%", (AlchemyStat.UltimateDamage, .05f)),
            Treasure(AlchemyQuality.White, "藏息佩", "冲刺冷却时间 −5%", (AlchemyStat.DashCooldown, .05f)),
            Treasure(AlchemyQuality.White, "素铜铃", "造成的伤害 +3%", (AlchemyStat.Damage, .03f)),
            Treasure(AlchemyQuality.White, "温玉瓶", "受到的治疗量 +8%", (AlchemyStat.Healing, .08f)),
            Treasure(AlchemyQuality.White, "辟毒针", "受到的持续伤害 −8%", (AlchemyStat.DotReduction, .08f)),
            Treasure(AlchemyQuality.White, "定心木", "受击硬直时间 −8%", (AlchemyStat.HitRecovery, .08f)),
            Treasure(AlchemyQuality.White, "鹿纹带", "冲刺距离 +5%", (AlchemyStat.DashDistance, .05f)),
            Treasure(AlchemyQuality.White, "白锋石", "暴击率 +2%", (AlchemyStat.CritChance, .02f)),
            Treasure(AlchemyQuality.White, "断玉坠", "暴击伤害 +8%", (AlchemyStat.CritDamage, .08f)),

            Treasure(AlchemyQuality.Blue, "流泉剑穗", "普攻伤害 +6%，普攻攻速 +5%", (AlchemyStat.NormalDamage, .06f), (AlchemyStat.AttackSpeed, .05f)),
            Treasure(AlchemyQuality.Blue, "乌金镇纸", "重击伤害 +8%，重击蓄力所需时间 −6%", (AlchemyStat.HeavyDamage, .08f), (AlchemyStat.ChargeReduction, .06f)),
            Treasure(AlchemyQuality.Blue, "青纹护心镜", "最大生命 +9%，受到伤害 −4%", (AlchemyStat.MaxHealth, .09f), (AlchemyStat.Reduction, .04f)),
            Treasure(AlchemyQuality.Blue, "逐风环", "移动速度 +6%，冲刺冷却时间 −7%", (AlchemyStat.Move, .06f), (AlchemyStat.DashCooldown, .07f)),
            Treasure(AlchemyQuality.Blue, "润脉葫芦", "受到的治疗量 +10%，最大生命 +7%", (AlchemyStat.Healing, .10f), (AlchemyStat.MaxHealth, .07f)),
            Treasure(AlchemyQuality.Blue, "回澜玉简", "Q 伤害 +8%，Q 冷却时间 −5%", (AlchemyStat.TechniqueDamage, .08f), (AlchemyStat.TechniqueCooldown, .05f)),
            Treasure(AlchemyQuality.Blue, "照星罗盘", "R 伤害 +9%，R 冷却时间 −5%", (AlchemyStat.UltimateDamage, .09f), (AlchemyStat.UltimateCooldown, .05f)),
            Treasure(AlchemyQuality.Blue, "飞翎匣", "普攻攻速 +7%，移动速度 +4%", (AlchemyStat.AttackSpeed, .07f), (AlchemyStat.Move, .04f)),
            Treasure(AlchemyQuality.Blue, "断潮石", "剑气伤害 +8%，剑气飞行距离 +10%", (AlchemyStat.WaveDamage, .08f), (AlchemyStat.WaveDistance, .10f)),
            Treasure(AlchemyQuality.Blue, "万纹剑箍", "普攻伤害 +6%，普攻判定距离 +8%", (AlchemyStat.NormalDamage, .06f), (AlchemyStat.NormalReach, .08f)),
            Treasure(AlchemyQuality.Blue, "银纹镇铎", "造成的伤害 +5%，最大生命 +5%", (AlchemyStat.Damage, .05f), (AlchemyStat.MaxHealth, .05f)),
            Treasure(AlchemyQuality.Blue, "百草绦", "受到的治疗量 +12%，受到的持续伤害 −10%", (AlchemyStat.Healing, .12f), (AlchemyStat.DotReduction, .10f)),
            Treasure(AlchemyQuality.Blue, "磐石扣", "受击硬直时间 −12%，受到伤害 −3%", (AlchemyStat.HitRecovery, .12f), (AlchemyStat.Reduction, .03f)),
            Treasure(AlchemyQuality.Blue, "飞燕履", "冲刺距离 +8%，冲刺冷却时间 −5%", (AlchemyStat.DashDistance, .08f), (AlchemyStat.DashCooldown, .05f)),
            Treasure(AlchemyQuality.Blue, "明心镜", "主动技能冷却时间 −5%，移动速度 +4%", (AlchemyStat.TechniqueCooldown, .05f), (AlchemyStat.Move, .04f)),
            Treasure(AlchemyQuality.Blue, "鹰瞳玉", "暴击率 +4%，暴击伤害 +10%", (AlchemyStat.CritChance, .04f), (AlchemyStat.CritDamage, .10f)),

            Treasure(AlchemyQuality.Purple, "听雨剑匣", "普攻伤害 +11%，普攻攻速 +9%", (AlchemyStat.NormalDamage, .11f), (AlchemyStat.AttackSpeed, .09f)),
            Treasure(AlchemyQuality.Purple, "沉岳印", "重击伤害 +15%，重击蓄力所需时间 −9%", (AlchemyStat.HeavyDamage, .15f), (AlchemyStat.ChargeReduction, .09f)),
            Treasure(AlchemyQuality.Purple, "玄龟宝鉴", "最大生命 +14%，受到伤害 −7%", (AlchemyStat.MaxHealth, .14f), (AlchemyStat.Reduction, .07f)),
            Treasure(AlchemyQuality.Purple, "回雪玉璧", "Q 伤害 +13%，Q 冷却时间 −9%", (AlchemyStat.TechniqueDamage, .13f), (AlchemyStat.TechniqueCooldown, .09f)),
            Treasure(AlchemyQuality.Purple, "七星铜盘", "R 伤害 +16%，R 冷却时间 −8%", (AlchemyStat.UltimateDamage, .16f), (AlchemyStat.UltimateCooldown, .08f)),
            Treasure(AlchemyQuality.Purple, "叠潮珏", "流云剑诀：满层流云疾斩完成后，3 秒内下一次普攻首次命中额外获得 1 层叠浪。每次 Q 最多触发一次。", TreasureTrigger.CloudRebuild),
            Treasure(AlchemyQuality.Purple, "锁罡坠", "玄铁剑罡：满蓄力重击命中后，4 秒内下一次重击的蓄力所需时间 −25%。触发间隔 8 秒。", TreasureTrigger.IronCharge),
            Treasure(AlchemyQuality.Purple, "回风结", "冲刺结束后 1.5 秒内，下一次普攻首次命中使冲刺剩余冷却减少 0.6 秒。每次冲刺最多触发一次。", TreasureTrigger.DashRefund),
            Treasure(AlchemyQuality.Purple, "碎星筹", "五星剑斩的最后一道斩击命中至少一名敌人时，R 剩余冷却减少 3 秒。每次 R 最多触发一次。", TreasureTrigger.UltimateRefund),
            Treasure(AlchemyQuality.Purple, "护脉灯", "一次受伤使生命从 35% 以上降至 35% 或以下时，获得相当于最大生命 12% 的护盾，持续 4 秒。触发间隔 35 秒。", TreasureTrigger.LowHealthShield),
            Treasure(AlchemyQuality.Purple, "照夜珠", "造成的伤害 +9%，移动速度 +6%", (AlchemyStat.Damage, .09f), (AlchemyStat.Move, .06f)),
            Treasure(AlchemyQuality.Purple, "琉璃净瓶", "受到的治疗量 +18%，受到的持续伤害 −20%", (AlchemyStat.Healing, .18f), (AlchemyStat.DotReduction, .20f)),
            Treasure(AlchemyQuality.Purple, "缚影铃", "首次直接命中一名敌人时，使其移动速度降低 25%，持续 2 秒。同一敌人每场战斗触发一次。", TreasureTrigger.FirstHitSlow),
            Treasure(AlchemyQuality.Purple, "纳露盏", "治疗超过生命上限时，将溢出量的 60% 转为护盾，上限为最大生命的 10%，持续 5 秒。再次触发可补充护盾并刷新持续时间。", TreasureTrigger.OverhealShield),
            Treasure(AlchemyQuality.Purple, "护元结", "受伤后，接下来 3 秒内受到的下一次伤害降低 30%。触发间隔 12 秒，从首次受伤时开始计算。", TreasureTrigger.HitGuard),
            Treasure(AlchemyQuality.Purple, "照隙镜", "暴击率 +7%，暴击伤害 +16%", (AlchemyStat.CritChance, .07f), (AlchemyStat.CritDamage, .16f)),

            Treasure(AlchemyQuality.Orange, "无涯剑匣", "普攻伤害 +16%，普攻攻速 +14%", (AlchemyStat.NormalDamage, .16f), (AlchemyStat.AttackSpeed, .14f)),
            Treasure(AlchemyQuality.Orange, "天柱镇山印", "重击伤害 +22%，重击蓄力所需时间 −15%", (AlchemyStat.HeavyDamage, .22f), (AlchemyStat.ChargeReduction, .15f)),
            Treasure(AlchemyQuality.Orange, "九转玄甲镜", "最大生命 +22%，受到伤害 −11%", (AlchemyStat.MaxHealth, .22f), (AlchemyStat.Reduction, .11f)),
            Treasure(AlchemyQuality.Orange, "北辰天枢盘", "R 伤害 +25%，R 冷却时间 −12%", (AlchemyStat.UltimateDamage, .25f), (AlchemyStat.UltimateCooldown, .12f)),
            Treasure(AlchemyQuality.Orange, "沧溟归流珠", "流云剑诀：满层流云疾斩的追加斩击命中后，技能结束时返还 2 层叠浪。每次 Q 最多返还一次，返还层数计入叠浪上限。", TreasureTrigger.CloudFinisher),
            Treasure(AlchemyQuality.Orange, "地脉镇锋印", "玄铁剑罡：满蓄力重击生成剑痕后，下一道满蓄力剑气若在 5 秒内穿过这道剑痕，就会提前引爆并消耗该剑痕。爆发伤害为原剑痕爆发的 120%。", TreasureTrigger.EarlyScarBurst),
            Treasure(AlchemyQuality.Orange, "天关星枢", "每次消耗完整剑势施放五星剑斩后，下一轮累计所需次数降低。流云剑诀少 1 次，玄铁剑罡少 3 次。初次大招仍需完整蓄势。", TreasureTrigger.UltimateRequirement),
            Treasure(AlchemyQuality.Orange, "两仪定锋盘", "Q 直接命中后，5 秒内下一次满蓄力重击伤害 +35%。满蓄力重击直接命中后，5 秒内下一次 Q 伤害 +25%。两种增益各存一份，由对应招式首次命中消耗，后续段数也享受增伤。", TreasureTrigger.TechniqueHeavyLink),
            Treasure(AlchemyQuality.Orange, "太虚回剑鞘", "连段终击直接命中后，向前追加一道剑气，伤害为该终击基础伤害的 50%，可受剑气伤害加成。触发间隔 4 秒。", TreasureTrigger.FinisherWave),
            Treasure(AlchemyQuality.Orange, "回天锁", "受到致命伤害时，生命保留为 1，并获得 1.2 秒无敌。无敌期间首次以普攻或重击直接命中敌人，回复最大生命的 15%。触发间隔 120 秒。", TreasureTrigger.LethalGuard),
            Treasure(AlchemyQuality.Orange, "日曜宝轮", "造成的伤害 +14%，主动技能冷却时间 −8%", (AlchemyStat.Damage, .14f), (AlchemyStat.TechniqueCooldown, .08f)),
            Treasure(AlchemyQuality.Orange, "万象行舟", "移动速度 +12%，冲刺距离 +15%", (AlchemyStat.Move, .12f), (AlchemyStat.DashDistance, .15f)),
            Treasure(AlchemyQuality.Orange, "分光宝镜", "同一次攻击直接命中至少 2 名不同敌人时，在离自己最近的命中目标处引发一次震荡，对周围敌人造成该次攻击首段基础伤害的 60%。触发间隔 5 秒。", TreasureTrigger.MultiHitBurst),
            Treasure(AlchemyQuality.Orange, "同尘古印", "对同一敌人累计 4 次直接命中后，为其施加 4 秒印记，使你对其造成的伤害提高 20%。同一敌人每 10 秒可触发一次，冷却结束后重新计数。", TreasureTrigger.TargetMark),
            Treasure(AlchemyQuality.Orange, "流火轮", "战斗中累计主动移动 8 个游戏单位后，下一次直接命中附带该段基础伤害 80% 的额外伤害。触发后清空累计距离，6 秒后可重新累计。", TreasureTrigger.MovementHit),
            Treasure(AlchemyQuality.Orange, "破虚宝鉴", "暴击率 +10%，暴击伤害 +25%", (AlchemyStat.CritChance, .10f), (AlchemyStat.CritDamage, .25f)),
        });

        public static ((AlchemyStat Stat, float Amount)[] Stats, TreasureTrigger Trigger) Definition(AlchemyEntry entry)
            => definitions[entry];

        static AlchemyEntry Treasure(AlchemyQuality quality, string name, string effect, params (AlchemyStat, float)[] stats)
            => Treasure(quality, name, effect, TreasureTrigger.None, stats);

        static AlchemyEntry Treasure(AlchemyQuality quality, string name, string effect, TreasureTrigger trigger,
            params (AlchemyStat, float)[] stats)
        {
            var entry = new AlchemyEntry(quality, name, effect, "SlashDemo/Treasures/" + name);
            definitions.Add(entry, (stats, trigger));
            return entry;
        }
    }
}
