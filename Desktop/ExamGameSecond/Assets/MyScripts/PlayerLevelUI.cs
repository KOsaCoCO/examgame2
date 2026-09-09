using UnityEngine;
using TMPro;

// Lives directly on the "level" TMP text object (UI's/UI-Player in the Canvas). Same shape as
// HealthBarUI: reacts to GameEvents.OnPlayerLevelChanged rather than polling every frame.
[RequireComponent(typeof(TMP_Text))]
public class PlayerLevelUI : MonoBehaviour
{
    [Tooltip("{0} is replaced with the player's current level.")]
    public string textFormat = "player lvl : {0}";

    private TMP_Text label;

    void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    void Start()
    {
        int startingLevel = PlayerLevel.Instance != null ? PlayerLevel.Instance.CurrentLevel : 1;
        SetLevelText(startingLevel);
    }

    void OnEnable()
    {
        GameEvents.OnPlayerLevelChanged += SetLevelText;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerLevelChanged -= SetLevelText;
    }

    private void SetLevelText(int level)
    {
        label.text = string.Format(textFormat, level);
    }
}

// Implementation Steps:
// 1. Add this component to the "level" TMP text GameObject itself (UI's/UI-Player in the
//    Canvas) - it fetches its own TMP_Text via RequireComponent, nothing to drag in.
// 2. Make sure a PlayerLevel component exists somewhere in the scene (see PlayerLevel.cs).
// 3. Tune textFormat if "player lvl : N" needs a different layout - {0} is the level number.
