using System.Collections;
using UnityEngine;
using NTGD124;

// Magic Stick's E-press ability - a true instant AoE burst (not a lingering damage zone):
// expands a visual radius from the player, hits every mob within it once, plays a sound, then
// starts a cooldown during which the item's own hand-slot icon flips from yellow (ready) to
// red (cooling down) via PlayerHandManager.SetSlotVariant, back to yellow the instant the
// cooldown ends. Listens directly on PlayerController.onInteract (fires unconditionally on
// every E press, independent of any InteractableHotspot's own proximity gating) rather than
// through a hotspot, since this isn't tied to being near any specific object.
public class MagicStickAbility : MonoBehaviour
{
    private const string MagicStickItemName = "Magic Stick";
    private const string ReadyVariant = "";
    private const string CooldownVariant = "Cooldown";

    [Header("Ability")]
    public float radius = 10f;
    public float cooldownSeconds = 3f;

    [Header("Visual")]
    [Tooltip("Instantiated at the player's position when cast, as the expanding-radius visual.")]
    public GameObject castFxPrefab;
    [Tooltip("Uniform scale applied to castFxPrefab - eyeball this against the real radius in the Editor, there's no reliable way to compute a particle system's true footprint from source data alone.")]
    public float fxScale = 1f;
    [Tooltip("Seconds before the cast FX instance is destroyed.")]
    public float fxLifetime = 2f;

    [Header("Sound")]
    public AudioClip castClip;
    [Range(0f, 1f)] public float volume = 1f;

    private PlayerController player;
    private PlayerHandManager hand;
    private AudioSource audioSource;
    private float cooldownRemaining;

    // Lazy, retrying lookup rather than a one-time Start() fetch - PlayerHandManager doesn't
    // pre-exist in the scene, it's only created at runtime by UInavigator.Start()
    // (AddComponent<PlayerHandManager>()), and with no ScriptExecutionOrder asset in the project
    // there's no guarantee that runs before this component's own Start(). A one-time fetch that
    // lost that race cached null forever, silently no-oping every E-press with no sound, no
    // damage, and no error - same bug PlayerHealth.BoostSlots already hit and fixed this way.
    private PlayerHandManager Hand => hand != null
        ? hand
        : (hand = FindAnyObjectByType<PlayerHandManager>(FindObjectsInactive.Include));

    void Start()
    {
        player = GetComponent<PlayerController>();
        if (player == null) player = FindAnyObjectByType<PlayerController>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (player != null) player.onInteract.AddListener(TryCast);
    }

    void Update()
    {
        if (cooldownRemaining <= 0f) return;

        cooldownRemaining -= Time.deltaTime;
        if (cooldownRemaining <= 0f)
        {
            cooldownRemaining = 0f;
            SetIconVariant(ReadyVariant);
        }
    }

    private void TryCast()
    {
        if (Hand == null || player == null) return;
        if (!Hand.IsArmedWithItem(MagicStickItemName)) return;
        if (cooldownRemaining > 0f) return;

        Cast();

        cooldownRemaining = cooldownSeconds;
        SetIconVariant(CooldownVariant);
    }

    private void Cast()
    {
        Vector3 origin = player.transform.position;

        if (castFxPrefab != null)
        {
            GameObject fx = Instantiate(castFxPrefab, origin, Quaternion.identity);
            fx.transform.localScale = Vector3.zero;
            StartCoroutine(AnimateFx(fx));
        }

        if (castClip != null) audioSource.PlayOneShot(castClip, volume);

        Collider[] hits = Physics.OverlapSphere(origin, radius);
        foreach (Collider hit in hits)
        {
            MobHealth mob = hit.GetComponent<MobHealth>();
            if (mob != null) mob.ApplyMagicHit(origin);
        }
    }

    private void SetIconVariant(string variant)
    {
        if (Hand == null) return;

        int slotIndex = Hand.FindSlotIndex(MagicStickItemName);
        if (slotIndex >= 0) Hand.SetSlotVariant(slotIndex, variant);
    }

    // Grows the FX from nothing out to fxScale over the first half of fxLifetime, then shrinks
    // it back to nothing over the second half, so the visual radius actually expands and
    // contracts (matching the real instant OverlapSphere check's 5-unit reach) instead of just
    // popping in at a flat size - the hit-check itself already happened synchronously in Cast(),
    // this is purely the visual follow-through.
    private IEnumerator AnimateFx(GameObject fx)
    {
        float halfDuration = fxLifetime * 0.5f;

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            fx.transform.localScale = Vector3.one * fxScale * (elapsed / halfDuration);
            yield return null;
        }
        fx.transform.localScale = Vector3.one * fxScale;

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            fx.transform.localScale = Vector3.one * fxScale * (1f - elapsed / halfDuration);
            yield return null;
        }

        Destroy(fx);
    }
}

// Implementation Steps:
// 1. Add this component to the Player GameObject (the same one PlayerController lives on).
// 2. Assign castFxPrefab (Area_magic_multicolor.prefab) and castClip (Mgc_Fire_Throw_01) in the
//    Inspector, and tune fxScale by eye once you can see it in Play mode against the real
//    5-unit radius.
// 3. Add two ItemIconCatalog rows for "Magic Stick" in the scene - a default (empty Variant)
//    yellow tint and a "Cooldown" Variant red tint - so the hand slot's icon actually flips
//    color; nothing here renders that itself, it only calls PlayerHandManager.SetSlotVariant.
