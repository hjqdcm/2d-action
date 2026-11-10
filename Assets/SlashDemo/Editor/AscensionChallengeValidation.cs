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
        static readonly float[] Speed = { 3.10f, 4.50f, 3.05f, 3.45f };
        static readonly float[] Reach = { 1.25f, 1.58f, 2.48f, 3.05f };
        static readonly string[][] SkillNames =
        {
            new[] { "碎岩拳", "崩山踏", "撼岳双震" },
            new[] { "回风斩", "穿云剑", "十字剑阵" },
            new[] { "落星诀", "玄霜带", "三星连落" },
            new[] { "爆炎符", "双生符", "四门封阵" }
        };
        static readonly int[,] FirstRoundSkillDamage = { { 12, 14, 18 }, { 13, 16, 23 }, { 17, 20, 22 }, { 10, 13, 20 } };
        static readonly bool[] Seen = new bool[4];
        static readonly MethodInfo TickCombat = typeof(AscensionCultivator).GetMethod("Tick", Private);
        static readonly MethodInfo StopCombat = typeof(AscensionCultivator).GetMethod("Stop", Private);
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
            float preparation = (float)PrepareTime.GetValue(cultivator);
            if (preparation > 0) TickCombat.Invoke(cultivator, new object[] { preparation });
        }

        static void VerifySkillKit(AscensionCultivator cultivator)
        {
            int path = (int)cultivator.CultivationPath;
            var enemy = arena.Enemies[0];
            var warning = enemy.GetComponent<ObliqueActor>().GroundWarning;
            Vector2 origin = enemy.transform.position;
            Vector2 inRange = origin + Vector2.down * (enemy.attackRange * .65f);
            for (int pass = 0; pass < 2; pass++)
            {
                StopCombat.Invoke(cultivator, null);
                for (int skill = 0; skill < 3; skill++)
                {
                    player.Teleport(inRange);
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
                        player.Teleport(pass == 1 ? hitPoint : ring ? origin : hitPoint + Vector2.up * 10);
                        PlayerInvulnerability.SetValue(player, 0f);
                        int health = player.Health;
                        float windup = (float)CastWindup.GetValue(cultivator);
                        if (expanding)
                        {
                            var inner = lines[1];
                            float outerRadius = Vector2.Distance(start, hitPoint);
                            float initialRadius = Vector2.Distance(inner.GetPosition(0), hitPoint);
                            Check(initialRadius > 0 && initialRadius < outerRadius * .12f, "The inner circle did not start near the center.");
                            TickCombat.Invoke(cultivator, new object[] { windup * .5f });
                            float halfRadius = Vector2.Distance(inner.GetPosition(0), hitPoint);
                            Check(Mathf.Abs(halfRadius - outerRadius * .54f) < .001f, "The inner circle did not expand with cast progress.");
                            TickCombat.Invoke(cultivator, new object[] { windup * .49f });
                            float nearRadius = Vector2.Distance(inner.GetPosition(0), hitPoint);
                            Check((Vector2)first.GetPosition(0) == start
                                && nearRadius > outerRadius * .98f && nearRadius < outerRadius
                                && player.Health == health && cultivator.CastingSkill == name,
                                "The outer circle moved or the strike resolved before contact.");
                            TickCombat.Invoke(cultivator, new object[] { windup * .02f });
                        }
                        else TickCombat.Invoke(cultivator, new object[] { windup + .01f });
                        float multiplier = Mathf.Pow(1.1f, arena.Travel.ChallengeRound - 1);
                        int expected = pass == 1 ? Mathf.RoundToInt(FirstRoundSkillDamage[path, skill] * multiplier) : 0;
                        Check(player.Health == health - expected,
                            $"{name} wave {wave + 1} dealt {health - player.Health} damage, expected {expected}.");
                        if (expected > 0) player.Heal(expected);
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
            Vector2 inRange = origin + aim * (enemy.attackRange * .65f);
            int health = player.Health;
            StopCombat.Invoke(cultivator, null);
            player.Teleport(inRange);
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
            BeginTelegraph(cultivator);
            TickCombat.Invoke(cultivator, new object[] { enemy.windupTime + .01f });
            Check(player.Health == health - enemy.damage, "Opponent attack did not deal its displayed round damage.");
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
            walkStart = new Vector2(-1.2f, 0);
            opponent.GetComponent<Rigidbody2D>().position = opponentStart;
            opponent.transform.position = opponentStart;
            player.Teleport(walkStart);
            Physics2D.SyncTransforms();
            StopCombat.Invoke(cultivator, null);
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
            player.Teleport((Vector2)opponent.transform.position + Vector2.down * opponent.attackRange * .65f);
            ReducePlayerTo(1);
            PlayerInvulnerability.SetValue(player, 0f);
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
                        VerifySkillKit(cultivator);
                        VerifyAttack(cultivator);
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
