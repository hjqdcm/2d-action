using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SlashDemo.Editor
{
    public static class AscensionChallengeValidation
    {
        const string Key = "SlashDemo.AscensionChallengeValidation";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly int[] Health = { 660, 528, 495, 572 };
        static readonly float[] Damage = { 1, 1.08f, 1.42f, .84f };
        static readonly float[] Speed = { 3.55f, 4.50f * 1.05f, 3.05f, 3.45f };
        static readonly float[] Reach = { 1.25f, 1.58f, 2.48f, 3.05f };
        static readonly string[][] SkillNames =
        {
            new[] { "碎岩拳", "崩山踏", "撼岳双震", "震脉架势" },
            new[] { "回风斩", "穿云剑", "十字剑阵", "掠影双冲" },
            new[] { "落星诀", "玄霜带", "三星连落", "引星涡" },
            new[] { "爆炎符", "双生符", "四门封阵", "封路结界" }
        };
        static readonly int[,] FirstRoundSkillDamage = { { 12, 14, 18 }, { 13, 16, 23 }, { 17, 20, 22 }, { 10, 13, 20 } };
        static readonly bool[] Seen = new bool[4];
        static readonly MethodInfo TickCombat = typeof(AscensionCultivator).GetMethod("Tick", Private);
        static readonly MethodInfo StopCombat = typeof(AscensionCultivator).GetMethod("Stop", Private);
        static readonly MethodInfo FixedCombat = typeof(AscensionCultivator).GetMethod("FixedTick", Private);
        static readonly MethodInfo LinkedSkill = typeof(AscensionCultivator).GetMethod("LinkedSkill", Private);
        static readonly MethodInfo FixedPlayer = typeof(SlashPlayer).GetMethod("FixedUpdate", Private);
        static readonly FieldInfo PlayerMove = typeof(SlashPlayer).GetField("move", Private);
        static readonly PropertyInfo SealSlowRemaining = typeof(AscensionCultivator).GetProperty("SealSlowRemaining", Private);
        static readonly FieldInfo Cooldowns = typeof(AscensionCultivator).GetField("cooldowns", Private);
        static readonly FieldInfo PrepareTime = typeof(AscensionCultivator).GetField("prepareTime", Private);
        static readonly FieldInfo CastWindup = typeof(AscensionCultivator).GetField("castWindup", Private);
        static readonly FieldInfo PlayerInvulnerability = typeof(SlashPlayer).GetField("invulnerability", Private);
        static SlashArena arena;
        static SlashPlayer player;
        static SlashEnemy defeated;
        static EditorWindow view;
        static int stage, round, playerHealth, swings;
        static double deadline, next;
        static Vector2 click;
        static Keyboard walkingKeyboard;
        static Vector2 walkStart, opponentStart;
        static int walkingHealth, walkingSwings;
        static float walkingDeadline;

        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

        public static void RunBatch()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
            Application.runInBackground = true;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            deadline = EditorApplication.timeSinceStartup + 100;
            next = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void QueueClick(EventType type)
        {
            var queue = typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Check(queue != null, "Could not inject the challenge button click.");
            queue.Invoke(null, new object[] { new Event { type = type, button = 0, mousePosition = click } });
        }

        static AscensionCultivator CheckOpponent(int expectedRound)
        {
            var travel = arena.Travel;
            Check(travel.ChallengeRound == expectedRound && travel.Portal == null && !travel.HasModal && arena.Enemies.Count == 1,
                "Challenge did not produce exactly one live opponent.");
            var enemy = arena.Enemies[0];
            var cultivator = enemy.GetComponent<AscensionCultivator>();
            Check(cultivator != null && enemy.Alive && cultivator.Navigation.Walkable(enemy.transform.position), "Invalid cultivator spawn.");
            int path = (int)cultivator.CultivationPath;
            Check(enemy.GetComponentInChildren<SpriteRenderer>() != null
                && enemy.transform.Find("Visual/Cultivator art").GetComponent<SpriteRenderer>().sprite.texture.name.EndsWith("Cultivator-v1"),
                "Cultivator art is missing.");
            var settings = arena.Tuning.Settings;
            float multiplier = Mathf.Pow(1.1f, expectedRound - 1);
            int hp = Mathf.RoundToInt(Mathf.RoundToInt(Health[path] * settings.enemyHealthMultiplier) * multiplier);
            int damage = Mathf.RoundToInt(Mathf.RoundToInt(settings.enemyDamage * Damage[path]) * multiplier);
            arena.Tuning.ApplyEnemy(enemy);
            Check(enemy.Health == hp && enemy.maxHealth == hp && enemy.damage == damage, "Round strength did not apply or multiplied again on tuning.");
            Check(Mathf.Abs(enemy.moveSpeed - Speed[path] * settings.enemySpeedMultiplier) < .001f, "Round strength modified movement speed.");
            Check(Mathf.Abs(enemy.attackRange - Reach[path] * settings.enemyRange * 1.5f) < .001f, "Unexpected tuned skill range.");
            return cultivator;
        }

        static void VerifyMapSize()
        {
            var map = arena.Travel.ActiveMap;
            var renderer = map.GetComponent<SpriteRenderer>();
            Vector2 expectedSize = ManorEnvironment.WorldSize * .75f;
            Check(renderer.size == expectedSize, "Ascension artwork was not scaled to three quarters on both axes.");
            var edge = map.GetComponentInChildren<EdgeCollider2D>();
            var points = edge.points;
            var reference = map.Definition.boundary;
            Check(points.Length == reference.Length, "The resized platform lost collision boundary points.");
            for (int i = 0; i < points.Length; i++)
                Check(Vector2.Distance(points[i], ManorEnvironment.Ground(reference[i].x, reference[i].y) * .75f) < .001f,
                    "The resized platform art and collision boundary do not align.");
            var entry = map.Definition.entry;
            Check(Vector2.Distance(player.transform.position, ManorEnvironment.Ground(entry.x, entry.y) * .75f) < .001f,
                "The player did not spawn at the scaled platform entrance.");
        }

        static void VerifyDefense(AscensionCultivator cultivator)
        {
            var enemy = arena.Enemies[0];
            int before = enemy.Health;
            enemy.TakeHit(40, Vector2.right, 0);
            int damage = cultivator.CultivationPath == AscensionCultivator.Path.Body ? 34 : 40;
            Check(enemy.Health == before - damage, "The cultivator did not apply its damage reduction to actual hits.");
        }

        static void BeginTelegraph(AscensionCultivator cultivator)
        {
            TickCombat.Invoke(cultivator, new object[] { .01f });
            FinishPreparation(cultivator);
        }

        static void FinishPreparation(AscensionCultivator cultivator)
        {
            float preparation = (float)PrepareTime.GetValue(cultivator);
            if (preparation > 0) TickCombat.Invoke(cultivator, new object[] { preparation });
        }

        static void ReadyOnly(AscensionCultivator cultivator, int skill)
        {
            var cooldowns = (float[])Cooldowns.GetValue(cultivator);
            for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = i == skill ? 0 : 30;
        }

        static void VerifySkillChoice(AscensionCultivator cultivator)
        {
            var opponent = arena.Enemies[0];
            Vector2 origin = opponent.transform.position;
            var names = SkillNames[(int)cultivator.CultivationPath];
            var cooldowns = (float[])Cooldowns.GetValue(cultivator);
            var picked = new bool[names.Length];
            var randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(813);
                player.Teleport(origin + Vector2.down * (opponent.attackRange * .60f));
                for (int sample = 0; sample < 24; sample++)
                {
                    StopCombat.Invoke(cultivator, null);
                    BeginTelegraph(cultivator);
                    int choice = Array.IndexOf(names, cultivator.CastingSkill);
                    Check(choice >= 0, "Ready skills did not enter the random selection.");
                    picked[choice] = true;
                    for (int i = 0; i < cooldowns.Length; i++)
                        Check(i == choice ? cooldowns[i] > 0 : cooldowns[i] == 0, "Casting changed the wrong skill cooldown.");
                }
            }
            finally { UnityEngine.Random.state = randomState; }
            Check(Array.TrueForAll(picked, value => value), "Every ready skill, including the ultimate, should be able to open a fight.");

            StopCombat.Invoke(cultivator, null);
            float openingRangeScale = cultivator.CultivationPath switch
            {
                AscensionCultivator.Path.Body => .72f,
                AscensionCultivator.Path.Sword => .82f,
                _ => .94f
            };
            float justBeyondFirstSkill = opponent.attackRange * openingRangeScale * 1.04f;
            player.Teleport(origin + Vector2.down * justBeyondFirstSkill);
            cooldowns[1] = .5f;
            cooldowns[2] = cooldowns[3] = 2;
            TickCombat.Invoke(cultivator, new object[] { .25f });
            Check(opponent.IsChasing, "Opponent tried to cast a cooling skill or a skill outside its range.");
            TickCombat.Invoke(cultivator, new object[] { .25f });
            FinishPreparation(cultivator);
            Check(cultivator.CastingSkill == names[1], "The in-range skill was not selected when its cooldown expired.");
            StopCombat.Invoke(cultivator, null);
            Debug.Log("ASCENSION_SKILL_CHOICE_PASS: " + cultivator.CultivationPath);
        }

        static void VerifySkillKit(AscensionCultivator cultivator)
        {
            int path = (int)cultivator.CultivationPath;
            var enemy = arena.Enemies[0];
            var warning = enemy.GetComponent<ObliqueActor>().GroundWarning;
            Vector2 origin = enemy.transform.position;
            Vector2 inRange = origin + Vector2.down * (enemy.attackRange * .60f);
            for (int pass = 0; pass < 2; pass++)
            {
                StopCombat.Invoke(cultivator, null);
                for (int skill = 0; skill < 3; skill++)
                {
                    player.Teleport(inRange);
                    ReadyOnly(cultivator, skill);
                    BeginTelegraph(cultivator);
                    string name = SkillNames[path][skill];
                    int waves = skill != 2 ? 1 : path == 0 ? 2 : path == 2 ? 3 : 1;
                    for (int wave = 0; wave < waves; wave++)
                    {
                        Check(cultivator.CastingSkill == name && cultivator.CastingUltimate == (skill == 2)
                            && warning.gameObject.activeInHierarchy && !enemy.IsChasing, name + " did not telegraph.");
                        var lines = Array.FindAll(warning.GetComponentsInChildren<LineRenderer>(), line => line.enabled);
                        bool expanding = path == (int)AscensionCultivator.Path.Spell && skill == 2;
                        int outlines = path == 3 && skill == 2 ? 4 : expanding || (path == 1 && skill == 2)
                            || (path == 3 && skill == 1) || (path == 0 && wave == 1) ? 2 : 1;
                        Check(lines.Length == outlines, name + " drew the wrong number of warning outlines.");
                        var first = lines[0];
                        bool ring = path == 0 && wave == 1;
                        Vector2 start = first.GetPosition(0);
                        Vector2 middle = first.GetPosition(first.positionCount / 2);
                        Vector2 hitPoint = (start + middle) * .5f;
                        if (ring) hitPoint = (start + (Vector2)lines[1].GetPosition(0)) * .5f;
                        if (path == 3 && skill == 2) hitPoint = inRange; // Overlapping seals only deal damage once.
                        PlayerInvulnerability.SetValue(player, 0f);
                        int health = player.Health;
                        float windup = (float)CastWindup.GetValue(cultivator);
                        if (expanding)
                        {
                            const float LockedTime = .5f;
                            var inner = lines[1];
                            float outerRadius = Vector2.Distance(start, hitPoint);
                            float initialRadius = Vector2.Distance(inner.GetPosition(0), hitPoint);
                            Check(initialRadius > 0 && initialRadius < outerRadius * .12f, "The inner circle did not start near the center.");
                            Color trackingColor = first.startColor;
                            Vector2 trackedPosition = inRange + Vector2.right * (1.4f + wave * .75f);
                            Vector2 velocity = Vector2.right * 2.7f;
                            Vector2 lead = velocity * (LockedTime * .85f);
                            var playerBody = player.GetComponent<Rigidbody2D>();
                            player.Teleport(trackedPosition);
                            playerBody.linearVelocity = velocity;
                            float halfTracking = (windup - LockedTime) * .5f;
                            TickCombat.Invoke(cultivator, new object[] { halfTracking });
                            Vector2 movingCenter = (Vector2)first.GetPosition(0) - Vector2.right * outerRadius;
                            Check(Vector2.Distance(movingCenter, trackedPosition + lead) < .001f && first.startColor == trackingColor,
                                "The tracking outline failed to lead the moving player.");
                            float growingRadius = Vector2.Distance(inner.GetPosition(0), movingCenter);
                            Check(Mathf.Abs(growingRadius - outerRadius * Mathf.Lerp(.08f, 1, halfTracking / windup)) < .001f,
                                "The inner circle failed to expand around the tracking center.");

                            playerBody.linearVelocity = -velocity;
                            TickCombat.Invoke(cultivator, new object[] { halfTracking + .02f });
                            start = first.GetPosition(0);
                            hitPoint = start - Vector2.right * outerRadius;
                            Check(Vector2.Distance(hitPoint, trackedPosition - lead) < .001f && first.startColor != trackingColor,
                                "The landing failed to lock after the player's change of direction.");
                            player.Teleport(pass == 1 ? hitPoint : hitPoint + Vector2.up * 10);
                            TickCombat.Invoke(cultivator, new object[] { LockedTime - .01f });
                            float nearRadius = Vector2.Distance(inner.GetPosition(0), hitPoint);
                            Check((Vector2)first.GetPosition(0) == start
                                && nearRadius > outerRadius * .98f && nearRadius < outerRadius
                                && player.Health == health && cultivator.CastingSkill == name,
                                "The outer circle moved or the strike resolved before contact.");
                            TickCombat.Invoke(cultivator, new object[] { .02f });
                        }
                        else
                        {
                            player.Teleport(pass == 1 ? hitPoint : ring ? origin : hitPoint + Vector2.up * 10);
                            TickCombat.Invoke(cultivator, new object[] { windup + .01f });
                        }
                        float multiplier = Mathf.Pow(1.1f, arena.Travel.ChallengeRound - 1);
                        int expected = pass == 1 ? Mathf.RoundToInt(FirstRoundSkillDamage[path, skill] * multiplier) : 0;
                        Check(player.Health == health - expected,
                            $"{name} wave {wave + 1} dealt {health - player.Health} damage, expected {expected}.");
                        if (path == 3 && pass == 1)
                            Check(cultivator.PassiveBuff.stacks == (skill + 1) % 3,
                                "The passive did not display the mark stack or clear it when the slow began.");
                        if (expected > 0) player.Heal(expected);
                        if (path == 3 && pass == 1 && skill == 2) VerifySealSlow(cultivator, inRange);
                    }
                    Check(cultivator.CastingSkill == null && !warning.gameObject.activeSelf, name + " left a warning after recovery.");
                    TickCombat.Invoke(cultivator, new object[] { 1.2f });
                }
            }
            StopCombat.Invoke(cultivator, null);
            Debug.Log("ASCENSION_SKILLS_PASS: " + cultivator.CultivationPath + " (" + string.Join(", ", SkillNames[path]) + ")");
        }

        static void VerifyAttack(AscensionCultivator cultivator)
        {
            var enemy = arena.Enemies[0];
            Vector2 aim = Vector2.down;
            Vector2 origin = enemy.transform.position;
            Vector2 inRange = origin + aim * (enemy.attackRange * .60f);
            int health = player.Health;
            StopCombat.Invoke(cultivator, null);
            player.Teleport(inRange);
            ReadyOnly(cultivator, 0);
            BeginTelegraph(cultivator);
            var warning = enemy.GetComponent<ObliqueActor>().GroundWarning;
            Check(warning.gameObject.activeInHierarchy && !enemy.IsChasing, "Opponent did not warn before attacking.");
            var line = warning.GetComponentInChildren<LineRenderer>();
            var lockedOutline = line.GetPosition(1);
            player.Teleport(origin - aim * (enemy.attackRange + 2));
            float windup = (float)CastWindup.GetValue(cultivator);
            TickCombat.Invoke(cultivator, new object[] { windup * .5f });
            Check(warning.gameObject.activeSelf && line.GetPosition(1) == lockedOutline,
                "The warning moved after locking onto the player.");
            TickCombat.Invoke(cultivator, new object[] { windup * .5f + .01f });
            Check(player.Health == health && !warning.gameObject.activeSelf, "Locked attack followed the player or left its warning.");
            StopCombat.Invoke(cultivator, null);
            player.Teleport(inRange);
            PlayerInvulnerability.SetValue(player, 0f);
            ReadyOnly(cultivator, 0);
            BeginTelegraph(cultivator);
            TickCombat.Invoke(cultivator, new object[] { enemy.windupTime + .01f });
            Check(player.Health == health - enemy.damage, "Opponent attack did not deal its displayed round damage.");
        }

        static void ResolveWindup(AscensionCultivator cultivator)
            => TickCombat.Invoke(cultivator, new object[] { (float)CastWindup.GetValue(cultivator) + .01f });

        static void VerifySealSlow(AscensionCultivator cultivator, Vector2 inRange)
        {
            Check((float)SealSlowRemaining.GetValue(cultivator) == 3, "Three marks did not trigger a three-second slow.");
            var body = player.GetComponent<Rigidbody2D>();
            player.Teleport(inRange);
            PlayerMove.SetValue(player, Vector2.right);
            FixedPlayer.Invoke(player, null);
            float slowedSpeed = player.moveSpeed * .65f;
            Check(Mathf.Abs(body.linearVelocity.x - slowedSpeed) < .001f, "The marks did not slow walking by 35%.");
            Check(player.RequestAttack(Vector2.left), "Could not start an attack while slowed.");
            float expected = slowedSpeed * player.NormalAttackMoveSpeedMultiplier;
            Check(Mathf.Abs(body.linearVelocity.x - expected) < .001f, "The slow and attack movement were not multiplied at attack start.");
            FixedPlayer.Invoke(player, null);
            Check(Mathf.Abs(body.linearVelocity.x - expected) < .001f, "Physics movement did not preserve the combined slow.");

            player.Teleport(inRange);
            TickCombat.Invoke(cultivator, new object[] { 1.3f });
            ReadyOnly(cultivator, 0);
            BeginTelegraph(cultivator);
            PlayerInvulnerability.SetValue(player, 0f);
            int health = player.Health;
            ResolveWindup(cultivator);
            float remaining = (float)SealSlowRemaining.GetValue(cultivator);
            Check(player.Health < health && remaining > 0 && remaining < 1.8f && cultivator.PassiveBuff.stacks == 0,
                "A hit during the slow stacked a mark or extended the slow.");
            player.Heal(health - player.Health);

            TickCombat.Invoke(cultivator, new object[] { 3.01f });
            player.Teleport(inRange);
            PlayerMove.SetValue(player, Vector2.right);
            FixedPlayer.Invoke(player, null);
            Check(Mathf.Abs(body.linearVelocity.x - player.moveSpeed) < .001f, "Walking speed did not recover after the slow.");
            player.Teleport(inRange);
            Debug.Log("ASCENSION_SEAL_SLOW_PASS: walk and attack multipliers, no stacks during the slow, timed recovery.");
        }

        static void VerifyPassive(AscensionCultivator cultivator)
        {
            var enemy = arena.Enemies[0];
            var aura = enemy.transform.Find("Visual/Cultivator passive aura").GetComponent<SpriteRenderer>();
            switch (cultivator.CultivationPath)
            {
                case AscensionCultivator.Path.Body:
                    Check(cultivator.PassiveBuff.description.Contains("15%"), "The body passive is missing its reduction text.");
                    Check(Mathf.Abs(enemy.windupTime - arena.Tuning.Settings.enemyWindup * 1.08f) < .001f,
                        "The body windup did not use the faster attack timing.");
                    TickCombat.Invoke(cultivator, new object[] { .641f });
                    Check(enemy.IsChasing, "The body cultivator did not resume pursuit after a basic attack.");
                    float ambient = aura.color.a;
                    enemy.TakeHit(1, Vector2.right, 0);
                    Check(aura.enabled && aura.color.r > aura.color.g && aura.color.a > ambient,
                        "Iron Bone did not brighten its red aura on damage reduction.");
                    VerifyMeleeMovement(cultivator, enemy);
                    break;
                case AscensionCultivator.Path.Sword:
                    Check(cultivator.PassiveBuff.description.Contains("移速提高 5%")
                        && aura.enabled && aura.color.b > aura.color.r,
                        "Swift Sword did not display its new movement bonus and blue aura.");
                    Check(Mathf.Abs(enemy.windupTime - arena.Tuning.Settings.enemyWindup * .85f) < .001f,
                        "The sword passive did not shorten windup.");
                    TickCombat.Invoke(cultivator, new object[] { .56f * .85f + .001f });
                    Check(enemy.IsChasing, "The sword passive did not shorten recovery.");
                    VerifyMeleeMovement(cultivator, enemy);
                    break;
                case AscensionCultivator.Path.Spell:
                    StopCombat.Invoke(cultivator, null);
                    var body = enemy.GetComponent<Rigidbody2D>();
                    Vector2 origin = new Vector2(-3, 0);
                    body.position = origin;
                    enemy.transform.position = origin;
                    player.Teleport(origin + Vector2.right * 2);
                    Physics2D.SyncTransforms();
                    ReadyOnly(cultivator, 2);
                    BeginTelegraph(cultivator);
                    var cooldowns = (float[])Cooldowns.GetValue(cultivator);
                    cooldowns[0] = 0;
                    cooldowns[1] = 1.4f;
                    cooldowns[3] = 4.2f;
                    var remaining = (float[])cooldowns.Clone();
                    int toHalf = enemy.Health - enemy.maxHealth / 2;
                    Check(toHalf > 1, "Could not prepare the spell passive threshold.");
                    enemy.TakeHit(toHalf - 1, Vector2.right, 0);
                    Check(body.position == origin && cultivator.CastingUltimate && !aura.enabled,
                        "The spell passive triggered above half health.");
                    for (int i = 0; i < cooldowns.Length; i++)
                        Check(cooldowns[i] == remaining[i], "The spell passive reduced a cooldown above half health.");
                    enemy.TakeHit(1, Vector2.right, 0);
                    Vector2 destination = body.position;
                    Check(destination.x < origin.x - 3 && cultivator.Navigation.Walkable(destination) && enemy.Alive,
                        "The spell passive failed to blink to a valid point farther from the player.");
                    Check(cultivator.PassiveBuff.description.Contains("已触发") && cultivator.CastingSkill == null
                        && !enemy.GetComponent<ObliqueActor>().GroundWarning.gameObject.activeSelf,
                        "The blink failed to clear the warning or update passive status.");
                    for (int i = 0; i < cooldowns.Length; i++)
                        Check(cooldowns[i] == remaining[i] * .5f, "The blink did not halve every remaining cooldown.");
                    enemy.TakeHit(1, Vector2.right, 0);
                    Check(body.position == destination && aura.enabled, "The spell passive blinked twice or failed to flash on arrival.");
                    for (int i = 0; i < cooldowns.Length; i++)
                        Check(cooldowns[i] == remaining[i] * .5f, "The spell passive reduced a cooldown twice.");
                    TickCombat.Invoke(cultivator, new object[] { .56f });
                    Check(!aura.enabled, "The blink aura did not expire.");
                    break;
                case AscensionCultivator.Path.Talisman:
                    // VerifyAttack ends with one valid hit, after separately testing a dodge.
                    Check(cultivator.PassiveBuff.stacks == 1 && cultivator.PassiveBuff.duration == 6 && aura.enabled,
                        "The talisman badge did not track the active mark and duration.");
                    TickCombat.Invoke(cultivator, new object[] { 6.01f });
                    Check(cultivator.PassiveBuff.stacks == 0 && cultivator.PassiveBuff.duration == 0 && !aura.enabled,
                        "Inactive talisman marks or their aura did not expire.");
                    break;
            }
            Debug.Log("ASCENSION_PASSIVE_PASS: " + cultivator.CultivationPath + " · " + cultivator.PassiveBuff.name);
        }

        static void VerifyMeleeMovement(AscensionCultivator cultivator, SlashEnemy enemy)
        {
            var body = enemy.GetComponent<Rigidbody2D>();
            Vector2 origin = new Vector2(-3, 0);
            body.position = origin;
            enemy.transform.position = origin;
            StopCombat.Invoke(cultivator, null);
            player.Teleport(origin + Vector2.right * 5);
            Physics2D.SyncTransforms();
            var playerBody = player.GetComponent<Rigidbody2D>();
            playerBody.linearVelocity = Vector2.right * player.moveSpeed;
            Vector2 expected = Vector2.right * enemy.moveSpeed;
            FixedCombat.Invoke(cultivator, new object[] { .02f });
            if (cultivator.CultivationPath == AscensionCultivator.Path.Body)
            {
                float farSpeed = body.linearVelocity.x;
                player.Teleport(origin + Vector2.right * (enemy.attackRange * .72f + .02f));
                Physics2D.SyncTransforms();
                playerBody.linearVelocity = Vector2.right * player.moveSpeed;
                FixedCombat.Invoke(cultivator, new object[] { .02f });
                Check(farSpeed > player.moveSpeed * 1.1f && Mathf.Abs(body.linearVelocity.x - expected.x) < .01f,
                    "Body pursuit did not ease back to its movement speed before preparing an attack.");
            }
            else Check(Vector2.Distance(body.linearVelocity, expected) < .001f,
                "Sword pursuit exceeded its movement speed against a retreating player.");

            player.Teleport(origin + Vector2.right * (enemy.attackRange * .60f));
            Physics2D.SyncTransforms();
            ReadyOnly(cultivator, 0);
            TickCombat.Invoke(cultivator, new object[] { .01f });
            Check(!enemy.IsChasing && cultivator.CastingSkill == null, enemy.name + " did not enter attack preparation.");
            FixedCombat.Invoke(cultivator, new object[] { .02f });
            Check(Vector2.Distance(body.linearVelocity, expected) < .001f,
                enemy.name + " exceeded its movement speed in attack preparation.");
            StopCombat.Invoke(cultivator, null);
            Debug.Log($"ASCENSION_MELEE_SPEED_PASS: {enemy.name} at {expected.x:F3} entering preparation.");
        }

        static void VerifyMechanic(AscensionCultivator cultivator)
        {
            var enemy = arena.Enemies[0];
            var enemyBody = enemy.GetComponent<Rigidbody2D>();
            var playerBody = player.GetComponent<Rigidbody2D>();
            Vector2 origin = new Vector2(-3, 0);
            StopCombat.Invoke(cultivator, null);
            enemyBody.position = origin;
            enemy.transform.position = origin;
            player.Teleport(origin + Vector2.right * (enemy.attackRange * .7f));
            Physics2D.SyncTransforms();
            if (cultivator.CultivationPath == AscensionCultivator.Path.Body)
            {
                Vector2 delta = playerBody.position - enemyBody.position;
                var isAttacking = typeof(SlashPlayer).GetProperty(nameof(SlashPlayer.IsAttacking));
                Check((int)LinkedSkill.Invoke(cultivator, new object[] { 15, delta }) == -1,
                    "The body cultivator favored the stance against an idle player.");
                isAttacking.SetValue(player, true);
                try
                {
                    Check((int)LinkedSkill.Invoke(cultivator, new object[] { 15, delta }) == 3
                        && (int)LinkedSkill.Invoke(cultivator, new object[] { 7, delta }) == -1,
                        "The body cultivator did not favor a ready stance against a nearby attack.");
                }
                finally { isAttacking.SetValue(player, false); }
            }
            ReadyOnly(cultivator, 3);
            if (cultivator.CultivationPath == AscensionCultivator.Path.Talisman) playerBody.linearVelocity = Vector2.right * 2.7f;
            BeginTelegraph(cultivator);
            string name = SkillNames[(int)cultivator.CultivationPath][3];
            Check(cultivator.CastingSkill == name, name + " did not start from the ready skill pool.");
            var warning = enemy.GetComponent<ObliqueActor>().GroundWarning;
            int health = player.Health;
            PlayerInvulnerability.SetValue(player, 0f);
            var originalSimulation = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                switch (cultivator.CultivationPath)
                {
                    case AscensionCultivator.Path.Body:
                        int before = enemy.Health;
                        for (int i = 0; i < 4; i++) enemy.TakeHit(20, Vector2.right, 0);
                        Check(enemy.Health == before - 40, "The stance did not halve incoming damage.");
                        ResolveWindup(cultivator);
                        int chargedDamage = Mathf.RoundToInt(Mathf.RoundToInt(arena.Tuning.Settings.enemyDamage * .85f * 1.9f)
                            * Mathf.Pow(1.1f, round - 1));
                        Check(player.Health == health - chargedDamage, "The stance did not discharge its capped charge.");
                        Check((int)LinkedSkill.Invoke(cultivator, new object[] { 3, Vector2.right }) == 1
                            && (int)LinkedSkill.Invoke(cultivator, new object[] { 1, Vector2.right }) == -1,
                            "The charged stance failed to favor a ready stomp.");
                        break;
                    case AscensionCultivator.Path.Sword:
                        VerifyRush(cultivator, warning, enemyBody, health);
                        StopCombat.Invoke(cultivator, null);
                        enemyBody.position = origin;
                        enemy.transform.position = origin;
                        player.Teleport(origin + Vector2.right * (enemy.attackRange * .7f));
                        Physics2D.SyncTransforms();
                        ReadyOnly(cultivator, 3);
                        BeginTelegraph(cultivator);
                        VerifyRush(cultivator, warning, enemyBody, health, linked: true);
                        break;
                    case AscensionCultivator.Path.Spell:
                        float oldRadius = enemy.attackRange * .68f;
                        float radius = oldRadius * 1.20f;
                        var outline = warning.GetComponentInChildren<LineRenderer>();
                        float warningRadius = Vector3.Distance(outline.GetPosition(0), outline.GetPosition(outline.positionCount / 2)) * .5f;
                        Check(Mathf.Abs(warningRadius - radius) < .001f, "The vortex warning radius did not grow by 20%.");
                        ResolveWindup(cultivator);
                        var vortex = enemy.transform.parent.Find("Star vortex");
                        Check(vortex.gameObject.activeSelf && player.Health == health, "The vortex failed to form as a persistent pull.");
                        Check(Mathf.Abs(vortex.localScale.x - radius) < .001f, "The vortex visual radius did not grow by 20%.");
                        Vector2 center = vortex.position;
                        player.Teleport(center + Vector2.right * Mathf.Lerp(oldRadius, radius, .5f));
                        FixedPlayer.Invoke(player, null);
                        Check(playerBody.linearVelocity.x < 0, "The vortex did not pull inside the expanded area.");
                        player.Teleport(center + Vector2.right * (radius + .1f));
                        FixedPlayer.Invoke(player, null);
                        Check(playerBody.linearVelocity == Vector2.zero, "The vortex pulled beyond the expanded area.");
                        Vector2 edge = (Vector2)vortex.position + Vector2.right * 1.5f;
                        player.Teleport(edge);
                        Check((int)LinkedSkill.Invoke(cultivator, new object[] { 7, edge - origin }) == 2
                            && (int)LinkedSkill.Invoke(cultivator, new object[] { 3, edge - origin }) == 1,
                            "The vortex did not favor meteors or frost based on readiness.");
                        FixedPlayer.Invoke(player, null);
                        Check(playerBody.linearVelocity.x < -3, "The player did not feel the vortex pull.");
                        Check(player.TryDash(Vector2.right), "Could not test dashing through the pull.");
                        FixedPlayer.Invoke(player, null);
                        Check(playerBody.linearVelocity.x == player.dashSpeed, "The vortex interfered with dash escape.");
                        TickCombat.Invoke(cultivator, new object[] { 4.6f });
                        player.Teleport(edge);
                        FixedPlayer.Invoke(player, null);
                        Check(!vortex.gameObject.activeSelf && playerBody.linearVelocity == Vector2.zero, "An expired vortex still pulled.");
                        break;
                    case AscensionCultivator.Path.Talisman:
                        ResolveWindup(cultivator);
                        var barrier = enemy.transform.parent.Find("Talisman barrier");
                        Check(barrier.gameObject.activeSelf && barrier.position.x > playerBody.position.x + 2,
                            "The seal did not form ahead of the player's movement.");
                        Physics2D.SyncTransforms();
                        var wall = barrier.GetComponent<BoxCollider2D>();
                        Check(wall.size.x > 5 && wall.size.y < .35f && wall.bounds.extents.y > 2,
                            "The seal did not form a wall perpendicular to the player's movement.");
                        float edgeX = wall.bounds.max.x;
                        Vector2 approach = new Vector2(edgeX + .9f, barrier.position.y);
                        DrivePlayerIntoSeal(playerBody, approach);
                        Check(playerBody.position.x >= edgeX + .2f, "The player crossed the solid seal.");
                        Check((int)LinkedSkill.Invoke(cultivator, new object[] { 3, playerBody.position - origin }) == 1,
                            "An active seal did not favor a follow-up using twin talismans.");
                        DrivePlayerIntoSeal(playerBody, approach + Vector2.up * (wall.bounds.extents.y + .6f));
                        Check(playerBody.position.x < edgeX - .5f, "The player could not pass around the end of the wall.");
                        TickCombat.Invoke(cultivator, new object[] { 5.6f });
                        DrivePlayerIntoSeal(playerBody, approach);
                        Check(!barrier.gameObject.activeSelf && playerBody.position.x < edgeX - .5f, "The expired seal still blocked movement.");
                        VerifyFlankSeal(cultivator, enemy, warning, barrier, health);
                        break;
                }
            }
            finally { Physics2D.simulationMode = originalSimulation; }
            player.Heal(health - player.Health);
            StopCombat.Invoke(cultivator, null);
            Debug.Log("ASCENSION_MECHANIC_PASS: " + name);
            Debug.Log("ASCENSION_SYNERGY_PASS: " + cultivator.CultivationPath);
        }

        static void VerifyFlankSeal(AscensionCultivator cultivator, SlashEnemy enemy, Transform warning, Transform barrier, int health)
        {
            StopCombat.Invoke(cultivator, null);
            Vector2 position = (Vector2)enemy.transform.position + Vector2.right * (enemy.attackRange * .65f);
            player.Teleport(position);
            Physics2D.SyncTransforms();
            ReadyOnly(cultivator, 2);
            TickCombat.Invoke(cultivator, new object[] { .01f });
            var cooldowns = (float[])Cooldowns.GetValue(cultivator);
            cooldowns[3] = 0;
            FinishPreparation(cultivator);
            var lines = warning.GetComponentsInChildren<LineRenderer>();
            Check(!lines[4].enabled && cooldowns[3] == 0, "The ultimate automatically committed a seal against a stationary player.");

            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.up * 2.7f;
            TickCombat.Invoke(cultivator, new object[] { .1f });
            Check(lines[4].enabled && cooldowns[3] > 0 && !barrier.gameObject.activeSelf,
                "A sidestep did not trigger the flank seal warning and reserve its cooldown.");
            TickCombat.Invoke(cultivator, new object[] { .33f });
            Check(!barrier.gameObject.activeSelf, "The seal formed before its warning elapsed.");
            TickCombat.Invoke(cultivator, new object[] { .02f });
            Check(barrier.gameObject.activeSelf && barrier.position.y > position.y + 1 && !lines[4].enabled
                && warning.gameObject.activeSelf && cultivator.CastingUltimate && player.Health == health,
                "The flank seal did not block one escape direction while the ultimate was still charging.");
        }

        static void DrivePlayerIntoSeal(Rigidbody2D playerBody, Vector2 approach)
        {
            player.Teleport(approach);
            Physics2D.SyncTransforms();
            for (int i = 0; i < 22; i++)
            {
                playerBody.linearVelocity = Vector2.left * 6;
                Physics2D.Simulate(.02f);
            }
        }

        static void VerifyRush(AscensionCultivator cultivator, Transform warning, Rigidbody2D enemyBody, int health, bool linked = false)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Vector2 start = enemyBody.position;
                ResolveWindup(cultivator);
                Check(player.Health == health && !warning.gameObject.activeSelf, "The sword rush dealt damage before moving.");
                for (int i = 0; i < 20 && player.Health == health; i++)
                {
                    FixedCombat.Invoke(cultivator, new object[] { .02f });
                    Physics2D.Simulate(.02f);
                }
                Vector2 travel = enemyBody.position - start;
                Check(player.Health < health && (pass == 0 ? travel.x : travel.y) > 1, "The sword rush did not move and hit along its lane.");
                player.Heal(health - player.Health);
                PlayerInvulnerability.SetValue(player, 0f);
                player.Teleport(enemyBody.position + Vector2.up * 2.5f);
                Physics2D.SyncTransforms();
                if (linked && pass == 0)
                {
                    ((float[])Cooldowns.GetValue(cultivator))[0] = 0;
                    player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * 2.7f;
                }
                FixedCombat.Invoke(cultivator, new object[] { .02f });
                Check(warning.gameObject.activeSelf == (pass == 0), "The sword rush did not finish with exactly two passes.");
                if (linked && pass == 0)
                {
                    var line = warning.GetComponentInChildren<LineRenderer>();
                    Vector2 left = line.GetPosition(0), right = line.GetPosition(1);
                    Vector2 startCenter = (left + right) * .5f;
                    Vector2 side = (right - left).normalized;
                    Vector2 forward = new Vector2(side.y, -side.x);
                    Check(Vector2.Distance(left, right) > 2 && ((float[])Cooldowns.GetValue(cultivator))[0] > 0,
                        "The sidestep did not widen the charge preview and spend the slash cooldown.");
                    player.Teleport(startCenter + forward * 2.5f + side * 1.05f);
                    Physics2D.SyncTransforms();
                }
            }
        }

        static void InjectWalk()
        {
            if (InputState.currentUpdateType == InputUpdateType.Dynamic)
                InputSystem.QueueStateEvent(walkingKeyboard, new KeyboardState(UnityEngine.InputSystem.Key.D));
        }

        static void BeginWalkingAttack()
        {
            var cultivator = CheckOpponent(round);
            var opponent = arena.Enemies[0];
            opponentStart = new Vector2(-3, 0);
            // Start the heavy melee opponent just inside its closer opening distance.
            walkStart = new Vector2(cultivator.CultivationPath == AscensionCultivator.Path.Body ? -1.8f : -1.2f, 0);
            opponent.GetComponent<Rigidbody2D>().position = opponentStart;
            opponent.transform.position = opponentStart;
            player.Teleport(walkStart);
            Physics2D.SyncTransforms();
            StopCombat.Invoke(cultivator, null);
            ReadyOnly(cultivator, 0);
            PlayerInvulnerability.SetValue(player, 0f);
            walkingHealth = player.Health;
            walkingSwings = player.TotalSwings;
            walkingDeadline = Time.time + 3;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            walkingKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.onBeforeUpdate += InjectWalk;
            stage = 21;
        }

        static void CheckWalkingAttack()
        {
            player.RequestAttack(Vector2.left, false);
            var opponent = arena.Enemies[0];
            Check(Time.time < walkingDeadline, opponent.name + " could not hit a walking attacker.");
            if (player.Health >= walkingHealth) return;
            InputSystem.onBeforeUpdate -= InjectWalk;
            InputSystem.RemoveDevice(walkingKeyboard);
            Check(player.TotalSwings > walkingSwings && player.transform.position.x > walkStart.x + .7f,
                "Player was not walking and attacking before taking damage.");
            Check(Vector2.Distance(opponent.transform.position, opponentStart) > .15f,
                opponent.name + " stood still while attacking a moving player.");
            Debug.Log("ASCENSION_WALK_ATTACK_PASS: " + opponent.name);
            player.Heal(walkingHealth - player.Health);
            stage = 2;
        }

        static void DefeatOpponent()
        {
            defeated = arena.Enemies[0];
            defeated.TakeHit(100000, Vector2.right, 0);
            var travel = arena.Travel;
            Check(arena.Enemies.Count == 0 && travel.Portal == null && travel.IsChallengeDecisionOpen && travel.DefeatedChallenger == defeated.name,
                "Defeat did not open the choice before creating a portal.");
            Check(travel.ChallengeCompletedRounds == travel.ChallengeRound, "Victory did not record the completed fight.");
            Check(arena.Paused && Time.timeScale == 0 && arena.InputBlocked && !arena.PauseMenuOpen
                && !player.RequestAttack(Vector2.right), "Combat or the pause menu bypassed the challenge dialog.");
        }

        static AscensionCultivator ReenterThroughPortal()
        {
            var travel = arena.Travel;
            var portal = travel.Portal;
            Check(portal != null, "There is no exit portal.");
            player.Teleport(portal.transform.position);
            Check(travel.TryOpenPortal(), "The exit portal was unusable.");
            int health = player.Health;
            travel.SelectDestination(TravelDestination.AscensionPlatform);
            var cultivator = CheckOpponent(1);
            Check(travel.ChallengeCompletedRounds == 0 && player.Health == health, "Reentry carried over the score or changed health.");
            return cultivator;
        }

        static void LoseCurrentFight(int completedRounds)
        {
            var travel = arena.Travel;
            var opponent = arena.Enemies[0];
            var cultivator = opponent.GetComponent<AscensionCultivator>();
            var realm = player.Realm;
            var technique = player.Technique;
            int kills = arena.Kills;
            StopCombat.Invoke(cultivator, null);
            player.Teleport((Vector2)opponent.transform.position + Vector2.down * opponent.attackRange * .60f);
            ReducePlayerTo(1);
            PlayerInvulnerability.SetValue(player, 0f);
            ReadyOnly(cultivator, 0);
            BeginTelegraph(cultivator);
            TickCombat.Invoke(cultivator, new object[] { opponent.windupTime + .01f });
            Check(arena.Player == player && player.Health == Mathf.CeilToInt(player.maxHealth * .5f)
                && player.Realm == realm && player.Technique == technique, "Challenge loss changed the character or failed to restore half health.");
            Check(!arena.Defeated && !arena.Paused && !travel.HasModal && !arena.GetComponent<SlashHUD>().DefeatPanelVisible
                && travel.Portal != null && travel.ChallengeEndedByDefeat, "Challenge loss entered the ordinary defeat flow or did not open a portal.");
            Check(travel.ChallengeCompletedRounds == completedRounds && travel.ChallengeRound == completedRounds + 1
                && arena.Kills == kills && arena.Enemies.Count == 0 && opponent.Alive && !opponent.gameObject.activeSelf,
                "The losing fight was counted as a win or left the challenger active.");
        }

        static void ReducePlayerTo(int health)
        {
            PlayerInvulnerability.SetValue(player, 0f);
            Check(player.TakeDamage(player.Health - health, Vector2.zero) && player.Health == health, "Could not prepare the test health.");
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out.");
                if (stage == 21) { CheckWalkingAttack(); return; }
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + .26;
                switch (stage)
                {
                    case 0:
                        arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
                        if (arena == null || arena.Travel == null) return;
                        player = arena.Player;
                        arena.Tuning.RestoreDefaults();
                        arena.Tuning.Settings.maxHealth = 137;
                        arena.Tuning.ApplyAll();
                        player.invincible = false;
                        playerHealth = player.Health;
                        view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                        view.position = new Rect(0, 0, 1600, 940); view.Show(); view.Focus();
                        player.Teleport(SceneTravel.ManorGate.center);
                        stage = 1;
                        break;
                    case 1:
                        Check(arena.Travel.IsSelectionOpen, "Manor gate did not open.");
                        arena.Travel.SelectDestination(TravelDestination.AscensionPlatform);
                        VerifyMapSize();
                        round = 1;
                        stage = 20;
                        break;
                    case 20:
                        BeginWalkingAttack();
                        break;
                    case 2:
                        Check(arena.Player == player && player.Health == playerHealth, "Between rounds changed the player or health.");
                        var cultivator = arena.Enemies[0].GetComponent<AscensionCultivator>();
                        int path = (int)cultivator.CultivationPath;
                        Check(!Seen[path], "The same cultivation path appeared twice.");
                        Seen[path] = true;
                        VerifyDefense(cultivator);
                        VerifySkillChoice(cultivator);
                        VerifySkillKit(cultivator);
                        VerifyMechanic(cultivator);
                        VerifyAttack(cultivator);
                        VerifyPassive(cultivator);
                        int woundedHealth = player.Health;
                        DefeatOpponent();
                        Check(player.Health == (round < 4 ? woundedHealth : player.maxHealth),
                            "Victory applied the final round heal too early or failed to cap it at full health.");
                        Check(arena.Travel.CanContinueChallenge == (round < 4), "Continue button availability did not match the remaining pool.");
                        if (round == 1)
                        {
                            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
                            click = new Vector2(Screen.width * .5f - 126 * scale, Screen.height * .5f + 72 * scale);
                            swings = player.TotalSwings;
                            QueueClick(EventType.MouseDown);
                            stage = 3;
                        }
                        else if (round < 4)
                        {
                            arena.Travel.ResolveChallenge(true);
                            Check(!defeated.gameObject.activeSelf && !arena.Paused && player.Health == player.maxHealth,
                                "Continue retained the defeated opponent, pause or missing health.");
                            round++;
                            stage = 20;
                        }
                        else
                        {
                            arena.Travel.ResolveChallenge(true);
                            Check(arena.Travel.IsChallengeDecisionOpen && arena.Enemies.Count == 0, "A fifth fight was possible.");
                            arena.Travel.ResolveChallenge(false);
                            stage = 5;
                        }
                        break;
                    case 3:
                        QueueClick(EventType.MouseUp);
                        stage = 4;
                        break;
                    case 4:
                        Check(arena.Travel.ChallengeRound == 2 && !arena.Paused && !arena.Travel.HasModal
                            && player.TotalSwings == swings && player.Health == player.maxHealth,
                            "Continue button did not start round two at full health or leaked its click into combat.");
                        round++;
                        stage = 20;
                        break;
                    case 5:
                        Check(arena.Travel.Portal != null && arena.Enemies.Count == 0 && !arena.Paused
                            && arena.Travel.ChallengeCompletedRounds == 4, "Completion did not open the portal with four wins.");
                        VerifyAttack(ReenterThroughPortal());
                        DefeatOpponent();
                        float uiScale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
                        click = new Vector2(Screen.width * .5f + 126 * uiScale, Screen.height * .5f + 72 * uiScale);
                        QueueClick(EventType.MouseDown);
                        stage = 6;
                        break;
                    case 6:
                        QueueClick(EventType.MouseUp);
                        stage = 7;
                        break;
                    case 7:
                        var portal = arena.Travel.Portal;
                        Check(portal != null && !arena.Travel.HasModal && arena.Enemies.Count == 0 && arena.Travel.ChallengeRound == 1
                            && arena.Travel.ChallengeCompletedRounds == 1 && !arena.Travel.ChallengeEndedByDefeat && player.Health < player.maxHealth,
                            "The early exit button did not open a portal after only one fight.");
                        arena.Travel.ResolveChallenge(false);
                        Check(arena.Travel.Portal == portal, "Duplicate resolution created a second portal.");
                        ReenterThroughPortal();
                        player.SetRealm(CultivationRealm.GoldenCore);
                        LoseCurrentFight(0);
                        stage = 8;
                        break;
                    case 8:
                        ReenterThroughPortal();
                        ReducePlayerTo(10);
                        for (int win = 1; win <= 2; win++)
                        {
                            int expectedHealth = Mathf.Min(player.maxHealth, player.Health + Mathf.CeilToInt(player.maxHealth * .5f));
                            DefeatOpponent();
                            arena.Travel.ResolveChallenge(true);
                            Check(arena.Travel.ChallengeCompletedRounds == win && player.Health == expectedHealth,
                                "Continue did not keep the score and add a capped half-max heal.");
                        }
                        LoseCurrentFight(2);
                        stage = 9;
                        break;
                    case 9:
                        ReenterThroughPortal();
                        for (int win = 1; win <= 3; win++)
                        {
                            DefeatOpponent();
                            arena.Travel.ResolveChallenge(true);
                        }
                        const int lowHealth = 10;
                        ReducePlayerTo(lowHealth);
                        DefeatOpponent();
                        int healedHealth = lowHealth + Mathf.CeilToInt(player.maxHealth * .5f);
                        Check(player.Health == healedHealth && arena.Travel.ChallengeCompletedRounds == 4,
                            "Final victory did not add half the maximum health to the existing health.");
                        arena.Travel.ResolveChallenge(false);
                        Check(player.Health == healedHealth, "Opening the final portal applied the heal again.");
                        arena.Restart();
                        Check(arena.Travel.Current == TravelDestination.Manor && arena.Travel.ChallengeRound == 0
                            && arena.Travel.ChallengeCompletedRounds == 0 && arena.Enemies.Count == 3,
                            "Restart leaked challenge state or opponents into the manor.");
                        Debug.Log("ASCENSION_CHALLENGE_PASS: four unique cultivators, real attacks and dodge, compounded HP/damage, capped half-max heals on continue and final victory, first/later losses with half health and the correct score, native buttons, portals, reentry and restart.");
                        SessionState.SetBool(Key, false);
                        EditorApplication.Exit(0);
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogError("ASCENSION_CHALLENGE_FAIL: stage=" + stage + " round=" + round + " " + error);
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(1);
            }
        }
    }
}
