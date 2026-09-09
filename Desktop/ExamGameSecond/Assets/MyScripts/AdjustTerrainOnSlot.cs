using System.Collections.Generic;
using UnityEngine;

namespace NTGD124
{
    // Clears painted trees and detail objects (grass) off the terrain around this
    // slot's position. Call ClearTerrainAroundSlot() once this slot is unlocked -
    // BaseSlotExpander.TryUnlockSlot does this automatically for whichever slot's
    // GameObject carries this script.
    public class AdjustTerrainOnSlot : MonoBehaviour
    {
        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Removes painted trees and detail (grass) objects from the GameTerrain-tagged terrain within ClearRadius of this slot's position. Triggered by BaseSlotExpander when this slot is unlocked.";


        ///// Public Variables/Editor Properties /////

        [Tooltip("Tag placed on the terrain GameObject - found automatically at runtime.")]
        public string TerrainTag = "GameTerrain";

        [Tooltip("How far around this slot's position to clear trees and details, in world units.")]
        public float ClearRadius = 5f;


        ///// Private Variables /////

        private Terrain _terrain;

        // Tracks which Terrains already got a runtime TerrainData clone this play session,
        // so multiple slots sharing one terrain don't each overwrite each other's clone.
        private static readonly HashSet<Terrain> _clonedTerrains = new();


        ///// Unity Methods /////

        private void Start()
        {
            LocateTerrain();
        }


        ///// Trigger Methods /////

        // Trigger type: called by BaseSlotExpander once this slot becomes unlocked
        public void ClearTerrainAroundSlot()
        {
            if (_terrain == null) LocateTerrain();
            if (_terrain == null) return;

            RemoveTreesInRadius();
            RemoveDetailsInRadius();
        }


        ///// Action Methods /////

        private void LocateTerrain()
        {
            GameObject terrainObject = GameObject.FindGameObjectWithTag(TerrainTag);
            if (terrainObject == null)
            {
                Debug.LogWarning($"AdjustTerrainOnSlot on {gameObject.name} could not find an object tagged '{TerrainTag}'.");
                return;
            }

            _terrain = terrainObject.GetComponent<Terrain>();
            if (_terrain == null) return;

            EnsureRuntimeTerrainDataClone(terrainObject);
        }

        // Swaps in a runtime-only copy of the TerrainData the first time any slot needs to edit
        // it, so tree/detail removal never touches the actual TerrainData asset on disk - once
        // Play mode stops, the clone is discarded and the original painted terrain is untouched.
        private void EnsureRuntimeTerrainDataClone(GameObject terrainObject)
        {
            if (_clonedTerrains.Contains(_terrain)) return;

            TerrainData runtimeCopy = Instantiate(_terrain.terrainData);
            _terrain.terrainData = runtimeCopy;

            TerrainCollider terrainCollider = terrainObject.GetComponent<TerrainCollider>();
            if (terrainCollider != null) terrainCollider.terrainData = runtimeCopy;

            _clonedTerrains.Add(_terrain);
        }

        // Rebuilds the tree instance list, dropping any tree within ClearRadius of this slot
        private void RemoveTreesInRadius()
        {
            TerrainData terrainData = _terrain.terrainData;
            Vector3 terrainOrigin = _terrain.transform.position;

            List<TreeInstance> keptTrees = new();

            foreach (TreeInstance tree in terrainData.treeInstances)
            {
                Vector3 treeWorldPos = Vector3.Scale(tree.position, terrainData.size) + terrainOrigin;
                if (!IsWithinClearRadius(treeWorldPos)) keptTrees.Add(tree);
            }

            terrainData.treeInstances = keptTrees.ToArray();
        }

        // Zeroes out every detail (grass) layer's density map within a box around this slot
        private void RemoveDetailsInRadius()
        {
            TerrainData terrainData = _terrain.terrainData;
            Vector3 localPos = transform.position - _terrain.transform.position;

            int centerX = Mathf.RoundToInt(localPos.x / terrainData.size.x * terrainData.detailWidth);
            int centerZ = Mathf.RoundToInt(localPos.z / terrainData.size.z * terrainData.detailHeight);
            int radiusX = Mathf.CeilToInt(ClearRadius / terrainData.size.x * terrainData.detailWidth);
            int radiusZ = Mathf.CeilToInt(ClearRadius / terrainData.size.z * terrainData.detailHeight);

            int startX = Mathf.Clamp(centerX - radiusX, 0, terrainData.detailWidth - 1);
            int startZ = Mathf.Clamp(centerZ - radiusZ, 0, terrainData.detailHeight - 1);
            int width = Mathf.Clamp(radiusX * 2, 1, terrainData.detailWidth - startX);
            int height = Mathf.Clamp(radiusZ * 2, 1, terrainData.detailHeight - startZ);

            for (int layer = 0; layer < terrainData.detailPrototypes.Length; layer++)
            {
                int[,] emptyLayer = new int[height, width];
                terrainData.SetDetailLayer(startX, startZ, layer, emptyLayer);
            }
        }

        // Flat (XZ-only) distance check between a world position and this slot
        private bool IsWithinClearRadius(Vector3 worldPos)
        {
            Vector3 flatSlot = new(transform.position.x, 0f, transform.position.z);
            Vector3 flatPoint = new(worldPos.x, 0f, worldPos.z);
            return Vector3.Distance(flatSlot, flatPoint) <= ClearRadius;
        }
    }
}

// Implementation Steps:
// 1. Attach this script to each of the 9 base slot GameObjects (the "centerOrientBase..."
//    children under BaseSlots) - it clears terrain around whichever slot it's on.
// 2. Make sure your Terrain GameObject is tagged "GameTerrain" (or update TerrainTag to match).
// 3. Set ClearRadius per slot if some need a bigger cleared area than others (default 5).
// 4. No manual wiring needed beyond that - BaseSlotExpander.TryUnlockSlot automatically calls
//    ClearTerrainAroundSlot() on a slot's AdjustTerrainOnSlot component (if present) once it unlocks.
