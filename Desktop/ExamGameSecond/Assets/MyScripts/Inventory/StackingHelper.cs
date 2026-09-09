using System;
using UnityEngine;

// Shared "stack onto matching slots first, then fill empty ones" placement
// logic used by both InventoryManager.AddItem and PlayerHandManager.TryPlaceItem
// - kept in one place after those two drifted out of sync (inventory already
// stacked onto matching items; hand only ever looked for an empty slot, so
// moving items into hand never joined an existing stack there). Pre-checks
// total capacity before touching anything, so a call either fully succeeds or
// leaves every slot untouched - never a partial application that could
// duplicate or lose items if it doesn't all fit.
public static class StackingHelper
{
    // Total quantity of 'item' (tagged with 'variant') that could still fit across 'slots',
    // respecting MaxStackSize - existing matching stacks' remaining room, plus empty slots
    // counted at full stack size. A slot only counts as "matching" if its IconVariant matches
    // too - stacks with different variants never merge, so a slot's icon is never ambiguous.
    public static int CalculateCapacity(InventorySlotData[] slots, ItemDefinition item, string variant = null)
    {
        int capacity = 0;
        foreach (InventorySlotData slot in slots)
        {
            if (slot.IsEmpty) capacity += item.MaxStackSize;
            else if (slot.Item == item && slot.IconVariant == variant) capacity += item.MaxStackSize - slot.Quantity;
        }
        return capacity;
    }

    // Places 'quantity' of 'item' (tagged with 'variant') into 'slots', stacking onto existing
    // matching slots first (up to MaxStackSize) before filling empty ones. Returns false (and
    // changes nothing at all) if the total capacity can't fit the full quantity. onSlotChanged
    // is called with the index of each slot actually touched, so the caller can refresh its own
    // UI for just those slots.
    public static bool TryPlace(InventorySlotData[] slots, ItemDefinition item, int quantity, Action<int> onSlotChanged, string variant = null)
    {
        if (item == null || quantity <= 0) return false;
        if (CalculateCapacity(slots, item, variant) < quantity) return false;

        int remaining = quantity;

        if (item.MaxStackSize > 1)
        {
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].Item != item || slots[i].IconVariant != variant || slots[i].Quantity >= item.MaxStackSize) continue;

                int space = item.MaxStackSize - slots[i].Quantity;
                int add = Mathf.Min(space, remaining);
                slots[i].Quantity += add;
                remaining -= add;
                onSlotChanged(i);
            }
        }

        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;

            int add = item.MaxStackSize > 1 ? Mathf.Min(item.MaxStackSize, remaining) : 1;
            slots[i].Item = item;
            slots[i].Quantity = add;
            slots[i].IconVariant = variant;
            remaining -= add;
            onSlotChanged(i);
        }

        return true;
    }
}
