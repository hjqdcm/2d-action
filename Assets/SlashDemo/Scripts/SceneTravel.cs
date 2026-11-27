using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class SceneTravel : MonoBehaviour
    {
        SlashArena arena;
        Transform manor;
        Color manorBackground;
        bool gateArmed = true, waitingForRelease;
        int releaseAfterFrame;
        string error;
        readonly Queue<AlchemyEntry[]> lootChoices = new Queue<AlchemyEntry[]>();
        WildernessEncounter wildernessEncounter;
        AscensionChallenge ascension;
        TribulationChallenge tribulation;
        Transform tribulationMarker;
        Transform treatmentMarker;
        Transform pillStallMarker, treasureStallMarker;
        readonly List<(SlashEnemy enemy, bool active)> manorEnemies = new List<(SlashEnemy, bool)>();
        public TravelDestination Current { get; private set; } = TravelDestination.Manor;
        public TravelMapEnvironment ActiveMap { get; private set; }
        public TeleportFormation Portal { get; private set; }
        public bool ShowPortalPrompt => Portal != null && arena != null && arena.Player != null
            && arena.Player.IsAlive && !arena.Defeated && !arena.InputBlocked
            && (arena.Tuning == null || !arena.Tuning.IsOpen)
            && Vector2.Distance(arena.Player.transform.position, Portal.transform.position) <= TeleportFormation.InteractionRadius;
        public bool IsSelectionOpen { get; private set; }
        public bool IsChallengeDecisionOpen { get; private set; }
        public bool IsTribulationDecisionOpen { get; private set; }
        public bool IsTribulationActive => tribulation != null && tribulation.isActiveAndEnabled;
        public int TribulationRound => IsTribulationActive ? tribulation.Round : 0;
        public bool TribulationCleared { get; private set; }
        public bool ShowTribulationPrompt => tribulationMarker != null && arena.Player.IsAlive && !arena.InputBlocked
            && (arena.Tuning == null || !arena.Tuning.IsOpen)
            && Vector2.Distance(arena.Player.transform.position, tribulationMarker.position) <= 1.55f;
        public bool IsTreatmentOpen { get; private set; }
        public bool ShowTreatmentPrompt => treatmentMarker != null && arena.Player.IsAlive && !arena.InputBlocked
            && (arena.Tuning == null || !arena.Tuning.IsOpen)
            && Vector2.Distance(arena.Player.transform.position, treatmentMarker.position) <= 1.35f;
        public bool IsShopOpen { get; private set; }
        public bool ShowShopPrompt => Current == TravelDestination.Market && arena.Player.IsAlive && !arena.InputBlocked
            && (arena.Tuning == null || !arena.Tuning.IsOpen)
            && (NearStall(pillStallMarker) || NearStall(treasureStallMarker));
        public const int TreatmentCost = 0;
        public const int TreatmentHealPercent = 50;
        const int AscensionRefundRound = 3;
        public bool CanContinueChallenge => IsChallengeDecisionOpen && ascension.HasNext;
        public int ChallengeRound => ascension?.Round ?? 0;
        public int ChallengeCompletedRounds => ascension?.CompletedRounds ?? 0;
        public bool ChallengeEndedByDefeat => ascension != null && Portal != null && ascension.CompletedRounds < ascension.Round;
        public string DefeatedChallenger { get; private set; }
        public bool IsLootChoiceOpen => lootChoices.Count > 0;
        internal int RemainingLootChoices => lootChoices.Count;
        internal IReadOnlyList<AlchemyEntry> LootOptions => lootChoices.Peek();
        internal int LootSpiritStones { get; private set; }
        public bool HasModal => IsSelectionOpen || IsLootChoiceOpen || IsChallengeDecisionOpen
            || IsTribulationDecisionOpen || IsTreatmentOpen || IsShopOpen;
        public string Error => error;
        public bool BlocksGameplayInput => HasModal || waitingForRelease;
        public static Rect ManorGate => ManorEnvironment.GroundRect(new Rect(630, 770, 100, 75));

        AscensionCultivator ActiveCultivator
        {
            get
            {
                var opponent = ascension?.Opponent;
                return opponent != null && opponent.Alive ? opponent.GetComponent<AscensionCultivator>() : null;
            }
        }

        internal Vector2 ChallengePull(Vector2 position) => ActiveCultivator?.FieldPull(position) ?? Vector2.zero;
        internal float ChallengeSlowRemaining => ActiveCultivator?.SealSlowRemaining ?? 0;

        public void Initialize(SlashArena owner)
        {
            arena = owner;
            manor = owner.GetComponentInChildren<ManorEnvironment>().transform;
            manorBackground = owner.WorldCamera.backgroundColor;
        }

        void Update()
        {
            if (arena == null || arena.Player == null) return;
            if (waitingForRelease && Time.frameCount > releaseAfterFrame
                && !DemoInput.GameplayHeld)
                waitingForRelease = false;
            // Finish the current encounter before exhaustion sends the player onward.
            if (!arena.InputBlocked && arena.Player.IsAlive && (arena.Tuning == null || !arena.Tuning.IsOpen)
                && arena.Resources.FortunePoints == 0 && !arena.Resources.FinalBattleEnteredOnExhaustion
                && (Current == TravelDestination.Manor || (Portal != null
                    && (Current != TravelDestination.Tribulation || TribulationCleared))))
            {
                if (!EnterDestination(TravelDestination.FinalBattle))
                {
                    IsSelectionOpen = true;
                    PauseForModal();
                }
                return;
            }
            if (Current != TravelDestination.Manor)
            {
                if (IsShopOpen)
                {
                    if (DemoInput.InteractPressed && !DemoInput.PausePressed) CloseShop();
                    return;
                }
                if (DemoInput.InteractPressed && !TryOpenShop() && !TryOpenTreatment() && !TryOpenTribulation())
                    TryOpenPortal();
                return;
            }
            if (IsSelectionOpen) return;
            Vector2 position = arena.Player.transform.position;
            if (!ManorGate.Contains(position))
            {
                var rearm = ManorGate;
                rearm.xMin -= 0.5f; rearm.xMax += 0.5f;
                rearm.yMin -= 0.5f; rearm.yMax += 0.5f;
                if (!rearm.Contains(position)) gateArmed = true;
                return;
            }
            if (!gateArmed || arena.Paused || !arena.Player.IsAlive
                || (arena.Tuning != null && arena.Tuning.IsOpen)) return;
            gateArmed = false;
            OpenSelection();
        }

        void OpenSelection()
        {
            error = null;
            IsSelectionOpen = true;
            PauseForModal();
        }

        void PauseForModal()
        {
            arena.Player.CancelHeavyAttack();
            if (!arena.Paused) arena.TogglePause();
        }

        public bool TryOpenPortal()
        {
            if (!ShowPortalPrompt || IsSelectionOpen) return false;
            if ((arena.Codex != null && arena.Codex.ConsumedInputThisFrame)
                || (arena.CharacterPanel != null && arena.CharacterPanel.ConsumedInputThisFrame)) return false;
            OpenSelection();
            return true;
        }

        public bool TryOpenTreatment()
        {
            if (!ShowTreatmentPrompt) return false;
            IsTreatmentOpen = true;
            PauseForModal();
            return true;
        }

        bool TryOpenTribulation()
        {
            if (!ShowTribulationPrompt) return false;
            IsTribulationDecisionOpen = true;
            PauseForModal();
            return true;
        }

        public void ResolveTribulation(bool begin)
        {
            if (!IsTribulationDecisionOpen || Current != TravelDestination.Tribulation || !arena.Player.IsAlive) return;
            if (begin)
            {
                if (Portal != null)
                {
                    Portal.gameObject.SetActive(false);
                    Destroy(Portal.gameObject);
                    Portal = null;
                }
                tribulationMarker.gameObject.SetActive(false);
                Destroy(tribulationMarker.gameObject);
                tribulationMarker = null;
                tribulation = TribulationChallenge.Begin(arena, ActiveMap, FinishTribulation);
            }
            else SpawnTribulationPortal();
            CloseModal();
        }

        void SpawnTribulationPortal()
        {
            if (Portal == null) Portal = TeleportFormation.Spawn(ActiveMap, ActiveMap.Definition.Entry, 1.1f);
        }

        void FinishTribulation(bool survived)
        {
            tribulation = null;
            if (!survived) return;
            var player = arena.Player;
            if (player.Realm == CultivationRealm.QiRefining)
                player.SetRealm(CultivationRealm.FoundationEstablishment);
            arena.Resources.FoundationTribulationCleared = true;
            arena.FinishPotionBattle();
            TribulationCleared = true;
            SpawnTribulationPortal();
        }

        bool NearStall(Transform marker) => marker != null
            && Vector2.Distance(arena.Player.transform.position, marker.position) <= 1.35f;

        bool TryOpenShop()
        {
            if (!ShowShopPrompt) return false;
            IsShopOpen = true;
            PauseForModal();
            return true;
        }

        internal void CloseShop()
        {
            if (IsShopOpen) CloseModal();
        }

        public void ResolveTreatment(bool heal)
        {
            var player = arena.Player;
            if (!IsTreatmentOpen || Current != TravelDestination.HealingLodge || !player.IsAlive || arena.Defeated) return;

            // On the courtyard paving just below the treatment room steps.
            Portal = TeleportFormation.Spawn(ActiveMap, ActiveMap.Definition.Ground(965, 574), 1.8f);
            if (heal) player.Heal(Mathf.CeilToInt(player.EffectiveMaxHealth * (TreatmentHealPercent / 100f)));
            Destroy(treatmentMarker.gameObject);
            treatmentMarker = null;
            CloseModal();
        }

        void PlaceTreatmentMarker(TravelMapEnvironment map)
        {
            treatmentMarker = DemoArt.InteractionLight("床边疗伤光点", map.transform, map.Definition.Ground(550, 310), 1210);
        }

        public void EnemyDefeated(SlashEnemy defeated)
        {
            if (Current == TravelDestination.Manor || ActiveMap == null || Portal != null
                || defeated.trainingDummy || !defeated.transform.IsChildOf(ActiveMap.transform)) return;
            if (ascension != null)
            {
                if (IsChallengeDecisionOpen || !arena.Player.IsAlive || !ascension.RecordVictory(defeated)) return;
                if (ascension.CompletedRounds == AscensionRefundRound) arena.Resources.TryChangeFortunePoints(1);
                if (!ascension.HasNext) HealChallengeVictory();
                DefeatedChallenger = defeated.name;
                IsChallengeDecisionOpen = true;
                PauseForModal();
                return;
            }
            foreach (var enemy in arena.Enemies)
                if (enemy != null && enemy.Alive && !enemy.trainingDummy && enemy.gameObject.activeInHierarchy) return;
            if (Current == TravelDestination.FinalBattle && !defeated.Alive
                && defeated.TryGetComponent<EvilCultivatorBoss>(out var boss)
                && boss.CombatStyle == EvilCultivatorBoss.Style.BloodSaber)
                arena.Resources.RecordBloodSaberDefeat();
            var venom=ActiveMap.GetComponent<SerpentVenomField>();
            if(venom!=null) venom.Clear();
            ActiveMap.ReleaseCombatGate();
            Portal = TeleportFormation.Spawn(ActiveMap, defeated.transform.position);
            if (Current == TravelDestination.Wilderness && arena.Player.IsAlive && !arena.Defeated)
            {
                lootChoices.Enqueue(AlchemyLoot.RollOptions(PillCatalog.All));
                var resources = arena.Resources;
                LootSpiritStones = Mathf.Min(Random.Range(10, 31), int.MaxValue - resources.SpiritStones);
                resources.TryChangeSpiritStones(LootSpiritStones);
                PauseForModal();
            }
        }

        public bool TryClaimLoot(int index)
        {
            if (!lootChoices.TryPeek(out var options) || index < 0 || index >= options.Length
                || !arena.Player.IsAlive || arena.Defeated) return false;

            arena.Resources.CollectLoot(options[index], isTreasure: Current == TravelDestination.AscensionPlatform);
            if (Current == TravelDestination.AscensionPlatform) arena.Player.AcquireTreasure(options[index]);
            if (Current == TravelDestination.Wilderness) arena.Resources.RefillPotionSlots();
            lootChoices.Dequeue();
            if (!IsLootChoiceOpen) CloseModal();
            return true;
        }

        public void ResolveChallenge(bool continueChallenge)
        {
            if (!IsChallengeDecisionOpen || !arena.Player.IsAlive || arena.Defeated
                || (continueChallenge && !ascension.HasNext)) return;

            if (continueChallenge)
            {
                arena.Effects.Clear();
                ascension.SpawnNext(arena, ActiveMap);
                HealChallengeVictory();
                CloseModal();
            }
            else
            {
                Portal = TeleportFormation.Spawn(ActiveMap, ascension.Opponent.transform.position);
                IsChallengeDecisionOpen = false;
                DefeatedChallenger = null;
                OfferChallengeLoot();
            }
        }

        void OfferChallengeLoot()
        {
            for (int i = 0; i < ascension.CompletedRounds; i++)
                lootChoices.Enqueue(AlchemyLoot.RollOptions(TreasureCatalog.All));
            if (IsLootChoiceOpen) PauseForModal();
        }

        void HealChallengeVictory()
        {
            var player = arena.Player;
            player.Heal(Mathf.CeilToInt(player.EffectiveMaxHealth * .5f));
        }

        internal bool TrySettleChallengeDefeat(SlashEnemy opponent)
        {
            if (ascension == null || Portal != null || HasModal || opponent == null || ascension.Opponent != opponent || !opponent.Alive)
                return false;

            Portal = TeleportFormation.Spawn(ActiveMap, arena.Player.transform.position);
            ascension.RemoveOpponent(arena);
            arena.Effects.Clear();
            OfferChallengeLoot();
            return true;
        }

        public void CancelSelection()
        {
            if (!IsSelectionOpen) return;
            CloseModal();
        }

        public static int FortuneCost(TravelDestination destination) => destination switch
        {
            TravelDestination.Wilderness => 1,
            TravelDestination.AscensionPlatform => 4,
            _ => 0
        };

        public static bool UsesServiceVisit(TravelDestination destination) =>
            destination == TravelDestination.HealingLodge || destination == TravelDestination.Market;

        public bool ShowsDestination(TravelDestination destination)
        {
            var resources = arena.Resources;
            if (destination == TravelDestination.Tribulation)
                return !resources.FoundationTribulationCleared || resources.BloodSaberDefeated;
            return !UsesServiceVisit(destination) || resources.ServicesUnlocked;
        }

        public bool CanSelectDestination(TravelDestination destination) => destination != TravelDestination.Manor
            && ShowsDestination(destination) && arena.Resources.FortunePoints >= FortuneCost(destination)
            && (destination != TravelDestination.Tribulation || arena.Resources.TribulationUnlocked)
            && (!UsesServiceVisit(destination) || arena.Resources.ServiceVisits > 0);

        public void SelectDestination(TravelDestination destination)
        {
            if (IsSelectionOpen && arena.Player.IsAlive && CanSelectDestination(destination)) EnterDestination(destination);
        }

        bool EnterDestination(TravelDestination destination)
        {
            TravelMapEnvironment map;
            AscensionChallenge nextChallenge = null;
            Texture2D finalBossArt = null;
            const EvilCultivatorBoss.Style finalBossStyle = EvilCultivatorBoss.Style.BloodSaber;
            try
            {
                if (destination == TravelDestination.AscensionPlatform) nextChallenge = new AscensionChallenge();
                if (destination == TravelDestination.FinalBattle)
                    finalBossArt = EvilCultivatorBoss.LoadArt(finalBossStyle);
                map = TravelMapEnvironment.Build(manor.parent, TravelMaps.Get(destination));
            }
            catch (System.Exception exception)
            {
                error = "场景暂时无法进入，请重试";
                Debug.LogException(exception);
                return false;
            }
            // Preserve the player object, health, tuning and camera; only transient motion is cleared.
            arena.Effects.Clear();
            arena.Player.ResetAlchemyBattle();
            manor.gameObject.SetActive(false);
            if (Current == TravelDestination.Manor)
            {
                manorEnemies.Clear();
                foreach (var enemy in arena.Enemies)
                    if (enemy != null)
                    {
                        manorEnemies.Add((enemy, enemy.gameObject.activeSelf));
                        enemy.gameObject.SetActive(false);
                    }
                foreach (var entry in manorEnemies) arena.UnregisterEnemy(entry.enemy);
            }
            else UnloadMap();
            Current = destination;
            ActiveMap = map;
            ascension = nextChallenge;
            arena.WorldCamera.backgroundColor = map.Definition.MistColor;
            arena.Player.Teleport(map.Definition.Entry);
            Physics2D.SyncTransforms();
            if (destination == TravelDestination.Wilderness)
                (wildernessEncounter ??= new WildernessEncounter()).SpawnPack(arena, map);
            if (destination == TravelDestination.FinalBattle)
                EvilCultivatorBoss.Spawn(arena, map, finalBossStyle, finalBossArt);
            if (destination == TravelDestination.HealingLodge) PlaceTreatmentMarker(map);
            if (destination == TravelDestination.Tribulation)
            {
                var center = TravelMaps.TribulationCenter;
                tribulationMarker = DemoArt.InteractionLight("渡劫光点", map.transform,
                    map.Definition.Ground(center.x, center.y), 1210, roundCore: true);
            }
            if (destination == TravelDestination.Market)
            {
                var position = TravelMaps.MarketPortal;
                Portal = TeleportFormation.Spawn(map, map.Definition.Ground(position.x, position.y), .9f);
                pillStallMarker = DemoArt.InteractionLight("丹药摊光球", map.transform, map.Definition.Ground(421, 305), 1210, roundCore: true);
                treasureStallMarker = DemoArt.InteractionLight("法器摊光球", map.transform, map.Definition.Ground(1130, 612), 1210, roundCore: true);
            }
            if (ascension != null) ascension.SpawnNext(arena, map);
            var resources = arena.Resources;
            resources.TryChangeFortunePoints(-FortuneCost(destination));
            if (UsesServiceVisit(destination)) resources.UseServiceVisit();
            if (destination == TravelDestination.FinalBattle && resources.FortunePoints == 0)
                resources.FinalBattleEnteredOnExhaustion = true;
            CloseModal();
            arena.CameraRig.ResetFeedback();
            return true;
        }

        void CloseModal()
        {
            IsSelectionOpen = false;
            IsChallengeDecisionOpen = false;
            IsTribulationDecisionOpen = false;
            IsTreatmentOpen = false;
            IsShopOpen = false;
            lootChoices.Clear();
            LootSpiritStones = 0;
            DefeatedChallenger = null;
            error = null;
            waitingForRelease = true;
            releaseAfterFrame = Time.frameCount;
            if (arena.Paused) arena.TogglePause();
        }

        public void ResetForRestart()
        {
            RestoreManor();
            manorEnemies.Clear();
        }

        public void ReturnHome()
        {
            RestoreManor();
            arena.Player.ResetAlchemyBattle();
            foreach (var entry in manorEnemies)
                if (entry.enemy != null) entry.enemy.gameObject.SetActive(entry.active);
            manorEnemies.Clear();
            arena.Player.Teleport(ManorEnvironment.SpawnPoint);
            Physics2D.SyncTransforms();
        }

        void RestoreManor()
        {
            if (HasModal) CloseModal();
            UnloadMap();
            foreach (var entry in manorEnemies)
                if (entry.enemy != null) arena.RegisterEnemy(entry.enemy);
            Current = TravelDestination.Manor;
            arena.Player.SetRealm(arena.Resources.FoundationTribulationCleared
                ? CultivationRealm.FoundationEstablishment : CultivationRealm.QiRefining);
            manor.gameObject.SetActive(true);
            arena.WorldCamera.backgroundColor = manorBackground;
            gateArmed = true;
        }

        void UnloadMap()
        {
            if (ActiveMap != null)
            {
                foreach (var enemy in ActiveMap.GetComponentsInChildren<SlashEnemy>(true))
                    arena.UnregisterEnemy(enemy);
                ActiveMap.gameObject.SetActive(false);
                Destroy(ActiveMap.gameObject);
            }
            ActiveMap = null;
            Portal = null;
            tribulation = null;
            tribulationMarker = null;
            TribulationCleared = false;
            treatmentMarker = null;
            pillStallMarker = treasureStallMarker = null;
            ascension = null;
        }
    }
}
