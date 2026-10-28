using System;
using SlashDemo;
using UnityEngine;

static class TechniqueChecks
{
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    static void Near(float value, float expected, string message) => Check(Math.Abs(value - expected) < 0.0001f, message);
    static void Main()
    {
        var defaults = new SlashTuningSettings();
        defaults.Validate(new SlashTuningSettings());
        var finisher = defaults.flowingCloud.finisherStrike;
        Check(finisher.damage == 80 && defaults.strikes[2].damage == 40,
            "full-stack finisher doubles damage without changing the normal third strike");
        foreach (var field in typeof(SlashPlayer.Strike).GetFields())
            if (field.Name != "damage")
                Check(Equals(field.GetValue(finisher), field.GetValue(defaults.strikes[2])),
                    "finisher initially preserves third-strike " + field.Name);
        var migrated = new SlashTuningSettings();
        migrated.strikes[2].damage = 73; migrated.strikes[2].reach = 3.1f;
        migrated.strikes[2].duration = 0.64f;
        migrated.Validate(defaults);
        Check(migrated.flowingCloud.finisherStrike.damage == 146
            && migrated.flowingCloud.finisherStrike.reach == 3.1f
            && migrated.flowingCloud.finisherStrike.duration == 0.64f,
            "missing finisher settings migrate from the saved third strike with doubled damage");
        var cloned = migrated.Clone();
        cloned.flowingCloud.finisherStrike.damage = 117;
        cloned.flowingCloud.finisherStrike.reach = 1.7f;
        Check(migrated.flowingCloud.finisherStrike.damage == 146 && migrated.flowingCloud.finisherStrike.reach == 3.1f,
            "tuning snapshots deeply copy finisher data");
        cloned.strikes[2].damage = 9; cloned.strikes[2].reach = 4;
        cloned.Validate(defaults);
        Check(cloned.flowingCloud.finisherStrike.damage == 117 && cloned.flowingCloud.finisherStrike.reach == 1.7f,
            "later normal tuning cannot overwrite independent finisher settings or double damage again");
        cloned.flowingCloud.finisherStrike.damage = 999;
        cloned.flowingCloud.finisherStrike.duration = float.NaN;
        cloned.Validate(defaults);
        Check(cloned.flowingCloud.finisherStrike.damage == 400
            && !float.IsNaN(cloned.flowingCloud.finisherStrike.duration), "finisher tuning validates damage and invalid timing");
        var combo = new FlowingCloudCombo();
        var rebuild = new FlowingCloudRebuildReward();
        foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
        {
            rebuild.Clear();
            Near(rebuild.OnStacksIncreased(4, 5, realm, 5), 0, "full layers without a completed active skill grant no cooldown refund");
            rebuild.Arm(realm);
            bool unlocked = realm >= CultivationRealm.FoundationEstablishment;
            Check(rebuild.Pending == unlocked, "completed active skill arms the reward only from Foundation onward");
            Near(rebuild.OnStacksIncreased(3, 4, realm, 5), 0, "partial rebuild cannot refund cooldown");
            Near(rebuild.OnStacksIncreased(5, 5, realm, 5), 0, "refreshing layers already at the cap does not count as reaching full");
            Near(rebuild.OnStacksIncreased(4, 5, realm, 5), unlocked ? 1 : 0, "the first full rebuild refunds exactly one second");
            Check(!rebuild.Pending, "first full rebuild consumes the one-time refund");
            Near(rebuild.OnStacksIncreased(2, 3, realm, 5), 0, "dropping layers cannot rearm a spent reward");
            Near(rebuild.OnStacksIncreased(4, 5, realm, 5), 0, "reaching full a second time cannot refund again");
            rebuild.Arm(realm);
            Near(rebuild.OnStacksIncreased(3, 5, realm, 5), unlocked ? 1 : 0, "a two-layer whirlwind can trigger the next completed cast's reward");
            rebuild.Arm(realm); rebuild.Clear();
            Near(rebuild.OnStacksIncreased(4, 5, realm, 5), 0, "cancelling the next cast removes any stale refund opportunity");
        }
        rebuild.Arm(CultivationRealm.FoundationEstablishment);
        Near(rebuild.OnStacksIncreased(2, 3, CultivationRealm.FoundationEstablishment, 3), 1,
            "debug-tuned stack caps use the configured maximum");
        foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
        {
            bool pursuit = realm >= CultivationRealm.GoldenCore;
            combo.Clear();
            Check(combo.NextSegment(-1, true, realm) == 0, "fresh attack starts at first segment");
            Check(combo.NextSegment(0, false, realm) == 1, "first connects to second");
            Check(combo.NextSegment(1, false, realm) == 2, "second connects to third");
            Check(combo.NextSegment(2, false, realm) == 0, "whirlwind cannot trigger before the third segment hits");
            combo.BeginStrike(); combo.RegisterHit(2, 1, realm);
            Check(combo.NextSegment(2, false, realm) == (pursuit ? 3 : 0), "Foundation restarts at one while Golden Core retains pursuit");
            Check(combo.NextSegment(2, true, realm) == 0, "expired chain starts anew at every realm");
            Check(combo.NextSegment(3, false, realm) == 0, "pursuit restarts at the first segment");
            combo.Clear();
            for (int strike = 0; strike < 9; strike++)
            {
                combo.BeginStrike();
                int expectedStack = Math.Min(strike + 1, 5);
                Near(combo.DamageMultiplier(realm), 1 + Math.Min(strike, 5) * 0.08f + (strike >= 5 ? 0.20f : 0),
                    "damage uses existing layers and grants a further twenty percent only at five stacks");
                Check(combo.RegisterHit(strike % 3, strike, realm), "first contact registers");
                Check(!combo.RegisterHit(strike % 3, strike + 0.01f, realm), "additional enemies cannot grant extra stacks");
                Check(combo.Stacks == expectedStack, "only one stack is gained per segment");
                combo.FinishStrike(strike + 0.1f);
            }
            combo.BeginStrike(); combo.FinishStrike(8.8f);
            Check(combo.Stacks == 5 && combo.StackUntil == 10 && !combo.StrikeHit,
                "a missed segment preserves live layers and their original deadline without confirming a hit");
            combo.BeginStrike(); combo.RegisterHit(2, 10, realm); combo.Clear();
            Check(combo.Stacks == 0 && !combo.StrikeHit, "interruption removes all passive state");
            Near(combo.AttackSpeedMultiplier(realm), 1, "interruption removes attack speed with the layers");
            for (int stack = 0; stack <= 5; stack++)
            {
                Near(combo.AttackSpeedMultiplier(realm), realm >= CultivationRealm.FoundationEstablishment ? 1 + stack * 0.05f : 1,
                    "each live stack grants exactly five percent speed starting at Foundation Establishment");
                Near(combo.DamageMultiplier(realm), 1 + stack * 0.08f + (stack == 5 ? 0.20f : 0),
                    "all realms receive additive sixty percent total normal damage only at five layers");
                if (stack < 5) { combo.BeginStrike(); combo.RegisterHit(0, 20 + stack * 0.1f, realm); }
            }
            combo.ResetSequence();
            Near(combo.AttackSpeedMultiplier(realm), realm >= CultivationRealm.FoundationEstablishment ? 1.25f : 1,
                "normal sequence reset preserves live stack speed");
            combo.ExpireStacks(22.4f);
            Near(combo.AttackSpeedMultiplier(realm), 1, "attack speed expires with the two-second buff");
            Near(combo.DamageMultiplier(realm), 1, "expiring five layers removes both per-layer and full-stack damage bonuses");
        }
        for (int realm = 0; realm < 4; realm++)
            Check(FlowingCloudRules.ActiveDamage((CultivationRealm)realm) == 28 + realm * 6, "active skill improves at each realm");
        foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
            for (int initial = 0; initial <= 5; initial++)
            {
                combo.Clear();
                for (int i = 0; i < initial; i++) { combo.BeginStrike(); combo.RegisterHit(0, 10, realm); }
                float deadline = combo.StackUntil;
                combo.ApplyDodge(10.5f);
                int remaining = Math.Max(0, initial - 2);
                Check(combo.Stacks == remaining && !combo.StrikeHit, "dodge subtracts two layers without going negative and resets hit confirmation");
                Check(remaining == 0 ? float.IsNegativeInfinity(combo.StackUntil) : combo.StackUntil == deadline,
                    "dodge preserves the original deadline of surviving layers and clears an empty buff timer");
                Near(combo.DamageMultiplier(realm), 1 + remaining * 0.08f, "dodge immediately reduces damage bonus to the remaining layers");
                Near(combo.AttackSpeedMultiplier(realm), realm >= CultivationRealm.FoundationEstablishment ? 1 + remaining * 0.05f : 1,
                    "dodge immediately reduces Foundation speed to the remaining layers");
                combo.ApplyDodge(12);
                Check(combo.Stacks == 0 && combo.StackTimeLeft(12) == 0, "dodging at expiry never preserves or revives expired layers");
            }
        var rush = new SlashTuningSettings.FlowingCloudSettings();
        foreach (CultivationRealm realm in new[] { CultivationRealm.GoldenCore, CultivationRealm.NascentSoul })
            for (int initial = 0; initial <= 5; initial++)
            {
                combo.Clear();
                for (int i = 0; i < initial; i++) { combo.BeginStrike(); combo.RegisterHit(0, 1, realm); }
                combo.BeginStrike();
                Check(combo.RegisterHit(3, 1.1f, realm) && combo.Stacks == Math.Min(initial + 2, 5), "whirlwind grants exactly two layers on its first contact");
                Check(!combo.RegisterHit(3, 1.2f, realm) && combo.Stacks == Math.Min(initial + 2, 5), "additional whirlwind targets cannot grant more layers");
                combo.ResetSequence();
                combo.RegisterSwordTideHit(1.3f, realm);
                Check(combo.Stacks == Math.Min(initial + 3, 5) && !combo.StrikeHit, "sword tide grants one independent layer without confirming the current sword strike");
                Near(combo.StackTimeLeft(1.3f), 2, "sword tide resets the full stack timer");
            }
        Near(rush.RushDistance(0), 1.4f * 2.4f, "zero-stack rush uses the user's saved base distance and multiplier");
        foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
            for (int stacks = 0; stacks <= 5; stacks++)
            {
                combo.Clear();
                for (int i = 0; i < stacks; i++) { combo.BeginStrike(); combo.RegisterHit(2, 10 + i * 0.1f, realm); }
                combo.ExpireStacks(10.6f);
                float castMultiplier = combo.DamageMultiplier(realm);
                Near(castMultiplier, 1 + stacks * 0.08f + (stacks == 5 ? 0.2f : 0),
                    "Q snapshots the consumed layers' normal damage multiplier including the five-stack bonus");
                Check(combo.ConsumeStacks(10.6f) == stacks, "rush snapshots every live damage-bonus layer");
                Check(combo.Stacks == 0 && combo.StackTimeLeft(10.6f) == 0 && combo.AttackSpeedMultiplier(realm) == 1
                    && combo.DamageMultiplier(realm) == 1,
                    "Q consumption clears layers, duration and normal attack speed");
                Check(combo.ConsumeStacks(10.6f) == 0, "the same layers cannot be spent twice");
                Check(rush.ActiveDamage(realm, stacks) == 28 + (int)realm * 6 + stacks * 8,
                    "every consumed stack adds flat base damage before the snapshotted normal damage multiplier");
                Near(rush.RushDistance(stacks), (1.4f + 0.25f * stacks) * 2.4f, "all layers use the synchronized saved distance settings");
                Near(rush.ActiveWidth(stacks), 1.1f + 0.18f * stacks, "every stack widens the sword light");
            }
        combo.BeginStrike(); combo.RegisterHit(0, 20, CultivationRealm.QiRefining);
        Check(combo.ConsumeStacks(22) == 0, "layers expiring exactly at cast time cannot power a rush");
        Near(combo.DamageMultiplier(CultivationRealm.QiRefining), 1, "expired layers grant Q no normal damage bonus");
        Check(TechniqueCatalog.FlowingCloud.activeEffect.EndsWith("该技能伤害视为普攻伤害。"),
            "active description states normal attack damage classification");
        Near(rush.RushDistance(100), rush.RushDistance(5), "rush scaling is capped at five layers");
        Near(rush.ActiveWidth(-1), rush.ActiveWidth(0), "invalid negative layers cannot narrow the base attack");
        foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
        {
            combo.Clear(); combo.BeginStrike(); combo.RegisterHit(0, 10, realm);
            Near(combo.StackTimeLeft(10), 2, "a confirmed combo hit grants exactly two seconds of stacks");
            combo.ResetSequence(); combo.ExpireStacks(11.99f);
            Check(combo.Stacks == 1, "restarting the normal combo sequence does not shorten the buff");
            combo.BeginStrike(); combo.RegisterHit(1, 11.99f, realm);
            Near(combo.StackTimeLeft(11.99f), 2, "the next hit resets the full two-second duration");
            Check(!combo.RegisterHit(1, 12.8f, realm), "extra targets in the same segment do not refresh the buff");
            Near(combo.StackUntil, 13.99f, "multi-target contacts preserve the first contact deadline");
            combo.ExpireStacks(13.98f); Check(combo.Stacks == 2, "stacks remain until the deadline");
            combo.ExpireStacks(13.99f); Check(combo.Stacks == 0 && combo.StackTimeLeft(13.99f) == 0, "all stacks expire exactly at two seconds");
            combo.RefreshStacks(14); Check(combo.Stacks == 0, "a refresh cannot revive expired stacks");
            combo.Clear();
            for (int i = 0; i < 5; i++) { combo.BeginStrike(); combo.RegisterHit(0, i * 0.1f, realm); }
            combo.BeginStrike(); combo.RegisterHit(0, 2.3f, realm);
            Check(combo.Stacks == 5, "another hit at the stack cap preserves the capped bonus");
            Near(combo.StackTimeLeft(2.3f), 2, "hits still refresh duration at the stack cap");
            combo.RefreshStacks(4.2f);
            Check(combo.Stacks == 5, "an explicit timer refresh does not add layers");
            Near(combo.StackTimeLeft(4.2f), 2, "an explicit timer refresh grants the full duration");
            combo.Clear();
            Check(combo.StackTimeLeft(4.2f) == 0, "interruption clears the timer with all layers");
        }
        Check(TechniqueCatalog.All.Count > 0 && TechniqueCatalog.All[0] == TechniqueCatalog.FlowingCloud,
            "implemented technique is registered for the codex");
        var settings = new SlashTuningSettings.FlowingCloudSettings { attackSpeedPerStack = 0.07f, stackBonus = 0.12f, maxStacks = 3 };
        combo.Clear(); combo.Configure(settings);
        for (int i = 0; i < 7; i++) { combo.BeginStrike(); combo.RegisterHit(0, 0, CultivationRealm.FoundationEstablishment); }
        Check(combo.Stacks == 3, "tuned stack cap is applied");
        Near(combo.DamageMultiplier(CultivationRealm.FoundationEstablishment), 1.36f, "tuned stack bonus is applied");
        combo.BeginStrike(); combo.RegisterHit(2, 1, CultivationRealm.FoundationEstablishment);
        Check(combo.NextSegment(2, false, CultivationRealm.FoundationEstablishment) == 0, "Foundation no longer skips the first normal segment");
        Near(combo.AttackSpeedMultiplier(CultivationRealm.FoundationEstablishment), 1.21f, "tuned per-stack speed is applied");
        settings.maxStacks = 1; combo.Configure(settings);
        Check(combo.Stacks == 1, "reducing the tuned cap truncates existing stacks");
        combo.Clear(); combo.BeginStrike(); combo.FinishStrike(1);
        Check(combo.NextSegment(2, false, CultivationRealm.NascentSoul) == 0, "a missed third segment blocks whirlwind even at Nascent Soul");
        combo = new FlowingCloudCombo();
        foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
            for (int initial = 0; initial <= 5; initial++)
            {
                combo.Clear();
                for (int i = 0; i < initial; i++) { combo.BeginStrike(); combo.RegisterHit(0, 10, realm); }
                float deadline = combo.StackUntil;
                foreach (float endedAt in new[] { 10.4f, 10.8f, 11.2f, 11.6f })
                {
                    combo.BeginStrike(); combo.FinishStrike(endedAt);
                    Check(combo.Stacks == initial && combo.StackUntil == deadline && !combo.StrikeHit,
                        "consecutive missed attacks never change existing layers or extend their deadline");
                    Near(combo.StackTimeLeft(endedAt), initial > 0 ? 12 - endedAt : 0,
                        "a missed attack keeps using the last confirmed hit's remaining lifetime");
                }
                Check(combo.NextSegment(2, false, realm) == 0, "preserved layers cannot replace the hit prerequisite for whirlwind");
                combo.FinishStrike(12);
                Check(combo.Stacks == 0 && combo.StackTimeLeft(12) == 0, "even a missed attack ending at the deadline cannot preserve expired layers");
            }
        var spin = new SlashPlayer.Strike(48, 2.5f, 210, 0.3f, 7, SwordAction.SpinCleave, 0.16f, 0.46f, 0, 0.12f);
        float start = SwordMotion.SwingStart(spin), end = spin.windupRatio + spin.activeRatio;
        Near(SwordMotion.Evaluate(spin, end).angle - SwordMotion.Evaluate(spin, start).angle, 210, "the physical sword sweeps only 210 degrees");
        for (int frame = 0; frame <= 240; frame++)
        {
            var pose = SwordMotion.Evaluate(spin, frame / 240f);
            Check(pose.grip.x >= -0.1001f && pose.grip.x <= 0.3901f && pose.grip.y >= -0.0601f && pose.grip.y <= 0.1201f,
                "windup, swing and recovery keep the grip in the front arc");
            Check(Math.Abs(pose.torsoTurn) <= 11.001f && Math.Abs(pose.shoulderTurn) <= 13.751f, "anatomical torso and shoulder turns remain bounded");
        }
        Near(SwordMotion.WhirlwindRadius(0, spin.reach), spin.reach, "damage covers the complete visible circle from the emission frame");
        Near(SwordMotion.WhirlwindRadius(0.25f, spin.reach), spin.reach, "the effect never waits for delayed radial expansion");
        foreach (float distance in new[] { 0.3f, 1.5f, spin.reach })
            Check(SwordMotion.WhirlwindHit(Vector2.zero, -1, 0, spin.reach, Vector2.left * distance, 0.08f),
                "near, middle and edge enemies receive damage at the exact sword-light emission time");
        foreach (int frames in new[] { 1, 12, 30, 120 })
            for (int angle = 0; angle < 360; angle += 5)
                foreach (float distance in new[] { 0.3f, 1.5f, 2.4f })
                {
                    Vector2 target = new Vector2((float)Math.Cos(angle * Math.PI / 180), (float)Math.Sin(angle * Math.PI / 180)) * distance;
                    bool hit = false;
                    for (int frame = 0; frame < frames && !hit; frame++)
                        hit = SwordMotion.WhirlwindHit(Vector2.zero, frame == 0 ? -1 : frame * 0.28f / frames,
                            (frame + 1f) * 0.28f / frames, spin.reach, target, 0.08f);
                    Check(hit, "the first visible frame hits every angle across frame rates");
                }
        Check(!SwordMotion.WhirlwindHit(Vector2.zero, -1, 0.28f, spin.reach, Vector2.left * 3, 0.1f), "ring excludes targets beyond its reach");
        Check(!SwordMotion.WhirlwindHit(Vector2.zero, -1, -0.01f, spin.reach, Vector2.left, 0.1f), "ring cannot deal damage before emission");
        Check(!SwordMotion.WhirlwindHit(Vector2.zero, 0, 0.01f, spin.reach, Vector2.left, 0.1f),
            "an enemy arriving after the emission frame cannot receive delayed sword-light damage");
        Check(!SwordMotion.WhirlwindHit(Vector2.zero, 0.28f, 0.32f, spin.reach, Vector2.left * 2.5f, 0.1f), "late inner-ring visuals cannot deal extra damage");
        Check(!SwordMotion.SweptHit(spin, 0, 1, Vector2.zero, Vector2.zero, Vector2.right,
            Vector2.left * 1.5f, 0.1f, out _), "the sword itself does not swing through rear targets");
        Check(!SwordMotion.SweptHit(spin, 0, start - 0.001f, Vector2.zero, Vector2.zero, Vector2.right,
            Vector2.left, 0.1f, out _), "spin windup cannot deal damage");
        Console.WriteLine("TECHNIQUE_MATH_PASS: " + checks + " assertions");
    }
}
