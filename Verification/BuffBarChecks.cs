using System;
using SlashDemo;
using UnityEngine;

static class BuffBarChecks
{
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    static void Main()
    {
        var bar = new CombatBuffBar();
        bar.AddOrUpdate(new CombatBuffView("tide", "叠浪", 1));
        bar.AddOrUpdate(new CombatBuffView("ward", "护体", 2));
        bar.AddOrUpdate(new CombatBuffView("tide", "叠浪", 5));
        Check(bar.Items.Count == 2 && bar.Items[0].id == "tide" && bar.Items[0].stacks == 5
            && bar.Items[1].id == "ward", "updating one buff cannot duplicate it or replace another buff's slot");
        bar.Remove("tide");
        bar.AddOrUpdate(new CombatBuffView("haste", "疾行", 1));
        Check(bar.Items.Count == 2 && bar.Items[0].id == "ward" && bar.Items[1].id == "haste",
            "removal compacts the list before a new buff receives a free slot");
        bar.AddOrUpdate(new CombatBuffView("ward", "护体", 0));
        Check(bar.Items.Count == 1 && bar.Items[0].id == "haste", "zero layers remove only the matching buff");
        bar.AddOrUpdate(new CombatBuffView("inactive", "无效", -1));
        Check(bar.Items.Count == 1, "nonpositive layers cannot create visible buff cells");
        bar.Clear();
        Check(bar.Items.Count == 0, "a fresh collection cannot retain expired or previous-player buffs");
        var health = new Rect(30, 25, 330, 106);
        foreach (float width in new[] { 48f, 103f, 104f, 160f, 282f })
        {
            var area = new Rect(50, health.yMax + 10, width, 0);
            for (int i = 0; i < 32; i++)
            {
                Rect slot = BuffBarLayout.Slot(i, area);
                Check(slot.width == slot.height && slot.width == 48, "each buff uses one square cell");
                Check(slot.x >= area.x && slot.xMax <= area.xMax && slot.y >= health.yMax + 10,
                    "buff rows remain below the blood panel and inside the available width");
                Rect badge = BuffBarLayout.StackBadge(slot);
                Check(badge.x > slot.center.x && badge.y < slot.center.y && badge.xMax <= slot.xMax && badge.yMax <= slot.yMax,
                    "layer badge remains inside the top-right corner of its own buff cell");
                for (int j = 0; j < i; j++)
                    Check(!slot.Overlaps(BuffBarLayout.Slot(j, area)), "different buffs never share or overlap a slot across wrapped rows");
            }
        }
        var normal = new Rect(50, 141, 282, 0);
        Check(BuffBarLayout.Slot(4, normal).y == 141 && BuffBarLayout.Slot(5, normal).x == 50
            && BuffBarLayout.Slot(5, normal).y == 197, "the sixth buff wraps to the next row below the five-cell health-bar width");
        foreach (var viewport in new[] { new Rect(0, 0, 1600, 900), new Rect(0, 0, 800, 600), new Rect(0, 0, 360, 400) })
            foreach (var slot in new[] { new Rect(50, 141, 48, 48), new Rect(viewport.xMax - 60, 20, 48, 48),
                new Rect(viewport.xMax - 60, viewport.yMax - 60, 48, 48) })
            {
                Rect tooltip = BuffBarLayout.Tooltip(slot, new Vector2(340, 170), viewport);
                Check(tooltip.xMin >= viewport.xMin + 8 && tooltip.yMin >= viewport.yMin + 8
                    && tooltip.xMax <= viewport.xMax - 8 && tooltip.yMax <= viewport.yMax - 8,
                    "hover tooltip flips and clamps inside the viewport near every screen edge");
            }
        Console.WriteLine("BUFF_BAR_PASS: " + checks + " assertions");
    }
}
