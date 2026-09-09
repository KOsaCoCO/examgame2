using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Attached at runtime (by BoostSlotManager) to each of the 4 armor boost panels.
public class BoostSlotUI : MonoBehaviour, IPointerClickHandler
{
    private ArmorSlotType slotType;
    private BoostSlotManager manager;
    private TMP_Text label;
    private Image icon;
    private Image iconTint;

    public void Init(ArmorSlotType type, BoostSlotManager owner, TMP_Text textLabel, Image iconImage, Image iconTintImage)
    {
        slotType = type;
        manager = owner;
        label = textLabel;
        icon = iconImage;
        iconTint = iconTintImage;
    }

    public void SetDisplay(ItemDefinition item, int quantity, string variant = null)
    {
        ItemIconRenderer.Apply(label, icon, iconTint, item, quantity, variant);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager != null && eventData.clickCount == 2) manager.OnSlotDoubleClicked(slotType);
    }
}
