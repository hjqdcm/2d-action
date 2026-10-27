# 私人洞府庄园 v1

- 方式：内置 `image_gen` 图像编辑工具，非 CLI。
- 参考：`PrivateManor-reference.png`，来自本次对话中用户确认的小型庄园图。
- 游戏素材：`../Resources/SlashDemo/PrivateManor-v1.png`，1536 × 1024。
- XY 世界范围：48 × 32（原 24 × 16 的长宽各放大 2 倍，`LayoutScale=2`）；保留 `SlashCamera`、`ObliqueView` 与 `ObliqueProjection` 的现有实现。所有静态物件碰撞和前景切片均以原图左上角像素坐标登记，随整个地面一起投影；出生点、木桩位置、波次分布与训练场阵纹同步放大，角色自身尺寸不变。
- 中央出生像素：`(670, 520)`；训练场中心：`(1240, 370)`；右侧通行门洞位于 `y=434..510`。静态木人已从画面去除，由原游戏的三个真实可攻击木桩代替。
- 原图与整理后的底图分别保存，不覆盖参考图或任何角色素材。动态创建的前景 Sprite 共用原底图，并在环境销毁时释放。

## 最终编辑提示词（原文）

Use case: precise-object-edit

Edit target: the provided Chinese cultivation private manor illustration. Produce a polished game environment background matching the reference exactly in composition, architecture, lighting, painterly texture, camera, aspect ratio and all room positions. Preserve the exterior rectangular walls, open main gate at bottom center, central courtyard, upper central hall and tea table, left lower bedroom and open wardrobe, upper left meditation/alchemy room, right vertical passage, right training ground and lower right lotus pond. NO text, no people.

Only these gameplay preparation edits:

1. Remove EVERY wooden training dummy/mannequin from the right exercise ground, including their base disks and shadows, inpaint continuous flagstones underneath. Leave decorative weapon racks and drum only against outer edges, keep the entire central training floor empty for runtime enemies. Make that exercise floor slightly cooler grey slate, preserve a subtle faded teal circular martial seal.
2. Move the central courtyard bonsai planter away from the exact courtyard center to the left edge beside the veranda. The middle courtyard must be an empty clear stone-paved spawn area, continuous floor without a platform, obstacle or raised ornament.
3. Clearly open TWO wide walkable doorways aligned horizontally halfway down the image: one gap in the wall separating central courtyard and right vertical corridor around image x=59%, y=45%; another gap in the wall separating right corridor and training ground around x=68%, y=45%. Each gap is about 9% of image height in length. These must be clear floor-to-floor passages with no door, steps, wall, plant, weapons or furniture obstructing them. This gives a clear horizontal route from central courtyard through the corridor into the training field. Keep the decorative moon gate near the top of the vertical corridor as a landmark.
4. Ensure each left room is accessible from the courtyard through a visible open doorway in its right wall; preserve existing visible doorways if already present.

All other architecture, bed, wardrobe, furnishings and landscape stay in exactly their reference locations. Do not add new buildings, bridges, characters, UI, labels, extra dummies or cave scenery. Full scene visible, same 3:2 landscape composition.
