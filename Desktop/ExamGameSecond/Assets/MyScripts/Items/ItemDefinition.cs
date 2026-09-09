using System;

// Describes one type of item (Wood, Helmet, etc). Plain data class rather than
// a ScriptableObject asset, so the whole item list lives in ItemCatalog.cs and
// nothing needs to be created by hand in the Editor.
[Serializable]
public class ItemDefinition
{
    public string ItemName;
    public ItemCategory Category;
    public ResourceSubCategory ResourceSubCategory;
    public ToolSubCategory ToolSubCategory;

    // Only set for Armor items - which of the 4 boost slots it belongs in.
    public ArmorSlotType ArmorSlot;

    // Armor never stacks (1 item per slot). Resources/tools stack up to 99 per slot.
    public int MaxStackSize;

    // Capital-letter placeholder shown IN A SLOT ICON until real icon art exists - never
    // shown in body text (see DisplayName for that).
    public string PlaceholderLetter;

    // Full human-readable name for body text (descriptions, trade requirements, etc.) -
    // separate from ItemName because a few items (e.g. "SW") use a short internal ItemName
    // that doubles as their PlaceholderLetter, which reads fine on an icon but not in a
    // sentence. Falls back to ItemName for every item that doesn't need a friendlier one.
    public string DisplayName;

    public ItemDefinition(string itemName, ItemCategory category, int maxStackSize, string placeholderLetter,
        ResourceSubCategory resourceSubCategory = ResourceSubCategory.None,
        ToolSubCategory toolSubCategory = ToolSubCategory.None,
        ArmorSlotType armorSlot = ArmorSlotType.None,
        string displayName = null)
    {
        ItemName = itemName;
        Category = category;
        MaxStackSize = maxStackSize;
        PlaceholderLetter = placeholderLetter;
        ResourceSubCategory = resourceSubCategory;
        ToolSubCategory = toolSubCategory;
        ArmorSlot = armorSlot;
        DisplayName = string.IsNullOrEmpty(displayName) ? itemName : displayName;
    }
}
