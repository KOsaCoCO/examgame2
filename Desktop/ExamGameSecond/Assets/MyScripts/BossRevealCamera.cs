using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace NTGD124
{
    // Cinematic "boss reveal/hold" beat, reused for two moments: the instant the boss spawns
    // (watches GameEvents.OnNightBossSpawned directly) and the instant it starts dying
    // (BossDeathSequence calls TriggerReveal directly, since a death only ever happens once per
    // boss instance and it already holds the transform). A dedicated CinemachineCamera (inactive
    // the rest of the time - the target's position varies every playthrough/every fight, so it's
    // positioned/aimed here at trigger time rather than pre-authored in the scene) takes over
    // from the player's own third-person camera via Cinemachine's own priority blending
    // (CinemachineBrain automatically eases to whichever active CinemachineCamera has the
    // highest priority - see revealCamera's Priority in the scene, set higher than the
    // player's), framed on the target, while the player's input is frozen (SetInputLocked) so
    // they can't move/look away mid-hold. After revealDurationSeconds, this camera goes inactive
    // again - CinemachineBrain blends straight back to the player's camera automatically, since
    // it's then the only active one left.
    //
    // Player invulnerability is NOT granted for the spawn case - the boss isn't attacking yet at
    // that point, so there's nothing to protect against, and the spawn reveal is confirmed to
    // already work correctly as a camera-only freeze. Only the death case (BossDeathSequence)
    // opts in via grantInvulnerability - during that hold, other mobs may still be sieging the
    // base while the player is frozen and can't react, which is exactly what needs protecting.
    //
    // revealCamera has no Cinemachine Body/Aim components of its own on purpose - with none
    // present, Cinemachine leaves its transform exactly as this script sets it every frame below,
    // which sidesteps needing to hand-configure an Aim behavior for a target whose position isn't
    // known until runtime and (for the death case) keeps moving throughout the hold.
    public class BossRevealCamera : MonoBehaviour
    {
        [Tooltip("The dedicated reveal CinemachineCamera - starts inactive in the scene, positioned/aimed at the target and activated here the moment a reveal is triggered.")]
        public CinemachineCamera revealCamera;

        [Tooltip("World-space offset from the target's position the reveal camera is placed at before looking back at it.")]
        public Vector3 cameraOffset = new Vector3(0f, 10f, -18f);

        public float revealDurationSeconds = 5f;

        [Tooltip("Name NightBossSpawner gives the spawned boss instance - used to find it the moment OnNightBossSpawned fires.")]
        public string bossObjectName = "NightBoss";

        private PlayerController playerController;
        private PlayerHealth playerHealth;

        void OnEnable()
        {
            GameEvents.OnNightBossSpawned += HandleNightBossSpawned;
        }

        void OnDisable()
        {
            GameEvents.OnNightBossSpawned -= HandleNightBossSpawned;
        }

        private void HandleNightBossSpawned()
        {
            GameObject boss = GameObject.Find(bossObjectName);
            if (boss == null) return;

            TriggerReveal(boss.transform);
        }

        // grantInvulnerability: only the death case (BossDeathSequence) passes true - see the
        // class comment above for why the spawn case deliberately never does.
        // restoreControlWhenDone=false lets a caller (BossDeathSequence) keep input locked (and,
        // if it also passed grantInvulnerability=true, invulnerable) past this coroutine's own
        // end - needed because GameEndUI.HandleBossDefeated() takes over ownership of "stay
        // frozen" once the win screen shows (partway through the death spectacle) and must not
        // have that undone by this routine's own unlock later - PlayerController.SetInputLocked
        // is a plain bool overwrite with no lock-owner concept, so a symmetric unlock here would
        // silently un-freeze the player during the win screen.
        // overrideDurationSeconds: null uses revealDurationSeconds (the spawn case's default,
        // 5s). BossDeathSequence passes its own longer total (pulse + burst + the extra delay
        // before the win screen shows) so the camera keeps holding on the death spot for the
        // player's entire "watch the boss die" beat, not just the shorter default - without
        // touching revealDurationSeconds itself, which the spawn case still relies on unchanged.
        public void TriggerReveal(Transform target, bool grantInvulnerability = false, bool restoreControlWhenDone = true, float? overrideDurationSeconds = null)
        {
            if (revealCamera == null || target == null) return;
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();

            StartCoroutine(RevealRoutine(target, grantInvulnerability, restoreControlWhenDone, overrideDurationSeconds ?? revealDurationSeconds));
        }

        // Re-aimed every frame, not a one-shot LookAt - the death case's target actively
        // levitates/pulses/expands throughout this hold (see BossDeathSequence), so a static
        // shot captured at the start would drift off it within a second or two. Transform's
        // Unity fake-null check naturally handles the target's own Destroy(gameObject) mid-hold
        // (the boss model is gone well before this hold ends): once destroyed, target != null
        // goes false and this simply stops re-aiming, holding on the last good frame - right
        // where the death confetti burst spawns.
        private IEnumerator RevealRoutine(Transform target, bool grantInvulnerability, bool restoreControlWhenDone, float duration)
        {
            revealCamera.gameObject.SetActive(true);
            if (playerController != null) playerController.SetInputLocked(true);
            if (grantInvulnerability && playerHealth != null) playerHealth.SetInvulnerable(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target != null)
                {
                    revealCamera.transform.position = target.position + cameraOffset;
                    revealCamera.transform.LookAt(target);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            revealCamera.gameObject.SetActive(false);
            if (restoreControlWhenDone)
            {
                if (playerController != null) playerController.SetInputLocked(false);
                if (grantInvulnerability && playerHealth != null) playerHealth.SetInvulnerable(false);
            }
        }
    }
}

// Implementation Steps:
// 1. Nothing to place manually - the reveal CinemachineCamera and this controller are both
//    already wired in the scene, watching GameEvents.OnNightBossSpawned. BossDeathSequence also
//    calls TriggerReveal directly for the death case.
// 2. Tune cameraOffset/revealDurationSeconds in the Inspector if the framing/timing needs
//    adjusting once seen in Play mode.
