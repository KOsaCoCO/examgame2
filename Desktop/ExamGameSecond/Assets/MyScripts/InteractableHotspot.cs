using UnityEngine;
using UnityEngine.Events;

// Attach to any interactable object - tag it "Interactive" too, as a label for
// future systems to find/recognize it by - together with a trigger Collider
// sized to its interaction radius. Proximity is driven entirely by that
// Collider's physics trigger events (a CharacterController generates these on
// overlap without needing a Rigidbody on either side), not manual distance
// checks. Shows/hides the shared "press E" prompt panel while the player is
// inside, and fires OnInteract when E is pressed while inside.
//
// Reused by ButtonHUB today; NPCs/tools/other interactables can use this same
// component later - just add it, a trigger Collider sized for their own
// interaction radius, and the "Interactive" tag.
[RequireComponent(typeof(Collider))]
public class InteractableHotspot : MonoBehaviour
{
    [Tooltip("Tag the player's GameObject carries - only a collider with this tag counts as the player entering/leaving.")]
    public string playerTag = "Player";
    [Tooltip("Name of the always-active parent the prompt panel lives under.")]
    public string interactPromptParentName = "UI-Player";
    [Tooltip("Name of the shared 2D 'press E' UI panel (a child of interactPromptParentName). Starts inactive in the scene, so it's found via Transform.Find from an active parent, not GameObject.Find - GameObject.Find can't see inactive objects.")]
    public string interactPromptPanelName = "E-Key";

    // Field initializers, not just bare declarations - this component is added at
    // runtime via AddComponent (see UIPromptActionHub.Start, HarvestableResource.Start),
    // which skips the Inspector/deserialization step that would normally construct a
    // UnityEvent automatically, leaving it null until something explicitly assigns it.
    [Tooltip("Fired when E is pressed while the player is inside this hotspot's trigger.")]
    public UnityEvent OnInteract = new();
    [Tooltip("Fired when right-click is pressed while the player is inside this hotspot's trigger - for interactions E doesn't suit, e.g. repeated chopping.")]
    public UnityEvent OnSecondaryInteract = new();
    [Tooltip("Fired when the player enters this hotspot's trigger - for a per-object reaction beyond the shared E-Key prompt, e.g. an NPC turning to face the player or a custom on-screen prompt (see DogNpcBehavior).")]
    public UnityEvent OnPlayerEnterRange = new();
    [Tooltip("Fired when the player exits this hotspot's trigger (including via OnDisable, same as the shared prompt panel).")]
    public UnityEvent OnPlayerExitRange = new();

    // For a late subscriber (e.g. DogNpcBehavior, which only finishes binding a few
    // frames after the dog spawns) to sync its own prompt to the current state instead of
    // missing an OnPlayerEnterRange that already fired before it was listening.
    public bool IsPlayerInside => playerInside;

    private PlayerController playerController;
    private GameObject interactPromptPanel;
    private bool playerInside;

    void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerController.onInteract.AddListener(HandlePlayerInteractKey);
            playerController.onSecondaryInteract.AddListener(HandlePlayerSecondaryInteractKey);
        }

        GameObject parent = GameObject.Find(interactPromptParentName);
        Transform panelTransform = parent != null ? parent.transform.Find(interactPromptPanelName) : null;
        interactPromptPanel = panelTransform != null ? panelTransform.gameObject : null;

        if (interactPromptPanel == null)
        {
            Debug.LogWarning($"InteractableHotspot on '{name}' could not find a prompt panel named '{interactPromptPanelName}' under '{interactPromptParentName}'.");
        }
        else
        {
            interactPromptPanel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (playerController == null) return;
        playerController.onInteract.RemoveListener(HandlePlayerInteractKey);
        playerController.onSecondaryInteract.RemoveListener(HandlePlayerSecondaryInteractKey);
    }

    // Covers SetActive(false) on this object (or its parent, e.g. ButtonHUB
    // deactivating once every base slot is unlocked) - trigger-exit isn't
    // reliably guaranteed to fire in that case, but OnDisable always is.
    void OnDisable()
    {
        if (!playerInside) return;
        playerInside = false;
        SetPromptVisible(false);
        OnPlayerExitRange.Invoke();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        Debug.Log($"[Interact] Player entered '{name}' trigger (other collider: '{other.name}', tag '{other.tag}').");
        playerInside = true;
        SetPromptVisible(true);
        OnPlayerEnterRange.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        Debug.Log($"[Interact] Player exited '{name}' trigger.");
        playerInside = false;
        SetPromptVisible(false);
        OnPlayerExitRange.Invoke();
    }

    // Only logs when actually inside - with many hotspots in the scene (e.g. 18
    // planted trees), this fires on every one of them per keypress, so logging
    // the "not inside" case for all of them would just be noise.
    private void HandlePlayerInteractKey()
    {
        if (!playerInside) return;
        Debug.Log($"[Interact] E accepted by '{name}'.");
        OnInteract.Invoke();
    }

    private void HandlePlayerSecondaryInteractKey()
    {
        if (!playerInside) return;
        Debug.Log($"[Interact] Right-click accepted by '{name}'.");
        OnSecondaryInteract.Invoke();
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactPromptPanel != null) interactPromptPanel.SetActive(visible);
    }
}
