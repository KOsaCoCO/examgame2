// Runtime contents of one inventory slot: which item (if any) and how many.
public class InventorySlotData
{
    public ItemDefinition Item;
    public int Quantity;

    // Cosmetic-only tag (e.g. "Easy"/"Medium"/"Hard" - see MobHealth.RollDrops) letting the same
    // item show a different icon depending on where this particular stack came from, without it
    // being a genuinely different item for crafting/cost purposes (ItemName stays the same, so
    // spending/counting via ItemCatalog lookups is completely unaffected). Null for every item
    // that doesn't care about this (the vast majority). Stacks with different variants never
    // merge together (see StackingHelper) so a slot's icon is always unambiguous.
    public string IconVariant;

    public bool IsEmpty => Item == null || Quantity <= 0;
}
