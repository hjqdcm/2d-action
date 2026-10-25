# 正面双腿与肩部修正

使用内置 `image_gen` 编辑 v3 图集中的正面人物，生成单张透明正面素材。将可见双腿和双靴分开、露出两侧小腿，并将球状肩头改为平顺的白袍布料；活动手臂仍由现有独立衣袖绘制。

项目使用 `Resources/SlashDemo/WhiteSwordsmanDirections-v4.png`。仅将生成的正面人物按原正面可见高度 419 像素缩放，放入左上格子；原 v3 图集其他七个格子逐像素保留。图集仍为 1774×887，RGBA 透明，导入设置沿用 v3 的可读、无压缩、不缩放。v3 保留以便回退。

内置生成原件：`C:/Users/HP/.codex/generated_images/01a163fb-6258-7c72-8e2a-02f825aa780d/exec-17a11193-5828-4f54-88a5-74e499fc3071.png`。此路径只用于溯源，运行时仅引用项目内图集。

实际生成提示词：

Use case: precise-object-edit. Asset type: transparent single front-facing 2D game character body sprite. Reference image is an eight-direction atlas. Edit ONLY the FRONT-FACING swordsman in the TOP LEFT cell and output ONLY this ONE character on transparent background, portrait framing. Match that exact character identity, face, white high ponytail, blue crossed inner collar, embroidered ivory open robe, silver belt, jade pendant and blue tassel, painterly crisp fantasy game rendering, original front camera slightly looking down. Keep the original body proportions, silhouette width/height and head size. Correct two anatomy problems: (1) show TWO distinct pale trouser legs and TWO distinct white boots side by side in a balanced relaxed standing stance, a small transparent gap between the lower legs and boots, both boots soles approximately same vertical position. Open the central blue skirt panels slightly to reveal each leg down from the knees; no single central leg, no third leg, no crossed feet. (2) remove the big spherical protruding shoulder caps/pads and make both shoulders smooth natural narrow white cloth shoulder-to-torso transitions with subtle cloth shading. IMPORTANT this is a modular body sprite whose moving arms are drawn separately by game code: NO arms below the shoulder, NO forearms, NO hands, NO sword, NO scabbard. Shoulder stubs must be filled white cloth, with no hollow circles, no holes, no dark cuff rings, no circular armor plates. Preserve the beautiful fine hair/cloth linework and original character. No ground, no cast shadow, no text, no grid. True transparent background. Full ponytail and both boot tips inside image with a small transparent margin.
