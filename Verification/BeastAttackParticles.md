# 熊妖与蛇妖攻击粒子

## 蛇妖扇形辨识度增强

扇面改用更明亮的青绿色，蓄力时加入沿半径流动的纹理；爆发时提高扇面透明度和波前亮度，增加两道后随弧形波纹、流动光脉与淡金色边缘。普攻碎粒由 36 个增至 48 个，另在扇形末端加入 12 个短寿命亮屑，继续使用原有共享粒子批次与 384 个粒子上限。

特效仍由原有攻击进度驱动，爆发持续 0.48 秒。扇形范围沿用熊妖的 0.9 倍，普攻基础伤害为 8；暂停、落空与清理仍走原有逻辑。

Unity 6000.6.7f1 临时项目实测通过，日志为 `Logs/SerpentGaze-visibility-v3.log`：扇面渲染改变 2351 个实际像素，攻击粒子改变 2801 个实际像素；`SERPENT_GAZE_PASS`、`SNAKE_BITE_PASS`、`SNAKE_ANIMATION_PASS` 通过，覆盖范围一致、单次命中、落空、暂停和禁用/死亡清理。已检查实际渲染。预览：`Verification/Artifacts/SerpentGaze-visibility-v3/preview.gif`；爆发截图：同目录 `impact.png`。

这次为两种妖兽补充出招与命中的粒子表现，技能数值沿用工作区当前设置。

- 熊妖：撞击蓄力时脚下扬尘，冲锋时留下土黄色尘土与小石块，碰到玩家或墙壁时碎石飞散；挥爪伤害帧迸出短促金色火星、石屑与地面尘土。
- 蛇妖：普攻扇形爆发时散出青绿色毒光碎粒、少量淡金细屑与薄雾；吐毒时口部喷出小液滴，飞行毒团带液滴拖尾，落地迸溅，毒池持续产生低密度薄雾和上浮气泡。没有恢复眼部发光。

`BeastAttackParticles` 由地图共享，使用一个网格批量绘制软烟、石屑、毒滴、亮屑和气泡，最多同时 384 个粒子，不为每个粒子创建 GameObject。内部随机数不改变遭遇类型的全局随机序列。粒子只有视觉功能，不参与碰撞、伤害和技能范围。使用游戏时钟，暂停和顿帧冻结；寿命结束自动移除，禁用或卸载地图时统一清空。新粒子在出招当帧的 LateUpdate 刷新，减少首次显示的延迟。

新增文件：`Assets/SlashDemo/Scripts/BeastAttackParticles.cs`、`Assets/SlashDemo/Resources/SlashDemo/BeastParticles.shader`。触发位置位于 `BearCombat`、`SnakeCombat` 和 `SerpentVenomField` 的原有攻击事件及毒池更新中。

Unity 6000.6.7f1 对临时项目副本实测通过：

- `Logs/BeastParticles-Bear-v1.log`：`BEAST_PARTICLES_PASS`、`BEAST_ATTACK_RENDER_PASS`、`BEAR_PARTICLES_PASS`、`BEAR_SKILLS_PASS`。检查渲染、暂停、数量上限和寿命；真实爪击粒子改变 2093 个画面像素，撞击粒子改变 10029 个像素；原有爪击伤害、冲锋命中/躲避、撞墙、受击不打断与返程清理通过。
- `Logs/BeastParticles-Venom-v1.log`：`BEAST_PARTICLES_PASS`、`SNAKE_PARTICLES_PASS`、`SNAKE_VENOM_PASS`。真实喷吐、落地飞溅、毒池薄雾/气泡与清理通过；毒池半径、半秒 3 点伤害、重叠不叠伤、锁定原落点、暂停、寿命及地图流程通过。
- `Logs/BeastParticles-Gaze-v1.log`：`BEAST_ATTACK_RENDER_PASS`、`GAZE_PARTICLES_PASS`、`SERPENT_GAZE_PASS`、`SNAKE_ANIMATION_PASS`。实际普攻粒子改变 1737 个像素，与伤害帧同步；使用最新普攻范围参数，动画、尾部遮罩、无眼部光晕与暂停/清理回归通过。

已检查实际出招和命中画面。预览为测试项目实机录制，分别展示熊妖爪击/冲锋和蛇妖普攻/吐毒：

- `Verification/Artifacts/Bear-claw-particles-v1.gif`
- `Verification/Artifacts/Bear-charge-particles-v1.gif`
- `Verification/Artifacts/Snake-gaze-particles-v1.gif`
- `Verification/Artifacts/Snake-venom-particles-v1.gif`

原始截图与逐帧记录：`Verification/Artifacts/BeastParticles-v1/`。熊妖录制保留命中后的收招阶段，以便检查粒子飞散，不再在出招结束时立即切换测试位置。
