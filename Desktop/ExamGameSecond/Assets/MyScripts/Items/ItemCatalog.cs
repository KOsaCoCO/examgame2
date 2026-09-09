using System.Collections.Generic;
using System.Linq;

// The reference list of every item type in the game. Inventory code and other
// scripts (e.g. BaseSlotExpander's item requirements) look items up here by
// name instead of hardcoding their own copies.
public static class ItemCatalog
{
    public static readonly List<ItemDefinition> All = new()
    {
        new ItemDefinition("Helmet", ItemCategory.Armor, maxStackSize: 1, placeholderLetter: "H",
            armorSlot: ArmorSlotType.Helmet),
        new ItemDefinition("Chestplate", ItemCategory.Armor, maxStackSize: 1, placeholderLetter: "C",
            armorSlot: ArmorSlotType.Chestplate),
        new ItemDefinition("Boots", ItemCategory.Armor, maxStackSize: 1, placeholderLetter: "B",
            armorSlot: ArmorSlotType.Boots),
        new ItemDefinition("Pants", ItemCategory.Armor, maxStackSize: 1, placeholderLetter: "P",
            armorSlot: ArmorSlotType.Pants),

        new ItemDefinition("Wood", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "W",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Rock", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "R",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Food", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "F",
            resourceSubCategory: ResourceSubCategory.Edible),

        new ItemDefinition("Axe", ItemCategory.Tool, maxStackSize: 1, placeholderLetter: "A",
            toolSubCategory: ToolSubCategory.Cutting),
        new ItemDefinition("Pickaxe", ItemCategory.Tool, maxStackSize: 1, placeholderLetter: "K",
            toolSubCategory: ToolSubCategory.Mining),
        new ItemDefinition("Sword", ItemCategory.Tool, maxStackSize: 1, placeholderLetter: "S",
            toolSubCategory: ToolSubCategory.Weapon),

        new ItemDefinition("Dog", ItemCategory.Resource, maxStackSize: 1, placeholderLetter: "D",
            resourceSubCategory: ResourceSubCategory.NonEdible),

        new ItemDefinition("Bone", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "Bo",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Skin", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "Sk",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Meat", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "Mt",
            resourceSubCategory: ResourceSubCategory.Edible),

        // Wizard quest chain: SW is the tool that fells the miasma-source big tree (bought
        // from the wizard for Skin/Bone/Meat/Crystallized Soul, see TradeManager's offers),
        // which drops a Miasma Stick, which trades for the Magic Stick payoff - also at the wizard.
        new ItemDefinition("SW", ItemCategory.Tool, maxStackSize: 1, placeholderLetter: "SW",
            toolSubCategory: ToolSubCategory.MiasmaCutting, displayName: "Special Axe"),
        new ItemDefinition("Miasma Stick", ItemCategory.Resource, maxStackSize: 1, placeholderLetter: "MS",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Magic Stick", ItemCategory.Tool, maxStackSize: 1, placeholderLetter: "MG"),

        // Plant nodes (see HarvestablePlant/PlantSpawnerBase and its Night/Day/Regular
        // subclasses) - one item per category rather than per prefab: each of these is granted
        // by a PlantItemNameOverride on its source prefab (e.g. "Mushroom Cluster 1.prefab"
        // grants "Day Mushroom") rather than the prefab's own asset name, so the
        // Prefabs/ folder can keep its original per-prefab names while players only ever see
        // one mushroom item and one flower item per category. Same stacking/subcategory shape
        // as Bone/Skin/Meat.
        new ItemDefinition("Day Mushroom", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "DMu",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Night Mushroom", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "NMu",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Regular Mushroom", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "RMu",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Day Flower", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "DFl",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Night Flower", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "NFl",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        new ItemDefinition("Regular Flower", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "RFl",
            resourceSubCategory: ResourceSubCategory.NonEdible),

        // Miasma immunity - traded for at the wizard (see MiasmaCloakTradeOffer) using one of
        // each plant category from both the Night and Day spawners. A wearable Chestplate item,
        // not a consumable - immunity (PlayerHealth.IsImmuneToMiasma) lasts only as long as it
        // stays equipped in that boost slot, lost the moment it's unequipped. Formerly a
        // drink-once "Elixir" granting permanent immunity - replaced by this equip-based version.
        new ItemDefinition("Miasma Protection Cloak", ItemCategory.Armor, maxStackSize: 1, placeholderLetter: "Cl",
            armorSlot: ArmorSlotType.Chestplate),

        // Rare Hard-mob-only drop (see ItemDropRates.HardMobDrops) - proof of having hunted
        // Hard mobs, spent on the Runed Rock trade and SW's recipe at the wizard (see
        // RunedRockTradeOffer/TradeManager's offers).
        new ItemDefinition("Crystallized Soul", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "CS",
            resourceSubCategory: ResourceSubCategory.NonEdible),
        // Base slot 8's unlock cost - traded for at the wizard (see RunedRockTradeOffer) using
        // Crystallized Souls plus 2 of each Night plant category.
        new ItemDefinition("Runed Rock", ItemCategory.Resource, maxStackSize: 99, placeholderLetter: "RR",
            resourceSubCategory: ResourceSubCategory.NonEdible),
    };

    public static ItemDefinition GetByName(string itemName)
    {
        return All.FirstOrDefault(item => item.ItemName == itemName);
    }
}
