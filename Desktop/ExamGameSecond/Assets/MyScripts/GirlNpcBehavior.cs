using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Everything specific to the girl NPC's "find my lost dog" conversation lives here -
// generic roaming/pause/ground-snap stays in NPCai (shared with wizard/dog later), this
// file only owns what's unique to her. Screen-space UI (see fields below) instead of a
// world-space label floating next to her - assign dialoguePanel/dialogueText/yesButton/
// noButton in the Inspector to the "dialogueGirlNPC" panel under "Interactives UI" (its
// own TMP text and two buttons). This is one specific conversation, not a general
// dialogue system - kept as an explicit state machine rather than a data-driven tree
// since there's only the one NPC/quest that needs it right now.
public class GirlNpcBehavior : MonoBehaviour
{
    private enum State { Idle, Greeting, QuestOffer, HappyResponse, DogHint, Insisting, DogDelivered, Warning, Repeat }

    [Header("Setup")]
    [Tooltip("Name of the spawned girl NPC to attach this dialogue to - matches NPCManager's npcLabel.")]
    public string girlObjectName = "Girl";
    [Tooltip("How long each auto-advancing line stays up before moving to the next. Doesn't apply to the quest-offer line, which waits for a Yes/No click instead.")]
    public float autoAdvanceSeconds = 3f;

    [Header("UI (drag from the dialogueGirlNPC panel)")]
    [Tooltip("Root panel to show/hide - the whole dialogueGirlNPC screen-space UI object.")]
    public GameObject dialoguePanel;
    public TMP_Text dialogueText;
    public Button yesButton;
    public Button noButton;

    [Header("Dialogue Text")]
    [TextArea(2, 3)] public string greetingLine = "Oh, hello! I'm glad I found this place.";
    [TextArea(2, 3)] public string questOfferLine = "Actually... my dog ran off and I can't find him anywhere. Could you help me look?";
    [TextArea(2, 3)] public string acceptedLine = "Really? Thank you so much, that means a lot!";
    [TextArea(2, 3)] public string dogHintLine = "I think he's on the island somewhere.";
    [TextArea(2, 3)] public string insistLine = "Please? I really can't find him on my own, I need your help.";
    [TextArea(2, 3)] public string foundDogLine = "You found him! Thank you, thank you so much!";
    [TextArea(2, 3)] public string warningLine = "Be careful at night, it's scary out there - monsters come around. Why don't you get some weapons?";

    private const string QuestName = "Find the girl's lost dog";
    // DogNpcBehavior renames the quest to this once the dog is picked up (see
    // dogQuestNameAfterPickup there) - must match exactly, since that's the name this
    // quest actually carries by the time there's a Dog item to turn in.
    private const string QuestNameAfterDogPickup = "Bring the dog back to the girl";
    private const string DogItemName = "Dog";

    // Lets external systems (UInavigator's I/Q/Esc cross-panel switching, see
    // GameplayRoadmap.md's UI key-shortcut work) check whether her dialogue currently owns
    // the player's input/cursor before opening a browsing panel (Inventory/Quest Full View)
    // on top of it - same shape as WizardNpcBehavior.IsConversing.
    public bool IsConversing => state != State.Idle;

    private Transform girlTransform;
    private NPCai girlAi;
    private PlayerController playerController;
    private InventoryManager inventory;

    private State state = State.Idle;
    private float lineTimer;
    private bool questResolved;
    private bool listenerBound;
    private bool ownsGirlBinding;
    private bool justDeliveredDog;

    // Tracks which NPC object names already have a bound GirlNpcBehavior - guards against
    // two instances of this component existing in the scene at once (e.g. one left over
    // from an earlier setup pass) both binding to the same girl and both opening a panel
    // on the same E press. Only the first ever gets a working Yes/No flow; any duplicate
    // would otherwise sit there stuck on the question forever with no listener wired to
    // its own buttons. Static, so it's shared across every instance and resets cleanly on
    // each domain reload/Play session.
    private static readonly HashSet<string> boundGirlObjects = new();

