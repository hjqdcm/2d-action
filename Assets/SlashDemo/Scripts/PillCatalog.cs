using System;
using System.Collections.Generic;

namespace SlashDemo
{
    static class PillCatalog
    {
        public static IReadOnlyList<AlchemyEntry> All { get; } = Array.AsReadOnly(new[]
        {
            Pill(AlchemyQuality.White, "强身丹", "最大生命 +8%"),
            Pill(AlchemyQuality.White, "锐气丹", "造成的伤害 +5%"),
            Pill(AlchemyQuality.White, "铁衣散", "受到伤害 −5%"),
            Pill(AlchemyQuality.White, "轻身丸", "移动速度 +6%"),
            Pill(AlchemyQuality.White, "回元散", "受到的治疗量 +12%"),
            Pill(AlchemyQuality.White, "疾风丹", "普攻速度 +7%"),
            Pill(AlchemyQuality.White, "蓄劲丸", "重击蓄力时间 −8%"),
            Pill(AlchemyQuality.White, "清毒散", "受到的持续伤害 −15%"),
            Pill(AlchemyQuality.White, "定神丸", "受击硬直时间 −12%"),
            Pill(AlchemyQuality.White, "行气丹", "主动技能冷却时间 −5%"),

            Pill(AlchemyQuality.Blue, "玉髓丹", "最大生命 +12%，受到伤害 −4%"),
            Pill(AlchemyQuality.Blue, "振元丹", "造成的伤害 +8%，普攻速度 +5%"),
            Pill(AlchemyQuality.Blue, "铁骨丸", "受到伤害 −8%，受击硬直时间 −10%"),
            Pill(AlchemyQuality.Blue, "御风散", "移动速度 +8%，冲刺冷却时间 −10%"),
            Pill(AlchemyQuality.Blue, "生津丹", "受到的治疗量 +18%，最大生命 +6%"),
            Pill(AlchemyQuality.Blue, "澄脉丹", "主动技能冷却时间 −8%，主动技能伤害 +6%"),
            Pill(AlchemyQuality.Blue, "壮气丹", "重击伤害 +12%，重击蓄力时间 −8%"),
            Pill(AlchemyQuality.Blue, "涤尘丹", "受到的持续伤害 −25%，受到的治疗量 +10%"),
            Pill(AlchemyQuality.Blue, "逐燕丸", "冲刺距离 +12%，移动速度 +5%"),
            Pill(AlchemyQuality.Blue, "聚神丹", "大招伤害 +12%，大招冷却时间 −6%"),

            Pill(AlchemyQuality.Purple, "紫金壮骨丹", "最大生命 +18%，受到伤害 −8%"),
            Pill(AlchemyQuality.Purple, "凝锋丹", "造成的伤害 +12%，普攻速度 +8%"),
            Pill(AlchemyQuality.Purple, "净莲丹", "受到的持续伤害 −40%，受到的治疗量 +15%"),
            Pill(AlchemyQuality.Purple, "青冥行气丹", "主动技能冷却时间 −12%，主动技能伤害 +10%"),
            Pill(AlchemyQuality.Purple, "沉岳炼体丹", "重击伤害 +18%，重击蓄力时间 −14%"),
            Pill(AlchemyQuality.Purple, "回春玉露丹", "每场战斗结束后回复最大生命的 8%"),
            Pill(AlchemyQuality.Purple, "玄龟护体丹", "每场战斗首次受伤时，该次伤害降低 50%"),
            Pill(AlchemyQuality.Purple, "飞鸿丹", "冲刺结束后，移动速度 +20%，持续 2 秒。触发间隔 5 秒。"),
            Pill(AlchemyQuality.Purple, "丹霞聚神丹", "每场战斗首次施放大招时，该次大招伤害 +25%"),
            Pill(AlchemyQuality.Purple, "赤心丹", "生命低于 40% 时，造成的伤害 +18%，受到伤害 −8%"),

            Pill(AlchemyQuality.Orange, "九转金身丹", "最大生命 +25%，受到伤害 −12%"),
            Pill(AlchemyQuality.Orange, "太一凝神丹", "造成的伤害 +18%，主动技能冷却时间 −12%"),
            Pill(AlchemyQuality.Orange, "乾元破岳丹", "重击伤害 +28%，重击蓄力时间 −20%"),
            Pill(AlchemyQuality.Orange, "凌虚御风丹", "移动速度 +14%，冲刺距离 +16%，冲刺冷却时间 −12%"),
            Pill(AlchemyQuality.Orange, "万象养元丹", "受到的治疗量 +30%，每场战斗结束后回复最大生命的 10%"),
            Pill(AlchemyQuality.Orange, "开阳醒神丹", "每场战斗首次施放大招后，立即清除主动技能的剩余冷却"),
            Pill(AlchemyQuality.Orange, "天罡护命丹", "每场战斗首次受伤后获得相当于最大生命 18% 的护盾，持续 6 秒"),
            Pill(AlchemyQuality.Orange, "赤霄破阵丹", "每场战斗中，对每名敌人的首次直接命中伤害 +35%"),
            Pill(AlchemyQuality.Orange, "玄元无垢丹", "受到的持续伤害 −55%，受击硬直时间 −30%"),
            Pill(AlchemyQuality.Orange, "归墟续命丹", "药效期间限一次。受到致命伤害时，生命保留为 1，并获得 1.5 秒无敌。"),
        });

        static AlchemyEntry Pill(AlchemyQuality quality, string name, string effect)
            => new AlchemyEntry(quality, name, "持续 3 场战斗\n" + effect, "SlashDemo/Pills/" + name);
    }
}
