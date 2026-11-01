using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class RevivalValidation
    {
        const string Key = "SlashDemo.RevivalValidation";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static SlashArena arena;
        static EditorWindow view;
        static int stage, scenario, checks, kills, wave;
        static double next, deadline;
        static SlashPlayer defeated;
        static TravelMapEnvironment oldMap;
        static TechniqueDefinition equipped;
        static CultivationRealm realm;
        static Vector2 click;
        static void Check(bool value, string message)
        { if (!value) throw new Exception(message); checks++; }

        public static void RunBatch()
        {
            SessionState.SetBool(Key + ".HadTechnique", PlayerPrefs.HasKey(SlashPlayer.TechniqueSaveKey));
            SessionState.SetString(Key + ".Technique", PlayerPrefs.GetString(SlashPlayer.TechniqueSaveKey, ""));
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
            Application.runInBackground = true;
        }
        [InitializeOnLoadMethod]
        static void Hook()
        {
            deadline = EditorApplication.timeSinceStartup + 120;
            next = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        static void QueueClick(EventType type)
        {
            var queue = typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Check(queue != null, "Native Game view input injection unavailable.");
            queue.Invoke(null, new object[] { new Event { type = type, button = 0, mousePosition = click } });
        }
        static void RestoreSavedSelection()
        {
            if (SessionState.GetBool(Key + ".HadTechnique", false))
                PlayerPrefs.SetString(SlashPlayer.TechniqueSaveKey, SessionState.GetString(Key + ".Technique", ""));
            else PlayerPrefs.DeleteKey(SlashPlayer.TechniqueSaveKey);
            PlayerPrefs.Save();
        }
        static void Capture(string name)
        {
            MethodInfo render = null;
            for (Type type = view.GetType(); type != null && render == null; type = type.BaseType)
                render = type.GetMethod("RenderView", Flags, null, new[] { typeof(Vector2), typeof(bool) }, null);
            var texture = render?.Invoke(view, new object[] { Vector2.zero, false }) as RenderTexture;
            Check(texture != null, "Game view did not render the revival UI.");
            var previous = RenderTexture.active;
            var readable = RenderTexture.GetTemporary(texture.width, texture.height, 0);
            Graphics.Blit(texture, readable, new Vector2(1, -1), new Vector2(0, 1));
            RenderTexture.active = readable;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
            Directory.CreateDirectory("Verification/Artifacts/Revival-v1");
            File.WriteAllBytes("Verification/Artifacts/Revival-v1/" + name + ".png", image.EncodeToPNG());
            UnityEngine.Object.Destroy(image); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(readable);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Revival validation timed out at " + stage);
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + .3;
                switch (stage)
                {
                    case 0:
                        arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
                        if (arena == null || arena.Travel == null || arena.Tuning == null) return;
                        view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                        view.position = new Rect(0, 0, 1600, 940); view.Show(); view.Focus();
                        arena.Tuning.Settings.maxHealth = 137;
                        arena.Tuning.Settings.invincible = false; arena.Tuning.ApplyAll();
                        var alive = arena.Player; arena.ReviveAtHome();
                        Check(arena.Player == alive, "Revival replaced a living player.");
                        stage = 1; break;
                    case 1:
                        if (scenario > 0)
                        {
                            arena.Player.Teleport(SceneTravel.ManorGate.center);
                            typeof(SceneTravel).GetMethod("OpenSelection", Flags).Invoke(arena.Travel, null);
                            WildernessEncounterValidation.Enter(arena, WildernessCreature.Snake);
                            oldMap = arena.Travel.ActiveMap;
                            Check(oldMap != null && arena.Enemies.Count == 4, "Wilderness revival setup failed.");
                        }
                        equipped = scenario == 0 ? TechniqueCatalog.FlowingCloud : TechniqueCatalog.IronSword;
                        realm = scenario == 0 ? CultivationRealm.GoldenCore : CultivationRealm.NascentSoul;
                        arena.Player.EquipTechnique(equipped, true); arena.Player.SetRealm(realm);
                        kills = arena.Kills; wave = arena.Wave;
                        stage = 2; break;
                    case 2:
                        defeated = arena.Player;
                        Check(defeated.TakeDamage(100000, Vector2.zero), "Lethal hit was rejected.");
                        Check(!defeated.IsAlive && arena.Defeated && arena.Paused && Time.timeScale == 0, "Death did not pause combat.");
                        Check(arena.InputBlocked && !arena.PauseMenuOpen && arena.GetComponent<SlashHUD>().DefeatPanelVisible,
                            "Death panel did not replace ordinary pause controls.");
                        arena.ResumeGame();
                        Check(arena.Paused && arena.Defeated, "Ordinary resume bypassed resurrection.");
                        stage = 3; break;
                    case 3:
                        Capture(scenario == 0 ? "Defeat-manor" : "Defeat-wilderness");
                        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
                        click = new Vector2(Screen.width * .5f, Screen.height * .5f + 68 * scale);
                        QueueClick(EventType.MouseDown);
                        stage = 4; break;
                    case 4:
                        QueueClick(EventType.MouseUp);
                        stage = 5; break;
                    case 5:
                        var player = arena.Player;
                        Check(player != defeated && player.IsAlive && player.Health == 137 && player.Health == player.maxHealth,
                            "Click did not revive at the tuned maximum health.");
                        Check(!arena.Defeated && !arena.Paused && Time.timeScale == 1 && !arena.GetComponent<SlashHUD>().DefeatPanelVisible,
                            "Resurrection left combat or the death overlay frozen.");
                        Check(arena.Travel.Current == TravelDestination.Manor && arena.Travel.ActiveMap == null
                            && Vector2.Distance(player.transform.position, ManorEnvironment.SpawnPoint) < .01f, "Resurrection did not return to the manor spawn.");
                        Check(player.Technique == TechniqueCatalog.FlowingCloud && player.Realm == CultivationRealm.QiRefining,
                            "Resurrection retained technique or realm instead of restoring initial progression.");
                        Check(PlayerPrefs.GetString(SlashPlayer.TechniqueSaveKey) == TechniqueCatalog.FlowingCloud.id,
                            "The previous technique can return from the saved selection after resurrection.");
                        Check(arena.Kills == kills && arena.Wave == wave, "Resurrection reset encounter statistics.");
                        Check(arena.CameraRig.target == player.transform && !player.IsAttacking && !player.IsHeavyBusy && !player.IsDashing,
                            "Camera or transient combat state still references the defeated player.");
                        Check(oldMap == null || !oldMap.gameObject.activeSelf, "Wilderness hazards remain active after resurrection.");
                        var revived = player; arena.ReviveAtHome();
                        Check(arena.Player == revived, "A second click created a duplicate player.");
                        Capture(scenario == 0 ? "Revived-manor" : "Revived-from-wilderness");
                        if (scenario++ == 0) stage = 1;
                        else stage = 6;
                        break;
                    case 6:
                        Check(!arena.InputBlocked, "Revival input lock did not release.");
                        Check(arena.Player.RequestAttack(Vector2.right), "Revived character cannot attack.");
                        Debug.Log("REVIVAL_PASS: " + checks + " checks; real death panel button clicks in manor/wilderness; tuned full HP; reset realm and technique including saved selection; preserved stats; camera; map cleanup; repeated click guard; input release and combat resume; UI screenshots.");
                        RestoreSavedSelection();
                        SessionState.SetBool(Key, false); EditorApplication.Exit(0); break;
                }
            }
            catch (Exception error)
            {
                Debug.LogError("REVIVAL_FAIL: stage=" + stage + " " + error);
                RestoreSavedSelection();
                SessionState.SetBool(Key, false); EditorApplication.Exit(1);
            }
        }
    }
}
