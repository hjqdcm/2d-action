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
        public bool ConsumedInputThisFrame => consumedFrame == Time.frameCount;
        public bool NearTable
        {
            get
            {
                if (arena == null || arena.Player == null || !arena.Player.IsAlive
                    || arena.Travel == null || arena.Travel.Current != TravelDestination.Manor) return false;
                Rect table = ManorCollisionLayout.FurnitureFootprint("Tea table");
                Vector2 p = arena.Player.transform.position;
                Vector2 nearest = new Vector2(Mathf.Clamp(p.x, table.xMin, table.xMax), Mathf.Clamp(p.y, table.yMin, table.yMax));
                return Vector2.Distance(p, nearest) <= 1.35f;
            }
        }
        public bool ShowPrompt => NearTable && !arena.Paused && !arena.Defeated
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
            else if (ShowPrompt && DemoInput.InteractPressed) Open();
        }

        public bool Open()
        {
            if (IsOpen || !ShowPrompt) return false;
            IsOpen = true;
            consumedFrame = Time.frameCount;
            arena.TogglePause();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            consumedFrame = Time.frameCount;
            if (arena.Paused) arena.TogglePause();
        }

        public bool Equip(TechniqueDefinition technique)
        {
            if (!IsOpen || !NearTable || arena.Player == null) return false;
            return arena.Player.EquipTechnique(technique, true);
        }
    }
}
