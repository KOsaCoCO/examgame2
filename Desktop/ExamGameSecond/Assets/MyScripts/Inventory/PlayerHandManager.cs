using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// The 4 hand-tool slots under the "HandTools" panel (UI-Player/HandTools).
// Attach to "HandTools" (done automatically by UInavigator) - it finds its own
// Hand/Hand1/Hand2/Hand3 children, same pattern as BoostSlotManager.
public class PlayerHandManager : MonoBehaviour, IResourceContainer
{
    public const int SlotCount = 4;

    [HideInInspector] public ItemPlacementTracker Tracker;

    private static readonly string[] SlotNames = { "Hand", "Hand1", "Hand2", "Hand3" };

    private readonly InventorySlotData[] slots = CreateEmptySlots();
    private readonly HandSlotUI[] slotUIs = new HandSlotUI[SlotCount];

    // Which hand slot is currently "armed" (equipped/active) via number keys 1-4 -
    // -1 means none. Only the armed slot's tool counts for world interactions like
    // chopping (see IsArmedWithTool, used by HarvestableResource) - just having a
    // matching tool somewhere in hand isn't enough, it has to be the selected one.
    private int armedSlotIndex = -1;

    // Field initializer (not Awake) so the slot data always exists even before Play starts.
    private static InventorySlotData[] CreateEmptySlots()
    {
        InventorySlotData[] emptySlots = new InventorySlotData[SlotCount];
        for (int i = 0; i < SlotCount; i++) emptySlots[i] = new InventorySlotData();
        return emptySlots;
    }

    void Awake()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            Transform slotTransform = transform.Find(SlotNames[i]);
            if (slotTransform == null)
            {
                Debug.LogWarning($"PlayerHandManager could not find hand slot '{SlotNames[i]}'.");
                continue;
            }

            HandSlotUI slotUI = slotTransform.GetComponent<HandSlotUI>();
            if (slotUI == null) slotUI = slotTransform.gameObject.AddComponent<HandSlotUI>();

            TMP_Text label = slotTransform.GetComponentInChildren<TMP_Text>(true);
            if (label == null) label = CreateLabel(slotTransform);

            Image slotImage = slotTransform.GetComponent<Image>();
            Image icon = ItemIconRenderer.FindOrCreateIcon(slotTransform);
            Image iconTint = ItemIconRenderer.FindOrCreateIconTint(slotTransform);

