using System.Collections;
using UnityEngine;

// Win/lose screens for the "UI-GameOver_Win" panel the user hand-built (GameWinCanvas/
// GameOverCanvas, each already authored active-by-default) - same "find named child, cache it,
// SetActive toggle" convention as MainMenuController. Both canvases are force-hidden at Start
// (the scene authors them visible by default) and shown only by GameEvents.OnNightBossDefeated/
// OnPlayerDied. Either way, the user's own Main Menu (Restart/Exit) opens automatically a few
// seconds after the win/game-over screen shows, since neither panel has its own restart/
// continue button.
public class GameEndUI : MonoBehaviour
{
    [Tooltip("Name of the GameWinCanvas child to show on GameEvents.OnNightBossDefeated.")]
    public string winCanvasName = "GameWinCanvas";
    [Tooltip("Name of the GameOverCanvas child to show on GameEvents.OnPlayerDied.")]
    public string loseCanvasName = "GameOverCanvas";
    [Tooltip("Instantiated as a child of Camera.main, positioned a couple units in front of it, when the win screen shows.")]
    public GameObject confettiPrefab;
    [Tooltip("Seconds after the win or game-over screen shows before the Main Menu opens automatically, letting the player Restart or Exit.")]
    public float mainMenuDelaySeconds = 3f;

    private GameObject winCanvas;
    private GameObject loseCanvas;

    void Start()
    {
        Transform winTransform = transform.Find(winCanvasName);
        Transform loseTransform = transform.Find(loseCanvasName);
        winCanvas = winTransform != null ? winTransform.gameObject : null;
        loseCanvas = loseTransform != null ? loseTransform.gameObject : null;

        if (winCanvas == null) Debug.LogWarning($"GameEndUI: no child named '{winCanvasName}' found under '{gameObject.name}'.");
        if (loseCanvas == null) Debug.LogWarning($"GameEndUI: no child named '{loseCanvasName}' found under '{gameObject.name}'.");

        if (winCanvas != null) winCanvas.SetActive(false);
        if (loseCanvas != null) loseCanvas.SetActive(false);
    }

    void OnEnable()
    {
        GameEvents.OnNightBossDefeated += HandleBossDefeated;
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    void OnDisable()
    {
        GameEvents.OnNightBossDefeated -= HandleBossDefeated;
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    private void HandleBossDefeated()
    {
        if (winCanvas != null) winCanvas.SetActive(true);
        SpawnConfetti();
        FreezePlayer();
        StartCoroutine(OpenMainMenuAfterDelay());
    }

    private void HandlePlayerDied()
    {
        if (loseCanvas != null) loseCanvas.SetActive(true);
        FreezePlayer();
        StartCoroutine(OpenMainMenuAfterDelay());
    }

    private IEnumerator OpenMainMenuAfterDelay()
    {
        yield return new WaitForSeconds(mainMenuDelaySeconds);

        MainMenuController mainMenu = FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (mainMenu != null) mainMenu.Open();
    }

    private void SpawnConfetti()
    {
        if (confettiPrefab == null || Camera.main == null) return;

        GameObject confetti = Instantiate(confettiPrefab, Camera.main.transform);
        confetti.transform.localPosition = new Vector3(0f, 0f, 2f);
        confetti.transform.localRotation = Quaternion.identity;
    }

    private void FreezePlayer()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.SetInputLocked(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

// Implementation Steps:
// 1. Add this component directly to "UI-GameOver_Win" (the parent of GameWinCanvas/
//    GameOverCanvas) - it finds its own two children by name, nothing to drag.
// 2. Assign confettiPrefab (e.g. Assets/FreeAssets/Lana Studio/Hyper Casual FX/Prefabs/
//    Confetti/Confetti_blast_multicolor.prefab) in the Inspector for the win-screen burst -
//    leave it null to skip the confetti entirely.
