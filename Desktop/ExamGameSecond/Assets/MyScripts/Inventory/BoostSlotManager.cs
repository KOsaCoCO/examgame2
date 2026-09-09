using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The 4 armor "boost" slots next to the player preview. Attach to the
// "player view in inventory" GameObject (done automatically by UInavigator) -
// it finds its own PanelBoost/PanelBooost1/PanelBoost2/PanelBoost3 children.
public class BoostSlotManager : MonoBehaviour
{
    [HideInInspector] public ItemPlacementTracker Tracker;

    private readonly Dictionary<ArmorSlotType, InventorySlotData> slots = new()
    {
        { ArmorSlotType.Helmet, new InventorySlotData() },
        { ArmorSlotType.Chestplate, new InventorySlotData() },
        { ArmorSlotType.Boots, new InventorySlotData() },
        { ArmorSlotType.Pants, new InventorySlotData() },
    };

    private readonly Dictionary<ArmorSlotType, BoostSlotUI> slotUIs = new();

    // Matches this scene's existing boost panel names to the armor type they hold.
    private static readonly (string GameObjectName, ArmorSlotType Type)[] SlotMap =
    {
        ("PanelBoost2", ArmorSlotType.Helmet),
        ("PanelBoost", ArmorSlotType.Chestplate),
        ("PanelBoost3", ArmorSlotType.Boots),
        ("PanelBooost1", ArmorSlotType.Pants),
    };

    void Awake()
    {
        foreach ((string gameObjectName, ArmorSlotType type) in SlotMap)
        {
            Transform slotTransform = transform.Find(gameObjectName);
            if (slotTransform == null)
            {
                Debug.LogWarning($"BoostSlotManager could not find boost panel '{gameObjectName}'.");
                continue;
            }

            BoostSlotUI slotUI = slotTransform.GetComponent<BoostSlotUI>();
            if (slotUI == null) slotUI = slotTransform.gameObject.AddComponent<BoostSlotUI>();

            TMP_Text label = slotTransform.GetComponentInChildren<TMP_Text>(true);
            if (label == null) label = CreateLabel(slotTransform);

            Image icon = ItemIconRenderer.FindOrCreateIcon(slotTransform);
            Image iconTint = ItemIconRenderer.FindOrCreateIconTint(slotTransform);

            slotUI.Init(type, this, label, icon, iconTint);
            slotUIs[type] = slotUI;
        }
    }

    public InventorySlotData GetSlot(ArmorSlotType type) => slots[type];

    // Called by ItemPlacementTracker. Returns true if the item was placed
    // (matching, empty slot).
    public bool TryPlace(ArmorSlotType type, ItemDefinition item)
    {
        if (type == ArmorSlotType.None || !slots.TryGetValue(type, out InventorySlotData slot) || !slot.IsEmpty) return false;

        slot.Item = item;
        slot.Quantity = 1;
        RefreshSlot(type);
        return true;
    }

    public void Clear(ArmorSlotType type)
    {
        if (!slots.TryGetValue(type, out InventorySlotData slot)) return;

        slot.Item = null;
        slot.Quantity = 0;
        RefreshSlot(type);
    }

    // Called when a boost slot is double-clicked - asks the tracker to send
    // the item back to the inventory.
    public void OnSlotDoubleClicked(ArmorSlotType type)
    {
        Tracker?.HandleBoostDoubleClick(type);
    }

    private void RefreshSlot(ArmorSlotType type)
    {
        if (!slotUIs.TryGetValue(type, out BoostSlotUI ui) || ui == null) return;

        InventorySlotData data = slots[type];
        ui.SetDisplay(data.IsEmpty ? null : data.Item, data.Quantity, data.IconVariant);
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
