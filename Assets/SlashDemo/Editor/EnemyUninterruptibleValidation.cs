using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class EnemyUninterruptibleValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }

        public static void Verify(SlashArena arena, SlashEnemy bear)
        {
            var combat = bear.GetComponent<BearCombat>();
            var body = bear.GetComponent<Rigidbody2D>();
            var warning = bear.GetComponent<ObliqueActor>().GroundWarning;
            var nextClaw = typeof(BearCombat).GetField("nextClaw", Private);
            var nextCharge = typeof(BearCombat).GetField("nextCharge", Private);
            var age = typeof(BearCombat).GetField("age", Private);
            var immunity = typeof(SlashPlayer).GetField("invulnerability", Private);
            float oldClaw = (float)nextClaw.GetValue(combat), oldCharge = (float)nextCharge.GetValue(combat);
            float oldImmunity = (float)immunity.GetValue(arena.Player);
            Vector2 playerPosition = arena.Player.transform.position, bearPosition = body.position;
            int playerHealth = arena.Player.Health;
            bool invincible = arena.Player.invincible;
            SlashEnemy ordinary = null;
            var fixedUpdate = typeof(SlashEnemy).GetMethod("FixedUpdate", Private);

            void HitBear()
            {
                var state = combat.Current;
                float elapsed = (float)age.GetValue(combat), progress = combat.Progress;
                Vector2 aim = combat.LockedAim;
                bool visible = warning.gameObject.activeSelf;
                int health = bear.Health;
                bear.TakeHit(1, Vector2.left, 30, true);
                combat.Interrupt(.5f);
                Check(bear.Health == health - 1, "Super armor blocked damage.");
                Check(combat.Current == state && (float)age.GetValue(combat) == elapsed
                    && combat.Progress == progress && combat.LockedAim == aim, "Hit reset bear skill or progress: " + state);
                Check(warning.gameObject.activeSelf == visible, "Hit changed skill warning: " + state);
                fixedUpdate.Invoke(bear, null);
                Check(body.linearVelocity.sqrMagnitude < .0001f, "Hit knocked bear out of its skill: " + state);
            }

            try
            {
                arena.Player.invincible = false;
                combat.Stop();
                arena.Player.Teleport(bearPosition + Vector2.down * (bear.attackRange * .8f));
                immunity.SetValue(arena.Player, 0f);
                nextClaw.SetValue(combat, -1f);
                combat.Tick(.01f);
                Check(combat.Current == BearCombat.State.ClawWindup, "Claw test did not begin.");
                int events = combat.DamageEvents, initialHealth = arena.Player.Health;
                for (int i = 0; i < 40 && combat.Current != BearCombat.State.Recovery; i++)
                {
                    HitBear();
                    combat.Tick(.05f);
                }
                Check(combat.Current == BearCombat.State.Recovery && combat.DamageEvents == events + 1
                    && arena.Player.Health == initialHealth - bear.damage, "Repeated hits prevented claw from landing once.");
                HitBear(); combat.Tick(.86f);
                Check(combat.Current == BearCombat.State.Pursuit, "Hit delayed recovery.");

                arena.Player.Teleport(bearPosition + Vector2.down * 4);
                nextCharge.SetValue(combat, -1f); combat.Tick(.01f);
                Check(combat.Current == BearCombat.State.ChargeWindup, "Charge windup test did not begin.");
                for (int i = 0; i < 40 && combat.Current == BearCombat.State.ChargeWindup; i++)
                {
                    HitBear(); combat.Tick(.05f);
                }
                Check(combat.Current == BearCombat.State.Charging, "Repeated hits prevented charge from starting.");
                combat.Stop();

                ordinary = SlashArena.CreateEnemy(arena.transform, bearPosition + Vector2.right * 5, false, 100, 0);
                arena.RegisterEnemy(ordinary);
                ordinary.windupTime = Mathf.Max(.01f, Time.deltaTime * 12);
                var update = typeof(SlashEnemy).GetMethod("Update", Private);
                var phase = typeof(SlashEnemy).GetField("phase", Private);
                var timer = typeof(SlashEnemy).GetField("timer", Private);
                var ordinaryWarning = ordinary.GetComponent<ObliqueActor>().GroundWarning;
                arena.Player.Teleport((Vector2)ordinary.transform.position + Vector2.down * .9f);
                immunity.SetValue(arena.Player, 0f);
                initialHealth = arena.Player.Health;
                update.Invoke(ordinary, null);
                Check(phase.GetValue(ordinary).ToString() == "Windup", "Ordinary enemy did not begin attack.");
                int hits = 0;
                while (phase.GetValue(ordinary).ToString() == "Windup" && hits < 30)
                {
                    float remaining = (float)timer.GetValue(ordinary);
                    int health = ordinary.Health;
                    ordinary.TakeHit(1, Vector2.left, 30, hits % 2 == 0, hits % 3 != 0);
                    Check(ordinary.Health == health - 1 && (float)timer.GetValue(ordinary) == remaining
                        && ordinaryWarning.gameObject.activeSelf, "Hit reset ordinary enemy windup.");
                    fixedUpdate.Invoke(ordinary, null);
                    Check(ordinary.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude < .0001f, "Ordinary enemy was knocked back.");
                    update.Invoke(ordinary, null); hits++;
                }
                Check(hits >= 2 && phase.GetValue(ordinary).ToString() == "Recover"
                    && arena.Player.Health == initialHealth - ordinary.damage, "Repeated hits stun-locked ordinary enemy.");
                ordinary.TakeHit(100000, Vector2.left, 30, true);
                Check(!ordinary.Alive && !ordinary.GetComponent<Collider2D>().enabled
                    && !ordinaryWarning.gameObject.activeSelf && !arena.Enemies.Contains(ordinary), "Death did not stop ordinary enemy.");
                Debug.Log("ENEMY_UNINTERRUPTIBLE_PASS: repeated heavy hits preserve claw windup/swing/recovery and charge windup; claw lands once; ordinary enemy completes attack under repeated hits; damage, no knockback, death cleanup.");
            }
            finally
            {
                if (ordinary != null) { arena.UnregisterEnemy(ordinary); UnityEngine.Object.DestroyImmediate(ordinary.gameObject); }
                combat.Stop(); body.position = bearPosition;
                nextClaw.SetValue(combat, oldClaw); nextCharge.SetValue(combat, oldCharge);
                arena.Player.Teleport(playerPosition);
                if (arena.Player.Health < playerHealth) arena.Player.Heal(playerHealth - arena.Player.Health);
                immunity.SetValue(arena.Player, oldImmunity); arena.Player.invincible = invincible;
                Physics2D.SyncTransforms();
            }
        }
    }
}
