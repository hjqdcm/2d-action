using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SlashDemo.Editor
{
    public static class WildernessEncounterValidation
    {
        const string Key = "SlashDemo.WildernessEncounterValidation";
        static readonly FieldInfo EncounterField = typeof(SceneTravel).GetField("wildernessEncounter", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly WildernessCreature[] encounters = new WildernessCreature[7];
        static SlashArena arena;
        static SlashEnemy[] pack;
        static Vector2[] initial;
        static int stage, health, encounterCount;
        static double next, deadline;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Advance(int value, double delay = .3) { stage = value; next = EditorApplication.timeSinceStartup + delay; }

        // Exercise the real entry path deterministically without adding a gameplay override.
        public static void Enter(SlashArena owner, WildernessCreature kind)
        {
            var saved = Random.state;
            var savedEncounter = EncounterField.GetValue(owner.Travel);
            try
            {
                Random.InitState(0);
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    var encounter = new WildernessEncounter();
                    if (encounter.NextCreature != kind) continue;
                    EncounterField.SetValue(owner.Travel, encounter);
                    owner.Travel.SelectDestination(TravelDestination.Wilderness);
                    CheckPack(owner, kind);
                    return;
                }
                throw new Exception("Could not start a rotation with " + kind + ".");
            }
            finally
            {
                EncounterField.SetValue(owner.Travel, savedEncounter);
                Random.state = saved;
            }
        }

        static void CheckPack(SlashArena owner, WildernessCreature kind)
        {
            Check(owner.Enemies.Count == (kind == WildernessCreature.Bear ? BearDemon.WildernessCount
                : kind == WildernessCreature.Snake ? SnakeDemon.WildernessCount : ApeDemon.WildernessCount), "Entry created an incorrect pack size.");
            foreach (var enemy in owner.Enemies)
                Check(PackKind(enemy) == kind, "Entry created an unexpected creature.");
        }

        static WildernessCreature PackKind(SlashEnemy enemy) => enemy.rockBear ? WildernessCreature.Bear
            : enemy.GetComponent<SnakeDemon>() != null ? WildernessCreature.Snake
            : enemy.GetComponent<ApeDemon>() != null ? WildernessCreature.Ape
            : throw new Exception("Unexpected enemy in the wilderness.");

        public static void RunBatch()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Launch", true);
        }
        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            deadline = EditorApplication.timeSinceStartup + 100;
            next = EditorApplication.timeSinceStartup + 2;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (SessionState.GetBool(Key + ".Launch", false))
                {
                    SessionState.SetBool(Key + ".Launch", false);
                    EditorApplication.isPlaying = true;
                    deadline = EditorApplication.timeSinceStartup + 100;
                    Advance(0, 2); return;
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out at stage " + stage);
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
                switch (stage)
                {
                    case 0:
                        arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
                        if (arena == null || arena.Travel == null) return;
                        arena.Player.invincible = false;
                        arena.Player.Teleport(SceneTravel.ManorGate.center); Advance(1); break;
                    case 1:
                        Check(arena.Travel.IsSelectionOpen, "Gate did not open.");
                        Enter(arena, WildernessCreature.Snake);
                        Physics2D.SyncTransforms();
                        pack = arena.Enemies.ToArray(); initial = new Vector2[pack.Length];
                        for (int i = 0; i < pack.Length; i++)
                        {
                            var enemy = pack[i]; initial[i] = enemy.transform.position;
                            Check(enemy.Alive && !enemy.rockBear && enemy.GetComponent<BearCombat>() == null, "Snake uses bear combat.");
                            Check(enemy.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic, "Snake can be pushed.");
                            Check(enemy.GetComponent<SnakeDemon>().Navigation.Walkable(initial[i]), "Snake overlaps the boundary.");
                            Check(enemy.transform.Find("Visual/Jade snake skin").GetComponent<SpriteRenderer>().sprite != null, "Missing snake sprite.");
                        }
                        Check(Mathf.Abs(initial[0].y-initial[2].y)<.001f && Mathf.Abs(initial[1].x-initial[0].x-initial[2].x+initial[1].x)<.001f,
                            "Pack formation changed.");
                        Advance(2, 1.5); break;
                    case 2:
                        for (int i = 0; i < pack.Length; i++) Check(Vector2.Distance(initial[i], pack[i].transform.position) > .5f, "Snake did not pursue.");
                        arena.Player.Teleport((Vector2)pack[1].transform.position + Vector2.down * 1.65f);
                        health = arena.Player.Health;
                        Advance(3, 1.6); break;
                    case 3:
                        Check(arena.Player.Health < health, "Bite did not damage the player.");
                        pack[1].TakeHit(10, Vector2.up, 10, true);
                        Check(pack[1].Health == pack[1].maxHealth - 10, "Snake did not take damage.");
                        arena.TogglePause(); health = arena.Player.Health;
                        for (int i = 0; i < pack.Length; i++) initial[i] = pack[i].transform.position;
                        Advance(4, .7); break;
                    case 4:
                        Check(health == arena.Player.Health, "Pause allowed damage.");
                        for (int i = 0; i < pack.Length; i++) Check(Vector2.Distance(initial[i], pack[i].transform.position) < .01f, "Pause allowed movement.");
                        var camera = arena.WorldCamera;
                        camera.transform.position = pack[1].transform.position + arena.View.Up * 1.4f - ObliqueProjection.Forward(arena.View.elevation) * 20;
                        camera.orthographicSize = 4;
                        Directory.CreateDirectory("Verification/Artifacts");
                        var image = ManorOcclusionValidation.Capture(camera);
                        File.WriteAllBytes("Verification/Artifacts/JadeSnake-wilderness.png", image.EncodeToPNG());
                        UnityEngine.Object.Destroy(image);
                        arena.TogglePause();
                        pack[0].TakeHit(100000, Vector2.zero, 0);
                        Check(arena.Travel.Portal == null, "Portal spawned before clear.");
                        pack[1].TakeHit(100000, Vector2.zero, 0);
                        Check(arena.Travel.Portal == null, "Portal spawned before last kill.");
                        pack[2].TakeHit(100000, Vector2.zero, 0);
                        Check(arena.Travel.Portal == null, "Portal spawned before fourth snake died.");
                        pack[3].TakeHit(100000, Vector2.zero, 0);
                        Check(arena.Travel.Portal != null && arena.Enemies.Count == 0, "Snake clear did not spawn portal.");
                        Advance(5, .8); break;
                    case 5:
                        Check(arena.Travel.ActiveMap.GetComponentsInChildren<SlashEnemy>().Length == 0, "Snake respawned in cleared map.");
                        arena.TogglePause(); arena.ReturnHome();
                        Check(arena.Enemies.Count == 3, "Manor dummies missing.");
                        arena.Player.Teleport(SceneTravel.ManorGate.center); Advance(6); break;
                    case 6:
                        Enter(arena, WildernessCreature.Bear);
                        BearPackValidation.CheckFormation(arena);
                        BearAllyChargeValidation.Verify(arena);
                        BearSpacingValidation.Verify(arena);
                        arena.TogglePause(); arena.ReturnHome();
                        arena.Player.Teleport(SceneTravel.ManorGate.center); Advance(7); break;
                    case 7:
                        Check(arena.Travel.IsSelectionOpen, "Gate did not open for a rotation entry.");
                        arena.Travel.SelectDestination(TravelDestination.Wilderness);
                        Check(arena.Enemies.Count > 0, "Wilderness entry did not spawn a pack.");
                        var kind = PackKind(arena.Enemies[0]);
                        CheckPack(arena, kind);
                        Check(arena.Travel.ActiveMap.GetComponentsInChildren<SlashEnemy>().Length == arena.Enemies.Count, "Reentry duplicated enemies.");
                        if (encounterCount > 0) Check(kind != encounters[encounterCount - 1], "Two consecutive visits spawned the same creature.");
                        if (encounterCount > 1) Check(kind != encounters[encounterCount - 2], "Three consecutive visits repeated a creature.");
                        encounters[encounterCount++] = kind;
                        var old = arena.Travel.ActiveMap;
                        if (encounterCount == 3 || encounterCount == encounters.Length) arena.Restart();
                        else { arena.TogglePause(); arena.ReturnHome(); }
                        Check(!old.gameObject.activeInHierarchy && arena.Travel.Current == TravelDestination.Manor && arena.Enemies.Count == 3,
                            "Returning to the manor leaked wilderness enemies.");
                        if (encounterCount < encounters.Length)
                        {
                            arena.Player.Teleport(SceneTravel.ManorGate.center);
                            Advance(encounterCount == 2 ? 8 : 7); break;
                        }
                        SessionState.SetBool(Key, false);
                        Debug.Log("WILDERNESS_ENCOUNTER_PASS: " + string.Join(", ", encounters)
                            + "; no repeat in any three visits across returns, another destination and restart; snake combat and bear formation regressions passed.");
                        EditorApplication.Exit(0); break;
                    case 8:
                        arena.Travel.SelectDestination(TravelDestination.HealingLodge);
                        Check(arena.Travel.Current == TravelDestination.HealingLodge && arena.Enemies.Count == 0, "Healing lodge spawned wilderness enemies.");
                        arena.TogglePause(); arena.ReturnHome();
                        arena.Player.Teleport(SceneTravel.ManorGate.center); Advance(7); break;
                }
            }
            catch (Exception ex)
            { SessionState.SetBool(Key, false); Debug.LogError("WILDERNESS_ENCOUNTER_FAIL: stage=" + stage + " " + ex); EditorApplication.Exit(1); }
        }
    }
}
