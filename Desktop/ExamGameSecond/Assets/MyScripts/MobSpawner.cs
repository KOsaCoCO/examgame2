using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Finds the "Center Orient" hookable object under the BaseSlot-tagged object and spawns easy/medium/hard mob prefabs in rings of increasing distance from it
namespace NTGD124
{
    public class MobSpawner : MonoBehaviour
    {
        ///// Component Description /////

        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Looks up the BaseSlot-tagged object and caches its 'Center Orient' child as the ring center. Spawns mob clones at random positions inside distance rings from that center: no mobs near the middle (safe zone), easy mobs in the next ring, medium mobs further out, and hard mobs in the outermost ring. The terrain is still used to sample ground height at each spawn point. Each ring tracks how many of its mobs are still alive and only spawns enough to top back up to its allowed count.";


        ///// Public Variables /////

        [Header("Terrain Lookup")]
        public string TerrainTag = "GameTerrain"; // tag assigned to the Terrain GameObject in the scene, used to sample ground height

        [Header("Ring Center Lookup")]
        public string BaseSlotTag = "BaseSlot"; // tag placed on the empty parent object holding the base's active children
        public string CenterOrientName = "Center Orient"; // name of the placeholder child that rings are centered on

        [Header("Ring Radius Settings (distance from Center Orient)")]
        public float SafeZoneRadius = 100f; // 0 to this radius: no mobs spawn here
        public float EasyZoneOuterRadius = 300f; // SafeZoneRadius to this radius: easy mobs spawn here
        public float MediumZoneOuterRadius = 700f; // EasyZoneOuterRadius to this radius: medium mobs spawn here
        public float HardZoneOuterRadius = 950f; // MediumZoneOuterRadius to this radius: hard mobs spawn here (stays clear of the map edge)

        [Header("Mob Prefabs")]
        public GameObject[] EasyMobPrefabs; // source assets to clone for the easy ring
        public GameObject[] MediumMobPrefabs; // source assets to clone for the medium ring
        public GameObject[] HardMobPrefabs; // source assets to clone for the hard ring

        [Header("Spawn Limits (max mobs allowed alive at once, per ring)")]
        public int EasyMobLimit = 10;
        public int MediumMobLimit = 10;
        public int HardMobLimit = 10;

        [Header("Timing (spawn delay per ring difficulty)")]
        public float EasyRingDelaySeconds = 0f;
        public float MediumRingDelaySeconds = 0f;
        public float HardRingDelaySeconds = 0f;

        [Header("Spawn Placement Safeguards")]
        public float SpawnSampleRadius = 5f; // how far to search for a valid NavMesh point around a candidate spawn position
        public int SpawnPlacementAttempts = 5; // how many random ring points to try before giving up on a spawn slot (guards against a single bad point landing inside solid geometry)


        ///// Private Variables /////

        private Terrain _targetTerrain; // the terrain found via TerrainTag, used for ground height sampling
        private Transform _centerOrientTarget; // cached ring center, found under the BaseSlot-tagged object
        private List<GameObject> _easyMobsAlive = new List<GameObject>(); // easy ring mobs currently spawned in the scene
        private List<GameObject> _mediumMobsAlive = new List<GameObject>(); // medium ring mobs currently spawned in the scene
        private List<GameObject> _hardMobsAlive = new List<GameObject>(); // hard ring mobs currently spawned in the scene


        ///// Unity Methods /////

        // Time-based trigger: runs once automatically when the scene starts
        private void Start()
        {
            BeginMobSpawning();
        }


        ///// Trigger Methods /////

        // Trigger type: time-based (called from Start)
        private void BeginMobSpawning()
        {
            LocateTerrain();
            LocateCenterOrientTarget();
            if (_targetTerrain == null || _centerOrientTarget == null) return;

            SpawnAllRings();
        }


        ///// Action Methods /////

        // Public action, tops up every ring at once using each ring's own delay
        public void SpawnAllRings()
        {
            SpawnEasyRingMobs();
            SpawnMediumRingMobs();
            SpawnHardRingMobs();
        }

