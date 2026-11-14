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
        public bool IsWardrobeOpen { get; private set; }
        public CharacterPanelPage ActivePage { get; private set; }
        public bool ConsumedInputThisFrame => consumedFrame == Time.frameCount;
        public bool ShowWardrobePrompt => CanOpen && !arena.InputBlocked && NearWardrobe;
        public void Initialize(SlashArena owner) { arena = owner; }

        bool CanOpen => !IsOpen && arena != null && arena.Player != null
            && arena.Player.IsAlive && !arena.Paused && !arena.Defeated
            && (arena.Tuning == null || !arena.Tuning.IsOpen)
            && (arena.Codex == null || !arena.Codex.ConsumedInputThisFrame);

        bool NearWardrobe
        {
            get
            {
                if (arena.Travel == null || arena.Travel.Current != TravelDestination.Manor) return false;
                Rect wardrobe = ManorCollisionLayout.FurnitureFootprint("Wardrobe");
                Vector2 position = arena.Player.transform.position;
                var front = new Vector2(Mathf.Clamp(position.x, wardrobe.xMin, wardrobe.xMax), wardrobe.yMin);
                return position.y <= front.y && Vector2.Distance(position, front) <= 1.35f;
            }
        }

        void Update()
        {
            if (arena == null) return;
            if (IsOpen)
            {
                if (DemoInput.PausePressed || (IsWardrobeOpen ? DemoInput.InteractPressed : DemoInput.CharacterPanelPressed))
                    Close();
            }
            else if (DemoInput.InteractPressed && ShowWardrobePrompt) OpenPanel(true);
            else if (DemoInput.CharacterPanelPressed) Open();
        }

        public bool Open()
        {
            if (!CanOpen) return false;
            ActivePage = CharacterPanelPage.Techniques;
            OpenPanel(false);
            return true;
        }

        void OpenPanel(bool wardrobe)
        {
            IsWardrobeOpen = wardrobe;
            IsOpen = true;
            consumedFrame = Time.frameCount;
            arena.TogglePause();
        }

        public void ConfirmCharacter(PlayableCharacter character)
        {
            if (!IsWardrobeOpen || !NearWardrobe || character != PlayableCharacter.Swordsman) return;
            arena.Player.Character = character;
            Close();
        }

        public void SelectPage(CharacterPanelPage page)
        {
            if (IsOpen && !IsWardrobeOpen && page >= CharacterPanelPage.Techniques && page <= CharacterPanelPage.Items) ActivePage = page;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsWardrobeOpen = false;
            IsOpen = false;
            consumedFrame = Time.frameCount;
            if (arena.Paused) arena.TogglePause();
        }
    }
}
