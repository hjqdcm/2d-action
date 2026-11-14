using System;
using System.Collections.Generic;

namespace SlashDemo
{
    enum TreasureQuality { White, Blue, Purple, Orange }

    readonly struct TreasureEntry
    {
        public readonly TreasureQuality Quality;
        public readonly string Name;
        public readonly string Effect;
        public readonly string IconResource;

        public TreasureEntry(TreasureQuality quality, string name, string effect)
        {
            Quality = quality;
            Name = name;
            Effect = effect;
            IconResource = "SlashDemo/Treasures/" + name;
        }
    }

    static class TreasureCatalog
    {
        public static IReadOnlyList<TreasureEntry> All { get; } = Array.AsReadOnly(new[]
        {
            new TreasureEntry(TreasureQuality.White, "磨剑石", "普攻伤害 +4%"),
            new TreasureEntry(TreasureQuality.White, "铁胎坠", "重击伤害 +5%"),
            new TreasureEntry(TreasureQuality.White, "轻羽扣", "普攻攻速 +4%"),
            new TreasureEntry(TreasureQuality.White, "双鱼玉环", "最大生命 +6%"),
            new TreasureEntry(TreasureQuality.White, "细鳞护符", "受到伤害 −3%"),
            new TreasureEntry(TreasureQuality.White, "青竹履", "移动速度 +4%"),
            new TreasureEntry(TreasureQuality.White, "束锋绳", "重击蓄力所需时间 −5%"),
            new TreasureEntry(TreasureQuality.White, "聚气珠", "Q 冷却时间 −4%"),
            new TreasureEntry(TreasureQuality.White, "刻星牌", "R 伤害 +5%"),
            new TreasureEntry(TreasureQuality.White, "藏息佩", "冲刺冷却时间 −5%"),
            new TreasureEntry(TreasureQuality.White, "素铜铃", "造成的伤害 +3%"),
            new TreasureEntry(TreasureQuality.White, "温玉瓶", "受到的治疗量 +8%"),
            new TreasureEntry(TreasureQuality.White, "辟毒针", "受到的持续伤害 −8%"),
            new TreasureEntry(TreasureQuality.White, "定心木", "受击硬直时间 −8%"),
            new TreasureEntry(TreasureQuality.White, "鹿纹带", "冲刺距离 +5%"),

            new TreasureEntry(TreasureQuality.Blue, "流泉剑穗", "普攻伤害 +6%，普攻攻速 +5%"),
            new TreasureEntry(TreasureQuality.Blue, "乌金镇纸", "重击伤害 +8%，重击蓄力所需时间 −6%"),
            new TreasureEntry(TreasureQuality.Blue, "青纹护心镜", "最大生命 +9%，受到伤害 −4%"),
            new TreasureEntry(TreasureQuality.Blue, "逐风环", "移动速度 +6%，冲刺冷却时间 −7%"),
            new TreasureEntry(TreasureQuality.Blue, "润脉葫芦", "受到的治疗量 +10%，最大生命 +7%"),
            new TreasureEntry(TreasureQuality.Blue, "回澜玉简", "Q 伤害 +8%，Q 冷却时间 −5%"),
            new TreasureEntry(TreasureQuality.Blue, "照星罗盘", "R 伤害 +9%，R 冷却时间 −5%"),
            new TreasureEntry(TreasureQuality.Blue, "飞翎匣", "普攻攻速 +7%，移动速度 +4%"),
            new TreasureEntry(TreasureQuality.Blue, "断潮石", "剑气伤害 +8%，剑气飞行距离 +10%"),
            new TreasureEntry(TreasureQuality.Blue, "万纹剑箍", "普攻伤害 +6%，普攻判定距离 +8%"),
            new TreasureEntry(TreasureQuality.Blue, "银纹镇铎", "造成的伤害 +5%，最大生命 +5%"),
            new TreasureEntry(TreasureQuality.Blue, "百草绦", "受到的治疗量 +12%，受到的持续伤害 −10%"),
            new TreasureEntry(TreasureQuality.Blue, "磐石扣", "受击硬直时间 −12%，受到伤害 −3%"),
            new TreasureEntry(TreasureQuality.Blue, "飞燕履", "冲刺距离 +8%，冲刺冷却时间 −5%"),
            new TreasureEntry(TreasureQuality.Blue, "明心镜", "主动技能冷却时间 −5%，移动速度 +4%"),

            new TreasureEntry(TreasureQuality.Purple, "听雨剑匣", "普攻伤害 +11%，普攻攻速 +9%"),
            new TreasureEntry(TreasureQuality.Purple, "沉岳印", "重击伤害 +15%，重击蓄力所需时间 −9%"),
            new TreasureEntry(TreasureQuality.Purple, "玄龟宝鉴", "最大生命 +14%，受到伤害 −7%"),
            new TreasureEntry(TreasureQuality.Purple, "回雪玉璧", "Q 伤害 +13%，Q 冷却时间 −9%"),
            new TreasureEntry(TreasureQuality.Purple, "七星铜盘", "R 伤害 +16%，R 冷却时间 −8%"),
            new TreasureEntry(TreasureQuality.Purple, "叠潮珏", "流云剑诀：满层流云疾斩完成后，3 秒内下一次普攻首次命中额外获得 1 层叠浪。每次 Q 最多触发一次。"),
            new TreasureEntry(TreasureQuality.Purple, "锁罡坠", "玄铁剑罡：满蓄力重击命中后，4 秒内下一次重击的蓄力所需时间 −25%。触发间隔 8 秒。"),
            new TreasureEntry(TreasureQuality.Purple, "回风结", "冲刺结束后 1.5 秒内，下一次普攻首次命中使冲刺剩余冷却减少 0.6 秒。每次冲刺最多触发一次。"),
            new TreasureEntry(TreasureQuality.Purple, "碎星筹", "五星剑斩的最后一道斩击命中至少一名敌人时，R 剩余冷却减少 3 秒。每次 R 最多触发一次。"),
            new TreasureEntry(TreasureQuality.Purple, "护脉灯", "一次受伤使生命从 35% 以上降至 35% 或以下时，获得相当于最大生命 12% 的护盾，持续 4 秒。触发间隔 35 秒。"),
            new TreasureEntry(TreasureQuality.Purple, "照夜珠", "造成的伤害 +9%，移动速度 +6%"),
            new TreasureEntry(TreasureQuality.Purple, "琉璃净瓶", "受到的治疗量 +18%，受到的持续伤害 −20%"),
            new TreasureEntry(TreasureQuality.Purple, "缚影铃", "首次直接命中一名敌人时，使其移动速度降低 25%，持续 2 秒。同一敌人每场战斗触发一次。"),
            new TreasureEntry(TreasureQuality.Purple, "纳露盏", "治疗超过生命上限时，将溢出量的 60% 转为护盾，上限为最大生命的 10%，持续 5 秒。再次触发可补充护盾并刷新持续时间。"),
            new TreasureEntry(TreasureQuality.Purple, "护元结", "受伤后，接下来 3 秒内受到的下一次伤害降低 30%。触发间隔 12 秒，从首次受伤时开始计算。"),

            new TreasureEntry(TreasureQuality.Orange, "无涯剑匣", "普攻伤害 +16%，普攻攻速 +14%"),
            new TreasureEntry(TreasureQuality.Orange, "天柱镇山印", "重击伤害 +22%，重击蓄力所需时间 −15%"),
            new TreasureEntry(TreasureQuality.Orange, "九转玄甲镜", "最大生命 +22%，受到伤害 −11%"),
            new TreasureEntry(TreasureQuality.Orange, "北辰天枢盘", "R 伤害 +25%，R 冷却时间 −12%"),
            new TreasureEntry(TreasureQuality.Orange, "沧溟归流珠", "流云剑诀：满层流云疾斩的追加斩击命中后，技能结束时返还 2 层叠浪。每次 Q 最多返还一次，返还层数计入叠浪上限。"),
            new TreasureEntry(TreasureQuality.Orange, "地脉镇锋印", "玄铁剑罡：满蓄力重击生成剑痕后，下一道满蓄力剑气若在 5 秒内穿过这道剑痕，就会提前引爆并消耗该剑痕。爆发伤害为原剑痕爆发的 120%。"),
            new TreasureEntry(TreasureQuality.Orange, "天关星枢", "每次消耗完整剑势施放五星剑斩后，下一轮累计所需次数降低。流云剑诀少 1 次，玄铁剑罡少 3 次。初次大招仍需完整蓄势。"),
            new TreasureEntry(TreasureQuality.Orange, "两仪定锋盘", "Q 直接命中后，5 秒内下一次满蓄力重击伤害 +35%。满蓄力重击直接命中后，5 秒内下一次 Q 伤害 +25%。两种增益各存一份，由对应招式首次命中消耗，后续段数也享受增伤。"),
            new TreasureEntry(TreasureQuality.Orange, "太虚回剑鞘", "连段终击直接命中后，向前追加一道剑气，伤害为该终击基础伤害的 50%，可受剑气伤害加成。触发间隔 4 秒。"),
            new TreasureEntry(TreasureQuality.Orange, "回天锁", "受到致命伤害时，生命保留为 1，并获得 1.2 秒无敌。无敌期间首次以普攻或重击直接命中敌人，回复最大生命的 15%。触发间隔 120 秒。"),
            new TreasureEntry(TreasureQuality.Orange, "日曜宝轮", "造成的伤害 +14%，主动技能冷却时间 −8%"),
            new TreasureEntry(TreasureQuality.Orange, "万象行舟", "移动速度 +12%，冲刺距离 +15%"),
            new TreasureEntry(TreasureQuality.Orange, "分光宝镜", "同一次攻击直接命中至少 2 名不同敌人时，在离自己最近的命中目标处引发一次震荡，对周围敌人造成该次攻击首段基础伤害的 60%。触发间隔 5 秒。"),
            new TreasureEntry(TreasureQuality.Orange, "同尘古印", "对同一敌人累计 4 次直接命中后，为其施加 4 秒印记，使你对其造成的伤害提高 20%。同一敌人每 10 秒可触发一次，冷却结束后重新计数。"),
            new TreasureEntry(TreasureQuality.Orange, "流火轮", "战斗中累计主动移动 8 个游戏单位后，下一次直接命中附带该段基础伤害 80% 的额外伤害。触发后清空累计距离，6 秒后可重新累计。"),
        });
    }
}
