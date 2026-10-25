using UnityEngine;
using UnityEngine.InputSystem;

namespace SlashDemo
{
    // Uses the project's already enabled Input System; no project input setting changes.
    public static class DemoInput
    {
        public static Vector2 Move
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                return Vector2.ClampMagnitude(new Vector2(
                    ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1 : 0) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1 : 0),
                    ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1 : 0) - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1 : 0)), 1);
            }
        }
        public static bool AttackHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool AttackPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool HeavyHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;
        public static bool HeavyPressed => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static bool HeavyReleased => Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame;
        public static bool DashPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public static bool RestartPressed => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        public static bool PracticePressed => Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
        public static bool PausePressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        public static bool TuningPressed => Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
        public static bool MouseAvailable => Mouse.current != null;
        public static Vector2 Pointer => Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
    }
}
