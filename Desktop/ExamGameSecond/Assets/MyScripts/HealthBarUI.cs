using UnityEngine;
using UnityEngine.UI;

// Lives directly on the "Healthbar" RawImage (UI's/UI-Player in the Canvas). Tints it across a
// healthyColor -> midColor -> lowHealthColor gradient as PlayerHealth.CurrentHealth drops
// toward 0 (driven by GameEvents.OnPlayerHealthChanged, no per-frame polling for the target),
// smoothly animating toward that target color every frame rather than snapping to it.
[RequireComponent(typeof(RawImage))]
public class HealthBarUI : MonoBehaviour
{
    public string playerTag = "Player";
    public Color healthyColor = new Color(0.3f, 0.6f, 0.3f); // dulled green, full Color.green was too bright
    public Color midColor = new Color(0.8f, 0.45f, 0f); // dulled orange
    public Color lowHealthColor = new Color(0.6f, 0.15f, 0.15f); // dulled red
    public float transitionSpeed = 3f;

    private RawImage image;
    private Color targetColor;

    void Awake()
    {
        image = GetComponent<RawImage>();
        targetColor = healthyColor;
    }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        PlayerHealth playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (playerHealth != null) SetTargetColor(playerHealth.CurrentHealth, playerHealth.maxHealth);
        image.color = targetColor;
    }

    void OnEnable()
    {
        GameEvents.OnPlayerHealthChanged += SetTargetColor;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerHealthChanged -= SetTargetColor;
    }

    void Update()
    {
        image.color = Color.Lerp(image.color, targetColor, Time.deltaTime * transitionSpeed);
    }

    // healthy->mid over the top half of the health pool, mid->low over the bottom half - just
    // sets the target, the actual on-screen color eases toward it in Update.
    private void SetTargetColor(int currentHealth, int maxHealth)
    {
        float remainingFraction = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
        targetColor = remainingFraction >= 0.5f
            ? Color.Lerp(midColor, healthyColor, (remainingFraction - 0.5f) * 2f)
            : Color.Lerp(lowHealthColor, midColor, remainingFraction * 2f);
    }
}

// Implementation Steps:
// 1. Add this component to the "Healthbar" RawImage GameObject itself (UI's/UI-Player in the
//    Canvas) - it fetches its own RawImage via RequireComponent, nothing to drag in.
// 2. Make sure the Player GameObject is tagged "Player" (same tag Mobai/PlayerHealth already
//    rely on) and has PlayerHealth on it.
// 3. Tune healthyColor/midColor/lowHealthColor for the gradient stops (kept deliberately
//    dulled/muted rather than pure Color.green/red - full brightness read as too bright) and
//    transitionSpeed for how quickly the bar eases to its new color (higher = snappier).
