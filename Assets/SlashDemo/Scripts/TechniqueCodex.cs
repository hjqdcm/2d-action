using UnityEngine;

namespace SlashDemo
{
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class TechniqueCodex : MonoBehaviour
    {
        SlashArena arena;
        int consumedFrame = -1;
        public bool IsOpen { get; private set; }
        public bool IsAlchemyOpen { get; private set; }
        public bool ConsumedInputThisFrame => consumedFrame == Time.frameCount;
        public bool NearTable => NearFurniture("Tea table");

        bool NearFurniture(string furniture)
        {
            if (arena == null || arena.Player == null || !arena.Player.IsAlive
                || arena.Travel == null || arena.Travel.Current != TravelDestination.Manor) return false;
            Rect bounds = ManorCollisionLayout.FurnitureFootprint(furniture);
            Vector2 p = arena.Player.transform.position;
            Vector2 nearest = new Vector2(Mathf.Clamp(p.x, bounds.xMin, bounds.xMax), Mathf.Clamp(p.y, bounds.yMin, bounds.yMax));
            return Vector2.Distance(p, nearest) <= 1.35f;
        }
        public bool ShowPrompt => NearTable && !arena.Paused && !arena.Defeated
            && (arena.Tuning == null || !arena.Tuning.IsOpen);
        public bool ShowAlchemyPrompt => NearFurniture("Alchemy furnace") && !arena.InputBlocked
            && (arena.Tuning == null || !arena.Tuning.IsOpen);
        public void Initialize(SlashArena owner) { arena = owner; }

        void Update()
        {
            if (arena == null) return;
            if (arena.CharacterPanel != null && arena.CharacterPanel.ConsumedInputThisFrame) return;
            if (IsOpen)
            {
                if (DemoInput.InteractPressed || DemoInput.PausePressed) Close();
            }
            else if (DemoInput.InteractPressed)
            {
                if (ShowAlchemyPrompt) OpenAtFurniture(true);
                else Open();
            }
        }

        public bool Open()
        {
            if (IsOpen || !ShowPrompt) return false;
            OpenAtFurniture(false);
            return true;
        }

        void OpenAtFurniture(bool alchemy)
        {
            IsAlchemyOpen = alchemy;
            IsOpen = true;
            consumedFrame = Time.frameCount;
            arena.TogglePause();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsAlchemyOpen = false;
            IsOpen = false;
            consumedFrame = Time.frameCount;
            if (arena.Paused) arena.TogglePause();
        }

        public bool Equip(TechniqueDefinition technique)
        {
            if (!IsOpen || IsAlchemyOpen || !NearTable) return false;
            return arena.Player.EquipTechnique(technique, true);
        }
    }
}
