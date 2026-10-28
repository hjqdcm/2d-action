using UnityEngine;

namespace SlashDemo
{
    public sealed class HeavyReleaseState
    {
        public const float Pause = 0.033f, Sweep = 0.17f, WavePoint = 0.55f;
        public float Age { get; private set; }
        public bool Active { get; private set; }
        public float SweepProgress => Mathf.Clamp01((Age - Pause) / Sweep);
        public float RecoveryProgress => Mathf.Clamp01((Age - Pause - Sweep) / Mathf.Max(0.001f, recovery));
        float recovery, totalDuration;
        bool emitted;
        public void Begin(float duration)
        {
            Age = 0; recovery = Mathf.Max(0, duration);
            // Store the rounded float once: Mono may otherwise compare the clamped Age
            // against an unrounded intermediate sum and leave short recoveries active forever.
            totalDuration = Pause + Sweep + recovery;
            Active = true; emitted = false;
        }
        public bool Advance(float dt)
        {
            if (!Active || dt <= 0) return false;
            Age = Mathf.Min(Age + dt, totalDuration);
            bool fire = !emitted && Age >= Pause + Sweep * WavePoint;
            if (fire) emitted = true;
            if (Age >= totalDuration) Active = false;
            return fire;
        }
        public void Cancel() { Active = false; Age = 0; emitted = false; }
    }

    public sealed class SwordEquipState
    {
        public float Amount { get; private set; }
        public bool Ready => Amount >= 1;
        public bool Sheathed => Amount <= 0;
        public bool ScabbardVisible => Amount < SwordMotion.ExtractFraction;
        float idle;
        public void Advance(float dt, bool combat, float drawTime, float sheathTime, float delay)
        {
            if (dt <= 0) return;
            if (combat)
            {
                idle = 0;
                Amount = Mathf.MoveTowards(Amount, 1, dt / Mathf.Max(0.01f, drawTime));
            }
            else
            {
                float before = idle;
                idle += dt;
                float stowTime = Mathf.Max(0, idle - delay) - Mathf.Max(0, before - delay);
                Amount = Mathf.MoveTowards(Amount, 0, stowTime / Mathf.Max(0.01f, sheathTime));
            }
        }
    }

    // This action clock uses playable time only; each charge snapshots its own timings.
    public sealed class ChargedAttackState
    {
        public bool Charging { get; private set; }
        public bool Recovering => recoveryLeft > 0;
        public bool Busy => Charging || Recovering;
        public float Progress => Charging ? Mathf.Clamp01(chargeAge / requiredTime) : 0;
        public bool Ready => Charging && chargeAge >= requiredTime;
        public float RecoveryProgress => Recovering ? 1 - recoveryLeft / recoveryDuration : 1;
        float chargeAge, requiredTime, recoveryDuration, recoveryLeft;

        public bool Begin(float chargeTime, float recovery)
        {
            if (Busy) return false;
            Charging = true;
            chargeAge = 0;
            requiredTime = Mathf.Max(0.01f, chargeTime);
            recoveryDuration = Mathf.Max(0, recovery);
            return true;
        }
        public void Advance(float dt)
        {
            dt = Mathf.Max(0, dt);
            if (Charging) chargeAge = Mathf.Min(requiredTime, chargeAge + dt);
            else recoveryLeft = Mathf.Max(0, recoveryLeft - dt);
        }
        public bool Release(bool allowPartial = false)
        {
            if (!Charging) return false;
            bool fire = allowPartial || Ready;
            Charging = false;
            recoveryLeft = fire ? recoveryDuration : 0;
            chargeAge = 0;
            return fire;
        }
        public void Cancel() { Charging = false; chargeAge = recoveryLeft = 0; }
    }

    // Button edges are collected every rendered frame, including during hit stop.
    // Only playable time ages commands; a long freeze cannot expire a short tap.
    public sealed class CombatInputBuffer
    {
        public const float Window = 0.22f;
        float attackRemaining, dashRemaining;
        public bool HasAttack => attackRemaining > 0;
        public bool HasDash => dashRemaining > 0;
        public Vector2 AttackDirection { get; private set; }
        public float AttackTime { get; private set; }
        public Vector2 DashDirection { get; private set; }

        public void Advance(float unscaledDelta, bool frozen)
        {
            if (frozen) return;
            attackRemaining = Mathf.Max(0, attackRemaining - Mathf.Max(0, unscaledDelta));
            dashRemaining = Mathf.Max(0, dashRemaining - Mathf.Max(0, unscaledDelta));
        }
        static Vector2 Direction(Vector2 direction) => direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        public void Attack(Vector2 direction, float atTime = 0)
        { AttackDirection = Direction(direction); AttackTime = atTime; attackRemaining = Window; }
        public void Dash(Vector2 direction) { DashDirection = Direction(direction); dashRemaining = Window; }
        public void ConsumeAttack() { attackRemaining = 0; }
        public void Clear() { attackRemaining = dashRemaining = 0; }
    }
}
