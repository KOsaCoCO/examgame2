using UnityEngine;

// Centralized combat one-shot sounds (a landed hit either direction, a mob kill) + kill confetti -
// one shared volume slider covers all of it (same "let me keep it from being too loud" ask as
// SoundScapeMusic's own slider). Same singleton shape as PlayerLevel.Instance/QuestManager.Instance -
// MobHealth/Mobai call into it directly rather than each finding/caching their own reference.
public class CombatSoundEffects : MonoBehaviour
{
    public static CombatSoundEffects Instance { get; private set; }

    [Header("Hit Sounds")]
    [Tooltip("Plays when the player lands a hit on a mob (sword or Magic Stick).")]
    public AudioClip playerHitMobClip;
    [Tooltip("Plays when a mob lands a hit on the player.")]
    public AudioClip mobHitPlayerClip;
    [Tooltip("Plays whenever the player's health actually goes down, regardless of source (mob melee, the night boss, miasma fog ticks) - layered on top of mobHitPlayerClip on a mob hit, the only source that already has its own sound.")]
    public AudioClip playerDamagedClip;

    [Header("Kill")]
    [Tooltip("Plays once when a mob dies.")]
    public AudioClip mobKilledClip;
    [Tooltip("Instantiated once at the mob's death position, alongside mobKilledClip.")]
    public GameObject killConfettiPrefab;

    [Range(0f, 1f)] public float volume = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void PlayPlayerHitMob(Vector3 position)
    {
        if (playerHitMobClip != null) AudioSource.PlayClipAtPoint(playerHitMobClip, position, volume);
    }

    public void PlayMobHitPlayer(Vector3 position)
    {
        if (mobHitPlayerClip != null) AudioSource.PlayClipAtPoint(mobHitPlayerClip, position, volume);
    }

    public void PlayPlayerDamaged(Vector3 position)
    {
        if (playerDamagedClip != null) AudioSource.PlayClipAtPoint(playerDamagedClip, position, volume);
    }

    // Plays the kill sound and spawns a one-time confetti burst at the mob's death position -
    // auto-destroyed a few seconds later rather than left to clean itself up.
    public void PlayMobKilled(Vector3 position)
    {
        if (mobKilledClip != null) AudioSource.PlayClipAtPoint(mobKilledClip, position, volume);

        if (killConfettiPrefab != null)
        {
            GameObject confetti = Instantiate(killConfettiPrefab, position, Quaternion.identity);
            Destroy(confetti, 3f);
        }
    }
}

// Implementation Steps:
// 1. Add this component to any one GameObject in the scene (e.g. alongside SoundScapeMusic under
//    World Operators) - nothing to wire, MobHealth/Mobai call CombatSoundEffects.Instance directly.
// 2. Assign playerHitMobClip/mobHitPlayerClip/mobKilledClip/killConfettiPrefab in the Inspector.
