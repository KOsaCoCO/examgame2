using System.Collections;
using UnityEngine;

// Attach (or auto-attach, see UInavigator) to any world object that should
// give a resource when hit with the right tool - chop a tree for Wood today,
// same component reusable later for e.g. mining a rock node for Rock. Both E
// and right-click count as a hit (via InteractableHotspot.OnInteract and
// OnSecondaryInteract, in range only) - temporarily wired to both while
// diagnosing whether right-click alone was failing because the cursor is
// locked for the mouse-look camera; drop the OnInteract listener once that's
// confirmed one way or the other. A hit counts if the player has the required
// tool armed - selected via hand slot hotkeys 1-4 (PlayerHandManager.
// IsArmedWithTool), not just held anywhere in hand; once hitsRequired is
// reached, this object deactivates - not destroyed, same "hidden but not
// deleted" pattern as an unlocked base slot - and stops being interactable (a
// disabled GameObject's Collider can't fire trigger events).
[RequireComponent(typeof(Collider))]
public class HarvestableResource : MonoBehaviour
{
    [Tooltip("Item name (must match ItemCatalog.All) granted per hit.")]
    public string resourceItemName = "Wood";
    [Tooltip("Total hits needed before this object is spent.")]
    public int hitsRequired = 5;
    [Tooltip("How much of the resource one hit grants.")]
    public int amountPerHit = 1;
    [Tooltip("Tool subcategory the player needs in a hand slot to land a hit.")]
    public ToolSubCategory requiredTool = ToolSubCategory.Cutting;

    [Header("Hit Feedback")]
    [Tooltip("Scale at the peak of the bounce - 1.15 = 15% bigger.")]
    public float bounceScale = 1.15f;
    [Tooltip("Total bounce duration (up and back down), in seconds.")]
    public float bounceDuration = 0.15f;

    private InventoryManager inventory;
    private PlayerHandManager hand;
    private InteractableHotspot hotspot;
    private int hitsRemaining;
    private Vector3 baseScale;
    private Coroutine bounceCoroutine;

    void Awake()
    {
        hitsRemaining = hitsRequired;
        baseScale = transform.localScale;
    }

    void Start()
    {
        // Include inactive hierarchies - PanelInventory (InventoryManager's
        // GameObject) is a descendant of the UI-Inventory canvas, which
        // UInavigator hides right after creating this component (via
        // AddComponent, deferring this Start() until later) - the default
        // "active objects only" search would otherwise always come back null.
        inventory = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
        hand = FindAnyObjectByType<PlayerHandManager>(FindObjectsInactive.Include);

        hotspot = GetComponent<InteractableHotspot>();
        if (hotspot == null) hotspot = gameObject.AddComponent<InteractableHotspot>();
        hotspot.OnInteract.AddListener(HandleHit);
        hotspot.OnSecondaryInteract.AddListener(HandleHit);

        Debug.Log($"[Harvest] '{name}' ready - inventory found={inventory != null}, hand found={hand != null}, hitsRequired={hitsRequired}.");
    }

    void OnDestroy()
    {
        if (hotspot == null) return;
        hotspot.OnInteract.RemoveListener(HandleHit);
        hotspot.OnSecondaryInteract.RemoveListener(HandleHit);
    }

    // E or right-click while in range. Silently does nothing without the required
    // tool armed (selected via hand slot hotkeys 1-4) - no rejection UI yet, keeping
    // this test pass minimal.
    private void HandleHit()
    {
        bool armed = hand != null && hand.IsArmedWithTool(requiredTool);
        Debug.Log($"[Harvest] Hit attempt on '{name}': inventory={inventory != null}, hand={hand != null}, armedWith{requiredTool}={armed}, hitsRemaining={hitsRemaining}.");

        if (inventory == null || !armed) return;

        inventory.AddItem(ItemCatalog.GetByName(resourceItemName), amountPerHit);
        if (resourceItemName == "Wood" || resourceItemName == "Rock") PlayerLevel.Instance?.AddExperience(1);
        hitsRemaining--;

        if (requiredTool == ToolSubCategory.Cutting) HarvestSoundEffects.Instance?.PlayChop(transform.position);
        else if (requiredTool == ToolSubCategory.Mining) HarvestSoundEffects.Instance?.PlayMine(transform.position);

        Debug.Log($"[Harvest] Hit landed on '{name}' - granted {amountPerHit}x {resourceItemName}, {hitsRemaining} hit(s) remaining.");

        if (hitsRemaining <= 0)
        {
            Debug.Log($"[Harvest] '{name}' fully harvested - deactivating.");
            gameObject.SetActive(false);
            return;
        }

        if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
        bounceCoroutine = StartCoroutine(Bounce());
    }

    // Quick scale pulse (up then back down) so a hit reads as a hit, not deep
    // enough to matter for the colliders during the brief moment it plays.
    private IEnumerator Bounce()
    {
        Vector3 peakScale = baseScale * bounceScale;
        float half = bounceDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(baseScale, peakScale, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(peakScale, baseScale, t / half);
            yield return null;
        }

        transform.localScale = baseScale;
        bounceCoroutine = null;
    }
}
