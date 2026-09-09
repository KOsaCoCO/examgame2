using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using NTGD124;

// The "BUY LAND" prompt. Lives directly on the 3D TextMeshPro object sitting
// on ButtonHUB's face. This single script drives ButtonHUB entirely:
// - Hovers it HoverHeight above whichever base slot is next to unlock
//   (BaseSlotExpander.GetFirstLockedSlotTransform), centered on that slot,
//   and shows that slot's cost (BaseSlotExpander.GetRequirementsText).
// - Turns it to face the player, and keeps this text SurfacePadding units in
//   front of whichever face currently points at the player.
// - Proximity + E to interact (not a mouse click - the cursor is locked to
//   screen center for the mouse-look camera, so clicking on world objects
//   isn't reliable anymore). Proximity and the "press E" panel are entirely
//   InteractableHotspot's job (auto-added to ButtonHUB below) - this script
//   just listens to its OnInteract event. While in range:
//     - if the slot isn't affordable, shows a "not enough resources" result
//       and moves on - no point asking to confirm something impossible.
//     - if it is affordable, arms a confirmation prompt and waits - no timer -
//       until either E confirms the purchase, or Escape cancels it back to
//       the normal prompt.
//   A confirmed purchase shows a YES/NO result for a second before moving on
//   to hover above the next locked slot. Once every slot is unlocked, both
//   ButtonHUB and this text deactivate.
//
// Only put this script on the text object - ButtonHUB itself needs no manual
// setup either, InteractableHotspot is added to it automatically.
public class UIPromptActionHub : MonoBehaviour
{
    [Tooltip("Name of the interactable world object this prompt is attached to.")]
    public string buttonHubObjectName = "ButtonHUB";
    [Tooltip("How high above the current target slot ButtonHUB hovers.")]
    public float hoverHeight = 3.5f;
    [Tooltip("How far in front of ButtonHUB's current face this text sits.")]
    public float surfacePadding = 0.14f;
    [Tooltip("How long the YES/NO/not-enough result stays on screen before moving on to the next slot.")]
    public float resultDisplaySeconds = 1f;

    private const string BasePromptMessage = "BUY LAND";
    private const string ConfirmMessage = "PRESS E TO CONFIRM\n(Esc to cancel)";
    private const string YesMessage = "YES";
    private const string NoMessage = "NO";
    private const string NotEnoughMessage = "NOT ENOUGH\nRESOURCES";

    private TMP_Text promptText;
    private Transform buttonHubTransform;
    private BaseSlotExpander baseSlotExpander;
    private PlayerController playerController;
    private InteractableHotspot interactHotspot;
    private Transform currentTargetSlot;

    // Awaiting confirmation has no timer - it waits indefinitely for an explicit
    // confirm (press E again) or cancel (Escape). Showing a result (YES/NO/not
    // enough) does still time out, purely as a display duration, not a decision.
    private bool awaitingConfirm;
    private bool showingResult;
    private float resultTimer;

    void Awake()
    {
        promptText = GetComponent<TMP_Text>();
    }

    void Start()
    {
        baseSlotExpander = FindAnyObjectByType<BaseSlotExpander>();
        playerController = FindAnyObjectByType<PlayerController>();

        GameObject buttonHubObject = GameObject.Find(buttonHubObjectName);
        if (buttonHubObject != null)
        {
            buttonHubTransform = buttonHubObject.transform;

            interactHotspot = buttonHubObject.GetComponent<InteractableHotspot>();
            if (interactHotspot == null) interactHotspot = buttonHubObject.AddComponent<InteractableHotspot>();
            interactHotspot.OnInteract.AddListener(HandleInteractPressed);
        }
    }

    void OnDestroy()
    {
        if (interactHotspot != null) interactHotspot.OnInteract.RemoveListener(HandleInteractPressed);
    }

