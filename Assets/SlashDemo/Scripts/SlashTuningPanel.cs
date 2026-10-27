using System;
using UnityEngine;

namespace SlashDemo
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SlashTuningPanel : MonoBehaviour
    {
        const string SaveKey = "SlashDemo.RuntimeTuning.v3";
        const string PreviousSaveKey = "SlashDemo.RuntimeTuning.v2";
        const string LegacySaveKey = "SlashDemo.RuntimeTuning.v1";
        SlashArena arena;
        SlashTuningSettings defaults;
        public SlashTuningSettings Settings { get; private set; }
        public bool IsOpen { get; private set; }
        bool pointerCaptured;
        int page, strikeIndex, techniquePage;
        Vector2 scroll;
        Font font;
        GUIStyle label, heading, note, button, toggle;
        string status;
        float statusUntil;
        static readonly string[] Pages = { "角色", "连招", "敌人", "反馈", "视角", "重击", "功法", "主动技能" };
        static readonly string[] RealmOptions = {
            SlashPlayer.GetRealmName(CultivationRealm.QiRefining),
            SlashPlayer.GetRealmName(CultivationRealm.FoundationEstablishment),
            SlashPlayer.GetRealmName(CultivationRealm.GoldenCore),
            SlashPlayer.GetRealmName(CultivationRealm.NascentSoul)
        };
        static readonly Color Background = new Color(0.025f, 0.047f, 0.063f, 0.98f);

        static float Scale => Mathf.Max(0.01f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
        static Rect PanelRect => new Rect(Screen.width / Scale - 540, 160, 510, Screen.height / Scale - 190);
        static Rect ToggleRect => new Rect(Screen.width / Scale - 210, 160, 180, 38);
        bool PointerInside
        {
            get
            {
                if (!DemoInput.MouseAvailable) return false;
                Vector2 p = DemoInput.Pointer;
                p = new Vector2(p.x, Screen.height - p.y) / Scale;
                return (IsOpen ? PanelRect : ToggleRect).Contains(p);
            }
        }
        public bool BlocksGameplayInput => isActiveAndEnabled && (PointerInside || pointerCaptured);

        public void Initialize(SlashArena owner)
        {
            if (Settings != null) return;
            arena = owner;
            defaults = SlashTuningSettings.Capture(arena.Player, arena.Effects, arena.CameraRig, arena.View);
            defaults.Validate(new SlashTuningSettings());
            Settings = defaults.Clone();
            LoadSaved(false);
            ApplyAll();
        }

        void Update()
        {
            if (Settings == null) return;
            if (arena.Paused) return;
            if (DemoInput.TuningPressed) IsOpen = !IsOpen;
            if (!DemoInput.AttackHeld && !DemoInput.HeavyHeld) pointerCaptured = false;
            else if (PointerInside) pointerCaptured = true;
        }

        public void ApplyAll()
        {
            if (arena == null || Settings == null) return;
            Settings.Validate(defaults);
            arena.View.elevation = Settings.viewElevation;
            arena.View.characterScale = Settings.characterScale;
            arena.View.zoom = Settings.viewZoom;
            ApplyPlayer(arena.Player);
            foreach (var enemy in arena.Enemies) ApplyEnemy(enemy);
            arena.Effects.hitStopMultiplier = Settings.hitStopMultiplier;
            arena.Effects.SetVolume(Settings.volume);
            arena.CameraRig.shakeMultiplier = Settings.shakeMultiplier;
            arena.CameraRig.punchAmount = Settings.bladeFeedback.punchAmount;
        }

        public void ApplyPlayer(SlashPlayer player)
        {
            if (player != null && Settings != null) player.ApplyTuning(Settings);
        }
        public void ApplyEnemy(SlashEnemy enemy)
        {
            if (enemy != null && Settings != null) enemy.ApplyTuning(Settings);
        }

        void SetStatus(string value) { status = value; statusUntil = Time.unscaledTime + 5; }
        public void RestoreDefaults()
        {
            Settings = defaults.Clone();
            ApplyAll();
            SetStatus("已恢复场景默认值；点击保存可覆盖存档。");
        }
        void Save()
        {
            try
            {
                Settings.Validate(defaults);
                PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Settings));
                PlayerPrefs.Save();
                SetStatus("已保存，下次运行会自动载入。");
            }
            catch (Exception error) { Debug.LogWarning("Slash tuning save failed: " + error.Message); SetStatus("保存失败，请查看 Console。"); }
        }
        void LoadSaved(bool showStatus)
        {
            try
            {
                string key = PlayerPrefs.HasKey(SaveKey) ? SaveKey : PlayerPrefs.HasKey(PreviousSaveKey) ? PreviousSaveKey : LegacySaveKey;
                if (!PlayerPrefs.HasKey(key)) { if (showStatus) SetStatus("尚无保存参数。"); return; }
                var loaded = JsonUtility.FromJson<SlashTuningSettings>(PlayerPrefs.GetString(key));
                if (loaded == null || loaded.version < 1 || loaded.version > 3) { SetStatus("保存参数版本无效，保留当前值。"); return; }
                loaded.Validate(defaults);
                Settings = loaded;
                ApplyAll();
                if (showStatus) SetStatus("已载入保存参数。");
            }
            catch (Exception error) { Debug.LogWarning("Slash tuning load failed: " + error.Message); SetStatus("无法载入参数，保留当前值。"); }
        }

        void InitStyles()
        {
            if (label != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Arial" }, 18);
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, alignment = TextAnchor.MiddleLeft };
            label.normal.textColor = new Color(0.85f, 0.92f, 0.91f);
            heading = new GUIStyle(label) { fontSize = 23, fontStyle = FontStyle.Bold };
            heading.normal.textColor = DemoArt.Jade;
            note = new GUIStyle(label) { fontSize = 15, wordWrap = true };
            note.normal.textColor = new Color(0.58f, 0.72f, 0.74f);
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17 };
            toggle = new GUIStyle(GUI.skin.toggle) { font = font, fontSize = 18 };
            toggle.normal.textColor = Color.white;
            toggle.onNormal.textColor = DemoArt.Jade;
        }

        void OnGUI()
        {
            if (Settings == null) return;
            if (arena.Paused) return;
            InitStyles();
            int previousDepth = GUI.depth;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.depth = -20;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);
            if (!IsOpen)
            {
                if (GUI.Button(ToggleRect, "调参面板  [F1]", button)) IsOpen = true;
                GUI.matrix = previousMatrix;
                GUI.depth = previousDepth;
                return;
            }

            Rect r = PanelRect;
            Color oldColor = GUI.color;
            GUI.color = Background;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = DemoArt.Jade;
            GUI.DrawTexture(new Rect(r.x, r.y, 3, r.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(new Rect(r.x + 20, r.y + 10, 330, 36), "运行时调参", heading);
            if (GUI.Button(new Rect(r.xMax - 95, r.y + 10, 77, 32), "收起 F1", button)) IsOpen = false;
            int nextPage = GUI.SelectionGrid(new Rect(r.x + 18, r.y + 57, r.width - 36, 68), page, Pages, 4, button);
            if (nextPage != page) { page = nextPage; scroll = Vector2.zero; }
            var view = new Rect(r.x + 18, r.y + 140, r.width - 36, r.height - 250);
            float contentHeight = page == 7 ? 1150 : page == 6 ? (techniquePage == 1 ? 1300 : 900)
                : page == 0 ? 560 : page == 1 ? 760 : page == 2 ? 430 : page == 5 ? 560 : page == 3 ? 600 : 330;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 22, contentHeight));
            float y = 0;
            float w = view.width - 28;
            bool changedBefore = GUI.changed;
            GUI.changed = false;
            if (page == 0) DrawPlayer(ref y, w);
            else if (page == 1) DrawStrike(ref y, w);
            else if (page == 2) DrawEnemies(ref y, w);
            else if (page == 3) DrawFeedback(ref y, w);
            else if (page == 4) DrawView(ref y, w);
            else if (page == 5) DrawHeavy(ref y, w);
            else if (page == 6) DrawTechnique(ref y, w);
            else DrawTechniqueActive(ref y, w);
            bool tuningChanged = GUI.changed;
            GUI.changed |= changedBefore;
            GUI.EndScrollView();
            if (tuningChanged) ApplyAll();

            float footer = r.yMax - 94;
            float cell = (r.width - 48) / 3;
            if (GUI.Button(new Rect(r.x + 18, footer, cell, 34), "恢复默认", button))
                RestoreDefaults();
            if (GUI.Button(new Rect(r.x + 24 + cell, footer, cell, 34), "保存参数", button)) Save();
            if (GUI.Button(new Rect(r.x + 30 + cell * 2, footer, cell, 34), "载入保存", button)) LoadSaved(true);
            GUI.Label(new Rect(r.x + 18, r.yMax - 52, r.width - 36, 45), Time.unscaledTime < statusUntil ? status : "即时生效 · 重来保留 · 保存后可跨次运行", note);
            GUI.matrix = previousMatrix;
            GUI.depth = previousDepth;
        }

        float Slider(ref float y, float width, string name, float value, float min, float max, float step, string unit = "")
        {
            GUI.Label(new Rect(0, y, width - 120, 26), name, label);
            GUI.Label(new Rect(width - 120, y, 120, 26), value.ToString(step >= 1 ? "0" : step < 0.01f ? "0.000" : "0.00") + unit,
                new GUIStyle(label) { alignment = TextAnchor.MiddleRight });
            float next = GUI.HorizontalSlider(new Rect(30, y + 32, width - 60, 20), value, min, max);
            if (GUI.Button(new Rect(0, y + 28, 24, 24), "−", button)) next -= step;
            if (GUI.Button(new Rect(width - 24, y + 28, 24, 24), "+", button)) next += step;
            y += 63;
            // Merely opening a tab must not round a value imported from the scene.
            float result = next == value ? value : Mathf.Clamp(Mathf.Round(next / step) * step, min, max);
            if (result != value) GUI.changed = true;
            return result;
        }
        void DrawPlayer(ref float y, float width)
        {
            var s = Settings;
            var player = arena.Player;
            GUI.Label(new Rect(0, y, width, 26), "境界", label);
            y += 32;
            int realm = GUI.Toolbar(new Rect(0, y, width, 32), (int)player.Realm, RealmOptions, button);
            if (realm != (int)player.Realm) player.SetRealm((CultivationRealm)realm);
            y += 38;
            GUI.Label(new Rect(0, y, width, 40), "境界即时生效；返回洞府重置为练气，不随调参保存。", note);
            y += 48;
            s.moveSpeed = Slider(ref y, width, "移动速度", s.moveSpeed, 1, 12, 0.1f);
            s.dashSpeed = Slider(ref y, width, "冲刺速度", s.dashSpeed, 5, 30, 0.5f);
            s.dashDuration = Slider(ref y, width, "冲刺时长", s.dashDuration, 0.06f, 0.4f, 0.01f, " s");
            s.dashCooldown = Slider(ref y, width, "冲刺冷却", s.dashCooldown, 0.15f, 3, 0.05f, " s");
            s.comboResetTime = Slider(ref y, width, "连招衔接窗口", s.comboResetTime, 0.15f, 2, 0.05f, " s");
            s.maxHealth = Mathf.RoundToInt(Slider(ref y, width, "最大气血（保留血量比例）", s.maxHealth, 20, 500, 10));
            s.invincible = GUI.Toggle(new Rect(0, y + 5, width, 32), s.invincible, " 调试无敌", toggle);
        }
        void DrawStrike(ref float y, float width)
        {
            strikeIndex = GUI.Toolbar(new Rect(0, y, width, 32), strikeIndex, new[] { "第一段", "第二段", "第三段" }, button);
            y += 42;
            var s = Settings.strikes[strikeIndex];
            GUI.Label(new Rect(0, y, width, 32), new[] { "前方劈砍", "前方反向劈砍", "前方大范围劈砍" }[strikeIndex], heading);
            y += 48;
            s.damage = Mathf.RoundToInt(Slider(ref y, width, "伤害", s.damage, 1, 200, 1));
            s.reach = Slider(ref y, width, "攻击距离", s.reach, 0.5f, 4.5f, 0.05f);
            s.arc = Slider(ref y, width, "挥剑幅度", s.arc, 30, 170, 5, "°");
            s.bladeWidth = Slider(ref y, width, "剑刃判定宽度", s.bladeWidth, 0.04f, 0.4f, 0.01f);
            s.duration = Slider(ref y, width, "每刀总时长", s.duration, 0.2f, 1.2f, 0.01f, " s");
            s.windupRatio = Slider(ref y, width, "起手占比", s.windupRatio, 0.1f, 0.45f, 0.01f);
            s.activeRatio = Slider(ref y, width, "发力占比", s.activeRatio, 0.15f, Mathf.Min(0.55f, 0.85f - s.windupRatio), 0.01f);
            s.knockback = Slider(ref y, width, "击退力度", s.knockback, 0, 20, 0.5f);
            GUI.Label(new Rect(0, y + 5, width, 78), "攻击期间方向移动速度降低60%，无自动前踏；空格可冲刺打断。仅发力阶段的剑刃轨迹造成伤害，参数在下一次出手时生效。", note);
        }
        void DrawEnemies(ref float y, float width)
        {
            var s = Settings;
            s.enemySpeedMultiplier = Slider(ref y, width, "敌人移速倍率", s.enemySpeedMultiplier, 0, 3, 0.05f, "×");
            s.enemyHealthMultiplier = Slider(ref y, width, "敌人血量倍率", s.enemyHealthMultiplier, 0.2f, 5, 0.1f, "×");
            s.enemyDamage = Mathf.RoundToInt(Slider(ref y, width, "敌人伤害", s.enemyDamage, 1, 60, 1));
            s.enemyWindup = Slider(ref y, width, "攻击预警时长", s.enemyWindup, 0.2f, 2, 0.02f, " s");
            s.enemyRange = Slider(ref y, width, "敌人攻击距离", s.enemyRange, 0.5f, 3, 0.05f);
            s.dummyHealth = Mathf.RoundToInt(Slider(ref y, width, "木桩最大血量", s.dummyHealth, 50, 2000, 50));
            GUI.Label(new Rect(0, y + 3, width, 46), "倍率作用于本波基础数值，新生成敌人也会沿用。", note);
        }
        void DrawFeedback(ref float y, float width)
        {
            var s = Settings;
            s.hitStopMultiplier = Slider(ref y, width, "命中停顿倍率", s.hitStopMultiplier, 0, 2.5f, 0.05f, "×");
            s.shakeMultiplier = Slider(ref y, width, "镜头震动倍率", s.shakeMultiplier, 0, 2.5f, 0.05f, "×");
            s.trailLifetime = Slider(ref y, width, "刀光拖尾时长", s.trailLifetime, 0.10f, 0.14f, 0.01f, " s");
            s.bladeFeedback.glowWidth = Slider(ref y, width, "刀光外晕宽度", s.bladeFeedback.glowWidth, 0.05f, 0.18f, 0.01f);
            s.bladeFeedback.hdrIntensity = Slider(ref y, width, "刀光 HDR 强度", s.bladeFeedback.hdrIntensity, 1, 3, 0.1f);
            s.bladeFeedback.crescentPower = Slider(ref y, width, "月牙收尖指数", s.bladeFeedback.crescentPower, 0.4f, 1, 0.05f);
            s.bladeFeedback.punchAmount = Slider(ref y, width, "命中镜头推近幅度", s.bladeFeedback.punchAmount, 0, 0.08f, 0.005f);
            s.volume = Slider(ref y, width, "音效音量", s.volume, 0, 1, 0.01f);
            GUI.Label(new Rect(0, y + 5, width, 78), "镜头冲击整体按 10% 输出，推近 0.04 实际约 0.4%，设为 0 可关闭。收尖指数越大，中段越集中。", note);
        }

        void DrawView(ref float y, float width)
        {
            var s = Settings;
            s.viewElevation = Slider(ref y, width, "俯角（相对地面）", s.viewElevation, 30, 65, 1, "°");
            s.characterScale = Slider(ref y, width, "角色视觉比例", s.characterScale, 1, 1.6f, 0.05f, "×");
            s.viewZoom = Slider(ref y, width, "镜头放大", s.viewZoom, 0.85f, 1.25f, 0.05f, "×");
            GUI.Label(new Rect(0, y + 5, width, 78), "默认 45° 斜上方。俯角越小，地面纵深压缩越强。视觉比例不改变脚下碰撞与攻击距离。", note);
        }

        void DrawHeavy(ref float y, float width)
        {
            var s = Settings.heavyAttack;
            s.chargeTime = Slider(ref y, width, "重击蓄力时长", s.chargeTime, 0.1f, 3, 0.05f, " s");
            s.recoveryTime = Slider(ref y, width, "释放后摇时长", s.recoveryTime, 0, 1.5f, 0.01f, " s");
            s.width = Slider(ref y, width, "剑气宽度", s.width, 0.25f, 5, 0.05f);
            s.distance = Slider(ref y, width, "剑气飞行距离", s.distance, 1, 18, 0.25f);
            s.speed = Slider(ref y, width, "剑气飞行速度", s.speed, 3, 30, 0.5f);
            s.damage = Mathf.RoundToInt(Slider(ref y, width, "剑气伤害", s.damage, 1, 200, 1));
            s.knockback = Slider(ref y, width, "剑气击退", s.knockback, 0, 20, 0.5f);
            GUI.Label(new Rect(0, y, width, 80), "满蓄松手后停顿 0.033s，再横扫 0.17s；扫过前方时发出剑气，之后进入收势。全程锁定移动，空格可打断。参数从下一次蓄力生效。", note);
        }

        void DrawTechnique(ref float y, float width)
        {
            var s = Settings.flowingCloud;
            var player = arena.Player;
            GUI.Label(new Rect(0, y, width, 32), "《流云剑诀》 · 连段", heading);
            y += 40;
            int realm = GUI.Toolbar(new Rect(0, y, width, 32), (int)player.Realm, RealmOptions, button);
            if (realm != (int)player.Realm) player.SetRealm((CultivationRealm)realm);
            y += 42;
            int nextPage = GUI.Toolbar(new Rect(0, y, width, 32), techniquePage, new[] { "被动与旋风斩", "Q 主动" }, button);
            if (nextPage != techniquePage) { techniquePage = nextPage; scroll = Vector2.zero; }
            y += 40;
            GUI.Label(new Rect(0, y, width, 50), "叠浪 " + player.ComboStacks + " / " + s.maxStacks
                + "    剩余 " + player.ComboStackTimeLeft.ToString("0.0") + " s"
                + "    普攻攻速 +" + ((player.NormalAttackSpeedMultiplier - 1) * 100).ToString("0.#") + "%\n满层减 Q 冷却："
                + (player.Realm < CultivationRealm.FoundationEstablishment ? "筑基解锁"
                    : player.RebuildCooldownRewardPending ? "待触发（首次满层减 1 秒）" : "需完成下一次 Q 释放"), note);
            y += 56;
            if (techniquePage == 0)
            {
                float percent = Slider(ref y, width, "叠浪每层增伤", s.stackBonus * 100, 0, 25, 1, "%");
                if (percent != s.stackBonus * 100) s.stackBonus = percent / 100;
                s.maxStacks = Mathf.RoundToInt(Slider(ref y, width, "叠浪最大层数", s.maxStacks, 1, FlowingCloudRules.MaxStacks, 1));
                GUI.Label(new Rect(0, y, width, 50), "叠浪达到 5 层时额外提升 20% 普攻伤害。\n当前普攻总增伤 +"
                    + (player.ComboDamageBonus * 100).ToString("0.#") + "%", note);
                y += 56;
                float speedPercent = Slider(ref y, width, "筑基起风 · 每层普攻攻速", s.attackSpeedPerStack * 100, 0, 25, 1, "%");
                if (speedPercent != s.attackSpeedPerStack * 100) s.attackSpeedPerStack = speedPercent / 100;
                s.pursuitDamage = Mathf.RoundToInt(Slider(ref y, width, "旋风斩基础伤害", s.pursuitDamage, 1, 200, 1));
                s.pursuitReach = Slider(ref y, width, "旋风斩环形剑气半径", s.pursuitReach, 0.5f, 4.5f, 0.05f);
                s.pursuitDuration = Slider(ref y, width, "旋风斩挥剑时长", s.pursuitDuration, 0.18f, 0.8f, 0.01f, " s");
                s.pursuitKnockback = Slider(ref y, width, "旋风斩击退", s.pursuitKnockback, 0, 20, 0.5f);
                var wave = Settings.heavyAttack;
                GUI.Label(new Rect(0, y, width, 116), "金丹风舞：旋风斩命中叠两层叠浪。元婴剑潮共用「重击」页基础参数：伤害 "
                    + wave.damage + "，宽度 " + wave.width.ToString("0.##") + "，距离 " + wave.distance.ToString("0.##")
                    + "，速度 " + wave.speed.ToString("0.##") + "，击退 " + wave.knockback.ToString("0.##")
                    + "。剑潮按普攻增伤结算；每道首次命中叠一层并减少 Q 冷却 1 秒。", note);
                y += 124;
            }
            else
            {
                DrawTechniqueActive(ref y, width);
                return;
            }
            if (GUI.Button(new Rect(0, y, width / 2 - 5, 34), "清空连击", button)) player.ResetTechniqueCombo();
            if (GUI.Button(new Rect(width / 2 + 5, y, width / 2 - 5, 34), "刷新 Q 冷却", button)) player.ResetTechniqueCooldown();
        }

        void DrawTechniqueActive(ref float y, float width)
        {
            var s = Settings.flowingCloud;
            var player = arena.Player;
            GUI.Label(new Rect(0, y, width, 32), "Q · 流云疾斩", heading); y += 38;
            GUI.Label(new Rect(0, y, width, 48), "叠浪 " + player.ComboStacks + " / " + s.maxStacks
                + "    冷却 " + player.TechniqueCooldownLeft.ToString("0.0") + " s\n实际距离 "
                + s.RushDistance(0).ToString("0.##") + "–" + s.RushDistance(5).ToString("0.##")
                + " 米；施放时消耗全部叠浪，Q 命中不叠层。", note); y += 56;
            float cell = (width - 12) / 3;
            if (GUI.Button(new Rect(0, y, cell, 34), "叠满连击", button)) player.FillTechniqueStacksForDebug();
            if (GUI.Button(new Rect(cell + 6, y, cell, 34), "清空连击", button)) player.ResetTechniqueCombo();
            if (GUI.Button(new Rect((cell + 6) * 2, y, cell, 34), "刷新 Q 冷却", button)) player.ResetTechniqueCooldown();
            y += 44;
            s.activeCooldown = Slider(ref y, width, "Q 冷却", s.activeCooldown, 0.2f, 15, 0.1f, " s");
            s.rushDistanceMultiplier = Slider(ref y, width, "Q 突进距离倍率", s.rushDistanceMultiplier, 1, 5, 0.1f, " ×");
            s.rushDistance = Slider(ref y, width, "Q 基础距离（倍率前）", s.rushDistance, 0, 3, 0.05f);
            s.rushDistancePerStack = Slider(ref y, width, "Q 每层距离（倍率前）", s.rushDistancePerStack, 0.05f, 0.6f, 0.05f);
            s.rushDuration = Slider(ref y, width, "Q 突进时长", s.rushDuration, 0.06f, 0.3f, 0.01f, " s");
            s.activeBaseDamage = Mathf.RoundToInt(Slider(ref y, width, "Q 练气基础伤害", s.activeBaseDamage, 1, 200, 1));
            s.activeDamagePerRealm = Mathf.RoundToInt(Slider(ref y, width, "Q 每境界增加伤害", s.activeDamagePerRealm, 0, 50, 1));
            s.activeDamagePerStack = Mathf.RoundToInt(Slider(ref y, width, "Q 每层追加伤害", s.activeDamagePerStack, 1, 50, 1));
            s.activeWidth = Slider(ref y, width, "Q 基础剑光宽度", s.activeWidth, 0.4f, 3, 0.05f);
            s.activeWidthPerStack = Slider(ref y, width, "Q 每层追加宽度", s.activeWidthPerStack, 0.05f, 0.4f, 0.01f);
            s.rushTrailLifetime = Slider(ref y, width, "Q 轨迹剑光消散时间", s.rushTrailLifetime, 0.2f, 2, 0.05f, " s");
            s.activeDuration = Slider(ref y, width, "Q 突进动作时长", s.activeDuration, 0.16f, 0.8f, 0.01f, " s");
            s.activeKnockback = Slider(ref y, width, "Q 沿途击退", s.activeKnockback, 0, 20, 0.5f);
            var finisher = Settings.strikes[2];
            GUI.Label(new Rect(0, y, width, 76), "5 层落点追加普攻第三段：伤害 " + finisher.damage
                + "，范围 " + finisher.reach.ToString("0.##") + "，挥剑 " + finisher.arc.ToString("0")
                + "°。动作与数值共用「连招 → 第三段」设置；此斩击属于 Q，不叠连击。", note);
            y += 84;
            if (GUI.Button(new Rect(0, y, width, 34), "调整满层收招：普攻第三段", button))
            { page = 1; strikeIndex = 2; scroll = Vector2.zero; }
        }

        void OnDisable() { pointerCaptured = false; }
        void OnDestroy() { if (font != null) Destroy(font); }
    }
}
