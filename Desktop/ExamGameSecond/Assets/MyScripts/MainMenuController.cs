using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Continue/Restart/KeyBindings/Exit for the hand-built "UI-MainMenu" panel (under "Ui's").
// Finds its own MainMenuPanel child and the 4 buttons by name at Awake, same "nothing to drag
// in the Inspector" convention as BoostSlotManager/TradeManager's WireUi. Opened/closed
// exclusively through UInavigator's Esc handling (see its HandleEscape) - nothing else should
// call Open()/Close() directly.
public class MainMenuController : MonoBehaviour
{
    [Tooltip("Name of the child panel to show/hide (the dimmed background + the 4 buttons).")]
    public string menuPanelName = "MainMenuPanel";
    public string continueButtonName = "Continue_button";
    public string restartButtonName = "Restart_button";
    public string exitButtonName = "Exit_button";
    [Tooltip("The KeyBindings button, and the explanation panel nested directly under it (KeyBindPanel > ... > a TMP_Text) that it opens/closes.")]
    public string keyBindingsButtonName = "KeyBindings_button";
    public string keyBindPanelName = "KeyBindPanel";

    // Shown in KeyBindPanel's own TMP_Text (found anywhere in its children) - kept as one
    // literal string here rather than trying to read it back out of PlayerController, since
    // that's scattered across several Update() checks rather than one clean table.
    private const string ControlsText =
        "WASD - Move\n" +
        "Mouse - Look\n" +
        "Space - Jump\n" +
        "E - Interact\n" +
        "Right Click - Secondary Interact (chop / mine / hit)\n" +
        "1-4 - Arm Hand Slot\n" +
        "R (hovering Food in Inventory) - Eat\n" +
        "I - Inventory\n" +
        "Q - Quest Log\n" +
        "Esc - Menu / Close";

    private GameObject menuPanel;
    private GameObject keyBindPanel;

    public bool IsOpen => menuPanel != null && menuPanel.activeSelf;

    // Fired at the end of Close(), regardless of what triggered it (Continue button click or
    // Esc) - UInavigator subscribes to this to re-run RefreshCursorLock(), since Continue's own
    // click handler used to bypass UInavigator entirely and leave the cursor stuck unlocked.
    public event Action OnClosed;

    void Awake()
    {
        Transform panelTransform = transform.Find(menuPanelName);
        menuPanel = panelTransform != null ? panelTransform.gameObject : null;
        if (menuPanel == null)
        {
            Debug.LogWarning($"MainMenuController: no child named '{menuPanelName}' found under '{gameObject.name}'.");
            return;
        }

        WireButton(continueButtonName, Close);
        WireButton(restartButtonName, Restart);
        WireButton(exitButtonName, ExitGame);
        WireKeyBindings();
    }

    public void Open()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    public void Close()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        CloseKeyBindings(); // don't leave it open for the next time the menu opens
        OnClosed?.Invoke();
    }

    private void WireButton(string buttonName, UnityAction action)
    {
        Transform buttonTransform = menuPanel.transform.Find(buttonName);
        Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        if (button == null)
        {
            Debug.LogWarning($"MainMenuController: no Button named '{buttonName}' found under '{menuPanel.name}'.");
            return;
        }

        button.onClick.AddListener(action);
    }

    // KeyBindPanel sits directly under KeyBindings_button itself (not a sibling under
    // MainMenuPanel), so it's found relative to the button's own transform rather than
    // through WireButton's flat menuPanel lookup.
    private void WireKeyBindings()
    {
        Transform buttonTransform = menuPanel.transform.Find(keyBindingsButtonName);
        if (buttonTransform == null)
        {
            Debug.LogWarning($"MainMenuController: no Button named '{keyBindingsButtonName}' found under '{menuPanel.name}'.");
            return;
        }

        Button keyBindingsButton = buttonTransform.GetComponent<Button>();
        if (keyBindingsButton != null) keyBindingsButton.onClick.AddListener(OpenKeyBindings);

        Transform panelTransform = buttonTransform.Find(keyBindPanelName);
        keyBindPanel = panelTransform != null ? panelTransform.gameObject : null;
        if (keyBindPanel == null)
        {
            Debug.LogWarning($"MainMenuController: no panel named '{keyBindPanelName}' found under '{buttonTransform.name}'.");
            return;
        }

        TMP_Text explanationText = keyBindPanel.GetComponentInChildren<TMP_Text>(true);
        if (explanationText != null) explanationText.text = ControlsText;

        // KeyBindPanel's own background Image already sits on top of (and would otherwise
        // block clicks back to) KeyBindings_button once open, so it needs its own way to
        // close - click anywhere on it to dismiss, rather than relying on re-clicking the
        // button underneath it.
        Button dismissButton = keyBindPanel.GetComponent<Button>();
        if (dismissButton == null) dismissButton = keyBindPanel.AddComponent<Button>();
        dismissButton.onClick.AddListener(CloseKeyBindings);

        keyBindPanel.SetActive(false);
    }

    private void OpenKeyBindings()
    {
        if (keyBindPanel != null) keyBindPanel.SetActive(true);
    }

    private void CloseKeyBindings()
    {
        if (keyBindPanel != null) keyBindPanel.SetActive(false);
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

// Implementation Steps:
// 1. Nothing to wire by hand - UInavigator finds/adds this component on "UI-MainMenu" the
//    same way it does every other panel (FindUIComponent<T>), and this script finds its own
//    MainMenuPanel child, the 4 buttons, and KeyBindPanel's TMP_Text all by name at Awake.
// 2. UInavigator hides MainMenuPanel at Start and drives Open()/Close() itself from Esc -
//    see UInavigator.HandleEscape. Close() also closes KeyBindPanel so it doesn't stay open
//    for next time.
// 3. Continue closes the menu; Restart reloads the current scene; Exit quits (stops Play mode
//    in the Editor); KeyBindings opens KeyBindPanel (click anywhere on it to dismiss).
