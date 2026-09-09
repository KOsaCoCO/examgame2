using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Real icon art for ItemCatalog entries, replacing the capital-letter placeholder shown in
// every slot UI (Inventory/Hand/Boost/Trade) once an item has one assigned here. A single list
// living on its own component (not ItemCatalog itself, which is plain static C# data and can't
// hold Inspector-dragged Sprite/GameObject references) rather than duplicated per UI panel -
// every slot UI queries GetIcon(itemName) through ItemIconRenderer instead of keeping its own
// copy. Falls back to the existing placeholder-letter behaviour automatically for any item that
// doesn't have an icon entry yet, so this can be filled in gradually.
public class ItemIconCatalog : MonoBehaviour
{
    [System.Serializable]
    public class IconEntry
    {
        [Tooltip("Optional - drag the item's own world prefab here (e.g. the plant/resource prefab) just to make it easy to see which item this row is while you're picking its icon. Its name is also used as ItemName automatically if you leave that field blank.")]
        public GameObject ItemPrefab;
        [Tooltip("Must match an ItemCatalog entry's ItemName exactly. Leave blank to use ItemPrefab's own name instead (matches the existing plant-prefab naming convention).")]
        public string ItemName;
        [Tooltip("Leave blank for this item's normal/default icon. Set to a stack's IconVariant tag (e.g. \"Easy\"/\"Medium\"/\"Hard\" - see MobHealth.RollDrops) to give that specific variant of the item its own icon instead - e.g. Bone dropped by a Hard mob showing differently than one from an Easy mob.")]
        public string Variant;
        [Tooltip("The icon shown in every slot UI for this item (and variant, if set) once set - e.g. from Assets/FreeAssets/Tiny Fantasy Icons/.")]
        public Sprite Icon;
        [Tooltip("Optional color wash drawn over the icon (e.g. purple for Night plants, yellow for Day) - set its Alpha for opacity (0.3 = 30%); leave Alpha at 0 (the default) for no tint at all. Lets several items share one piece of icon art while still reading as visually distinct.")]
        public Color Tint = Color.clear;

        public string ResolvedItemName => string.IsNullOrEmpty(ItemName) && ItemPrefab != null ? ItemPrefab.name : ItemName;
    }

    [Tooltip("One entry per item (optionally per-variant) that has real icon art. Items left out here keep showing their ItemCatalog.PlaceholderLetter instead.")]
    public List<IconEntry> Icons = new();

    private static ItemIconCatalog _instance;

    void Awake()
    {
        _instance = this;
    }

    // Finds the scene's catalog lazily (rather than requiring it to Awake before every slot
    // manager does) - same FindAnyObjectByType(Include) pattern already used everywhere else
    // in this project (InventoryManager, PlayerHandManager, etc.) to sidestep Awake-order
    // dependencies between unrelated GameObjects. Prefers an exact (itemName, variant) match
    // (e.g. Bone+"Hard"); a tagged stack with no dedicated icon for its variant falls back to
    // that item's variant-less entry, if one exists, rather than showing nothing.
    private static IconEntry Resolve(string itemName, string variant)
    {
        if (string.IsNullOrEmpty(itemName)) return null;
        if (_instance == null) _instance = FindAnyObjectByType<ItemIconCatalog>(FindObjectsInactive.Include);
        if (_instance == null) return null;

        IconEntry fallback = null;
        foreach (IconEntry entry in _instance.Icons)
        {
            if (entry.ResolvedItemName != itemName) continue;

            if (entry.Variant == variant) return entry;
            if (string.IsNullOrEmpty(entry.Variant)) fallback = entry;
        }
        return fallback;
    }

    public static Sprite GetIcon(string itemName, string variant = null) => Resolve(itemName, variant)?.Icon;
    public static Color GetTint(string itemName, string variant = null) => Resolve(itemName, variant)?.Tint ?? Color.clear;
}

