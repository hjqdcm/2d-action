# 私人洞府庄园 v2：细节重绘与放大显示

- 方式：内置 `image_gen` 图像编辑工具；以 `../Resources/SlashDemo/PrivateManor-v1.png` 为编辑目标。
- 当前游戏素材：`../Resources/SlashDemo/PrivateManor-v2.png`；实际输出为 **1536 × 1024**。提示词请求 3072 × 2048，但工具未输出该尺寸；本版本不宣称原生 3K / 4K 或新增四倍像素。
- 改善：重绘屋瓦、砖缝、木质家具、织物、花叶和池岸等细节；Unity 中使用环境专用的 `ManorSurface.shader`，通过九次双线性采样重建 Catmull-Rom 曲面，并限制局部色彩范围，减轻普通双线性放大的软化与明显振铃。
- 导入：无压缩、不做 NPOT 缩放、关闭 Mipmap，纹理上限 4096。提高上限仅避免未来高清素材被截小，并不增加当前图片分辨率。
- 保留：庄园世界尺寸 48 × 32、1536 × 1024 参考坐标、家具和通道布局、碰撞、前景切片、角色、战斗和当前居中跟随相机。底图和前景切片共用一套纹理与采样材质，避免显示接缝；环境销毁时释放专用材质。
- v1 和原参考图保留。编辑器验证会在同一相机位置保存当前图及 `Manor-before-detail.png`，用于比较放大后的实际游戏效果。
- 验证：运行时与编辑器 C# 编译通过；独立工程副本中 Shader 实际导入无错误，四条物理通路、木桩与十轮刷怪位置、初始出生、重来与进入训练场后启动波次均通过。相同镜头的新旧版本截图已导出到工程根目录 `Verification/Artifacts` 并完成目视检查。

## 最终编辑提示词（原文）

Use case: precise-object-edit
Asset type: high-resolution production Unity 2.5D environment texture, detail restoration of an existing registered map.
Input image 1: EDIT TARGET, the exact existing game manor map. It has already registered collision boxes and foreground cutouts, so its geometry is locked.
Primary request: create a genuinely more finely resolved, beautifully detailed HD version of this SAME Chinese xianxia manor illustration. Output a large 3072 x 2048 pixel image, exact 3:2 aspect ratio, with newly resolved fine detail rather than a blurry enlarged painting. Maintain the exact framing, original perspective, architectural outlines and object positions at identical normalized image coordinates. This must fit as a direct higher-resolution replacement of the supplied map. NO recomposition, no new objects or architecture.
Improve only surface fidelity: crisp individual grey roof tiles and their narrow seams; elegant restrained wood carvings and visible natural wood grain on the gate, wardrobe, tea table and screens; individually resolved stone paving joints with subtle stone grain; fine latticework and brass lantern fittings; delicate bedding fabric weave, robe folds, scrolls and teapot details; defined bamboo leaves, flowers and lotus petals; clean pond-edge rock facets, soft transparent water and precise plant silhouettes. Rich but controlled texture, clear structural edges, refined painterly game environment art, natural local contrast and restrained physically coherent shadows. Keep original warm daylight, palette, atmospheric softness in lighting but sharp surface detail. No blur, no depth-of-field, no smeared foliage, no oversharpening halos, no grainy noise.
ABSOLUTE INVARIANTS: preserve the open main gate at bottom center, all wall locations and thicknesses, central clear courtyard spawn floor with NO bonsai or obstacle in its middle, upper central hall and its table, lower left bedroom with bed and open wardrobe, upper left meditation/alchemy room, right vertical corridor and its moon gate, two open sideways passages around 46% of image height connecting courtyard -> corridor -> training field, right empty training field with subtle martial seal and decorative racks only along the outer edges, lower right lotus pond. Every door, tabletop, bed, wardrobe, screen, furnace, pond and foreground gate silhouette must retain the exact original footprint and pixel alignment after scaling. Keep every traversable opening equally wide and unobstructed. No people, no training dummies, no text, no UI, no labels, no watermark. This is a texture detail enhancement, not a layout redesign.
