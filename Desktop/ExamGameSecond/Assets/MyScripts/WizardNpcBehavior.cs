using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Everything specific to the wizard's "clear the miasma" conversation lives here - generic
// roaming/follow/pause/ground-snap stays in NPCai (shared with girl/dog), this file only
// owns what's unique to him. Unlike the girl (E-key, walk-up-and-press) the wizard follows
// the player after arriving (NPCai.SetFollowTarget) and the dialogue opens automatically
// once he actually catches up - reuses InteractableHotspot.OnPlayerEnterRange (already a
// "something entered my trigger Collider" event) as that contact signal rather than
// building a separate collision system. Screen-space UI (see fields below), same shape as
// GirlNpcBehavior - assign dialoguePanel/dialogueText/yesButton/noButton in the Inspector
// to the "dialogueWizardNPC" panel. Only covers the offer/insist/accept exchange - what
// "clearing the miasma" actually involves is still undesigned (see GameplayRoadmap.md),
// so accepting just adds the quest, the same as the girl's dog quest did before that
// mechanic existed.
public class WizardNpcBehavior : MonoBehaviour
{
    private enum State { Idle, Greeting, QuestOffer, HappyResponse, TradeHint, Insisting, TurnIn }

    [Header("Setup")]
    [Tooltip("Name of the spawned wizard NPC to attach this dialogue to - matches NPCManager's npcLabel.")]
    public string wizardObjectName = "Wizard";
    [Tooltip("Movement speed while chasing the player to start the quest - deliberately well above the player's own moveSpeed (5f) so he decisively catches up rather than merely edging ahead.")]
    public float chaseSpeed = 9f;
    [Tooltip("How long each auto-advancing line stays up before moving to the next. Doesn't apply to the quest-offer line, which waits for a Yes/No click instead.")]
    public float autoAdvanceSeconds = 3f;
    [Tooltip("How far the wizard wanders once the quest is accepted and he stops following - centered on wherever he caught up to the player.")]
    public float localRoamRadius = 6f;

    [Header("UI (drag from the dialogueWizardNPC panel)")]
    [Tooltip("Root panel to show/hide - the whole dialogueWizardNPC screen-space UI object.")]
    public GameObject dialoguePanel;
    public TMP_Text dialogueText;
    public Button yesButton;
    public Button noButton;

    [Header("Dialogue Text")]
    [TextArea(2, 3)] public string greetingLine = "At last - please, wait, I need your help!";
    [TextArea(2, 3)] public string questOfferLine = "A red miasma has overrun my home, and I can no longer clear it alone. Help me drive it out, and I will grant you some of my own magic in return.";
    [TextArea(2, 3)] public string acceptedLine = "Thank you. Return to me when you are ready, and we will begin.";
    [TextArea(2, 3)] public string tradeHintLine = "Before you go - come speak with me again anytime. Press E and I'll show you what I have to trade.";
    [TextArea(2, 3)] public string insistLine = "Please - I have nowhere else to turn. Will you help me?";
    [TextArea(2, 3)] public string turnInLine = "The red miasma is lifting - you've done it! Take my thanks; when you're ready, I have something special waiting for you at my trade stall.";

    private const string QuestName = "Clear the wizard's home of the red miasma";
    // Bridges the gap between the miasma quest completing and NightBossSpawner actually
    // spawning the boss (player level 6+ / night begins are the other two gate conditions) -
    // without this the quest log went silent in between. Must match the literal string
    // NightBossSpawner.SpawnBoss removes right before adding its own "Defeat the monster
    // threatening the base" quest.
    public const string BossPrepQuestName = "Prepare for the boss's attack tonight";

    // Lets other per-NPC scripts on the same object (e.g. TradeManager) check whether his
    // own dialogue currently owns the player's input/cursor before doing anything that
    // would fight over the same state.
    public bool IsConversing => state != State.Idle;

    // Fired right after the dialogue panel actually opens - UInavigator subscribes to force-
    // close every other browsing panel (Inventory/Quest Full View/Main Menu) the same way it
    // already does for TradeManager.OnShopOpened, so the two can't stack on top of each other.
    public event Action OnDialogueOpened;

    private Transform wizardTransform;
    private NPCai wizardAi;
    private InteractableHotspot wizardHotspot;
    private PlayerController playerController;
    private TradeManager tradeManager; // lazy-found, same retry pattern as PlayerHealth.BoostSlots

    private TradeManager TradeManager => tradeManager != null ? tradeManager : (tradeManager = FindAnyObjectByType<TradeManager>());

    private State state = State.Idle;
    private float lineTimer;
    private bool questResolved;
    private bool miasmaCleared;
    private bool questCompleted;
    private bool listenerBound;
    private bool ownsWizardBinding;

    // Same duplicate guard as GirlNpcBehavior.boundGirlObjects - if two WizardNpcBehavior
    // instances ever end up in the scene, only the first to bind gets a working panel; the
    // second self-disables instead of opening a second, stuck one on the same contact.
    private static readonly HashSet<string> boundWizardObjects = new();

