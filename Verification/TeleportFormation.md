# 清场传送阵

素材：`Assets/SlashDemo/Resources/SlashDemo/TeleportFormation-v1.png`。使用内置 image_gen（imagegen 技能）生成，保留真实透明通道；以地面 XY 平面上的 Sprite 显示，随现有斜视镜头自然投影，位于人物下方。青玉灵光、金色符纹、八卦与太极图案，出现时渐显并轻微呼吸。

在有怪物的探索地图中，击败最后一只存活怪物时生成一个传送阵；洞府木桩、尚未清空的怪物及原本没有怪物的场景不会触发。生成位置优先靠近最后一只怪物，向附近搜索能容纳阵图的场地，避开边界与障碍。传送阵不阻挡行走。

走入中心 1.65 世界单位内显示“F 开启传送阵”，按 F 后暂停并复用洞府大门的“选择去处”界面，目的地直接使用 `TravelMaps.Choices`，当前为升仙台、山野、疗伤医庐。进入范围不会自动传送；范围外按 F 无效。取消后继续探索，需要重新按 F 才会再打开。选择场景后卸载旧地图与传送阵；再次选择山野会重新生成三只熊妖。传送保留角色、气血、功法、境界与调试参数。返回洞府仍恢复原有木桩；重新开始清理地图和传送阵。

验证入口：`SlashDemo.Editor.TeleportFormationValidation.RunBatch`，只在临时项目副本中运行，会退出 Unity。覆盖最后一击才出现、唯一生成、实际素材绑定、无实体碰撞、范围外 F、进入等待、真实键盘 F 事件、暂停选择、取消与重新开启、全部目的地、旧地图清理、角色气血保留、木桩恢复和重新开始。日志 `Logs/TeleportFormation.log`，预览 `Verification/Artifacts/Teleport-formation-world.png`。

生成方式：内置 image_gen，`transparent_background=true`。最终提示词：

> Use case: stylized-concept. Asset type: production transparent ground-effect sprite for a Chinese xianxia cultivation 2.5D action RPG. Primary request: one elegant ancient Taoist teleportation formation, jade cyan spiritual light and restrained antique gold inscriptions, concentric engraved rings, eight auspicious trigram-like abstract talisman segments, fine cloud-scroll ornament and a luminous yin-yang inspired central seal. Composition: strictly orthographic TOP DOWN, perfectly circular, flat on the ground with subtle luminous ink-like wisps, all elements centered within the circle, generous transparent padding. Style: refined realistic hand-painted fantasy game VFX asset, readable at small size, beautiful fine gold linework on turquoise light, center transparent enough to see ground. Constraints: isolated single magic circle only, true transparent background including open spaces within the design, no scenery, no people, no platform thickness, no upright portal, no perspective ellipse, no large vertical light beams, no interface, no letters or readable words, no watermark. The game engine will project this flat circle onto its own angled ground plane.