            slotUI.Init(i, this, label, slotImage, icon, iconTint);
            slotUIs[i] = slotUI;
        }
    }

    void Update()
    {
        int pressed = InventoryHotkeys.GetPressedHandSlotIndex();
        if (pressed >= 0) ArmSlot(pressed);
    }

    public InventorySlotData GetSlot(int index) => slots[index];

    // Arms (equips) a hand slot via number keys 1-4 - only one slot is armed at
    // a time. Highlights the newly armed slot and un-highlights the previous one.
    public void ArmSlot(int index)
    {
        if (index == armedSlotIndex) return;

        if (armedSlotIndex >= 0 && slotUIs[armedSlotIndex] != null) slotUIs[armedSlotIndex].SetArmed(false);
        armedSlotIndex = index;
        if (slotUIs[armedSlotIndex] != null) slotUIs[armedSlotIndex].SetArmed(true);
    }

    // Whether the currently armed slot holds a tool of the given subcategory -
    // what world interactions (e.g. HarvestableResource) should check, rather
    // than just scanning all 4 hand slots for a match.
    public bool IsArmedWithTool(ToolSubCategory subCategory)
    {
        if (armedSlotIndex < 0) return false;

        InventorySlotData data = slots[armedSlotIndex];
        return !data.IsEmpty && data.Item.ToolSubCategory == subCategory;
    }

    // Same shape as IsArmedWithTool, keyed on ItemName directly instead of ToolSubCategory -
    // for items like Magic Stick that aren't a Tool/ToolSubCategory at all, just a plain held
    // item with its own E-press behaviour (see MagicStickAbility).
    public bool IsArmedWithItem(string itemName)
    {
        if (armedSlotIndex < 0) return false;

        InventorySlotData data = slots[armedSlotIndex];
        return !data.IsEmpty && data.Item.ItemName == itemName;
    }

    // Index of the first hand slot currently holding the named item, or -1 if none does.
    public int FindSlotIndex(string itemName)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (!slots[i].IsEmpty && slots[i].Item.ItemName == itemName) return i;
        }
        return -1;
    }

    // Live-updates a slot's icon variant (e.g. Magic Stick's cooldown red tint, see
    // MagicStickAbility) and refreshes its display immediately - unlike normal variant tagging
    // (set once when an item is first added, e.g. Bone's drop-tier), this is meant to be called
    // repeatedly by gameplay code as ability/cooldown state changes over time.
    public void SetSlotVariant(int index, string variant)
    {
        if (index < 0 || index >= SlotCount || slots[index].IsEmpty) return;

        slots[index].IconVariant = variant;
        RefreshSlot(index);
    }

    // Called by ItemPlacementTracker. Stacks onto a matching hand slot first
    // (e.g. moving more Wood in when a hand slot already holds some), then
    // falls back to an empty slot - see StackingHelper. Either fully succeeds
    // or changes nothing.
    public bool TryPlaceItem(ItemDefinition item, int quantity = 1, string variant = null)
    {
        return StackingHelper.TryPlace(slots, item, quantity, RefreshSlot, variant);
    }

    // Removes 'amount' units from a hand slot (used when an item moves back
    // to the inventory). Only ItemPlacementTracker should call this.
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

    // Total quantity of the named item currently held across all 4 hand slots.
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

    // Removes 'quantity' total units of the named item, spread across as many hand
    // slots as needed. Returns false (and removes nothing) if there isn't enough.
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

    // Called when a hand slot itself is double-clicked - asks the tracker
    // to send the item back to the inventory.
    public void OnSlotDoubleClicked(int index)
    {
        Tracker?.HandleHandDoubleClick(index, InventoryHotkeys.MoveWholeStackHeld());
    }

    private void RefreshSlot(int index)
    {
        if (slotUIs[index] == null) return;

        InventorySlotData data = slots[index];
        slotUIs[index].SetDisplay(data.IsEmpty ? null : data.Item, data.Quantity, data.IconVariant);
    }

    private TMP_Text CreateLabel(Transform parent)
    {
        GameObject textObject = new("Label", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 36;
        label.color = Color.black;
        label.text = "";
        // Only the slot's own Image should be a raycast target - a raycastable
        // label on top of it would otherwise be a second, redundant click target.
        label.raycastTarget = false;
        return label;
    }
}

// Attached at runtime (by PlayerHandManager) to each of the 4 hand-tool slots.
internal class HandSlotUI : MonoBehaviour, IPointerClickHandler
{
    // Gentle yellow/orange tint shown on the slot's box while it's armed.
    private static readonly Color ArmedColor = new(1f, 0.7f, 0.25f, 0.6f);

    private int slotIndex;
    private PlayerHandManager manager;
    private TMP_Text label;
    private Image slotImage;
    private Image icon;
    private Image iconTint;
    private Color defaultColor;

    public void Init(int index, PlayerHandManager owner, TMP_Text textLabel, Image image, Image iconImage, Image iconTintImage)
    {
        slotIndex = index;
        manager = owner;
        label = textLabel;
        slotImage = image;
        icon = iconImage;
        iconTint = iconTintImage;
        if (slotImage != null) defaultColor = slotImage.color;
    }

    public void SetDisplay(ItemDefinition item, int quantity, string variant = null)
    {
        ItemIconRenderer.Apply(label, icon, iconTint, item, quantity, variant);
    }

    public void SetArmed(bool armed)
    {
        if (slotImage != null) slotImage.color = armed ? ArmedColor : defaultColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager != null && eventData.clickCount == 2) manager.OnSlotDoubleClicked(slotIndex);
    }
}
