using UnityEngine;
using NTGD124;

// Scatters real, harvestable prefab instances across the terrain at Play
// start - trees for Wood, rock prefabs for Rock, and whatever else follows
// the same pattern later. The terrain's own painted decorative trees/rocks
// have no individual Collider/GameObject to hang HarvestableResource/
// InteractableHotspot on (see ResourceEconomyDesignNotes.txt), so this spawns
// real prefab instances instead. Each prefab already carries the
// "Interactive" tag + trigger Collider baked in; HarvestableResource (which
// self-adds InteractableHotspot) is attached and configured directly here as
// each node is placed, no separate scene-scanning pass needed afterward.
//
// One GameObject per resource type (e.g. "TreeSpawner", "RockSpawner"), each
// holding its own instance of this component configured differently - set up
// by UInavigator, not manually in the scene.
public class ResourceNodeSpawner : MonoBehaviour
{
    [Tooltip("Prefab(s) to scatter - e.g. just tree_1 for trees, or all 11 rock variants for rocks.")]
    public GameObject[] prefabs;
    [Tooltip("Tag on the Terrain GameObject - matches AdjustTerrainOnSlot/WaterFloater's convention.")]
    public string terrainTag = "GameTerrain";
    [Tooltip("How many instances to scatter PER prefab - e.g. 10 rocks per prefab x 11 rock prefabs = 110 total.")]
    public int countPerPrefab = 200;
    [Tooltip("Item name (must match ItemCatalog.All) granted per hit on any spawned node.")]
    public string resourceItemName = "Wood";
    [Tooltip("Tool subcategory required to land a hit on any spawned node.")]
    public ToolSubCategory requiredTool = ToolSubCategory.Cutting;
    [Tooltip("Radius checked for a nearby water volume before placing a node - increase if nodes still land in water, decrease if valid shoreline spots get skipped.")]
    public float waterCheckRadius = 2f;
    [Tooltip("Attempts per node before giving up on that one, in case repeated random spots keep landing in water.")]
    public int maxAttemptsPerNode = 10;

    void Start()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning($"{name} has no prefabs assigned - nothing to spawn.");
            return;
        }

        Terrain terrain = FindTerrain();
        if (terrain == null)
        {
            Debug.LogWarning($"{name} could not find a terrain tagged '{terrainTag}'.");
            return;
        }

        int spawned = 0;
        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;

            for (int i = 0; i < countPerPrefab; i++)
            {
                if (TrySpawnOne(prefab, terrain)) spawned++;
            }
        }

        Debug.Log($"[{name}] Spawned {spawned}/{prefabs.Length * countPerPrefab} nodes across {prefabs.Length} prefab(s).");
    }

    private Terrain FindTerrain()
    {
        GameObject terrainObject = GameObject.FindGameObjectWithTag(terrainTag);
        return terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
    }

    private bool TrySpawnOne(GameObject prefab, Terrain terrain)
    {
        for (int attempt = 0; attempt < maxAttemptsPerNode; attempt++)
        {
            Vector3 position = RandomPointOnTerrain(terrain);
            if (IsNearWater(position)) continue;

            GameObject instance = Instantiate(prefab, position, Quaternion.identity, transform);
            instance.name = prefab.name;

            HarvestableResource harvestable = instance.GetComponent<HarvestableResource>();
            if (harvestable == null) harvestable = instance.AddComponent<HarvestableResource>();
            harvestable.resourceItemName = resourceItemName;
            harvestable.requiredTool = requiredTool;

            return true;
        }

        return false;
    }

    private Vector3 RandomPointOnTerrain(Terrain terrain)
    {
        Vector3 size = terrain.terrainData.size;
        Vector3 origin = terrain.transform.position;

        float worldX = origin.x + Random.Range(0f, size.x);
        float worldZ = origin.z + Random.Range(0f, size.z);
        float worldY = origin.y + terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));

        return new Vector3(worldX, worldY, worldZ);
    }

    // Water bodies (see WaterFloater) auto-fit their own trigger BoxCollider to
    // their mesh bounds at Awake - checking for that component on anything
    // overlapping this radius covers every water volume in the scene without
    // needing a dedicated tag or layer for water.
    private bool IsNearWater(Vector3 position)
    {
        Collider[] hits = Physics.OverlapSphere(position, waterCheckRadius);
        foreach (Collider hit in hits)
        {
            if (hit.GetComponent<WaterFloater>() != null) return true;
        }
        return false;
    }
}