        // Public action, callable from this script's own trigger or from an external editor-based event trigger
        public void SpawnEasyRingMobs()
        {
            StartCoroutine(SpawnRingRoutine(EasyMobPrefabs, _easyMobsAlive, EasyMobLimit, SafeZoneRadius, EasyZoneOuterRadius, EasyRingDelaySeconds));
        }

        // Public action, callable from this script's own trigger or from an external editor-based event trigger
        public void SpawnMediumRingMobs()
        {
            StartCoroutine(SpawnRingRoutine(MediumMobPrefabs, _mediumMobsAlive, MediumMobLimit, EasyZoneOuterRadius, MediumZoneOuterRadius, MediumRingDelaySeconds));
        }

        // Public action, callable from this script's own trigger or from an external editor-based event trigger
        public void SpawnHardRingMobs()
        {
            StartCoroutine(SpawnRingRoutine(HardMobPrefabs, _hardMobsAlive, HardMobLimit, MediumZoneOuterRadius, HardZoneOuterRadius, HardRingDelaySeconds));
        }

        // Public wrapper around the same ring-sampling logic SpawnHardRingMobs() uses internally,
        // for one-off spawns that don't belong to a tracked ring (e.g. NightBossSpawner's
        // one-time boss). Requires LocateTerrain()/LocateCenterOrientTarget() to have already run
        // (both happen in this component's own Start()).
        public bool TryGetSpawnPointInHardRing(out Vector3 spawnPoint)
        {
            if (_targetTerrain == null || _centerOrientTarget == null)
            {
                spawnPoint = Vector3.zero;
                return false;
            }

            return TryFindNavMeshSpawnPoint(MediumZoneOuterRadius, HardZoneOuterRadius, out spawnPoint);
        }

        // Finds the terrain using TerrainTag, kept only for ground height sampling at spawn points
        private void LocateTerrain()
        {
            GameObject terrainObject = GameObject.FindGameObjectWithTag(TerrainTag);
            if (terrainObject == null)
            {
                Debug.LogWarning($"MobSpawner could not find a terrain tagged '{TerrainTag}'.");
                return;
            }

            _targetTerrain = terrainObject.GetComponent<Terrain>();
            if (_targetTerrain == null)
            {
                Debug.LogWarning($"MobSpawner found '{terrainObject.name}' but it has no Terrain component.");
            }
        }

        // Finds the BaseSlot-tagged parent and caches its "Center Orient" child as the ring center
        private void LocateCenterOrientTarget()
        {
            GameObject baseSlotObject = GameObject.FindGameObjectWithTag(BaseSlotTag);
            if (baseSlotObject == null)
            {
                Debug.LogWarning($"MobSpawner could not find an object tagged '{BaseSlotTag}'.");
                return;
            }

            Transform centerOrient = baseSlotObject.transform.Find(CenterOrientName);
            if (centerOrient == null)
            {
                Debug.LogWarning($"MobSpawner could not find a child named '{CenterOrientName}' under the base slot.");
                return;
            }

            _centerOrientTarget = centerOrient;
        }

        // Spawning limiter: clears out destroyed mobs, then only spawns enough new ones to bring this ring back up to its allowed limit
        private void SpawnMobsInRing(GameObject[] mobPrefabs, List<GameObject> aliveMobs, int mobLimit, float minRadius, float maxRadius)
        {
            aliveMobs.RemoveAll(mob => mob == null);

            int slotsAvailable = mobLimit - aliveMobs.Count;
            for (int i = 0; i < slotsAvailable; i++)
            {
                GameObject prefab = GetRandomMobPrefab(mobPrefabs);
                if (prefab == null) continue;

                if (!TryFindNavMeshSpawnPoint(minRadius, maxRadius, out Vector3 navMeshPosition))
                {
                    Debug.LogWarning($"MobSpawner: Could not find a valid NavMesh point in ring {minRadius}-{maxRadius} after {SpawnPlacementAttempts} attempts. Skipping this spawn.");
                    continue;
                }

                GameObject mobClone = Instantiate(prefab, navMeshPosition, Quaternion.identity);

                // Warp syncs the agent's internal state to the sampled point instead of just moving the transform,
                // so the mob never sits mid-embedded in geometry for even one frame
                NavMeshAgent agent = mobClone.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.Warp(navMeshPosition);
                    if (!agent.isOnNavMesh)
                    {
                        Debug.LogWarning($"MobSpawner: {mobClone.name} warped but is not on the NavMesh. Destroying instead of leaving it stuck.");
                        Destroy(mobClone);
                        continue;
                    }
                }

                aliveMobs.Add(mobClone);
            }

