using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    enum AlchemyStat
    {
        MaxHealth, Damage, Reduction, Move, Healing, AttackSpeed, ChargeReduction, DotReduction, HitRecovery,
        TechniqueCooldown, CritChance, DashCooldown, TechniqueDamage, HeavyDamage, DashDistance, UltimateDamage,
        UltimateCooldown, CritDamage, NormalDamage, NormalReach, WaveDamage, WaveDistance, Count
    }

    enum PlayerAttackKind { Normal, Technique, Heavy, Ultimate, SwordTide, Secondary }

    sealed class PillEffects
    {
        readonly Dictionary<AlchemyEntry, double> startsAt = new Dictionary<AlchemyEntry, double>();
        readonly List<AlchemyEntry> expired = new List<AlchemyEntry>();
        readonly float[] stats = new float[(int)AlchemyStat.Count];
        double lastCombatTime;

        public IReadOnlyDictionary<AlchemyEntry, double> Active => startsAt;
        public float this[AlchemyStat stat] => stats[(int)stat];

        public void Activate(AlchemyEntry entry, double combatTime)
        {
            startsAt[entry] = combatTime;
            Recalculate();
        }

        public int Advance(double combatTime, int maxHealth)
        {
            if (combatTime <= lastCombatTime) return 0;
            int recovery = 0;
            expired.Clear();
            foreach (var pill in startsAt)
            {
                int duration = pill.Key.CombatDurationSeconds;
                double start = pill.Value;
                if (combatTime >= start + duration) expired.Add(pill.Key);

                float healPerTick = PillCatalog.Definition(pill.Key).HealPerTick;
                if (healPerTick <= 0) continue;
                int due = RecoveryTicksAt(combatTime, start, duration) - RecoveryTicksAt(lastCombatTime, start, duration);
                recovery += due * Mathf.CeilToInt(maxHealth * healPerTick);
            }
            lastCombatTime = combatTime;
            foreach (var entry in expired) startsAt.Remove(entry);
            if (expired.Count > 0) Recalculate();
            return recovery;
        }

        static int RecoveryTicksAt(double combatTime, double start, int duration)
            => combatTime >= start + duration ? duration / PillCatalog.RecoveryIntervalSeconds
                : (int)(Math.Max(0, combatTime - start) / PillCatalog.RecoveryIntervalSeconds);

        void Recalculate()
        {
            Array.Clear(stats, 0, stats.Length);
            foreach (var entry in startsAt.Keys)
                foreach (var change in PillCatalog.Definition(entry).Stats) stats[(int)change.Stat] += change.Amount;
        }
    }
}
