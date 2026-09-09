using UnityEngine;
using TMPro;

// Numeric HP readout - lives directly on "HealthAmountTMP" (Ui's/UI-Player/Healthbar), the
// TMP text showing just the number (as opposed to HealthBarUI, which tints "Healthbar"
// itself and lives on that separate object). Driven by GameEvents.OnPlayerHealthChanged, the
// same event HealthBarUI already listens to - no per-frame polling, so every TakeDamage/Heal
// call updates this the instant it happens.
[RequireComponent(typeof(TMP_Text))]
public class HealthAmountUI : MonoBehaviour
{
    public string playerTag = "Player";

    private TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        PlayerHealth playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (playerHealth != null) SetText(playerHealth.CurrentHealth, playerHealth.maxHealth);
    }

    void OnEnable()
    {
        GameEvents.OnPlayerHealthChanged += SetText;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerHealthChanged -= SetText;
    }

    // maxHealth is unused (the number always reads as currentHealth alone, capped at 50 by
    // PlayerHealth.maxHealth itself - see TakeDamage/Heal) but kept in the signature to match
    // GameEvents.OnPlayerHealthChanged's Action<int, int> shape without a lambda wrapper.
    private void SetText(int currentHealth, int maxHealth)
    {
        text.text = currentHealth.ToString();
    }
}

// Implementation Steps:
// 1. Nothing to wire by hand - UInavigator finds/adds this component on "HealthAmountTMP"
//    automatically (same FindUIComponent trick used for every other panel), and it fetches
//    its own TMP_Text via RequireComponent.
// 2. Make sure the Player GameObject is tagged "Player" (same tag PlayerHealth/Mobai already
//    rely on) and has PlayerHealth on it.
