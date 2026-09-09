using UnityEngine;

namespace NTGD124
{
    // Optional per-prefab override so a plant node's own asset name (e.g. "Mushroom Cluster 1" -
    // kept stable so the project/Prefabs folder stays recognizable) can differ from the
    // ItemCatalog entry it actually grants on harvest (e.g. "Day Mushroom" - the player-facing,
    // category-based name several prefabs across Day/Night/Regular now share). Add this to a
    // plant prefab and set ItemName to opt into that; a prefab with no override on it (or a blank
    // ItemName) keeps using its own prefab.name exactly as before - see PlantSpawnerBase.TrySpawnOne.
    public class PlantItemNameOverride : MonoBehaviour
    {
        public string ItemName;
    }
}
