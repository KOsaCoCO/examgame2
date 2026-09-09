using System.Collections.Generic;
using UnityEngine;

// Checks/spends an item across several IResourceContainers (inventory, hand,
// and later others) as one combined pool, in a fixed priority order - e.g.
// "check inventory first, then whatever's in hand". Used anywhere a cost
// needs to draw from "wherever the player is holding it" rather than one
// specific container.
public static class ResourcePool
{
    public static bool HasItems(IEnumerable<IResourceContainer> sources, string itemName, int quantity)
    {
        int total = 0;
        foreach (IResourceContainer source in sources)
        {
            if (source == null) continue;

            total += source.CountItems(itemName);
            if (total >= quantity) return true;
        }
        return false;
    }

    // Spends 'quantity' of itemName, draining sources in the given order.
    // Returns false (and spends nothing) if the combined total isn't enough.
    public static bool TryRemoveItems(IEnumerable<IResourceContainer> sources, string itemName, int quantity)
    {
        if (!HasItems(sources, itemName, quantity)) return false;

        int remaining = quantity;
        foreach (IResourceContainer source in sources)
        {
            if (source == null || remaining <= 0) continue;

            int take = Mathf.Min(source.CountItems(itemName), remaining);
            if (take <= 0) continue;

            source.TryRemoveItems(itemName, take);
            remaining -= take;
        }
        return true;
    }
}
