using UnityEngine;

namespace SlashDemo
{
    public sealed class SlashHUD : MonoBehaviour
    {
        SlashArena arena;
        Font font;
        GUIStyle text, small, title, center, number, button;
        static readonly Color Muted = new Color(0.58f, 0.69f, 0.72f);
        static readonly Color Panel = new Color(0.025f, 0.046f, 0.061f, 0.94f);

        void Awake() { arena = GetComponent<SlashArena>(); }
        void InitStyles()
        {
            if (text != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Arial" }, 20);
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 19, alignment = TextAnchor.MiddleLeft };
            text.normal.textColor = Color.white;
            small = new GUIStyle(text) { fontSize = 15 };
            title = new GUIStyle(text) { fontSize = 37, fontStyle = FontStyle.Bold };
            center = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter, fontSize = 23 };
            number = new GUIStyle(center) { fontSize = 23, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 19, fixedHeight = 48 };
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

            Box(new Rect(30, 25, 330, 122), Panel);
            Box(new Rect(30, 25, 3, 122), DemoArt.Jade);
            Label(new Rect(50, 35, 280, 32), "霜庭  /  白发剑客", text, Color.white);
            Label(new Rect(50, 72, 90, 23), "气血", small, Muted);
            Label(new Rect(220, 70, 112, 25), player.Health + " / " + player.maxHealth, small, DemoArt.Jade);
            Box(new Rect(50, 106, 282, 8), new Color(0.12f, 0.21f, 0.23f));
            Box(new Rect(50, 106, 282 * Mathf.Clamp01(player.Health / (float)player.maxHealth), 8), DemoArt.Jade);

            Box(new Rect(width - 310, 25, 280, 122), Panel);
            Label(new Rect(width - 288, 38, 242, 28), arena.practiceMode ? "试刀模式  /  TRAINING" : "第 " + arena.Wave.ToString("00") + " 波  /  SURVIVAL", text, arena.practiceMode ? DemoArt.Gold : DemoArt.Red);
            Label(new Rect(width - 288, 75, 242, 25), "场上 " + arena.Enemies.Count + "   ·   击破 " + arena.Kills, small, Muted);
            Label(new Rect(width - 288, 108, 242, 22), "T  切换练习 / 波次", small, Muted);

            Label(new Rect(40, height - 125, 450, 42), "01  /  试刀庭院", title, new Color(0.79f, 0.86f, 0.82f));
            Label(new Rect(43, height - 153, 650, 25), "右键按住蓄力 / 松手剑气    提前松手取消    蓄力时无法移动", small, Muted);
            Label(new Rect(43, height - 74, 650, 25), "WASD 移动    鼠标瞄准    按住左键 连斩（期间减速50%）", small, Muted);
            Label(new Rect(43, height - 46, 650, 25), "空格 冲刺 / 打断攻击    T 切换模式    R 重来    Esc 暂停    F1 调参", small, Muted);