// Shared by every slot UI type (Inventory/Hand/Boost/Shop) so the icon-vs-placeholder-letter
// fallback logic and quantity formatting only exist in one place.
public static class ItemIconRenderer
{
    public static void Apply(TMP_Text label, Image icon, Image iconTint, ItemDefinition item, int quantity, string variant = null)
    {
        if (item == null)
        {
            if (label != null) label.text = "";
            if (icon != null) icon.enabled = false;
            if (iconTint != null) iconTint.enabled = false;
            return;
        }

        Sprite sprite = ItemIconCatalog.GetIcon(item.ItemName, variant);
        Color tint = ItemIconCatalog.GetTint(item.ItemName, variant);

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        // Reuses the same sprite as the base icon (rather than needing separate tint art per
        // item) - a colored, partially-transparent copy drawn directly on top reads as a color
        // wash over the icon rather than a plain tinted rectangle.
        if (iconTint != null)
        {
            iconTint.sprite = sprite;
            iconTint.color = tint;
            iconTint.enabled = sprite != null && tint.a > 0f;
        }

        if (label != null)
        {
            label.text = sprite != null
                ? (quantity > 1 ? $"x{quantity}" : "")
                : (quantity > 1 ? $"{item.PlaceholderLetter} x{quantity}" : item.PlaceholderLetter);
        }
    }

    // Finds a slot's dedicated "Icon" child Image (distinct from the slot's own root Image,
    // which is the button background/skin), creating one the same "create if missing"
    // convention each slot's own CreateLabel helper already uses for its text child. Forced to
    // sibling index 0 (of this slot's children) every time so it always renders below the tint
    // overlay and the label, regardless of creation order.
    public static Image FindOrCreateIcon(Transform slotTransform)
    {
        Image image = FindOrCreateOverlayImage(slotTransform, "Icon");
        image.transform.SetSiblingIndex(0);
        return image;
    }

    // The color-wash layer drawn directly on top of "Icon" (same rect, same sprite assigned at
    // render time in Apply) - forced to sibling index 1 every time so it renders above the icon
    // but stays below the label/quantity text.
    public static Image FindOrCreateIconTint(Transform slotTransform)
    {
        Image image = FindOrCreateOverlayImage(slotTransform, "IconTint");
        image.transform.SetSiblingIndex(1);
        return image;
    }

    private static Image FindOrCreateOverlayImage(Transform slotTransform, string childName)
    {
        Transform existing = slotTransform.Find(childName);
        if (existing != null) return existing.GetComponent<Image>();

        GameObject imageObject = new(childName, typeof(RectTransform));
        imageObject.transform.SetParent(slotTransform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.enabled = false;
        return image;
    }
}

// Implementation Steps:
// 1. Add this component next to the wizard's TradeManager (same GameObject) - the only place
//    an item list like this currently lives - though it's used by every slot UI in the game,
//    not just Trade.
// 2. In the Inspector, add one Icons entry per item you have real art for: drag the item's own
//    prefab into ItemPrefab (optional, just for easy identification) or type its exact ItemName,
//    and drag a Sprite (e.g. from Assets/FreeAssets/Tiny Fantasy Icons/) into Icon.
// 3. Nothing else to wire - every slot UI (Inventory/Hand/Boost/Trade) already renders through
//    ItemIconRenderer.Apply, so any item you add an icon for here shows it everywhere
//    immediately; anything left out keeps showing its placeholder letter exactly as before.
// 4. Bone drops from mob kills are tagged by tier (MobHealth.RollDrops passes the killed mob's
//    Mobai.DifficultyLevel as the variant string - "Easy"/"Medium"/"Hard") - a Bone from an Easy
//    mob will never stack with one from a Medium/Hard mob, so each tier's stack always shows one
//    unambiguous icon. Set ItemName "Bone" + Variant "Easy"/"Medium"/"Hard" (3 separate rows) to
//    give each tier its own icon - Hard mobs and the night boss (also Hard-tier) share the same
//    "Hard" row.
