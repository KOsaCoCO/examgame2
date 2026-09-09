using System.Collections.Generic;
using UnityEngine;

namespace NTGD124
{
    // Attached to Objects/PlantLocations/regularPlantSpawner. Spawns its full batch once at
    // scene Start and never clears it on a cycle boundary (unlike Night/Day) - individual plants
    // only get replaced once two full day/night cycles have finished since they were harvested
    // (DayNightTimeCycle.CycleCount needs to advance by 2), still capped at maxAlivePerPrefab per
    // prefab. Re-checks pending cooldowns on every day/night transition rather than polling every
    // frame - cheap, and cooldowns can only mature at a cycle boundary anyway.
    public class RegularPlantSpawner : PlantSpawnerBase
    {
        private class PendingRespawn
        {
            public GameObject Prefab;
            public int EligibleAtCycle;
        }

        private DayNightTimeCycle _dayNightCycle;
        private readonly List<PendingRespawn> _pendingRespawns = new();

        protected override void Start()
        {
            base.Start();
            _dayNightCycle = FindAnyObjectByType<DayNightTimeCycle>();
            SpawnFullBatch();

            GameEvents.OnDayBegan += HandleCycleTick;
            GameEvents.OnNightBegan += HandleCycleTick;
        }

        private void OnDestroy()
        {
            GameEvents.OnDayBegan -= HandleCycleTick;
            GameEvents.OnNightBegan -= HandleCycleTick;
        }

        // Queues a cooldown instead of spawning a replacement immediately - "spawn again after
        // two daynight cycles have been fully finished," not right away.
        protected override void HandlePlantHarvested(HarvestablePlant plant)
        {
            if (_dayNightCycle == null || plant.SourcePrefab == null) return;

            _pendingRespawns.Add(new PendingRespawn
            {
                Prefab = plant.SourcePrefab,
                EligibleAtCycle = _dayNightCycle.CycleCount + 2
            });
        }

        private void HandleCycleTick()
        {
            if (_dayNightCycle == null) return;

            for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
            {
                PendingRespawn pending = _pendingRespawns[i];
                if (_dayNightCycle.CycleCount < pending.EligibleAtCycle) continue;

                TopUpPrefab(pending.Prefab); // still respects maxAlivePerPrefab, won't overshoot
                _pendingRespawns.RemoveAt(i);
            }
        }
    }
}

// Implementation Steps:
// 1. Already attached to Objects/PlantLocations/regularPlantSpawner in the scene (it has its own
//    BoxCollider marking the spawn footprint).
// 2. Drag Mushroom Cluster 2 and Flower 3 into Prefabs in the Inspector - each carries a
//    PlantItemNameOverride granting "Regular Mushroom"/"Regular Flower" respectively, distinct
//    from their own prefab names.
// 3. Requires a DayNightTimeCycle already in the scene (used to read CycleCount for the 2-cycle
//    respawn cooldown) - nothing else to wire.
