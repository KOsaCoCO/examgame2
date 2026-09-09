using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Attached at runtime (by InventoryManager) to each InventoryIcon slot.
// Shows the item's icon (or placeholder letter/count, see ItemIconRenderer) and reports
// clicks and hover state back to the manager - hover drives the "R to eat Food" hotkey (see
// InventoryManager.Update/ItemPlacementTracker.HandleInventoryConsumeHotkey).
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private int slotIndex;
    private InventoryManager manager;
    private TMP_Text label;
    private Image icon;
    private Image iconTint;

    public void Init(int index, InventoryManager owner)
    {
        slotIndex = index;
        manager = owner;
        label = GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = Color.black;
            label.text = "";
        }
        icon = ItemIconRenderer.FindOrCreateIcon(transform);
        iconTint = ItemIconRenderer.FindOrCreateIconTint(transform);
    }

    public void SetDisplay(ItemDefinition item, int quantity, string variant = null)
    {
        ItemIconRenderer.Apply(label, icon, iconTint, item, quantity, variant);
        if (label != null) label.color = Color.black;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager == null) return;

        if (eventData.clickCount == 2) manager.OnSlotDoubleClicked(slotIndex);
        else manager.OnSlotClicked(slotIndex);
    }

    public void OnPointerEnter(PointerEventData eventData) => manager?.SetHoveredSlot(slotIndex);

    public void OnPointerExit(PointerEventData eventData) => manager?.ClearHoveredSlot(slotIndex);
}
