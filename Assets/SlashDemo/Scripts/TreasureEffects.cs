using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    enum TreasureTrigger
    {
        None, CloudRebuild, IronCharge, DashRefund, UltimateRefund, LowHealthShield, FirstHitSlow,
        OverhealShield, HitGuard, CloudFinisher, EarlyScarBurst, UltimateRequirement, TechniqueHeavyLink,
        FinisherWave, LethalGuard, MultiHitBurst, TargetMark, MovementHit
    }

    sealed class TreasureAttack
    {
        public readonly PlayerAttackKind Kind;
        public readonly int FirstDamage;
        public readonly bool FullHeavy, FullIron, Terminal;
        public bool Hit, Completed;
        public float LinkBonus;
        public HashSet<SlashEnemy> Targets;
        public float NearestDistance = float.PositiveInfinity;
        public Vector2 BurstCenter;

        public TreasureAttack(PlayerAttackKind kind, int firstDamage, bool fullHeavy, bool fullIron, bool terminal)
        {
            Kind = kind;
            FirstDamage = firstDamage;
            FullHeavy = fullHeavy;
            FullIron = fullIron;
            Terminal = terminal;
        }
    }

    sealed class TreasureEffects
    {
        struct Mark
        {
            public int Hits;
            public double Until, Rearm;
        }

        struct Shield
        {
            public int Amount;
            public double Until;

            public int Absorb(int damage, double now)
            {
                if (now >= Until) return damage;
                int blocked = Mathf.Min(Amount, damage);
                Amount -= blocked;
                return damage - blocked;
            }
        }

        readonly Dictionary<TreasureTrigger, int> triggers = new Dictionary<TreasureTrigger, int>();
        readonly float[] stats = new float[(int)AlchemyStat.Count];
        readonly HashSet<SlashEnemy> slowed = new HashSet<SlashEnemy>();
        readonly Dictionary<SlashEnemy, Mark> marks = new Dictionary<SlashEnemy, Mark>();
        double cloudUntil, chargeUntil, chargeRearm, dashUntil, guardUntil, guardRearm, lowShieldRearm;
        double heavyLinkUntil, techniqueLinkUntil, finisherWaveRearm, lethalUntil, lethalRearm, mirrorRearm, movementRearm;
        bool lethalHealPending, movementSampleValid;
        float movementDistance;
        Vector2 movementSample, movementDirection;
        float movementBudget;
        Shield lowShield, overflowShield;

        static double Now => Time.timeAsDouble;
        public float this[AlchemyStat stat] => stats[(int)stat];
        public int Stacks(TreasureTrigger trigger) => triggers.TryGetValue(trigger, out int count) ? count : 0;
        public bool Has(TreasureTrigger trigger) => Stacks(trigger) > 0;
        public bool Invulnerable => Now < lethalUntil;

        public void Equip(AlchemyEntry entry)
        {
            var definition = TreasureCatalog.Definition(entry);
            foreach (var change in definition.Stats) stats[(int)change.Stat] += change.Amount;
            if (definition.Trigger != TreasureTrigger.None) triggers[definition.Trigger] = Stacks(definition.Trigger) + 1;
        }

        public TreasureAttack BeginAttack(PlayerAttackKind kind, int firstDamage, bool fullHeavy = false,
            bool fullIron = false, bool terminal = false)
            => triggers.Count > 0 ? new TreasureAttack(kind, firstDamage, fullHeavy, fullIron, terminal) : null;

        public float LinkBonus(TreasureAttack attack)
        {
            if (attack == null || !Has(TreasureTrigger.TechniqueHeavyLink)) return 0;
            if (attack.Hit) return attack.LinkBonus;
            if (attack.Kind == PlayerAttackKind.Technique && Now < techniqueLinkUntil)
            {
                attack.LinkBonus = .25f * Stacks(TreasureTrigger.TechniqueHeavyLink);
                techniqueLinkUntil = 0;
            }
            if (attack.FullHeavy && Now < heavyLinkUntil)
            {
                attack.LinkBonus = .35f * Stacks(TreasureTrigger.TechniqueHeavyLink);
                heavyLinkUntil = 0;
            }
            return attack.LinkBonus;
        }

        public void RecordHit(TreasureAttack attack, SlashEnemy enemy, Vector2 ownerPosition)
        {
            if (attack == null) return;
            if (!attack.Hit)
            {
                attack.Hit = true;
                if (Has(TreasureTrigger.TechniqueHeavyLink))
                {
                    if (attack.Kind == PlayerAttackKind.Technique) heavyLinkUntil = Now + 5;
                    if (attack.FullHeavy) techniqueLinkUntil = Now + 5;
                }
                if (attack.FullIron && Has(TreasureTrigger.IronCharge) && Now >= chargeRearm)
                {
                    chargeUntil = Now + 4;
                    chargeRearm = Now + 8;
                }
            }
            if (Has(TreasureTrigger.MultiHitBurst) && (attack.Targets ??= new HashSet<SlashEnemy>()).Add(enemy))
            {
                Vector2 point = enemy.transform.position;
                float distance = (point - ownerPosition).sqrMagnitude;
                if (distance < attack.NearestDistance)
                {
                    attack.NearestDistance = distance;
                    attack.BurstCenter = point;
                }
            }
            if (Has(TreasureTrigger.FirstHitSlow) && slowed.Add(enemy))
                enemy.ApplyTreasureSlow(Mathf.Min(.95f, .25f * Stacks(TreasureTrigger.FirstHitSlow)));
            if (!Has(TreasureTrigger.TargetMark)) return;
            marks.TryGetValue(enemy, out var mark);
            if (Now < mark.Rearm) return;
            if (++mark.Hits == 4)
                mark = new Mark { Until = Now + 4, Rearm = Now + 10 };
            marks[enemy] = mark;
        }

        public float MarkBonus(SlashEnemy enemy)
            => marks.TryGetValue(enemy, out var mark) && Now < mark.Until ? .20f * Stacks(TreasureTrigger.TargetMark) : 0;

        public bool TryFinishMirror(TreasureAttack attack)
        {
            if (attack == null || attack.Completed) return false;
            attack.Completed = true;
            if (attack.Targets == null || attack.Targets.Count < 2 || Now < mirrorRearm) return false;
            mirrorRearm = Now + 5;
            return true;
        }

        public void FinishCloudSkill()
        {
            if (Has(TreasureTrigger.CloudRebuild)) cloudUntil = Now + 3;
        }

        public bool TryConsumeCloudStack()
        {
            if (Now >= cloudUntil) return false;
            cloudUntil = 0;
            return true;
        }

        public float ConsumeChargeReduction()
        {
            if (Now >= chargeUntil) return 0;
            chargeUntil = 0;
            return Mathf.Min(.95f, .25f * Stacks(TreasureTrigger.IronCharge));
        }

        public void FinishDash()
        {
            if (Has(TreasureTrigger.DashRefund)) dashUntil = Now + 1.5;
        }

        public bool TryConsumeDashRefund()
        {
            if (Now >= dashUntil) return false;
            dashUntil = 0;
            return true;
        }

        public bool TryFinisherWave()
        {
            if (!Has(TreasureTrigger.FinisherWave) || Now < finisherWaveRearm) return false;
            finisherWaveRearm = Now + 4;
            return true;
        }

        public void RecordMovement(Vector2 position, Vector2 commandedVelocity, float dt, bool inCombat)
        {
            if (!Has(TreasureTrigger.MovementHit)) return;
            if (inCombat && movementSampleValid && Now >= movementRearm)
            {
                float moved = Vector2.Dot(position - movementSample, movementDirection);
                movementDistance = Mathf.Min(8, movementDistance + Mathf.Clamp(moved, 0, movementBudget));
            }
            movementSample = position;
            movementDirection = commandedVelocity.normalized;
            movementBudget = commandedVelocity.magnitude * dt;
            movementSampleValid = inCombat && Now >= movementRearm;
        }

        public bool TryMovementHit()
        {
            if (movementDistance < 8) return false;
            movementDistance = 0;
            movementRearm = Now + 6;
            movementSampleValid = false;
            return true;
        }

        public float ConsumeDamageGuard()
        {
            if (Now >= guardUntil) return 1;
            guardUntil = 0;
            return 1 - Mathf.Min(.95f, .30f * Stacks(TreasureTrigger.HitGuard));
        }

        public void OnHurt(int before, int after, int maxHealth)
        {
            if (Has(TreasureTrigger.HitGuard) && Now >= guardRearm)
            {
                guardUntil = Now + 3;
                guardRearm = Now + 12;
            }
            long threshold = (long)maxHealth * 35;
            if (Has(TreasureTrigger.LowHealthShield) && Now >= lowShieldRearm
                && (long)before * 100 > threshold && (long)after * 100 <= threshold)
            {
                lowShield = new Shield {
                    Amount = Mathf.RoundToInt(maxHealth * .12f * Stacks(TreasureTrigger.LowHealthShield)), Until = Now + 4
                };
                lowShieldRearm = Now + 35;
            }
        }

        public void OnOverheal(int overflow, int maxHealth)
        {
            if (overflow <= 0 || !Has(TreasureTrigger.OverhealShield)) return;
            int remaining = Now < overflowShield.Until ? overflowShield.Amount : 0;
            int count = Stacks(TreasureTrigger.OverhealShield);
            overflowShield = new Shield {
                Amount = Mathf.Min(Mathf.RoundToInt(maxHealth * .10f * count), remaining + Mathf.RoundToInt(overflow * .60f * count)),
                Until = Now + 5
            };
        }

        public int AbsorbDamage(int damage) => overflowShield.Absorb(lowShield.Absorb(damage, Now), Now);

        public bool PreventLethalDamage()
        {
            if (!Has(TreasureTrigger.LethalGuard) || Now < lethalRearm) return false;
            lethalUntil = Now + 1.2 * Stacks(TreasureTrigger.LethalGuard);
            lethalRearm = Now + 120;
            lethalHealPending = true;
            return true;
        }

        public bool TryLethalGuardHeal(PlayerAttackKind kind)
        {
            if (!Invulnerable || !lethalHealPending || (kind != PlayerAttackKind.Normal && kind != PlayerAttackKind.Heavy))
                return false;
            lethalHealPending = false;
            return true;
        }

        public void ResetBattle()
        {
            slowed.Clear();
            marks.Clear();
            movementSampleValid = false;
        }
    }
}