    void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
        inventory = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);

        if (yesButton != null) yesButton.onClick.AddListener(() => AnswerYes());
        if (noButton != null) noButton.onClick.AddListener(() => AnswerNo());

        SetPanelVisible(false);
        TryBindToGirl();
    }

    void OnDestroy()
    {
        if (girlAi != null) girlAi.OnTalkedTo.RemoveListener(BeginConversation);
        if (ownsGirlBinding) boundGirlObjects.Remove(girlObjectName);
    }

    void Update()
    {
        if (!listenerBound) TryBindToGirl();
        if (girlTransform == null || state == State.Idle || state == State.QuestOffer) return;

        UpdateAutoAdvance();
    }

    // The girl doesn't exist until NPCManager spawns her, so this keeps retrying each
    // frame (cheap no-op once found) instead of only checking once in Start. Gated on
    // listenerBound rather than girlAi == null so a failed AddListener (e.g. OnTalkedTo
    // not ready yet) doesn't get stuck retrying forever without ever re-attempting it.
    private void TryBindToGirl()
    {
        GameObject girlObject = GameObject.Find(girlObjectName);
        if (girlObject == null) return;

        girlAi = girlObject.GetComponent<NPCai>();
        if (girlAi == null) return;

        if (!boundGirlObjects.Add(girlObjectName))
        {
            Debug.LogWarning($"GirlNpcBehavior: another instance has already bound to '{girlObjectName}' - disabling this duplicate on '{name}' so it doesn't open a second, stuck dialogue panel. Find and remove the extra GirlNpcBehavior GameObject from the scene.");
            listenerBound = true; // stop retrying every frame - this instance is done for good
            enabled = false;
            return;
        }

        girlTransform = girlObject.transform;
        girlAi.OnTalkedTo.AddListener(BeginConversation);
        listenerBound = true;
        ownsGirlBinding = true;
    }

    // Fired by NPCai.OnTalkedTo (E pressed while in her interaction range). Freezes her
    // in place, turns her to face the player, locks the player's own input, and opens
    // the panel - see AdvanceFrom for how the conversation plays out.
    private void BeginConversation()
    {
        if (state != State.Idle) return;

        girlAi.BeginForcedPause();
        FaceGirlAtPlayer();

        if (playerController != null) playerController.SetInputLocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetPanelVisible(true);

        if (CanTurnInDog())
        {
            TurnInDog();
            return;
        }

        state = questResolved ? State.Repeat : State.Greeting;
        ShowLine(questResolved ? warningLine : greetingLine);
    }

    // Gate for the auto turn-in below: the dog quest has to still be active (not already
    // turned in) and the player has to actually be holding the picked-up Dog item (see
    // DogNpcBehavior.PickUp).
    private bool CanTurnInDog()
    {
        // Re-fetched here rather than trusting Start()'s one-shot lookup - see the matching
        // comment in DogNpcBehavior.CompletePickup for why a null cache here would
        // otherwise never recover.
        if (inventory == null) inventory = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
        if (inventory == null) return false;
        if (QuestManager.Instance == null) return false;

        bool questActive = QuestManager.Instance.quests.Find(q => q.questName == QuestNameAfterDogPickup && q.isActive) != null;
        return questActive && inventory.HasItems(DogItemName, 1);
    }

    // Consumes the Dog item and raises GameEvents.OnDogFound - DogQuestProgress removes
    // the quest and NPCManager spawns the dog back in as a roaming companion. Feeds into
    // the same Warning -> end tail as the normal accepted flow (see AdvanceFrom).
    // justDeliveredDog is separate from OnDogFound - it marks THIS conversation as the one
    // that should raise OnDogDeliveryConversationEnded once it actually closes (see
    // EndConversation), so the wizard's arrival waits for the panel to be gone and the
    // player's input/cursor to be free again, instead of racing it.
    private void TurnInDog()
    {
        inventory.TryRemoveItems(DogItemName, 1);
        GameEvents.RaiseDogFound();
        justDeliveredDog = true;

        state = State.DogDelivered;
        ShowLine(foundDogLine);
    }

    private void FaceGirlAtPlayer()
    {
        if (playerController == null) return;

        Vector3 direction = playerController.transform.position - girlTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        girlTransform.rotation = Quaternion.LookRotation(direction);
    }

    private void ShowLine(string line)
    {
        if (dialogueText != null) dialogueText.text = line;
        lineTimer = autoAdvanceSeconds;

        bool isQuestion = state == State.QuestOffer;
        if (yesButton != null) yesButton.gameObject.SetActive(isQuestion);
        if (noButton != null) noButton.gameObject.SetActive(isQuestion);
    }

    private void UpdateAutoAdvance()
    {
        lineTimer -= Time.deltaTime;
        if (lineTimer > 0f) return;

        AdvanceFrom(state);
    }

    private void AnswerYes()
    {
        if (state == State.QuestOffer) AnswerQuest(accepted: true);
    }

    private void AnswerNo()
    {
        if (state == State.QuestOffer) AnswerQuest(accepted: false);
    }

    // No doesn't end the conversation - she just keeps insisting and re-asks until the
    // player clicks Yes (see AdvanceFrom's Insisting case, which loops back to QuestOffer
    // instead of moving on). Only Yes resolves the quest.
    private void AnswerQuest(bool accepted)
    {
        if (accepted)
        {
            questResolved = true;
            QuestManager.Instance?.AddQuest(QuestName, isMainQuest: true);
            GameEvents.RaiseDogQuestAccepted();
            state = State.HappyResponse;
            ShowLine(acceptedLine);
        }
        else
        {
            state = State.Insisting;
            ShowLine(insistLine);
        }
    }

    // Moves the conversation on to whatever comes after the line that just finished:
    // Greeting -> QuestOffer -> (Insisting -> QuestOffer, looping on every No) ->
    // HappyResponse -> DogHint -> Warning -> ends. DogDelivered (the auto turn-in, see
    // TurnInDog - a later, separate conversation) skips DogHint and feeds straight into that
    // same Warning -> end tail. Repeat (a later interaction after the quest is already
    // resolved, with no dog to turn in) just replays Warning, then ends.
    private void AdvanceFrom(State finishedState)
    {
        switch (finishedState)
        {
            case State.Greeting:
            case State.Insisting:
                state = State.QuestOffer;
                ShowLine(questOfferLine);
                break;
            case State.HappyResponse:
                state = State.DogHint;
                ShowLine(dogHintLine);
                break;
            case State.DogHint:
            case State.DogDelivered:
                state = State.Warning;
                ShowLine(warningLine);
                break;
            default: // Warning or Repeat - either way, nothing left to say
                EndConversation();
                break;
        }
    }

    private void EndConversation()
    {
        state = State.Idle;
        SetPanelVisible(false);
        girlAi.EndForcedPause();

        if (playerController != null) playerController.SetInputLocked(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (justDeliveredDog)
        {
            justDeliveredDog = false;
            GameEvents.RaiseDogDeliveryConversationEnded();
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(visible);
        if (!visible)
        {
            if (yesButton != null) yesButton.gameObject.SetActive(false);
            if (noButton != null) noButton.gameObject.SetActive(false);
        }
    }
}

// Implementation Steps:
// 1. Under "Interactives UI", the new "dialogueGirlNPC" panel (with its TMP text and two
//    buttons for Yes/No) should already exist per this session's setup. Put this script on
//    that panel object (or any convenient manager object - it no longer needs to live on
//    the text itself, since everything is Inspector-wired now).
// 2. Drag the panel's root GameObject onto Dialogue Panel, its TMP text onto Dialogue Text,
//    and the two buttons onto Yes Button / No Button.
// 3. Edit the dialogue line fields in the Inspector to taste - they're plain strings, no
//    code changes needed to tweak wording.
// 4. Press Play, buy the first base slot so the girl arrives, walk up and press E - she
//    freezes and turns to face you, your own movement/look locks, and the panel opens on
//    screen: a greeting, then the dog quest offer (click Yes/No - the only line that
//    waits, everything else auto-advances after autoAdvanceSeconds). Clicking No shows
//    insistLine and loops back to the same question - she keeps asking until you click
//    Yes. Clicking Yes shows acceptedLine, then the night-monster warning line, then
//    input unlocks and she resumes roaming.
// 5. Interacting again after the quest is resolved just replays the warning line, then ends -
//    doesn't re-offer the quest.
// 6. Once the player has picked up the roaming dog (see DogNpcBehavior - it's added to
//    the inventory as a "Dog" item), talking to the girl again skips straight to
//    foundDogLine, consumes the item, completes the quest, and plays the warning line -
//    no extra click needed.
