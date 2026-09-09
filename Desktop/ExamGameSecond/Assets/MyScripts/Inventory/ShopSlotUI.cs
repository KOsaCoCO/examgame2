using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Attached at runtime (by TradeManager) to each InventoryIconT slot under PanelInventoryT -
// same shape as InventorySlotUI, single click shows this offer's required items in the
// shared description label, double click attempts to buy it (shows "Not enough resources"
// there instead if it fails).
public class ShopSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("How large the icon grows at the peak of the purchase pulse (1 = no change).")]
    public float pulseScale = 1.3f;
    [Tooltip("Total seconds the purchase pulse takes, up and back down combined.")]
    public float pulseDuration = 0.25f;

    private int offerIndex;
    private TradeManager manager;
    private TMP_Text label;
    private Image icon;
    private Image iconTint;
    private RectTransform iconRect;
    private Vector3 baseIconScale;
    private Coroutine pulseCoroutine;

    public void Init(int index, TradeManager owner)
    {
        offerIndex = index;
        manager = owner;
        label = GetComponentInChildren<TMP_Text>(true);
        icon = ItemIconRenderer.FindOrCreateIcon(transform);
        iconTint = ItemIconRenderer.FindOrCreateIconTint(transform);
        iconRect = icon.rectTransform;
        baseIconScale = iconRect.localScale;
    }

    public void SetDisplay(ItemDefinition item, int quantity)
    {
        ItemIconRenderer.Apply(label, icon, iconTint, item, quantity);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager == null) return;

        if (eventData.clickCount == 2) manager.OnOfferDoubleClicked(offerIndex);
        else manager.OnOfferClicked(offerIndex);
    }

    // Called by TradeManager right after a successful purchase - a quick "pop" (up then back
    // down once) on the icon so the purchase reads as having actually happened, same up-then-
    // back shape HarvestableResource/MiasmaTree use for a hit landing.
    public void PlayPurchasePulse()
    {
        if (iconRect == null) return;
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(Pulse());
    }

    private IEnumerator Pulse()
    {
        Vector3 peakScale = baseIconScale * pulseScale;
        float half = pulseDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            iconRect.localScale = Vector3.Lerp(baseIconScale, peakScale, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            iconRect.localScale = Vector3.Lerp(peakScale, baseIconScale, t / half);
            yield return null;
        }

        iconRect.localScale = baseIconScale;
        pulseCoroutine = null;
    }
}
