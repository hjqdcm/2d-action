using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class DashEscapeValidation
    {
        const string Key = "SlashDemo.DashEscapeValidation";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly MethodInfo FixedTick = typeof(SlashPlayer).GetMethod("FixedUpdate", Private);
        static readonly FieldInfo Remaining = typeof(SlashPlayer).GetField("dashRemaining", Private);
        static readonly FieldInfo Cooldown = typeof(SlashPlayer).GetField("dashCooldownLeft", Private);
        static readonly FieldInfo Move = typeof(SlashPlayer).GetField("move", Private);
        static double deadline;
        static int checks;
        static void Check(bool value, string message)
        { if (!value) throw new Exception(message); checks++; }

        public static void RunBatch()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Startup timed out.");
                var arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
                if (arena == null || arena.Tuning == null || arena.Tuning.Settings == null) return;
                Physics2D.simulationMode = SimulationMode2D.Script;
                var map = TravelMapEnvironment.Build(arena.transform, TravelMaps.Get(TravelDestination.Wilderness));
                foreach (WildernessCreature kind in Enum.GetValues(typeof(WildernessCreature))) Verify(arena, map, kind);
                Debug.Log("DASH_ESCAPE_PASS: " + checks + " checks; real bear/snake/ape rings; walking blocked, dash passes; overlap exit; walls; cooldown; pause; pre-existing ignores; teleport; disable; Q handoff.");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("DASH_ESCAPE_FAIL: " + error);
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(1);
            }
        }

        static void Verify(SlashArena arena, TravelMapEnvironment map, WildernessCreature kind)
        {
            var player = arena.Player;
            var body = player.GetComponent<Rigidbody2D>();
            var playerCollider = player.GetComponent<CircleCollider2D>();
            Vector2 center = new Vector2(0, 100); // Isolate physics from the map's scenery.
            var pack = new SlashEnemy[8];
            var colliders = new CircleCollider2D[pack.Length];
            for (int i = 0; i < pack.Length; i++)
            {
                Vector2 position = center + DemoArt.Direction(i * 45) * 1.5f;
                pack[i] = kind == WildernessCreature.Snake ? SnakeDemon.Spawn(arena, map, position, i)
                    : kind == WildernessCreature.Ape ? ApeDemon.Spawn(arena, map, position, i) : BearDemon.Spawn(arena, map, position);
                colliders[i] = pack[i].GetComponent<CircleCollider2D>();
            }
            void Reset()
            {
                player.Teleport(center);
                Cooldown.SetValue(player, 0f);
                Remaining.SetValue(player, 0f);
                Move.SetValue(player, Vector2.zero);
                typeof(SlashArena).GetField("waitingForMenuRelease", Private).SetValue(arena, false);
                Physics2D.SyncTransforms();
            }
            void Step(float dt = .01f)
            { FixedTick.Invoke(player, null); Physics2D.Simulate(dt); }
            void StartDash()
            {
                Check(player.TryDash(Vector2.right), "Dash did not start.");
                foreach (var collider in colliders)
                    Check(Physics2D.GetIgnoreCollision(playerCollider, collider), "Dash did not ignore a creature.");
            }
            void FinishDash()
            {
                float duration = player.dashDuration;
                for (float elapsed = 0; elapsed < duration - .00001f; elapsed += .01f)
                {
                    Remaining.SetValue(player, duration - elapsed);
                    Step(Mathf.Min(.01f, duration - elapsed));
                }
                Remaining.SetValue(player, 0f);
                Step();
            }

            Reset();
            Move.SetValue(player, Vector2.right);
            for (int i = 0; i < 30; i++) Step();
            Check(body.position.x < .4f, "Ordinary walking passed through the ring.");
            Reset(); StartDash();
            Check(!player.TryDash(Vector2.up), "Cooldown accepted a second dash.");
            arena.TogglePause();
            float remaining = (float)Remaining.GetValue(player);
            Step();
            Check(body.position == center && (float)Remaining.GetValue(player) == remaining, "Pause advanced dash movement.");
            arena.TogglePause();
            FinishDash();
            Check(body.position.x > 2.3f, "Dash was blocked by the ring: " + body.position);
            Check(playerCollider.Distance(colliders[0]).distance < 0
                == Physics2D.GetIgnoreCollision(playerCollider, colliders[0]), "Landing overlap was not handled safely.");
            Move.SetValue(player, Vector2.right);
            for (int i = 0; i < 40; i++) Step();
            foreach (var collider in colliders)
                Check(!Physics2D.GetIgnoreCollision(playerCollider, collider), "Collision remained ignored after walking clear.");
            for (int i = 0; i < pack.Length; i++)
                Check(Vector2.Distance(pack[i].transform.position, center + DemoArt.Direction(i * 45) * 1.5f) < .001f,
                    "Dash pushed a creature.");

            Reset();
            var wall = new GameObject("Dash solid wall test").AddComponent<BoxCollider2D>();
            wall.transform.position = center + Vector2.right * .7f;
            wall.size = new Vector2(.15f, 6);
            Physics2D.SyncTransforms(); StartDash(); FinishDash();
            Check(body.position.x < .38f && !Physics2D.GetIgnoreCollision(playerCollider, wall), "Dash passed through a solid wall.");
            UnityEngine.Object.DestroyImmediate(wall.gameObject);

            Reset();
            Physics2D.IgnoreCollision(playerCollider, colliders[7], true);
            StartDash(); player.Teleport(center + Vector2.left * 8);
            Check(Physics2D.GetIgnoreCollision(playerCollider, colliders[7]), "Cleanup lost a pre-existing collision ignore.");
            Physics2D.IgnoreCollision(playerCollider, colliders[7], false);
            foreach (var collider in colliders)
                Check(!Physics2D.GetIgnoreCollision(playerCollider, collider), "Teleport leaked dash collision ignores.");

            Reset(); StartDash();
            player.enabled = false;
            foreach (var collider in colliders)
                Check(!Physics2D.GetIgnoreCollision(playerCollider, collider), "Disable leaked dash collision ignores.");
            player.enabled = true;

            // A dash ending inside a creature can hand its collision ownership to Q.
            Reset(); player.EquipTechnique(TechniqueCatalog.FlowingCloud);
            StartDash(); Remaining.SetValue(player, 0f);
            body.position = center + Vector2.right * 1.5f;
            body.transform.position = body.position; Physics2D.SyncTransforms(); Step();
            player.ResetTechniqueCooldown();
            Check(player.TryTechniqueSkill(Vector2.left), "Q could not start after a dash landing.");
            player.ResetTechniqueCombo();
            foreach (var collider in colliders)
                Check(!Physics2D.GetIgnoreCollision(playerCollider, collider), "Q handoff leaked collision ignores.");
            player.Teleport(center);
            foreach (var enemy in pack)
            { arena.UnregisterEnemy(enemy); enemy.gameObject.SetActive(false); UnityEngine.Object.Destroy(enemy.gameObject); }
        }
    }
}