            Debug.Log($"MobSpawner ring at radius {minRadius}-{maxRadius}: {aliveMobs.Count}/{mobLimit} mobs alive.");
        }

        // Tries several random points within the ring, sampling the NavMesh near each one, until a valid on-mesh
        // position is found - guards against a single unlucky point (eg landing inside a rock) wasting a spawn slot
        private bool TryFindNavMeshSpawnPoint(float minRadius, float maxRadius, out Vector3 navMeshPosition)
        {
            for (int attempt = 0; attempt < SpawnPlacementAttempts; attempt++)
            {
                Vector3 candidate = GetRandomPointInRing(minRadius, maxRadius);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, SpawnSampleRadius, NavMesh.AllAreas))
                {
                    navMeshPosition = hit.position;
                    return true;
                }
            }

            navMeshPosition = Vector3.zero;
            return false;
        }

        // Picks a random point on the terrain surface inside the given ring around Center Orient
        private Vector3 GetRandomPointInRing(float minRadius, float maxRadius)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minRadius, maxRadius);

            Vector3 center = _centerOrientTarget.position;
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            float y = _targetTerrain.SampleHeight(new Vector3(x, 0f, z)) + _targetTerrain.transform.position.y;

            return new Vector3(x, y, z);
        }

        // Picks one random prefab from a difficulty's mob prefab list
        private GameObject GetRandomMobPrefab(GameObject[] mobPrefabs)
        {
            if (mobPrefabs == null || mobPrefabs.Length == 0) return null;
            return mobPrefabs[Random.Range(0, mobPrefabs.Length)];
        }


        ///// Coroutines /////

        // Waits for a ring's own delay, then tops that ring back up to its allowed mob limit
        private IEnumerator SpawnRingRoutine(GameObject[] mobPrefabs, List<GameObject> aliveMobs, int mobLimit, float minRadius, float maxRadius, float delay)
        {
            yield return new WaitForSeconds(delay);
            SpawnMobsInRing(mobPrefabs, aliveMobs, mobLimit, minRadius, maxRadius);
        }
    }
}

// Implementation Steps:
// 1. In Unity, go to Edit > Project Settings > Tags and Layers and add a tag for your terrain (e.g. "GameTerrain").
// 2. Select your Terrain GameObject in the scene and set its Tag to match TerrainTag above.
// 3. Make sure a "BaseSlot" tag exists (also used by Mobai.cs) on the empty parent object that holds your base's
//    active children, with an empty child object under it named "Center Orient" - rings are centered on this point.
// 4. Add this MobSpawner script to any GameObject in the scene (it does not need to be a child of the terrain).
// 5. Fill in Easy/Medium/Hard Mob Prefabs with the mob prefab assets you want spawned at each difficulty.
// 6. Adjust the ring radii (Safe/Easy/Medium/Hard), spawn limits, and per-ring delays to match your game's pacing.
// 7. On Start, every ring spawns automatically after its own delay. To top a ring back up later (e.g. after mobs
//    are killed), call SpawnEasyRingMobs(), SpawnMediumRingMobs(), SpawnHardRingMobs(), or SpawnAllRings() again -
//    each call only spawns enough mobs to refill that ring up to its limit, it will never exceed it.
