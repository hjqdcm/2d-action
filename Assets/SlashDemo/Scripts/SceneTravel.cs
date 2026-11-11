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
        WildernessEncounter wildernessEncounter;
        AscensionChallenge ascension;
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
        public bool CanContinueChallenge => IsChallengeDecisionOpen && ascension.HasNext;
        public int ChallengeRound => ascension?.Round ?? 0;
        public int ChallengeCompletedRounds => ascension?.CompletedRounds ?? 0;
        public bool ChallengeEndedByDefeat => ascension != null && Portal != null && ascension.CompletedRounds < ascension.Round;
        public string DefeatedChallenger { get; private set; }
        public bool HasModal => IsSelectionOpen || IsChallengeDecisionOpen;
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
            if (Current != TravelDestination.Manor)
            {
                if (DemoInput.InteractPressed) TryOpenPortal();
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

        public void EnemyDefeated(SlashEnemy defeated)
        {
            if (Current == TravelDestination.Manor || ActiveMap == null || Portal != null
                || defeated.trainingDummy || !defeated.transform.IsChildOf(ActiveMap.transform)) return;
            if (ascension != null)
            {
                if (IsChallengeDecisionOpen || !arena.Player.IsAlive || !ascension.RecordVictory(defeated)) return;
                if (!ascension.HasNext) HealChallengeVictory();
                DefeatedChallenger = defeated.name;
                IsChallengeDecisionOpen = true;
                PauseForModal();
                return;
            }
            foreach (var enemy in arena.Enemies)
                if (enemy != null && enemy.Alive && !enemy.trainingDummy && enemy.gameObject.activeInHierarchy) return;
            var venom=ActiveMap.GetComponent<SerpentVenomField>();
            if(venom!=null) venom.Clear();
            Portal = TeleportFormation.Spawn(ActiveMap, defeated.transform.position);
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
            }
            else
            {
                Portal = TeleportFormation.Spawn(ActiveMap, ascension.Opponent.transform.position);
            }
            CloseModal();
        }

        void HealChallengeVictory()
        {
            var player = arena.Player;
            player.Heal(Mathf.CeilToInt(player.maxHealth * .5f));
        }

        internal bool TrySettleChallengeDefeat(SlashEnemy opponent)
        {
            if (ascension == null || Portal != null || HasModal || opponent == null || ascension.Opponent != opponent || !opponent.Alive)
                return false;

            Portal = TeleportFormation.Spawn(ActiveMap, arena.Player.transform.position);
            ascension.RemoveOpponent(arena);
            arena.Effects.Clear();
            return true;
        }

        public void CancelSelection()
        {
            if (!IsSelectionOpen) return;
            CloseModal();
        }

        public void SelectDestination(TravelDestination destination)
        {
            if (!IsSelectionOpen || destination == TravelDestination.Manor) return;
            TravelMapEnvironment map;
            AscensionChallenge nextChallenge = null;
            try
            {
                if (destination == TravelDestination.AscensionPlatform) nextChallenge = new AscensionChallenge();
                map = TravelMapEnvironment.Build(manor.parent, TravelMaps.Get(destination));
            }
            catch (System.Exception exception)
            {
                error = "场景暂时无法进入，请重试";
                Debug.LogException(exception);
                return;
            }
            // Preserve the player object, health, tuning and camera; only transient motion is cleared.
            arena.Effects.Clear();
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
            if (ascension != null) ascension.SpawnNext(arena, map);
            CloseModal();
            arena.CameraRig.ResetFeedback();
        }

        void CloseModal()
        {
            IsSelectionOpen = false;
            IsChallengeDecisionOpen = false;
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
            arena.Player.ResetRealm();
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
            ascension = null;
        }
    }
}
