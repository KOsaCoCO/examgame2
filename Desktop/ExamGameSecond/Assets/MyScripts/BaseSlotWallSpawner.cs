using UnityEngine;

namespace NTGD124
{
    // Spawns one copy of the wall-kit prefab (4 straight walls + 4 corners, see
    // BaseSlotWall's Implementation Steps for how that prefab should be set up - it's the
    // prefab-ified version of the "WallExample" reference object) at every one of
    // BaseSlotExpander's 9 slots, once, at scene Start - not just at unlocked ones, since a
    // locked slot still needs its walls visible to block it off. Each spawned piece then
    // hides itself independently via BaseSlotWall once its own slot and whatever it borders
    // are unlocked.
    public class BaseSlotWallSpawner : MonoBehaviour
    {
        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Instantiates WallKitPrefab once per slot in SlotExpander.Slots, positioned at that slot but deliberately NOT parented under the slot's own GameObject - BaseSlotExpander hides a locked slot's GameObject entirely (see RefreshSlotVisual), which would drag any child walls down with it and defeat the point of a wall standing there while the slot is still locked. Spawned kits parent under this component's own transform instead.";


        ///// Public Variables/Editor Properties /////

        [Tooltip("The BaseSlotExpander holding the 9 slots to spawn a wall kit at - same object this is usually placed alongside.")]
        public BaseSlotExpander SlotExpander;

        [Tooltip("Prefab containing the 4 walls + 4 corners, each with a BaseSlotWall component and its Directions already set (see BaseSlotWall's Implementation Steps).")]
        public GameObject WallKitPrefab;

        [Tooltip("Grid coord of the slot WallKitPrefab's geometry was actually tuned against (e.g. its fence pieces were hand-dragged onto the terrain at this slot) - NOT necessarily (0,0). Every spawn is positioned as an offset FROM this reference slot rather than reset to the target slot directly, so whatever fine alignment exists between the kit's authored root and this slot survives being reproduced elsewhere.")]
        public Vector2Int ReferenceSlotGridCoord = new(0, -50);


        ///// Unity Methods /////

        private void Start()
        {
            Debug.Log($"[BaseSlotWallSpawner] Start() running on '{gameObject.name}'. SlotExpander={(SlotExpander == null ? "NULL" : SlotExpander.name)}, WallKitPrefab={(WallKitPrefab == null ? "NULL" : WallKitPrefab.name)}.");

            if (SlotExpander == null || WallKitPrefab == null)
            {
                Debug.LogWarning($"[BaseSlotWallSpawner] on {gameObject.name} is missing SlotExpander or WallKitPrefab - no wall kits will spawn.");
                return;
            }

            Transform referenceSlot = SlotExpander.GetSlotAtGridCoord(ReferenceSlotGridCoord);
            if (referenceSlot == null)
            {
                Debug.LogWarning($"[BaseSlotWallSpawner] No slot found at ReferenceSlotGridCoord {ReferenceSlotGridCoord} - can't compute a safe spawn offset, falling back to WallKitPrefab's own authored position for every slot (likely wrong for all but the reference slot itself).");
            }
            else
            {
                Debug.Log($"[BaseSlotWallSpawner] Reference slot '{referenceSlot.name}' at grid {ReferenceSlotGridCoord} resolved to world position {referenceSlot.position}. WallKitPrefab's own authored position is {WallKitPrefab.transform.position} - every spawn will be offset from THIS pairing.");
            }

            Debug.Log($"[BaseSlotWallSpawner] SlotExpander.Slots has {SlotExpander.Slots.Length} entries - spawning one wall kit per slot.");

            foreach (BaseSlotExpander.BaseSlot slot in SlotExpander.Slots)
            {
                SpawnWallKit(slot, referenceSlot);
            }
        }


        ///// Action Methods /////

        private void SpawnWallKit(BaseSlotExpander.BaseSlot slot, Transform referenceSlot)
        {
            if (slot == null || slot.SlotObject == null)
            {
                Debug.LogWarning("[BaseSlotWallSpawner] Skipped a slot with no SlotObject assigned.");
                return;
            }

            // The kit's geometry was tuned to look right AT the reference slot, not at a clean
            // local origin - so spawn position is "however far this slot is from the reference
            // slot, applied on top of the kit's own authored position" rather than a hard reset
            // to slot.SlotObject.position. If referenceSlot is missing, this degrades to the old
            // (likely wrong) behavior as a visible fallback rather than a silent one.
            Vector3 spawnPosition;
            if (referenceSlot == null)
            {
                spawnPosition = slot.SlotObject.position;
            }
            else
            {
                Vector3 offsetFromReference = slot.SlotObject.position - referenceSlot.position;
                spawnPosition = WallKitPrefab.transform.position + offsetFromReference;
            }

            GameObject kit = Instantiate(WallKitPrefab, spawnPosition, WallKitPrefab.transform.rotation, transform);
            kit.name = $"WallKit ({slot.GridCoord.x},{slot.GridCoord.y})";

            BaseSlotWall[] wallPieces = kit.GetComponentsInChildren<BaseSlotWall>(true);
            Debug.Log($"[BaseSlotWallSpawner] Spawned '{kit.name}' at {spawnPosition} (slot itself is at {slot.SlotObject.position}, grid {slot.GridCoord}) - found {wallPieces.Length} BaseSlotWall piece(s) inside it.");

            foreach (BaseSlotWall wallPiece in wallPieces)
            {
                wallPiece.Initialize(SlotExpander, slot.GridCoord);
            }
        }
    }
}

// Implementation Steps:
// 1. Turn the WallExample hierarchy into a prefab: the "wall" object (with wall1-4 as its
//    children) and the 4 cornerWalls objects should all end up as children of one root
//    GameObject (e.g. rename/repurpose WallExample itself) - drag that root into Assets/ to
//    create the prefab asset, then assign it to WallKitPrefab below.
// 2. Attach BaseSlotWall to each of the 8 pieces (wall1-4, 4 corners) inside that prefab and
//    set Directions per BaseSlotWall's own Implementation Steps.
// 3. Add this component anywhere in the scene (alongside BaseSlotExpander is simplest), assign
//    SlotExpander and WallKitPrefab in the Inspector.
// 4. Fill in GridCoord on all 9 of BaseSlotExpander.Slots first (see its own Implementation
//    Steps) - the spawner needs those to name/position kits and for BaseSlotWall's neighbor
//    lookups to work.
// 5. Set ReferenceSlotGridCoord to whichever slot WallKitPrefab's geometry was actually built/
//    dragged onto (check the Console log on Start - it prints both the reference slot's world
//    position and WallKitPrefab's own authored position so you can sanity-check they're close
//    together before trusting the spawn positions elsewhere).
