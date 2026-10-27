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
        readonly List<(SlashEnemy enemy, bool active)> manorEnemies = new List<(SlashEnemy, bool)>();
        public TravelDestination Current { get; private set; } = TravelDestination.Manor;
        public TravelMapEnvironment ActiveMap { get; private set; }
        public bool IsSelectionOpen { get; private set; }
        public string Error => error;
        public bool BlocksGameplayInput => IsSelectionOpen || waitingForRelease;
        public static Rect ManorGate => ManorEnvironment.GroundRect(new Rect(630, 770, 100, 75));

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
            if (Current != TravelDestination.Manor || IsSelectionOpen) return;
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
            error = null;
            IsSelectionOpen = true;
            arena.Player.CancelHeavyAttack();
            arena.Player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            arena.TogglePause();
        }

        public void CancelSelection()
        {
            if (!IsSelectionOpen) return;
            CloseSelection();
        }

        public void SelectDestination(TravelDestination destination)
        {
            if (!IsSelectionOpen || destination == TravelDestination.Manor) return;
            TravelMapEnvironment map;
            try
            {
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
            manorEnemies.Clear();
            foreach (var enemy in arena.Enemies)
                if (enemy != null)
                {
                    manorEnemies.Add((enemy, enemy.gameObject.activeSelf));
                    enemy.gameObject.SetActive(false);
                }
            Current = destination;
            ActiveMap = map;
            arena.WorldCamera.backgroundColor = map.Definition.MistColor;
            arena.Player.Teleport(map.Definition.Entry);
            Physics2D.SyncTransforms();
            CloseSelection();
            arena.CameraRig.ResetFeedback();
        }

        void CloseSelection()
        {
            IsSelectionOpen = false;
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
            if (IsSelectionOpen) CloseSelection();
            if (ActiveMap != null)
            {
                ActiveMap.gameObject.SetActive(false);
                Destroy(ActiveMap.gameObject);
            }
            ActiveMap = null;
            Current = TravelDestination.Manor;
            arena.Player.ResetRealm();
            manor.gameObject.SetActive(true);
            arena.WorldCamera.backgroundColor = manorBackground;
            gateArmed = true;
        }
    }
}