    void Update()
    {
        // Don't retarget mid-confirmation or mid-celebration - stay put on the
        // current slot until the player resolves it or the result clears.
        if (!awaitingConfirm && !showingResult && !UpdateHoverTarget()) return;

        FaceAndFollowPlayer();

        if (showingResult) UpdateResultTimer();
        else if (awaitingConfirm) CheckForCancel();
    }


    ///// Action Methods /////

    // Finds the next locked slot and hovers ButtonHUB centered above it. Returns false
    // (and deactivates the whole hub) once there is no locked slot left to move to.
    private bool UpdateHoverTarget()
    {
        if (baseSlotExpander == null || buttonHubTransform == null) return false;

        currentTargetSlot = baseSlotExpander.GetFirstLockedSlotTransform();
        if (currentTargetSlot == null)
        {
            DeactivateHub();
            return false;
        }

        buttonHubTransform.position = currentTargetSlot.position + Vector3.up * hoverHeight;
        RefreshIdlePrompt();
        return true;
    }

    // Turns ButtonHUB to face the player (yaw only, stays upright), then places this
    // text SurfacePadding units in front of whichever face is now pointing at the
    // player, facing the same way - so the text always sits on the visible surface.
    private void FaceAndFollowPlayer()
    {
        if (playerController == null || buttonHubTransform == null) return;

        Vector3 direction = buttonHubTransform.position - playerController.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion facing = Quaternion.LookRotation(direction);
        buttonHubTransform.rotation = facing;

        Vector3 surfacePosition = buttonHubTransform.position - buttonHubTransform.forward * surfacePadding;
        transform.SetPositionAndRotation(surfacePosition, facing);
    }

    // Fired by InteractableHotspot.OnInteract - only while the player is inside
    // ButtonHUB's trigger Collider, so no proximity check needed here.
    private void HandleInteractPressed()
    {
        if (showingResult || currentTargetSlot == null) return;

        if (awaitingConfirm)
        {
            awaitingConfirm = false;
            bool unlocked = baseSlotExpander.TryUnlockSlot(currentTargetSlot);
            ShowResult(unlocked ? YesMessage : NoMessage);
            return;
        }

        // Idle: arm a confirmation if affordable, or reject immediately - no point
        // confirming a purchase that can't happen.
        if (baseSlotExpander.CanAffordSlot(currentTargetSlot))
        {
            awaitingConfirm = true;
            if (promptText != null) promptText.text = ConfirmMessage;
        }
        else
        {
            ShowResult(NotEnoughMessage);
        }
    }

    // Escape has no equivalent event to reuse, so this still polls the keyboard
    // directly - cancelling isn't range-gated, unlike starting/confirming a purchase.
    private void CheckForCancel()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        awaitingConfirm = false;
        RefreshIdlePrompt();
    }

    private void ShowResult(string message)
    {
        if (promptText != null) promptText.text = message;
        showingResult = true;
        resultTimer = resultDisplaySeconds;
    }

    private void UpdateResultTimer()
    {
        resultTimer -= Time.deltaTime;
        if (resultTimer > 0f) return;

        showingResult = false;
        RefreshIdlePrompt();
    }

    // Shows "BUY LAND" plus the current target slot's cost, e.g. "BUY LAND\n(3 Wood, 3 Rock)",
    // so the player can see what a slot costs before ever interacting.
    private void RefreshIdlePrompt()
    {
        if (promptText == null || currentTargetSlot == null || baseSlotExpander == null) return;

        string requirements = baseSlotExpander.GetRequirementsText(currentTargetSlot);
        promptText.text = string.IsNullOrEmpty(requirements) ? BasePromptMessage : $"{BasePromptMessage}\n({requirements})";
    }

    private void DeactivateHub()
    {
        // Disabling ButtonHUB also disables its InteractableHotspot, whose own
        // OnDisable hides the "press E" panel if the player happened to be inside.
        if (buttonHubTransform != null) buttonHubTransform.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }
}
