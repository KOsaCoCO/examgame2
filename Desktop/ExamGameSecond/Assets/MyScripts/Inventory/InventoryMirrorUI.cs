using UnityEngine;

// Read-only-in-spirit live mirror of the player's real inventory, shown in a side panel (e.g.
// the Trade window's left half) so the player doesn't have to keep switching back to their own
// inventory panel to see what they're holding. Clones an existing InventoryIcon template 28
// times at Start, then polls the real InventoryManager every frame (only while this GameObject -
// a child of whatever panel it lives on - is actually active, e.g. only while the shop is open)
// instead of wiring a change-broadcast through InventoryManager itself, keeping that core class
// untouched. Because each clone is Init()'d with the real InventoryManager and the real slot
// index, clicking a mirrored slot behaves exactly like clicking the real one (e.g. double-click
// to arm it in hand) - a side effect of reusing the same InventorySlotUI, not extra code.
public class InventoryMirrorUI : MonoBehaviour
{
    [Tooltip("Name of the real inventory panel to clone a slot template from and read live data out of.")]
    public string inventoryPanelName = "PanelInventory";

    private InventoryManager inventoryManager;
    private InventorySlotUI[] mirrorSlots;

    void Start()
    {
        inventoryManager = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
        GameObject panel = GameObject.Find(inventoryPanelName);
        if (inventoryManager == null || panel == null || panel.transform.childCount == 0)
        {
            Debug.LogWarning($"InventoryMirrorUI on {name}: could not find '{inventoryPanelName}' (or it has no children) to clone slots from.");
            return;
        }

        Transform template = panel.transform.GetChild(0);
        mirrorSlots = new InventorySlotUI[InventoryManager.SlotCount];
        for (int i = 0; i < InventoryManager.SlotCount; i++)
        {
            GameObject clone = Instantiate(template.gameObject, transform);
            clone.name = $"InventoryIconMirror ({i})";

            InventorySlotUI slotUI = clone.GetComponent<InventorySlotUI>();
            if (slotUI == null) slotUI = clone.AddComponent<InventorySlotUI>();
            slotUI.Init(i, inventoryManager);
            mirrorSlots[i] = slotUI;
        }
    }

    void Update()
    {
        if (inventoryManager == null || mirrorSlots == null) return;

        for (int i = 0; i < mirrorSlots.Length; i++)
        {
            InventorySlotData data = inventoryManager.GetSlot(i);
            mirrorSlots[i].SetDisplay(data.IsEmpty ? null : data.Item, data.Quantity, data.IconVariant);
        }
    }
}

// Implementation Steps:
// 1. Add this component to a GameObject under the panel it should mirror into (e.g. a new
//    "PanelInventoryMirror" placed in the Trade window's empty left half) - it clones its slot
//    template as direct children of whatever it's attached to, so size/position that GameObject
//    the way the mirrored grid should actually appear.
// 2. Nothing else to wire - inventoryPanelName defaults to "PanelInventory" (the real inventory
//    grid) and it finds InventoryManager itself.
