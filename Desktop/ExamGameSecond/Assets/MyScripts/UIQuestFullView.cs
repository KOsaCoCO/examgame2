using UnityEngine;
using TMPro;

// The expanded quest view. Shows the same live quest sub text as the
// corner UIQuest HUD, in its own TextContainMQ / TextContainSQ fields.
// Never touches the "Main quest"/"Side Quest" titles.
public class UIQuestFullView : MonoBehaviour
{
    private const string MainQuestTextName = "TextContainMQ(TMP)";
    private const string SideQuestTextName = "TextContainSQ(TMP)";

    private TMP_Text mainQuestSubText;
    private TMP_Text sideQuestSubText;

    void OnEnable()
    {
        if (mainQuestSubText == null) mainQuestSubText = QuestUIHelper.FindSubText(transform, MainQuestTextName);
        if (sideQuestSubText == null) sideQuestSubText = QuestUIHelper.FindSubText(transform, SideQuestTextName);

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged += Refresh;

        Refresh();
    }

    void OnDisable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged -= Refresh;
    }

    public void Refresh()
    {
        if (QuestManager.Instance == null) return;
        if (mainQuestSubText != null) mainQuestSubText.text = QuestManager.Instance.GetMainQuestText();
        if (sideQuestSubText != null) sideQuestSubText.text = QuestManager.Instance.GetSideQuestText();
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
    public void Toggle() { gameObject.SetActive(!gameObject.activeSelf); }
}
