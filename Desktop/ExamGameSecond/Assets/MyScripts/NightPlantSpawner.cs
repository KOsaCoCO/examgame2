namespace NTGD124
{
    // Attached to Objects/PlantLocations/NightPlantsSpawner. Populates its footprint with a
    // fresh batch (up to maxAlivePerPrefab per prefab) the instant night begins, and clears
    // every remaining instance the instant day begins - no mid-window top-up, matching this
    // session's spec ("by the end of night, the prefabs get removed"). Already-collected items
    // sitting in the player's inventory are unaffected either way.
    public class NightPlantSpawner : PlantSpawnerBase
    {
        protected override void Start()
        {
            base.Start();
            GameEvents.OnNightBegan += HandleNightBegan;
            GameEvents.OnDayBegan += HandleDayBegan;
        }

        private void OnDestroy()
        {
            GameEvents.OnNightBegan -= HandleNightBegan;
            GameEvents.OnDayBegan -= HandleDayBegan;
        }

        private void HandleNightBegan() => SpawnFullBatch();
        private void HandleDayBegan() => ClearAll();
    }
}

// Implementation Steps:
// 1. Already attached to Objects/PlantLocations/NightPlantsSpawner in the scene (it has its own
//    BoxCollider marking the spawn footprint).
// 2. Drag Mushroom Cluster 3 and Flower 4 into Prefabs in the Inspector - each carries a
//    PlantItemNameOverride granting "Night Mushroom"/"Night Flower" respectively, distinct from
//    their own prefab names.
// 3. Nothing else to wire - GameEvents.OnNightBegan/OnDayBegan (DayNightTimeCycle) drive this
//    automatically.
