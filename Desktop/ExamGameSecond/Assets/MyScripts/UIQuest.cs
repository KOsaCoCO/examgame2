using UnityEngine;
using TMPro;

// The compact quest HUD that sits in the corner of the screen.
// Only edits the live sub text fields (TextContainMQQ / TextContainSQQ) -
// never touches the "Main quest"/"Side Quest" titles.
// Since it's a small side bar, long quest text is clipped with "..." here;
// the full text is only shown in UIQuestFullView.
public class UIQuest : MonoBehaviour
{
    private const string MainQuestTextName = "TextContainMQQ(TMP)";
    private const string SideQuestTextName = "TextContainSQQ(TMP)";

    private TMP_Text mainQuestSubText;
    private TMP_Text sideQuestSubText;

    void OnEnable()
    {
        if (mainQuestSubText == null) mainQuestSubText = QuestUIHelper.FindSubText(transform, MainQuestTextName);
        if (sideQuestSubText == null) sideQuestSubText = QuestUIHelper.FindSubText(transform, SideQuestTextName);

        if (mainQuestSubText != null) mainQuestSubText.overflowMode = TextOverflowModes.Ellipsis;
        if (sideQuestSubText != null) sideQuestSubText.overflowMode = TextOverflowModes.Ellipsis;

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
