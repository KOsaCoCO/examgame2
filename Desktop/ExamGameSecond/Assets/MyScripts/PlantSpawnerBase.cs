using System.Collections.Generic;
using UnityEngine;

namespace NTGD124
{
    // Shared spawn/registry logic for the 3 plant-node spawners under Objects/PlantLocations
    // (NightPlantSpawner, DayPlantSpawner, RegularPlantSpawner - each a thin subclass wiring
    // this base to a different day/night trigger). Spawns real HarvestablePlant instances of
    // Prefabs at random points inside this GameObject's own BoxCollider footprint (the "three
    // colliders" marking each spawner's terrain area, used purely as a spawn-area locator - not
    // a physical obstacle), sampled onto the terrain, avoiding water and rejecting only points
    // that would actually overlap an existing tree/rock/plant node's own collider - plants are
    // free to spawn right next to one. Caps concurrently-alive instances per prefab at maxAlivePerPrefab.
    [RequireComponent(typeof(BoxCollider))]
    public abstract class PlantSpawnerBase : MonoBehaviour
    {
        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Spawns/tracks HarvestablePlant instances of Prefabs inside this object's own BoxCollider footprint on the terrain. Subclasses (Night/Day/Regular) decide when SpawnFullBatch()/ClearAll() get called - this base only owns the actual spawn placement, per-prefab cap, and registry bookkeeping.";


        ///// Public Variables/Editor Properties /////

        [Header("Prefabs")]
        [Tooltip("Plant prefabs this spawner scatters - ItemCatalog must have an entry whose ItemName exactly matches each prefab's own asset name, unless the prefab carries a PlantItemNameOverride (e.g. several prefabs sharing one category-based item like \"Day Mushroom\").")]
        public GameObject[] Prefabs;

        [Header("Spawn Limits")]
        [Tooltip("Max concurrently-alive instances allowed per prefab, at any one time.")]
        public int maxAlivePerPrefab = 3;

        [Header("Terrain & Avoidance")]
        public string terrainTag = "GameTerrain";
        [Tooltip("Radius checked for a nearby water volume before placing a plant.")]
        public float waterCheckRadius = 2f;
        [Tooltip("Broad-phase radius searched for existing tree/rock/plant nodes before placing a plant - just needs to be wide enough to catch any node whose own collider might reach the candidate point. The actual accept/reject decision uses harvestableClearance instead, so plants can spawn right next to a tree/rock, they just can't overlap into it.")]
        public float harvestableSearchRadius = 8f;
        [Tooltip("Minimum clearance, in world units, a spawn point must keep from an existing tree/rock/plant node's own collider surface - rejects only an actual (near-)overlap, not mere proximity.")]
        public float harvestableClearance = 0.5f;
        [Tooltip("Random ring points to try per spawn slot before giving up on it this pass.")]
        public int maxAttemptsPerSpawn = 10;

        [Header("Spawn FX")]
        [Tooltip("Optional - if assigned, spawned as a child of every plant instance this spawner creates, centered on the model's rendered bounds. Wired as the instance's HarvestablePlant.FxObject, so it shows as soon as the plant spawns and hides the instant it's harvested or force-removed. Leave null for spawners that shouldn't have FX (e.g. Day/Regular).")]
        public GameObject fxPrefab;
        [Tooltip("Uniform scale applied to the spawned fx instance, independent of the plant it's attached to (e.g. 0.333 shrinks it to a third of the fx prefab's own authored size). 1 = fx prefab's authored size, unchanged.")]
        public float fxScale = 1f;


        ///// Private Variables /////

        private BoxCollider _footprint;
        private Terrain _terrain;
        private readonly Dictionary<GameObject, List<HarvestablePlant>> _activeByPrefab = new();


        ///// Unity Methods /////

        protected virtual void Awake()
        {
            _footprint = GetComponent<BoxCollider>();
            // These footprints are pure spawn-area markers, not physical obstacles - force this
            // regardless of how it was authored in the Editor, so a solid box never silently
            // walls off a chunk of terrain.
            _footprint.isTrigger = true;
        }

        protected virtual void Start()
        {
            _terrain = FindTerrain();
        }


        ///// Action Methods /////

        // Tops every configured prefab back up to maxAlivePerPrefab. Called by subclasses at
        // whatever moment their own window opens (scene Start, OnDayBegan, OnNightBegan, ...).
        protected void SpawnFullBatch()
        {
            if (Prefabs == null) return;
            foreach (GameObject prefab in Prefabs)
            {
                if (prefab != null) TopUpPrefab(prefab);
            }
        }

        // Shrinks and destroys every currently-alive instance across every prefab, granting
        // nothing - Night/Day spawners clearing their window's leftovers at its end. Already-
        // collected items sitting in the player's inventory are untouched either way.
        protected void ClearAll()
        {
            foreach (List<HarvestablePlant> instances in _activeByPrefab.Values)
            {
                foreach (HarvestablePlant plant in instances)
                {
                    if (plant != null) plant.ForceRemove();
                }
            }
            _activeByPrefab.Clear();
        }

        // Spawns enough new instances of one prefab to bring it back up to maxAlivePerPrefab -
        // used both by SpawnFullBatch (every prefab at once) and RegularPlantSpawner (one prefab
        // at a time, once its 2-cycle cooldown on a harvested slot has elapsed).
        protected void TopUpPrefab(GameObject prefab)
        {
            if (_terrain == null || _footprint == null) return;

            List<HarvestablePlant> instances = GetOrCreateList(prefab);
            instances.RemoveAll(p => p == null); // keep the registry clean, never an ever-growing list of dead references

            int slotsAvailable = maxAlivePerPrefab - instances.Count;
            for (int i = 0; i < slotsAvailable; i++)
            {
                HarvestablePlant plant = TrySpawnOne(prefab);
                if (plant != null) instances.Add(plant);
            }
        }

