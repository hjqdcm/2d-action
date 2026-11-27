using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SlashDemo.Editor
{
    public static class SceneTravelValidation
    {
        const string Pending = "SlashDemo.SceneTravelValidation.Pending";
        static int stage, choice, afterFrame, health, swings, expectedFortune;
        static double next;
        static SlashPlayer original;
        static Quaternion rotation;
        static float zoom;
        static Mouse mouse;
        static Keyboard keyboard;
        static KeyboardState? queuedKeys;
        static MouseState? queuedMouse;
        static CultivationRealm OutboundRealm => Destination.destination == TravelDestination.Tribulation
            ? CultivationRealm.QiRefining : (CultivationRealm)(choice % (int)CultivationRealm.NascentSoul + 1);
        static TravelMapDefinition Destination => choice < TravelMaps.Choices.Length ? TravelMaps.Choices[choice]
            : TravelMaps.Get(TravelDestination.FinalBattle);

        // Inject edges into gameplay's input update, not the editor's separate state buffer.
        static void InjectGameplayInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            if (queuedKeys.HasValue)
            {
                InputSystem.QueueStateEvent(keyboard, queuedKeys.Value);
                queuedKeys = null;
            }
            if (queuedMouse.HasValue)
            {
                InputSystem.QueueStateEvent(mouse, queuedMouse.Value);
                queuedMouse = null;
            }
        }

        static void QueueKeys(KeyboardState state) { queuedKeys = state; }
        static void QueueMouse(MouseState state) { queuedMouse = state; }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + ".Launch", true);
            Application.runInBackground = true;
            next = EditorApplication.timeSinceStartup + 3;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
            next = EditorApplication.timeSinceStartup + 3;
        }

        static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            stage = -2;
            choice = 0;
            afterFrame = Time.frameCount + 4;
            // Batch mode has no focused Game view; route synthetic devices to gameplay explicitly.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            queuedKeys = null;
            queuedMouse = null;
            InputSystem.onBeforeUpdate -= InjectGameplayInput;
            InputSystem.onBeforeUpdate += InjectGameplayInput;
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        static void Advance(int value)
        {
            stage = value;
            afterFrame = Time.frameCount + 4;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.04;
            if (SessionState.GetBool(Pending + ".Launch", false))
            {
                SessionState.SetBool(Pending + ".Launch", false);
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying || Time.frameCount < afterFrame) return;
            var arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
            if (arena == null || arena.Player == null || arena.Travel == null) return;
            var travel = arena.Travel;
            try
            {
                switch (stage)
                {
                    case -2:
                        original = arena.Player;
                        Check(original.Realm == CultivationRealm.QiRefining && original.RealmDisplayName == "练气",
                            "Player did not start in Qi Refining.");
                        original.SetRealm(CultivationRealm.NascentSoul);
                        travel.ReturnHome();
                        Check(original.Realm == CultivationRealm.QiRefining,
                            "Returning home while already in the manor did not reset the realm.");
                        original.Teleport(ManorEnvironment.SpawnPoint);
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(-1);
                        break;
                    case -1:
                        Check(arena.PauseMenuOpen && arena.Paused && Time.timeScale == 0 && arena.InputBlocked,
                            "Esc did not open the paused menu; paused=" + arena.Paused
                            + ", queued=" + queuedKeys.HasValue + ", key=" + keyboard.escapeKey.isPressed
                            + ", update=" + InputSystem.settings.updateMode);
                        Directory.CreateDirectory("Verification/Artifacts");
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/Artifacts/Pause-menu.png"));
                        QueueKeys(new KeyboardState(Key.W));
                        Advance(-3);
                        break;
                    case -3:
                        Check(Vector2.Distance(original.transform.position, ManorEnvironment.SpawnPoint) < 0.001f,
                            "Movement continued behind the pause menu.");
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(-4);
                        break;
                    case -4:
                        Check(!arena.PauseMenuOpen && !arena.Paused && Time.timeScale > 0,
                            "Second Esc did not resume the game.");
                        QueueKeys(new KeyboardState());
                        Debug.Log("PAUSE_MENU_PASS: Esc opens/resumes, game simulation and movement pause behind menu.");
                        Advance(0);
                        break;
                    case 0:
                        Check(!travel.IsSelectionOpen && travel.Current == TravelDestination.Manor, "Spawn opened the destination picker.");
                        original = arena.Player;
                        Check(original.Realm == CultivationRealm.QiRefining, "Restart did not reset the realm.");
                        Check(arena.Resources.SpiritStones == 50 && arena.Resources.FortunePoints == 15,
                            "A new run must start with 50 spirit stones and 15 fortune points.");
                        Check(arena.Resources.FortuneSpent == 0 && arena.Resources.TribulationFortuneSpent == 0
                            && !arena.Resources.TribulationUnlocked,
                            "A new run retained previous tribulation unlock progress.");
                        Check(arena.Resources.TryChangeSpiritStones(-5) && arena.Resources.TryChangeFortunePoints(2),
                            "Could not set non-default balances for travel validation.");
                        Check(!travel.ShowsDestination(TravelDestination.HealingLodge) && !travel.ShowsDestination(TravelDestination.Market),
                            "Services were visible before spending fortune.");
                        if (SceneTravel.UsesServiceVisit(Destination.destination))
                            Check(arena.Resources.TryChangeFortunePoints(-10) && arena.Resources.TryChangeFortunePoints(10)
                                && arena.Resources.ServiceVisits == 2, "Gross spending did not grant two shared visits.");
                        if (Destination.destination == TravelDestination.Tribulation)
                        {
                            Check(arena.Resources.TryChangeFortunePoints(-11)
                                && arena.Resources.TryChangeFortunePoints(11)
                                && !arena.Resources.TryChangeFortunePoints(-arena.Resources.FortunePoints - 1)
                                && arena.Resources.FortuneSpent == 11 && !arena.Resources.TribulationUnlocked
                                && arena.Resources.ServiceVisits == 2, "Refunded or failed spending changed unlock progress or shared visits.");
                        }
                        expectedFortune = arena.Resources.FortunePoints - SceneTravel.FortuneCost(Destination.destination);
                        original.SetRealm(OutboundRealm);
                        original.invincible = false;
                        original.TakeDamage(12, Vector2.right);
                        health = original.Health;
                        arena.Tuning.Settings.moveSpeed = 7.1f;
                        arena.Tuning.ApplyAll();
                        Check(original.Realm == OutboundRealm, "Applying tuning overwrote the current realm.");
                        rotation = arena.WorldCamera.transform.rotation;
                        zoom = arena.View.zoom;
                        QueueKeys(new KeyboardState(Key.R, Key.T));
                        original.Teleport(SceneTravel.ManorGate.center);
                        Advance(1);
                        break;
                    case 1:
                        Check(arena.Player == original && arena.practiceMode, "R or T shortcut is still active.");
                        Check(travel.IsSelectionOpen && arena.Paused && Time.timeScale == 0 && arena.InputBlocked, "Gate picker did not pause and block combat.");
                        if (Destination.destination == TravelDestination.Tribulation)
                        {
                            Check(travel.ShowsDestination(TravelDestination.Tribulation)
                                && !travel.CanSelectDestination(TravelDestination.Tribulation), "The locked tribulation route was unavailable for preview or could already be selected.");
                            travel.SelectDestination(TravelDestination.Tribulation);
                            Check(travel.Current == TravelDestination.Manor && travel.IsSelectionOpen, "The locked route could be entered.");
                            Check(arena.Resources.TryChangeFortunePoints(-1)
                                && arena.Resources.TryChangeFortunePoints(1)
                                && arena.Resources.FortuneSpent == 12 && arena.Resources.TribulationUnlocked
                                && travel.CanSelectDestination(TravelDestination.Tribulation),
                                "The tribulation route did not unlock at exactly twelve gross points.");
                        }
                        travel.CancelSelection();
                        QueueKeys(new KeyboardState());
                        Advance(2);
                        break;
                    case 2:
                        Check(!travel.IsSelectionOpen && !arena.Paused, "Cancel immediately reopened the gate picker.");
                        original.Teleport(ManorEnvironment.Ground(680, 720));
                        Advance(3);
                        break;
                    case 3:
                        original.Teleport(SceneTravel.ManorGate.center);
                        Advance(4);
                        break;
                    case 4:
                        Check(travel.IsSelectionOpen, "Gate picker did not rearm after leaving.");
                        if (choice == 0)
                        {
                            QueueKeys(new KeyboardState(Key.Escape));
                            Advance(50);
                        }
                        else Advance(54);
                        break;
                    case 50:
                        Check(arena.PauseMenuOpen && travel.IsSelectionOpen && arena.Paused && Time.timeScale == 0,
                            "Esc did not open the pause menu over the gate picker.");
                        QueueKeys(new KeyboardState());
                        Advance(51);
                        break;
                    case 51:
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(52);
                        break;
                    case 52:
                        Check(!arena.PauseMenuOpen && travel.IsSelectionOpen && arena.Paused && Time.timeScale == 0 && arena.InputBlocked,
                            "Closing the pause menu with Esc did not restore the paused gate picker.");
                        QueueKeys(new KeyboardState());
                        Advance(53);
                        break;
                    case 53:
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(55);
                        break;
                    case 55:
                        Check(arena.PauseMenuOpen && travel.IsSelectionOpen, "Could not reopen pause over the gate picker.");
                        arena.ResumeGame();
                        Check(!arena.PauseMenuOpen && travel.IsSelectionOpen && arena.Paused && Time.timeScale == 0 && arena.InputBlocked,
                            "Continuing from the pause menu did not restore the paused gate picker.");
                        QueueKeys(new KeyboardState());
                        Debug.Log("TRAVEL_PICKER_PAUSE_PASS: Esc and Continue restore the paused destination picker.");
                        Advance(54);
                        break;
                    case 54:
                        swings = original.TotalSwings;
                        QueueMouse(new MouseState().WithButton(MouseButton.Left));
                        Advance(5);
                        break;
                    case 5:
                        Check(original.TotalSwings == swings, "Clicking while choosing triggered an attack.");
                        travel.SelectDestination(Destination.destination);
                        if (Destination.destination == TravelDestination.FinalBattle) CheckFinalBoss(arena, exerciseSkills: true);
                        Check(!travel.IsSelectionOpen && !arena.Paused, "Destination selection did not close picker.");
                        Check(arena.Resources.SpiritStones == 45 && arena.Resources.FortunePoints == expectedFortune,
                            "Selecting a route charged the wrong balance.");
                        if (SceneTravel.UsesServiceVisit(Destination.destination))
                            Check(arena.Resources.ServiceVisits == 1, "Service entry did not use one shared visit.");
                        Check(arena.Player == original && original.Health == health, "Travel replaced the character or changed health.");
                        Check(original.Realm == OutboundRealm, "Outbound travel changed the realm.");
                        Check(Mathf.Abs(original.moveSpeed - 7.1f) < 0.001f, "Travel lost character tuning.");
                        Check(Vector2.Distance(original.transform.position, Destination.Entry) < 0.001f, "Travel did not arrive at map entrance.");
                        Check(Quaternion.Angle(rotation, arena.WorldCamera.transform.rotation) < 0.001f && arena.View.zoom == zoom, "Travel changed camera projection.");
                        Check(arena.InputBlocked, "Selection click can leak through into combat.");
                        Advance(6);
                        break;
                    case 6:
                        Check(original.TotalSwings == swings, "Held selection click triggered an attack after travel.");
                        QueueMouse(new MouseState());
                        Advance(7);
                        break;
                    case 7:
                        Check(!travel.BlocksGameplayInput, "Input did not resume after releasing the selection click.");
                        foreach (var enemy in arena.Enemies)
                            Check(enemy == null || enemy.transform.IsChildOf(travel.ActiveMap.transform), "Manor enemy remained registered in the new map.");
                        Check(!arena.GetComponentInChildren<ManorEnvironment>(true).gameObject.activeInHierarchy, "Manor colliders remained active.");
                        ValidateMap(travel.ActiveMap, original.GetComponent<CircleCollider2D>().radius);
                        arena.CameraRig.ResetFeedback();
                        ManorValidation.Render(arena, Path.GetFullPath("Verification/Artifacts/Travel-" + travel.Current + ".png"));
                        if (travel.Current == TravelDestination.HealingLodge) HealingLodgeValidation.Run(arena);
                        original.Teleport(travel.ActiveMap.ReservedExit.transform.position);
                        Advance(8);
                        break;
                    case 8:
                        Check(travel.Current == Destination.destination && !travel.IsSelectionOpen, "Reserved exit unexpectedly teleported the player.");
                        Debug.Log("SCENE_TRAVEL_DESTINATION_PASS: " + travel.Current + "; entry, routes, character state, camera, click suppression, inactive reserved exit.");
                        if (travel.Current == TravelDestination.Tribulation)
                        {
                            Check(arena.Enemies.Count == 0 && travel.Portal == null && !arena.InCombat,
                                "Tribulation must arrive with neither a portal nor combat.");
                            var center = TravelMaps.TribulationCenter;
                            original.Teleport(travel.ActiveMap.Definition.Ground(center.x, center.y));
                            Check(travel.ShowTribulationPrompt, "The tribulation light is not interactable at the center.");
                            QueueKeys(new KeyboardState(Key.F));
                            Advance(60);
                            break;
                        }
                        if (travel.Current == TravelDestination.HealingLodge)
                        {
                            Check(travel.Portal == null && !travel.ShowTreatmentPrompt, "Treatment must start at the bed, with no portal yet.");
                            original.Teleport(travel.ActiveMap.Definition.Ground(550, 310));
                            QueueKeys(new KeyboardState(Key.F));
                            Advance(20);
                            break;
                        }
                        if (travel.Current == TravelDestination.Market)
                        {
                            var definition = travel.ActiveMap.Definition;
                            Check(definition.WorldSize == new Vector2(27, 18), "The market must be 27 by 18 world units.");
                            Check(arena.Enemies.Count == 0 && travel.Portal != null &&
                                travel.ActiveMap.GetComponentsInChildren<TeleportFormation>().Length == 1,
                                "The market must start with one portal and no enemies.");
                            var marker = TravelMaps.MarketPortal;
                            Check(Vector2.Distance(travel.Portal.transform.position, definition.Ground(marker.x, marker.y)) < .01f,
                                "The market portal did not fit at the foot of the entrance steps.");
                            original.Teleport(definition.Entry);
                            Check(travel.ShowPortalPrompt, "The market arrival point is out of reach of its portal.");
                            QueueKeys(new KeyboardState(Key.F));
                            Advance(30);
                            break;
                        }
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(9);
                        break;
                    case 60:
                        Check(travel.IsTribulationDecisionOpen && arena.Paused && !arena.PauseMenuOpen,
                            "F did not open the paused tribulation confirmation.");
                        travel.ResolveTribulation(false);
                        Check(!travel.HasModal && !arena.InCombat && travel.Portal != null
                            && Vector2.Distance(travel.Portal.transform.position, travel.ActiveMap.Definition.Entry) < .01f,
                            "Declining did not spawn an exit at the tribulation entrance.");
                        QueueKeys(new KeyboardState());
                        Advance(62);
                        break;
                    case 62:
                        Check(travel.ShowTribulationPrompt, "Declining removed the center light.");
                        QueueKeys(new KeyboardState(Key.F));
                        Advance(63);
                        break;
                    case 63:
                        Check(travel.IsTribulationDecisionOpen, "The light did not reopen the confirmation.");
                        var tribulationExit = travel.Portal;
                        travel.ResolveTribulation(true);
                        Check(travel.IsTribulationActive && arena.InCombat && travel.TribulationRound == 1
                            && travel.Portal == null && !tribulationExit.gameObject.activeSelf,
                            "Starting did not begin round one and remove the exit.");
                        ValidateTribulation(arena);
                        QueueKeys(new KeyboardState());
                        Advance(61);
                        break;
                    case 61:
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(9);
                        break;
                    case 30:
                        Check(travel.IsSelectionOpen && arena.Paused, "The market portal did not open the route picker.");
                        travel.CancelSelection();
                        QueueKeys(new KeyboardState());
                        Advance(31);
                        break;
                    case 31:
                        var pillOrb = travel.ActiveMap.transform.Find("丹药摊光球");
                        Check(pillOrb != null && travel.ActiveMap.transform.Find("法器摊光球") != null,
                            "Both market stalls must have an interaction orb.");
                        Check(!travel.ShowShopPrompt, "The shop prompt is visible from the entrance portal.");
                        original.Teleport(pillOrb.position);
                        Check(travel.ShowShopPrompt && !travel.ShowPortalPrompt, "Wrong F prompt at the pill stall.");
                        QueueKeys(new KeyboardState(Key.F));
                        Advance(32);
                        break;
                    case 32:
                        Check(travel.IsShopOpen && arena.Paused && arena.InputBlocked && !arena.PauseMenuOpen,
                            "F at the pill stall did not open the shop.");
                        QueueKeys(new KeyboardState());
                        Advance(33);
                        break;
                    case 33:
                        Check(travel.IsShopOpen, "Releasing F closed the shop.");
                        QueueKeys(new KeyboardState(Key.F));
                        Advance(34);
                        break;
                    case 34:
                        Check(!travel.IsShopOpen && !arena.Paused && arena.InputBlocked,
                            "Pressing F again did not close the shop and consume the held key.");
                        QueueKeys(new KeyboardState());
                        Advance(35);
                        break;
                    case 35:
                        original.Teleport(travel.ActiveMap.transform.Find("法器摊光球").position);
                        Check(travel.ShowShopPrompt, "Missing F prompt at the treasure stall.");
                        QueueKeys(new KeyboardState(Key.F));
                        Advance(36);
                        break;
                    case 36:
                        Check(travel.IsShopOpen && arena.Paused && arena.InputBlocked, "F at the treasure stall did not open the shop.");
                        Check(arena.Resources.SpiritStones == 45, "Browsing the shop changed the spirit stone balance.");
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(37);
                        break;
                    case 37:
                        Check(!travel.IsShopOpen && !arena.Paused, "Esc did not close the treasure stall shop.");
                        QueueKeys(new KeyboardState());
                        Advance(38);
                        break;
                    case 38:
                        Debug.Log("MARKET_SHOP_PASS: F opens at both orbs, F and Esc close the shop, balance is preserved.");
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(9);
                        break;
                    case 20:
                        Check(travel.IsTreatmentOpen && arena.Paused && arena.InputBlocked && !arena.PauseMenuOpen,
                            "F at the bed did not open a modal treatment choice.");
                        Check(SceneTravel.TreatmentCost == 0, "Treatment must be free.");
                        travel.ResolveTreatment(false);
                        Check(original.Health == health && travel.Portal != null && !arena.Paused && !travel.IsTreatmentOpen,
                            "Declining treatment changed health or failed to spawn the portal.");
                        Check(Vector2.Distance(travel.Portal.transform.position, travel.ActiveMap.Definition.Ground(965, 574)) < .01f,
                            "The healing lodge portal did not fit outside the treatment room.");
                        QueueKeys(new KeyboardState());
                        Advance(21);
                        break;
                    case 21:
                        Check(!travel.ShowTreatmentPrompt && !travel.TryOpenTreatment(), "The treatment marker remained usable after resolving.");
                        original.Teleport(travel.Portal.transform.position);
                        QueueKeys(new KeyboardState(Key.F));
                        Advance(22);
                        break;
                    case 22:
                        Check(travel.IsSelectionOpen && arena.Paused, "The lodge portal did not open the standard travel picker.");
                        travel.SelectDestination(TravelDestination.HealingLodge);
                        Check(arena.Resources.ServiceVisits == 0 && travel.ShowsDestination(TravelDestination.Market)
                            && !travel.CanSelectDestination(TravelDestination.Market), "Both services must share the same pool after unlocking.");
                        Check(travel.Portal == null, "Revisiting the lodge reused its previous portal.");
                        original.Teleport(travel.ActiveMap.Definition.Ground(550, 310));
                        original.TakePoisonDamage(original.Health - Mathf.CeilToInt(original.maxHealth * .25f));
                        health = original.Health;
                        QueueKeys(new KeyboardState());
                        Advance(23);
                        break;
                    case 23:
                        Check(travel.TryOpenTreatment(), "The treatment marker did not reappear on a new visit.");
                        travel.ResolveTreatment(true);
                        health += Mathf.CeilToInt(original.maxHealth * .5f);
                        Check(original.Health == health && travel.Portal != null && !travel.HasModal && arena.InputBlocked,
                            "Treatment did not heal half of maximum health and open the portal.");
                        travel.ResolveTreatment(true);
                        Check(original.Health == health, "Treatment could be resolved twice in one visit.");
                        Debug.Log("HEALING_LODGE_TREATMENT_PASS: F interaction, heal and decline, once per visit, courtyard portal and shared travel picker.");
                        QueueKeys(new KeyboardState(Key.Escape));
                        Advance(9);
                        break;
                    case 9:
                        Check(arena.PauseMenuOpen && arena.Paused, "Esc did not open pause menu while away from home.");
                        swings = original.TotalSwings;
                        QueueMouse(new MouseState().WithButton(MouseButton.Left));
                        arena.ReturnHome();
                        Check(arena.Player == original && original.Health == health, "Return Home reset the player or health.");
                        var homeRealm = Destination.destination == TravelDestination.Tribulation
                            ? CultivationRealm.FoundationEstablishment : CultivationRealm.QiRefining;
                        Check(original.Realm == homeRealm && original.RealmDisplayName == SlashPlayer.GetRealmName(homeRealm),
                            "Return Home did not restore the realm earned this run.");
                        arena.Tuning.ApplyAll();
                        Check(original.Realm == homeRealm, "Tuning reapplied the old realm after returning home.");
                        Check(Mathf.Abs(original.moveSpeed - 7.1f) < 0.001f, "Return Home lost character tuning.");
                        Check(travel.Current == TravelDestination.Manor && travel.ActiveMap == null, "Return Home did not unload destination map.");
                        Check(Vector2.Distance(original.transform.position, ManorEnvironment.SpawnPoint) < 0.001f, "Return Home missed the central courtyard.");
                        Check(!arena.PauseMenuOpen && !arena.Paused && Time.timeScale > 0, "Return Home did not resume the game.");
                        QueueKeys(new KeyboardState());
                        Advance(10);
                        break;
                    case 10:
                        Check(original.TotalSwings == swings && arena.InputBlocked, "Return Home click leaked into an attack.");
                        Check(arena.GetComponentInChildren<ManorEnvironment>().gameObject.activeInHierarchy, "Manor was not restored.");
                        foreach (var enemy in arena.Enemies)
                            Check(enemy != null && enemy.gameObject.activeInHierarchy, "Home practice dummies were not restored.");
                        Check(Quaternion.Angle(rotation, arena.WorldCamera.transform.rotation) < 0.001f && arena.View.zoom == zoom,
                            "Return Home changed camera projection.");
                        QueueMouse(new MouseState());
                        Advance(11);
                        break;
                    case 11:
                        Check(!arena.InputBlocked && !travel.IsSelectionOpen, "Controls did not resume after returning home.");
                        Check(arena.Resources.SpiritStones == 45 && arena.Resources.FortunePoints == expectedFortune,
                            "Returning home must preserve both resource balances.");
                        if (Destination.destination == TravelDestination.Tribulation)
                        {
                            Check(arena.Resources.TribulationUnlocked && arena.Resources.FortuneSpent == 12,
                                "Returning home lost tribulation unlock progress.");
                            original.Teleport(SceneTravel.ManorGate.center);
                            Advance(64);
                            break;
                        }
                        Debug.Log("PAUSE_RETURN_HOME_PASS: " + Destination.destination + "; same player/health/tuning/camera, manor and dummies restored, menu closed, no click-through.");
                        goto case 12;
                    case 64:
                        Check(travel.IsSelectionOpen && !travel.ShowsDestination(TravelDestination.Tribulation)
                            && !travel.CanSelectDestination(TravelDestination.Tribulation),
                            "The tribulation route reappeared after returning home, before beating Blood Saber.");
                        travel.SelectDestination(TravelDestination.Tribulation);
                        Check(travel.Current == TravelDestination.Manor && travel.IsSelectionOpen,
                            "The hidden tribulation route could still be selected.");
                        travel.SelectDestination(TravelDestination.FinalBattle);
                        Advance(65);
                        break;
                    case 65:
                        var bloodSaber = CheckFinalBoss(arena);
                        var fortunes = arena.Resources;
                        int oldVisits = fortunes.ServiceVisits;
                        bloodSaber.TakeHit(100000, Vector2.zero, 0);
                        Check(bloodSaber.Alive && !travel.ShowsDestination(TravelDestination.Tribulation),
                            "Blood Saber's phase transition unlocked tribulation before victory.");
                        bloodSaber.SendMessage("Tick", 4f);
                        bloodSaber.SendMessage("Tick", .75f);
                        bloodSaber.TakeHit(100000, Vector2.zero, 0);
                        Check(!bloodSaber.Alive && travel.Portal != null
                            && travel.ShowsDestination(TravelDestination.Tribulation)
                            && !travel.CanSelectDestination(TravelDestination.Tribulation)
                            && fortunes.TribulationFortuneSpent == 0,
                            "Defeating Blood Saber did not show the locked route with a fresh spending counter.");
                        Check(fortunes.FortunePoints == expectedFortune && fortunes.FortuneSpent == 12 && fortunes.ServiceVisits == oldVisits,
                            "Resetting tribulation spending changed the fortune balance or service progress.");
                        Check(fortunes.TryChangeFortunePoints(-11) && fortunes.TribulationFortuneSpent == 11
                            && !travel.CanSelectDestination(TravelDestination.Tribulation),
                            "The reopened tribulation route unlocked before twelve new points were spent.");
                        Check(fortunes.TryChangeFortunePoints(-1) && fortunes.TribulationFortuneSpent == 12
                            && travel.CanSelectDestination(TravelDestination.Tribulation),
                            "The reopened tribulation route did not unlock after twelve new points were spent.");
                        Debug.Log("TRIBULATION_TRAVEL_PASS: initial unlock, ten waves, Foundation Establishment, hidden until Blood Saber falls, then twelve new points to unlock.");
                        goto case 12;
                    case 12:
                        choice++;
                        if (choice <= TravelMaps.Choices.Length)
                        {
                            arena.Restart();
                            Advance(0);
                        }
                        else
                        {
                            arena.Restart();
                            Check(arena.Resources.TryChangeFortunePoints(1 - arena.Resources.FortunePoints), "Could not set up last wilderness trip.");
                            arena.Player.Teleport(SceneTravel.ManorGate.center);
                            Advance(40);
                        }
                        break;
                    case 40:
                        Check(travel.IsSelectionOpen && !travel.CanSelectDestination(TravelDestination.AscensionPlatform),
                            "Picker did not reject the unaffordable ascension trip.");
                        travel.SelectDestination(TravelDestination.Wilderness);
                        Check(arena.Resources.FortunePoints == 0 && travel.Current == TravelDestination.Wilderness,
                            "Last fortune point must pay for the wilderness encounter.");
                        Advance(41);
                        break;
                    case 41:
                        Check(travel.Current == TravelDestination.Wilderness && arena.Enemies.Count > 0,
                            "Exhaustion interrupted an active encounter.");
                        int stonesBeforeClear = arena.Resources.SpiritStones;
                        foreach (var enemy in new System.Collections.Generic.List<SlashEnemy>(arena.Enemies))
                            enemy.TakeHit(100000, Vector2.zero, 0);
                        int stonesFromClear = arena.Resources.SpiritStones - stonesBeforeClear;
                        Check(stonesFromClear >= 10 && stonesFromClear <= 30 && travel.IsLootChoiceOpen && arena.Paused
                            && !travel.TryOpenPortal(), "Wilderness did not award stones and block travel until the pill was chosen.");
                        Check(!travel.TryClaimLoot(3) && travel.TryClaimLoot(2) && !travel.TryClaimLoot(0)
                            && !travel.IsLootChoiceOpen, "The wilderness must grant one of the three pills exactly once.");
                        Advance(42);
                        break;
                    case 42:
                        Check(travel.Current == TravelDestination.FinalBattle && travel.ActiveMap != null,
                            "Exhaustion did not send the player to the final arena after combat.");
                        arena.Player.invincible = false;
                        Check(arena.Player.TakePoisonDamage(arena.Player.EffectiveMaxHealth / 2),
                            "Could not lower the player's health before testing the potion hotkey.");
                        health = arena.Player.Health;
                        QueueKeys(new KeyboardState(Key.Digit1));
                        Advance(44);
                        break;
                    case 44:
                        bool potionActive = false;
                        foreach (var buff in arena.GetComponent<SlashHUD>().VisibleBuffs)
                            if (buff.id.StartsWith("SlashDemo/Pills/", StringComparison.Ordinal) && buff.duration == 60
                                && buff.RemainingFraction > 0)
                                potionActive = true;
                        Check(potionActive || arena.Player.Health > health,
                            "The wilderness reward did not auto-fill slot one and take effect after using the hotkey.");
                        QueueKeys(new KeyboardState());
                        var boss = CheckFinalBoss(arena);
                        boss.TakeHit(100000, Vector2.zero, 0);
                        Check(boss.Health == 1 && boss.GetComponent<EvilCultivatorBoss>().CastingSkill == "血雾升腾"
                            && travel.Portal == null, "Lethal damage bypassed the first arena fog transition.");
                        boss.SendMessage("Tick", 4f);
                        boss.SendMessage("Tick", .75f);
                        boss.TakeHit(100000, Vector2.zero, 0);
                        Check(arena.Enemies.Count == 0 && travel.Portal != null && !boss.GetComponent<ObliqueActor>().GroundWarning.gameObject.activeSelf,
                            "Final boss defeat did not clear combat and create the exit portal.");
                        CheckFinalBattleGate(arena, boss, closed: false);
                        var exit = travel.Portal;
                        travel.EnemyDefeated(boss);
                        Check(travel.Portal == exit, "Final boss defeat created more than one exit portal.");
                        arena.Player.Teleport(exit.transform.position);
                        Advance(43);
                        break;
                    case 43:
                        Check(travel.TryOpenPortal(), "Victory portal did not reopen travel.");
                        travel.SelectDestination(TravelDestination.FinalBattle);
                        CheckFinalBoss(arena);
                        SessionState.SetBool(Pending, false);
                        InputSystem.onBeforeUpdate -= InjectGameplayInput;
                        InputSystem.RemoveDevice(mouse);
                        InputSystem.RemoveDevice(keyboard);
                        Debug.Log("CULTIVATION_REALM_PASS: starts/restarts in Qi Refining, outbound travel and tuning retain the current realm, and returning home restores the realm earned this run.");
                        Debug.Log("SCENE_TRAVEL_PASS: all destinations, gate/cancel/rearm, R/T disabled, reserved exits have no destination, market portal and shop access.");
                        Debug.Log("FORTUNE_TRAVEL_PASS: route costs, shared visits, voluntary and automatic final battle travel.");
                        Debug.Log("FINAL_BOSS_PASS: Blood Saber on each entry path, 2000 base health, reduced direct damage, 10x lifesteal, homing blood dagger with impact and expiry bursts, eight movement facings, invulnerable quarter-health transition, and a single victory portal.");
                        EditorApplication.Exit(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                SessionState.SetBool(Pending, false);
                InputSystem.onBeforeUpdate -= InjectGameplayInput;
                Debug.LogError("SCENE_TRAVEL_FAIL: " + exception);
                EditorApplication.Exit(1);
            }
        }

        static void ValidateTribulation(SlashArena arena)
        {
            var travel = arena.Travel;
            var player = arena.Player;
            var challenge = travel.ActiveMap.transform.Find("渡劫");
            var zones = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in challenge)
                if (child.name == "落雷区域") zones.Add(child);
            Check(zones.Count == 9 && challenge.Find("雷云") != null, "Tribulation did not create its storm and nine warning areas.");
            float firstRadius = zones[0].Find("落雷边界").localScale.x;
            Vector2 center = zones[0].position;
            Vector2 safePoint = travel.ActiveMap.ReservedExit.transform.position;
            Check(Vector2.Distance(center, player.transform.position) < .001f, "The first warning did not target the player's position.");
            zones[1].position = center + Vector2.right * (firstRadius * .5f);

            int startingHealth = player.Health, originalMax = player.maxHealth;
            bool wasInvincible = player.invincible;
            player.maxHealth = (originalMax / 10 + 1) * 10 + 1;
            player.invincible = false;
            int damage = Mathf.CeilToInt(player.EffectiveMaxHealth * .1f);
            challenge.SendMessage("Advance", .79f);
            Check(player.Health == startingHealth, "Tribulation struck before the 0.8-second warning ended.");
            challenge.SendMessage("Advance", .02f);
            Check(player.Health == startingHealth - damage, "Overlapping strikes must deal a single rounded-up 10% hit.");
            challenge.SendMessage("Advance", .1f);
            Check(player.Health == startingHealth - damage, "Lingering lightning applied damage again.");
            player.maxHealth = originalMax;
            player.invincible = wasInvincible;
            player.Heal(damage);
            Check(player.Health == startingHealth, "Tribulation fixture failed to restore test health.");

            for (int round = 2; round <= 10; round++)
            {
                player.Teleport(center + Vector2.right * (.5f * (round - 1)));
                challenge.SendMessage("Advance", 1f);
                challenge.SendMessage("Advance", 1f);
                Check(Vector2.Distance(zones[0].position, player.transform.position) < .001f,
                    "The new round did not target the player's current position.");
                float radius = zones[0].Find("落雷边界").localScale.x;
                Check(travel.TribulationRound == round && Mathf.Abs(radius - firstRadius * (1 + .04f * (round - 1))) < .001f,
                    "Tribulation rounds or radius growth are incorrect.");
                player.Teleport(safePoint);
                challenge.SendMessage("Advance", 2f);
                Check(player.Health == startingHealth && travel.Portal == null, "A safe area caused damage or the portal returned early.");
                Check(player.Realm == CultivationRealm.QiRefining, "Tribulation advanced the realm before the encounter ended.");
            }

            challenge.SendMessage("Advance", 1f);
            Check(travel.TribulationCleared && !arena.InCombat && !challenge.gameObject.activeSelf
                && travel.Portal != null && Vector2.Distance(travel.Portal.transform.position, travel.ActiveMap.Definition.Entry) < .01f,
                "Surviving ten rounds did not clear the storm and restore the exit.");
            Check(player.Realm == CultivationRealm.FoundationEstablishment && !travel.ShowsDestination(TravelDestination.Tribulation)
                && !travel.CanSelectDestination(TravelDestination.Tribulation), "Clearing tribulation did not promote the player and hide the route.");
            player.Teleport(travel.Portal.transform.position);
        }

        static SlashEnemy CheckFinalBoss(SlashArena arena, bool exerciseSkills = false)
        {
            var travel = arena.Travel;
            Check(travel.Current == TravelDestination.FinalBattle && travel.Portal == null && arena.Enemies.Count == 1,
                "Final battle must begin with one live opponent and no exit portal.");
            var enemy = arena.Enemies[0];
            var boss = enemy.GetComponent<EvilCultivatorBoss>();
            Check(enemy.Alive && boss != null && boss.CombatStyle == EvilCultivatorBoss.Style.BloodSaber
                && boss.transform.IsChildOf(travel.ActiveMap.transform),
                "The final battle did not spawn Blood Saber in its map.");
            var passive = typeof(EvilCultivatorBoss).GetProperty("PassiveBuff",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check(passive.GetValue(boss) is CombatBuffView lifesteal && lifesteal.duration == 0
                && Resources.Load<Texture2D>(lifesteal.iconResource) != null,
                "The permanent Blood Saber lifesteal status must have a HUD icon.");
            int expectedHealth = Mathf.RoundToInt(2000 * arena.Tuning.Settings.enemyHealthMultiplier);
            Check(enemy.maxHealth == expectedHealth, "Blood Saber did not use 2000 as its base health.");
            var skin = boss.transform.Find("Visual/Boss art").GetComponent<SpriteRenderer>();
            Check(skin.sprite.texture == Resources.Load<Texture2D>("SlashDemo/Cultivators/BloodSaberCultivator-Directions-v1"),
                "The final boss is using the wrong artwork.");
            Check(boss.GetComponent<WildernessNavigation>().Walkable(boss.transform.position),
                "Final boss spawn overlaps the battlefield boundary.");
            CheckFinalBattleGate(arena, enemy, closed: true);
            if (exerciseSkills)
            {
                CheckBossLocomotion(arena, enemy, skin);
                CheckBossSkills(arena, enemy, boss);
                CheckBloodMist(arena, enemy, boss, skin);
                CheckBloodDagger(arena, enemy, boss);
                CheckBloodFrenzy(arena, enemy, boss);
                CheckBloodSaberStages(arena, enemy, boss);
            }
            return enemy;
        }

        static void CheckFinalBattleGate(SlashArena arena, SlashEnemy enemy, bool closed)
        {
            var map = arena.Travel.ActiveMap;
            Vector2 entry = map.Definition.Entry;
            Vector2 passage = map.Definition.Ground(755, 970);
            Vector2 path = passage - entry;
            float radius = arena.Player.GetComponent<CircleCollider2D>().radius;
            bool blocked = false;
            Physics2D.SyncTransforms();
            foreach (var hit in Physics2D.CircleCastAll(entry, radius, path.normalized, path.magnitude))
                blocked |= BlocksRoute(hit.collider, map);
            Check(blocked == closed && enemy.GetComponent<WildernessNavigation>().Walkable(passage) != closed,
                "The final arena gate did not block the passage during combat and reopen after victory.");
        }

        static void CheckBloodMist(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss, SpriteRenderer skin)
        {
            var player = arena.Player;
            var body = boss.GetComponent<Rigidbody2D>();
            var navigation = boss.GetComponent<WildernessNavigation>();
            var warning = boss.GetComponent<ObliqueActor>().GroundWarning;
            var gait = skin.GetComponent<HeroWalkMesh>();
            var blade = skin.transform.Find("Saber blade");
            var bladeMesh = blade.GetComponent<MeshFilter>().sharedMesh;
            var immunity = typeof(SlashPlayer).GetField("invulnerability",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var aim = typeof(SlashPlayer).GetProperty(nameof(SlashPlayer.Aim));
            Vector2 start = body.position, previousPlayerPosition = player.transform.position;
            // Stay beyond the regular rotation's range to isolate the ambush timer.
            Vector2 distant = start + Vector2.right * 7f;
            Vector2 previousAim = player.Aim;
            object previousImmunity = immunity.GetValue(player);
            int previousHealth = player.Health, previousDamage = enemy.damage;
            int previousBossHealth = enemy.Health;
            bool invincible = player.invincible;
            GameObject blocker = null;

            void StartAmbush()
            {
                body.position = boss.transform.position = start;
                boss.SendMessage("Stop");
                player.Teleport(distant);
                aim.SetValue(player, Vector2.left);
                Physics2D.SyncTransforms();
                boss.SendMessage("Tick", 4.99f);
                Check(enemy.IsChasing && !warning.gameObject.activeSelf,
                    "Blood Saber used the ambush in the first five seconds.");
                boss.SendMessage("Tick", .02f);
                Check(boss.CastingSkill == "血雾突袭" && warning.gameObject.activeSelf,
                    "Blood Saber did not telegraph the ambush at range.");
                boss.SendMessage("Tick", .30f);
                Check(skin.color.a < .3f && Vector2.Distance(body.position, start) < .01f,
                    "Blood Saber moved before dissolving into mist.");
                boss.SendMessage("Tick", .13f);
                Check(skin.color.a == 1 && navigation.Walkable(body.position) && body.position.x > distant.x + .4f
                    && warning.gameObject.activeSelf && boss.CastingSkill == "血雾突袭",
                    "Blood Saber did not reappear behind the player with a slash warning.");
            }

            try
            {
                player.invincible = false;
                immunity.SetValue(player, 0f);
                enemy.damage = 9;
                const int slashDamage = 3;
                enemy.TakeHit(100, Vector2.zero, 0);
                int bossHealthBeforeSlash = enemy.Health;
                StartAmbush();
                Vector2 unobstructed = body.position;
                Check(Mathf.Abs(unobstructed.y - distant.y) < .05f && player.Health == previousHealth,
                    "The open rear landing was offset or dealt damage on arrival.");
                gait.Refresh();
                Vector2 restAxis = SaberRestAxis(bladeMesh);
                Vector2 restEdge = -Mathf.Sign(restAxis.x) * Vector2.Perpendicular(restAxis);
                Vector3 ready = blade.TransformDirection(restAxis);
                for (int frame = 0; frame < 13; frame++)
                {
                    boss.SendMessage("Tick", .03f);
                    gait.Refresh();
                    Vector3 axis = blade.TransformDirection(restAxis);
                    Vector3 edge = blade.TransformDirection(restEdge);
                    Vector3 leading = Vector3.Cross(Vector3.forward, axis).normalized;
                    Check(Vector3.Dot(edge, leading) > .98f, "The ambush moved spine-first instead of cutting along the fan.");
                }
                Check(Vector3.Angle(ready, blade.TransformDirection(restAxis)) > 40,
                    "Blood Saber did not sweep the blade across the fan after reappearing.");
                Check(player.Health == previousHealth && warning.gameObject.activeSelf,
                    "The quick slash resolved before its 0.4-second warning ended.");
                boss.SendMessage("Tick", .02f);
                Check(player.Health == previousHealth - slashDamage && !warning.gameObject.activeSelf,
                    "The quick slash did not hit once for the reduced damage at the end of its warning.");
                Check(enemy.Health == bossHealthBeforeSlash + 10 * slashDamage,
                    "Blood Saber did not heal for 10 times the damage actually dealt.");
                gait.Refresh();
                Quaternion lastBladeRotation = blade.rotation;
                for (int frame = 0; frame < 51; frame++)
                {
                    boss.SendMessage("Tick", .01f);
                    gait.Refresh();
                    Check(Quaternion.Angle(lastBladeRotation, blade.rotation) < 30,
                        "The ambush blade flipped while following through or returning to carry.");
                    lastBladeRotation = blade.rotation;
                }
                player.Teleport(start);
                boss.SendMessage("Tick", .04f);
                Check(boss.CastingSkill != "血雾突袭", "Blood Saber repeated the ambush during cooldown.");
                player.Heal(slashDamage);
                immunity.SetValue(player, 0f);

                blocker = new GameObject("Blood mist blocked landing");
                blocker.transform.SetParent(arena.Travel.ActiveMap.transform, false);
                blocker.transform.position = unobstructed;
                blocker.AddComponent<CircleCollider2D>().radius = .22f;
                StartAmbush();
                Check(Vector2.Distance(body.position, unobstructed) > .65f,
                    "Blood Saber blinked into the blocked rear landing.");
                player.Teleport(start);
                boss.SendMessage("Tick", .41f);
                Check(player.Health == previousHealth && !warning.gameObject.activeSelf,
                    "The slash tracked the player out of the warned area or failed to resolve.");
            }
            finally
            {
                if (blocker != null) UnityEngine.Object.DestroyImmediate(blocker);
                body.position = boss.transform.position = start;
                boss.SendMessage("Stop");
                player.Heal(previousHealth - player.Health);
                enemy.SendMessage("Heal", previousBossHealth - enemy.Health);
                immunity.SetValue(player, previousImmunity);
                aim.SetValue(player, previousAim);
                player.invincible = invincible;
                enemy.damage = previousDamage;
                player.Teleport(previousPlayerPosition);
                Physics2D.SyncTransforms();
            }
        }

        static void CheckBloodDagger(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var daggerPosition = typeof(EvilCultivatorBoss).GetField("bloodDaggerPosition", flags);
            var ambushCooldown = typeof(EvilCultivatorBoss).GetField("bloodMistCooldown", flags);
            var immunity = typeof(SlashPlayer).GetField("invulnerability", flags);
            var effects = arena.Travel.ActiveMap.transform.Find("Final boss skill effects");
            var dagger = effects.Find("Homing blood dagger");
            var bladeMesh = dagger.GetComponent<MeshFilter>().sharedMesh;
            Check(bladeMesh.bounds.size.x > bladeMesh.bounds.size.y * 2.5f, "The blood dagger did not have a short blade silhouette.");
            var mist = effects.Find("Blood dagger mist").GetComponent<SpriteRenderer>();
            var player = arena.Player;
            Vector2 center = boss.transform.position;
            Vector2 distant = center + Vector2.right * 7;
            Vector2 previousPosition = player.transform.position;
            int health = player.Health, bossHealth = enemy.Health, damage = enemy.damage;
            float range = enemy.attackRange;
            bool invincible = player.invincible;
            object previousImmunity = immunity.GetValue(player);

            Vector2 Position() => (Vector2)daggerPosition.GetValue(boss);

            void Launch()
            {
                boss.SendMessage("Stop");
                enemy.attackRange = 2.875f;
                ambushCooldown.SetValue(boss, 30f);
                player.Teleport(distant);
                boss.SendMessage("Tick", 4.99f);
                Check(boss.CastingSkill == null && !dagger.gameObject.activeSelf, "Blood Saber charged a dagger in the first five seconds.");
                boss.SendMessage("Tick", .02f);
                Check(boss.CastingSkill == "追魂血匕" && dagger.gameObject.activeSelf && !mist.enabled,
                    "Blood Saber did not ready the dagger at long range.");
                float windup = Mathf.Max(.52f, enemy.windupTime * 1.064f);
                boss.SendMessage("Tick", windup - .01f);
                Check(boss.CastingSkill == "追魂血匕", "Blood Dagger launched before its shortened windup ended.");
                var gait = boss.GetComponentInChildren<HeroWalkMesh>();
                gait.Refresh();
                Check(gait.transform.Find("Saber support arm").GetComponent<MeshRenderer>().enabled,
                    "The throwing arm did not articulate in the side view.");
                boss.SendMessage("Tick", .02f);
                Check(boss.CastingSkill == null && dagger.gameObject.activeSelf && !mist.enabled && Position() == center,
                    "The charge did not release a projectile at the boss.");
                enemy.attackRange = 0;
            }

            try
            {
                enemy.damage = 47;
                enemy.TakeHit(Mathf.Min(450, bossHealth / 4), Vector2.zero, 0);
                player.invincible = false;
                immunity.SetValue(player, 0f);
                int wounded = enemy.Health;
                Launch();
                float releaseHeight = Vector3.Dot(dagger.position - (Vector3)Position(), arena.View.Up);
                boss.SendMessage("Tick", .25f);
                Vector2 first = Position();
                Check((first - center - Vector2.right * (.25f * 2.35f)).sqrMagnitude < .0001f && player.Health == health,
                    "Blood Dagger did not fly at the configured speed.");
                float flightHeight = Vector3.Dot(dagger.position - (Vector3)first, arena.View.Up);
                Check(flightHeight >= releaseHeight && flightHeight - releaseHeight < .16f,
                    "The dagger did not maintain the throwing hand's height after release.");
                player.Teleport(first + Vector2.down * 2);
                boss.SendMessage("Tick", .25f);
                Check((Position() - first - Vector2.down * (.25f * 2.35f)).sqrMagnitude < .0001f && player.Health == health,
                    "Blood Dagger did not steer toward the player's current position.");
                Check(Vector3.Angle(dagger.right, -arena.View.Up) < 1f, "The dagger tip did not follow its turn toward the player.");
                boss.SendMessage("Tick", .8f);
                Check(!dagger.gameObject.activeSelf && mist.enabled && (Vector2)mist.transform.position == Position()
                    && Mathf.Abs(mist.transform.localScale.x - 3.12f) < .005f,
                    "A Blood Dagger hit did not leave the expanded mist patch at the blast point.");
                Check(player.Health == health - 29 && enemy.Health == Mathf.Min(enemy.maxHealth, wounded + 290),
                    "Blood Dagger did not deal 29 blast damage with tenfold lifesteal.");
                float playerRadius = player.GetComponent<CircleCollider2D>().radius;
                player.Teleport(Position() + Vector2.right * (1.38f + playerRadius));
                boss.SendMessage("Tick", .5f);
                Check(player.Health == health - 29, "Blood Dagger mist ticked early.");
                boss.SendMessage("Tick", .5f);
                Check(player.Health == health - 31 && enemy.Health == Mathf.Min(enemy.maxHealth, wounded + 310),
                    "Blood Dagger mist did not deal two damage and heal for twenty.");
                player.Teleport(distant);
                boss.SendMessage("Tick", 3f);
                Check(!mist.enabled && player.Health == health - 31, "Blood Dagger mist did not expire after four seconds.");

                Launch();
                for (int i = 0; i < 5; i++)
                {
                    player.Teleport(center + Vector2.right * (i % 2 == 0 ? 7 : -7));
                    boss.SendMessage("Tick", 1f);
                    Check(dagger.gameObject.activeSelf && !mist.enabled, "Blood Dagger burst before its six-second flight expired.");
                }
                player.Teleport(center - Vector2.right * 7);
                boss.SendMessage("Tick", 1f);
                Check(!dagger.gameObject.activeSelf && mist.enabled && (Vector2)mist.transform.position == Position()
                    && player.Health == health - 31, "Blood Dagger expiry did not burst in place at its final position.");
                boss.SendMessage("Stop");
                Check(!dagger.gameObject.activeSelf && !mist.enabled, "Blood Dagger effects persisted after combat stopped.");
            }
            finally
            {
                boss.SendMessage("Stop");
                enemy.attackRange = range;
                enemy.damage = damage;
                enemy.SendMessage("Heal", bossHealth - enemy.Health);
                player.invincible = invincible;
                immunity.SetValue(player, previousImmunity);
                player.Heal(health - player.Health);
                player.Teleport(previousPosition);
                Physics2D.SyncTransforms();
            }
        }

        static void CheckBloodFrenzy(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var buff = typeof(EvilCultivatorBoss).GetProperty("FrenzyBuff", flags);
            var cooldowns = (float[])typeof(EvilCultivatorBoss).GetField("cooldowns", flags).GetValue(boss);
            var player = arena.Player;
            var immunity = typeof(SlashPlayer).GetField("invulnerability", flags);
            object previousImmunity = immunity.GetValue(player);
            bool previousInvincible = player.invincible;
            Vector2 previousPosition = player.transform.position;
            int playerHealth = player.Health, bossHealth = enemy.Health, attackDamage = enemy.damage;
            float attackRange = enemy.attackRange;
            var body = boss.GetComponent<Rigidbody2D>();
            var gait = boss.GetComponentInChildren<HeroWalkMesh>();
            var robe = gait.transform.Find("Walking mesh").GetComponent<MeshRenderer>();
            var sleeve = gait.transform.Find("Saber arm").GetComponent<MeshRenderer>();
            var blade = gait.transform.Find("Saber blade").GetComponent<MeshRenderer>();
            var supportSleeve = gait.transform.Find("Saber support arm").GetComponent<MeshRenderer>();
            var sleeveMesh = sleeve.GetComponent<MeshFilter>().sharedMesh;
            var supportMesh = supportSleeve.GetComponent<MeshFilter>().sharedMesh;
            var materials = new MaterialPropertyBlock();

            void CheckGlow(float intensity)
            {
                gait.Refresh();
                blade.GetPropertyBlock(materials);
                Check(Mathf.Approximately(materials.GetFloat("_SaberBladeGlow"), intensity),
                    "Blood Frenzy glow did not follow the buff on the saber blade.");
                robe.GetPropertyBlock(materials);
                Check(materials.GetFloat("_SaberBladeGlow") == 0, "Blood Frenzy lit up the boss's body.");
                sleeve.GetPropertyBlock(materials);
                Check(materials.GetFloat("_SaberBladeGlow") == 0, "Blood Frenzy lit up the boss's sleeve.");
            }

            float ChaseSpeed()
            {
                boss.SendMessage("FixedTick", .02f);
                float speed = body.linearVelocity.magnitude;
                body.linearVelocity = Vector2.zero;
                return speed;
            }

            try
            {
                boss.SendMessage("Stop");
                Vector2 front = (Vector2)boss.transform.position + Vector2.down * 1.3f;
                player.Teleport(front);
                float baselineSpeed = ChaseSpeed();
                Check(baselineSpeed > 0, "Blood Saber did not pursue for the speed test.");
                CheckGlow(0);
                var restArm = sleeveMesh.vertices;
                int shoulder = NearestVertex(restArm, new Vector2(-.112f, .674f));
                int elbow = NearestVertex(restArm, new Vector2(-.141f, .569f));
                int hand = NearestVertex(restArm, new Vector2(-.147f, .464f));
                player.invincible = true;
                for (int i = 0; i < 2; i++)
                {
                    boss.SendMessage("Tick", .04f);
                    boss.SendMessage("Tick", 4f);
                    boss.SendMessage("Tick", 4f);
                }
                enemy.TakeHit(enemy.Health - Mathf.CeilToInt(enemy.maxHealth * .75f) + 1, Vector2.zero, 0);
                enemy.SendMessage("Heal", bossHealth - enemy.Health);
                boss.SendMessage("Tick", .04f);
                Check(boss.CastingSkill == "血祭" && Mathf.Approximately(cooldowns[2], 20),
                    "Blood Frenzy did not set its twenty-second cooldown.");
                enemy.TakeHit(bossHealth / 4, Vector2.zero, 0);
                int before = enemy.Health;
                boss.SendMessage("Tick", 2f);
                Check(enemy.Health == before - Mathf.CeilToInt(before * .1f)
                    && buff.GetValue(boss) is CombatBuffView { timeLeft: 20f } frenzy
                    && Resources.Load<Texture2D>(frenzy.iconResource) != null,
                    "Blood Frenzy did not spend ten percent of current health or show its timed status icon.");
                CheckGlow(1);
                var raisedArm = sleeveMesh.vertices;
                Check(raisedArm[elbow].y < raisedArm[shoulder].y
                    && raisedArm[elbow].y < raisedArm[hand].y,
                    "Blood Frenzy did not raise the blade with one bent arm.");
                Check((raisedArm[hand] - blade.transform.localPosition).sqrMagnitude < .0008f,
                    "Blood Frenzy's raised hand slipped off the handle.");
                Vector2 restingOffhand = new Vector2(.145f, .446f);
                Check(supportSleeve.enabled, "Blood Frenzy did not animate the free arm.");
                var offhandVertices = supportMesh.vertices;
                int offhand = NearestVertex(robe.GetComponent<MeshFilter>().sharedMesh.vertices, restingOffhand);
                Check(offhandVertices[offhand].y > restingOffhand.y + .08f
                    && (offhandVertices[offhand] - blade.transform.localPosition).sqrMagnitude > .03f,
                    "Blood Frenzy's free hand did not lift separately from the saber.");
                boss.SendMessage("Tick", .5f);
                gait.Refresh();
                Check(!supportSleeve.enabled, "Blood Frenzy did not settle its free arm after casting.");
                Check(Mathf.Abs(ChaseSpeed() - baselineSpeed * 1.10f) < .01f,
                    "Blood Frenzy did not increase the boss's pursuit speed by ten percent.");
                boss.SendMessage("Tick", .04f);
                Check(boss.CastingSkill == "裂地血潮", "Blood Frenzy did not return to the skill rotation.");
                player.invincible = false;
                immunity.SetValue(player, 0f);
                enemy.damage = 40;
                float windup = Mathf.Max(.52f, enemy.windupTime * 1.45f);
                boss.SendMessage("Tick", windup * .82f);
                Check(boss.CastingSkill == null && player.Health == playerHealth - 17,
                    "Blood Frenzy did not speed up the cast or boost the reduced direct damage by twenty percent.");
                enemy.attackRange = 0;
                player.Teleport((Vector2)boss.transform.position + Vector2.right * (2 * attackRange));
                boss.SendMessage("Tick", 20f);
                Check(buff.GetValue(boss) == null, "Blood Frenzy stayed active after twenty seconds.");
                CheckGlow(0);
                player.Teleport(front);
                Check(Mathf.Abs(ChaseSpeed() - baselineSpeed) < .01f,
                    "Blood Frenzy's move speed bonus persisted after expiry.");
            }
            finally
            {
                boss.SendMessage("Stop");
                enemy.attackRange = attackRange;
                enemy.damage = attackDamage;
                enemy.SendMessage("Heal", bossHealth - enemy.Health);
                player.invincible = previousInvincible;
                immunity.SetValue(player, previousImmunity);
                player.Heal(playerHealth - player.Health);
                player.Teleport(previousPosition);
            }
        }

        static void CheckBloodSaberStages(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var nextSkill = typeof(EvilCultivatorBoss).GetField("nextSkill", flags);
            var cooldowns = (float[])typeof(EvilCultivatorBoss).GetField("cooldowns", flags).GetValue(boss);
            var windup = typeof(EvilCultivatorBoss).GetField("windup", flags);
            var arenaMist = typeof(EvilCultivatorBoss).GetField("arenaMist", flags);
            var warning = boss.GetComponent<ObliqueActor>().GroundWarning;
            var lines = warning.GetComponentsInChildren<LineRenderer>(true);
            var map = arena.Travel.ActiveMap;
            var player = arena.Player;
            int health = player.Health, bossHealth = enemy.Health;
            bool invincible = player.invincible;
            Vector2 previousPosition = player.transform.position;
            float range = enemy.attackRange;

            void CheckBladeWarnings(int expectedCount, float lengthScale)
            {
                int active = 0;
                foreach (var line in lines)
                {
                    if (line.name != "Telegraph" || !line.enabled) continue;
                    active++;
                    float length = Vector2.Distance(line.GetPosition(1), line.GetPosition(2));
                    float width = Vector2.Distance(line.GetPosition(0), line.GetPosition(1));
                    Check(Mathf.Abs(length - 2.5f * range * lengthScale) < .005f
                        && Mathf.Abs(width - .38f * range) < .005f,
                        $"Flying blades used the wrong lane dimensions at {enemy.Health}/{enemy.maxHealth} HP.");
                }
                Check(active == expectedCount, $"Expected {expectedCount} flying blade lanes, received {active}.");
            }

            void StartBlades()
            {
                nextSkill.SetValue(boss, 4);
                cooldowns[4] = 0;
                player.Teleport((Vector2)boss.transform.position + Vector2.down * 1.3f);
                boss.SendMessage("Tick", .04f);
                Check(boss.CastingSkill == "三途飞刃", "Blood Saber did not cast its flying blades.");
            }

            void FinishBlades()
            {
                boss.SendMessage("Tick", 4f);
                boss.SendMessage("Tick", 4f);
            }

            try
            {
                boss.SendMessage("Stop");
                player.invincible = true;
                int firstLengthStep = (enemy.maxHealth + 9) / 10;
                enemy.TakeHit(firstLengthStep - 1, Vector2.zero, 0);
                StartBlades();
                CheckBladeWarnings(3, 1);
                enemy.TakeHit(1, Vector2.zero, 0);
                CheckBladeWarnings(3, 1);
                FinishBlades();
                StartBlades();
                CheckBladeWarnings(3, 1.05f);
                FinishBlades();

                enemy.TakeHit(enemy.Health - Mathf.CeilToInt(enemy.maxHealth * .50f), Vector2.zero, 0);
                StartBlades();
                CheckBladeWarnings(3, 1.25f);
                enemy.TakeHit(1, Vector2.zero, 0);
                CheckBladeWarnings(3, 1.25f);
                FinishBlades();
                StartBlades();
                CheckBladeWarnings(5, 1.25f);
                Check(Mathf.Approximately((float)windup.GetValue(boss), Mathf.Max(.52f, enemy.windupTime * 1.42f)),
                    "Blood Saber did not telegraph the blades at normal speed below half health.");
                FinishBlades();
                enemy.TakeHit(enemy.Health - enemy.maxHealth * 2 / 5, Vector2.zero, 0);
                StartBlades();
                CheckBladeWarnings(5, 1.30f);
                FinishBlades();
                enemy.TakeHit(enemy.Health - enemy.maxHealth * 3 / 10, Vector2.zero, 0);
                StartBlades();
                CheckBladeWarnings(5, 1.30f);
                FinishBlades();
                enemy.SendMessage("Heal", bossHealth - enemy.Health);
                StartBlades();
                CheckBladeWarnings(5, 1);
                FinishBlades();

                var definition = map.Definition;
                var effects = map.transform.Find("Final boss skill effects");
                var temporaryMist = effects.Find("Blood tide mist").GetComponent<SpriteRenderer>();
                player.Teleport((Vector2)boss.transform.position + Vector2.up * .55f);
                player.invincible = false;
                enemy.attackRange = 0;
                enemy.TakeHit(enemy.Health - Mathf.CeilToInt(enemy.maxHealth * .25f), Vector2.zero, 0);
                Check(arenaMist.GetValue(boss) == null && boss.CastingSkill == null,
                    "The arena fog transition started at exactly one-quarter health.");
                enemy.attackRange = range;
                nextSkill.SetValue(boss, 1);
                cooldowns[1] = 0;
                boss.SendMessage("Tick", .04f);
                boss.SendMessage("Tick", .20f);
                Check(boss.CastingSkill == "贯血刀", "Could not interrupt a live attack to test the transition.");
                var gait = boss.GetComponentInChildren<HeroWalkMesh>();
                var blade = gait.transform.Find("Saber blade");
                var bladeRenderer = blade.GetComponent<MeshRenderer>();
                gait.Refresh();
                Vector3 previousGrip = blade.localPosition;
                Quaternion previousBladeAngle = blade.localRotation;
                enemy.TakeHit(1, Vector2.zero, 0);
                gait.Refresh();
                Check((blade.localPosition - previousGrip).sqrMagnitude < .000001f
                    && Quaternion.Angle(previousBladeAngle, blade.localRotation) < .1f,
                    "The phase transition snapped the saber away from its previous pose.");
                enemy.attackRange = 0;
                int transitionHealth = enemy.Health;
                float transitionWindup = (float)windup.GetValue(boss);
                var transitionBuff = typeof(EvilCultivatorBoss).GetProperty("TransitionBuff", flags);
                Check(boss.CastingSkill == "血雾升腾" && arenaMist.GetValue(boss) == null && !warning.gameObject.activeSelf,
                    "Blood Saber did not start the non-attacking ground stab immediately below one-quarter health.");
                Check(transitionBuff.GetValue(boss) is CombatBuffView shield && shield.RemainingFraction == 1
                    && Resources.Load<Texture2D>(shield.iconResource) != null,
                    "Blood Saber did not display its invulnerability icon at the start of the transition.");
                enemy.TakeHit(20, Vector2.zero, 0);
                Check(enemy.Health == transitionHealth, "Blood Saber took damage at the start of the transition.");
                boss.SendMessage("Tick", transitionWindup * .75f);
                gait.Refresh();
                Check(!bladeRenderer.enabled, "Blood Saber exposed the blade through its back during the transition stab.");
                enemy.TakeHit(20, Vector2.zero, 0);
                Check(enemy.Health == transitionHealth && arenaMist.GetValue(boss) == null,
                    "Blood Saber took damage or raised the arena fog before blade contact.");
                boss.SendMessage("Tick", transitionWindup);
                gait.Refresh();
                Check(!bladeRenderer.enabled, "Blood Saber's planted blade showed between its boots during recovery.");
                var mist = map.transform.Find("Arena blood mist").GetComponent<SpriteRenderer>();
                Check(mist.enabled && player.Health == health && !temporaryMist.enabled
                    && !warning.gameObject.activeSelf && CountBossEffectLines(effects) == 0,
                    "The blade contact did not raise only the permanent arena fog.");
                enemy.TakeHit(20, Vector2.zero, 0);
                Check(enemy.Health == transitionHealth, "Blood Saber took damage during transition recovery.");
                Check(transitionBuff.GetValue(boss) is CombatBuffView recoveryShield && recoveryShield.RemainingFraction < 1,
                    "The invulnerability icon stopped counting down during transition recovery.");
                player.Teleport(definition.Ground(755, 475));
                boss.SendMessage("Tick", .75f);
                gait.Refresh();
                Check(bladeRenderer.enabled, "Blood Saber did not restore the blade to its carry pose.");
                enemy.TakeHit(1, Vector2.zero, 0);
                Check(enemy.Health == transitionHealth - 1 && transitionBuff.GetValue(boss) == null,
                    "Blood Saber retained invulnerability or its icon after withdrawing the blade.");

                object field = arenaMist.GetValue(boss);
                var contains = field.GetType().GetMethod("Contains");
                Vector2 size = definition.WorldSize;
                const int columns = 72, rows = 48;
                int covered = 0;
                for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    Vector2 position = Vector2.Scale(new Vector2((x + .5f) / columns - .5f, (y + .5f) / rows - .5f), size);
                    if ((bool)contains.Invoke(field, new object[] { position, 0f })) covered++;
                }
                float area = 0;
                var boundary = map.WalkableBoundary;
                for (int i = 0, previous = boundary.Count - 1; i < boundary.Count; previous = i++)
                    area += Vector3.Cross(boundary[previous], boundary[i]).z;
                float coverage = covered * size.x * size.y / (columns * rows * .5f * Mathf.Abs(area));
                Check(Mathf.Abs(coverage - .40f) < .025f, $"Arena fog covers {coverage:P0} of the courtyard.");

                boss.SendMessage("Tick", 10f);
                Check(mist.enabled && player.Health == health, "The permanent arena fog expired or covered the center.");
                Check(!(bool)contains.Invoke(field, new object[] { definition.Ground(755, 935), 0f }),
                    "The blood fog spilled past the closed arena gate.");
                player.Teleport(definition.Ground(755, 835));
                int before = enemy.Health;
                boss.SendMessage("Tick", .5f);
                Check(player.Health == health, "The arena fog damaged the player before one second.");
                boss.SendMessage("Tick", .5f);
                Check(player.Health == health - 2 && enemy.Health == before + 20,
                    "The arena fog did not deal two damage and heal Blood Saber for twenty.");
                enemy.SendMessage("Heal", bossHealth - enemy.Health);
                boss.SendMessage("Tick", 1f);
                Check(mist.enabled && player.Health == health - 4, "The arena fog disappeared after Blood Saber healed.");
                boss.SendMessage("Stop");
                Check(!mist.enabled, "The arena fog remained after combat stopped.");
            }
            finally
            {
                boss.SendMessage("Stop");
                enemy.attackRange = range;
                enemy.SendMessage("Heal", bossHealth - enemy.Health);
                player.invincible = invincible;
                player.Heal(health - player.Health);
                player.Teleport(previousPosition);
            }
        }

        static void CheckBossSkills(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss)
        {
            var player = arena.Player;
            Vector2 originalPosition = player.transform.position;
            bool invincible = player.invincible;
            bool bell = boss.CombatStyle == EvilCultivatorBoss.Style.SoulBell;
            var skills = bell
                ? new[] { "摄魂铃震", "十字断魄", "三叠追魂", "镇魂回响", "四方锁魂阵" }
                : new[] { "饮血横斩", "贯血刀", "裂地血潮", "三途飞刃" };
            int[][] outlines = bell
                ? new[] { new[] { 1 }, new[] { 2 }, new[] { 1, 1, 1 }, new[] { 2 }, new[] { 4, 1 } }
                : new[] { new[] { 1 }, new[] { 1 }, new[] { 1 }, new[] { 3 } };
            var warning = boss.GetComponent<ObliqueActor>().GroundWarning;
            var lines = warning.GetComponentsInChildren<LineRenderer>(true);
            var gait = boss.GetComponentInChildren<HeroWalkMesh>();
            var skin = gait.GetComponent<SpriteRenderer>();
            var model = boss.transform.Find("Visual");
            var map = arena.Travel.ActiveMap;
            var effects = map.transform.Find("Final boss skill effects");
            Check(effects != null, "Final boss did not initialize its skill effects.");
            var weaponGlow = effects.GetChild(0).GetComponent<LineRenderer>();
            Check(effects.Find("Saber whirlwind") == null, "The removed whirlwind effect was still created.");
            var bloodTideMist = bell ? null : effects.Find("Blood tide mist").GetComponent<SpriteRenderer>();
            var particles = map.GetComponent<BeastAttackParticles>();
            var saberLayer = bell ? null : skin.transform.Find("Saber arm");
            Check(bell || saberLayer != null, "Blood Saber is missing its separately rendered sword arm.");
            var armMesh = bell ? gait.DeformedMesh : saberLayer.GetComponent<MeshFilter>().sharedMesh;
            var armRenderer = bell ? null : saberLayer.GetComponent<MeshRenderer>();
            var bladeLayer = bell ? null : skin.transform.Find("Saber blade");
            Check(bell || bladeLayer != null, "Blood Saber is missing its rigid blade.");
            var bladeMesh = bell ? null : bladeLayer.GetComponent<MeshFilter>().sharedMesh;
            var supportLayer = bell ? null : skin.transform.Find("Saber support arm");
            Check(bell || supportLayer != null, "Blood Saber is missing its second arm.");
            var supportRenderer = bell ? null : supportLayer.GetComponent<MeshRenderer>();
            var supportMesh = bell ? null : supportLayer.GetComponent<MeshFilter>().sharedMesh;
            if (!bell) CheckSaberBreathing(boss, enemy, gait);
            player.invincible = true;
            Vector2 front = (Vector2)boss.transform.position + Vector2.down * 1.3f;
            Vector2 flank = (Vector2)boss.transform.position + Vector2.right * 1.3f;
            float followStep = bell ? .07f : .07f / 1.5f;
            for (int skill = 0; skill < skills.Length; skill++)
            {
                bool groundStab = !bell && skills[skill] == "裂地血潮";
                bool sweeps = !bell && skills[skill] == "饮血横斩";
                Vector2 castPosition = groundStab
                    ? (Vector2)boss.transform.position + Vector2.down * (1.43f * enemy.attackRange) : front;
                player.Teleport(castPosition);
                boss.SendMessage("Tick", .04f);
                Check(boss.CastingSkill == skills[skill], "Final boss skipped " + skills[skill]);
                gait.Refresh();
                Sprite castingFacing = skin.sprite;
                var rest = gait.DeformedMesh.vertices;
                var triangles = gait.DeformedMesh.GetTriangles(0);
                int grip = NearestVertex(rest, bell ? new Vector2(-.199f, .857f) : new Vector2(-.147f, .464f));
                int boot = NearestVertex(rest, new Vector2(0, .033f));
                int bladeTip = bell ? 0 : NearestVertex(bladeMesh.vertices, new Vector2(-.195f, -.288f));
                Vector3 blade = bell ? Vector3.zero : bladeMesh.vertices[bladeTip];
                int supportGrip = bell ? 0 : NearestVertex(supportMesh.vertices, new Vector2(.145f, .446f));
                if (!bell)
                    Check(blade.magnitude > .325f,
                        "Blood Saber did not equip the longer blade.");
                int[] waist = bell ? null : SaberTorsoVertices(rest, 0);
                int[] core = {
                    NearestVertex(rest, new Vector2(-.045f, .66f)), NearestVertex(rest, new Vector2(.045f, .66f)),
                    NearestVertex(rest, new Vector2(-.045f, .86f)), NearestVertex(rest, new Vector2(.045f, .86f)),
                    NearestVertex(rest, new Vector2(0, .93f))
                };
                for (int wave = 0; wave < outlines[skill].Length; wave++)
                {
                    int count = 0;
                    LineRenderer primary = null;
                    foreach (var line in lines)
                    {
                        if (line.name != "Telegraph" || !line.enabled) continue;
                        primary ??= line;
                        count++;
                    }
                    Check(warning.gameObject.activeSelf && count == outlines[skill][wave],
                        "Final boss drew the wrong warning count for " + skills[skill]);
                    if (groundStab)
                        Check(Mathf.Abs(Vector2.Distance(boss.transform.position, primary.GetPosition(0)) - 1.488f * enemy.attackRange) < .005f,
                            "Blood Tide did not telegraph the expanded radius.");
                    if (!bell && skills[skill] == "贯血刀")
                        Check(Mathf.Abs(Vector2.Distance(primary.GetPosition(1), primary.GetPosition(2)) - 2.45f * enemy.attackRange) < .005f,
                            "Blood Cut did not telegraph the extended lane.");
                    Vector3 sweepStart = Vector3.zero, sweepEnd = Vector3.zero;
                    if (sweeps)
                    {
                        Vector3 center = primary.GetPosition(0);
                        sweepStart = primary.GetPosition(1) - center;
                        sweepEnd = primary.GetPosition(primary.positionCount - 1) - center;
                    }
                    Check(weaponGlow.enabled, "Final boss did not charge its weapon for " + skills[skill]);
                    Vector3 glowStart = weaponGlow.GetPosition(0);
                    gait.Refresh();
                    var start = gait.DeformedMesh.vertices;
                    var armStart = armMesh.vertices;
                    Vector3 tipStart = bell ? Vector3.zero : BladeTip(bladeLayer, blade);
                    Quaternion planted = model.localRotation;
                    float readyStep = .26f;
                    float timeToImpact = 0;
                    if (!bell)
                    {
                        var castTime = typeof(EvilCultivatorBoss).GetField("windup",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        float duration = (float)castTime.GetValue(boss);
                        float readyAge = groundStab ? duration - .30f : Mathf.Min(.68f * duration, duration - .28f);
                        readyStep = readyAge - (wave > 0 ? followStep : 0);
                        timeToImpact = duration - readyAge;
                    }
                    boss.SendMessage("Tick", readyStep);
                    gait.Refresh();
                    var ready = gait.DeformedMesh.vertices;
                    var armReady = armMesh.vertices;
                    Vector3 gripReady = bell ? Vector3.zero : bladeLayer.localPosition;
                    Vector3 tipReady = bell ? Vector3.zero : BladeTip(bladeLayer, blade);
                    Check(boss.CastingSkill == skills[skill], "Final boss left windup too early during " + skills[skill]);
                    Check(bell || armRenderer.enabled, "Blood Saber did not display its separate arm during windup.");
                    if (wave == 0)
                        Check((armReady[grip] - armStart[grip]).sqrMagnitude > .000001f,
                            "Final boss did not move the painted weapon during " + skills[skill]);
                    Check((ready[boot] - start[boot]).sqrMagnitude < .000001f, "Final boss moved its planted feet.");
                    Check(Quaternion.Angle(planted, model.localRotation) < .1f, "Final boss pitched its entire model during windup.");
                    Check((weaponGlow.GetPosition(0) - glowStart).sqrMagnitude > .000001f,
                        "Final boss weapon glow did not follow its attack animation.");
                    CheckCastingMesh(rest, ready, triangles, skills[skill]);
                    CheckCastingCore(rest, ready, core, skills[skill]);
                    if (!bell)
                    {
                        Check((ready[core[0]] - start[core[0]]).sqrMagnitude > .00004f,
                            "Blood Saber's torso did not shift during " + skills[skill]);
                        CheckBladeLength(blade, gripReady, tipReady);
                        CheckSaberBladeEdge(bladeLayer, bladeMesh, skills[skill]);
                        CheckCastingCore(rest, ready, waist, skills[skill]);
                        if (sweeps) CheckSaberSweep(bladeLayer.parent, gripReady, tipReady, sweepStart, skills[skill]);
                        if (groundStab)
                        {
                            int shoulder = NearestVertex(armStart, new Vector2(-.112f, .674f));
                            int elbow = NearestVertex(armStart, new Vector2(-.141f, .569f));
                            float bend = Vector3.Angle(armReady[elbow] - armReady[shoulder], armReady[grip] - armReady[elbow]);
                            Check(bend > 55 && bend < 143, $"Blood Tide folded its raised saber arm too tightly ({bend:F1}°).");
                            Check(armReady[elbow].y < armReady[shoulder].y && gripReady.y < armReady[shoulder].y,
                                "Blood Tide lifted the saber hand or elbow above the shoulder.");
                            Check(Mathf.Abs((gripReady.x + tipReady.x) * .5f) < .03f,
                                "Blood Tide did not raise the blade in the center of the body.");
                        }
                    }
                    if (wave + 1 < outlines[skill].Length)
                        player.Teleport(wave % 2 == 0 ? flank : front);
                    if (groundStab)
                    {
                        boss.SendMessage("Tick", timeToImpact * .82f);
                        gait.Refresh();
                        var tip = BladeTip(bladeLayer, blade);
                        Check(skin.sprite == castingFacing && warning.gameObject.activeSelf && tip.y > .05f,
                            "Blood Tide turned or struck before the blade reached the ground.");
                    }
                    boss.SendMessage("Tick", 4f);
                    gait.Refresh();
                    var contact = gait.DeformedMesh.vertices;
                    var armContact = armMesh.vertices;
                    Check(skin.sprite == castingFacing, "Final boss changed its body facing between skill waves.");
                    CheckCastingMesh(rest, contact, triangles, skills[skill]);
                    CheckCastingCore(rest, contact, core, skills[skill]);
                    if (!bell)
                    {
                        Check((contact[core[0]] - ready[core[0]]).sqrMagnitude > .00004f,
                            "Blood Saber's torso did not follow its release during " + skills[skill]);
                        Vector3 gripContact = bladeLayer.localPosition;
                        Vector3 tipContact = BladeTip(bladeLayer, blade);
                        CheckBladeLength(blade, gripContact, tipContact);
                        CheckSaberBladeEdge(bladeLayer, bladeMesh, skills[skill]);
                        Check(supportRenderer.enabled, "Blood Saber's support arm did not join the attack.");
                        CheckTwoHandedGrip(gripContact, tipContact, supportMesh.vertices[supportGrip], skills[skill]);
                        CheckCastingCore(rest, contact, waist, skills[skill]);
                        if (sweeps) CheckSaberSweep(bladeLayer.parent, gripContact, tipContact,
                            (sweepStart.normalized + sweepEnd.normalized).normalized, skills[skill]);
                        Check(Mathf.Max((tipContact - tipReady).sqrMagnitude, (tipReady - tipStart).sqrMagnitude) > .01f,
                            "Blood Saber's blade did not travel through the attack during " + skills[skill]);
                        if (sweeps)
                        {
                            Check(Vector3.Angle(tipReady - gripReady, tipContact - gripContact) > 18,
                                "Blood Saber moved its grip without swinging the blade during the sweep.");
                            int shoulder = NearestVertex(armStart, new Vector2(-.112f, .674f));
                            int elbow = NearestVertex(armStart, new Vector2(-.141f, .569f));
                            float readyBend = Vector3.Angle(armReady[elbow] - armReady[shoulder], armReady[grip] - armReady[elbow]);
                            float contactBend = Vector3.Angle(armContact[elbow] - armContact[shoulder],
                                armContact[grip] - armContact[elbow]);
                            Check(readyBend > 40 && contactBend < readyBend - 25
                                && (armContact[elbow] - armReady[elbow]).sqrMagnitude > .0064f,
                                "Blood Saber's elbow did not lead and extend through the horizontal cut.");
                        }
                        Check(sweeps || (tipContact - gripContact).y < -.15f,
                            "Blood Saber did not finish its attack animation during " + skills[skill]);
                        if (groundStab)
                            Check(Mathf.Abs(tipContact.y) < .04f && tipReady.y - tipContact.y > .25f
                                && Mathf.Abs((gripContact.x + tipContact.x) * .5f) < .03f,
                                $"Blood Tide did not drive the centered blade into the ground ({tipReady.y:F3}, {tipContact.y:F3}).");
                    }
                    Quaternion bladeContact = bell ? Quaternion.identity : bladeLayer.localRotation;
                    Check((armContact[grip] - armReady[grip]).sqrMagnitude > .0001f,
                        "Final boss did not reach a separate contact pose for " + skills[skill]);
                    Check(Quaternion.Angle(planted, model.localRotation) < .1f, "Final boss pitched its entire model at impact.");
                    int active = CountBossEffectLines(effects);
                    Check(active > (boss.CastingSkill == null ? 0 : 2) && particles.ActiveCount > 0,
                        "Final boss did not release contact effects for " + skills[skill]);
                    boss.SendMessage("Tick", followStep);
                    gait.Refresh();
                    var follow = gait.DeformedMesh.vertices;
                    var armFollow = armMesh.vertices;
                    CheckCastingMesh(rest, follow, triangles, skills[skill]);
                    CheckCastingCore(rest, follow, core, skills[skill]);
                    if (!bell)
                    {
                        Vector3 gripFollow = bladeLayer.localPosition;
                        Vector3 tipFollow = BladeTip(bladeLayer, blade);
                        CheckBladeLength(blade, gripFollow, tipFollow);
                        CheckSaberBladeEdge(bladeLayer, bladeMesh, skills[skill]);
                        CheckTwoHandedGrip(gripFollow, tipFollow, supportMesh.vertices[supportGrip], skills[skill]);
                        CheckCastingCore(rest, follow, waist, skills[skill]);
                        if (groundStab)
                        {
                            Check(Mathf.Abs(tipFollow.y) < .04f, "Blood Tide immediately released its planted blade.");
                            boss.SendMessage("Tick", .20f);
                            gait.Refresh();
                            Check(BladeTip(bladeLayer, blade).y > .09f, "Blood Tide did not withdraw its planted blade.");
                        }
                        else Check(Quaternion.Angle(bladeContact, bladeLayer.localRotation) > 1,
                            "Blood Saber did not follow through with its blade during " + skills[skill]);
                    }
                    Check(groundStab || (armFollow[grip] - armContact[grip]).sqrMagnitude > .000001f,
                        "Final boss did not follow through after impact for " + skills[skill]);
                    Check((follow[boot] - start[boot]).sqrMagnitude < .000001f
                        && Quaternion.Angle(planted, model.localRotation) < .1f, "Final boss lost its stance during follow-through.");
                }
                Check(boss.CastingSkill == null && !enemy.IsChasing, "Final boss skipped recovery.");
                if (groundStab) CheckBloodTideMist(boss, enemy, player, bloodTideMist);
                else boss.SendMessage("Tick", 4f);
                gait.Refresh();
                Check(enemy.IsChasing && !warning.gameObject.activeSelf, "Final boss left a stale impact.");
                Check(bell || (armRenderer.enabled && !supportRenderer.enabled),
                    "Blood Saber did not return to its one-handed carry after recovery.");
                Check(CountBossEffectLines(effects) == 0, "Final boss left stale skill effects.");
            }
            boss.SendMessage("Tick", .04f);
            Check(warning.gameObject.activeSelf, "Final boss stopped using skills after its first rotation.");
            boss.SendMessage("Stop");
            Check(!warning.gameObject.activeSelf && boss.CastingSkill == null, "Final boss retained a warning after stopping.");
            Check(CountBossEffectLines(effects) == 0 && particles.ActiveCount == 0, "Final boss retained skill effects after stopping.");
            if (!bell)
            {
                CheckSaberTorsoFacings(arena, enemy, boss, skin, gait,
                    effects.Find("Saber swing trail").GetComponent<LineRenderer>());
                CheckSaberCastFacings(arena, enemy, boss, skin, gait);
                CheckSaberPlant(arena, enemy, boss, gait);
            }
            player.Teleport(originalPosition);
            player.invincible = invincible;
        }

        static void CheckCastingMesh(Vector3[] rest, Vector3[] pose, int[] triangles, string skill)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                float restArea = Vector3.Cross(rest[b] - rest[a], rest[c] - rest[a]).z;
                float posedArea = Vector3.Cross(pose[b] - pose[a], pose[c] - pose[a]).z;
                float ratio = posedArea / restArea;
                if (!(ratio > .25f && ratio < 1.75f))
                    throw new Exception($"Final boss body mesh folded or stretched during {skill} at {rest[a]:F3} (area ratio {ratio:F2}).");
            }
        }

        static void CheckCastingCore(Vector3[] rest, Vector3[] pose, int[] core, string skill)
        {
            for (int i = 0; i < core.Length; i++)
            for (int j = 0; j < i; j++)
            {
                float original = Vector3.Distance(rest[core[i]], rest[core[j]]);
                float posed = Vector3.Distance(pose[core[i]], pose[core[j]]);
                Check(Mathf.Abs(posed - original) < .0001f, "Final boss stretched its body or clothing during " + skill);
            }
        }

        static void CheckSaberBreathing(EvilCultivatorBoss boss, SlashEnemy enemy, HeroWalkMesh gait)
        {
            float range = enemy.attackRange;
            try
            {
                boss.SendMessage("Stop");
                enemy.attackRange = 0;
                gait.Refresh();
                var rest = gait.DeformedMesh.vertices;
                int chest = NearestVertex(rest, new Vector2(0, .71f));
                int foot = NearestVertex(rest, new Vector2(0, .03f));
                boss.SendMessage("Tick", .55f);
                gait.Refresh();
                var breath = gait.DeformedMesh.vertices;
                Check((breath[chest] - rest[chest]).sqrMagnitude > .000015f,
                    "Blood Saber did not breathe while standing still.");
                Check((breath[foot] - rest[foot]).sqrMagnitude < .000001f,
                    "Blood Saber's breath moved its planted feet.");
            }
            finally
            {
                enemy.attackRange = range;
                boss.SendMessage("Stop");
            }
        }

        static int[] SaberTorsoVertices(Vector3[] rest, int facing)
        {
            // Probe fabric clear of the blade where it crosses the lower silhouette in profile.
            float tunicX = facing == 2 ? -.103f : facing == 3 ? -.025f : facing == 6 ? .055f : 0;
            return new[]
            {
                NearestVertex(rest, new Vector2(0, .86f)),
                NearestVertex(rest, new Vector2(-.05f, .53f)), NearestVertex(rest, new Vector2(.05f, .53f)),
                NearestVertex(rest, new Vector2(-.065f, .59f)), NearestVertex(rest, new Vector2(.065f, .59f)),
                NearestVertex(rest, new Vector2(tunicX - .022f, .39f)),
                NearestVertex(rest, new Vector2(tunicX + .022f, .39f)),
                NearestVertex(rest, new Vector2(tunicX, .365f))
            };
        }

        static void CheckSaberTorsoFacings(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss,
            SpriteRenderer skin, HeroWalkMesh gait, LineRenderer bladeTrail)
        {
            var atlas = skin.sprite.texture;
            var saberMesh = skin.transform.Find("Saber arm").GetComponent<MeshFilter>().sharedMesh;
            var bladeLayer = skin.transform.Find("Saber blade");
            var bladeMesh = bladeLayer.GetComponent<MeshFilter>().sharedMesh;
            float nearArmLength = 0;
            for (int facing = 0; facing < 8; facing++)
            {
                float angle = (facing * 45 - 90) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 ground = ObliqueProjection.UnprojectGround(direction, arena.View.elevation);
                // Stand past the horizontal cut's trigger range to exercise the straight-arm chop.
                arena.Player.Teleport((Vector2)boss.transform.position + ground.normalized * (enemy.attackRange * 1.2f));
                boss.SendMessage("Tick", .04f);
                Check(boss.CastingSkill == "贯血刀" && skin.sprite.rect.x == facing % 4 * atlas.width / 4
                    && skin.sprite.rect.y == (1 - facing / 4) * atlas.height / 2,
                    "Blood Saber did not start casting in facing " + facing);
                gait.Refresh();
                var rest = gait.DeformedMesh.vertices;
                int[] torso = SaberTorsoVertices(rest, facing);
                var triangles = gait.DeformedMesh.GetTriangles(0);
                string casting = "Blood Saber facing " + facing;
                bool profile = facing == 2 || facing == 6;
                var armRest = profile ? saberMesh.vertices : null;
                int hand = profile ? NearestVertex(armRest, facing == 2 ? new Vector2(-.002f, .442f)
                    : new Vector2(-.058f, .460f)) : 0;
                var weaponVertices = bladeMesh.vertices;
                Vector3 blade = profile ? weaponVertices[NearestVertex(weaponVertices,
                    facing == 2 ? new Vector2(.193f, -.320f) : new Vector2(-.143f, -.233f))] : Vector3.zero;
                int shoulder = profile ? NearestVertex(armRest, facing == 2 ? new Vector2(-.009f, .672f)
                    : new Vector2(-.007f, .658f)) : 0;
                int elbow = profile ? NearestVertex(armRest, facing == 2 ? new Vector2(-.011f, .556f)
                    : new Vector2(-.022f, .563f)) : 0;
                if (profile)
                {
                    float armLength = Vector3.Distance(armRest[shoulder], armRest[elbow])
                        + Vector3.Distance(armRest[elbow], armRest[hand]);
                    if (facing == 2) nearArmLength = armLength;
                    else Check(armLength < nearArmLength * .97f, "Blood Saber's far arm was longer than its near arm.");
                }
                bool showedSlash = false, checkedStraightArms = false;
                float peakGrip = float.NegativeInfinity;
                for (int frame = 0; frame < 80 && !enemy.IsChasing; frame++)
                {
                    bool windingUp = boss.CastingSkill != null;
                    boss.SendMessage("Tick", .025f);
                    gait.Refresh();
                    var pose = gait.DeformedMesh.vertices;
                    CheckCastingCore(rest, pose, torso, casting);
                    CheckCastingMesh(rest, pose, triangles, casting);
                    CheckSaberBladeEdge(bladeLayer, bladeMesh, casting);
                    if (profile && windingUp)
                    {
                        Vector3 grip = bladeLayer.localPosition;
                        Vector3 tip = BladeTip(bladeLayer, blade);
                        float forward = facing == 2 ? 1 : -1;
                        Quaternion torsoTurn = Quaternion.FromToRotation(rest[torso[1]] - rest[torso[0]],
                            pose[torso[1]] - pose[torso[0]]);
                        float relativeGrip = (rest[torso[0]] + Quaternion.Inverse(torsoTurn) * (grip - pose[torso[0]])).y;
                        peakGrip = Mathf.Max(peakGrip, relativeGrip);
                        CheckBladeLength(blade, grip, tip);
                        if (bladeTrail.enabled)
                        {
                            var sleeve = saberMesh.vertices;
                            Vector3 upper = sleeve[elbow] - sleeve[shoulder];
                            Vector3 forearm = sleeve[hand] - sleeve[elbow];
                            Check(Vector3.Angle(upper, forearm) < 22,
                                "Blood Saber's arm bent during its straight-arm chop in facing " + facing);
                            checkedStraightArms = true;
                        }
                        if (boss.CastingSkill == null)
                        {
                            Check((tip.x - grip.x) * forward > .1f,
                                "Blood Saber did not chop in front of its torso in profile " + facing);
                            Check(peakGrip > .76f && peakGrip < .84f && relativeGrip > .52f,
                                $"Blood Saber's grip fell outside its chop height in profile {facing} ({peakGrip:F3}, {relativeGrip:F3}).");
                            Check((saberMesh.vertices[hand] - grip).sqrMagnitude < .0005f,
                                "Blood Saber's main hand slipped off the handle in profile " + facing);
                        }
                    }
                    showedSlash |= bladeTrail.enabled && bladeTrail.positionCount >= 2;
                }
                Check(enemy.IsChasing, "Blood Saber did not finish casting in facing " + facing);
                Check(!profile || checkedStraightArms, "Blood Saber did not show its extended arms in facing " + facing);
                Check(showedSlash && !bladeTrail.enabled, "Blood Saber's blade trail did not appear and expire in facing " + facing);
                boss.SendMessage("Stop");
            }
        }

        static void CheckSaberCastFacings(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss,
            SpriteRenderer skin, HeroWalkMesh gait)
        {
            const System.Reflection.BindingFlags fields =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var nextSkill = typeof(EvilCultivatorBoss).GetField("nextSkill", fields);
            var windup = typeof(EvilCultivatorBoss).GetField("windup", fields);
            var rigField = typeof(HeroWalkMesh).GetField("paintedGesture", fields);
            var layer = gait.transform.Find("Saber blade");
            var bladeRenderer = layer.GetComponent<MeshRenderer>();
            var blade = layer.GetComponent<MeshFilter>().sharedMesh;
            var armMesh = gait.transform.Find("Saber arm").GetComponent<MeshFilter>().sharedMesh;
            var support = gait.transform.Find("Saber support arm").GetComponent<MeshRenderer>();
            var atlas = skin.sprite.texture;
            var names = new[] { "饮血横斩", "裂地血潮", "三途飞刃" };
            var rotationSlots = new[] { 0, 3, 4 };
            const float step = .02f;
            for (int facing = 0; facing < 8; facing++)
            for (int attack = 0; attack < names.Length; attack++)
            {
                boss.SendMessage("Stop");
                nextSkill.SetValue(boss, rotationSlots[attack]);
                Vector2 screen = DemoArt.Direction(facing * 45 - 90);
                Vector2 direction = ObliqueProjection.UnprojectGround(screen, arena.View.elevation).normalized;
                arena.Player.Teleport((Vector2)boss.transform.position + direction * (enemy.attackRange * .65f));
                boss.SendMessage("Tick", step);
                gait.Refresh();
                string label = $"{names[attack]} in facing {facing}";
                Check(boss.CastingSkill == names[attack] && skin.sprite.rect.x == facing % 4 * atlas.width / 4
                    && skin.sprite.rect.y == (1 - facing / 4) * atlas.height / 2, "Could not start " + label);
                var rig = rigField.GetValue(gait);
                var arm = rig.GetType().GetField("SaberArm").GetValue(rig);
                var grip = (Vector2)arm.GetType().GetProperty("Grip").GetValue(arm);
                var tip = (Vector2)arm.GetType().GetProperty("ExtendedTip").GetValue(arm);
                var shoulder = (Vector2)arm.GetType().GetProperty("Shoulder").GetValue(arm);
                var elbow = (Vector2)arm.GetType().GetField("elbow", fields).GetValue(arm);
                int wristVertex = NearestVertex(armMesh.vertices, grip);
                int shoulderVertex = NearestVertex(armMesh.vertices, shoulder);
                int elbowVertex = NearestVertex(armMesh.vertices, elbow);
                Vector3 restBlade = tip - grip;
                Vector3 lastAxis = layer.localRotation * restBlade;
                Vector3 lastGrip = layer.localPosition;
                float duration = (float)windup.GetValue(boss);
                bool stabbed = false;
                bool checkFlyingArm = attack == 2 && Mathf.Abs(screen.x) > .9f;
                Vector3 readyFlyingGrip = default, readyFlyingElbow = default, readyFlyingAxis = default;
                float readyFlyingBend = 0;
                bool armLedBlade = false;
                for (int frame = 1; frame < 120 && !enemy.IsChasing; frame++)
                {
                    bool windingUp = boss.CastingSkill != null;
                    boss.SendMessage("Tick", step);
                    gait.Refresh();
                    Vector3 axis = layer.localRotation * restBlade;
                    Vector3 handle = layer.localPosition;
                    var armVertices = armMesh.vertices;
                    CheckSaberBladeEdge(layer, blade, label);
                    float turn = Vector3.Angle(lastAxis, axis);
                    float travel = Vector3.Distance(lastGrip, handle);
                    Check(turn < 40 && travel < .075f,
                        $"Saber snapped during {label} at frame {frame} ({turn:F1}°, grip travel {travel:F3}).");
                    Check(Vector3.Distance(armVertices[wristVertex], handle) < .042f, "Wrist separated from saber during " + label);
                    if (facing == 5 && windingUp && support.enabled)
                        Check(support.sortingOrder < gait.WalkingRenderer.sortingOrder, "The joining hand drew over the northwest cape.");
                    if (attack == 2 && Mathf.Abs(screen.x) > .5f && frame * step >= duration * .52f
                        && frame * step <= duration * .70f)
                    {
                        Check(handle.x * screen.x > .035f && axis.x * screen.x > .09f,
                            $"Flying blades prepared from behind the body during {label} at frame {frame} "
                            + $"(grip {handle.x * screen.x:F3}, blade {axis.x * screen.x:F3}).");
                    }
                    if (attack == 1 && frame * step >= duration * .42f && frame * step <= duration)
                    {
                        Check(armVertices[elbowVertex].y < armVertices[shoulderVertex].y
                            && handle.y < armVertices[shoulderVertex].y, "Saber arm lifted above its shoulder during " + label);
                    }
                    if (attack == 1 && windingUp && boss.CastingSkill == null)
                    {
                        stabbed = true;
                        Vector3 bladeTip = handle + axis;
                        float bladeCenter = (handle.x + bladeTip.x) * .5f;
                        if (facing is >= 3 and <= 5)
                        {
                            Check(bladeRenderer.sortingOrder < gait.WalkingRenderer.sortingOrder
                                && support.sortingOrder < gait.WalkingRenderer.sortingOrder,
                                "Ground stab drew over the body during " + label);
                        }
                        Check(bladeRenderer.enabled == (facing != 4), "Wrong planted blade visibility during " + label);
                        Check(Mathf.Abs(bladeTip.y) < .035f, "Ground stab missed the floor during " + label);
                        if (Mathf.Abs(screen.x) > .5f)
                            Check(bladeCenter * screen.x > .075f && bladeTip.x * screen.x > .11f,
                                "Ground stab did not clear the silhouette during " + label);
                        else
                            Check(Mathf.Abs(bladeCenter) < .02f, "Ground stab missed the center during " + label);
                    }
                    if (checkFlyingArm && windingUp)
                    {
                        Vector3 posedElbow = armVertices[elbowVertex];
                        float bend = Vector3.Angle(posedElbow - armVertices[shoulderVertex], handle - posedElbow);
                        if (frame * step >= duration * .64f && frame * step <= duration * .70f)
                        {
                            readyFlyingGrip = handle;
                            readyFlyingElbow = posedElbow;
                            readyFlyingAxis = axis;
                            readyFlyingBend = bend;
                        }
                        if (frame * step > duration * .70f && (handle.x - readyFlyingGrip.x) * screen.x > .045f
                            && Vector3.Angle(readyFlyingAxis, axis) < 16)
                            armLedBlade = true;
                        if (boss.CastingSkill == null)
                        {
                            float push = (handle.x - readyFlyingGrip.x) * screen.x;
                            float elbowTravel = Vector3.Distance(posedElbow, readyFlyingElbow);
                            Check(armLedBlade && push > .16f && elbowTravel > .055f && readyFlyingBend - bend > 22,
                                $"Flying blades did not extend the arm during {label} "
                                + $"(grip {push:F3}, elbow {elbowTravel:F3}, bend {readyFlyingBend:F1}° to {bend:F1}°).");
                            Check(axis.normalized.y < -.69f, "Flying blades stopped too high during " + label);
                        }
                    }
                    lastAxis = axis;
                    lastGrip = handle;
                }
                Check(enemy.IsChasing && (attack != 1 || stabbed), "Cast failed to finish during " + label);
            }
            boss.SendMessage("Stop");
        }

        static void CheckSaberPlant(SlashArena arena, SlashEnemy enemy, EvilCultivatorBoss boss, HeroWalkMesh gait)
        {
            float range = enemy.attackRange;
            var body = enemy.GetComponent<Rigidbody2D>();
            Vector2 start = body.position;
            Vector2 heading = new Vector2(.24f, -1).normalized;
            enemy.attackRange = 0;
            arena.Player.Teleport(start + heading * 2);
            boss.SendMessage("FixedTick", .02f);
            gait.Refresh();
            var rest = gait.DeformedMesh.vertices;
            int leftBoot = NearestVertex(rest, new Vector2(-.073f, .045f));
            int rightBoot = NearestVertex(rest, new Vector2(.073f, .045f));
            int support = -1, swing = -1;
            float scale = gait.transform.localScale.y * arena.View.characterScale;
            Vector3 Foot(Vector3[] pose, int index) => pose[index] + Vector3.up * (gait.Bob / scale);
            for (int frame = 0; frame < 35 && swing < 0; frame++)
            {
                body.position += heading * .065f;
                boss.SendMessage("Tick", .025f);
                gait.Refresh();
                var pose = gait.DeformedMesh.vertices;
                if (Foot(pose, leftBoot).y - rest[leftBoot].y > .038f) { swing = leftBoot; support = rightBoot; }
                else if (Foot(pose, rightBoot).y - rest[rightBoot].y > .038f) { swing = rightBoot; support = leftBoot; }
            }
            Check(swing >= 0, "Could not start a cast with one foot in the air.");
            var stride = gait.DeformedMesh.vertices;
            Vector3 supportStart = Foot(stride, support), swingStart = Foot(stride, swing);
            enemy.attackRange = range;
            boss.SendMessage("Tick", .01f);
            gait.Refresh();
            var first = gait.DeformedMesh.vertices;
            Check(boss.CastingSkill == "饮血横斩" && Vector3.Distance(Foot(first, swing), swingStart) < .002f,
                "The lifted foot snapped to the ground when the cast started.");
            boss.SendMessage("Tick", .17f);
            gait.Refresh();
            var planted = gait.DeformedMesh.vertices;
            Check(Mathf.Abs(planted[support].x - supportStart.x) < .014f
                && Mathf.Abs(planted[swing].y - rest[swing].y) < .012f, "The step did not settle onto the attack stance.");
            body.position = start;
            body.linearVelocity = Vector2.zero;
            boss.SendMessage("Stop");
        }

        static void CheckBloodTideMist(EvilCultivatorBoss boss, SlashEnemy enemy, SlashPlayer player, SpriteRenderer mist)
        {
            Vector2 previousPosition = player.transform.position;
            Vector2 center = boss.transform.position;
            float previousRange = enemy.attackRange;
            int previousHealth = player.Health, previousBossHealth = enemy.Health;
            bool invincible = player.invincible;
            try
            {
                Check(mist.enabled && (Vector2)mist.transform.position == center,
                    "Blood Tide did not leave mist at its impact point.");
                Check(Mathf.Abs(mist.transform.localScale.x * .5f - 1.488f * previousRange) < .005f,
                    "Blood Tide mist did not cover the expanded radius.");
                enemy.TakeHit(100, Vector2.zero, 0);
                int woundedHealth = enemy.Health;
                enemy.attackRange = 0;
                player.invincible = false;
                Vector2 outside = center + Vector2.right * (2 * previousRange);
                player.Teleport(outside);
                boss.SendMessage("Tick", 1f);
                Check(player.Health == previousHealth && enemy.Health == woundedHealth,
                    "Blood Tide mist dealt damage outside its radius.");
                player.Teleport(center + Vector2.down * (1.43f * previousRange));
                boss.SendMessage("Tick", .5f);
                Check(player.Health == previousHealth, "Blood Tide mist ticked before one second of exposure.");
                boss.SendMessage("Tick", .5f);
                Check(player.Health == previousHealth - 2 && enemy.Health == woundedHealth + 20,
                    "Blood Tide mist did not deal two damage and heal Blood Saber for twenty.");
                boss.SendMessage("Tick", 2f);
                Check(player.Health == previousHealth - 6 && enemy.Health == woundedHealth + 60,
                    "Blood Tide mist did not continue to damage and heal once per second.");
                player.Teleport(outside);
                boss.SendMessage("Tick", 3.5f);
                Check(player.Health == previousHealth - 6 && mist.enabled, "Blood Tide mist expired early or hurt the player outside.");
                boss.SendMessage("Tick", .6f);
                player.Teleport(center);
                boss.SendMessage("Tick", 1f);
                Check(player.Health == previousHealth - 6 && !mist.enabled,
                    "Blood Tide mist continued after eight seconds.");
            }
            finally
            {
                enemy.attackRange = previousRange;
                enemy.SendMessage("Heal", previousBossHealth - enemy.Health);
                player.invincible = invincible;
                player.Heal(previousHealth - player.Health);
                player.Teleport(previousPosition);
            }
        }

        static Vector3 BladeTip(Transform bladeLayer, Vector3 blade)
            => bladeLayer.localPosition + bladeLayer.localRotation * Vector3.Scale(blade, bladeLayer.localScale);

        static void CheckBladeLength(Vector3 blade, Vector3 grip, Vector3 tip)
        {
            Check(Mathf.Abs(blade.magnitude - Vector3.Distance(grip, tip)) < .001f,
                "Blood Saber blade stretched during the arm motion.");
        }

        static Vector2 SaberRestAxis(Mesh bladeMesh)
        {
            // The last four vertices are the handle extension, ordered back pair then front pair.
            var vertices = bladeMesh.vertices;
            int end = vertices.Length;
            return ((Vector2)(vertices[end - 1] + vertices[end - 2]
                - vertices[end - 3] - vertices[end - 4])).normalized;
        }

        static void CheckSaberBladeEdge(Transform bladeLayer, Mesh bladeMesh, string skill)
        {
            Vector2 restAxis = SaberRestAxis(bladeMesh);
            Vector2 redEdge = -Mathf.Sign(restAxis.x) * Vector2.Perpendicular(restAxis);
            Vector2 posedAxis = bladeLayer.localRotation * (Vector3)restAxis;
            Vector2 posedEdge = bladeLayer.localRotation * (Vector3)redEdge;
            float edgeSide = Vector3.Cross(posedAxis, posedEdge).z * Mathf.Sign(posedAxis.x);
            Check(edgeSide <= .0001f, $"Blood Saber's red edge turned above the spine during {skill}.");
        }

        static void CheckSaberSweep(Transform art, Vector3 grip, Vector3 tip, Vector3 edge, string skill)
        {
            Vector3 bladeDirection = art.TransformDirection(tip - grip);
            Check(Vector3.Angle(bladeDirection, edge) < 12,
                $"Blood Saber's blade did not align with the fan edge during {skill} ({bladeDirection:F3}, {edge:F3}).");
        }

        static void CheckTwoHandedGrip(Vector3 grip, Vector3 tip, Vector3 support, string skill)
        {
            Vector3 axis = (tip - grip).normalized;
            Vector3 expected = grip - axis * .043f;
            Check(((Vector2)(support - expected)).sqrMagnitude < .00065f,
                $"Blood Saber's second hand slipped off the handle during {skill} ({support:F3}, expected {expected:F3}).");
        }

        static int CountBossEffectLines(Transform effects)
        {
            int count = 0;
            foreach (var line in effects.GetComponentsInChildren<LineRenderer>())
                if (line.enabled) count++;
            return count;
        }

        static void CheckBossLocomotion(SlashArena arena, SlashEnemy enemy, SpriteRenderer skin)
        {
            const float step = .04f;
            var body = enemy.GetComponent<Rigidbody2D>();
            var gait = skin.GetComponent<HeroWalkMesh>();
            Check(gait != null && gait.WalkingRenderer.enabled && skin.forceRenderingOff,
                "Final boss did not initialize its walking mesh.");
            Vector2 spawn = body.position;
            Vector2 initialVelocity = body.linearVelocity;
            Vector2 playerPosition = arena.Player.transform.position;
            float attackRange = enemy.attackRange;
            bool usesBell = enemy.GetComponent<EvilCultivatorBoss>().CombatStyle == EvilCultivatorBoss.Style.SoulBell;
            var saberLayer = usesBell ? null : skin.transform.Find("Saber arm");
            Check(usesBell || saberLayer != null, "Blood Saber is missing its separate arm for the walking pose.");
            var saberMesh = saberLayer != null ? saberLayer.GetComponent<MeshFilter>().sharedMesh : null;
            var saberRenderer = saberLayer != null ? saberLayer.GetComponent<MeshRenderer>() : null;
            var bladeLayer = usesBell ? null : skin.transform.Find("Saber blade");
            Check(usesBell || bladeLayer != null, "Blood Saber is missing its rigid blade for the walking pose.");
            var bladeRenderer = bladeLayer != null ? bladeLayer.GetComponent<MeshRenderer>() : null;
            var bodyProperties = new MaterialPropertyBlock();
            float frontFootX = usesBell ? .025f : .073f;
            var center = arena.Travel.ActiveMap.Definition.Ground(755, 500);
            enemy.attackRange = 0;
            var atlas = skin.sprite.texture;
            for (int i = 0; i < 8; i++)
            {
                body.position = center;
                float angle = (i * 45 - 90) * Mathf.Deg2Rad;
                var screen = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var ground = ObliqueProjection.UnprojectGround(screen, arena.View.elevation);
                arena.Player.Teleport(center + ground.normalized * 2);
                Physics2D.SyncTransforms();
                enemy.SendMessage("FixedUpdate");
                Check(body.linearVelocity.sqrMagnitude > .01f, "Final boss did not move for direction " + i);
                Check(skin.sprite.texture == atlas && skin.sprite.rect.x == i % 4 * atlas.width / 4
                    && skin.sprite.rect.y == (1 - i / 4) * atlas.height / 2,
                    "Final boss displayed the wrong movement direction " + i);
                enemy.SendMessage("Stop");
                gait.Refresh();
                var resting = gait.DeformedMesh.vertices;
                var restingSaber = saberMesh != null ? saberMesh.vertices : null;
                Vector3 restingGrip = bladeLayer != null ? bladeLayer.localPosition : Vector3.zero;
                bool profile = i == 2 || i == 6;
                bool bellProfile = usesBell && profile;
                float trailingHemX = i == 2 ? -.19f : .19f;
                int hem = NearestVertex(resting, new Vector2(trailingHemX, .065f));
                int drape = NearestVertex(resting, new Vector2(trailingHemX, .22f));
                var farLeg = skin.transform.Find("Far profile leg");
                var farRenderer = farLeg != null ? farLeg.GetComponent<MeshRenderer>() : null;
                var farMesh = profile && farLeg != null ? farLeg.GetComponent<MeshFilter>().sharedMesh : null;
                Check(!profile || farMesh != null, "Final boss is missing its far profile leg.");
                Check(farRenderer == null || !farRenderer.enabled, "Far profile leg is visible in the resting pose.");
                Vector3 farRest = profile ? farMesh.vertices[0] : Vector3.zero;
                Vector2 velocity = body.linearVelocity;
                var triangles = gait.DeformedMesh.GetTriangles(0);
                int leftBoot = NearestVertex(resting, new Vector2(-frontFootX, .05f));
                int rightBoot = NearestVertex(resting, new Vector2(frontFootX, .05f));
                int shin = NearestVertex(resting, new Vector2(0, .25f));
                float leftLift = 0, rightLift = 0, forwardSwing = 0, shinBend = 0;
                float farAhead = 0, farBehind = 0;
                bool tookStep = false;
                for (int frame = 0; frame < 40; frame++)
                {
                    body.position += velocity * step;
                    enemy.SendMessage("Tick", step);
                    gait.Refresh();
                    var walking = gait.DeformedMesh.vertices;
                    if (saberMesh != null)
                    {
                        gait.WalkingRenderer.GetPropertyBlock(bodyProperties);
                        Check(saberRenderer.enabled && bladeRenderer.enabled && bodyProperties.GetFloat("_SaberCutMode") == 1,
                            "Blood Saber did not use its separate knife layer while walking.");
                        var walkingSaber = saberMesh.vertices;
                        Quaternion carryTurn = CheckRigidSaberCarry(restingSaber, walkingSaber, bladeLayer.localRotation, i);
                        Vector3 expectedGrip = walkingSaber[0] + carryTurn * (restingGrip - restingSaber[0]);
                        float gripError = Vector3.Distance(bladeLayer.localPosition, expectedGrip);
                        float turnError = Mathf.Abs(Mathf.DeltaAngle(bladeLayer.localEulerAngles.z, carryTurn.eulerAngles.z));
                        Check(gripError < .001f && turnError < .1f
                            && bladeLayer.localScale == Vector3.one,
                            $"Blood Saber's blade slipped while walking in facing {i} (grip error {gripError:F5}, angle error {turnError:F3}).");
                    }
                    tookStep |= HasBossStep(resting, walking);
                    Vector3 leftOffset = walking[leftBoot] - resting[leftBoot];
                    Vector3 rightOffset = walking[rightBoot] - resting[rightBoot];
                    leftLift = Mathf.Max(leftLift, leftOffset.y - rightOffset.y);
                    rightLift = Mathf.Max(rightLift, rightOffset.y - leftOffset.y);
                    forwardSwing = Mathf.Max(forwardSwing, Mathf.Abs(leftOffset.x));
                    shinBend = Mathf.Max(shinBend, (leftOffset - (walking[shin] - resting[shin])).magnitude);
                    CheckBossBootShape(resting, walking, triangles, i, bellProfile);
                    if (bellProfile)
                        Check((walking[hem] - resting[hem]).sqrMagnitude < .000225f
                            && (walking[drape] - resting[drape]).sqrMagnitude < .000225f,
                            "Soul Bell's robe is following the leg stride in profile " + i);
                    if (profile)
                    {
                        Check(farRenderer.enabled && farRenderer.sortingOrder < gait.WalkingRenderer.sortingOrder,
                            "Final boss far leg must render behind the walking portrait.");
                        float separation = farMesh.vertices[0].x - farRest.x - leftOffset.x;
                        farAhead = Mathf.Max(farAhead, separation);
                        farBehind = Mathf.Max(farBehind, -separation);
                    }
                }
                Check(gait.Phase > 0 && tookStep,
                    "Final boss did not deform the lower body while moving in direction " + i);
                if (i == 0)
                    Check(leftLift > .045f && rightLift > .045f, "Final boss did not visibly lift each foot in turn.");
                if (profile)
                {
                    Check(forwardSwing > .05f && shinBend > .035f, "Final boss did not swing and bend its visible leg in profile.");
                    Check(farAhead > .08f && farBehind > .08f, "Final boss profile legs did not pass and visibly separate.");
                }

                float phase = gait.Phase;
                for (int frame = 0; frame < 60; frame++) enemy.SendMessage("Tick", step);
                gait.Refresh();
                Check(gait.Phase == phase && gait.Bob == 0 && !HasBossStep(resting, gait.DeformedMesh.vertices),
                    "Final boss gait did not settle after movement stopped.");
                Check(farRenderer == null || !farRenderer.enabled, "Far profile leg did not settle into the resting portrait.");
                if (saberRenderer != null)
                {
                    gait.WalkingRenderer.GetPropertyBlock(bodyProperties);
                    Check(saberRenderer.enabled && bodyProperties.GetFloat("_SaberCutMode") == 1,
                        "Blood Saber changed back to the shorter baked blade after stopping.");
                }
            }

            var last = skin.sprite;
            body.position = center;
            arena.Player.Teleport(center);
            Physics2D.SyncTransforms();
            enemy.SendMessage("FixedUpdate");
            Check(body.linearVelocity == Vector2.zero && skin.sprite == last,
                "Final boss did not keep its facing when movement stopped.");

            enemy.attackRange = attackRange;
            float walkingPhase = gait.Phase;
            enemy.SendMessage("Tick", step);
            Check(!enemy.IsChasing && gait.Phase == (usesBell ? 0 : walkingPhase),
                "Final boss did not carry the intended walk phase into the attack.");
            enemy.SendMessage("Tick", .18f);
            Check(gait.Bob == 0, "Final boss did not settle its walking bob during the windup.");

            body.position = spawn;
            body.linearVelocity = initialVelocity;
            arena.Player.Teleport(playerPosition);
            Physics2D.SyncTransforms();
            enemy.SendMessage("Stop");
        }

        static Quaternion CheckRigidSaberCarry(Vector3[] rest, Vector3[] walking, Quaternion bladeTurn, int facing)
        {
            int last = rest.Length - 1;
            Vector2 original = rest[last] - rest[0], current = walking[last] - walking[0];
            float angle = Mathf.Atan2(Vector3.Cross(original, current).z, Vector2.Dot(original, current)) * Mathf.Rad2Deg;
            // The cape hides the northwest sleeve. Its mesh is collapsed and has no axis.
            Quaternion turn = original.sqrMagnitude > .000001f ? Quaternion.AngleAxis(angle, Vector3.forward) : bladeTurn;
            for (int i = 1; i < rest.Length; i++)
                if ((walking[i] - walking[0] - turn * (rest[i] - rest[0])).sqrMagnitude > .00000001f)
                    throw new Exception("Blood Saber's sleeve stretched while walking in facing " + facing);
            return turn;
        }

        static bool HasBossStep(Vector3[] rest, Vector3[] pose)
        {
            for (int i = 0; i < rest.Length; i++)
                if (rest[i].y >= 0 && rest[i].y < .16f && Mathf.Abs(rest[i].x) < .14f
                    && Vector3.SqrMagnitude(rest[i] - pose[i]) > .000001f) return true;
            return false;
        }

        static int NearestVertex(Vector3[] vertices, Vector2 point)
        {
            int best = 0;
            for (int i = 1; i < vertices.Length; i++)
                if (((Vector2)vertices[i] - point).sqrMagnitude < ((Vector2)vertices[best] - point).sqrMagnitude) best = i;
            return best;
        }

        static void CheckBossBootShape(Vector3[] rest, Vector3[] pose, int[] triangles, int facing, bool bellProfile)
        {
            bool profile = facing == 2 || facing == 6;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (rest[a].y < -.015f || rest[a].y > .34f || Mathf.Abs(rest[a].x) >= .16f) continue;
                var ab = pose[b] - pose[a];
                var ac = pose[c] - pose[a];
                var restAB = rest[b] - rest[a];
                var restAC = rest[c] - rest[a];
                float areaRatio = Vector3.Cross(ab, ac).z / Vector3.Cross(restAB, restAC).z;
                Check(areaRatio > .1f, "Final boss lower leg mesh folded in facing " + facing);
                if (!IsBootVertex(rest[a], bellProfile) || !IsBootVertex(rest[b], bellProfile)
                    || !IsBootVertex(rest[c], bellProfile)) continue;
                Check(areaRatio > .4f && areaRatio < 1.6f, "Final boss boot mesh pinched or folded in facing " + facing);
                if (profile)
                    Check((ab - restAB).sqrMagnitude < .00000001f && (ac - restAC).sqrMagnitude < .00000001f,
                        "Final boss visible profile boot changed shape during a step.");
            }
        }

        static bool IsBootVertex(Vector3 point, bool bellProfile) => bellProfile
            ? point.y >= -.015f && point.y <= .06f && Mathf.Abs(point.x) <= .03f
            : point.y >= -.015f && point.y <= .14f && Mathf.Abs(point.x) < .16f;

        static void ValidateMap(TravelMapEnvironment map, float radius)
        {
            Check(map != null, "No destination environment.");
            Check(map.ReservedExit != null && map.ReservedExit.isTrigger, "Missing unlinked exit reservation.");
            var entry = map.Definition.entry;
            Vector2[] route;
            switch (map.Definition.destination)
            {
                case TravelDestination.AscensionPlatform:
                    route = new[] { entry, new Vector2(755, 735), new Vector2(755, 500), new Vector2(755, 350) };
                    break;
                case TravelDestination.Wilderness:
                    route = new[] { entry, new Vector2(800, 740), new Vector2(800, 520), new Vector2(800, 270) };
                    break;
                case TravelDestination.Tribulation:
                    route = new[] { entry, new Vector2(765, 610), TravelMaps.TribulationCenter };
                    break;
                case TravelDestination.FinalBattle:
                    Check(map.Definition.WorldSize == TravelMaps.Get(TravelDestination.Wilderness).WorldSize,
                        "Final arena is not sized like the other battle maps.");
                    route = new[] { entry, new Vector2(755, 500), new Vector2(755, 255) };
                    var center = map.Definition.Ground(755, 500);
                    foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                    {
                        bool blocked = false;
                        foreach (var hit in Physics2D.CircleCastAll(center, radius, direction, map.Definition.WorldSize.x))
                            blocked |= BlocksRoute(hit.collider, map);
                        Check(blocked, "Final arena boundary did not block travel toward " + direction);
                    }
                    break;
                case TravelDestination.Market:
                    route = new[] { entry, new Vector2(758, 313), new Vector2(421, 305),
                        new Vector2(758, 313), new Vector2(758, 612), new Vector2(1130, 612) };
                    break;
                default:
                    route = new[] { entry, new Vector2(1170, 740), new Vector2(1235, 815),
                        new Vector2(1170, 740), new Vector2(1000, 650), new Vector2(930, 520),
                        new Vector2(925, 460), new Vector2(900, 400), new Vector2(800, 360) };
                    break;
            }
            Physics2D.SyncTransforms();
            foreach (var point in route)
                foreach (var collider in Physics2D.OverlapCircleAll(map.Definition.Ground(point.x, point.y), radius))
                    Check(!BlocksRoute(collider, map), "Blocked entrance route point " + point + " by " + collider.name);
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = map.Definition.Ground(route[i - 1].x, route[i - 1].y);
                Vector2 b = map.Definition.Ground(route[i].x, route[i].y);
                foreach (var hit in Physics2D.CircleCastAll(a, radius, b - a, (b - a).magnitude))
                    Check(!BlocksRoute(hit.collider, map), "Blocked entrance route by " + hit.collider.name);
            }
        }

        static bool BlocksRoute(Collider2D collider, TravelMapEnvironment map)
            => !collider.isTrigger && collider.transform.IsChildOf(map.transform) && collider.GetComponentInParent<SlashEnemy>() == null;
    }
}
