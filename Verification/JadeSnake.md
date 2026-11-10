# 山野碧鳞蛇妖与随机遭遇

首次进入山野时随机排列岩甲熊妖、碧鳞蛇妖和山魈猿妖的顺序，之后按此顺序轮换，任意连续三次各遇到一种。熊妖群三只、蛇妖群四只、猿妖群三只，沿用北侧水平等距刷新点。清场后生成传送阵，其他目的地不生成山野妖兽。

蛇妖以用户附图为设计参考，保留青绿鳞片、浅色腹甲、金色竖瞳、玉色长角和盘绕蛇身，使用透明 2.5D 精灵，带分段游动和左右转向。生命 150，沿用生命调试倍率；移动速度为人物基础移速的 0.52 倍（人物默认 5.4 时为 2.808），出生和调参均使用该比例；普攻噬咬基础伤害为 8 点，前摇为敌人参数的 1.16 倍，攻击半径为熊妖扇形攻击半径的 0.9 倍（敌人范围参数的 2.0925 倍，默认约 2.4064）。出生与调参共用熊妖范围倍率，红色扇形预警、特效和伤害判定同步读取该半径。显示红色扇形预警，锁定出招方向，可横移躲避。受击扣血但不打断出招、不被击退。蛇妖使用独立的普通噬咬流程，不使用熊妖的爪子分层动画。

两种妖兽共用 `WildernessNavigation` 的圆形扫掠、边界路径搜索和滑动避障，熊妖原接口保留。蛇妖采用 Kinematic 刚体，角色无法推动。沿用原有死亡、血条、暂停、最后一只死亡生成传送阵、返程及重开清理。

素材：`Assets/SlashDemo/Resources/SlashDemo/JadeSnake-v1.png`。使用内置 image_gen，保留生成的透明通道。最终提示词：

> Use case: stylized-concept. Asset type: production transparent sprite for an oblique 2.5D Chinese cultivation action RPG, matching a realistically hand-painted rock bear enemy. Image 1 is a character design reference only. Create ONE full body serpent demon based closely on it: emerald and deep teal overlapping scales with gold edges, creamy ivory segmented underside, golden slit eyes, two long pale jade swept-back horns, small dorsal spines, long thin forked tongue. No legs, no wings, no human body. Compact heavy coiled lower body resting on ground with clearly visible curled tail and an elegant raised S-shaped neck, head facing bottom right in three-quarter view, slightly elevated game camera. Whole body visible including horns and tail, centered, generous transparent margin, ground contact near bottom center. Realistic richly detailed game painting, strong readable silhouette at small game size. Actual transparent background. No mountain scenery, no ground rocks, no environment, no text, no UI, no baked ground shadow, no large magical effects.

实机验证入口：`SlashDemo.Editor.WildernessEncounterValidation.RunBatch`。只对临时项目副本执行，会退出 Unity。验证随机分支、蛇妖阵型与边界、追击、噬咬伤害、暂停、受击死亡、清场传送阵、不自动补怪、熊妖导航/同伴冲锋/间距回归、再次进入与重开清理。既有熊妖专项测试通过独立随机种子选择熊妖分支，避免随机遭遇造成测试偶发失败。日志：`Logs/WildernessEncounter.log`；预览：`Verification/Artifacts/JadeSnake-wilderness.png`。

验证结果：Unity 6000.6.7f1 在临时项目副本中编译与 Play 实测通过，日志包含 `WILDERNESS_ENCOUNTER_PASS`、`BEAR_PLAYER_PUSH_PASS`、`BEAR_ALLY_CHARGE_PASS` 和 `BEAR_SPACING_PASS`。已检查实际渲染截图。素材为 1254×1254 PNG，边角 alpha 为 0，保留真实透明通道。

## 蛇妖移动与攻击动画

`SnakeAnimation` 当前为三段叠层动画：盘绕蛇身、头颈、尾部各自使用固定原画 UV 和刚性转动/平移，错开相位。默认盘身摆角 1.2°，头颈 5.5°，尾部 10°，同时头颈有小幅侧移；实际位移驱动循环，停止后回到站姿。鳞片、腹甲与头角在每一段中保持长度和形状，不再对整张图片施加波浪变形。Shader 沿颈根和尾部交界分层并保留重叠区，遮住关节连接。攻击继续沿用原有颈部伸出与噬咬判定，暂停冻结，镜像、死亡透明度与离场清理继承原流程。

