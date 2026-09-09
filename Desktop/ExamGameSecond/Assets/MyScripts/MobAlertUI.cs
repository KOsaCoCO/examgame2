using System.Collections;
using UnityEngine;

// Flashing "monsters incoming" signal panel - purely a visual/audio alert, no other gameplay
// effect. Attach directly to the user-built "UI-MobAlert" holder; finds MobAlertCanvas/
// MobAlertPanel by name underneath it (same "find by name" convention as MainMenuController/
// GameEndUI) and adds a CanvasGroup to MobAlertPanel directly (none existed) so the whole panel
// can fade as one unit. Fires on GameEvents.OnNightRallyBegan (the first mob to successfully
// roll into tonight's rally) and GameEvents.OnNightBossSpawned - both play a shared alert ping
// once; the boss case additionally layers its own roar on top.
public class MobAlertUI : MonoBehaviour
{
    [Tooltip("Name of the child Canvas to find under this object.")]
    public string canvasName = "MobAlertCanvas";
    [Tooltip("Name of the child panel (under the canvas) to fade - a CanvasGroup is added to it directly if missing.")]
    public string panelName = "MobAlertPanel";

    [Header("Flash")]
    [Tooltip("How many fade-in-then-out cycles play before the panel disappears.")]
    public int flashCount = 3;
    [Tooltip("Seconds for one full fade-in-then-out cycle.")]
    public float flashCycleDuration = 0.6f;

    [Header("Sound")]
    [Tooltip("Shared alert ping - plays once for both a night rally and a boss spawn.")]
    public AudioClip alertClip;
    [Tooltip("Extra one-shot roar, played only when the boss spawns, layered on top of alertClip.")]
    public AudioClip bossRoarClip;
    [Range(0f, 1f)] public float volume = 1f;

    private GameObject panelObject;
    private CanvasGroup panelGroup;
    private Coroutine flashCoroutine;

    void Awake()
    {
        Transform canvasTransform = transform.Find(canvasName);
        Transform panelTransform = canvasTransform != null ? canvasTransform.Find(panelName) : null;
        panelObject = panelTransform != null ? panelTransform.gameObject : null;

        if (panelObject == null)
        {
            Debug.LogWarning($"MobAlertUI: no '{panelName}' found under '{canvasName}' on '{gameObject.name}'.");
            return;
        }

        panelGroup = panelObject.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = panelObject.AddComponent<CanvasGroup>();

        panelObject.SetActive(false);
    }

    void OnEnable()
    {
        GameEvents.OnNightRallyBegan += HandleNightRallyBegan;
        GameEvents.OnNightBossSpawned += HandleNightBossSpawned;
    }

    void OnDisable()
    {
        GameEvents.OnNightRallyBegan -= HandleNightRallyBegan;
        GameEvents.OnNightBossSpawned -= HandleNightBossSpawned;
    }

    private void HandleNightRallyBegan() => TriggerAlert(playBossRoar: false);

    private void HandleNightBossSpawned() => TriggerAlert(playBossRoar: true);

    private void TriggerAlert(bool playBossRoar)
    {
        if (panelObject == null || panelGroup == null) return;

        if (alertClip != null) AudioSource.PlayClipAtPoint(alertClip, transform.position, volume);
        if (playBossRoar && bossRoarClip != null) AudioSource.PlayClipAtPoint(bossRoarClip, transform.position, volume);

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    // Fades the whole panel in-then-out flashCount times (a slow beacon), then hides it again -
    // pure signaling, nothing else reacts to this.
    private IEnumerator FlashRoutine()
    {
        panelObject.SetActive(true);
        float half = flashCycleDuration * 0.5f;

        for (int i = 0; i < flashCount; i++)
        {
            yield return Fade(0f, 1f, half);
            yield return Fade(1f, 0f, half);
        }

        panelObject.SetActive(false);
        flashCoroutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            panelGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        panelGroup.alpha = to;
    }
}

// Implementation Steps:
// 1. Add this component directly to "UI-MobAlert" - it finds MobAlertCanvas/MobAlertPanel by
//    name underneath it, nothing to drag.
// 2. Assign alertClip (S_EF_CE_mine_S) and bossRoarClip (Drn_Alien_01) in the Inspector.