            Box(new Rect(width - 330, height - 132, 300, 105), Panel);
            Label(new Rect(width - 308, height - 125, 270, 32), player.IsComboRecovering ? "连招收势 · 空格可打断" : "流风三式", text, Color.white);
            string[] actionNames = { "劈", "劈", "大劈" };
            for (int i = 0; i < 3; i++)
            {
                bool active = player.IsAttacking && player.ComboIndex == i;
                string action = i < player.strikes.Length ? actionNames[(int)player.strikes[i].action] : "—";
                Label(new Rect(width - 308 + i * 88, height - 87, 90, 28), (i + 1) + " · " + action, small, active ? DemoArt.Gold : Muted);
                Box(new Rect(width - 308 + i * 88, height - 51, 70, 3), active ? DemoArt.Gold : new Color(0.15f, 0.25f, 0.28f));
            }
            Box(new Rect(width / 2 - 105, height - 58, 210, 31), Panel);
            Box(new Rect(width / 2 - 105, height - 28, 210 * player.DashReady, 3), DemoArt.Jade);
            Label(new Rect(width / 2 - 105, height - 58, 210, 30), player.DashReady >= 1 ? "空格  /  冲刺就绪" : "冲刺冷却中", small, player.DashReady >= 1 ? DemoArt.Jade : Muted);
            if (player.IsHeavyBusy)
            {
                Box(new Rect(width / 2 - 150, height - 122, 300, 48), Panel);
                Color chargeColor = player.HeavyReady ? DemoArt.Gold : new Color(0.45f, 0.85f, 1);
                string chargeText = player.IsHeavyRecovering ? (player.IsHeavyReleasePaused ? "蓄势待发 · 空格可打断"
                    : player.IsHeavySweeping ? "重击横扫 · 空格可打断" : "重击收势 · 空格可打断") : player.HeavyReady
                    ? "蓄力完成 · 松开右键释放" : "蓄力 " + Mathf.FloorToInt(player.ChargeProgress * 100) + "% · 松手取消";
                Label(new Rect(width / 2 - 145, height - 118, 290, 32), chargeText, small, chargeColor);
                Box(new Rect(width / 2 - 140, height - 83, 280, 3), new Color(0.12f, 0.21f, 0.23f));
                if (player.IsCharging) Box(new Rect(width / 2 - 140, height - 83, 280 * player.ChargeProgress, 3), chargeColor);
            }

            if (!arena.Paused && !arena.Defeated)
            {
                if (arena.Banner.Length > 0)
                {
                    Box(new Rect(width / 2 - 220, 175, 440, 52), Panel);
                    Label(new Rect(width / 2 - 220, 175, 440, 52), arena.Banner, center, DemoArt.Gold);
                }
                DrawWorldLabels(scale);
                DrawPointer(scale);
            }
            if (arena.Paused || arena.Defeated)
            {
                Box(new Rect(0, 0, width, height), new Color(0.015f, 0.028f, 0.042f, 0.82f));
                float x = width / 2 - 230, y = height / 2 - 165;
                Box(new Rect(x, y, 460, 330), Panel);
                Label(new Rect(x, y + 25, 460, 60), arena.Defeated ? "胜负乃兵家常事" : "暂歇片刻", new GUIStyle(title) { alignment = TextAnchor.MiddleCenter }, Color.white);
                Label(new Rect(x, y + 100, 460, 35), arena.Defeated ? "坚持 " + arena.Wave + " 波  ·  击破 " + arena.Kills : "Esc 返回庭院", center, Muted);
                if (GUI.Button(new Rect(x + 65, y + 165, 330, 48), arena.Defeated ? "重新试刀  [R]" : "继续", button))
                { if (arena.Defeated) arena.Restart(); else arena.TogglePause(); }
                if (GUI.Button(new Rect(x + 65, y + 232, 330, 48), arena.practiceMode ? "进入波次挑战  [T]" : "返回木桩练习  [T]", button))
                { arena.practiceMode = !arena.practiceMode; arena.Restart(); }
            }
            GUI.matrix = previous;
        }

        void DrawWorldLabels(float scale)
        {
            foreach (var item in arena.Effects.Texts)
            {
                float t = Mathf.Clamp01((Time.time - item.born) / 0.65f);
                Vector3 p = arena.WorldCamera.WorldToScreenPoint(arena.View.LiftPoint(item.position, arena.View.LabelHeight + t * 0.8f));
                var c = item.color;
                c.a *= 1 - t;
                Label(new Rect(p.x / scale - 40, (Screen.height - p.y) / scale - 22, 80, 40), item.text, number, c);
            }
            if (!arena.practiceMode) return;
            foreach (var enemy in arena.Enemies)
            {
                if (enemy == null) continue;
                float labelHeight = enemy.GetComponent<ObliqueActor>().LabelHeight;
                Vector3 p = arena.WorldCamera.WorldToScreenPoint(arena.View.LiftPoint(enemy.transform.position, labelHeight));
                Label(new Rect(p.x / scale - 70, (Screen.height - p.y) / scale - 12, 140, 24), "木桩 · " + enemy.Health, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }, DemoArt.Gold);
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

        void OnDestroy() { if (font != null) Destroy(font); }
    }
}
