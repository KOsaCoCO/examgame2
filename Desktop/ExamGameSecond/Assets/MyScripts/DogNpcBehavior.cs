using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Handles finding and picking up the roaming Dog NPC. Reuses NPCai's existing
// InteractableHotspot the same way GirlNpcBehavior does, but "talking" to the dog just
// means picking it up (E, in range) rather than opening a conversation - no dialogue
// panel of its own, just a center-screen prompt while in range. Lives on its own object
// in the scene (same convention as GirlNpcBehavior originally used) rather than on the
// dog prefab itself, since it should only ever bind to the one quest-phase dog (see
// NPCManager) - once picked up, PickUp itself stays unbound for good (see CompletePickup),
// replaced there by HandleCompanionGreeted - a placeholder "turn to face the player, then
// go back to ignoring them" idle reaction for the post-quest companion dog that reappears
// once the quest is turned in. No dialogue panel, no mechanical effect - same "outside the
// quest" small-talk spirit as GirlNpcBehavior's.
public class DogNpcBehavior : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Name of the spawned dog NPC to attach this pickup behavior to - matches NPCManager's npcLabel.")]
    public string dogObjectName = "Dog";
    [Tooltip("Item added to the inventory on pickup - must exist in ItemCatalog.")]
    public string dogItemName = "Dog";

    [Header("Quest text")]
    [Tooltip("The quest's name while the dog still needs to be found - matches GirlNpcBehavior's QuestName, added when the quest is accepted.")]
    public string dogQuestNameBeforePickup = "Find the girl's lost dog";
    [Tooltip("Renamed to this once the dog is picked up, so the quest UI reflects the next step - GirlNpcBehavior/DogQuestProgress look for this exact name from then on.")]
    public string dogQuestNameAfterPickup = "Bring the dog back to the girl";

    [Header("UI - optional manual override")]
    [Tooltip("Drag your own screen-center TMP object here if you want to style/place it yourself. Leave both blank (the common case) and one is built automatically at Start under Prompt Canvas Name instead - sidesteps Canvas/RectTransform setup mistakes entirely.")]
    public GameObject pickupPromptPanel;
    public TMP_Text pickupPromptText;

    [Header("UI - auto-created prompt (used when the fields above are blank)")]
    [TextArea(1, 2)] public string pickupPromptMessage = "Click E to pick up the dog";
    [Tooltip("Name of an existing Canvas in the scene to build the prompt under - reuses UI-Player, the same parent InteractableHotspot's own working E-Key prompt already lives under.")]
    public string promptCanvasName = "UI-Player";
    public float pickupPromptFontSize = 48f;
    public Color pickupPromptColor = Color.white;

    [Header("Companion Small Talk (placeholder)")]
    [Tooltip("How long (seconds) the post-delivery companion dog stays turned toward the player before resuming its normal roaming and otherwise ignoring them - no dialogue, no mechanical effect.")]
    public float companionAcknowledgeSeconds = 1.5f;

    // Lets external systems (UInavigator's I/Q/Esc cross-panel switching, see
    // GameplayRoadmap.md's UI key-shortcut work) check whether the player is mid pickup-
    // confirm before opening a browsing panel (Inventory/Quest Full View) on top of it -
    // same shape as WizardNpcBehavior.IsConversing/GirlNpcBehavior.IsConversing.
    public bool IsBusy => awaitingPickupConfirm;

    private NPCai dogAi;
    private InteractableHotspot dogHotspot;
    private InventoryManager inventory;
    private PlayerController playerController;
    private bool listenerBound;
    private bool ownsDogBinding;
    private bool awaitingPickupConfirm;
    private bool loggedNoObject;
    private bool loggedNoAi;
    private bool loggedNoHotspot;

    // Same duplicate guard as GirlNpcBehavior.boundGirlObjects - if two DogNpcBehavior
    // instances ever end up in the scene, only the first to bind gets a working pickup
    // flow; the second self-disables instead of duplicating it.
    private static readonly HashSet<string> boundDogObjects = new();

    void Start()
    {
        Debug.Log($"[Dog] DogNpcBehavior.Start() running on '{name}', looking for '{dogObjectName}'.");

        inventory = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
        playerController = FindAnyObjectByType<PlayerController>();

        if (pickupPromptPanel == null) CreatePickupPrompt();
        if (pickupPromptText != null) pickupPromptText.text = pickupPromptMessage;
        SetPromptVisible(false);

        TryBindToDog();
    }

    // Builds a guaranteed-working centered TMP prompt at runtime instead of relying on a
    // hand-built Canvas/RectTransform hierarchy - only runs if Pickup Prompt Panel was left
    // unassigned in the Inspector. Parents under an existing Canvas (promptCanvasName) so
    // no new Canvas/EventSystem setup is needed either.
    private void CreatePickupPrompt()
    {
        GameObject canvasObject = GameObject.Find(promptCanvasName);
        if (canvasObject == null)
        {
            Debug.LogWarning($"DogNpcBehavior: no '{promptCanvasName}' object found - could not auto-create the pickup prompt. Either create one, or drag your own object onto Pickup Prompt Panel/Pickup Prompt Text.");
            return;
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObject.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = canvasObject.GetComponentInChildren<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning($"DogNpcBehavior: '{promptCanvasName}' has no Canvas on it (or nearby) - could not auto-create the pickup prompt.");
            return;
        }

        GameObject textObject = new GameObject("DogPickupPrompt", typeof(RectTransform));
        textObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(700f, 120f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = pickupPromptFontSize;
        text.color = pickupPromptColor;

        pickupPromptPanel = textObject;
        pickupPromptText = text;

        Debug.Log($"[Dog] Auto-created pickup prompt under '{canvas.name}'.");
    }

    void OnDestroy()
    {
        if (dogAi != null)
        {
            dogAi.OnTalkedTo.RemoveListener(PickUp);
            dogAi.OnTalkedTo.RemoveListener(HandleCompanionGreeted);
        }
        if (dogHotspot != null) dogHotspot.OnPlayerExitRange.RemoveListener(HandlePlayerExitRange);
        if (ownsDogBinding) boundDogObjects.Remove(dogObjectName);
    }

    void Update()
    {
        if (!listenerBound) TryBindToDog();
    }

    // The dog doesn't exist until NPCManager spawns it (on GameEvents.OnDogQuestAccepted),
    // so this keeps retrying each frame (cheap no-op once bound) the same way
    // GirlNpcBehavior.TryBindToGirl does. Waits for the hotspot too (added by NPCai.Start
    // a moment after the dog spawns), not just NPCai itself, so the prompt hookup below
    // never silently misses binding to it.
    private void TryBindToDog()
    {
        GameObject dogObject = GameObject.Find(dogObjectName);
        if (dogObject == null)
        {
            if (!loggedNoObject)
            {
                Debug.Log($"[Dog] Still waiting - no active GameObject named '{dogObjectName}' found yet.");
                loggedNoObject = true;
            }
            return;
        }

        NPCai foundAi = dogObject.GetComponent<NPCai>();
        if (foundAi == null)
        {
            if (!loggedNoAi)
            {
                Debug.LogWarning($"[Dog] Found '{dogObjectName}' but it has no NPCai component on it.");
                loggedNoAi = true;
            }
            return;
        }

        InteractableHotspot foundHotspot = dogObject.GetComponent<InteractableHotspot>();
        if (foundHotspot == null)
        {
            if (!loggedNoHotspot)
            {
                Debug.LogWarning($"[Dog] Found '{dogObjectName}' with NPCai but no InteractableHotspot on it yet - will keep retrying.");
                loggedNoHotspot = true;
            }
            return;
        }

        if (!boundDogObjects.Add(dogObjectName))
        {
            Debug.LogWarning($"DogNpcBehavior: another instance has already bound to '{dogObjectName}' - disabling this duplicate on '{name}'. Find and remove the extra DogNpcBehavior GameObject from the scene.");
            listenerBound = true; // stop retrying every frame - this instance is done for good
            enabled = false;
            return;
        }

        dogAi = foundAi;
        dogHotspot = foundHotspot;

        dogAi.OnTalkedTo.AddListener(PickUp);
        dogHotspot.OnPlayerExitRange.AddListener(HandlePlayerExitRange);

        listenerBound = true;
        ownsDogBinding = true;
        Debug.Log($"[Dog] DogNpcBehavior bound to '{dogObject.name}' - pickup and prompt are live.");
    }

    // Walking away mid-confirm cancels it instead of leaving the dog frozen and the
    // player's input locked with nobody there to press E a second time.
    private void HandlePlayerExitRange()
    {
        if (!awaitingPickupConfirm) return;

        awaitingPickupConfirm = false;
        SetPromptVisible(false);
        if (playerController != null) playerController.SetInputLocked(false);
        if (dogAi != null) dogAi.EndForcedPause();
    }

    // Fired by NPCai.OnTalkedTo (E pressed while in range) - a deliberate two-press chain,
    // not a single-press pickup: the first E freezes the dog, turns it to face the player,
    // locks the player's own input, and reveals the confirm prompt (BeginPickupConfirm);
    // only the second E (with the prompt already up) actually completes the pickup
    // (CompletePickup). A stray first press near the dog can't accidentally consume it.
    private void PickUp()
    {
        if (dogAi == null) return;

        if (!awaitingPickupConfirm) BeginPickupConfirm();
        else CompletePickup();
    }

    private void BeginPickupConfirm()
    {
        awaitingPickupConfirm = true;

        dogAi.BeginForcedPause();
        FaceDogAtPlayer();
        if (playerController != null) playerController.SetInputLocked(true);

        SetPromptVisible(true);
    }

    // Adds the dog to the inventory (shown with ItemCatalog's "D" placeholder letter),
    // renames the quest to reflect the delivery step, and deactivates the dog. If the
    // inventory turns out to be full, everything just unfreezes/cancels instead of losing
    // the dog or silently dropping the quest rename.
    private void CompletePickup()
    {
        awaitingPickupConfirm = false;
        SetPromptVisible(false);
        if (playerController != null) playerController.SetInputLocked(false);

        // Re-fetched here rather than trusting Start()'s one-shot lookup - InventoryManager
        // is created at runtime by UInavigator, and if that hadn't run yet when this
        // object's own Start() fired, inventory would otherwise stay null forever and the
        // pickup would silently never register.
        if (inventory == null) inventory = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);

        ItemDefinition dogItem = ItemCatalog.GetByName(dogItemName);
        bool added = dogItem != null && inventory != null && inventory.AddItem(dogItem, 1);

        if (dogItem == null) Debug.LogWarning($"[Dog] No ItemCatalog entry named '{dogItemName}'.");
        else if (inventory == null) Debug.LogWarning("[Dog] Could not find an InventoryManager - pickup did not register.");

        if (!added)
        {
            dogAi.EndForcedPause();
            return;
        }

        QuestManager.Instance?.RenameQuest(dogQuestNameBeforePickup, dogQuestNameAfterPickup);

        // Detach from this exact NPCai instance before deactivating it - NPCManager
        // reactivates this same GameObject as the post-quest companion (see
        // HandleDogFound), and without this its OnTalkedTo would still carry PickUp,
        // triggering the whole confirm flow again on an already-delivered dog. Wired here
        // (rather than needing a separate script placed in the scene) since it's the exact
        // same NPCai instance NPCManager reactivates later - the listener survives the
        // SetActive(false) below and is already live by the time it comes back as a
        // companion.
        dogAi.OnTalkedTo.RemoveListener(PickUp);
        dogAi.OnTalkedTo.AddListener(HandleCompanionGreeted);
        if (dogHotspot != null) dogHotspot.OnPlayerExitRange.RemoveListener(HandlePlayerExitRange);

        dogAi.gameObject.SetActive(false);
    }

    // Placeholder small talk for the post-delivery companion dog: pressing E just turns it
    // to face the player briefly, then it resumes roaming and otherwise ignores them - no
    // dialogue panel, no mechanical effect, same "outside the quest" spirit as
    // GirlNpcBehavior's small talk.
    private void HandleCompanionGreeted()
    {
        if (dogAi == null) return;

        FaceDogAtPlayer();
        dogAi.BeginForcedPause();

        CancelInvoke(nameof(ResumeCompanionRoaming));
        Invoke(nameof(ResumeCompanionRoaming), companionAcknowledgeSeconds);
    }

    private void ResumeCompanionRoaming()
    {
        if (dogAi != null) dogAi.EndForcedPause();
    }

    private void FaceDogAtPlayer()
    {
        if (playerController == null) return;

        Vector3 direction = playerController.transform.position - dogAi.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        dogAi.transform.rotation = Quaternion.LookRotation(direction);
    }

    private void SetPromptVisible(bool visible)
    {
        if (pickupPromptPanel != null) pickupPromptPanel.SetActive(visible);
    }
}

