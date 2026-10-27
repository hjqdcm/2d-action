# 外出场景素材

使用内置 image_gen 工具生成 / 编辑，无 CLI。三张图均为 1536 × 1024，参考坐标沿用左上角原点，世界尺寸 48 × 32。

- 升仙台：`../Resources/SlashDemo/AscensionPlatform-v1.png`，选用用户要求的朴素版，源 `exec-fe38b701-c6a7-45ad-8831-1275cc405443.png`。
- 山野：`../Resources/SlashDemo/Wilderness-v1.png`，选用减少花草后的朴素版，源 `exec-2d33b52b-b7bf-4a7f-be3a-d5e29e2faf27.png`。
- 医庐：`../Resources/SlashDemo/HealingLodge-open-v1.png`，基于原医庐仅修改门的开合，源 `exec-5220e060-9d8e-4bf6-84df-ac0b925b7442.png`。原始闭门图保留在生成目录。

医庐最终编辑提示词：

> 编辑这张疗伤医庐图片：让右下方的瓦顶大院门完全敞开，两片木门向两侧打开，中间露出通向院内的连贯石路；让上方房间连接庭院的双扇木门也完全打开，露出可进屋的通道。其余床榻、药柜、院落、建筑轮廓、山景、手绘画风、斜上方视角、构图尺寸全部保持原样。仅修改这两处门和开口，不要人物、文字或UI。

升仙台入口参考像素 (755,865)，山野 (810,865)，位于下方道路。医庐进入时直接传送到用户标注的院中「1」处，参考像素 (1050,600)。

医庐碰撞数据为 `../Resources/SlashDemo/HealingLodgeCollision-v1.json`：依据用户红线描摹，统一边界排除床榻、床前凳、药柜、围墙和水井，并保留床间通道、室内门口、院门及下方台阶。坐标原点位于参考图左上角。`Verification/TraceHealingLodgeCollision.py` 可从标注图重新导出碰撞数据，并生成 `Verification/Artifacts/HealingLodge-collision.png` 预览。

院门瓦顶使用原画中的屋顶轮廓生成独立前景精灵，绘制在角色之上；屋顶不添加地面碰撞体，角色可从下方通过。轮廓之外不重绘整块背景，避免地面覆盖角色。

`SceneTravelValidation.RunBatch` 包含医庐进入落点、角色半径下的室内/床间/台阶/院门通路、六处边界阻挡，以及门顶遮挡和无遮挡位置的实际渲染对比。验证图输出在运行项目的 `Verification/Artifacts` 下。

2026-10-26 验证通过：Unity 6000.6.7f1 验证副本完成上述 Physics2D 与实际渲染检查；两处屋顶下测试分别覆盖角色 7379、2475 个像素，院中和门外远处测试均为 0。三个外出目的地的场景切换、返回洞府、暂停和点击输入隔离回归通过。日志为 `Logs/HealingLodgeValidation.log`，碰撞预览及屋顶前后对比已复制到主项目 `Verification/Artifacts`。

出口只预留触发区域，不绑定返回洞府或其他目的地。三处场景均保留当前角色、血量、参数、视角与跟随行为。