    void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();

        if (yesButton != null) yesButton.onClick.AddListener(() => AnswerQuest(accepted: true));
        if (noButton != null) noButton.onClick.AddListener(() => AnswerQuest(accepted: false));

        SetPanelVisible(false);
        TryBindToWizard();
    }

    void OnEnable()
    {
        GameEvents.OnMiasmaCleared += HandleMiasmaCleared;
    }

    void OnDisable()
    {
        GameEvents.OnMiasmaCleared -= HandleMiasmaCleared;
    }

    void OnDestroy()
    {
        if (wizardHotspot != null) wizardHotspot.OnPlayerEnterRange.RemoveListener(BeginConversation);
        if (ownsWizardBinding) boundWizardObjects.Remove(wizardObjectName);
    }

    private void HandleMiasmaCleared()
    {
        miasmaCleared = true;
    }

    void Update()
    {
        if (!listenerBound) TryBindToWizard();
        if (wizardTransform == null || state == State.Idle || state == State.QuestOffer) return;

        UpdateAutoAdvance();
    }

    // The wizard doesn't exist until NPCManager spawns him (on GameEvents.
    // OnDogDeliveryConversationEnded, once the girl's own dog-delivery dialogue has fully
    // closed - not the instant turn-in starts, so the two conversations never fight over
    // Cursor/SetInputLocked state - see NPCManager.HandleWizardArrival), so this keeps
    // retrying each frame (cheap no-op once bound) the same way GirlNpcBehavior.
    // TryBindToGirl does. Starts him following the player the moment he's found - that's
    // his whole approach behavior, not something NPCManager needs to know about.
    private void TryBindToWizard()
    {
        GameObject wizardObject = GameObject.Find(wizardObjectName);
        if (wizardObject == null) return;

        NPCai foundAi = wizardObject.GetComponent<NPCai>();
        if (foundAi == null) return;

        InteractableHotspot foundHotspot = wizardObject.GetComponent<InteractableHotspot>();
        if (foundHotspot == null) return;

        if (!boundWizardObjects.Add(wizardObjectName))
        {
            Debug.LogWarning($"WizardNpcBehavior: another instance has already bound to '{wizardObjectName}' - disabling this duplicate on '{name}' so it doesn't open a second, stuck dialogue panel. Find and remove the extra WizardNpcBehavior GameObject from the scene.");
            listenerBound = true; // stop retrying every frame - this instance is done for good
            enabled = false;
            return;
        }

        wizardAi = foundAi;
        wizardTransform = wizardObject.transform;
        wizardHotspot = foundHotspot;

        wizardAi.followSpeed = chaseSpeed;
        wizardAi.SetFollowTarget(playerController != null ? playerController.transform : null);
        wizardHotspot.OnPlayerEnterRange.AddListener(BeginConversation);

        listenerBound = true;
        ownsWizardBinding = true;

        // Catches the case where the player is already standing in his trigger by the time
        // binding finishes (e.g. he spawned close, or followSpeed closed the gap in the
        // handful of frames binding took) - otherwise an OnPlayerEnterRange that already
        // fired before anything was listening would be missed entirely, and he'd just stand
        // there touching the player without ever opening the dialogue.
        if (wizardHotspot.IsPlayerInside) BeginConversation();
    }

    // Fired by InteractableHotspot.OnPlayerEnterRange (his trigger Collider touching the
    // player's) rather than an E press - he's chasing the player down initially, not waiting
    // to be talked to; after accepting he stops following and roams locally instead, but the
    // same trigger keeps firing whenever the player wanders back into that local range, which
    // is also how the turn-in conversation below gets triggered. Freezes him, turns both of
    // them to face each other, locks the player's own input/camera, and opens the panel - see
    // AdvanceFrom for how it plays out. Fully done (questCompleted) or already mid-conversation
    // - silent no-op. Accepted but still out clearing the miasma (questResolved && !
    // miasmaCleared) - also a silent no-op for now, same "no rejection UI yet" convention as
    // HarvestableResource; there's nothing new to say until MiasmaTree raises OnMiasmaCleared.
    // Also a silent no-op while his own Trade shop is open (E-triggered, independent of this
    // proximity trigger) - otherwise the turn-in dialogue could pop up behind an already-open
    // shop, blocking the view of both.
    private void BeginConversation()
    {
        if (questCompleted || state != State.Idle) return;
        if (questResolved && !miasmaCleared) return;
        if (TradeManager != null && TradeManager.IsShopOpen) return;

        wizardAi.BeginForcedPause();
        FaceEachOther();

        if (playerController != null) playerController.SetInputLocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetPanelVisible(true);

        // state must move off Idle BEFORE OnDialogueOpened fires - UInavigator's handler for
        // this event calls RefreshCursorLock() synchronously, which reads IsConversing (state !=
        // Idle) to decide whether to keep the cursor unlocked. With state still Idle at invoke
        // time, RefreshCursorLock() would see no modal dialogue active and immediately re-lock/
        // hide the cursor we just showed above - the exact "mouse disappears" bug.
        if (questResolved) // implies miasmaCleared - returning to turn the quest in
        {
            state = State.TurnIn;
        }
        else
        {
            state = State.Greeting;
        }

        OnDialogueOpened?.Invoke();

        ShowLine(questResolved ? turnInLine : greetingLine);
    }

    // Turns the wizard to face the player (same as GirlNpcBehavior.FaceGirlAtPlayer) AND
    // the player's own transform (which the camera tracks via PlayerController.Look) to
    // face the wizard - a proper face-to-face beat instead of the player looking at his
    // side/back after being caught from behind.
    private void FaceEachOther()
    {
        if (playerController == null) return;

        Vector3 toPlayer = playerController.transform.position - wizardTransform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.0001f) wizardTransform.rotation = Quaternion.LookRotation(toPlayer);

        Vector3 toWizard = wizardTransform.position - playerController.transform.position;
        toWizard.y = 0f;
        if (toWizard.sqrMagnitude > 0.0001f) playerController.transform.rotation = Quaternion.LookRotation(toWizard);
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

    // No doesn't end the conversation - he just keeps insisting and re-asks until the
    // player clicks Yes (see AdvanceFrom's Insisting case, which loops back to QuestOffer
    // instead of moving on). Only Yes resolves the quest.
    private void AnswerQuest(bool accepted)
    {
        if (state != State.QuestOffer) return;

        if (accepted)
        {
            questResolved = true;
            QuestManager.Instance?.AddQuest(QuestName, isMainQuest: true);
            GameEvents.RaiseWizardQuestAccepted();
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
    // HappyResponse -> TradeHint -> ends. TurnIn (a separate, later conversation - see
    // BeginConversation) completes the quest and ends too.
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
                state = State.TradeHint;
                ShowLine(tradeHintLine);
                break;
            case State.TurnIn:
                questCompleted = true;
                QuestManager.Instance?.RemoveQuest(QuestName);
                PlayerLevel.Instance?.AddExperience(50);
                GameEvents.RaiseWizardQuestCompleted();
                QuestManager.Instance?.AddQuest(BossPrepQuestName, isMainQuest: true);
                EndConversation();
                break;
            default: // TradeHint - nothing left to say
                EndConversation();
                break;
        }
    }

    private void EndConversation()
    {
        state = State.Idle;
        SetPanelVisible(false);
        wizardAi.EndForcedPause();

        if (playerController != null) playerController.SetInputLocked(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (questResolved)
        {
            // Stops chasing the player now that the offer's been made and accepted -
            // switches to wandering locally around wherever he caught up, instead of
            // trailing the player forever.
            wizardAi.SetFollowTarget(null);
            wizardAi.SetRoamArea(wizardTransform.position, localRoamRadius);
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
// 1. Create the "dialogueWizardNPC" panel (TMP text + two Yes/No buttons), same shape as
//    the existing "dialogueGirlNPC" one - can be a straight duplicate with new text/names.
// 2. Add an empty GameObject anywhere in the scene and attach this script to it. Drag the
//    panel's root onto Dialogue Panel, its TMP text onto Dialogue Text, and the two buttons
//    onto Yes Button / No Button.
// 3. Make sure the wizard prefab has either its own sized trigger Collider (for a tight
//    "actually touching" range) or none at all - NPCai.EnsureInteractionTrigger falls back
//    to an auto-added 3-unit sphere otherwise, which reads as "nearby" rather than "touching".
// 4. Press Play, finish the dog quest (deliver it to the girl) - the wizard spawns behind
//    the player and immediately starts following, faster than the player's own moveSpeed
//    so he actually catches up. The moment his trigger touches the player: both freeze and
//    turn to face each other, your movement/camera lock, and the panel opens - a greeting,
//    then the miasma quest offer (click Yes/No). Clicking No shows insistLine and loops
//    back to the same question - he keeps asking until you click Yes. Clicking Yes shows
//    acceptedLine, then tradeHintLine (pointing the player at his E-press trade shop), adds
//    the quest, and he stops following - from then on he wanders locally (localRoamRadius)
//    around wherever he caught up, instead of trailing you forever.
// 5. "Clearing the miasma" now means: buy SW from this same wizard's Trade shop (costs
//    Skin/Bone/Meat/Special Item - hunted from mobs via MobHealth), arm it, and chop down
//    "tree_1 Big" (see MiasmaTree.cs) - the Redish Fog attached to it is the miasma, and
//    falls with the tree when it deactivates. Wander back into the wizard's (by then
//    local-roam) range afterward to trigger turnInLine and complete the quest - the Miasma
//    Stick you got from felling the tree separately trades for a Magic Stick, same shop.
