using System.Collections.Generic;

// Item drop-rate/rarity data. Each entry's DropChance is rolled independently
// (not a shared pool that sums to 100%) - see MobHealth.RollDrops, which walks
// a mob's difficulty-matched table below and rolls every entry separately.
public class DropRateEntry
{
    public string ItemName;
    public float DropChance; // 0-1, e.g. 0.25 = 25%

    public DropRateEntry(string itemName, float dropChance)
    {
        ItemName = itemName;
        DropChance = dropChance;
    }
}

public static class ItemDropRates
{
    // Easy mobs (10 hits to kill): Skin/Bone, 50/50 each.
    public static readonly List<DropRateEntry> EasyMobDrops = new()
    {
        new DropRateEntry("Skin", 0.5f),
        new DropRateEntry("Bone", 0.5f),
    };

    // Medium mobs (50 hits to kill): Bone at 50/50, Meat guaranteed.
    public static readonly List<DropRateEntry> MediumMobDrops = new()
    {
        new DropRateEntry("Bone", 0.5f),
        new DropRateEntry("Meat", 1f),
    };

    // Hard mobs (100 hits to kill): Skin/Bone at 50/50 each, Meat guaranteed, plus a 60/40
    // Crystallized Soul - the only source of that item, so it doubles as proof of having hunted
    // Hard mobs (see RunedRockTradeOffer) and as SW's "rare special item" ingredient. Formerly
    // had a separate "Monster Soul Shard" entry for that special-item role - retired since it
    // overlapped with Crystallized Soul; the two were meant to be the same drop.
    public static readonly List<DropRateEntry> HardMobDrops = new()
    {
        new DropRateEntry("Skin", 0.5f),
        new DropRateEntry("Bone", 0.5f),
        new DropRateEntry("Meat", 1f),
        new DropRateEntry("Crystallized Soul", 0.6f),
    };
}
