using UnityEngine;

// Listens for GameEvents.OnDogFound (the dog handed back to the girl - see
// GirlNpcBehavior.TurnInDog) and removes the dog quest, same shape as
// MainQuestProgress.cs does for the land quest.
public class DogQuestProgress : MonoBehaviour
{
    // By the time this fires the quest has already been renamed by DogNpcBehavior on
    // pickup (see dogQuestNameAfterPickup there) - must match that exactly, not the
    // original "Find the girl's lost dog" name it started as.
    private const string DogQuestName = "Bring the dog back to the girl";

    void OnEnable()
    {
        GameEvents.OnDogFound += HandleDogFound;
    }

    void OnDisable()
    {
        GameEvents.OnDogFound -= HandleDogFound;
    }

    private void HandleDogFound()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.RemoveQuest(DogQuestName);
        PlayerLevel.Instance?.AddExperience(20);
    }
}
