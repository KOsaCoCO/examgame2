// Shared category labels every item belongs to. Other scripts (inventory,
// base-slot unlock requirements, drop tables, etc.) can reference these
// instead of comparing raw item name strings.
public enum ItemCategory
{
    Armor,
    Resource,
    Tool
}

public enum ResourceSubCategory
{
    None,
    Edible,
    NonEdible
}

public enum ToolSubCategory
{
    None,
    Building,
    Cutting,
    Mining,
    Weapon,
    MiasmaCutting
}

// Which of the 4 armor boost slots an Armor item belongs to.
public enum ArmorSlotType
{
    None,
    Helmet,
    Chestplate,
    Boots,
    Pants
}
