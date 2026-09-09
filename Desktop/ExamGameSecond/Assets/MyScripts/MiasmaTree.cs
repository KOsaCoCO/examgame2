using System.Collections;
using UnityEngine;

// Makes "clearing the miasma" concrete: the Redish Fog particle system already sitting as a
// child of this GameObject in the scene IS the miasma - felling this tree deactivates the
// whole GameObject, which takes the fog down with it for free (a disabled parent disables
// its children too), no separate fog-toggle code needed. Requires SW
// (ToolSubCategory.MiasmaCutting, bought from the wizard - see TradeManager's offers) armed
// in a hand slot instead of the regular Axe, same tool-gating shape as HarvestableResource/
// MobHealth. Grants one Miasma Stick on the final hit only (not per hit, unlike
// HarvestableResource - there's nothing to repeatedly harvest here) and raises
// GameEvents.OnMiasmaCleared so WizardNpcBehavior can unlock its turn-in dialogue.
[RequireComponent(typeof(Collider))]
public class MiasmaTree : MonoBehaviour
{
    [Tooltip("Total hits (with SW armed) needed to fell the tree.")]
    public int hitsRequired = 10;

    [Header("Hit Feedback")]
    [Tooltip("Scale at the peak of the bounce - 1.15 = 15% bigger.")]
    public float bounceScale = 1.15f;
    [Tooltip("Total bounce duration (up and back down), in seconds.")]
    public float bounceDuration = 0.15f;

    [Header("Highlight FX")]
    [Tooltip("Optional FX object (placed by hand, e.g. near/around this tree) that activates the moment SW is bought from the wizard - pointing the player at what to chop - and deactivates the moment the tree is actually felled. Left unassigned = no-op.")]
    public GameObject highlightFx;

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
        // Include inactive hierarchies, same reasoning as HarvestableResource/MobHealth -
        // InventoryManager/PlayerHandManager can be found mid-startup before their canvases
        // are shown.
        inventory = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
        hand = FindAnyObjectByType<PlayerHandManager>(FindObjectsInactive.Include);

        hotspot = GetComponent<InteractableHotspot>();
        if (hotspot == null) hotspot = gameObject.AddComponent<InteractableHotspot>();
        hotspot.OnInteract.AddListener(HandleHit);
        hotspot.OnSecondaryInteract.AddListener(HandleHit);
    }

    void OnDestroy()
    {
        if (hotspot == null) return;
        hotspot.OnInteract.RemoveListener(HandleHit);
        hotspot.OnSecondaryInteract.RemoveListener(HandleHit);
    }

    void OnEnable()
    {
        GameEvents.OnItemTraded += HandleItemTraded;
    }

    void OnDisable()
    {
        GameEvents.OnItemTraded -= HandleItemTraded;
    }

    // SW is the tool that fells this tree (see HandleHit's armed-tool check below) - the
    // moment it's bought, point the player at what to actually use it on.
    private void HandleItemTraded(string itemName, int quantity)
    {
        if (itemName != "SW" || highlightFx == null) return;
        highlightFx.SetActive(true);
    }

    // E or right-click while in range, same dual-binding as HarvestableResource/MobHealth.
    // Silently does nothing without SW armed - no rejection UI yet, same "test pass minimal"
    // convention as the rest of this interaction family.
    private void HandleHit()
    {
        bool armed = hand != null && hand.IsArmedWithTool(ToolSubCategory.MiasmaCutting);
        if (inventory == null || !armed) return;

        hitsRemaining--;

        if (hitsRemaining > 0)
        {
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            bounceCoroutine = StartCoroutine(Bounce());
            return;
        }

        inventory.AddItem(ItemCatalog.GetByName("Miasma Stick"), 1);
        GameEvents.RaiseMiasmaCleared();
        if (highlightFx != null) highlightFx.SetActive(false);
        gameObject.SetActive(false); // takes the child Redish Fog down with it - that's the point
    }

    // Quick scale pulse (up then back down) so a hit reads as a hit - identical to
    // HarvestableResource.Bounce.
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

// Implementation Steps:
// 1. Add this component to the "tree_1 Big" GameObject (the manually-placed decorative tree
//    under Environment that the Redish Fog is parented to - it already has an "Interactive"
//    tag and a trigger CapsuleCollider from earlier session work, so RequireComponent<Collider>
//    is already satisfied and no new proximity trigger needs sizing).
// 2. Tune hitsRequired if 10 needs adjusting.
// 3. Make sure SW (ToolSubCategory.MiasmaCutting) is armed in a hand slot - hits are silently
//    ignored otherwise. SW is bought from the wizard's Trade shop for Skin/Bone/Meat/Special
//    Item (see TradeManager.offers) - all four only obtainable by hunting mobs (MobHealth).
// 4. Once felled, the tree deactivates (taking the fog with it), the player gets a Miasma
//    Stick, and GameEvents.OnMiasmaCleared fires - WizardNpcBehavior listens for this and
//    unlocks its turn-in dialogue next time the player wanders back into his (now local-roam)
//    range. The Miasma Stick itself trades for a Magic Stick at the same wizard shop.
