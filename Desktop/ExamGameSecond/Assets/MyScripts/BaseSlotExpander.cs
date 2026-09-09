using System;
using System.Collections.Generic;
using UnityEngine;

namespace NTGD124
{
    // Holds the 9 base slots that unlock over the course of the game. Each
    // slot stays hidden until unlocked. Drag each slot's GameObject (from the
    // "BaseSlots" hierarchy in the scene) into the Slots array below, in the
    // order they should unlock.
    public class BaseSlotExpander : MonoBehaviour
    {
        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Manages the 9 unlockable base slots. Each slot's GameObject is hidden until IsUnlocked is true. UnlockRequirements is the item/resource cost to unlock that slot (empty = free). HasRequiredItems checks real inventory counts via Inventory (wired automatically by UInavigator); TryUnlockSlot spends the items on a successful unlock.";

        // Wired automatically by UInavigator.Start() - a plain field assignment, so it's
        // safe regardless of Awake/Start order between this and UInavigator.
        [HideInInspector] public InventoryManager Inventory;
        [HideInInspector] public PlayerHandManager Hand;

        // Costs draw from inventory first, then whatever's in hand - treated as one
        // combined pool via ResourcePool, since a player shouldn't have to manually
        // move an item back to inventory just to spend it.
        private IResourceContainer[] ResourceSources => new IResourceContainer[] { Inventory, Hand };


        ///// Data Types /////

        [Serializable]
        public class ItemRequirement
        {
            public string ItemName;
            public int Quantity = 1;
        }

        [Serializable]
        public class BaseSlot
        {
            public Transform SlotObject;
            public bool IsUnlocked;

            [Tooltip("This slot's position in the fixed 3x3 grid, matching the (X,Z) in its " +
                "\"centerOrientBase (X,Z)\" name (e.g. centerOrientBase (0,-50) -> GridCoord " +
                "0,-50). Used by BaseSlotWallSpawner/BaseSlotWall to find which slot sits in a " +
                "given compass direction from another. Drawn as X/Z (see GridAxisLabelsDrawer) " +
                "even though it's still a plain Vector2Int (its second component IS world Z, " +
                "just labeled Y by Unity's default Vector2Int drawer).")]
            [GridAxisLabels]
            public Vector2Int GridCoord;

            [Tooltip("Items/quantities needed to unlock this slot. Leave empty until the inventory system exists.")]
            public List<ItemRequirement> UnlockRequirements = new();

            [Tooltip("If set, this slot also needs the wizard's miasma quest (GameEvents.OnWizardQuestCompleted) finished before it can be bought, on top of any UnlockRequirements above (empty UnlockRequirements + this flag = free once the quest is done).")]
            public bool RequiresWizardQuestCompleted;
        }


        ///// Public Variables/Editor Properties /////

        [Tooltip("The 9 base slots, in unlock order.")]
        public BaseSlot[] Slots = new BaseSlot[9];

        [Tooltip("Siege hits (from the boss and/or rallying mobs during a boss fight) an unlocked slot can take before DestroySlot() reverts it back to locked - see StructureHealth.")]
        public int SlotMaxHits = 20;


        ///// Private Variables /////

        private bool _wizardQuestCompleted;


        ///// Unity Methods /////

        private void OnEnable()
        {
            GameEvents.OnWizardQuestCompleted += HandleWizardQuestCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OnWizardQuestCompleted -= HandleWizardQuestCompleted;
        }

        private void Start()
        {
            foreach (BaseSlot slot in Slots)
            {
                RefreshSlotVisual(slot);
            }
        }

        private void HandleWizardQuestCompleted()
        {
            _wizardQuestCompleted = true;
        }


        ///// Trigger Methods /////

