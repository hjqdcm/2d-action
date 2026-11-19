using System;
using System.Collections.Generic;

namespace SlashDemo
{
    sealed class EnemyCodexEntry
    {
        public readonly string Name, Location, PortraitResource;
        public readonly IReadOnlyList<(string Name, string Effect)> Skills;

        public EnemyCodexEntry(string name, string location, string portraitResource,
            params (string Name, string Effect)[] skills)
        {
            Name = name;
            Location = location;
            PortraitResource = portraitResource;
            Skills = Array.AsReadOnly(skills);
        }
    }

    static class EnemyCatalog
    {
        const string CultivatorArt = "SlashDemo/Cultivators/";

        public static IReadOnlyList<EnemyCodexEntry> All { get; } = Array.AsReadOnly(new[]
        {
            new EnemyCodexEntry("岩甲熊妖", "山野", BearDemon.ArtResource,
                ("冲撞", "蓄力后沿红色预警路线冲锋，可穿过同伴，撞到玩家或墙壁时停下。"),
                ("挥爪", "抬爪蓄力后横扫，对前方扇形范围内的目标造成伤害。")),
            new EnemyCodexEntry("碧鳞蛇妖", "山野", SnakeDemon.ArtResource,
                ("凝视噬咬", "凝视并蓄力，随后对红色扇形范围内的目标发动噬咬。"),
                ("吐毒", $"锁定玩家当时所在位置吐出毒液，落地留下持续 {SerpentVenomField.PoolDuration:0} 秒的毒池。"
                    + $"每 {SerpentVenomField.TickInterval:0.0} 秒造成 {SerpentVenomField.TickDamage} 点毒伤，同场最多两处，重叠时伤害不叠加。")),
            new EnemyCodexEntry("山魈猿妖", "山野", ApeDemon.ArtResource,
                ("长臂横扫", "摆臂蓄力后横扫身前扇形区域。"),
                ("跃起砸地", "以红圈预警落点，跃起后砸向地面，对落点周围造成范围伤害。")),

            new EnemyCodexEntry("体修", "升仙台", CultivatorArt + "BodyCultivator-v1",
                ("铁骨（被动）", "常驻减伤 15%。"),
                ("碎岩拳", "出拳击打前方近距离扇形区域。"),
                ("崩山踏", "踏击地面，对自身周围造成伤害。"),
                ("撼岳双震", "先震击近身圆形区域，再以第二道震波打击外圈。"),
                ("震脉架势", "架势期间减伤 50%，结束时震击周围，每承受一次命中提高反击伤害，最多计三次。")),
            new EnemyCodexEntry("剑修", "升仙台", CultivatorArt + "SwordCultivator-v1",
                ("疾剑（被动）", "移速提高 5%，技能前摇和后摇缩短 15%。"),
                ("回风斩", "挥剑斩击身前扇形区域。"),
                ("穿云剑", "沿正前方的窄直线出剑。"),
                ("十字剑阵", "以玩家位置为中心，发动两道互相垂直的剑击。"),
                ("掠影双冲", "连续两次沿预警路线冲刺，第二次重新瞄准，有时会在途中追加横斩。")),
            new EnemyCodexEntry("法修", "升仙台", CultivatorArt + "SpellCultivator-v1",
                ("移星（被动）", "首次降至半血时闪现拉开距离，并将所有技能的剩余冷却减半。"),
                ("落星诀", "在锁定位置落下一颗星，对小范围造成伤害。"),
                ("玄霜带", "沿前方宽直线释放霜带。"),
                ("三星连落", "连续三次锁定落点，分别降下星击。"),
                ("引星涡", "在目标位置留下持续 4.5 秒的星涡，将附近的玩家拉向中心。")),
            new EnemyCodexEntry("符修", "升仙台", CultivatorArt + "TalismanCultivator-v1",
                ("余符（被动）", "符术命中叠印，三层使玩家移速降低 35%，持续 3 秒。减速期间不叠印，6 秒未命中则消印。"),
                ("爆炎符", "在玩家所在位置引爆一张符。"),
                ("双生符", "在锁定位置两侧同时引爆两张符。"),
                ("四门封阵", "在锁定位置四周布下四处爆符，施法过程中也可能在玩家侧方升起结界。"),
                ("封路结界", "升起一面持续 5.5 秒的符墙，阻挡玩家通行。")),

            new EnemyCodexEntry("血刀邪修", "关底战场", CultivatorArt + "BloodSaberCultivator-Front-v3",
                ("吸血（被动）", "对玩家造成伤害后，回复实际扣除气血量的 10 倍。"),
                ("饮血横斩", "挥动血刀，横扫前方宽扇形区域。"),
                ("贯血刀", "沿狭长直线劈出一道血色刀气。"),
                ("裂地血潮", "插刀震击周围，并在原地留下持续 8 秒的伤害血雾。"),
                ("三途飞刃", "向前方斩出三道分散的飞刃。气血低于 50% 后，改为五道。"),
                ("追魂血匕", "放出追踪玩家的血匕，接触玩家或飞行到期时爆开，留下持续 4 秒的伤害血雾。"),
                ("血雾突袭", "化作血雾接近玩家，现身后挥刀横斩。"),
                ("血祭", "气血低于 75% 后可施放。消耗当前气血的 10%，在 20 秒内提高 20% 伤害、25% 攻速和 10% 移速。"),
                ("血雾升腾", "气血低于 25% 时进入最终阶段，施放期间无敌，并在战场升起持续造成伤害的血雾。")),
            new EnemyCodexEntry("摄魂铃邪修", "当前未出场", CultivatorArt + "SoulBellCultivator-Front-v1",
                ("摄魂铃震", "在锁定位置释放圆形铃震。"),
                ("十字断魄", "以玩家位置为中心，释放两道互相垂直的魂波。"),
                ("三叠追魂", "逐次重新锁定位置，连续释放三轮圆形铃震。"),
                ("镇魂回响", "以玩家位置为中心，释放仅打击外圈的环形魂波。"),
                ("四方锁魂阵", "先引爆锁定位置周围的四处魂印，再引爆阵眼。"))
        });
    }
}
