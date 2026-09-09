namespace NTGD124
{
    // Attached to Objects/PlantLocations/DayPlantsSpawner. Spawns immediately at scene Start
    // (the game always begins in daytime - see DayNightTimeCycle.Start()), tops back up to a
    // fresh batch on every subsequent day (GameEvents.OnDayBegan), and clears everything the
    // instant night begins - the mirror image of NightPlantSpawner.
    public class DayPlantSpawner : PlantSpawnerBase
    {
        protected override void Start()
        {
            base.Start();
            SpawnFullBatch(); // the scene always starts in daytime, so there's no OnDayBegan for "day 1"
            GameEvents.OnDayBegan += HandleDayBegan;
            GameEvents.OnNightBegan += HandleNightBegan;
        }

        private void OnDestroy()
        {
            GameEvents.OnDayBegan -= HandleDayBegan;
            GameEvents.OnNightBegan -= HandleNightBegan;
        }

        private void HandleDayBegan() => SpawnFullBatch();
        private void HandleNightBegan() => ClearAll();
    }
}

// Implementation Steps:
// 1. Already attached to Objects/PlantLocations/DayPlantsSpawner in the scene (it has its own
//    BoxCollider marking the spawn footprint).
// 2. Drag Mushroom Cluster 1 and Flower 1 into Prefabs in the Inspector - each carries a
//    PlantItemNameOverride granting "Day Mushroom"/"Day Flower" respectively, distinct from
//    their own prefab names.
// 3. Nothing else to wire - GameEvents.OnDayBegan/OnNightBegan (DayNightTimeCycle) drive this
//    automatically after the first (immediate) spawn.
