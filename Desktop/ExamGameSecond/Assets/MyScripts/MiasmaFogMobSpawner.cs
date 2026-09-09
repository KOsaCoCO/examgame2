using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NTGD124
{
    // Spawns Hard mob prefabs directly inside the miasma fog's own trigger area (the BoxCollider
    // MiasmaFogArea.Awake adds to this same GameObject, "Redish Fog") - a separate pool from
    // MobSpawner's own outer Hard ring, with its own limit (FogHardMobLimit) rather than sharing
    // HardMobLimit. Reuses MobSpawner.HardMobPrefabs directly (same mob roster, no duplicate list
    // to keep in sync) but tracks/tops up its own alive-count independently, topping back up on
    // every GameEvents.OnNightBegan/OnDayBegan tick rather than only once at Start - and, unlike
    // any day/night despawn behaviour elsewhere, never despawns anything itself, so these mobs
    // stay in the fog regardless of time of day. Lives directly on "Redish Fog" (added the same
    // way MiasmaFogArea already is - see SampleScene.unity's PrefabInstance m_AddedComponents) so
    // it needs no separate "is the fog still active" check: MiasmaTree.HandleHit deactivating
    // tree_1 Big once felled takes this whole GameObject (and so this component) down with it -
    // OnDisable unsubscribes from the day/night events automatically, which is exactly "stop
    // spawning once the miasma is gone." Mobs already alive when that happens are left alone.
    public class MiasmaFogMobSpawner : MonoBehaviour
    {
        [Tooltip("Reuses this MobSpawner's own HardMobPrefabs roster - leave unassigned to auto-find the scene's MobSpawner.")]
        public MobSpawner mobSpawner;

        [Tooltip("How many hard mobs stay alive in the fog area at once - topped back up (not exceeded) on every day/night transition, independent of MobSpawner's own HardMobLimit for its outer ring.")]
        public int FogHardMobLimit = 6;

        [Tooltip("Tag on the Terrain GameObject, used to sample ground height at each spawn point.")]
        public string TerrainTag = "GameTerrain";

        [Tooltip("How far to search for a valid NavMesh point around a candidate spawn position inside the fog.")]
        public float SpawnSampleRadius = 5f;
        [Tooltip("How many random points inside the fog area to try before giving up on a spawn slot for this tick.")]
        public int SpawnPlacementAttempts = 8;

        private BoxCollider _fogCollider;
        private Terrain _targetTerrain;
        private readonly List<GameObject> _fogMobsAlive = new List<GameObject>();

        private void Start()
        {
            if (mobSpawner == null) mobSpawner = FindAnyObjectByType<MobSpawner>();

            // MiasmaFogArea.Awake() adds this GameObject's BoxCollider at runtime (it's not a
            // serialized component in the scene) - safe to read here since every component's
            // Awake() across the scene runs before any component's Start(), regardless of
            // sibling order on this GameObject.
            _fogCollider = GetComponent<BoxCollider>();
            if (_fogCollider == null)
            {
                Debug.LogWarning($"[MiasmaFogMobSpawner] No BoxCollider found on '{gameObject.name}' - expected MiasmaFogArea.Awake() to have already added one. No fog mobs will spawn.");
            }

            LocateTerrain();
            TopUpFogMobs();
        }

        private void OnEnable()
        {
            GameEvents.OnNightBegan += TopUpFogMobs;
            GameEvents.OnDayBegan += TopUpFogMobs;
        }

        private void OnDisable()
        {
            GameEvents.OnNightBegan -= TopUpFogMobs;
            GameEvents.OnDayBegan -= TopUpFogMobs;
        }

        private void LocateTerrain()
        {
            GameObject terrainObject = GameObject.FindGameObjectWithTag(TerrainTag);
            if (terrainObject == null)
            {
                Debug.LogWarning($"[MiasmaFogMobSpawner] could not find a terrain tagged '{TerrainTag}'.");
                return;
            }

            _targetTerrain = terrainObject.GetComponent<Terrain>();
        }

        private void TopUpFogMobs()
        {
            if (_fogCollider == null || _targetTerrain == null) return;
            if (mobSpawner == null || mobSpawner.HardMobPrefabs == null || mobSpawner.HardMobPrefabs.Length == 0) return;

            _fogMobsAlive.RemoveAll(mob => mob == null);
            int slotsAvailable = FogHardMobLimit - _fogMobsAlive.Count;

            for (int i = 0; i < slotsAvailable; i++)
            {
                GameObject prefab = mobSpawner.HardMobPrefabs[Random.Range(0, mobSpawner.HardMobPrefabs.Length)];
                if (prefab == null) continue;

                if (!TryFindNavMeshSpawnPointInFog(out Vector3 navMeshPosition))
                {
                    Debug.LogWarning("[MiasmaFogMobSpawner] Could not find a valid NavMesh point inside the fog area after several attempts. Skipping this spawn.");
                    continue;
                }

                GameObject mobClone = Instantiate(prefab, navMeshPosition, Quaternion.identity);

                // Warp syncs the agent's internal state to the sampled point instead of just moving the transform,
                // so the mob never sits mid-embedded in geometry for even one frame - same as MobSpawner.
                NavMeshAgent agent = mobClone.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.Warp(navMeshPosition);
                    if (!agent.isOnNavMesh)
                    {
                        Debug.LogWarning($"[MiasmaFogMobSpawner]: {mobClone.name} warped but is not on the NavMesh. Destroying instead of leaving it stuck.");
                        Destroy(mobClone);
                        continue;
                    }
                }

                _fogMobsAlive.Add(mobClone);
            }

            Debug.Log($"[MiasmaFogMobSpawner] fog hard mobs: {_fogMobsAlive.Count}/{FogHardMobLimit} alive.");
        }

        private bool TryFindNavMeshSpawnPointInFog(out Vector3 navMeshPosition)
        {
            for (int attempt = 0; attempt < SpawnPlacementAttempts; attempt++)
            {
                Vector3 candidate = GetRandomPointInFog();
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, SpawnSampleRadius, NavMesh.AllAreas))
                {
                    navMeshPosition = hit.position;
                    return true;
                }
            }

            navMeshPosition = Vector3.zero;
            return false;
        }

        // Random XZ point within the fog's own trigger BoxCollider bounds, with Y sampled from
        // the real terrain height at that point (matching MobSpawner.GetRandomPointInRing) rather
        // than the box's own (much taller) vertical extent.
        private Vector3 GetRandomPointInFog()
        {
            Bounds bounds = _fogCollider.bounds;
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);
            float y = _targetTerrain.SampleHeight(new Vector3(x, 0f, z)) + _targetTerrain.transform.position.y;
            return new Vector3(x, y, z);
        }
    }
}

// Implementation Steps:
// 1. Add this component directly to "Redish Fog" (Environment/tree_1 Big/Redish Fog), alongside
//    MiasmaFogArea - it reads that same GameObject's BoxCollider (added by MiasmaFogArea.Awake)
//    to know where inside the fog it's allowed to spawn.
// 2. Assign mobSpawner in the Inspector (or leave it unassigned to auto-find the scene's
//    MobSpawner) - HardMobPrefabs is read directly from it, no separate roster to maintain here.
// 3. Adjust FogHardMobLimit (default 6) if the fog's own mob count needs tuning independently of
//    MobSpawner's outer HardMobLimit.
