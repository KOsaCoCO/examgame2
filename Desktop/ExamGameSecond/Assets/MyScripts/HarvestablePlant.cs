using System;
using UnityEngine;

namespace NTGD124
{
    // One spawned plant node (a Mushroom/Flower variant from Assets/FreeAssets/Ultrasonic/
    // Decorative Plant Assets/Prefabs/) - single-hit harvest via E/right-click, no tool needed
    // (unlike HarvestableResource's trees/rocks, which require an armed Axe/Pickaxe). Appears/
    // disappears instantly (no grow/shrink animation) since these are runtime-spawned and
    // removed rather than permanent scene decoration. The owning spawner (PlantSpawnerBase) calls Initialize()
    // right after Instantiate + AddComponent, then listens to OnHarvested to keep its own
    // registry (and, for RegularPlantSpawner, its respawn-cooldown tracking) in sync - this
    // component never talks back to a specific spawner type directly.
    [RequireComponent(typeof(Collider))]
    public class HarvestablePlant : MonoBehaviour
    {
        [Header("FX (wire in later)")]
        [Tooltip("Optional - left null until real FX exists. Shown as soon as this plant spawns, hidden the instant it's harvested or force-removed.")]
        public GameObject FxObject;

        // Fired the moment this plant is harvested by the player, before it's destroyed - the
        // owning spawner listens to remove it from its own registry immediately rather than
        // waiting for the GameObject to actually finish being destroyed.
        public event Action<HarvestablePlant> OnHarvested;

        // The ItemCatalog entry granted on harvest - matches this instance's source prefab name
        // exactly (see PlantSpawnerBase.TrySpawnOne).
        public string ItemName { get; private set; }
        public GameObject SourcePrefab { get; private set; }

        private InventoryManager inventory;
        private InteractableHotspot hotspot;
        private bool isHarvested;

        // Called once by the spawner right after Instantiate + AddComponent<HarvestablePlant>.
        public void Initialize(GameObject sourcePrefab, string itemName)
        {
            SourcePrefab = sourcePrefab;
            ItemName = itemName;
        }

        void Start()
        {
            // Include inactive hierarchies, same reasoning as HarvestableResource - InventoryManager
            // can be found mid-startup before its canvas is shown.
            inventory = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);

            hotspot = GetComponent<InteractableHotspot>();
            if (hotspot == null) hotspot = gameObject.AddComponent<InteractableHotspot>();
            hotspot.OnInteract.AddListener(HandleHit);
            hotspot.OnSecondaryInteract.AddListener(HandleHit);

            if (FxObject != null) FxObject.SetActive(true);
        }

        void OnDestroy()
        {
            if (hotspot == null) return;
            hotspot.OnInteract.RemoveListener(HandleHit);
            hotspot.OnSecondaryInteract.RemoveListener(HandleHit);
        }

        // E or right-click while in range - a single hit harvests it, no matter what (if
        // anything) is armed in hand. Unlike HarvestableResource's trees/rocks, plants don't
        // need a tool.
        private void HandleHit()
        {
            if (isHarvested || inventory == null) return;

            isHarvested = true;
            inventory.AddItem(ItemCatalog.GetByName(ItemName), 1);
            PlayerLevel.Instance?.AddExperience(1);
            HarvestSoundEffects.Instance?.PlayPluck(transform.position);

            OnHarvested?.Invoke(this);
            RemoveNow();
        }

        // Called by the owning spawner to remove this instance without granting anything - e.g.
        // NightPlantSpawner/DayPlantSpawner clearing their window's unharvested leftovers.
        public void ForceRemove()
        {
            if (isHarvested) return; // already removed via HandleHit
            RemoveNow();
        }

        // Destroys the GameObject immediately - keeps the spawner's own registry list clean (no
        // ever-growing list of dead references) since OnHarvested already told it to drop this
        // instance, or (for a force-removal) the spawner is clearing its whole list right after
        // calling this anyway.
        private void RemoveNow()
        {
            if (FxObject != null) FxObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}

// Implementation Steps:
// 1. Nothing to place manually - PlantSpawnerBase adds this component to each spawned plant
//    instance and calls Initialize() itself.
// 2. FxObject is left null until real FX assets exist - once you have one, assign it per-prefab
//    on the plant node right after AddComponent (in PlantSpawnerBase.TrySpawnOne), or drag it
//    directly onto the source prefab as a child (inactive by default) if you'd rather author it
//    per-prefab in the Editor instead of in code.