`SnakeCombat` 独立控制追击、后缩蓄力、快速探头噬咬、收招。前摇沿用调试参数，噬咬阶段 0.24 秒、收招 0.75 秒；探头达到最远距离时执行一次扇形伤害判定。整个出招锁定方向，受击不取消攻击；暂停冻结，死亡、禁用及离场清理动画和预警。根碰撞体在攻击中保持原位，蛇颈向锁定方向伸出。

动画验证入口：`SlashDemo.Editor.SnakeAnimationValidation.RunBatch`，仅在临时副本运行（会退出 Unity）。覆盖实际追击动画、移动全过程所有三角形边长恒定、轻微摆幅、头角刚性、UV、网格无翻折、镜像、停止、高帧率、Shader，以及攻击命中时机、单次伤害、躲避、受击、暂停和死亡清理。日志：`Logs/SnakeAnimation.log`；逐帧预览：`Verification/Artifacts/SnakeAnimation/`。

动画验证结果：Unity 6000.6.7f1 编译、Shader 与 Play 实测通过，日志包含 `SNAKE_MESH_PASS`、`SNAKE_BITE_PASS`、`SNAKE_ANIMATION_PASS`。已检查蓄力、探头噬咬与游动的实际渲染。预览：`Verification/Artifacts/Snake-walk.gif`、`Snake-bite.gif`、`Snake-live.gif`。原山野集成验证与熊妖防推动、同伴冲锋、群体间距回归均通过，见 `Logs/SnakeAnimation-EncounterRegression.log`。

移动动画修正：取消移动阶段所有局部形变，改为保持整张原画形状的轻微刚性摆动。`Logs/SnakeRigidGlide.log` 的 Play 验证通过，检查了整个移动周期内全部网格三角形边长恒定，鳞片和腹甲不再拉伸；攻击、暂停、停止、镜像与死亡清理也通过。新预览：`Verification/Artifacts/Snake-glide-v2.gif`、`Snake-bite-v2.gif`。


第三版：回应“移动动画几乎看不见”的问题，用盘身、头颈、尾部独立的错相摆动替代第二版的整图微晃。验证不只检查无拉伸，还要求头颈相对盘身的运动范围超过 0.16 世界单位、尾部超过 0.10；遍历各段的所有三角形边，验证整个移动周期长度不变。日志：`Logs/SnakeSegments-v3.log`；逐帧：`Verification/Artifacts/SnakeSegments-v3/`；预览：`Verification/Artifacts/Snake-segments-walk-v3.gif`、`Snake-segments-live-v3.gif` 和 `Snake-segments-bite-v3.gif`。第二版记录保留为历史说明。

第三版最终结果：Unity 编译、Shader 和 Play 验证通过，`SNAKE_MESH_PASS`、`SNAKE_BITE_PASS`、`SNAKE_ANIMATION_PASS` 均通过。已检查实际移动与噬咬渲染，预览包含完整 32 帧移动周期。


第四版修复双尾：自由尾部和回勾尾尖统一仅归尾部层绘制，从盘身和头颈层同时剔除原尾尖；重叠只保留在尾根连接处。保留第三版盘身、头颈与尾部独立摆动的幅度和节奏。增加实际 GPU 遮罩审计，在尾部/尾尖区域采样 70 点，检查盘身、头颈均无绘制，尾部层全部绘制。`Logs/SnakeSingleTail-v4.log` 包含 `SNAKE_SINGLE_TAIL_PASS`、`SNAKE_MESH_PASS`、`SNAKE_BITE_PASS` 和 `SNAKE_ANIMATION_PASS`；Unity Play 验证通过，检查完整 32 帧移动周期的实际渲染。新预览：`Verification/Artifacts/Snake-single-tail-walk-v4.gif`、`Snake-single-tail-live-v4.gif` 与 `Snake-single-tail-bite-v4.gif`。


