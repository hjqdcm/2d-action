using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    public readonly struct CombatBuffView
    {
        public readonly string id, name, iconResource, description;
        public readonly int stacks;
        public readonly float timeLeft, duration;
        public float RemainingFraction => duration > 0 ? Mathf.Clamp01(timeLeft / duration) : 1;
        public CombatBuffView(string id, string name, int stacks, string iconResource = null, string description = null,
            float timeLeft = 0, float duration = 0)
        {
            this.id = id; this.name = name; this.stacks = stacks; this.iconResource = iconResource; this.description = description;
            this.timeLeft = timeLeft; this.duration = duration;
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
            if (buff.stacks <= 0) { Remove(buff.id); return; }
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
        GUIStyle codexBody, codexHeading;
        int codexIndex;
        Vector2 techniqueScroll;
        Vector2 codexScroll;
        static readonly string[] CharacterPageNames = { "功法", "属性", "物品" };
        GUIStyle small, health, damage, status, travelTitle, travelButton;
        GUIStyle buffName, buffStacks, buffTooltipBody, buffTooltipTitle;
        readonly Dictionary<string, Texture2D> buffIcons = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        readonly Dictionary<string, BuffCountdownMask> buffCountdowns = new Dictionary<string, BuffCountdownMask>(StringComparer.Ordinal);
        CombatBuffView hoveredBuff;
        Rect hoveredBuffSlot;
        public string HoveredBuffId { get; private set; }
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
        static readonly Color Muted = new Color(0.43f, 0.40f, 0.31f);
        static readonly Color Jade = new Color(0.22f, 0.43f, 0.34f);
        static readonly Color Cinnabar = new Color(0.61f, 0.23f, 0.18f);

        void Awake() { arena = GetComponent<SlashArena>(); }
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
            buffTooltipBody = new GUIStyle(small) { fontSize = 15, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset() };
            buffTooltipTitle = new GUIStyle(buffTooltipBody) { fontSize = 18, fontStyle = FontStyle.Bold };
            status = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            travelTitle = new GUIStyle(status) { fontSize = 27, fontStyle = FontStyle.Bold };
            travelButton = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20 };
            travelButton.normal.textColor = Ink;
            codexBody = new GUIStyle(small) { fontSize = 17, wordWrap = true };
            codexHeading = new GUIStyle(codexBody) { fontSize = 22, fontStyle = FontStyle.Bold };
            skillIcon = Resources.Load<Texture2D>("SlashDemo/FlowingCloudActiveIcon");
            paper = new Texture2D(64, 64, TextureFormat.RGBA32, false) {
                name = "Manor HUD rice paper", wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    // Deterministic grain leaves gameplay's random sequence untouched.
                    float grain = (((x * 73 + y * 151 + x * y * 19) % 101) / 100f - 0.5f) * 0.025f;
                    pixels[y * 64 + x] = new Color(0.88f + grain, 0.83f + grain, 0.70f + grain, 0.80f);
                }
            paper.SetPixels(pixels);
            paper.Apply(false, true);
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

        void DrawFrame(Rect rect)
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
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(inside, paper, new Rect(0, 0, inside.width / 64, inside.height / 64));
            GUI.color = previous;
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
            Label(new Rect(220, 64, 112, 25), player.Health + " / " + player.maxHealth, small, Cinnabar);
            DrawGauge(new Rect(50, 102, 282, 8), player.Health / (float)player.maxHealth, Cinnabar);
            DrawBuffBar(new Rect(50, healthPanel.yMax + 10, 282, 0), scale);

            DrawFrame(new Rect(width / 2 - 105, height - 58, 210, 31));
            DrawGauge(new Rect(width / 2 - 105, height - 28, 210, 3), player.DashReady, Jade);
            Label(new Rect(width / 2 - 105, height - 58, 210, 30), player.DashReady >= 1 ? "冲刺就绪" : "冲刺冷却中", status, player.DashReady >= 1 ? Jade : Muted);
            DrawActiveSkill(width, height);
            if (arena.Codex != null && arena.Codex.ShowPrompt)
            {
                var prompt = new Rect(width / 2 - 175, height / 2 - 25, 350, 50);
                DrawFrame(prompt);
                Label(prompt, "F查看功法图鉴", status, Ink);
            }

            if (!arena.Paused && !arena.Defeated)
            {
                DrawWorldHealth(scale);
                DrawDamageNumbers(scale);
                DrawPointer(scale);
            }
            DrawBuffTooltip(new Rect(0, 0, width, height));
            if (arena.Codex != null && arena.Codex.IsOpen) DrawTechniqueCodex(width, height);
            else if (arena.CharacterPanel != null && arena.CharacterPanel.IsOpen) DrawCharacterPanel(width, height);
            else if (arena.Travel != null && arena.Travel.IsSelectionOpen) DrawTravelSelection(width, height);
            else if (arena.PauseMenuOpen) DrawPauseMenu(width, height);
            GUI.matrix = previous;
        }

        void DrawBuffBar(Rect area, float scale)
        {
            HoveredBuffId = null;
            var buffs = VisibleBuffs;
            Vector2 pointer = new Vector2(DemoInput.Pointer.x / scale, (Screen.height - DemoInput.Pointer.y) / scale);
            bool canHover = DemoInput.MouseAvailable && !arena.PauseMenuOpen
                && (arena.CharacterPanel == null || !arena.CharacterPanel.IsOpen)
                && (arena.Codex == null || !arena.Codex.IsOpen) && (arena.Travel == null || !arena.Travel.IsSelectionOpen);
            for (int i = 0; i < buffs.Count; i++)
            {
                var slot = BuffBarLayout.Slot(i, area);
                DrawFrame(slot);
                Texture2D icon = null;
                string resource = buffs[i].iconResource;
                if (!string.IsNullOrEmpty(resource) && !buffIcons.TryGetValue(resource, out icon))
                { icon = Resources.Load<Texture2D>(resource); buffIcons.Add(resource, icon); }
                if (icon != null)
                    GUI.DrawTexture(new Rect(slot.x + 2, slot.y + 2, slot.width - 4, slot.height - 4), icon, ScaleMode.ScaleToFit);
                else Label(new Rect(slot.x + 4, slot.y + 20, slot.width - 8, 20), buffs[i].name, buffName, Jade);
                DrawBuffCountdown(slot, buffs[i]);
                Rect badge = BuffBarLayout.StackBadge(slot);
                Box(badge, new Color(0.02f, 0.06f, 0.12f, 0.92f));
                Outline(badge, Bronze);
                Label(badge, buffs[i].stacks.ToString(), buffStacks, Color.white);
                if (canHover && slot.Contains(pointer) && !string.IsNullOrEmpty(buffs[i].description))
                { HoveredBuffId = buffs[i].id; hoveredBuff = buffs[i]; hoveredBuffSlot = slot; }
            }
        }

        void DrawBuffCountdown(Rect slot, CombatBuffView buff)
        {
            if (buff.duration <= 0 || Event.current.type != EventType.Repaint) return;
            if (!buffCountdowns.TryGetValue(buff.id, out var countdown))
            { countdown = new BuffCountdownMask(); buffCountdowns.Add(buff.id, countdown); }
            float remaining = buff.RemainingFraction;
            countdown.Update(remaining);
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
            Box(rect, new Color(0.025f, 0.06f, 0.08f, 0.98f));
            Outline(rect, Bronze);
            Label(new Rect(rect.x + 12, rect.y + 9, rect.width - 24, 27), hoveredBuff.name, buffTooltipTitle, new Color(0.64f, 0.91f, 0.86f));
            Label(new Rect(rect.x + 12, rect.y + 40, rect.width - 24, bodyHeight), hoveredBuff.description, buffTooltipBody,
                new Color(0.92f, 0.92f, 0.84f));
            GUI.depth = previousDepth;
        }

        void DrawActiveSkill(float width, float height)
        {
            var player = arena.Player;
            var icon = new Rect(60, height - 174, 108, 108);
            Color previous = GUI.color;
            GUI.color = player.TechniqueReady ? Color.white : new Color(0.48f, 0.52f, 0.51f);
            GUI.DrawTexture(icon, skillIcon);
            GUI.color = previous;
            if (player.TechniqueCooldownLeft > 0)
                Label(icon, player.TechniqueCooldownLeft.ToString("0.0"), damage, Color.white);
            var caption = new Rect(icon.x - 30, icon.yMax + 7, 168, 31);
            DrawFrame(caption);
            Label(caption, "Q · " + player.Technique.activeName, status, Ink);
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

        void DrawCharacterPanel(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(0.06f, 0.11f, 0.09f, 0.72f));
            var panel = new Rect(width / 2 - 540, height / 2 - 360, 1080, 720);
            Box(panel, new Color(0.90f, 0.86f, 0.74f, 1));
            DrawFrame(panel);
            for (int i = 0; i < CharacterPageNames.Length; i++)
                if (DrawCharacterButton(new Rect(panel.x + 28 + i * 192, panel.y + 22, 180, 46),
                    CharacterPageNames[i], arena.CharacterPanel.ActivePage == (CharacterPanelPage)i))
                { arena.CharacterPanel.SelectPage((CharacterPanelPage)i); techniqueScroll = Vector2.zero; }
            if (GUI.Button(new Rect(panel.xMax - 212, panel.y + 24, 184, 42), "关闭 Tab / Esc", travelButton))
            { arena.CharacterPanel.Close(); GUI.depth = previousDepth; return; }
            Box(new Rect(panel.x + 28, panel.y + 83, panel.width - 56, 1), Bronze);
            if (arena.CharacterPanel.ActivePage == CharacterPanelPage.Techniques) DrawCharacterTechniques(panel);
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
                if (GUI.Button(new Rect(panel.x + 28, panel.y + 129 + i * 72, 205, 58), entry.name, travelButton))
                { codexIndex = i; codexScroll = Vector2.zero; }
            }
            Label(new Rect(panel.x + 28, panel.yMax - 116, 210, 70), "当前修习\n《" + arena.Player.Technique.name + "》\nF / Esc 返回", codexBody, Jade);
            Box(new Rect(panel.x + 252, panel.y + 128, 1, panel.height - 156), Bronze);
            var technique = TechniqueCatalog.All[Mathf.Clamp(codexIndex, 0, TechniqueCatalog.All.Count - 1)];
            DrawTechniqueDetails(new Rect(panel.x + 275, panel.y + 119, panel.width - 303, panel.height - 147),
                technique, ref codexScroll);
            GUI.depth = previousDepth;
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

        void DrawTravelSelection(float width, float height)
        {
            int previousDepth = GUI.depth;
            GUI.depth = -30;
            Box(new Rect(0, 0, width, height), new Color(0.08f, 0.12f, 0.10f, 0.65f));
            var panel = new Rect(width / 2 - 450, height / 2 - 220, 900, 440);
            // Give the choice panel a legible backing while the two HUD frames stay translucent.
            Box(panel, new Color(0.88f, 0.83f, 0.70f, 0.96f));
            DrawFrame(panel);
            Label(new Rect(panel.x, panel.y + 15, panel.width, 45), "选择去处", travelTitle, Ink);
            for (int i = 0; i < TravelMaps.Choices.Length; i++)
            {
                var choice = TravelMaps.Choices[i];
                var card = new Rect(panel.x + 30 + i * 285, panel.y + 80, 270, 250);
                DrawFrame(card);
                var thumbnail = Resources.Load<Texture2D>(choice.resource);
                if (thumbnail != null)
                    GUI.DrawTexture(new Rect(card.x + 12, card.y + 12, 246, 164), thumbnail, ScaleMode.ScaleToFit);
                if (GUI.Button(new Rect(card.x + 12, card.y + 190, 246, 44), choice.title, travelButton))
                {
                    arena.Travel.SelectDestination(choice.destination);
                    GUI.depth = previousDepth;
                    return;
                }
            }
            if (!string.IsNullOrEmpty(arena.Travel.Error))
                Label(new Rect(panel.x + 20, panel.y + 330, panel.width - 40, 28), arena.Travel.Error, status, Cinnabar);
            if (GUI.Button(new Rect(panel.center.x - 95, panel.y + 365, 190, 44), "暂不出发", travelButton))
                arena.Travel.CancelSelection();
            GUI.depth = previousDepth;
        }

        void DrawWorldHealth(float scale)
        {
            if (!arena.practiceMode) return;
            foreach (var enemy in arena.Enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float labelHeight = enemy.GetComponent<ObliqueActor>().LabelHeight;
                Vector3 p = arena.WorldCamera.WorldToScreenPoint(arena.View.LiftPoint(enemy.transform.position, labelHeight));
                Label(new Rect(p.x / scale - 70, (Screen.height - p.y) / scale - 12, 140, 24), enemy.Health.ToString(), health, DemoArt.Gold);
            }
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
                var rect = new Rect(p.x / scale - 50, (Screen.height - p.y) / scale - 22, 100, 40);
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
