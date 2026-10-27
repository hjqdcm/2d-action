using UnityEngine;

namespace SlashDemo
{
    public enum CharacterPanelPage { Techniques, Attributes, Items }

    [DefaultExecutionOrder(-95)]
    [DisallowMultipleComponent]
    public sealed class CharacterPanel : MonoBehaviour
    {
        SlashArena arena;
        int consumedFrame = -1;
        public bool IsOpen { get; private set; }
        public CharacterPanelPage ActivePage { get; private set; }
        public bool ConsumedInputThisFrame => consumedFrame == Time.frameCount;
        public void Initialize(SlashArena owner) { arena = owner; }

        void Update()
        {
            if (arena == null) return;
            if (IsOpen)
            {
                if (DemoInput.CharacterPanelPressed || DemoInput.PausePressed) Close();
            }
            else if (DemoInput.CharacterPanelPressed) Open();
        }

        public bool Open()
        {
            if (IsOpen || arena == null || arena.Player == null || !arena.Player.IsAlive || arena.Paused || arena.Defeated
                || (arena.Tuning != null && arena.Tuning.IsOpen)
                || (arena.Codex != null && arena.Codex.ConsumedInputThisFrame)) return false;
            ActivePage = CharacterPanelPage.Techniques;
            IsOpen = true;
            consumedFrame = Time.frameCount;
            arena.TogglePause();
            return true;
        }

        public void SelectPage(CharacterPanelPage page)
        {
            if (IsOpen && page >= CharacterPanelPage.Techniques && page <= CharacterPanelPage.Items) ActivePage = page;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            consumedFrame = Time.frameCount;
            if (arena.Paused) arena.TogglePause();
        }
    }
}
