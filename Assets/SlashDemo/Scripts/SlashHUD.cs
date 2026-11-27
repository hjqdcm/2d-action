using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    public readonly struct CombatBuffView
    {
        public readonly string id, name, iconResource, description;
        public readonly int stacks;
        public readonly bool showZeroStacks, cropIconToSquare;
        public readonly float timeLeft, duration;
        public float RemainingFraction => duration > 0 ? Mathf.Clamp01(timeLeft / duration) : 1;
        public CombatBuffView(string id, string name, int stacks, string iconResource = null, string description = null,
            float timeLeft = 0, float duration = 0, bool showZeroStacks = false, bool cropIconToSquare = false)
        {
            this.id = id; this.name = name; this.stacks = stacks; this.iconResource = iconResource; this.description = description;
            this.timeLeft = timeLeft; this.duration = duration;
            this.showZeroStacks = showZeroStacks; this.cropIconToSquare = cropIconToSquare;
        }
    }

    // Buff sources supply data only; one ordered collection and layout own every visible slot.
    public sealed class CombatBuffBar
    {
        readonly List<CombatBuffView> items = new List<CombatBuffView>(8);
        readonly Dictionary<string, int> slots = new Dictionary<string, int>(StringComparer.Ordinal);
        public IReadOnlyList<CombatBuffView> Items => items;
        public void Clear() { items.Clear(); slots.Clear(); }
        public void AddOrUpdate(CombatBuffView buff)
        {
            if (string.IsNullOrEmpty(buff.id)) throw new ArgumentException("A buff needs a stable, non-empty ID.");
            if (buff.stacks <= 0 && !buff.showZeroStacks) { Remove(buff.id); return; }
            if (slots.TryGetValue(buff.id, out int index)) items[index] = buff;
            else { slots.Add(buff.id, items.Count); items.Add(buff); }
        }
        public bool Remove(string id)
        {
            if (!slots.TryGetValue(id, out int index)) return false;
            items.RemoveAt(index); slots.Remove(id);
            for (int i = index; i < items.Count; i++) slots[items[i].id] = i;
            return true;
        }
    }

    public static class BuffBarLayout
    {
        public const float CellSize = 48, Gap = 8;
        public static Rect Slot(int index, Rect area)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            int columns = Mathf.Max(1, Mathf.FloorToInt((area.width + Gap) / (CellSize + Gap)));
            return new Rect(area.x + index % columns * (CellSize + Gap),
                area.y + index / columns * (CellSize + Gap), CellSize, CellSize);
        }
        public static Rect StackBadge(Rect slot) => new Rect(slot.xMax - 20, slot.y + 2, 18, 18);
        public static Rect Tooltip(Rect slot, Vector2 size, Rect viewport)
        {
            float width = Mathf.Min(size.x, viewport.width - 16), height = Mathf.Min(size.y, viewport.height - 16);
            float x = slot.xMax + 10;
            if (x + width > viewport.xMax - 8) x = slot.x - width - 10;
            return new Rect(Mathf.Clamp(x, viewport.x + 8, viewport.xMax - width - 8),
                Mathf.Clamp(slot.y, viewport.y + 8, viewport.yMax - height - 8), width, height);
        }
    }

    // Texture coordinates start at the bottom; elapsed time sweeps from twelve o'clock towards nine.
    public sealed class BuffCountdownMask : IDisposable
    {
        const int Size = 96;
        readonly float[] angles = new float[Size * Size];
        readonly Color32[] pixels = new Color32[Size * Size];
        public Texture2D Texture { get; }
        float previousElapsed = -1;
        public BuffCountdownMask()
        {
            Texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) {
                name = "Buff counterclockwise countdown", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    angles[y * Size + x] = CounterclockwiseTurn(new Vector2(x + 0.5f - Size / 2f, y + 0.5f - Size / 2f));
        }
        public static float CounterclockwiseTurn(Vector2 offset)
            => Mathf.Repeat(Mathf.Atan2(-offset.x, offset.y) / (Mathf.PI * 2), 1);
        public void Update(float remainingFraction)
        {
            float elapsed = 1 - Mathf.Clamp01(remainingFraction);
            if (elapsed == previousElapsed) return;
            previousElapsed = elapsed;
            for (int i = 0; i < pixels.Length; i++)
            {
                float coverage = Mathf.Clamp01((elapsed - angles[i]) * Size * 3);
                pixels[i] = new Color32(2, 9, 18, (byte)(coverage * 175));
            }
            Texture.SetPixels32(pixels);
            Texture.Apply(false, false);
        }
        public void Dispose() { UnityEngine.Object.Destroy(Texture); }
    }

    public sealed class SlashHUD : MonoBehaviour
    {
        SlashArena arena;
        Font font;
        Texture2D paper;
        Texture2D skillIcon;
        Texture2D ultimateIcon;
        Texture2D ironSkillIcon;
        Texture2D swordsmanPortrait;
        GUIStyle codexBody, codexHeading;
        GUIStyle alchemyDescription, alchemyTitle;
        int codexIndex;
        Vector2 techniqueScroll;
        Vector2 attributeScroll;
        Vector2 pillInventoryScroll, treasureInventoryScroll;
        readonly List<float> pillEffectHeights = new List<float>();
        Vector2 codexScroll;
        Vector2 treasureScroll;
        Vector2 pillScroll;
        int enemyCodexIndex;
        Vector2 enemyListScroll, enemySkillScroll;
        readonly Dictionary<IReadOnlyList<AlchemyEntry>, float[]> alchemyRowOffsets = new Dictionary<IReadOnlyList<AlchemyEntry>, float[]>();
        float alchemyCardWidth;
        static readonly string[] CharacterPageNames = { "功法", "属性", "物品" };
        GUIStyle small, health, damage, status, travelTitle, travelButton;
        GUIStyle routeTitle, routeBalanceValue, pricedButtonTitle;
        const string SpiritStoneIcon = "SlashDemo/SpiritStoneIcon";
        const string FortunePointIcon = "SlashDemo/FortunePointIcon";
        const string SpiritStoneDescription = "修仙界通用货币，常用于交易丹药、法宝等修炼物资。";
        const string FortunePointDescription = "游历所需的机缘，可用于前往山野、升仙台等地。";
        static readonly Rect SpiritStoneIconCrop = new Rect(.125f, .125f, .75f, .75f);
        static readonly Rect FortunePointIconCrop = new Rect(.22f, .235f, .58f, .58f);
        static readonly Rect SquareIconCrop = new Rect(.165f, .165f, .67f, .67f);
        readonly GUIContent damageNumberContent = new GUIContent();
        readonly GUIContent resourceAmountContent = new GUIContent();
        GUIStyle buffName, buffStacks, passiveStacks, buffTooltipBody, buffTooltipTitle, bossCastName;
        readonly Dictionary<string, Texture2D> hudIcons = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        readonly Dictionary<string, BuffCountdownMask> buffCountdowns = new Dictionary<string, BuffCountdownMask>(StringComparer.Ordinal);
        CombatBuffView hoveredBuff;
        Rect hoveredBuffSlot;
        public string HoveredBuffId { get; private set; }
        public bool HeavyChargeBarVisible => arena != null && arena.Player != null && arena.Player.IsAlive
            && arena.Player.IsCharging && !arena.Paused && !arena.Defeated;
        public bool DefeatPanelVisible => arena != null && arena.Defeated;
        readonly CombatBuffBar buffBar = new CombatBuffBar();
        public IReadOnlyList<CombatBuffView> VisibleBuffs
        {
            get
            {
                if (arena != null && arena.Player != null) arena.Player.CollectBuffs(buffBar);
                else buffBar.Clear();
                return buffBar.Items;
            }
        }
        static readonly Color Ink = new Color(0.24f, 0.25f, 0.20f);
        static readonly Color Bronze = new Color(0.53f, 0.42f, 0.25f);
        static readonly Color PaperTint = new Color(.88f, .83f, .70f, .80f);
        static readonly Color Muted = new Color(0.43f, 0.40f, 0.31f);
        static readonly Color Jade = new Color(0.22f, 0.43f, 0.34f);
        static readonly Color Cinnabar = new Color(0.61f, 0.23f, 0.18f);
        static readonly Color[] AlchemyNameColors = {
            Color.white, new Color(0.46f, 0.75f, 1f), new Color(0.81f, 0.63f, 1f), new Color(1f, 0.65f, 0.32f)
        };
        static readonly string[] AlchemyQualityNames = { "白色", "蓝色", "紫色", "橙色" };

        void Awake()
        {
            arena = GetComponent<SlashArena>();
            skillIcon = Resources.Load<Texture2D>("SlashDemo/FlowingCloudActiveIcon");
            ironSkillIcon = Resources.Load<Texture2D>("SlashDemo/IronSwordActiveIcon");
            ultimateIcon = Resources.Load<Texture2D>(FiveStarSwordSlash.IconResource);
            swordsmanPortrait = Resources.Load<Texture2D>("SlashDemo/Cultivators/WhiteSwordsmanFullBody-v1");
            paper = CreatePaperTexture();
        }

        void InitStyles()
        {
            if (small != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Arial" }, 20);
            small = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, alignment = TextAnchor.MiddleLeft };
            small.normal.textColor = Color.white;
            health = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            damage = new GUIStyle(health) { fontSize = 23, fontStyle = FontStyle.Bold };
            buffName = new GUIStyle(small) { fontSize = 12, alignment = TextAnchor.MiddleCenter, padding = new RectOffset() };
            buffStacks = new GUIStyle(damage) { fontSize = 16, alignment = TextAnchor.MiddleCenter, padding = new RectOffset() };
            passiveStacks = new GUIStyle(buffStacks) { fontSize = 10 };
            buffTooltipBody = new GUIStyle(small) { fontSize = 15, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset() };
            buffTooltipTitle = new GUIStyle(buffTooltipBody) { fontSize = 18, fontStyle = FontStyle.Bold };
            status = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            bossCastName = new GUIStyle(small) { alignment = TextAnchor.MiddleRight };
            travelTitle = new GUIStyle(status) { fontSize = 27, fontStyle = FontStyle.Bold };
            routeTitle = new GUIStyle(travelTitle) { alignment = TextAnchor.MiddleLeft };
            routeBalanceValue = new GUIStyle(travelTitle) { fontSize = 34 };
            pricedButtonTitle = new GUIStyle(status) { fontSize = 18, fontStyle = FontStyle.Bold };
            travelButton = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20 };
            travelButton.normal.textColor = Ink;
            codexBody = new GUIStyle(small) { fontSize = 17, wordWrap = true };
            codexHeading = new GUIStyle(codexBody) { fontSize = 22, fontStyle = FontStyle.Bold };
            alchemyDescription = new GUIStyle(codexBody) {
                fontSize = 16, alignment = TextAnchor.UpperLeft, padding = new RectOffset()
            };
            alchemyTitle = new GUIStyle(codexHeading) {
                fontSize = 19, alignment = TextAnchor.MiddleCenter, padding = new RectOffset()
            };
        }

        static Texture2D CreatePaperTexture()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) {
                name = "Manor HUD rice paper", wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    // Deterministic grain leaves gameplay's random sequence untouched.
                    float grain = (((x * 73 + y * 151 + x * y * 19) % 101) / 100f - 0.5f) * 0.025f;
                    pixels[y * 64 + x] = new Color(PaperTint.r + grain, PaperTint.g + grain, PaperTint.b + grain, PaperTint.a);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static void Box(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
        static void Label(Rect rect, string value, GUIStyle style, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, value, style);
            GUI.color = previous;
        }

        static void Outline(Rect rect, Color color, float thickness = 1)
        {
            Box(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Box(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Box(new Rect(rect.x, rect.y + thickness, thickness, rect.height - thickness * 2), color);
            Box(new Rect(rect.xMax - thickness, rect.y + thickness, thickness, rect.height - thickness * 2), color);
        }

        void DrawFrame(Rect rect, float paperOpacity = 1)
        {
            // Only outline the frame; opaque backing layers would hide the scene through the paper.
            Outline(new Rect(rect.x + 1, rect.y + 3, rect.width, rect.height), new Color(0.08f, 0.10f, 0.07f, 0.15f), 2);
            var ink = Ink;
            ink.a = 0.8f;
            var bronze = Bronze;
            bronze.a = 0.75f;
            Outline(rect, ink);
            Outline(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), bronze, 2);
            var inside = new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6);
            if (paper != null)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1, 1, 1, paperOpacity);
                GUI.DrawTextureWithTexCoords(inside, paper, new Rect(0, 0, inside.width / 64, inside.height / 64));
                GUI.color = previous;
            }
            else Box(inside, new Color(PaperTint.r, PaperTint.g, PaperTint.b, PaperTint.a * paperOpacity));
            Box(new Rect(rect.x + 3, rect.y + 3, rect.width - 6, 1), new Color(1, 0.96f, 0.79f, 0.4f));
            for (int i = 0; i < 4; i++)
            {
                bool right = (i & 1) != 0, bottom = (i & 2) != 0;
                float x = right ? rect.xMax - 6 : rect.x + 6;
                float y = bottom ? rect.yMax - 6 : rect.y + 6;
                Box(new Rect(right ? x - 11 : x, y, 12, 1), bronze);
                Box(new Rect(x, bottom ? y - 8 : y, 1, 9), bronze);
                Box(new Rect(right ? x - 5 : x + 3, bottom ? y - 4 : y + 3, 3, 2), bronze);
            }
        }

        static void DrawGauge(Rect rect, float value, Color color)
        {
            Box(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), Bronze);
            Box(rect, new Color(0.26f, 0.27f, 0.21f));
            float fill = rect.width * Mathf.Clamp01(value);
            if (fill <= 0) return;
            Box(new Rect(rect.x, rect.y, fill, rect.height), color);
            Box(new Rect(rect.x, rect.y, fill, 1), Color.Lerp(color, new Color(0.96f, 0.88f, 0.66f), 0.45f));
        }

        void OnGUI()
        {
            if (arena == null || arena.Player == null) return;
            InitStyles();
            if (paper == null && Event.current.type == EventType.Repaint) paper = CreatePaperTexture();
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            if (scale <= 0) return;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            var player = arena.Player;

            var healthPanel = new Rect(30, 25, 330, 106);
            DrawFrame(healthPanel);
            Label(new Rect(50, 36, 282, 23), "境界 · " + player.RealmDisplayName, small, Jade);
            Label(new Rect(50, 64, 90, 23), "气血", small, Ink);
            Label(new Rect(220, 64, 112, 25), player.Health + " / " + player.EffectiveMaxHealth, small, Cinnabar);
            DrawGauge(new Rect(50, 102, 282, 8), player.Health / (float)player.EffectiveMaxHealth, Cinnabar);
            DrawBuffBar(new Rect(50, healthPanel.yMax + 10, 282, 0), scale);

            DrawFrame(new Rect(width / 2 - 105, height - 58, 210, 31));
            DrawGauge(new Rect(width / 2 - 105, height - 28, 210, 3), player.DashReady, Jade);
            Label(new Rect(width / 2 - 105, height - 58, 210, 30), player.DashReady >= 1 ? "冲刺就绪" : "冲刺冷却中", status, player.DashReady >= 1 ? Jade : Muted);
            DrawActiveSkill(height);
            DrawUltimateSkill(height);
            const float potionTop = 20, potionSize = 62, potionGap = 12, potionPadding = 10;
            float potionWidth = GameResources.PotionSlotCount * (potionSize + potionGap) - potionGap;
            float potionStart = (width - potionWidth) * .5f;
            var potionFrame = new Rect(potionStart - potionPadding, potionTop - potionPadding,
                potionWidth + potionPadding * 2, potionSize + potionPadding * 2);
            DrawFrame(potionFrame, paperOpacity: .5f);
            for (int i = 0; i < GameResources.PotionSlotCount; i++)
                DrawPotionSlot(new Rect(potionStart + i * (potionSize + potionGap), potionTop, potionSize, potionSize),
                    i, selected: false, translucent: true);
            float fightStatusTop = potionFrame.yMax + 12;
            AscensionCultivator challenger = null;
            if (arena.Travel != null && arena.Travel.ChallengeRound > 0 && arena.Enemies.Count > 0)
            {
                challenger = arena.Enemies[0].GetComponent<AscensionCultivator>();
                string skill = challenger != null ? challenger.CastingSkill : null;
                var round = new Rect(width / 2 - 190, fightStatusTop, 380, skill == null ? 44 : 69);
                DrawFrame(round);
                Label(new Rect(round.x, round.y, round.width, 44),
                    "升仙台 · 第 " + arena.Travel.ChallengeRound + " / " + AscensionCultivator.PathCount
                    + " 战 · " + arena.Enemies[0].name, status, Ink);
                if (skill != null)
                    Label(new Rect(round.x, round.y + 39, round.width, 24),
                        challenger.CastingUltimate ? "大招 · " + skill : skill, status, Cinnabar);
            }
            else if (arena.Travel != null && arena.Travel.ChallengeEndedByDefeat)
            {
                var result = new Rect(width / 2 - 220, fightStatusTop, 440, 72);
                DrawFrame(result);
                Label(new Rect(result.x, result.y + 5, result.width, 28),
                    "升仙台 · 本轮完成 " + arena.Travel.ChallengeCompletedRounds + " 战", status, Ink);
                Label(new Rect(result.x, result.y + 35, result.width, 28), "已恢复一半气血 · 可使用传送阵离开", status, Jade);
            }
            if (arena.Travel != null && (arena.Travel.IsTribulationActive || arena.Travel.TribulationCleared))
            {
                var round = new Rect(width / 2 - 160, fightStatusTop, 320, 44);
                DrawFrame(round);
                Label(round, arena.Travel.TribulationCleared ? "渡劫完成"
                    : $"渡劫 · 第 {arena.Travel.TribulationRound} / {TribulationChallenge.RoundCount} 轮", status, Ink);
            }
            if (arena.Travel?.Current == TravelDestination.FinalBattle && arena.Enemies.Count > 0)
            {
                var opponent = arena.Enemies[0];
                var boss = opponent.GetComponent<EvilCultivatorBoss>();
                if (boss != null && opponent.Alive)
                    DrawBossStatus(width, fightStatusTop, scale, opponent, boss);
            }
            if (arena.Travel != null && arena.Travel.ShowPortalPrompt)
            {
                var prompt = new Rect(width / 2 - 175, height / 2 + 80, 350, 50);
                DrawFrame(prompt);
                Label(prompt, "F 开启传送阵", status, Ink);
            }
            if (arena.Travel != null && arena.Travel.ShowTreatmentPrompt)
            {
                var prompt = new Rect(width / 2 - 175, height / 2 + 80, 350, 50);
                DrawFrame(prompt);
                Label(prompt, "F  床边疗伤", status, Ink);
            }
            if (arena.Travel != null && arena.Travel.ShowTribulationPrompt)
            {
                var prompt = new Rect(width / 2 - 175, height / 2 + 80, 350, 50);
                DrawFrame(prompt);
                Label(prompt, "F 开始渡劫", status, Ink);
            }
            if (arena.Travel != null && arena.Travel.ShowShopPrompt)
            {
                var prompt = new Rect(width / 2 - 175, height / 2 + 80, 350, 50);
                DrawFrame(prompt);
                Label(prompt, "F  购买", status, Ink);
            }
            if (arena.Codex != null && arena.Codex.ShowPrompt)
            {
                var prompt = new Rect(width / 2 - 175, height / 2 - 25, 350, 50);
                DrawFrame(prompt);
                Label(prompt, "F查看功法图鉴", status, Ink);
            }
            if (arena.Codex != null && arena.Codex.ShowAlchemyPrompt)
            {
                var prompt = new Rect(width / 2 - 175, height / 2 - 25, 350, 50);
                DrawFrame(prompt);
                Label(prompt, "F  查看炼丹炉图鉴", status, Ink);
            }
            if (arena.CharacterPanel != null && arena.CharacterPanel.ShowWardrobePrompt)
            {
                var prompt = new Rect(width / 2 - 175, height / 2 - 25, 350, 50);
                DrawFrame(prompt);
                Label(prompt, "F  选择角色", status, Ink);
            }

            if (!arena.Paused && !arena.Defeated)
            {
                DrawHeavyChargeBar(scale);
                DrawWorldHealth(scale);
                if (challenger != null) DrawCultivatorPassive(challenger, scale);
                DrawDamageNumbers(scale);
                DrawPointer(scale);
            }
            DrawBuffTooltip(new Rect(0, 0, width, height));
            if (arena.Codex == null || !arena.Codex.IsAlchemyOpen)
            {
                treasureScroll = pillScroll = enemyListScroll = enemySkillScroll = Vector2.zero;
                enemyCodexIndex = 0;
            }
            if (DefeatPanelVisible) DrawDefeatPanel(width, height);
            else if (arena.Codex != null && arena.Codex.IsAlchemyOpen) DrawAlchemyCodex(width, height);
            else if (arena.Codex != null && arena.Codex.IsOpen) DrawTechniqueCodex(width, height);
            else if (arena.CharacterPanel != null && arena.CharacterPanel.IsWardrobeOpen) DrawWardrobe(width, height);
            else if (arena.CharacterPanel != null && arena.CharacterPanel.IsOpen) DrawCharacterPanel(width, height);
            else if (arena.Travel != null && arena.Travel.IsLootChoiceOpen) DrawLootChoice(width, height);
            else if (arena.Travel != null && arena.Travel.IsChallengeDecisionOpen) DrawChallengeDecision(width, height);
            else if (arena.Travel != null && arena.Travel.IsTribulationDecisionOpen) DrawTribulationDecision(width, height);
            else if (arena.Travel != null && arena.Travel.IsTreatmentOpen) DrawTreatmentDecision(width, height);
            else if (arena.Travel != null && arena.Travel.IsShopOpen) DrawShop(width, height);
            else if (arena.PauseMenuOpen) DrawPauseMenu(width, height);
            else if (arena.Travel != null && arena.Travel.IsSelectionOpen) DrawTravelSelection(width, height);
            GUI.matrix = previous;
        }

        bool TryGetHeavyChargeBarRect(float scale, out Rect rect)
        {
            rect = default;
            if (!HeavyChargeBarVisible || scale <= 0 || arena.WorldCamera == null) return false;
            Vector3 head = arena.View.LiftPoint(arena.Player.transform.position,
                (HeroAppearance.BodyHeight + 0.30f) * arena.View.characterScale);
            Vector3 screen = arena.WorldCamera.WorldToScreenPoint(head);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) return false;
            rect = new Rect(screen.x / scale - 42, (Screen.height - screen.y) / scale - 4, 84, 8);
            return true;
        }

        void DrawHeavyChargeBar(float scale)
        {
            if (!TryGetHeavyChargeBarRect(scale, out Rect rect)) return;
            var player = arena.Player;
            float progress = Mathf.Clamp01(player.ChargeProgress);
            bool ready = progress >= 1;
            Color edge = ready ? new Color(1, 0.81f, 0.38f, 0.46f) : new Color(0.76f, 0.80f, 0.65f, 0.26f);
            Box(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), new Color(0.01f, 0.03f, 0.04f, 0.10f));
            Box(rect, new Color(0.025f, 0.06f, 0.07f, 0.28f));
            Outline(rect, edge);
            float fill = (rect.width - 4) * progress;
            if (fill <= 0) return;
            Color color = ready ? new Color(1, 0.77f, 0.27f, 0.46f + 0.05f * Mathf.Sin(Time.time * 7))
                : new Color(0.38f, 0.84f, 0.79f, 0.40f);
            Box(new Rect(rect.x + 2, rect.y + 2, fill, rect.height - 4), color);
            Box(new Rect(rect.x + 2, rect.y + 2, fill, 1), new Color(1, 0.97f, 0.78f, 0.20f));
        }

        void DrawBuffBar(Rect area, float scale)
        {
            HoveredBuffId = null;
            var buffs = VisibleBuffs;
            for (int i = 0; i < buffs.Count; i++) DrawBuffSlot(BuffBarLayout.Slot(i, area), buffs[i], scale);
        }

        void DrawBossStatus(float width, float top, float scale, SlashEnemy opponent, EvilCultivatorBoss boss)
        {
            var panel = new Rect(width / 2 - 220, top, 440, 51);
            DrawFrame(panel);
            var gauge = new Rect(panel.x + 24, panel.y + 34, panel.width - 48, 8);
            Label(new Rect(gauge.x, panel.y + 3, 250, 26),
                opponent.name + " · " + opponent.Health + " / " + opponent.maxHealth, small, Ink);
            if (boss.CastingSkill is string cast)
                Label(new Rect(gauge.x + 250, panel.y + 3, gauge.width - 250, 26), cast, bossCastName, Cinnabar);
            DrawGauge(gauge, opponent.Health / (float)opponent.maxHealth, Cinnabar);

            var area = new Rect(gauge.x, panel.yMax + 10, gauge.width, 0);
            int index = 0;
            if (boss.PassiveBuff is CombatBuffView passive)
                DrawBuffSlot(BuffBarLayout.Slot(index++, area), passive, scale);
            if (boss.FrenzyBuff is CombatBuffView frenzy)
                DrawBuffSlot(BuffBarLayout.Slot(index++, area), frenzy, scale);
            if (boss.TransitionBuff is CombatBuffView transition)
                DrawBuffSlot(BuffBarLayout.Slot(index, area), transition, scale);
        }

        void DrawBuffSlot(Rect slot, CombatBuffView buff, float scale)
        {
            DrawFrame(slot);
            if (!DrawHudIcon(slot, buff.iconResource, buff.cropIconToSquare ? SquareIconCrop : (Rect?)null))
                Label(new Rect(slot.x + 4, slot.y + 20, slot.width - 8, 20), buff.name, buffName, Jade);
            DrawBuffCountdown(slot, buff);
            if (buff.stacks > 0 || buff.showZeroStacks)
            {
                Rect badge = BuffBarLayout.StackBadge(slot);
                Box(badge, new Color(0.02f, 0.06f, 0.12f, 0.92f));
                Outline(badge, Bronze);
                Label(badge, buff.stacks.ToString(), buffStacks, Color.white);
            }
            HoverBuffSlot(slot, buff, scale);
        }

        bool DrawHudIcon(Rect slot, string resource, Rect? crop = null)
        {
            if (string.IsNullOrEmpty(resource)) return false;
            if (!hudIcons.TryGetValue(resource, out Texture2D icon))
            {
                icon = Resources.Load<Texture2D>(resource);
                hudIcons.Add(resource, icon);
            }
            if (icon == null) return false;
            var iconRect = new Rect(slot.x + 2, slot.y + 2, slot.width - 4, slot.height - 4);
            if (crop.HasValue)
            {
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTextureWithTexCoords(iconRect, icon, crop.Value);
            }
            else GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            return true;
        }

        void HoverBuffSlot(Rect slot, CombatBuffView buff, float scale)
        {
            Vector2 pointer = new Vector2(DemoInput.Pointer.x / scale, (Screen.height - DemoInput.Pointer.y) / scale);
            bool canHover = DemoInput.MouseAvailable && !arena.PauseMenuOpen
                && (arena.CharacterPanel == null || !arena.CharacterPanel.IsOpen)
                && (arena.Codex == null || !arena.Codex.IsOpen) && (arena.Travel == null || !arena.Travel.HasModal);
            if (canHover && slot.Contains(pointer) && !string.IsNullOrEmpty(buff.description))
            { HoveredBuffId = buff.id; hoveredBuff = buff; hoveredBuffSlot = slot; }
        }

        void DrawBuffCountdown(Rect slot, CombatBuffView buff)
        {
            if (buff.duration <= 0 || Event.current.type != EventType.Repaint) return;
            if (!buffCountdowns.TryGetValue(buff.id, out var countdown))
            { countdown = new BuffCountdownMask(); buffCountdowns.Add(buff.id, countdown); }
            countdown.Update(buff.RemainingFraction);
            Rect icon = new Rect(slot.x + 2, slot.y + 2, slot.width - 4, slot.height - 4);
            GUI.DrawTexture(icon, countdown.Texture);
        }

        void DrawBuffTooltip(Rect viewport)
        {
            if (HoveredBuffId == null) return;
            const float width = 340;
            float bodyHeight = buffTooltipBody.CalcHeight(new GUIContent(hoveredBuff.description), width - 24);
            Rect rect = BuffBarLayout.Tooltip(hoveredBuffSlot, new Vector2(width, bodyHeight + 56), viewport);
            int previousDepth = GUI.depth; GUI.depth = -15;
            DrawTooltip(rect, hoveredBuff.name, hoveredBuff.description, bodyHeight);
            GUI.depth = previousDepth;
        }

        void DrawTooltip(Rect rect, string title, string description, float bodyHeight)
        {
            Box(rect, new Color(0.025f, 0.06f, 0.08f, 0.98f));
            Outline(rect, Bronze);
            Label(new Rect(rect.x + 12, rect.y + 9, rect.width - 24, 27), title, buffTooltipTitle, new Color(0.64f, 0.91f, 0.86f));
            Label(new Rect(rect.x + 12, rect.y + 40, rect.width - 24, bodyHeight), description, buffTooltipBody,
                new Color(0.92f, 0.92f, 0.84f));
        }

        void DrawActiveSkill(float height)
        {
            var player = arena.Player;
            DrawSkillSlot(new Rect(60, height - 174, 108, 108), player.HasIronSword ? ironSkillIcon : skillIcon,
                player.TechniqueReady, player.TechniqueCooldownLeft, "Q · " + player.Technique.activeName);
        }

        void DrawSkillSlot(Rect icon, Texture2D texture, bool ready, float cooldown, string captionText)
        {
            if (texture != null)
            {
                Color previous = GUI.color;
                GUI.color = ready ? Color.white : new Color(0.48f, 0.52f, 0.51f);
                GUI.DrawTexture(icon, texture);
                GUI.color = previous;
            }
            if (cooldown > 0)
                Label(icon, cooldown.ToString("0.0"), damage, Color.white);
            var caption = new Rect(icon.x - 30, icon.yMax + 7, 168, 31);
            DrawFrame(caption);
            Label(caption, captionText, status, Ink);
        }

        void DrawUltimateSkill(float height)
        {
            var player = arena.Player;
            var ultimate = player.Ultimate;
            if (ultimate == null) return;
            DrawSkillSlot(new Rect(238, height - 174, 108, 108), ultimateIcon, ultimate.Ready || ultimate.Active,
                player.InfiniteUltimateForDebug ? 0 : ultimate.CooldownLeft, "R · " + FiveStarSwordSlash.Name);
        }

        void DrawPotionSlot(Rect rect, int slot, bool selected, bool translucent = false)
        {
            var entry = arena.Resources.PotionSlots[slot];
            Box(rect, translucent ? new Color(PaperTint.r, PaperTint.g, PaperTint.b, .24f) : new Color(.14f, .20f, .21f, .92f));
            if (entry.HasValue && Event.current.type == EventType.Repaint)
                DrawHudIcon(new Rect(rect.x + 7, rect.y + 7, rect.width - 14, rect.height - 14), entry.Value.IconResource);
            Color border = selected ? new Color(.95f, .77f, .39f)
                : !translucent && entry.HasValue ? AlchemyNameColors[(int)entry.Value.Quality] : Bronze;
            if (translucent) border.a = .65f;
            Outline(rect, border);
            var hotkey = new Rect(rect.x + 3, rect.y + 3, 19, 19);
            Box(hotkey, new Color(.10f, .14f, .13f, translucent ? .76f : .94f));
            Label(hotkey, (slot + 1).ToString(), buffStacks, Color.white);
        }

        bool DrawCharacterButton(Rect rect, string text, bool selected)
        {
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            bool hovered = rect.Contains(Event.current.mousePosition);
            Box(rect, selected ? Jade : hovered ? new Color(0.84f, 0.80f, 0.67f) : new Color(0.89f, 0.85f, 0.73f));
            Outline(rect, selected ? Bronze : Muted);
            Label(rect, text, travelTitle, selected ? new Color(0.98f, 0.96f, 0.86f) : Ink);
            return clicked;
        }

        Rect DrawPaperModal(float width, float height)
        {
            Box(new Rect(0, 0, width, height), new Color(0.06f, 0.11f, 0.09f, 0.72f));
            var panel = new Rect(width / 2 - 540, height / 2 - 360, 1080, 720);
            Box(panel, new Color(0.90f, 0.86f, 0.74f, 1));
            DrawFrame(panel);
            return panel;
        }

        void DrawCharacterPanel(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            var panel = DrawPaperModal(width, height);
            for (int i = 0; i < CharacterPageNames.Length; i++)
                if (DrawCharacterButton(new Rect(panel.x + 28 + i * 192, panel.y + 22, 180, 46),
                    CharacterPageNames[i], arena.CharacterPanel.ActivePage == (CharacterPanelPage)i))
                {
                    arena.CharacterPanel.SelectPage((CharacterPanelPage)i);
                    techniqueScroll = Vector2.zero;
                    attributeScroll = Vector2.zero;
                }
            if (GUI.Button(new Rect(panel.xMax - 212, panel.y + 24, 184, 42), "关闭 Tab / Esc", travelButton))
            { arena.CharacterPanel.Close(); GUI.depth = previousDepth; return; }
            Box(new Rect(panel.x + 28, panel.y + 83, panel.width - 56, 1), Bronze);
            if (arena.CharacterPanel.ActivePage == CharacterPanelPage.Techniques) DrawCharacterTechniques(panel);
            else if (arena.CharacterPanel.ActivePage == CharacterPanelPage.Attributes)
                DrawCharacterAttributes(panel);
            else if (arena.CharacterPanel.ActivePage == CharacterPanelPage.Items)
                DrawCharacterItems(panel);
            GUI.depth = previousDepth;
        }

        void DrawCharacterItems(Rect panel)
        {
            const float margin = 28, gap = 16;
            float width = panel.width - margin * 2;
            float balanceWidth = (width - gap) / 2;
            var stones = new Rect(panel.x + margin, panel.y + 106, balanceWidth, 68);
            var fortune = new Rect(stones.xMax + gap, stones.y, stones.width, stones.height);
            DrawResourceBalance(stones, SpiritStoneIcon, arena.Resources.SpiritStones);
            DrawResourceBalance(fortune, FortunePointIcon, arena.Resources.FortunePoints);

            var character = arena.CharacterPanel;
            var pillsTab = new Rect(stones.x, stones.yMax + 14, 132, 40);
            var treasureTab = new Rect(pillsTab.xMax + 12, pillsTab.y, pillsTab.width, pillsTab.height);
            if (DrawCharacterButton(pillsTab, "丹药", character.ActiveInventoryPage == CharacterInventoryPage.Pills))
                character.SelectInventoryPage(CharacterInventoryPage.Pills);
            if (DrawCharacterButton(treasureTab, "法宝", character.ActiveInventoryPage == CharacterInventoryPage.Treasures))
                character.SelectInventoryPage(CharacterInventoryPage.Treasures);

            float inventoryY = pillsTab.yMax + 14;
            if (character.ActiveInventoryPage == CharacterInventoryPage.Pills)
            {
                DrawPotionConfiguration(new Rect(stones.x, inventoryY, balanceWidth, 114));
                inventoryY += 120;
                DrawInventoryList(new Rect(stones.x, inventoryY, width, panel.yMax - inventoryY - margin),
                    arena.Resources.Pills, ref pillInventoryScroll, isPills: true);
            }
            else
            {
                DrawInventoryList(new Rect(stones.x, inventoryY, width, panel.yMax - inventoryY - margin),
                    arena.Resources.Treasures, ref treasureInventoryScroll);
            }

            Vector2 pointer = Event.current.mousePosition;
            if (stones.Contains(pointer))
                DrawInventoryResourceTooltip(stones, "灵石", SpiritStoneDescription);
            else if (fortune.Contains(pointer))
                DrawInventoryResourceTooltip(fortune, "机缘点", FortunePointDescription);
        }

        void DrawInventoryResourceTooltip(Rect card, string title, string description)
        {
            const float width = 360;
            float bodyHeight = buffTooltipBody.CalcHeight(new GUIContent(description), width - 24);
            DrawTooltip(new Rect(card.x, card.yMax + 8, width, bodyHeight + 56), title, description, bodyHeight);
        }

        void DrawPotionConfiguration(Rect area)
        {
            Label(new Rect(area.x, area.y, area.width, 26), "药水槽", codexHeading, Ink);
            bool enabled = GUI.enabled;
            bool canConfigure = arena.CanConfigurePotions;
            GUI.enabled = enabled && canConfigure;
            for (int i = 0; i < GameResources.PotionSlotCount; i++)
            {
                var slot = new Rect(area.x + i * 72, area.y + 31, 58, 58);
                if (GUI.Button(slot, GUIContent.none, GUIStyle.none)) arena.PotionConfigSlot = i;
                DrawPotionSlot(slot, i, arena.PotionConfigSlot == i);
            }
            int selected = arena.PotionConfigSlot;
            GUI.enabled = enabled && canConfigure && arena.Resources.PotionSlots[selected].HasValue;
            if (GUI.Button(new Rect(area.xMax - 124, area.y + 45, 124, 32), "清空所选", travelButton))
                arena.Resources.TryAssignPotion(selected, null);
            GUI.enabled = enabled;
            Label(new Rect(area.x, area.y + 91, area.width, 23),
                canConfigure ? "选择槽位后，从下方丹药装入。" : "战斗间隙可配置药水槽。", small, Muted);
        }

        void DrawInventoryList(Rect viewport, IReadOnlyDictionary<AlchemyEntry, int> items, ref Vector2 scroll,
            bool isPills = false)
        {
            const float rowHeight = 108, gap = 9;
            bool canConfigure = isPills && arena.CanConfigurePotions;
            if (items.Count == 0)
            {
                Label(new Rect(viewport.x, viewport.y, viewport.width, 28),
                    isPills ? "尚未获得丹药" : "尚未获得法宝", codexBody, Muted);
                scroll = Vector2.zero;
                return;
            }

            float width = viewport.width - 18;
            float height = items.Count * (rowHeight + gap) - gap;
            scroll.y = Mathf.Clamp(scroll.y, 0, Mathf.Max(0, height - viewport.height));
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, width, height));
            float y = 0;
            IEnumerable<AlchemyEntry> entries = isPills ? arena.Resources.PillOrder : items.Keys;
            foreach (var entry in entries)
            {
                if (y >= scroll.y + viewport.height) break;
                if (y + rowHeight >= scroll.y)
                {
                    Box(new Rect(0, y, width, rowHeight), new Color(.14f, .20f, .21f));
                    var icon = new Rect(10, y + 10, 62, 62);
                    if (Event.current.type == EventType.Repaint) DrawHudIcon(icon, entry.IconResource);
                    Label(new Rect(82, y + 8, width - 142, 27), entry.Name, codexBody, AlchemyNameColors[(int)entry.Quality]);
                    Label(new Rect(width - 46, y + 8, 40, 27), $"×{items[entry]}", status, Color.white);
                    float descriptionWidth = width - (canConfigure ? 218 : 94);
                    Label(new Rect(82, y + 38, descriptionWidth, rowHeight - 43), entry.Effect, alchemyDescription, Color.white);
                    if (canConfigure)
                    {
                        bool enabled = GUI.enabled;
                        GUI.enabled = enabled && arena.Resources.CanAssignPotion(arena.PotionConfigSlot, entry);
                        if (GUI.Button(new Rect(width - 124, y + 54, 112, 34), "装入", travelButton))
                            arena.Resources.TryAssignPotion(arena.PotionConfigSlot, entry);
                        GUI.enabled = enabled;
                    }
                }
                y += rowHeight + gap;
            }
            GUI.EndScrollView();
        }

        void DrawResourceBalance(Rect card, string icon, int balance)
        {
            DrawFrame(card);
            DrawResourceAmount(card, icon, balance.ToString(), travelTitle, Jade, 46);
        }

        void DrawResourceAmount(Rect area, string icon, string amount, GUIStyle style, Color color, float iconSize)
        {
            resourceAmountContent.text = amount;
            float textWidth = Mathf.Ceil(style.CalcSize(resourceAmountContent).x) + 2;
            const float gap = 6;
            float left = area.center.x - (iconSize + gap + textWidth) * .5f;
            var iconRect = new Rect(left, area.center.y - iconSize * .5f, iconSize, iconSize);
            Color previous = GUI.color;
            if (!GUI.enabled) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * .45f);
            DrawHudIcon(iconRect, icon, icon == FortunePointIcon ? FortunePointIconCrop : SpiritStoneIconCrop);
            GUI.color = previous;
            Label(new Rect(iconRect.xMax + gap, area.y, textWidth, area.height), amount, style, color);
        }

        bool DrawPricedButton(Rect button, string title, string cost, string icon = null)
        {
            bool pressed = GUI.Button(button, GUIContent.none, travelButton);
            Label(new Rect(button.x, button.y + 4, button.width, 24), title, pricedButtonTitle, Ink);
            var price = new Rect(button.x, button.y + 27, button.width, 22);
            if (icon == null) Label(price, cost, status, Ink);
            else DrawResourceAmount(price, icon, cost, status, Ink, 22);
            return pressed;
        }

        void DrawCharacterAttributes(Rect panel)
        {
            const float rowHeight = 38, headingHeight = 38, gap = 16;
            const int rowsPerColumn = 11;
            var viewport = new Rect(panel.x + 28, panel.y + 99, panel.width - 56, panel.height - 127);
            float width = viewport.width - 20;
            float columnWidth = (width - gap) / 2;
            float effectsY = headingHeight + rowsPerColumn * rowHeight + 18;
            var player = arena.Player;
            var effects = player.ActivePills;
            float contentHeight = headingHeight + rowsPerColumn * rowHeight;
            pillEffectHeights.Clear();
            if (effects.Count > 0)
            {
                contentHeight = effectsY + headingHeight;
                foreach (var entry in effects.Keys)
                {
                    float height = 40 + codexBody.CalcHeight(new GUIContent(entry.Effect), width - 24);
                    pillEffectHeights.Add(height);
                    contentHeight += height;
                }
            }

            var content = new Rect(0, 0, width, contentHeight);
            attributeScroll.y = Mathf.Clamp(attributeScroll.y, 0, Mathf.Max(0, contentHeight - viewport.height));
            attributeScroll = GUI.BeginScrollView(viewport, attributeScroll, content);
            float chargeTime = (player.heavyAttack?.chargeTime ?? new SlashTuningSettings.HeavyAttackSettings().chargeTime)
                * (1 - player.AlchemyReduction(AlchemyStat.ChargeReduction))
                / (player.HasIronSword ? player.IronChargeSpeed : 1);
            var row = new Rect(0, headingHeight, columnWidth, rowHeight);
            Label(new Rect(row.x + 12, 0, row.width - 24, headingHeight), "生存与机动", codexHeading, Ink);
            DrawAttributeRow(ref row, "最大生命", player.EffectiveMaxHealth.ToString());
            float ironReduction = player.IronBodyActive ? player.IronSword.bodyDamageReduction : 0;
            DrawAttributeRow(ref row, "受到伤害减免", $"{(1 - (1 - ironReduction) * (1 - player.AlchemyDamageReduction)):P0}");
            DrawAttributeRow(ref row, "受到的治疗量加成", $"+{player.AlchemyBonus(AlchemyStat.Healing):P0}");
            DrawAttributeRow(ref row, "受到的持续伤害减免", $"{player.AlchemyReduction(AlchemyStat.DotReduction):P0}");
            DrawAttributeRow(ref row, "受击硬直时间缩短", $"{player.AlchemyReduction(AlchemyStat.HitRecovery):P0}");
            DrawAttributeRow(ref row, "移动速度", $"{player.WalkingSpeed:0.##} 单位/秒");
            DrawAttributeRow(ref row, "冲刺距离", $"{player.DashDistance:0.##} 单位");
            DrawAttributeRow(ref row, "冲刺冷却时间", $"{player.EffectiveDashCooldown:0.##} 秒");
            DrawAttributeRow(ref row, "重击满蓄力时间", $"{chargeTime:0.##} 秒");
            DrawAttributeRow(ref row, "Q · 主动技能冷却时间", $"{player.TechniqueCooldown:0.##} 秒");
            DrawAttributeRow(ref row, "R · 大招冷却时间", $"{player.UltimateCooldown:0.##} 秒");

            row = new Rect(columnWidth + gap, headingHeight, columnWidth, rowHeight);
            Label(new Rect(row.x + 12, 0, row.width - 24, headingHeight), "攻击与暴击", codexHeading, Ink);
            DrawAttributeRow(ref row, "造成的伤害加成", $"+{player.AlchemyDamageBonus:P0}");
            float normalDamage = (1 + player.ComboDamageBonus) * (1 + player.AlchemyBonus(AlchemyStat.NormalDamage)) - 1;
            DrawAttributeRow(ref row, "普攻伤害加成", $"+{normalDamage * 100:0.#}%");
            DrawAttributeRow(ref row, "普攻攻速加成", $"+{(player.NormalAttackSpeedMultiplier - 1) * 100:0.#}%");
            DrawAttributeRow(ref row, "普攻判定距离加成", $"+{player.AlchemyBonus(AlchemyStat.NormalReach):P0}");
            DrawAttributeRow(ref row, "重击伤害加成", $"+{player.AlchemyBonus(AlchemyStat.HeavyDamage):P0}");
            DrawAttributeRow(ref row, "剑气伤害加成", $"+{player.AlchemyBonus(AlchemyStat.WaveDamage):P0}");
            DrawAttributeRow(ref row, "剑气飞行距离加成", $"+{player.AlchemyBonus(AlchemyStat.WaveDistance):P0}");
            DrawAttributeRow(ref row, "Q · 主动技能伤害加成", $"+{player.AlchemyBonus(AlchemyStat.TechniqueDamage):P0}");
            DrawAttributeRow(ref row, "R · 大招伤害加成", $"+{player.AlchemyBonus(AlchemyStat.UltimateDamage):P0}");
            DrawAttributeRow(ref row, "暴击率", $"{player.CriticalChance * 100:0.#}%");
            DrawAttributeRow(ref row, "暴击伤害", $"+{player.CriticalDamage * 100:0.#}%");

            if (effects.Count > 0)
            {
                Label(new Rect(12, effectsY, width - 24, headingHeight), "生效中的丹药", codexHeading, Ink);
                float y = effectsY + headingHeight;
                int index = 0;
                foreach (var entry in effects.Keys)
                {
                    float height = pillEffectHeights[index++];
                    Box(new Rect(0, y, width, 1), Bronze);
                    Label(new Rect(12, y + 4, width - 24, 28), entry.Name, codexHeading, Jade);
                    Label(new Rect(12, y + 34, width - 24, height - 40), entry.Effect, codexBody, Ink);
                    y += height;
                }
            }
            GUI.EndScrollView();
        }

        void DrawAttributeRow(ref Rect row, string name, string value)
        {
            Box(new Rect(row.x, row.y, row.width, 1), Bronze);
            Label(new Rect(row.x + 12, row.y, row.width - 188, row.height), name, codexBody, Ink);
            Label(new Rect(row.xMax - 176, row.y, 176, row.height), value, status, Jade);
            row.y += row.height;
        }

        void DrawWardrobe(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(0.06f, 0.11f, 0.09f, 0.72f));
            var panel = new Rect(width / 2 - 540, height / 2 - 350, 1080, 700);
            Box(panel, new Color(0.90f, 0.86f, 0.74f));
            DrawFrame(panel);
            Label(new Rect(panel.x + 30, panel.y + 18, 480, 44), "衣柜 · 角色选择", codexHeading, Ink);
            if (GUI.Button(new Rect(panel.xMax - 170, panel.y + 22, 140, 38), "关闭 F / Esc", travelButton))
            {
                arena.CharacterPanel.Close();
                GUI.depth = previousDepth;
                return;
            }
            Box(new Rect(panel.x + 30, panel.y + 75, panel.width - 60, 1), Bronze);

            var portrait = new Rect(panel.x + 30, panel.y + 94, 432, 576);
            Box(portrait, new Color(0.19f, 0.27f, 0.29f));
            Outline(portrait, Bronze);
            if (swordsmanPortrait != null)
                GUI.DrawTexture(new Rect(portrait.x + 10, portrait.y + 10, portrait.width - 20, portrait.height - 20),
                    swordsmanPortrait, ScaleMode.ScaleToFit);

            float x = panel.x + 498, y = panel.y + 96, w = panel.xMax - 30 - x;
            Label(new Rect(x, y, w, 36), "剑修", codexHeading, Ink);
            Label(new Rect(x, y + 42, w, 32), "流派 · 近战剑术", codexHeading, Jade);
            Label(new Rect(x, y + 77, w, 52), "擅长近身连斩，也可蓄力挥出剑气。", codexBody, Ink);
            Label(new Rect(x, y + 130, w, 30), TechniqueCatalog.FlowingCloud.name + " · 连段与突进", codexBody, Ink);
            Label(new Rect(x, y + 164, w, 30), TechniqueCatalog.IronSword.name + " · 蓄力重击与剑气", codexBody, Ink);
            Box(new Rect(x, y + 218, w, 1), Bronze);

            if (ultimateIcon != null)
                GUI.DrawTexture(new Rect(x, y + 240, 70, 70), ultimateIcon, ScaleMode.ScaleToFit);
            Label(new Rect(x + 86, y + 239, w - 86, 38), "R · " + FiveStarSwordSlash.Name, codexHeading, Ink);
            Label(new Rect(x + 86, y + 277, w - 86, 28), $"冷却 {FiveStarSwordSlash.Cooldown:0} 秒", codexBody, Jade);
            Label(new Rect(x, y + 326, w, 56), "斩出五道剑光，每段命中施放范围内的敌人。施放期间免受伤害。", codexBody, Ink);
            Label(new Rect(x, y + 388, w, 30),
                $"流云剑诀 · 完成 {FiveStarSwordSlash.FlowingCloudRequired} 次满层流云疾斩", codexBody, Ink);
            Label(new Rect(x, y + 422, w, 30),
                $"玄铁剑罡 · 释放 {FiveStarSwordSlash.IronSwordRequired} 次满蓄力重击", codexBody, Ink);
            if (DrawCharacterButton(new Rect(x, y + 524, w, 50), "确认使用剑修", true))
                arena.CharacterPanel.ConfirmCharacter(PlayableCharacter.Swordsman);
            GUI.depth = previousDepth;
        }

        void DrawCharacterTechniques(Rect panel)
        {
            Label(new Rect(panel.x + 28, panel.y + 94, panel.width - 56, 28),
                "当前修习 · 境界提升后，已解锁的招式效果一同生效", codexBody, Muted);
            var technique = arena.Player.Technique;
            if (GUI.Button(new Rect(panel.x + 28, panel.y + 140, 205, 58), technique.name, travelButton))
                techniqueScroll = Vector2.zero;
            Label(new Rect(panel.x + 28, panel.yMax - 116, 210, 70), "当前修习\n《" + technique.name + "》\nTab / Esc 返回", codexBody, Jade);
            Box(new Rect(panel.x + 252, panel.y + 140, 1, panel.height - 168), Bronze);
            DrawTechniqueDetails(new Rect(panel.x + 275, panel.y + 140, panel.width - 303, panel.height - 168),
                technique, ref techniqueScroll);
        }

        void DrawTechniqueCodex(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(0.06f, 0.11f, 0.09f, 0.72f));
            var panel = new Rect(width / 2 - 540, height / 2 - 360, 1080, 720);
            Box(panel, new Color(0.90f, 0.86f, 0.74f, 0.98f));
            DrawFrame(panel);
            Label(new Rect(panel.x + 28, panel.y + 18, 600, 45), "功法图鉴", codexHeading, Ink);
            Label(new Rect(panel.x + 28, panel.y + 63, 740, 28),
                "诸法归藏 · 境界提升后，已解锁的招式效果一同生效", codexBody, Muted);
            if (GUI.Button(new Rect(panel.xMax - 143, panel.y + 22, 115, 36), "关闭 Esc", travelButton))
            { arena.Codex.Close(); GUI.depth = previousDepth; return; }
            Box(new Rect(panel.x + 28, panel.y + 105, panel.width - 56, 1), Bronze);
            for (int i = 0; i < TechniqueCatalog.All.Count; i++)
            {
                var entry = TechniqueCatalog.All[i];
                if (GUI.Button(new Rect(panel.x + 28, panel.y + 129 + i * 72, 205, 58),
                    entry.name + (arena.Player.Technique == entry ? " · 已修习" : ""), travelButton))
                { codexIndex = i; codexScroll = Vector2.zero; }
            }
            Label(new Rect(panel.x + 28, panel.yMax - 116, 210, 70), "当前修习\n《" + arena.Player.Technique.name + "》\nF / Esc 返回", codexBody, Jade);
            Box(new Rect(panel.x + 252, panel.y + 128, 1, panel.height - 156), Bronze);
            var technique = TechniqueCatalog.All[Mathf.Clamp(codexIndex, 0, TechniqueCatalog.All.Count - 1)];
            bool selected = arena.Player.Technique == technique;
            if (GUI.Button(new Rect(panel.x + 28, panel.yMax - 184, 205, 46), selected ? "当前修习中" : "修习此功法", travelButton)
                && !selected) arena.Codex.Equip(technique);
            DrawTechniqueDetails(new Rect(panel.x + 275, panel.y + 119, panel.width - 303, panel.height - 147),
                technique, ref codexScroll);
            GUI.depth = previousDepth;
        }

        void DrawAlchemyCodex(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(0.06f, 0.11f, 0.09f, 0.72f));
            var panel = new Rect(width / 2 - 540, height / 2 - 360, 1080, 720);
            Box(panel, new Color(0.90f, 0.86f, 0.74f));
            DrawFrame(panel);

            var treasureTab = new Rect(panel.x + 28, panel.y + 22, 180, 44);
            var pillTab = new Rect(treasureTab.xMax + 12, treasureTab.y, treasureTab.width, treasureTab.height);
            var enemyTab = new Rect(pillTab.xMax + 12, pillTab.y, pillTab.width, pillTab.height);
            var codex = arena.Codex;
            if (DrawCharacterButton(treasureTab, "法宝图鉴", codex.AlchemyPage == AlchemyCodexPage.Treasures))
                codex.SelectAlchemyPage(AlchemyCodexPage.Treasures);
            if (DrawCharacterButton(pillTab, "丹药图鉴", codex.AlchemyPage == AlchemyCodexPage.Pills))
                codex.SelectAlchemyPage(AlchemyCodexPage.Pills);
            if (DrawCharacterButton(enemyTab, "敌人图鉴", codex.AlchemyPage == AlchemyCodexPage.Enemies))
                codex.SelectAlchemyPage(AlchemyCodexPage.Enemies);
            if (GUI.Button(new Rect(panel.xMax - 168, panel.y + 24, 140, 40), "关闭 F / Esc", travelButton))
            {
                codex.Close();
                GUI.depth = previousDepth;
                return;
            }
            Box(new Rect(panel.x + 28, panel.y + 82, panel.width - 56, 1), Bronze);
            var viewport = new Rect(panel.x + 28, panel.y + 99, panel.width - 56, panel.height - 127);
            switch (codex.AlchemyPage)
            {
                case AlchemyCodexPage.Treasures: DrawAlchemyGrid(viewport, TreasureCatalog.All, ref treasureScroll); break;
                case AlchemyCodexPage.Pills: DrawAlchemyGrid(viewport, PillCatalog.All, ref pillScroll); break;
                case AlchemyCodexPage.Enemies: DrawEnemyCodex(viewport); break;
            }
            GUI.depth = previousDepth;
        }

        void DrawEnemyCodex(Rect viewport)
        {
            const float listWidth = 210, itemHeight = 44, itemSpacing = 8;
            var entries = EnemyCatalog.All;
            var list = new Rect(viewport.x, viewport.y, listWidth, viewport.height);
            enemyListScroll = GUI.BeginScrollView(list, enemyListScroll,
                new Rect(0, 0, listWidth - 18, entries.Count * (itemHeight + itemSpacing) - itemSpacing));
            for (int i = 0; i < entries.Count; i++)
            {
                var item = new Rect(0, i * (itemHeight + itemSpacing), listWidth - 18, itemHeight);
                if (DrawCharacterButton(item, entries[i].Name, i == enemyCodexIndex) && i != enemyCodexIndex)
                {
                    enemyCodexIndex = i;
                    enemySkillScroll = Vector2.zero;
                }
            }
            GUI.EndScrollView();

            var entry = entries[enemyCodexIndex];
            float detailX = list.xMax + 10;
            float detailWidth = viewport.xMax - detailX;
            Label(new Rect(detailX, viewport.y, detailWidth, 32), entry.Name, codexHeading, Ink);
            Label(new Rect(detailX, viewport.y + 33, detailWidth, 27), entry.Location, codexBody, Muted);
            Box(new Rect(detailX, viewport.y + 67, detailWidth, 1), Bronze);

            var portrait = new Rect(detailX, viewport.y + 82, 324, viewport.height - 82);
            Box(portrait, new Color(.81f, .78f, .65f));
            Outline(portrait, Bronze);
            if (Event.current.type == EventType.Repaint)
                DrawHudIcon(new Rect(portrait.x + 10, portrait.y + 10, portrait.width - 20, portrait.height - 20),
                    entry.PortraitResource);

            float skillX = portrait.xMax + 18;
            float skillWidth = viewport.xMax - skillX;
            Label(new Rect(skillX, portrait.y, skillWidth, 30), "技能", codexHeading, Jade);
            DrawEnemySkills(new Rect(skillX, portrait.y + 36, skillWidth, portrait.height - 36), entry.Skills);
        }

        void DrawEnemySkills(Rect viewport, IReadOnlyList<(string Name, string Effect)> skills)
        {
            const float padding = 12, titleHeight = 30, gap = 8;
            float width = viewport.width - 20, textWidth = width - 2 * padding;
            float contentHeight = 0;
            foreach (var skill in skills)
                contentHeight += titleHeight + alchemyDescription.CalcHeight(new GUIContent(skill.Effect), textWidth) + padding + gap;

            enemySkillScroll = GUI.BeginScrollView(viewport, enemySkillScroll, new Rect(0, 0, width, contentHeight - gap));
            float y = 0;
            foreach (var skill in skills)
            {
                float effectHeight = alchemyDescription.CalcHeight(new GUIContent(skill.Effect), textWidth);
                float rowHeight = titleHeight + effectHeight + padding;
                Box(new Rect(0, y, width, rowHeight), new Color(.32f, .48f, .36f, .12f));
                Label(new Rect(padding, y, textWidth, titleHeight), skill.Name, codexBody, Jade);
                Label(new Rect(padding, y + titleHeight, textWidth, effectHeight), skill.Effect, alchemyDescription, Ink);
                y += rowHeight + gap;
            }
            GUI.EndScrollView();
        }

        void DrawAlchemyGrid(Rect viewport, IReadOnlyList<AlchemyEntry> entries, ref Vector2 scroll)
        {
            const int columns = 5;
            const float gap = 10, padding = 12, imageSize = 112, titleHeight = 30;
            const float descriptionTop = padding + imageSize + 4 + titleHeight + 4;
            int rowCount = (entries.Count + columns - 1) / columns;
            float contentWidth = viewport.width - 20;
            float cardWidth = (contentWidth - (columns - 1) * gap) / columns;
            float textWidth = cardWidth - 2 * padding;
            if (alchemyCardWidth != cardWidth)
            {
                alchemyCardWidth = cardWidth;
                alchemyRowOffsets.Clear();
            }
            if (!alchemyRowOffsets.TryGetValue(entries, out var rowOffsets))
            {
                rowOffsets = new float[rowCount + 1];
                for (int row = 0; row < rowCount; row++)
                {
                    float descriptionHeight = 0;
                    int end = Mathf.Min((row + 1) * columns, entries.Count);
                    for (int i = row * columns; i < end; i++)
                        descriptionHeight = Mathf.Max(descriptionHeight,
                            alchemyDescription.CalcHeight(new GUIContent(entries[i].Effect), textWidth));
                    rowOffsets[row + 1] = rowOffsets[row] + descriptionTop + descriptionHeight + padding + gap;
                }
                alchemyRowOffsets.Add(entries, rowOffsets);
            }

            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, contentWidth, Mathf.Max(0, rowOffsets[rowCount] - gap)));
            for (int row = 0; row < rowCount; row++)
            {
                float y = rowOffsets[row];
                if (y >= scroll.y + viewport.height) break;
                float cardHeight = rowOffsets[row + 1] - y - gap;
                if (y + cardHeight <= scroll.y) continue;

                int start = row * columns;
                int end = Mathf.Min(start + columns, entries.Count);
                for (int i = start; i < end; i++)
                {
                    var entry = entries[i];
                    float x = (i - start) * (cardWidth + gap);
                    var card = new Rect(x, y, cardWidth, cardHeight);
                    Box(card, new Color(0.14f, 0.20f, 0.21f));
                    Outline(card, Bronze);
                    var imageSlot = new Rect(x + (cardWidth - imageSize) / 2, y + padding, imageSize, imageSize);
                    Box(imageSlot, new Color(0.20f, 0.26f, 0.27f));
                    if (Event.current.type == EventType.Repaint)
                        DrawHudIcon(imageSlot, entry.IconResource);
                    Outline(imageSlot, Bronze);
                    Label(new Rect(x + padding, imageSlot.yMax + 4, textWidth, titleHeight),
                        entry.Name, alchemyTitle, AlchemyNameColors[(int)entry.Quality]);
                    Label(new Rect(x + padding, y + descriptionTop, textWidth, cardHeight - descriptionTop - padding),
                        entry.Effect, alchemyDescription, new Color(0.94f, 0.92f, 0.85f));
                }
            }
            GUI.EndScrollView();
        }

        void DrawTechniqueDetails(Rect viewport, TechniqueDefinition technique, ref Vector2 scroll)
        {
            float x = 0, w = viewport.width - 20, y = 0;
            float activeHeight = codexBody.CalcHeight(new GUIContent(technique.activeEffect), w);
            float contentHeight = 76 + 37 + activeHeight + 12;
            foreach (var stage in technique.stages)
                contentHeight += 41 + codexBody.CalcHeight(new GUIContent(stage.effect), w - 28);
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, w, contentHeight));
            Label(new Rect(x, y, w, 30), "《" + technique.name + "》  ·  主：" + technique.focus, codexHeading, Ink);
            Label(new Rect(x, y + 35, w, 28), technique.intent + "    当前境界：" + arena.Player.RealmDisplayName, codexBody, Jade);
            y += 76;
            foreach (var stage in technique.stages)
            {
                bool unlocked = arena.Player.Realm >= stage.realm;
                float bodyHeight = codexBody.CalcHeight(new GUIContent(stage.effect), w - 28);
                float cardHeight = 36 + bodyHeight;
                var card = new Rect(x, y, w, cardHeight);
                Box(card, unlocked ? new Color(0.32f, 0.48f, 0.36f, 0.12f) : new Color(0.28f, 0.27f, 0.24f, 0.08f));
                Box(new Rect(x, y, 3, cardHeight), unlocked ? Jade : Muted);
                Label(new Rect(x + 14, y + 3, w - 28, 27), SlashPlayer.GetRealmName(stage.realm) + "   " + stage.name
                    + (unlocked ? "   · 已解锁" : "   · 未解锁"), codexBody, unlocked ? Jade : Muted);
                Label(new Rect(x + 14, y + 31, w - 28, bodyHeight), stage.effect, codexBody, unlocked ? Ink : Muted);
                y += cardHeight + 5;
            }
            Label(new Rect(x, y + 1, w, 30), "主动·" + technique.activeName + "[Q]", codexHeading, Jade);
            Label(new Rect(x, y + 37, w, activeHeight), technique.activeEffect, codexBody, Ink);
            GUI.EndScrollView();
        }

        internal static Rect ReviveButtonRect(float width, float height)
            => new Rect(width / 2 - 160, height / 2 + 40, 320, 56);

        void DrawDefeatPanel(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -40;
            Box(new Rect(0, 0, width, height), new Color(0.08f, 0.10f, 0.09f, 0.72f));
            var panel = new Rect(width / 2 - 250, height / 2 - 150, 500, 300);
            Box(panel, new Color(0.88f, 0.83f, 0.70f, 0.98f));
            DrawFrame(panel);
            Label(new Rect(panel.x, panel.y + 32, panel.width, 48), "你已被击败", travelTitle, Cinnabar);
            Label(new Rect(panel.x + 30, panel.y + 105, panel.width - 60, 36), "返回洞府，恢复全部气血。", status, Ink);
            if (GUI.Button(ReviveButtonRect(width, height), "在洞府中复活", travelButton))
                arena.ReviveAtHome();
            GUI.depth = previousDepth;
        }

        void DrawPauseMenu(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(0.08f, 0.12f, 0.10f, 0.65f));
            var panel = new Rect(width / 2 - 220, height / 2 - 225, 440, 450);
            Box(panel, new Color(0.88f, 0.83f, 0.70f, 0.96f));
            DrawFrame(panel);
            Label(new Rect(panel.x, panel.y + 20, panel.width, 48), "暂停", travelTitle, Ink);
            if (GUI.Button(new Rect(panel.x + 60, panel.y + 90, 320, 54), "继续", travelButton))
                arena.ResumeGame();
            bool previousEnabled = GUI.enabled;
            GUI.enabled = false;
            GUI.Button(new Rect(panel.x + 60, panel.y + 165, 320, 54), new GUIContent("保存", "暂未开放"), travelButton);
            GUI.Button(new Rect(panel.x + 60, panel.y + 240, 320, 54), new GUIContent("设置", "暂未开放"), travelButton);
            GUI.enabled = previousEnabled;
            if (GUI.Button(new Rect(panel.x + 60, panel.y + 335, 320, 54), "返回家园", travelButton))
                arena.ReturnHome();
            GUI.depth = previousDepth;
        }

        void DrawLootChoice(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(.08f, .12f, .10f, .70f));
            var panel = new Rect(width / 2 - 510, height / 2 - 232, 1020, 464);
            Box(panel, new Color(.88f, .83f, .70f, .98f));
            DrawFrame(panel);

            var travel = arena.Travel;
            bool treasure = travel.Current == TravelDestination.AscensionPlatform;
            Label(new Rect(panel.x, panel.y + 18, panel.width, 42),
                treasure ? "升仙台结算 · 选择法宝" : "山野通关 · 选择丹药", travelTitle, Ink);
            int total = travel.ChallengeCompletedRounds;
            var details = new Rect(panel.x, panel.y + 63, panel.width, 30);
            if (treasure)
                Label(details, $"胜利 {total} 场 · 法宝 {total - travel.RemainingLootChoices + 1} / {total} · 三选一", status, Jade);
            else
                DrawResourceAmount(details, SpiritStoneIcon, $"+{travel.LootSpiritStones} · 丹药三选一", status, Jade, 28);

            const float margin = 24, gap = 16;
            var options = travel.LootOptions;
            float cardWidth = (panel.width - 2 * margin - (options.Count - 1) * gap) / options.Count;
            for (int i = 0; i < options.Count; i++)
            {
                var entry = options[i];
                var color = AlchemyNameColors[(int)entry.Quality];
                var card = new Rect(panel.x + margin + i * (cardWidth + gap), panel.y + 106, cardWidth, 332);
                Box(card, new Color(.14f, .20f, .21f));
                Outline(card, color);
                var icon = new Rect(card.center.x - 43, card.y + 15, 86, 86);
                if (Event.current.type == EventType.Repaint) DrawHudIcon(icon, entry.IconResource);
                Label(new Rect(card.x, card.y + 104, card.width, 30), entry.Name, alchemyTitle, color);
                Label(new Rect(card.x, card.y + 134, card.width, 22), AlchemyQualityNames[(int)entry.Quality], status, color);
                Label(new Rect(card.x + 16, card.y + 163, card.width - 32, 111), entry.Effect, alchemyDescription, Color.white);
                if (GUI.Button(new Rect(card.x + 16, card.yMax - 52, card.width - 32, 38), "领取", travelButton))
                {
                    travel.TryClaimLoot(i);
                    break;
                }
            }
            GUI.depth = previousDepth;
        }

        void DrawChallengeDecision(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(.08f, .12f, .10f, .65f));
            var panel = new Rect(width / 2 - 280, height / 2 - 142, 560, 284);
            Box(panel, new Color(.88f, .83f, .70f, .98f));
            DrawFrame(panel);
            var travel = arena.Travel;
            bool canContinue = travel.CanContinueChallenge;
            Label(new Rect(panel.x, panel.y + 24, panel.width, 48),
                canContinue ? "是否继续挑战？" : "升仙台挑战完成", travelTitle, Ink);
            Label(new Rect(panel.x, panel.y + 88, panel.width, 30),
                "第 " + travel.ChallengeRound + " / " + AscensionCultivator.PathCount + " 战 · 已击败" + travel.DefeatedChallenger, status, Jade);
            if (canContinue)
            {
                Label(new Rect(panel.x, panel.y + 119, panel.width, 28), "回复最大气血的 50%，至多回满", status, Ink);
                Label(new Rect(panel.x, panel.y + 148, panel.width, 28), "下一战对手气血与伤害再提升 10%", status, Ink);
                if (GUI.Button(new Rect(panel.x + 38, panel.y + 189, 232, 50), "继续挑战", travelButton))
                    travel.ResolveChallenge(true);
                else if (GUI.Button(new Rect(panel.x + 290, panel.y + 189, 232, 50), "结束挑战 · 领取法宝", travelButton))
                    travel.ResolveChallenge(false);
            }
            else
            {
                Label(new Rect(panel.x, panel.y + 123, panel.width, 30), "通关回复 · 最大气血的 50%，至多回满", status, Ink);
                if (GUI.Button(new Rect(panel.center.x - 120, panel.y + 189, 240, 50), "领取法宝", travelButton))
                    travel.ResolveChallenge(false);
            }
            GUI.depth = previousDepth;
        }

        void DrawTribulationDecision(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(.08f, .12f, .10f, .65f));
            var panel = new Rect(width / 2 - 230, height / 2 - 88, 460, 176);
            Box(panel, new Color(.88f, .83f, .70f, .98f));
            DrawFrame(panel);
            Label(new Rect(panel.x, panel.y + 23, panel.width, 48), "是否开始渡劫", travelTitle, Ink);
            if (GUI.Button(new Rect(panel.x + 35, panel.y + 100, 185, 46), "是", travelButton))
                arena.Travel.ResolveTribulation(true);
            else if (GUI.Button(new Rect(panel.x + 240, panel.y + 100, 185, 46), "否", travelButton))
                arena.Travel.ResolveTribulation(false);
            GUI.depth = previousDepth;
        }

        void DrawTreatmentDecision(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(.08f, .12f, .10f, .65f));
            var panel = new Rect(width / 2 - 280, height / 2 - 110, 560, 220);
            Box(panel, new Color(.88f, .83f, .70f, .98f));
            DrawFrame(panel);
            Label(new Rect(panel.x, panel.y + 22, panel.width, 48), "床边疗伤", travelTitle, Ink);
            Label(new Rect(panel.x, panel.y + 83, panel.width, 28),
                $"回复最大气血的 {SceneTravel.TreatmentHealPercent}%，至多回满", status, Ink);
            if (DrawPricedButton(new Rect(panel.x + 38, panel.y + 134, 232, 56),
                "治疗", SceneTravel.TreatmentCost.ToString(), SpiritStoneIcon))
                arena.Travel.ResolveTreatment(true);
            else if (GUI.Button(new Rect(panel.x + 290, panel.y + 134, 232, 56), "放弃治疗", travelButton))
                arena.Travel.ResolveTreatment(false);
            GUI.depth = previousDepth;
        }

        void DrawShop(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            var panel = DrawPaperModal(width, height);
            DrawResourceBalance(new Rect(panel.x + 28, panel.y + 22, 330, 68), SpiritStoneIcon, arena.Resources.SpiritStones);
            if (GUI.Button(new Rect(panel.xMax - 212, panel.y + 24, 184, 42), "关闭 F / Esc", travelButton))
                arena.Travel.CloseShop();
            // TODO: Display the selected stall's goods once the shop catalog is available.
            GUI.depth = previousDepth;
        }

        void DrawTravelSelection(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(0.08f, 0.12f, 0.10f, 0.65f));
            const float margin = 30, cardWidth = 270, gap = 15;
            var travel = arena.Travel;
            int choiceCount = 0;
            foreach (var choice in TravelMaps.Choices)
                if (travel.ShowsDestination(choice.destination)) choiceCount++;
            float panelWidth = margin * 2 + cardWidth * choiceCount + gap * (choiceCount - 1);
            const float panelHeight = 460;
            var panel = new Rect((width - panelWidth) / 2, (height - panelHeight) / 2, panelWidth, panelHeight);
            // Give the choice panel a legible backing while the two HUD frames stay translucent.
            Box(panel, new Color(0.88f, 0.83f, 0.70f, 0.96f));
            DrawFrame(panel);
            var resources = arena.Resources;
            Label(new Rect(panel.x + margin, panel.y + 16, 240, 42), "选择去处", routeTitle, Ink);
            if (resources.ServicesUnlocked)
                Label(new Rect(panel.x + margin, panel.y + 56, 270, 23), $"医庐与坊市共用次数 {resources.ServiceVisits}", small, Ink);

            string balance = resources.FortunePoints.ToString();
            resourceAmountContent.text = balance;
            float balanceWidth = Mathf.Max(204, Mathf.Ceil(routeBalanceValue.CalcSize(resourceAmountContent).x) + 130);
            var balanceCard = new Rect(panel.xMax - margin - balanceWidth, panel.y + 16, balanceWidth, 65);
            Box(balanceCard, new Color(.98f, .92f, .77f, .25f));
            Outline(balanceCard, new Color(Bronze.r, Bronze.g, Bronze.b, .48f));
            var balanceColor = new Color(.38f, .26f, .13f);
            Label(new Rect(balanceCard.x + 14, balanceCard.y, 48, balanceCard.height), "剩余", status, Muted);
            DrawResourceAmount(new Rect(balanceCard.x + 62, balanceCard.y, balanceCard.width - 74, balanceCard.height),
                FortunePointIcon, balance, routeBalanceValue, balanceColor, 44);

            int i = 0;
            foreach (var choice in TravelMaps.Choices)
            {
                if (!travel.ShowsDestination(choice.destination)) continue;
                var card = new Rect(panel.x + margin + i++ * (cardWidth + gap), panel.y + 96, cardWidth, 250);
                DrawFrame(card);
                var thumbnail = Resources.Load<Texture2D>(choice.resource);
                if (thumbnail != null)
                    GUI.DrawTexture(new Rect(card.x + 12, card.y + 12, 246, 164), thumbnail, ScaleMode.ScaleToFit);
                bool usesServiceVisit = SceneTravel.UsesServiceVisit(choice.destination);
                string cost = usesServiceVisit ? "共用次数 1" : SceneTravel.FortuneCost(choice.destination).ToString();
                if (choice.destination == TravelDestination.Tribulation && !resources.TribulationUnlocked)
                    cost = $"累计消耗 {resources.TribulationFortuneSpent}/{GameResources.TribulationUnlockSpend}";
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && travel.CanSelectDestination(choice.destination);
                bool selected = DrawPricedButton(new Rect(card.x + 12, card.y + 184, 246, 54), choice.title, cost,
                    usesServiceVisit ? null : FortunePointIcon);
                GUI.enabled = previousEnabled;
                if (selected)
                {
                    travel.SelectDestination(choice.destination);
                    GUI.depth = previousDepth;
                    return;
                }
            }
            if (!string.IsNullOrEmpty(travel.Error))
                Label(new Rect(panel.x + 20, panel.y + 346, panel.width - 40, 28), travel.Error, status, Cinnabar);
            if (GUI.Button(new Rect(panel.center.x - 206, panel.y + 381, 198, 44), "暂不出发", travelButton))
                travel.CancelSelection();
            if (GUI.Button(new Rect(panel.center.x + 8, panel.y + 381, 198, 44), "直接挑战关底 Boss", travelButton))
                travel.SelectDestination(TravelDestination.FinalBattle);
            GUI.depth = previousDepth;
        }

        void DrawWorldHealth(float scale)
        {
            if (!arena.practiceMode) return;
            double now = Time.timeAsDouble;
            foreach (var enemy in arena.Enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                if (enemy.TryGetComponent<EvilCultivatorBoss>(out var boss) && !boss.ShowsWorldHealth) continue;
                float labelHeight = enemy.GetComponent<ObliqueActor>().LabelHeight;
                Vector3 p = arena.WorldCamera.WorldToScreenPoint(arena.View.LiftPoint(enemy.transform.position, labelHeight));
                var healthLabel = new Rect(p.x / scale - 70, (Screen.height - p.y) / scale - 12, 140, 24);
                Label(healthLabel, enemy.Health.ToString(), health, DemoArt.Gold);
                if (!enemy.trainingDummy || !enemy.Alive || p.z <= 0) continue;
                var dpsLabel = new Rect(healthLabel.center.x - 104, healthLabel.y - 24, 208, 22);
                var stats = enemy.GetTrainingStats(now);
                Box(dpsLabel, new Color(.06f, .07f, .06f, .90f));
                Outline(dpsLabel, Bronze);
                Label(dpsLabel, $"总伤害 {stats.totalDamage}  ·  DPS {stats.dps:0.0}", health, DemoArt.Gold);
            }
        }

        void DrawCultivatorPassive(AscensionCultivator challenger, float scale)
        {
            var actor = challenger.GetComponent<ObliqueActor>();
            Vector3 point = arena.WorldCamera.WorldToScreenPoint(actor.HealthBarLowerLeft);
            if (point.z <= 0) return;
            const float size = BuffBarLayout.CellSize * .4f;
            var slot = new Rect(point.x / scale, (Screen.height - point.y) / scale + 4, size, size);
            DrawPassiveBuff(slot, challenger.PassiveBuff, scale);
        }

        void DrawPassiveBuff(Rect slot, CombatBuffView buff, float scale)
        {
            Box(slot, new Color(.08f, .11f, .11f, .92f));
            Outline(slot, Bronze);
            string title = buff.duration > 0 ? $"{buff.name} {Mathf.CeilToInt(buff.timeLeft)}秒" : buff.name;
            if (!DrawHudIcon(slot, buff.iconResource)) Label(slot, title, buffName, Cinnabar);
            if (buff.stacks > 0)
            {
                DrawBuffCountdown(slot, buff);
                var badge = new Rect(slot.xMax - 6, slot.yMax - 9, 10, 11);
                Box(badge, new Color(.02f, .06f, .12f, .92f));
                Outline(badge, Bronze);
                Label(badge, buff.stacks.ToString(), passiveStacks, Color.white);
            }
            HoverBuffSlot(slot, buff, scale);
        }

        void DrawDamageNumbers(float scale)
        {
            foreach (var item in arena.Effects.Texts)
            {
                float t = Mathf.Clamp01((Time.time - item.born) / 0.65f);
                Vector3 p = arena.WorldCamera.WorldToScreenPoint(
                    arena.View.LiftPoint(item.position, arena.View.LabelHeight + t * 0.8f));
                if (p.z <= 0) continue;
                var color = item.color;
                color.a *= 1 - t;
                damageNumberContent.text = item.text;
                float width = Mathf.Max(100, Mathf.Ceil(damage.CalcSize(damageNumberContent).x) + 4);
                var rect = new Rect(p.x / scale - width * .5f, (Screen.height - p.y) / scale - 22, width, 40);
                Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), item.text, damage,
                    new Color(0, 0, 0, color.a * 0.8f));
                Label(rect, item.text, damage, color);
            }
        }

        static void DrawPointer(float scale)
        {
            if (!DemoInput.MouseAvailable) return;
            Vector2 mouse = DemoInput.Pointer;
            float x = mouse.x / scale, y = (Screen.height - mouse.y) / scale;
            Color color = DemoArt.Jade;
            Box(new Rect(x - 9, y - 1, 5, 2), color);
            Box(new Rect(x + 4, y - 1, 5, 2), color);
            Box(new Rect(x - 1, y - 9, 2, 5), color);
            Box(new Rect(x - 1, y + 4, 2, 5), color);
        }

        void OnDestroy()
        {
            if (font != null) Destroy(font);
            if (paper != null) Destroy(paper);
            foreach (var countdown in buffCountdowns.Values) countdown.Dispose();
            buffCountdowns.Clear();
        }
    }
}
