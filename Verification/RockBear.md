# 山野岩甲熊妖

当前山野画幅为 36×24 世界单位，使用独立于洞府的地图尺寸；背景、边界、入口、刷新点、寻路和传送阵落点共用该地图的坐标换算。洞府与其他目的地保持原尺寸。冲锋冷却 3.5 秒，冲锋前摇 1.0 秒。

每次进入山野，等概率选择三只岩甲熊妖或四只碧鳞蛇妖。以下熊妖说明适用于随机选中熊妖群时；蛇妖与随机遭遇见 `Verification/JadeSnake.md`。初始位置在远离南侧入口的北侧半区，沿水平直线等间距排列，原画坐标为 (510,360)、(810,360)、(1110,360)，相邻间距为 300 原画像素（7.031 世界单位）。三只均可自动追击角色，只使用撞击和爪击两个技能；原普通攻击由独立的 `BearCombat` 完整替换。所有怪物受击正常扣血，但不进入硬直、不被击退、不重置前摇或出招，持续攻击也不能使其无法反击。死亡仍会立即终止技能；击杀后本次探索不刷新，重新进入山野会重新生成三只。返回洞府或重新开始会清理三只熊妖并恢复洞府木桩。

群体生成验证入口：`SlashDemo.Editor.BearPackValidation.RunBatch`。检查三只存活熊妖、北侧半区、距入口足够远、水平等距排列、碰撞体避开地图边界、三只均追击，以及返程清理、再次进入不重复生成、重新开始恢复洞府。独立技能/步行动画检查会先验证三只的生成阵型，再隔离中间一只以避免其他熊妖干扰测试。

分散接近：`BearPackSteering` 为三只熊妖分配相隔约 120° 的偏好接近方向，以玩家为中心寻找可行走的近战站位；偏好站位不可行或被同伴占用时，选择附近有效方向。站位每 0.3 秒更新，玩家移动超过 0.5 世界单位时提前更新。靠近玩家后沿圆周绕向侧面，避免直穿玩家或全部追向同一点；同伴接近时施加软分离，中心距离小于双方碰撞半径之和加 0.65 时先调整站位再爪击。边界探测会选择可行移动方向；冲刺、前摇、挥爪和受击不受包围转向影响，保留原技能与冲刺穿过同伴的规则。只剩一只活跃熊妖时恢复直接追击。`BearSpacingValidation.Verify` 验证同侧散开、追随移动玩家、绕后、入口边界、拥挤时先让位、空间充足时仍能爪击及单只追击，由群体生成验证调用。

开阔场地物理模拟中，从同一侧接近及玩家移动后的最小两熊间距均约 3.25。入口等狭窄处会收紧软间距，爪击让位余量降至碰撞半径之和加 0.20，优先遵循既有寻路通路，避免强行绕圈挤向边界。避让判断使用刚体的当前物理位置，减少显示插值造成的位置延迟。分散追击、边界、近战衔接与同伴穿越回归均通过。

同伴碰撞：正常碰撞半径为 1.12。撞击的路线检测及扫掠判定忽略同伴，仅在冲刺期间临时忽略同伴的实体碰撞；玩家与墙壁仍会挡停。多只同时冲刺时，每一对碰撞体共同持有临时忽略状态，最后一只结束后恢复原碰撞设置。结束时若与同伴重叠，会在不穿过墙壁/玩家的前提下选择附近空位，必要时退回最近的安全位置。技能停止、死亡、禁用和离场也会释放临时忽略状态。`BearAllyChargeValidation.Verify` 验证穿过同伴并命中玩家、无友伤或推挤、结束不重叠、撞墙、同时冲刺、保留预先存在的碰撞排除，以及禁用再启用后的恢复；由群体生成验证调用。

默认生命 240，移动速度 1.8，爪击伤害 12，前摇约 0.84 秒，爪击距离约 2.67（最初范围的 150%）。沿用调试面板的生命/速度倍率；伤害与普通敌人参数一致，前摇、范围分别为普通敌人参数的 1.45、2.325 倍。

角色不能推动熊妖：熊妖使用 Kinematic 刚体，由 AI 独立控制位移；角色行走或冲刺接触熊妖时，碰撞只阻挡角色，不会将熊妖顶走。普通移动通过 `BearDemon.ConstrainVelocity` 的圆形扫掠与贴边滑动避开墙壁、玩家及同伴；撞击继续沿用专用扫掠和临时忽略同伴碰撞的规则。群体验证中的 `BEAR_PLAYER_PUSH_PASS` 检查持续行走/冲刺顶住静止熊妖、角色不能穿过熊妖，以及移动熊妖的位置只遵循自身 AI 速度。

验证记录：`Logs/BearPlayerPush-pack-verified.log`，推动、冲刺穿过同伴、分散追击、入口碰撞边界和场景清理全部通过。手动模拟的分散测试每步清零无输入角色的速度，以对应实际角色 `FixedUpdate`，避免碰撞速度在测试内持续累积；入口仍要求所有熊接近至 5 单位内，额外检查熊之间不重叠、至少一只到达攻击距离。

撞击：距离约 2.8–8.5 且路线无阻挡时选择（近处优先爪击）。前摇 1.0 秒，显示红色冲刺通道并压低身体；锁定前摇开始时的方向，以速度 10 冲刺，最大距离 8.5，默认伤害 18（爪击伤害的 1.5 倍取整）。冲刺期间用双层土黄色弧线与四条气流拖尾形成破风标志，结束、死亡或离场时隐藏。扫掠碰撞检测防止高速穿过玩家和墙壁；单次最多命中一次，命中玩家、撞墙或达到距离后结束。冷却 3.5 秒，可横移躲避。

