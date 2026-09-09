using System.Collections;
using UnityEngine;
using TMPro;

// Listens for GameEvents to complete quests, one listener per quest with real
// completion criteria. Kept separate from QuestManager/UIQuest/UIQuestFullView
// so this wiring doesn't crowd those files - add one more HandleX method (and
// GameEvents subscription) here per quest as more get real criteria, instead
// of growing QuestManager itself.
//
// Currently just the one quest: buying the first base/land slot (GameEvents.
// OnBaseSlotUnlocked, slot 0) flashes "QUEST FINISHED" on the corner quest HUD
// for a beat, then removes the quest - the full quest view isn't touched
// directly, it just updates silently via QuestManager.OnQuestsChanged like normal.
public class MainQuestProgress : MonoBehaviour
{
    private const string BuyFirstLandQuestName = "Find your first land and try to buy it to be safe during the night";
    private const string FinishedFlashText = "QUEST FINISHED";
    private const string CornerMainQuestTextName = "TextContainMQQ(TMP)";

    [Tooltip("How long 'QUEST FINISHED' flashes on the corner HUD before the quest is removed.")]
    public float finishedFlashSeconds = 1f;

    void OnEnable()
    {
        GameEvents.OnBaseSlotUnlocked += HandleBaseSlotUnlocked;
    }

    void OnDisable()
    {
        GameEvents.OnBaseSlotUnlocked -= HandleBaseSlotUnlocked;
    }

    private void HandleBaseSlotUnlocked(int slotIndex)
    {
        if (slotIndex != 0) return;
        if (QuestManager.Instance == null) return;
        if (QuestManager.Instance.quests.Find(q => q.questName == BuyFirstLandQuestName) == null) return;

        StartCoroutine(FlashThenComplete());
    }

    private IEnumerator FlashThenComplete()
    {
        GameObject textObject = GameObject.Find(CornerMainQuestTextName);
        TMP_Text cornerText = textObject != null ? textObject.GetComponent<TMP_Text>() : null;
        if (cornerText != null) cornerText.text = FinishedFlashText;

        yield return new WaitForSeconds(finishedFlashSeconds);

        QuestManager.Instance.RemoveQuest(BuyFirstLandQuestName);
    }
}
