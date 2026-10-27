using UnityEngine;

namespace SlashDemo
{
    public enum SwordAction { Cleave, ReverseCleave, WideCleave, SpinCleave }

    // Physical sword motion has a fixed length; sword light and collision share the attack envelope.
    public static class SwordMotion
    {
        public struct Pose
        {
            public Vector2 grip, tip, bodyOffset;
            public float angle, bladeLength, lean;
            public bool kinetic, headLock;
            public float hipPhase, torsoPhase, shoulderPhase, swordPhase;
            public float torsoShear, shoulderShear, torsoTurn, shoulderTurn, sink;
        }

        public static SlashPlayer.Strike[] Defaults() => new[] {
            new SlashPlayer.Strike(22, 2, 90, 0.28f, 5, SwordAction.Cleave, 0.24f, 0.38f, 0, 0.10f),
            new SlashPlayer.Strike(26, 2, 145, 0.30f, 6, SwordAction.ReverseCleave, 0.22f, 0.40f, 0, 0.10f),
            new SlashPlayer.Strike(40, 2, 170, 0.43f, 10, SwordAction.WideCleave, 0.28f, 0.36f, 0, 0.16f)
        };

        public static void UpgradeLegacy(SlashPlayer.Strike strike, int slot)
        {
            if (strike.motionVersion >= 5) return;
            var d = Defaults()[Mathf.Clamp(slot, 0, 2)];
            if (strike.motionVersion < 3)
            {
                if (strike.motionVersion < 2)
                {
                    strike.windupRatio = d.windupRatio;
                    strike.activeRatio = d.activeRatio;
                    strike.bladeWidth = d.bladeWidth;
                }
                strike.action = d.action;
                strike.stepDistance = 0;
                strike.arc = slot == 2 ? Mathf.Max(d.arc, strike.arc) : Mathf.Clamp(strike.arc, 30, 170);
                if (slot == 2)
                {
                    strike.reach = Mathf.Max(d.reach, strike.reach);
                    strike.duration = Mathf.Max(d.duration, strike.duration);
                    strike.bladeWidth = Mathf.Max(d.bladeWidth, strike.bladeWidth);
                }
            }
            // Restore the previous finisher for saves made during the discarded raised/lowered combo.
            if (strike.motionVersion == 4 && slot == 2 && Mathf.Approximately(strike.arc, 190)) strike.arc = d.arc;
            strike.motionVersion = 5;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        public static readonly Vector2 CarryGrip = new Vector2(0.04f, -0.24f);
        public const float CarryAngle = -122, CarryLength = 0.72f;
        public const float BladeLength = 1.3f;
        public const float ExtractFraction = 0.72f;

        public static Vector2 CarryAxisInView(Vector2 facing, float elevation, int previous = -1)
        {
            int direction = HeroFacingMath.Direction(facing, previous, elevation);
            Vector2 screen = DemoArt.Direction(CarryAngle);
            screen.x = Mathf.Abs(screen.x) * (HeroArmMath.RightHandOnScreenLeft(direction) ? -1 : 1);
            return ObliqueProjection.UnprojectGround(screen, elevation).normalized;
        }

        public static void EquipHands(Vector2 grip, Vector2 socket, float amount, out Vector2 right, out Vector2 left)
        {
            float reach = Mathf.SmoothStep(0, 1, amount / 0.18f);
            // Aim-local +Y is the character's left; the right-side sword has negative Y.
            right = Vector2.Lerp(new Vector2(0.02f, -0.25f), grip, reach);
            left = Vector2.Lerp(new Vector2(0.02f, 0.25f), socket, reach);
        }

        public static Pose EquipPose(Pose ready, float amount, out Vector2 socket)
            => EquipPose(ready, amount, DemoArt.Direction(CarryAngle), out socket);

        public static Pose EquipPose(Pose ready, float amount, Vector2 carryAxis, out Vector2 socket)
        {
            float pull = Ease(amount / ExtractFraction);
            float align = Ease((amount - ExtractFraction) / (1 - ExtractFraction));
            Vector2 axis = carryAxis.sqrMagnitude > .0001f ? carryAxis.normalized : DemoArt.Direction(CarryAngle);
            float carryAngle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
            // The support hand draws the scabbard back as the sword hand pulls forward.
            socket = CarryGrip + axis * (CarryLength * 0.4f * pull * (1 - align));
            ready.grip = Vector2.Lerp(CarryGrip - axis * (CarryLength * 0.6f * pull), ready.grip, align);
            ready.angle = Mathf.LerpAngle(carryAngle, ready.angle, align);
            ready.bladeLength = Mathf.Lerp(CarryLength, ready.bladeLength, align);
            ready.tip = ready.grip + DemoArt.Direction(ready.angle) * ready.bladeLength;
            ready.kinetic = false;
            ready.bodyOffset = Vector2.zero;
            ready.lean = ready.sink = 0;
            return ready;
        }

        static float PhaseScale(SlashPlayer.Strike strike) => Mathf.Min(1, strike.activeRatio / 0.38f);
        public static float SwingStart(SlashPlayer.Strike strike) => strike.windupRatio + 0.16f * PhaseScale(strike);
        static float Drive(float t, float start, float end) => Ease((t - start) / Mathf.Max(0.01f, end - start));
        static float Load(float t, float windup, float delay) => Drive(t, delay, windup);
        static float Rebound(float t)
        {
            // Seven degrees beyond the six-degree target, then a damped spring back to rest.
            float spring = Mathf.Exp(-5 * t) * (Mathf.Cos(10 * t) + 0.5f * Mathf.Sin(10 * t));
            return (6 + 7 * spring) * (1 - Ease(t));
        }

        public static float HeavySweep(float s)
        {
            s = Mathf.Clamp01(s);
            const float forward = 100f / 190;
            if (s <= 0.25f) return 0;
            if (s <= 0.55f) { float u = (s - 0.25f) / 0.30f; return forward * u * u; }
            if (s <= 0.75f) { float u = (s - 0.55f) / 0.20f; return Mathf.Lerp(forward, 0.97f, 1 - (1 - u) * (1 - u)); }
            float tail = (s - 0.75f) / 0.25f;
            return Mathf.Lerp(0.97f, 1, 1 - (1 - tail) * (1 - tail));
        }

        public static Pose HeavyPose(float charge, bool release, float sweep, float recovery, float clock)
        {
            float load = Ease(charge / 0.65f);
            var p = new Pose { kinetic = true, headLock = true, bladeLength = BladeLength };
            p.grip = Vector2.Lerp(new Vector2(0.39f, -0.06f), new Vector2(0.24f, 0.18f), load);
            p.angle = Mathf.Lerp(-25, 100, load);
            p.bodyOffset = new Vector2(-0.06f * load, 0);
            p.sink = -0.10f * load;
            p.torsoShear = -0.16f * load; p.shoulderShear = -0.18f * load;
            p.torsoTurn = -6 * load; p.shoulderTurn = -8 * load;
            if (!release) p.angle += Mathf.Sin(clock * Mathf.PI * 5) * (0.2f + 0.3f * Mathf.Clamp01(charge)) * load;
            else
            {
                float hip = Ease(sweep), torso = Drive(sweep, 0.10f, 1), shoulder = Drive(sweep, 0.20f, 1);
                float blade = HeavySweep(sweep);
                p.grip = Vector2.Lerp(new Vector2(0.24f, 0.18f), new Vector2(0.52f, -0.18f), blade);
                p.angle = Mathf.Lerp(100, -90, blade);
                p.bodyOffset.x = Mathf.Lerp(-0.06f, 0.10f, hip);
                p.sink = Mathf.Lerp(-0.10f, -0.18f, hip);
                p.torsoShear = Mathf.Lerp(-0.16f, 0.22f, torso);
                p.shoulderShear = Mathf.Lerp(-0.18f, 0.20f, shoulder);
                p.torsoTurn = Mathf.Lerp(-6, 14, torso);
                p.shoulderTurn = Mathf.Lerp(-8, 14, shoulder);
                if (recovery > 0)
                {
                    float u = Ease(recovery), later = Ease(Mathf.Clamp01((recovery - 0.07f) / 0.93f));
                    p.grip = Vector2.Lerp(p.grip, new Vector2(0.39f, -0.06f), u);
                    p.angle = Mathf.Lerp(-90, -25, u);
                    p.bodyOffset.x = 0.10f * (1 - u);
                    p.sink = -0.18f * (1 - u) + 0.025f * Mathf.Sin(recovery * Mathf.PI * 2) * Mathf.Sin(recovery * Mathf.PI);
                    p.torsoShear = 0.22f * (1 - u); p.shoulderShear = 0.20f * (1 - later);
                    p.torsoTurn = Rebound(recovery) * (14f / 13); p.shoulderTurn = Rebound(Mathf.Clamp01((recovery - 0.07f) / 0.93f)) * (14f / 13);
                }
            }
            p.lean = p.torsoTurn;
            p.grip += p.bodyOffset;
            p.tip = p.grip + DemoArt.Direction(p.angle) * p.bladeLength;
            return p;
        }

        public static float WaveTravel(float age, float speed, float distance)
        {
            float ramp = Mathf.Max(0.001f, distance * 0.4f / Mathf.Max(0.01f, speed));
            age = Mathf.Max(0, age);
            return Mathf.Min(distance, age < ramp ? 0.5f * speed * age * age / ramp : distance * 0.2f + speed * (age - ramp));
        }

        public static Pose Evaluate(SlashPlayer.Strike strike, float normalizedTime)
            => EvaluatePose(strike, normalizedTime, false);

        // Keep range tuning in the energy envelope, never in the physical weapon or hand reach.
        public static Pose EvaluateAttack(SlashPlayer.Strike strike, float normalizedTime)
            => EvaluatePose(strike, normalizedTime, true);

        static Pose EvaluatePose(SlashPlayer.Strike strike, float normalizedTime, bool energy)
        {
            if (strike.action == SwordAction.SpinCleave) return SpinPose(strike, normalizedTime, energy);
            float t = Mathf.Clamp01(normalizedTime);
            float windup = strike.windupRatio, end = windup + strike.activeRatio;
            bool reverse = strike.action == SwordAction.ReverseCleave;
            bool wide = strike.action == SwordAction.WideCleave;
            float scale = energy ? strike.reach / (wide ? 2.65f : reverse ? 1.9f : 1.85f) : 1;
            var rest = new Vector2(0.22f, -0.06f);
            Vector2 ready = reverse ? new Vector2(0.18f, -0.30f) : new Vector2(0.14f, 0.30f);
            float startAngle = (reverse ? -1 : 1) * Mathf.Clamp(strike.arc, 30, 170) * 0.5f;
            float endAngle = -startAngle;
            float phaseScale = PhaseScale(strike);
            float stroke = reverse ? -1 : 1;
            var pose = new Pose { bladeLength = energy ? strike.reach * (wide ? 0.80f : 0.70f) : BladeLength, kinetic = true };
            pose.hipPhase = Drive(t, windup, end);
            pose.torsoPhase = Drive(t, windup + 0.05f * phaseScale, end);
            pose.shoulderPhase = Drive(t, windup + 0.10f * phaseScale, end);
            pose.swordPhase = Drive(t, SwingStart(strike), end);
            Vector2 finish = reverse ? new Vector2(0.48f, 0.18f) : new Vector2(0.48f, -0.18f);
            rest *= scale;
            ready *= scale;
            finish *= scale;
            if (t < windup)
            {
                float u = Load(t, windup, 0.08f * phaseScale);
                pose.grip = Vector2.Lerp(rest, ready, u);
                pose.angle = Mathf.Lerp(-25, startAngle, u);
                float hips = Ease(t / windup);
                float torso = Load(t, windup, 0.025f * phaseScale);
                float shoulder = Load(t, windup, 0.05f * phaseScale);
                pose.bodyOffset = new Vector2(-0.06f * hips, 0);
                pose.sink = -0.04f * hips;
                pose.torsoShear = -0.10f * torso * stroke;
                pose.shoulderShear = -0.10f * shoulder * stroke;
                pose.torsoTurn = -4 * torso * stroke;
                pose.shoulderTurn = -4 * shoulder * stroke;
            }
            else if (t <= end)
            {
                float u = pose.swordPhase;
                pose.grip = Vector2.Lerp(ready, finish, u);
                // Both cut directions sweep in front of the stationary root.
                pose.grip.x += Mathf.Sin(u * Mathf.PI) * 0.17f * scale;
                pose.angle = Mathf.Lerp(startAngle, endAngle, u);
                pose.bodyOffset = new Vector2(Mathf.Lerp(-0.06f, 0.08f, pose.hipPhase), 0);
                pose.sink = Mathf.Lerp(-0.04f, -0.12f, pose.hipPhase);
                pose.torsoShear = Mathf.Lerp(-0.10f, 0.16f, pose.torsoPhase) * stroke;
                pose.shoulderShear = Mathf.Lerp(-0.10f, 0.16f, pose.shoulderPhase) * stroke;
                pose.torsoTurn = (Mathf.Lerp(-4, 6, pose.torsoPhase) + 7 * Drive(pose.torsoPhase, 0.75f, 1)) * stroke;
                pose.shoulderTurn = (Mathf.Lerp(-4, 6, pose.shoulderPhase) + 7 * Drive(pose.shoulderPhase, 0.75f, 1)) * stroke;
            }
            else
            {
                float recovery = (t - end) / Mathf.Max(0.01f, 1 - end);
                float u = Ease(recovery);
                pose.grip = Vector2.Lerp(finish, rest, u);
                pose.angle = Mathf.Lerp(endAngle, -25, u);
                pose.bodyOffset = new Vector2(0.08f * (1 - u), 0);
                pose.sink = -0.12f * (1 - u);
                pose.torsoShear = 0.16f * (1 - u) * stroke;
                float shoulderRecovery = Mathf.Clamp01((recovery - 0.06f) / 0.94f);
                pose.shoulderShear = 0.16f * (1 - Ease(shoulderRecovery)) * stroke;
                pose.torsoTurn = Rebound(recovery) * stroke;
                pose.shoulderTurn = Rebound(shoulderRecovery) * stroke;
            }
            pose.lean = pose.torsoTurn;
            // Leave room for the abdomen, including the support hand behind the grip.
            pose.grip.x += 0.17f;
            pose.grip += pose.bodyOffset;
            pose.tip = pose.grip + DemoArt.Direction(pose.angle) * pose.bladeLength;
            return pose;
        }

        static Pose SpinPose(SlashPlayer.Strike strike, float normalizedTime, bool energy)
        {
            float t = Mathf.Clamp01(normalizedTime), start = SwingStart(strike);
            float end = strike.windupRatio + strike.activeRatio;
            var rest = new Vector2(0.39f, -0.06f);
            var pose = new Pose { kinetic = true, headLock = true,
                bladeLength = energy ? Mathf.Max(0.1f, strike.reach - 0.18f) : BladeLength };
            if (t < start)
            {
                float load = Ease(t / start);
                pose.angle = Mathf.Lerp(-25, -105, load);
                pose.grip = Vector2.Lerp(rest, new Vector2(-0.10f, 0.12f), load);
                pose.sink = -0.06f * load;
            }
            else if (t <= end)
            {
                float sweep = (t - start) / Mathf.Max(0.01f, end - start);
                float e = Ease(sweep);
                pose.angle = Mathf.Lerp(-105, 105, e);
                pose.grip = Vector2.Lerp(new Vector2(-0.10f, 0.12f), new Vector2(0.32f, -0.04f), e);
                pose.torsoTurn = Mathf.Sin(sweep * Mathf.PI) * 11;
                pose.shoulderTurn = pose.torsoTurn * 1.25f;
                pose.sink = -0.06f - Mathf.Sin(sweep * Mathf.PI) * 0.05f;
                pose.swordPhase = sweep;
            }
            else
            {
                float recovery = Ease((t - end) / Mathf.Max(0.01f, 1 - end));
                pose.angle = Mathf.Lerp(105, -25, recovery);
                pose.grip = Vector2.Lerp(new Vector2(0.32f, -0.04f), rest, recovery);
                pose.sink = -0.06f * (1 - recovery);
            }
            pose.lean = pose.torsoTurn;
            pose.tip = pose.grip + DemoArt.Direction(pose.angle) * pose.bladeLength;
            return pose;
        }

        public const float WhirlwindRingLife = 0.28f, WhirlwindInnerDelay = 0.04f;
        public const float WhirlwindEmissionPoint = 0.15f;
        public static float WhirlwindRadius(float age, float reach)
            => age < 0 ? 0 : reach;

        public static bool WhirlwindHit(Vector2 origin, float previousAge, float age, float reach, Vector2 target, float targetRadius)
        {
            // The visible sword light is already a full circle at emission, so damage is one pulse then.
            // Resolve the pulse even when a long frame crosses the entire visual lifetime.
            if (age < 0 || previousAge >= 0) return false;
            float padding = Mathf.Max(0, targetRadius) + 0.06f;
            return Vector2.Distance(origin, target) <= reach + padding;
        }

        public static Vector2 ToWorld(Vector2 local, Vector2 facing)
        {
            return facing * local.x + new Vector2(-facing.y, facing.x) * local.y;
        }

        public static float DistanceToBlade(Vector2 target, Vector2 bladeBase, Vector2 bladeTip, out Vector2 closest)
        {
            Vector2 axis = bladeTip - bladeBase;
            float t = axis.sqrMagnitude > 0.00001f ? Mathf.Clamp01(Vector2.Dot(target - bladeBase, axis) / axis.sqrMagnitude) : 0;
            closest = bladeBase + axis * t;
            return Vector2.Distance(target, closest);
        }

        public const int WaveArcSegments = 32;

        // A 60-degree slice of a circle whose radius equals its chord width.
        // The leading midpoint is the flight origin; the tips trail behind it.
        public static Vector2 WaveArcPoint(float width, float t)
        {
            float angle = Mathf.Clamp(t, -1, 1) * (Mathf.PI / 6);
            return new Vector2(width * (Mathf.Cos(angle) - 1), width * Mathf.Sin(angle));
        }

        public static bool WaveHit(Vector2 from, Vector2 facing, float travel, float width, Vector2 target, float radius)
        {
            Vector2 delta = target - from;
            var local = new Vector2(Vector2.Dot(delta, facing), Vector2.Dot(delta, new Vector2(-facing.y, facing.x)));
            Vector2 flight = Vector2.right * Mathf.Max(0, travel);
            float padding = Mathf.Max(0, radius) + 0.08f;
            Vector2 a = WaveArcPoint(width, -1);
            for (int i = 1; i <= WaveArcSegments; i++)
            {
                Vector2 b = WaveArcPoint(width, 2f * i / WaveArcSegments - 1);
                // Each moving arc segment sweeps a parallelogram, including long frames.
                if (local.y >= a.y && local.y <= b.y)
                {
                    float x = Mathf.Lerp(a.x, b.x, Mathf.InverseLerp(a.y, b.y, local.y));
                    if (local.x >= x && local.x <= x + flight.x) return true;
                }
                if (DistanceToBlade(local, a, b, out _) <= padding ||
                    DistanceToBlade(local, a + flight, b + flight, out _) <= padding ||
                    DistanceToBlade(local, a, a + flight, out _) <= padding ||
                    DistanceToBlade(local, b, b + flight, out _) <= padding) return true;
                a = b;
            }
            return false;
        }

        public static bool SweptHit(SlashPlayer.Strike strike, float previousTime, float currentTime,
            Vector2 previousOrigin, Vector2 origin, Vector2 facing, Vector2 target, float targetRadius, out Vector2 contact)
        {
            contact = target;
            float swingStart = SwingStart(strike);
            float start = Mathf.Max(previousTime, swingStart);
            float end = Mathf.Min(currentTime, strike.windupRatio + strike.activeRatio);
            if (end < start || currentTime < swingStart || previousTime > strike.windupRatio + strike.activeRatio) return false;
            float radius = Mathf.Max(0, targetRadius) + strike.bladeWidth * 0.5f;
            float fraction = (end - start) / Mathf.Max(0.01f, strike.windupRatio + strike.activeRatio - swingStart);
            bool spin = strike.action == SwordAction.SpinCleave;
            float travel = strike.reach * ((spin ? 210 : Mathf.Clamp(strike.arc, 30, 170)) * Mathf.Deg2Rad + 0.5f) * fraction;
            travel += Vector2.Distance(previousOrigin, origin);
            int samples = Mathf.Clamp(Mathf.CeilToInt(travel / Mathf.Max(0.025f, radius * 0.55f)), 1, spin ? 640 : 160);
            for (int i = 0; i <= samples; i++)
            {
                float t = Mathf.Lerp(start, end, i / (float)samples);
                float rootT = Mathf.InverseLerp(previousTime, currentTime, t);
                Vector2 root = Vector2.Lerp(previousOrigin, origin, rootT);
                var pose = EvaluateAttack(strike, t);
                Vector2 bladeBase = root + ToWorld(pose.grip, facing);
                Vector2 bladeTip = root + ToWorld(pose.tip, facing);
                if (DistanceToBlade(target, bladeBase, bladeTip, out contact) <= radius) return true;
            }
            return false;
        }
    }
}