        // Overridden by RegularPlantSpawner to start a respawn cooldown for the harvested slot.
        // Night/Day spawners don't need anything extra here - their own leftovers just get
        // cleared wholesale by ClearAll() at the window's end regardless of what was harvested.
        protected virtual void HandlePlantHarvested(HarvestablePlant plant)
        {
        }

        private List<HarvestablePlant> GetOrCreateList(GameObject prefab)
        {
            if (!_activeByPrefab.TryGetValue(prefab, out List<HarvestablePlant> instances))
            {
                instances = new List<HarvestablePlant>();
                _activeByPrefab[prefab] = instances;
            }
            return instances;
        }

        private HarvestablePlant TrySpawnOne(GameObject prefab)
        {
            for (int attempt = 0; attempt < maxAttemptsPerSpawn; attempt++)
            {
                Vector3 position = RandomPointInFootprint();
                if (IsNearWater(position) || IsNearExistingHarvestable(position)) continue;

                // Instantiated unparented first, then reparented with worldPositionStays so the
                // instance keeps the prefab's own scale - this spawner's own transform is stretched
                // non-uniformly to define its footprint area (see RandomPointInFootprint), and
                // Instantiate(prefab, pos, rot, parent) would otherwise make every spawned plant
                // inherit that distortion on top of its own scale.
                GameObject instance = Instantiate(prefab, position, Quaternion.identity);
                instance.transform.SetParent(transform, worldPositionStays: true);
                instance.name = prefab.name;
                instance.tag = "Interactive";

                if (instance.GetComponent<Collider>() == null)
                {
                    SphereCollider collider = instance.AddComponent<SphereCollider>();
                    collider.isTrigger = true;
                    collider.radius = 1f;
                }

                PlantItemNameOverride nameOverride = prefab.GetComponent<PlantItemNameOverride>();
                string itemName = !string.IsNullOrEmpty(nameOverride?.ItemName) ? nameOverride.ItemName : prefab.name;

                HarvestablePlant plant = instance.AddComponent<HarvestablePlant>();
                plant.Initialize(prefab, itemName);
                plant.OnHarvested += HandlePlantHarvested;

                if (fxPrefab != null)
                {
                    GameObject fx = Instantiate(fxPrefab, instance.transform);
                    fx.name = fxPrefab.name;
                    fx.transform.localPosition = GetLocalBoundsCenter(instance);
                    fx.transform.localRotation = Quaternion.identity;
                    fx.transform.localScale = Vector3.one * fxScale;
                    plant.FxObject = fx;
                }

                return plant;
            }

            Debug.LogWarning($"[{name}] Could not find a valid spot for '{prefab.name}' after {maxAttemptsPerSpawn} attempts - skipping this spawn slot.");
            return null;
        }

        // These plant models are pivoted at their base (so they sit flush on the terrain when
        // placed), not at their visual center - a naive Vector3.zero FX position would sit at
        // ground level instead of centered on the model. Combines every renderer's world-space
        // bounds and converts the result into the instance's local space, so the FX (parented to
        // it) lands centered on the model in x/y/z regardless of the model's own dimensions.
        private static Vector3 GetLocalBoundsCenter(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return Vector3.zero;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            return instance.transform.InverseTransformPoint(bounds.center);
        }

        private Terrain FindTerrain()
        {
            GameObject terrainObject = GameObject.FindGameObjectWithTag(terrainTag);
            return terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
        }

        // Picks a random point inside this spawner's own BoxCollider footprint, in the box's
        // local space (respects its authored position/rotation/scale correctly, unlike a naive
        // world-space AABB), then samples the terrain for ground height at that XZ.
        private Vector3 RandomPointInFootprint()
        {
            Vector3 localPoint = new Vector3(
                Random.Range(-0.5f, 0.5f) * _footprint.size.x,
                0f,
                Random.Range(-0.5f, 0.5f) * _footprint.size.z
            ) + _footprint.center;

            Vector3 worldPoint = _footprint.transform.TransformPoint(localPoint);
            worldPoint.y = _terrain.transform.position.y + _terrain.SampleHeight(worldPoint);
            return worldPoint;
        }

        // Water bodies (see WaterFloater) auto-fit their own trigger BoxCollider to their mesh
        // bounds at Awake - checking for that component on anything overlapping this radius
        // covers every water volume in the scene without needing a dedicated tag or layer.
        private bool IsNearWater(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, waterCheckRadius);
            foreach (Collider hit in hits)
            {
                if (hit.GetComponent<WaterFloater>() != null) return true;
            }
            return false;
        }

        // Rejects a candidate point only if it would actually overlap (within harvestableClearance
        // of) an existing tree/rock (HarvestableResource) or other plant node (HarvestablePlant,
        // including this spawner's own and every other spawner's) - plants are free to spawn right
        // next to one, they just can't visually cross into its mesh/collider.
        private bool IsNearExistingHarvestable(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, harvestableSearchRadius);
            foreach (Collider hit in hits)
            {
                if (hit.GetComponent<HarvestableResource>() == null && hit.GetComponent<HarvestablePlant>() == null) continue;

                Vector3 closestPoint = hit.ClosestPoint(position);
                if (Vector3.Distance(closestPoint, position) < harvestableClearance) return true;
            }
            return false;
        }
    }
}