爪击：近距离选择，显示扇形预警。前摇时整体抬起前爪，0.32 秒挥爪阶段中段执行一次原有近战扇形伤害判定，默认伤害 12；随后收爪。冷却 1.8 秒。爪子仍按刚性图层运动，不使用顶点拉伸。两个技能都有约 0.85 秒收招，暂停时冻结，死亡/离开地图时清理。所有阶段具有霸体：受击正常扣血，前摇、挥爪、冲刺和收招均不被攻击或 `Interrupt` 取消，也不会受击退影响。死亡、命中玩家、撞墙和达到冲刺距离仍正常结束冲刺。`EnemyUninterruptibleValidation.Verify` 由技能验证调用，连续重击覆盖爪击前摇/挥爪/收招、撞击前摇，以及普通敌人的连续受击反击、扣血、无击退及死亡清理；冲刺穿过同伴并命中玩家仍由群体测试覆盖。

预警样式：熊妖爪击扇形与撞击通道、其他敌人的攻击扇形统一使用红色，线宽为修改前的 40%；熊妖 0.055→0.022，其他敌人 0.045→0.018。实际爪击判定、预警半径和挥爪轨迹同步调整为最初的 150%。验证覆盖原范围之外的实际爪击命中、预警半径与判定一致、所有预警颜色和线宽，以及土黄色破风标志的开始/结束。之前 v5 预览记录的是 200% 范围及白蓝色气流，已由当前 150% 范围与土黄色气流替代。

技能实机验证入口 `SlashDemo.Editor.BearSkillValidation.RunBatch`（仅对临时项目副本运行，会退出 Unity）。已验证独立技能控制、爪击抬爪与单次伤害、冲刺预警、暂停冻结、锁定方向、横移闪避、撞击单次命中、撞墙停止、连续受击不打断、死亡和返程清理，并检查动画图层长度不变。预览：`Verification/Artifacts/Bear-claw-skill.gif`、`Verification/Artifacts/Bear-charge-skill.gif`。

外观为透明的 2.5D 精灵，带水平转向、行走起伏、攻击前倾、受击闪白和死亡淡出。

当前四爪移动动画：`BearWalkMesh` 将原画分成躯干和四肢五块，使用固定 UV，四肢仅绕关节整体旋转、平移。另有四块独立的关节覆盖层，使用各关节附近的原画毛发/岩甲，沿躯干与肢体的中间位置和角度移动，边缘渐隐。遮挡层覆盖活动接缝并盖住切边，减少断裂和背景穿透。爪子与关节覆盖层均保持刚性形状，不使用顶点权重拉伸。覆盖层继承转向、受击颜色、死亡透明度及深度排序。步频由实际位移驱动，停止时回到站姿，出招时停止步行，暂停与顿帧冻结相位。

接缝回归：`BearJointValidation.Verify` 由技能验证调用，检查 28 个走路、抬爪、撞击蓄力和镜像姿势中的四个关节中心不露背景；比较开启/关闭覆盖层的渲染，确认填补了原本露背景的像素。同时检查九块图层的边长与对角线在全部姿势中不变，保持爪子和岩甲形状。移动预览：`Verification/Artifacts/Bear-joints-walk-v4.gif`。

第三版验证逐帧检查五块图层各自六条顶点连线长度恒定，排除拉伸、剪切、压扁；同时检查四爪抬起和前摆、停止复位、暂停冻结、镜像、固定 UV、200 FPS/50 Hz 模拟与着色器。实际追击、攻击、死亡和返程检查通过。近景循环：`Verification/Artifacts/RockBear-rigid-walk-v3.gif`；实机追击：`Verification/Artifacts/RockBear-rigid-live-v3.gif`。更新后退出播放、重新运行并进入山野，以重新初始化熊妖。下述第二版记录仅保留作历史说明，其连续网格方案已由第三版替代。

第二版加强爪子动作：前后步幅由 ±0.22 提至 ±0.30，抬爪由 0.16 提至 0.26，摆腿时间由周期的 28% 提至 38%，增加脚腕弯曲。使用渲染插值后的角色位置驱动相位，并平滑速度，防止渲染帧率高于物理帧率时零位移帧反复压低动作幅度。扩大腿部权重过渡范围，减少边缘拉扯。验证包含 200 FPS/50 Hz 模拟，以及真实追击时四爪的持续运动；640×480、相机尺寸 5 时各爪的动画位移范围约 39–41 像素。实机录制预览：`Verification/Artifacts/RockBear-walk-v2-live.gif`。更新脚本后退出播放、重新运行并进入山野，以重新初始化熊妖网格。

素材：`Assets/SlashDemo/Resources/SlashDemo/RockBear-v1.png`。使用内置 image_gen，以用户附图为角色设计参考生成。保留生成的透明通道。

最终生成提示词：

> Use case: stylized-concept. Asset type: production transparent sprite for an oblique 2.5D Chinese cultivation action RPG. Input image is a character design reference only. Generate ONE full body massive bear demon matching the reference: brown thick fur, recognizable bear head, small round ears, amber eyes, massive forepaws with long dark claws, jagged grey stone armor over shoulders, back and forearms with glowing warm golden cracks. Three quarter view facing toward bottom right, slightly elevated camera, standing powerful hunched quadruped pose, all four feet fully visible, feet along bottom. Isolated character centered with ample transparent padding, no scenery, no rocks on ground, no baked shadow, no text or UI. Realistic hand-painted game art, readable silhouette and details at small sprite size. Actual transparent background.

批处理验证入口：`SlashDemo.Editor.BearDemonValidation.RunBatch`。请对临时项目副本运行；该入口会进入播放模式并在完成后退出 Unity。验证实际追击、攻击扣血、暂停、受击死亡、场景清理、再次进入、其他目的地与重新开始。截图保存到运行项目的 `Verification/Artifacts/RockBear-wilderness.png`。
