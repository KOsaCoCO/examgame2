using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// The 28-slot inventory. Attach to the "PanelInventory" GameObject (done
// automatically by UInavigator) - it finds its own InventoryIcon children and
// the ObjectDescription(TMP) label, so nothing needs wiring in the Inspector.
public class InventoryManager : MonoBehaviour, IResourceContainer
{
    public const int SlotCount = 28;

    [HideInInspector] public ItemPlacementTracker Tracker;

    private readonly InventorySlotData[] slots = CreateEmptySlots();
    private readonly InventorySlotUI[] slotUIs = new InventorySlotUI[SlotCount];
    private TMP_Text descriptionText;

    // Which slot the mouse currently sits over (see InventorySlotUI's IPointerEnter/Exit) -
    // -1 means none. Drives the "R to eat Food" hotkey below; only ever meaningful while this
    // panel is actually visible, since hover events can't fire otherwise.
    private int hoveredSlotIndex = -1;

    // Field initializer (not Awake) so the slot data always exists, even if this
    // context-menu debug helper gets clicked in Edit mode before Play starts.
    private static InventorySlotData[] CreateEmptySlots()
    {
        InventorySlotData[] emptySlots = new InventorySlotData[SlotCount];
        for (int i = 0; i < SlotCount; i++) emptySlots[i] = new InventorySlotData();
        return emptySlots;
    }

    void Awake()
    {
        int found = 0;
        for (int i = 0; i < transform.childCount && found < SlotCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith("InventoryIcon")) continue;

            InventorySlotUI slotUI = child.GetComponent<InventorySlotUI>();
            if (slotUI == null) slotUI = child.gameObject.AddComponent<InventorySlotUI>();

            slotUI.Init(found, this);
            slotUIs[found] = slotUI;
            found++;
        }

        GameObject descriptionObject = GameObject.Find("ObjectDescription(TMP)");
        if (descriptionObject != null) descriptionText = descriptionObject.GetComponent<TMP_Text>();

        GiveStartingTools();
    }

    void Update()
    {
        if (hoveredSlotIndex < 0) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.rKey.wasPressedThisFrame) return;

        Tracker?.HandleInventoryConsumeHotkey(hoveredSlotIndex);
    }

    // Called by InventorySlotUI's IPointerEnter/Exit - tracks which slot (if any) the mouse
    // currently sits over, for the R-to-eat hotkey above.
    public void SetHoveredSlot(int index) => hoveredSlotIndex = index;

    public void ClearHoveredSlot(int index)
    {
        if (hoveredSlotIndex == index) hoveredSlotIndex = -1;
    }

    // Every new game starts with these two - Wood/Rock are meant to be gathered in-world
    // (see HarvestableResource) instead, so only the tools themselves are granted directly
    // here. Runs for real now (used to be Debug: Add Test Items only, manually triggered).
    private void GiveStartingTools()
    {
        AddItem(ItemCatalog.GetByName("Axe"), 1);
        AddItem(ItemCatalog.GetByName("Pickaxe"), 1);
        AddItem(ItemCatalog.GetByName("Sword"), 1);
    }

    // Adds an item to the first matching stack(s), then into empty slots. This
    // either fully succeeds or changes nothing at all (see StackingHelper) -
    // never adds part of the amount and reports failure, which would
    // duplicate/lose items for whichever caller decides whether to remove the
    // source based on this return value.
    public bool AddItem(ItemDefinition item, int amount = 1, string variant = null)
    {
        if (!StackingHelper.TryPlace(slots, item, amount, RefreshSlot, variant))
        {
            Debug.LogWarning($"Inventory full - could not fit {amount}x {item?.ItemName}");
            return false;
        }

        ResourceEventLog.LogToInventory(item.ItemName, amount);
        return true;
    }

    // Total quantity of the named item currently held across every slot.
    public int CountItems(string itemName)
    {
        int total = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            if (!slots[i].IsEmpty && slots[i].Item.ItemName == itemName) total += slots[i].Quantity;
        }
        return total;
    }

    public bool HasItems(string itemName, int quantity) => CountItems(itemName) >= quantity;

    // Removes 'quantity' total units of the named item, spread across as many slots
    // as needed. Returns false (and removes nothing) if the inventory doesn't have enough.
    public bool TryRemoveItems(string itemName, int quantity)
    {
        if (!HasItems(itemName, quantity)) return false;

        int remaining = quantity;
        for (int i = 0; i < SlotCount && remaining > 0; i++)
        {
            if (slots[i].IsEmpty || slots[i].Item.ItemName != itemName) continue;

            int take = Mathf.Min(slots[i].Quantity, remaining);
            RemoveFromSlot(i, take);
            remaining -= take;
        }

        return true;
    }

    public void OnSlotClicked(int index)
    {
        if (descriptionText == null) return;

        InventorySlotData data = slots[index];
        descriptionText.text = data.IsEmpty ? "" : $"{data.Item.ItemName} ({data.Item.Category})";
    }

    public void OnSlotDoubleClicked(int index)
    {
        Tracker?.HandleInventoryDoubleClick(index, InventoryHotkeys.MoveWholeStackHeld());
    }

    public InventorySlotData GetSlot(int index) => slots[index];

    // Removes 'amount' units from a slot (used when an item moves to the hand
    // or a boost slot). Only ItemPlacementTracker should call this.
    public void RemoveFromSlot(int index, int amount)
    {
        InventorySlotData data = slots[index];
        if (data.IsEmpty) return;

        data.Quantity -= amount;
        if (data.Quantity <= 0)
        {
            data.Item = null;
            data.IconVariant = null;
        }
        RefreshSlot(index);
    }

    // Manual re-trigger for testing (e.g. after dropping/losing starting tools mid-session)
    // - GiveStartingTools already runs for real in Awake, this just re-runs the same thing.
    [ContextMenu("Debug: Add Test Items")]
    private void DebugAddTestItems() => GiveStartingTools();

    private void RefreshSlot(int index)
    {
        if (slotUIs[index] == null) return;

        InventorySlotData data = slots[index];
        slotUIs[index].SetDisplay(data.IsEmpty ? null : data.Item, data.Quantity, data.IconVariant);
    }
}