第五版修复剩余尾部重影：第四版仅验证尾尖区域，仍保留了自由尾根外侧的重复贴图。当前尾部层与盘身/头颈层使用完全相同的尾部区域划分，整条尾巴不再通过扩大遮罩做重叠。连接处的运动跟随盘身，在尾根的小范围内平滑过渡为尾部摆动，避免移除重叠后露缝。自由尾部和尾尖仍保持刚性摆动。

GPU 验证扩展为完整 UV 图，审计 8154 个尾部覆盖像素，除抗锯齿切边的子像素混色外，尾部内部与盘身/头颈的重复绘制为 0；原来的重叠条带也在检查范围内。完整 32 帧周期检查尾根与盘身接点一致、自由尾部不拉伸、网格不翻折，噬咬、暂停、停止、镜像及死亡清理均通过。日志 `Logs/SnakeTailPartition-v5.log`，包含 `SNAKE_SINGLE_TAIL_PASS`、`SNAKE_MESH_PASS`、`SNAKE_BITE_PASS`、`SNAKE_ANIMATION_PASS`。已检查放大尾部渲染。预览：`Verification/Artifacts/Snake-tail-partition-tailclose-v5.gif`、`Snake-tail-partition-walk-v5.gif`。


第五版近景复核补充：回弯尾段的下沿此前落在 `y < .46` 的盘身区域，因而与摆动的上沿错位。当前将整段回弯曲线一起归到尾部，根部使用径向过渡跟随盘身，取消尾部层任何扩大绘制。GPU 审计除整尾重复覆盖外，另检查覆盖回弯下沿至尾尖的 252 个区域点；整尾内部重复像素为 0。32 帧近景中回弯尾段连贯，无双轮廓或切开的尾段。此前只测尾尖的第四版检查不充分，已由完整范围审计替代。


## 凝视攻击特效

