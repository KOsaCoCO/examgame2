using System;
using System.Collections.Generic;
using UnityEngine;

// The single place that decides where an item currently lives: an inventory
// slot, the player's hand, or one of the 4 armor boost slots. Every "move an
// item" action goes through here, and the source is only cleared once the
// destination actually accepts the item - so an item can never end up
// duplicated or stuck between two places.
public class ItemPlacementTracker : MonoBehaviour
{
    // Item consumed on double-click instead of being routed anywhere - see
    // HandleInventoryDoubleClick's special case, checked before categoryRoutes.
    private const string FoodItemName = "Food";
    [Tooltip("Health points restored per Food eaten (double-clicked in inventory) - capped at PlayerHealth.maxHealth, see PlayerHealth.Heal.")]
    public int FoodHealAmount = 3;

    public InventoryManager Inventory;
    public PlayerHandManager Hand;
    public BoostSlotManager Boosts;

    private PlayerHealth playerHealth;

    // Where each item category goes on double-click, and how much of the
    // (item, amount) it accepts. Kept explicit and per-category (rather than
    // one shared "else" branch) so a missing destination manager fails safely
    // instead of silently falling through to the wrong place - e.g. Armor with
    // no BoostSlotManager should refuse the move, not land in the hand.
    private Dictionary<ItemCategory, Func<ItemDefinition, int, string, bool>> categoryRoutes;

    void Awake()
    {
        categoryRoutes = new Dictionary<ItemCategory, Func<ItemDefinition, int, string, bool>>
        {
            { ItemCategory.Armor, (item, amount, variant) => Boosts != null && Boosts.TryPlace(item.ArmorSlot, item) },
            { ItemCategory.Resource, PlaceInHand },
            { ItemCategory.Tool, PlaceInHand },
        };
    }

    private bool PlaceInHand(ItemDefinition item, int amount, string variant)
    {
        if (Hand == null || !Hand.TryPlaceItem(item, amount, variant)) return false;

        ResourceEventLog.LogToHand(item.ItemName, amount);
        return true;
    }

    // Double-clicking a filled inventory slot routes it by category (see categoryRoutes above).
    // Holding the "move whole stack" hotkey moves the slot's full quantity in one go instead of
    // a single unit.
    public void HandleInventoryDoubleClick(int slotIndex, bool moveWholeStack)
    {
        if (Inventory == null) return;

        InventorySlotData data = Inventory.GetSlot(slotIndex);
        if (data.IsEmpty) return;

        ItemDefinition item = data.Item;
        int moveAmount = moveWholeStack ? data.Quantity : 1;

        if (item.ItemName == FoodItemName)
        {
            EatFood(slotIndex, moveAmount);
            return;
        }

        if (!categoryRoutes.TryGetValue(item.Category, out Func<ItemDefinition, int, string, bool> route)) return;

        if (route(item, moveAmount, data.IconVariant)) Inventory.RemoveFromSlot(slotIndex, moveAmount);
    }

    // Called by InventoryManager when R is pressed while hovering a filled slot (see
    // InventorySlotUI's IPointerEnter/Exit) - eats one unit if it's Food, no-ops for
    // everything else (Food is the only consumable item right now).
    public void HandleInventoryConsumeHotkey(int slotIndex)
    {
        if (Inventory == null) return;

        InventorySlotData data = Inventory.GetSlot(slotIndex);
        if (data.IsEmpty || data.Item.ItemName != FoodItemName) return;

        EatFood(slotIndex, 1);
    }

    // Consumes Food directly on double-click instead of routing it anywhere - heals
    // FoodHealAmount per unit eaten (capped at max health by PlayerHealth.Heal itself).
    private void EatFood(int slotIndex, int amount)
    {
        if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (playerHealth == null) return;

        playerHealth.Heal(FoodHealAmount * amount);
        Inventory.RemoveFromSlot(slotIndex, amount);
    }

    // Double-clicking a filled hand slot sends its item back to the inventory. Holding the
    // "move whole stack" hotkey sends the whole stack at once instead of a single unit.
    public void HandleHandDoubleClick(int slotIndex, bool moveWholeStack)
    {
        if (Hand == null || Inventory == null) return;

        InventorySlotData data = Hand.GetSlot(slotIndex);
        if (data.IsEmpty) return;

        int moveAmount = moveWholeStack ? data.Quantity : 1;

        if (Inventory.AddItem(data.Item, moveAmount, data.IconVariant)) Hand.RemoveFromSlot(slotIndex, moveAmount);
    }

    // Double-clicking a filled boost slot sends its item back to the inventory.
    public void HandleBoostDoubleClick(ArmorSlotType slotType)
    {
        if (Boosts == null || Inventory == null) return;

        InventorySlotData data = Boosts.GetSlot(slotType);
        if (data.IsEmpty) return;

        if (Inventory.AddItem(data.Item, 1)) Boosts.Clear(slotType);
    }
}