// Implementation Steps:
// 1. Add an empty GameObject anywhere in the scene (e.g. next to NPCManager) and attach
//    this script to it.
// 2. Nothing else is required for the prompt - as long as a Canvas named "UI-Player"
//    exists (it already does, per InteractableHotspot's own E-Key prompt), a centered
//    "Click E to pick up the dog" text is built automatically at Start and starts hidden.
//    Only touch Pickup Prompt Panel/Pickup Prompt Text if you want to hand-style your own
//    instead - leaving them blank is the normal path.
// 3. Press Play, accept the girl's dog quest, walk up to the roaming dog. First E: it
//    pauses and turns to face you, your own movement locks, and the "Click E to pick up
//    the dog" prompt appears. Second E: it's added to your inventory as "Dog" (shown as
//    "D"), the quest renames to dogQuestNameAfterPickup, the prompt hides, and the dog
//    disappears from the world. Walking away before the second E cancels the whole thing.
// 4. Bring it to the girl and talk to her (see GirlNpcBehavior.TurnInDog) to complete the
//    quest - a fresh companion dog then reappears near the base (NPCManager.HandleDogFound).
// 5. Pressing E on that companion dog turns it to face you for companionAcknowledgeSeconds,
//    then it resumes roaming on its own and otherwise ignores further E presses the same
//    way - a placeholder reaction, no dialogue panel, no player input lock, no quest effect.
