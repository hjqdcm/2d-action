using System;
using SlashDemo;
using UnityEngine;

static class IronSwordChecks
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < 0.0001f, message);
    static void Main()
    {
        var iron = new SlashTuningSettings.IronSwordSettings();
        var basis = new SlashTuningSettings.HeavyAttackSettings();
        float[] ranges = { 0.60f, 0.67f, 0.73f, 0.80f, 0.87f, 0.93f, 1 };
        float[] multipliers = { 0.20f, 0.335f, 0.470f, 0.605f, 0.740f, 0.875f, 1.3635f };
        int[] damages = { 11, 18, 26, 33, 41, 48, 75 };
        int[] protectedDamages = { 14, 24, 34, 43, 53, 63, 97 };
        foreach (CultivationRealm realm in Enum.GetValues(typeof(CultivationRealm)))
            for (int stacks = 0; stacks <= 6; stacks++)
            {
                Near(IronSwordRules.DamageMultiplier(stacks), multipliers[stacks], "linear damage scaling grants the extra multiplier only at six layers");
                Near(IronSwordRules.RangeMultiplier(stacks), ranges[stacks], "seven range tiers match the table exactly");
                Near(IronSwordRules.Recovery(stacks), 0.24f, "all Gangqi tiers have fixed recovery");
                Check(IronSwordRules.Stacks(stacks / 6f) == stacks, "layer boundaries follow charge progress");
                if (stacks > 0) Check(IronSwordRules.Stacks(stacks / 6f - 0.0001f) == stacks - 1, "layers cannot unlock before their threshold");
                var wave = iron.Release(basis, stacks, realm);
                Check(wave.damage == damages[stacks], "partial release scales base damage once using the literal table multipliers");
                Near(wave.distance, 6 * ranges[stacks], "range scales flight distance");
                Near(wave.width, 3.4f * ranges[stacks] * (realm >= CultivationRealm.FoundationEstablishment && stacks >= 5 ? 1.3f : 1),
                    "Foundation width bonus unlocks only at five layers");
                Near(wave.recoveryTime, 0.24f, "Iron Sword release uses its fixed recovery");
                Check(basis.damage == 55 && basis.width == 3.4f && basis.distance == 6 && basis.recoveryTime == 0.25f,
                    "Iron Sword scaling never mutates other techniques' base damage or recovery");
                var protectedWave = iron.Release(basis, stacks, realm, true);
                Check(protectedWave.damage == protectedDamages[stacks], "protection adds thirty percent heavy damage with one final rounding");
                Check(protectedWave.width == wave.width && protectedWave.distance == wave.distance
                    && protectedWave.recoveryTime == wave.recoveryTime, "protection heavy bonus changes damage only");
            }
        Near(iron.ChargeSpeed(CultivationRealm.QiRefining, false), 1, "Qi refining uses baseline charge speed");
        Near(iron.ChargeSpeed(CultivationRealm.FoundationEstablishment, false), 1.20f, "Foundation charges 20 percent faster");
        Near(iron.ChargeSpeed(CultivationRealm.QiRefining, true), 1.3f, "Iron Body speeds an ongoing charge thirty percent");
        Near(iron.ChargeSpeed(CultivationRealm.NascentSoul, true), 1.50f, "charge speed bonuses add");
        Near(iron.bodyDuration, 3, "protection lasts three seconds");
        Near(iron.bodyDamageReduction, 0.5f, "protection reduces damage by fifty percent");
        Near(iron.bodyCooldown, 10, "protection retains its existing cooldown");
        Near(0.8f / iron.ChargeSpeed(CultivationRealm.FoundationEstablishment, false), 0.6666667f, "Foundation full charge is approximately 0.67 seconds");
        var charge = new ChargedAttackState();
        foreach (float age in new[] { 0f, 0.1f, 0.4f, 0.799f, 0.8f, 1f })
        {
            charge.Begin(0.8f, 0.1f); charge.Advance(age);
            Check(charge.Release(true), "Iron Sword always fires on release, including zero charge");
            Check(!charge.Release(true), "one release never fires twice"); charge.Cancel();
            Check(!charge.Release(true), "dash cancellation cannot leave a pending release");
        }
        charge.Begin(0.8f, 0.25f); charge.Advance(0.4f);
        Check(!charge.Release(), "Flowing Cloud retains its existing full-charge heavy attack");
        Check(TechniqueCatalog.All.Count == 2 && TechniqueCatalog.Find("black-iron-sword") == TechniqueCatalog.IronSword,
            "both exclusive techniques appear in the codex");
        Check(TechniqueCatalog.Find("missing") == TechniqueCatalog.FlowingCloud, "unknown saved selections fall back to Flowing Cloud");
        var settings = new SlashTuningSettings(); settings.Validate(new SlashTuningSettings());
        var copy = settings.Clone(); copy.ironSword.scarTickDamageRatio = 0.25f;
        Check(settings.ironSword.scarTickDamageRatio == 0.10f, "Iron Sword settings snapshot independently");
        copy.ironSword = null; copy.Validate(settings);
        Near(copy.ironSword.bodyCooldown, 10, "old saves receive Iron Sword defaults");
        copy.ironSword.bodyHeavyBonus = float.NaN; copy.Validate(settings);
        Near(copy.ironSword.bodyHeavyBonus, 0.3f, "invalid heavy bonus uses the protection default");
        copy.ironSword.bodyHeavyBonus = 0; copy.Validate(settings);
        Near(copy.ironSword.bodyHeavyBonus, 0, "debug settings may explicitly disable protection heavy bonus");
        Console.WriteLine("IRON_SWORD_MATH_PASS: " + checks + " assertions");
    }
}
