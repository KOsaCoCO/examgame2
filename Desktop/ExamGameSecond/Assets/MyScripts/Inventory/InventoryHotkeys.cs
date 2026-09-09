using UnityEngine.InputSystem;

// Single place for every inventory-related hotkey/modifier check, so the slot
// click handlers (InventorySlotUI, HandSlotUI, BoostSlotUI) don't each read
// the keyboard themselves. Add new inventory hotkeys here, not in the UI scripts.
// Uses the new Input System (this project's Active Input Handling is New Input
// System only - legacy Input.GetKey calls would silently do nothing).
public static class InventoryHotkeys
{
    // Held while double-clicking a stacked slot to move the whole stack at
    // once, instead of the default single unit.
    public static bool MoveWholeStackHeld()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;

        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }

    // Number keys 1-4 arm the matching hand slot (0-3). Returns -1 if none was
    // pressed this frame.
    public static int GetPressedHandSlotIndex()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return -1;

        if (keyboard.digit1Key.wasPressedThisFrame) return 0;
        if (keyboard.digit2Key.wasPressedThisFrame) return 1;
        if (keyboard.digit3Key.wasPressedThisFrame) return 2;
        if (keyboard.digit4Key.wasPressedThisFrame) return 3;
        return -1;
    }
}
