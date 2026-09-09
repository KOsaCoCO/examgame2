using System;

// Baseline event hub: gameplay systems (BaseSlotExpander today, NPCs/other
// triggers later) raise a typed event here when something meaningful happens,
// and anything that cares (quest tracking today, more later) subscribes -
// neither side needs a direct reference to the other. Add one new typed event
// per meaningful game moment, the same way OnBaseSlotUnlocked was added.
public static class GameEvents
{
    // Fired once a base/land slot finishes unlocking (BaseSlotExpander.TryUnlockSlot
    // succeeding - the purchase was confirmed and paid for), with the slot's index
    // in Slots[] (0 = the first slot to unlock).
    public static event Action<int> OnBaseSlotUnlocked;

    public static void RaiseBaseSlotUnlocked(int slotIndex) => OnBaseSlotUnlocked?.Invoke(slotIndex);

    // Fired when the player accepts the girl's "find my lost dog" quest offer
    // (GirlNpcBehavior.AnswerQuest) - cue for the dog NPC to arrive.
    public static event Action OnDogQuestAccepted;

    public static void RaiseDogQuestAccepted() => OnDogQuestAccepted?.Invoke();

    // Fired once the dog is handed back to the girl (GirlNpcBehavior.TurnInDog) - cue for
    // the quest to complete (DogQuestProgress) and for the dog to reappear as a roaming
    // base companion (NPCManager).
    public static event Action OnDogFound;

    public static void RaiseDogFound() => OnDogFound?.Invoke();

    // Fired once the girl's dog-delivery conversation actually CLOSES (GirlNpcBehavior.
    // EndConversation, panel deactivated, cursor/input unlocked) - deliberately later than
    // OnDogFound (which fires the instant turn-in starts, while her panel is still up).
    // Anything that needs the player's own state (input lock, cursor) to be fully free
    // again before it starts - e.g. the wizard arriving and immediately following the
    // player - should key off this, not OnDogFound, or the two conversations fight over
    // the same Cursor/SetInputLocked state.
    public static event Action OnDogDeliveryConversationEnded;

    public static void RaiseDogDeliveryConversationEnded() => OnDogDeliveryConversationEnded?.Invoke();

    // Fired when the player accepts the wizard's "clear the miasma" quest offer
    // (WizardNpcBehavior.AnswerQuest) - no subscriber yet, but a hook point for whatever
    // the miasma-clearing mechanic ends up needing once that's designed (see
    // GameplayRoadmap.md's wizard blind spot).
    public static event Action OnWizardQuestAccepted;

    public static void RaiseWizardQuestAccepted() => OnWizardQuestAccepted?.Invoke();

    // Fired once the miasma-source big tree is felled (MiasmaTree.cs) - cue for
    // WizardNpcBehavior to unlock its turn-in dialogue next time the player wanders back
    // into his (by then local-roam) range.
    public static event Action OnMiasmaCleared;

    public static void RaiseMiasmaCleared() => OnMiasmaCleared?.Invoke();

    // Fired whenever the player's hit points change (PlayerHealth.TakeDamage, and once from
    // Awake with the starting value) - (currentHealth, maxHealth). Cue for any health-bar UI
    // to refresh without polling every frame.
    public static event Action<int, int> OnPlayerHealthChanged;

    public static void RaisePlayerHealthChanged(int currentHealth, int maxHealth) => OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);

    // Fired whenever the player's level changes (PlayerLevel.AddExperience crossing a
    // threshold, and once from Start with the starting level) - cue for the level UI text to
    // refresh without polling every frame.
    public static event Action<int> OnPlayerLevelChanged;

    public static void RaisePlayerLevelChanged(int newLevel) => OnPlayerLevelChanged?.Invoke(newLevel);

    // Fired once the wizard's miasma quest is fully turned in (WizardNpcBehavior's TurnIn state
    // resolving, QuestManager.RemoveQuest already called) - deliberately later than
    // OnMiasmaCleared (which only fires when the tree is felled and unlocks turn-in dialogue).
    // Cue for the Monster quest step (Section 1) to check whether it can start - see
    // NightBossSpawner.
    public static event Action OnWizardQuestCompleted;

    public static void RaiseWizardQuestCompleted() => OnWizardQuestCompleted?.Invoke();

    // Fired once per cycle, the instant DayNightTimeCycle flips from day to night (not every
    // frame it's night) - cue for anything gated on "night just began" rather than polling
    // IsDaytime(). See NightBossSpawner.
    public static event Action OnNightBegan;

    public static void RaiseNightBegan() => OnNightBegan?.Invoke();

    // Fired once per cycle, the instant DayNightTimeCycle flips from night to day - the
    // counterpart to OnNightBegan. See DayPlantSpawner/NightPlantSpawner.
    public static event Action OnDayBegan;

    public static void RaiseDayBegan() => OnDayBegan?.Invoke();

    // Fired when MobHealth.Die() runs on a mob flagged IsUniqueBoss (currently only the
    // NightBossSpawner-spawned Monster35 boss) - cue for the Monster quest step to complete.
    public static event Action OnNightBossDefeated;

    public static void RaiseNightBossDefeated() => OnNightBossDefeated?.Invoke();

    // Fired once, the instant NightBossSpawner.SpawnBoss actually spawns the boss - cue for
    // every alive Mobai to drop its individual behaviour and join the siege on walls/base
    // slots, and for every NPCai to start fleeing nearby mobs at double speed. Cleared again by
    // OnNightBossDefeated/OnDayBegan (whichever fires first).
    public static event Action OnNightBossSpawned;

    public static void RaiseNightBossSpawned() => OnNightBossSpawned?.Invoke();

    // Fired once per night, the first time any mob's RollForNightRally actually succeeds
    // (Mobai.cs) - cue for MobAlertUI's flashing "monsters incoming" signal. Individual mobs
    // still roll independently after this; it only marks the first successful roll each night.
    public static event Action OnNightRallyBegan;

    public static void RaiseNightRallyBegan() => OnNightRallyBegan?.Invoke();

    // Fired once from PlayerHealth.Die() (currentHealth hits 0) - cue for the lose screen
    // (GameEndUI). Mirrors OnNightBossDefeated's win-screen cue exactly.
    public static event Action OnPlayerDied;

    public static void RaisePlayerDied() => OnPlayerDied?.Invoke();

    // Fired when the player's collider enters/exits the miasma fog's trigger (MiasmaFogArea, on
    // tree_1 Big) - cue for MiasmaOverlayUI to fade its screen vignette in/out. Neither side
    // needs a direct reference to the other.
    public static event Action OnPlayerEnteredMiasma;

    public static void RaisePlayerEnteredMiasma() => OnPlayerEnteredMiasma?.Invoke();

    public static event Action OnPlayerExitedMiasma;

    public static void RaisePlayerExitedMiasma() => OnPlayerExitedMiasma?.Invoke();

    // Fired whenever a Trade purchase actually succeeds (TradeManager.TryBuy), with the
    // reward item's ItemName and quantity bought. Cue for anything tracking "was this
    // specific item bought yet" without a direct reference back to TradeManager - e.g.
    // MiasmaSubQuestTracker clearing a pinned side quest once its recipe is bought.
    public static event Action<string, int> OnItemTraded;

    public static void RaiseItemTraded(string itemName, int quantity) => OnItemTraded?.Invoke(itemName, quantity);
}
