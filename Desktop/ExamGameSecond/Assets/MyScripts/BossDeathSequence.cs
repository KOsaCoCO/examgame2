using System.Collections;
using UnityEngine;

namespace NTGD124
{
    // The boss's own death spectacle - MobHealth.Die() delegates to this instead of its usual
    // immediate kill-sound + confetti + Destroy when IsUniqueBoss is true. Sequence: freeze/
    // invulnerable the player and stop the boss's own combat/movement brain immediately (it
    // shouldn't be able to land another hit while its body is playing this out), then levitate
    // upward while pulse_bass.wav's first sequenceDurationSeconds play, pulsing (shrink/expand)
    // and flashing an FX burst every pulseIntervalSeconds, then one final expand, the exact same
    // kill sound + confetti every regular mob gets (via CombatSoundEffects.PlayMobKilled - not
    // duplicated here), and only THEN raises GameEvents.OnNightBossDefeated (the cue GameEndUI
    // listens for to show the win screen) and destroys the GameObject. Everything else that also
    // keys off OnNightBossDefeated (mob siege mode, NPC fleeing, SoundScapeMusic's boss-music
    // interrupt) is left running for the full sequence on purpose - it all clears together the
    // moment the win screen shows. Added to the boss at spawn time by NightBossSpawner, same as
    // Mobai/MobHealth/NightBossAi/BossFootsteps.
    public class BossDeathSequence : MonoBehaviour
    {
        [Header("Levitation")]
        [Tooltip("Units per second the body rises for the whole sequence.")]
        public float levitateSpeed = 3f;

        [Header("Pulse")]
        [Tooltip("Seconds between one pulse (shrink-then-expand) and the next.")]
        public float pulseIntervalSeconds = 0.5f;
        [Tooltip("How long the whole levitate/pulse phase lasts - also how much of pulseMusicClip actually plays (it's cut off here even if the clip itself is longer).")]
        public float sequenceDurationSeconds = 3f;
        [Tooltip("Fraction of the boss's current scale it shrinks to on the first half of each pulse.")]
        [Range(0.1f, 1f)] public float pulseShrinkScale = 0.85f;
        [Tooltip("Fraction of the boss's current scale it bulges out to on the second half of each pulse.")]
        public float pulseExpandScale = 1.15f;

        [Header("Final Burst")]
        [Tooltip("Scale multiplier for the one last expand right before the body vanishes.")]
        public float finalExpandScale = 1.5f;
        public float finalExpandDuration = 0.3f;

        [Header("Win Screen")]
        [Tooltip("Extra seconds to hold on the death scene (camera + confetti) after the pulse/burst animation finishes, before the win screen actually shows.")]
        public float winScreenDelaySeconds = 1f;

        [Header("Sound")]
        [Tooltip("pulse_bass.wav - only its first sequenceDurationSeconds are actually heard.")]
        public AudioClip pulseMusicClip;

        [Header("FX")]
        [Tooltip("Flash_blue_purple.prefab - spawned at the boss's current position on every pulse.")]
        public GameObject flashFxPrefab;
        public float flashFxLifetime = 1.5f;

        private AudioSource audioSource;
        private Vector3 baseScale;

        void Awake()
        {
            // Explicitly its own dedicated AudioSource, not [RequireComponent] - the boss already
            // carries a separate AudioSource for BossFootsteps, and sharing one would make them
            // fight over clip/loop state (see BossFootsteps' own Awake for the full reasoning).
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
        }

        public void PlayDeathSequence()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // The boss's own combat/movement brain is removed outright (not just disabled -
            // disabling a component doesn't stop its already-running coroutines) so
            // BehaviourLoopRoutine can never call MoveAndAttackPlayer/MoveAndAttackStructure
            // again while this sequence plays out.
            NightBossAi bossAi = GetComponent<NightBossAi>();
            if (bossAi != null) Destroy(bossAi);

