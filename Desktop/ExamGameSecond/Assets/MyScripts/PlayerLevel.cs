using UnityEngine;

// Tracks the player's cumulative experience and derived level (1-10), same singleton shape as
// QuestManager.Instance. Experience is granted by other systems calling AddExperience directly
// (MobHealth on a kill, HarvestableResource on a Wood/Rock hit, DogQuestProgress/
// WizardNpcBehavior on a main-quest turn-in) rather than routed through GameEvents, since
// nothing else needs to react to "experience was gained" - only "the level changed" is
// broadcast, for the level UI text.
public class PlayerLevel : MonoBehaviour
{
    public static PlayerLevel Instance { get; private set; }

    public const int MaxLevel = 10;

    public int CurrentLevel { get; private set; } = 1;
    public int CurrentExperience { get; private set; } = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        GameEvents.RaisePlayerLevelChanged(CurrentLevel);
    }

    // Level N (2-10) requires N*10 cumulative experience to reach - level 2 needs 20, level 3
    // needs 30, ... level 10 needs 100. Level 1 is the free starting level (0 experience).
    public static int ExperienceRequiredForLevel(int level) => level * 10;

    public void AddExperience(int amount)
    {
        if (amount <= 0 || CurrentLevel >= MaxLevel) return;

        CurrentExperience += amount;

        bool leveledUp = false;
        while (CurrentLevel < MaxLevel && CurrentExperience >= ExperienceRequiredForLevel(CurrentLevel + 1))
        {
            CurrentLevel++;
            leveledUp = true;
        }

        if (leveledUp) GameEvents.RaisePlayerLevelChanged(CurrentLevel);
    }
}

// Implementation Steps:
// 1. Add this component to an empty GameObject in the scene (same pattern as QuestManager) -
//    only one should exist; a duplicate self-destroys via the Instance guard.
// 2. Experience sources are already wired: MobHealth (Easy=3, Medium=5, Hard=8 on kill),
//    HarvestableResource (1 per Wood/Rock hit), DogQuestProgress (20 on delivering the dog),
//    WizardNpcBehavior (50 on the miasma turn-in).
// 3. To grant experience from anywhere else, call PlayerLevel.Instance?.AddExperience(amount).
// 4. See PlayerLevelUI.cs for the "player lvl : N" text display.