参考英雄联盟蛇女「石化凝视」的眼部施法、扇形能量爆发节奏，为青鳞蛇妖制作原创程序特效。参考来源：[Riot 官方技能介绍](https://na.leagueoflegends.com/en/featured/mid-season-magic)、[英雄页面](https://www.leagueoflegends.com/en-us/champions/cassiopeia/)。使用项目自己的网格、Shader、粒子和配色。

`SerpentGazeVfx` 当前版本：已移除独立的眼部光晕，前摇保留淡青色扇形蓄势；探头噬咬的伤害帧触发一次整片扇形闪光，随后向外扩散青绿能量流与淡金弧形波前，0.48 秒消散。命中时补充石色碎光。红色预警和伤害规则保持原状，特效取现有 105° 扇形与实时攻击半径，不额外产生伤害或控制。末端弧线位于实际攻击范围边缘，施法方向锁定，落空也显示爆发。暂停冻结；死亡、禁用、离场会隐藏并清理特效。

新增资源：`Scripts/SerpentGazeVfx.cs`、`Resources/SlashDemo/SerpentGaze.shader`。`SnakeCombat` 驱动特效时钟和触发帧，避免视觉与命中分别计时。特效起点跟随实际动画位置，当前版本不创建眼部发光网格或材质。

验证：`SlashDemo.Editor.SnakeAnimationValidation.RunBatch` 在临时项目副本中通过。日志 `Logs/SerpentGaze-v1.log` 含 `SERPENT_GAZE_RENDER_PASS`（开关特效实际改变 999 个像素）、`SERPENT_GAZE_PASS`、`SNAKE_SINGLE_TAIL_PASS`、`SNAKE_MESH_PASS`、`SNAKE_BITE_PASS` 与 `SNAKE_ANIMATION_PASS`。覆盖实际 Shader 渲染、前摇、伤害帧只触发一次、方向/范围一致、落空、消散、暂停、禁用与死亡清理，亦回归蛇身游动及尾部不重复绘制。已检查蓄力、命中爆发与扩散波前的实机渲染。

预览：`Verification/Artifacts/Serpent-gaze-v1.gif`；截图：`Serpent-gaze-v1.png`；原始帧：`Verification/Artifacts/SnakeGaze-v1/Bite/`。


## 凝视特效与参数第二版

按用户要求移除眼部发光的独立网格、材质和 Shader 分支。技能攻击半径扩大 50%：默认由 2.415 到 3.6225；前摇缩短 20%：默认由 0.841 到 0.6728 秒。蛇妖出生参数与调试参数采用相同倍率（范围 3.15、前摇 1.16），重复调参不累乘；熊妖参数保持原规则。105° 扇形角度沿用原设置，红色预警、特效边缘与实际伤害均读取新的攻击半径。

验证日志：`Logs/SerpentGaze-v2.log`。实际命中目标放在新范围的 95% 处，位于旧伤害范围与角色判定余量之外，真实命中通过；检查缩短后的前摇、眼部发光渲染器已删除、重复应用调试参数稳定、特效边缘与新范围一致。`SERPENT_GAZE_RENDER_PASS`、`SERPENT_GAZE_PASS` 与蛇妖动画、尾部、噬咬、暂停/死亡清理回归通过。已检查去掉眼部光晕及扩大扇形的实机渲染。新预览：`Verification/Artifacts/Serpent-gaze-v2.gif`，按缩短后的前摇时长播放；逐帧：`Verification/Artifacts/SnakeGaze-v2/Bite/`。


## 吐毒技能与四蛇遭遇

蛇妖改为四只，每只基础生命 180，总生命 720，与三只 240 生命熊妖相同；沿用生命与移速调试倍率。北侧原画刷新点为 (450,360)、(690,360)、(930,360)、(1170,360)，均在地图边界内。熊妖仍为三只，随机选择种类的概率不变。

在原有近身扇形凝视之外，蛇妖追击时可选择吐毒：目标位于近身范围之外、8.5 世界单位以内且路线通畅时，开始 1 秒蓄力并锁定当时主角所在位置，用红圈标出落点。蓄力与吐毒期间停止移动，受到攻击正常扣血但不打断；主角之后移动不会改变落点。0.24 秒吐毒动作中喷出毒液，飞行 0.5 秒并呈抛物线落下，形成半径 2.15625、持续 4 秒的毒池。踩入会掉血，离开后不追加中毒；固定每 0.5 秒 3 点，保留冲刺和技能无敌保护，毒伤独立于普通攻击的 0.8 秒受伤免疫计时，毒池不会推动角色。

初始平衡：吐毒冷却 6 秒，首次吐毒按阵型槽错开；共享 0.9 秒的蓄力启动间隔，同场最多两处技能区域（包含蓄力、飞行、毒池），重叠毒池共享每 0.5 秒一次的伤害时钟，不叠加 DOT。近距离仍优先使用原有凝视攻击。相比熊妖 1 秒蓄力、3.5 秒冷却、8.5 距离和默认 18 点冲锋伤害，蛇妖吐毒更容易通过走位躲开，但能临时封锁地面；数值以四蛇总血量与铺毒限制作为初始强度基准，尚非长期玩家平衡测试结论。

`SerpentVenomField` 由地图持有，所以已喷出的毒液在单个施法者死亡后继续存在；最后一只妖兽死亡时统一清除毒液，再生成传送阵。暂停冻结蓄力、飞行、毒池时长与伤害时钟；返程、换地图和重新开始均清理。只有蓄力预警会在施法者死亡或禁用时立即取消。

新增资源：`Scripts/SerpentVenomField.cs`、`Resources/SlashDemo/SerpentVenom.shader`；`SnakeCombat` 增加独立的吐毒蓄力与喷吐状态。`SnakeDemon` 的 180 生命与四只阵型在激活前初始化。

验证：`SlashDemo.Editor.SnakeVenomValidation.RunBatch`，仅在临时项目副本执行（会退出 Unity）。`Logs/SnakeVenom-v1.log` 包含 `SNAKE_VENOM_PASS`；覆盖四只低血量蛇妖、安全刷新、初始锁点、移动后仍命中旧位置、整秒蓄力、投射物落地、实际每秒伤害、重叠不叠伤、两处上限、错峰、冷却、暂停、受击、施法者死亡后保留、到期、离开停止伤害、清场传送阵、返程、再次进入与重开。`Logs/SnakeVenom-WildernessRegression.log` 与 `Logs/SnakeVenom-AnimationRegression.log` 的随机遭遇、熊妖防推动/冲锋穿越/间距、原有凝视范围/前摇/无眼部光晕和尾部动画回归均通过。已检查实际飞行毒液和地面毒池的渲染。

预览：`Verification/Artifacts/Serpent-venom-v1.gif`；毒池近景：`Verification/Artifacts/SnakeVenom-v1/Pool/frame-000.png`；四蛇阵型：`Verification/Artifacts/SnakeVenom-v1/Formation/frame-000.png`。


## 体型、普攻与毒池调整

蛇妖原画显示高度缩小 15%（2.7 → 2.295 世界单位），碰撞半径同步从 1.12 调为 0.952，寻路扫掠使用相同半径，阴影同步缩小，血条高度随新体型定位。仍为四只，每只基础生命 180。

普攻半径在上一版基础上缩小 20%（默认 3.6225 → 2.898），调试参数倍率从 3.15 改为 2.52。预警、特效边界和伤害判定统一读取缩小后的范围，105° 扇形角度、前摇和吐毒距离保留现有设置。

毒池改为固定每 0.5 秒造成 3 点原始伤害，不再由敌人普攻伤害除以 3 计算。地图共享毒伤时钟，重叠不叠加。`SlashPlayer.TakePoisonDamage` 沿用现有扣血、减伤、反馈与死亡流程，但不受普通受击的 0.8 秒免疫拦截，也不为普通攻击重置免疫时间；冲刺、功法无敌与调试无敌仍阻挡毒伤。普通 `TakeDamage` 的接口及 0.8 秒受击免疫保留。

验证：`Logs/SnakeVenom-v2.log` 检查四只小体型蛇妖、缩小的物理半径、正常攻击连续命中的免疫仍然有效、毒伤在正常受伤免疫期间即时生效、实际游戏时钟内两个半秒周期合计扣 6 点，以及重叠限制、暂停、锁点、毒池寿命和场景清理。截图等待改按游戏内毒池时钟，避免离线录制耗时影响时间断言。预览：`Verification/Artifacts/Serpent-venom-v2.gif`；逐帧：`Verification/Artifacts/SnakeVenom-v2/`。

最终验证：`Logs/SnakeSizeRange-v3.log` 中 `SERPENT_GAZE_PASS` 验证新半径内命中、仅位于原大半径内的目标落空、预警/特效半径同步，以及前摇和调参倍率不累乘；`SNAKE_MESH_PASS`、`SNAKE_SINGLE_TAIL_PASS`、`SNAKE_BITE_PASS`、`SNAKE_ANIMATION_PASS` 均通过。已检查缩小体型与新范围的实际渲染。普攻预览：`Verification/Artifacts/Serpent-gaze-v3.gif`。

毒池半径第三版：按要求在 1.15 基础上扩大 50% 至 1.725，落点红圈、毒池网格和脚下伤害判定共用同一个半径。每 0.5 秒 3 点伤害、4 秒寿命、场上两处上限和重叠不叠伤保留。`Logs/SnakeVenom-v3.log` 的 `SNAKE_VENOM_PASS` 通过：检查红圈各点与毒池显示半径，并将角色放在距圆心 1.6 世界单位处（超出旧半径连同角色碰撞余量），实测连续半秒扣血；暂停、到期、清场和返程清理均通过。新预览：`Verification/Artifacts/Serpent-venom-v3.gif`，毒池近景：`Verification/Artifacts/SnakeVenom-v3/Pool/frame-000.png`。


毒池半径第四版：在 1.725 基础上再增加 25% 至 2.15625。落点红圈、毒池显示、飞溅/薄雾分布和伤害判定共用该半径；每 0.5 秒 3 点伤害、4 秒寿命及区域数量限制保持现有设置。范围验证已更新为新半径，并将新增覆盖区的命中测试移至距圆心 2.1 世界单位处；本次尚未重跑 Unity 实机验证。

基础生命调整为每只 150，四只蛇妖合计基础生命 600；出生与调参继续通过 BaseHealth 和生命调试倍率应用。
