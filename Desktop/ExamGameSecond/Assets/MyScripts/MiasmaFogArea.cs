using System.Collections;
using UnityEngine;

namespace NTGD124
{
    // Detects the player standing inside the "Redish Fog" particle system's area (a child of
    // tree_1 Big - see MiasmaTree.cs, which deactivates tree_1 Big once the miasma is cleared,
    // taking the fog - and this trigger, as its child - down with it) and deals periodic damage
    // while they're inside, unless they're currently wearing the Miasma Protection Cloak in the
    // Chestplate boost slot (PlayerHealth.IsImmuneToMiasma - see MiasmaCloakTradeOffer). Raises
    // GameEvents.OnPlayerEnteredMiasma/OnPlayerExitedMiasma so MiasmaOverlayUI can react without
    // a direct reference back here.
    //
    // Lives directly on "Redish Fog" itself (not tree_1 Big) so the trigger tracks the fog's own
    // authored position/rotation exactly. Adds its own BoxCollider rather than a SphereCollider -
    // Redish Fog's transform has an extreme non-uniform scale (roughly 41x21x49, stretched to
    // visually cover a wide area), which a BoxCollider handles correctly (each axis scales
    // independently, matching the fog's real stretched footprint) where a sphere would badly
    // distort.
    public class MiasmaFogArea : MonoBehaviour
    {
        [Header("Detection")]
        public string playerTag = "Player";
        [Tooltip("Local-space size of this component's own trigger BoxCollider - (1,1,1) fills Redish Fog's full authored scale (roughly 41x21x49 world units). Shrink this if the trigger reads as bigger than the fog's actual visible density.")]
        public Vector3 fogColliderSize = Vector3.one;
        [Tooltip("Local-space offset for the trigger Collider's center, in case the fog's own pivot isn't where the trigger should be centered.")]
        public Vector3 fogColliderCenter = Vector3.zero;

        [Header("Damage")]
        [Tooltip("Seconds between each hit while standing in the fog - the first hit lands after this interval too, not instantly on entry.")]
        public float damageTickSeconds = 2f;
        [Tooltip("Hits dealt per tick.")]
        public int damagePerTick = 1;

        [Header("Diagnostics")]
        [Tooltip("Temporary - kicks the camera (PlayerController.TriggerCameraJolt) on every damage tick so it's visually obvious the tick loop is actually reaching TakeDamage, independent of whatever the health bar shows.")]
        public bool cameraJoltOnDamage = true;

        private PlayerHealth playerHealth;
        private PlayerController playerController;
        private Coroutine damageCoroutine;
        private bool playerInside;

        void Awake()
        {
            BoxCollider fogTrigger = gameObject.AddComponent<BoxCollider>();
            fogTrigger.isTrigger = true;
            fogTrigger.size = fogColliderSize;
            fogTrigger.center = fogColliderCenter;
        }

        void Start()
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            playerController = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

            // If either of these logs "NOT found", that's the whole bug: the damage/jolt
            // block below is gated on playerHealth != null, so a null reference here means
            // TakeDamage and TriggerCameraJolt silently never run, even though OnTriggerEnter/
            // Exit (and the corner tint they drive via GameEvents) fire independently of this.
            Debug.Log($"[Miasma] Start: playerHealth {(playerHealth != null ? "found (" + playerHealth.gameObject.name + ")" : "NOT found")}, " +
                      $"playerController {(playerController != null ? "found (" + playerController.gameObject.name + ")" : "NOT found")}");
        }

        // Covers SetActive(false) on this GameObject or a parent (MiasmaTree.HandleHit
        // deactivating tree_1 Big once felled, taking this child down with it) - trigger-exit
        // isn't reliably guaranteed to fire in that case, same reasoning as
        // InteractableHotspot.OnDisable.
        void OnDisable()
        {
            if (!playerInside) return;
            EndExposure();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag) || playerInside) return;

            Debug.Log($"[Miasma] OnTriggerEnter: '{other.gameObject.name}' entered the fog - starting damage tick routine.");
            playerInside = true;
            GameEvents.RaisePlayerEnteredMiasma();
            damageCoroutine = StartCoroutine(DamageTickRoutine());
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            Debug.Log($"[Miasma] OnTriggerExit: '{other.gameObject.name}' left the fog.");
            EndExposure();
        }

        private void EndExposure()
        {
            playerInside = false;
            if (damageCoroutine != null) StopCoroutine(damageCoroutine);
            damageCoroutine = null;
            GameEvents.RaisePlayerExitedMiasma();
        }

        private IEnumerator DamageTickRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(damageTickSeconds);

                if (playerHealth == null)
                {
                    Debug.LogWarning("[Miasma] Tick fired but playerHealth is null - TakeDamage/TriggerCameraJolt cannot run. " +
                                      "Check the Start() log above: was PlayerHealth found in the scene?");
                    continue;
                }

                bool immune = playerHealth.IsImmuneToMiasma;
                Debug.Log($"[Miasma] Tick fired. IsImmuneToMiasma={immune}, currentHealth={playerHealth.CurrentHealth}");

                if (!immune)
                {
                    playerHealth.TakeDamage(damagePerTick);
                    Debug.Log($"[Miasma] TakeDamage({damagePerTick}) called - health now {playerHealth.CurrentHealth}.");

                    if (cameraJoltOnDamage)
                    {
                        if (playerController != null) playerController.TriggerCameraJolt();
                        else Debug.LogWarning("[Miasma] cameraJoltOnDamage is on but playerController is null - jolt skipped.");
                    }
                }
            }
        }
    }
}

// Implementation Steps:
// 1. Add this component directly to "Redish Fog" (Environment/tree_1 Big/Redish Fog) - it adds
//    its own BoxCollider automatically, no manual Collider setup needed.
// 2. Tune fogColliderSize/fogColliderCenter in the Editor if the default (1,1,1) - the fog's
//    full authored scale - reads as bigger than where the fog actually looks dense.
// 3. Tune damageTickSeconds/damagePerTick if 2s/1 hit needs adjusting.
// 4. Nothing else to wire - GameEvents.OnPlayerEnteredMiasma/OnPlayerExitedMiasma drive
//    MiasmaOverlayUI automatically, and PlayerHealth.IsImmuneToMiasma (true only while the
//    Miasma Protection Cloak is equipped in the Chestplate boost slot) is checked every tick.