        // Checks requirements, spends the required items, and unlocks the given slot.
        // Returns whether it unlocked. Re-checks affordability right before spending,
        // even though the UI should already have gated the click on CanAffordSlot -
        // so this stays correct even if inventory changed in between.
        public bool TryUnlockSlot(Transform slotObject)
        {
            Debug.Log($"[BaseSlotExpander] TryUnlockSlot called for '{(slotObject == null ? "NULL" : slotObject.name)}'.");

            BaseSlot slot = Array.Find(Slots, s => s != null && s.SlotObject == slotObject);
            if (slot == null)
            {
                Debug.LogWarning($"[BaseSlotExpander] TryUnlockSlot: no matching entry in Slots for '{slotObject?.name}'.");
                return false;
            }
            if (slot.IsUnlocked)
            {
                Debug.Log($"[BaseSlotExpander] TryUnlockSlot: '{slotObject.name}' is already unlocked.");
                return false;
            }
            if (!HasRequiredItems(slot))
            {
                Debug.Log($"[BaseSlotExpander] TryUnlockSlot: '{slotObject.name}' failed affordability check ({GetRequirementsText(slotObject)}).");
                return false;
            }
            if (!ConsumeRequiredItems(slot))
            {
                Debug.LogWarning($"[BaseSlotExpander] TryUnlockSlot: '{slotObject.name}' passed HasRequiredItems but ConsumeRequiredItems failed.");
                return false;
            }

            slot.IsUnlocked = true;
            RefreshSlotVisual(slot);
            TriggerTerrainCleanup(slot);
            int slotIndex = Array.IndexOf(Slots, slot);
            Debug.Log($"[BaseSlotExpander] '{slotObject.name}' (grid {slot.GridCoord}) UNLOCKED - raising GameEvents.OnBaseSlotUnlocked({slotIndex}).");
            GameEvents.RaiseBaseSlotUnlocked(slotIndex);
            return true;
        }


        // Reverts an unlocked slot back to locked and hides its GameObject (SetActive(false) via
        // RefreshSlotVisual) - the night boss's "destroy a base slot" attack (see NightBossAi).
        // No VFX yet, per this session's answer ("deactivate the object so it seems destroyed").
        // No-op if the slot is already locked or unrecognized.
        public void DestroySlot(Transform slotObject)
        {
            BaseSlot slot = Array.Find(Slots, s => s != null && s.SlotObject == slotObject);
            if (slot == null || !slot.IsUnlocked) return;

            slot.IsUnlocked = false;
            RefreshSlotVisual(slot);
            Debug.Log($"[BaseSlotExpander] '{slotObject.name}' was DESTROYED by the night boss - reverted to locked.");
        }


        ///// Getter Methods /////

        // All currently-unlocked slots' transforms - used by NightBossAi to find the nearest
        // slot still standing to target.
        public IEnumerable<Transform> GetUnlockedSlotTransforms()
        {
            foreach (BaseSlot slot in Slots)
            {
                if (slot != null && slot.IsUnlocked && slot.SlotObject != null) yield return slot.SlotObject;
            }
        }

        // Returns the transform of the first slot that is not yet unlocked, or null if all 9 are unlocked
        public Transform GetFirstLockedSlotTransform()
        {
            foreach (BaseSlot slot in Slots)
            {
                if (slot != null && slot.SlotObject != null && !slot.IsUnlocked) return slot.SlotObject;
            }
            return null;
        }

        // Read-only affordability check for the UI to gate a click on, without spending
        // anything - a slot with no requirements is always affordable (free).
        public bool CanAffordSlot(Transform slotObject)
        {
            BaseSlot slot = Array.Find(Slots, s => s != null && s.SlotObject == slotObject);
            return slot != null && HasRequiredItems(slot);
        }

        // Whether the given slot (matched by its SlotObject transform) is currently unlocked.
        // Used by BaseSlotWall to decide when a wall segment bordering this slot can hide.
        public bool IsSlotUnlocked(Transform slotObject)
        {
            BaseSlot slot = Array.Find(Slots, s => s != null && s.SlotObject == slotObject);
            return slot != null && slot.IsUnlocked;
        }

        // Finds the slot sitting at the given grid coordinate (e.g. a neighbor one step over
        // in some compass direction), or null if nothing occupies that coordinate - either
        // because it's off the 3x3 grid entirely (a perimeter/map-edge direction) or just not
        // wired up. Used by BaseSlotWall to resolve which neighbor slot a wall segment borders.
        public Transform GetSlotAtGridCoord(Vector2Int gridCoord)
        {
            BaseSlot slot = Array.Find(Slots, s => s != null && s.SlotObject != null && s.GridCoord == gridCoord);
            return slot?.SlotObject;
        }

        // Human-readable "3 Wood, 3 Rock" (plus "Clear the wizard's miasma quest" if that's also
        // required) cost text for a slot, or "" if it has neither.
        public string GetRequirementsText(Transform slotObject)
        {
            BaseSlot slot = Array.Find(Slots, s => s != null && s.SlotObject == slotObject);
            if (slot == null) return "";

            List<string> parts = new();
            if (slot.RequiresWizardQuestCompleted) parts.Add("Clear the wizard's miasma quest");
            if (slot.UnlockRequirements != null) parts.AddRange(slot.UnlockRequirements.ConvertAll(r => $"{r.Quantity} {r.ItemName}"));

            return string.Join(", ", parts);
        }


