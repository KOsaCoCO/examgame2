using System.Collections;
using UnityEngine;
using TMPro;

// Shows a one-time intro message on game start, then hides itself after displaySeconds.
// Looks the panel up via an always-active parent + Transform.Find (same pattern
// InteractableHotspot uses for its own "E-Key" prompt) rather than GameObject.Find on the
// panel itself, since that fails silently if the panel happens to start inactive in the
// scene - this script forces it active regardless, so that doesn't matter either way. The
// whole show-wait-hide sequence lives in one coroutine so there's no way for the hide half
// to get silently skipped if the lookup or show step behaves unexpectedly - each step logs
// clearly, so a stuck panel is diagnosable straight from the console instead of guessing.
public class GameIntroPanel : MonoBehaviour
{
    [Tooltip("Name of the always-active parent the intro panel lives under.")]
    public string introParentName = "UI's";
    [Tooltip("Name of the intro panel itself (a direct child of introParentName) - either carries the TMP text directly or has it as a child.")]
    public string introPanelName = "UI-Intro";
    [Tooltip("How long the intro text stays up before it hides itself.")]
    public float displaySeconds = 3f;
    [TextArea(2, 4)]
    public string introText = "The tools are in the inventory, press I for inventory and double-click to equip and de-equip items.";

    void Start()
    {
        StartCoroutine(ShowThenHide());
    }

    private IEnumerator ShowThenHide()
    {
        GameObject panel = FindPanel();
        if (panel == null)
        {
            Debug.LogWarning($"[Intro] Could not find '{introPanelName}' under '{introParentName}' - nothing to show. Check introParentName/introPanelName match your hierarchy.");
            yield break;
        }

        TMP_Text text = panel.GetComponent<TMP_Text>();
        if (text == null) text = panel.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = introText;
        else Debug.LogWarning($"[Intro] '{panel.name}' has no TMP_Text on it or its children - showing the panel with whatever text it already had.");

        panel.SetActive(true);
        Debug.Log($"[Intro] Showing '{panel.name}' for {displaySeconds}s.");

        yield return new WaitForSeconds(displaySeconds);

        panel.SetActive(false);
        Debug.Log($"[Intro] Hid '{panel.name}'.");
    }

    private GameObject FindPanel()
    {
        GameObject parent = GameObject.Find(introParentName);
        if (parent == null) return null;

        Transform panelTransform = parent.transform.Find(introPanelName);
        return panelTransform != null ? panelTransform.gameObject : null;
    }
}

// Implementation Steps:
// 1. Add an empty GameObject anywhere in the scene (e.g. next to NPCManager/UInavigator)
//    and attach this script to it - a SEPARATE object from the panel itself, not the
//    panel's own GameObject (if the panel starts inactive and the script lives on it too,
//    Start() would never run at all - same class of mistake as the earlier dog prompt).
// 2. Confirm introParentName/introPanelName match your actual hierarchy - defaults assume
//    a "UI's" parent with a "UI-Intro" child carrying (or containing) the TMP text.
// 3. Press Play - check the console for "[Intro] Showing..." then, displaySeconds later,
//    "[Intro] Hid...". If instead you only see the "could not find" warning, the names
//    don't match your scene - fix the two fields rather than the hierarchy.