            // Reuses BossRevealCamera's own camera hold for the death case too - see its class
            // comment for why grantInvulnerability/restoreControlWhenDone are set the way they
            // are here (in short: only death protects the player, and GameEndUI takes over
            // "stay frozen" ownership once the win screen shows, so this must not auto-restore).
            // Duration is overridden to cover the whole spectacle (pulse + burst) plus
            // winScreenDelaySeconds, so the camera keeps holding on the death spot for the full
            // "watch it die" beat instead of reverting to the player's own view before the win
            // screen even shows.
            BossRevealCamera revealCamera = FindAnyObjectByType<BossRevealCamera>();
            if (revealCamera != null)
            {
                float holdDuration = sequenceDurationSeconds + finalExpandDuration + winScreenDelaySeconds;
                revealCamera.TriggerReveal(transform, grantInvulnerability: true, restoreControlWhenDone: false, overrideDurationSeconds: holdDuration);
            }

            StartCoroutine(DeathSequenceRoutine());
        }

        private IEnumerator DeathSequenceRoutine()
        {
            baseScale = transform.localScale;

            audioSource.clip = pulseMusicClip;
            audioSource.Play();

            float elapsed = 0f;
            float pulseTimer = 0f;
            while (elapsed < sequenceDurationSeconds)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
                pulseTimer += delta;

                transform.position += Vector3.up * levitateSpeed * delta;

                if (pulseTimer >= pulseIntervalSeconds)
                {
                    pulseTimer -= pulseIntervalSeconds;
                    StartCoroutine(PulseOnce());
                    SpawnFlash();
                }

                yield return null;
            }

            // Cuts pulse_bass off here even if the clip itself runs longer - "only the initial
            // sequenceDurationSeconds" is the whole point of an explicit Stop() rather than just
            // letting it play out.
            audioSource.Stop();

            yield return FinalExpandAndVanish();
        }

        private IEnumerator PulseOnce()
        {
            float halfDuration = pulseIntervalSeconds * 0.5f;
            Vector3 shrunk = baseScale * pulseShrinkScale;
            Vector3 expanded = baseScale * pulseExpandScale;

            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(baseScale, shrunk, elapsed / halfDuration);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(shrunk, expanded, elapsed / halfDuration);
                yield return null;
            }

            transform.localScale = baseScale;
        }

        private void SpawnFlash()
        {
            if (flashFxPrefab == null) return;

            GameObject flash = Instantiate(flashFxPrefab, transform.position, Quaternion.identity);
            Destroy(flash, flashFxLifetime);
        }

        private IEnumerator FinalExpandAndVanish()
        {
            float elapsed = 0f;
            Vector3 burstScale = baseScale * finalExpandScale;
            while (elapsed < finalExpandDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(baseScale, burstScale, elapsed / finalExpandDuration);
                yield return null;
            }

            // Same kill sound + confetti burst every regular mob gets on death (CombatSoundEffects
            // already has both wired) - not duplicated here as separate fields.
            CombatSoundEffects.Instance?.PlayMobKilled(transform.position);

            // A couple extra seconds to actually watch the death scene (camera still held on the
            // confetti burst - see the overridden hold duration in PlayDeathSequence) before the
            // win screen interrupts it.
            yield return new WaitForSeconds(winScreenDelaySeconds);

            // Invulnerability is NOT lifted here - BossRevealCamera's own hold (triggered above
            // in PlayDeathSequence with restoreControlWhenDone: false) is the sole authority for
            // when it turns back off, and it deliberately never does for the death case: the
            // player stays frozen + invulnerable straight through into the win screen GameEndUI
            // shows on the very next line, matching its own "stay frozen" intent exactly.
            GameEvents.RaiseNightBossDefeated();
            Destroy(gameObject);
        }
    }
}

// Implementation Steps:
// 1. Nothing to place manually - NightBossSpawner adds this component to the boss instance at
//    spawn time and assigns pulseMusicClip/flashFxPrefab from its own Inspector-configured
//    BossDeathPulseMusicClip/BossDeathFlashPrefab.
// 2. MobHealth.Die() calls PlayDeathSequence() on this component instead of its usual immediate
//    destroy path whenever IsUniqueBoss is true - see MobHealth.cs.