        ///// Action Methods /////

        // A slot with no requirements set unlocks for free. RequiresWizardQuestCompleted gates
        // the slot on top of (or instead of) any item cost - a slot with that flag set can't be
        // bought until GameEvents.OnWizardQuestCompleted has fired, regardless of inventory.
        // Otherwise checks the combined inventory + hand pool (see ResourceSources) has enough
        // of every required item, wherever the player is currently holding it.
        private bool HasRequiredItems(BaseSlot slot)
        {
            if (slot.RequiresWizardQuestCompleted && !_wizardQuestCompleted) return false;
            if (slot.UnlockRequirements == null || slot.UnlockRequirements.Count == 0) return true;

            foreach (ItemRequirement requirement in slot.UnlockRequirements)
            {
                if (!ResourcePool.HasItems(ResourceSources, requirement.ItemName, requirement.Quantity)) return false;
            }
            return true;
        }

        // Spends every required item from the combined inventory + hand pool. Only
        // called after HasRequiredItems already confirmed there's enough of everything.
        private bool ConsumeRequiredItems(BaseSlot slot)
        {
            if (slot.UnlockRequirements == null || slot.UnlockRequirements.Count == 0) return true;

            foreach (ItemRequirement requirement in slot.UnlockRequirements)
            {
                if (!ResourcePool.TryRemoveItems(ResourceSources, requirement.ItemName, requirement.Quantity)) return false;
                ResourceEventLog.LogToDrain(requirement.ItemName, requirement.Quantity, slot.SlotObject.name);
            }
            return true;
        }

        private void RefreshSlotVisual(BaseSlot slot)
        {
            if (slot == null || slot.SlotObject == null) return;
            slot.SlotObject.gameObject.SetActive(slot.IsUnlocked);

            // Adds (or resets) the slot's StructureHealth every time it becomes unlocked, so a
            // slot that was destroyed and later re-bought (DestroySlot reverts IsUnlocked to
            // false, letting TryUnlockSlot succeed on it again) starts back at full health
            // rather than remembering damage from its previous life.
            if (slot.IsUnlocked)
            {
                StructureHealth health = slot.SlotObject.GetComponent<StructureHealth>();
                if (health == null) health = slot.SlotObject.gameObject.AddComponent<StructureHealth>();

                Transform slotObject = slot.SlotObject;
                health.OnDepleted = () => DestroySlot(slotObject);
                health.Initialize(SlotMaxHits);
            }
        }

        // Clears any painted trees/grass around the slot, if it has an AdjustTerrainOnSlot component
        private void TriggerTerrainCleanup(BaseSlot slot)
        {
            AdjustTerrainOnSlot terrainAdjuster = slot.SlotObject.GetComponent<AdjustTerrainOnSlot>();
            if (terrainAdjuster != null) terrainAdjuster.ClearTerrainAroundSlot();
        }
    }
}

// Implementation Steps:
// 1. Attach this script to the "BaseSlots" parent GameObject in the scene.
// 2. In the Inspector, expand "Slots" (size 9) and drag each of the 9 slot GameObjects
//    (the "centerOrientBase..." children under BaseSlots) into SlotObject, in the order
//    you want them to unlock. Leave IsUnlocked unchecked; fill in UnlockRequirements
//    per slot with the item name(s) (must match ItemDefinition.ItemName in ItemCatalog,
//    e.g. "Wood", "Rock") and quantities that slot should cost - leave the list empty
//    for a free slot.
// 3. UInavigator wires Inventory automatically at Play (no manual setup needed).
//    UIPromptActionHub finds this component automatically and, when clicked, calls
//    TryUnlockSlot on GetFirstLockedSlotTransform() - the next slot still waiting to be
//    unlocked - which now actually checks and spends real inventory items.
// 4. If a slot's GameObject also has an AdjustTerrainOnSlot component, TryUnlockSlot automatically
//    calls its ClearTerrainAroundSlot() to clear painted trees/grass around that slot on unlock.
// 5. Slot 0 in this scene is currently set up with a 3 Wood + 3 Rock test requirement
//    (see ResourceEconomyDesignNotes.txt) - edit or clear it in the Inspector as needed.
// 6. Fill in each slot's GridCoord to match the (X,Z) in its "centerOrientBase (X,Z)" name
//    (e.g. centerOrientBase (0,-50) -> GridCoord x=0, y=-50) - BaseSlotWallSpawner/BaseSlotWall
//    need this to find neighbor slots for the wall toggle logic (see GameplayRoadmap.md
//    Section 4).
