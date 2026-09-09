using UnityEngine;
using UnityEngine.AI;

namespace NTGD124
{
    // Same cycling-footstep-loop shape as PlayerFootsteps (plays immediately the moment
    // movement starts, then every stepInterval seconds while it continues, cycling through
    // footstepClips in order) - reads NavMeshAgent.velocity directly instead of a
    // PlayerController.IsMoving property, since the boss has no such property to read from.
    // Added to the boss at spawn time by NightBossSpawner, same as Mobai/MobHealth/NightBossAi.
    public class BossFootsteps : MonoBehaviour
    {
        [Tooltip("Played in order, one per step, wrapping back to the first after the last.")]
        public AudioClip[] footstepClips;
        [Tooltip("Seconds between one footstep and the next while continuously moving.")]
        public float stepInterval = 0.5f;
        [Tooltip("Minimum NavMeshAgent speed (units/sec) to count as actively moving.")]
        public float movementThreshold = 0.1f;
        [Range(0f, 1f)] public float volume = 1f;

        private AudioSource audioSource;
        private NavMeshAgent agent;
        private float stepTimer;
        private bool wasMoving;
        private int nextClipIndex;

        void Awake()
        {
            // Explicitly adds its own dedicated AudioSource rather than [RequireComponent] +
            // GetComponent - the boss also gets a separate BossDeathSequence component with its
            // own AudioSource, and [RequireComponent] would just reuse whichever one already
            // exists, making the two fight over the same source's clip/loop state. Same pattern
            // MagicStickAbility already uses for this exact reason.
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f; // 3D - unlike the player's own footsteps, this should be heard coming from wherever the boss actually is
        }

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        void Update()
        {
            if (agent == null) return;

            bool isMoving = agent.isOnNavMesh && agent.velocity.magnitude > movementThreshold;
            if (isMoving && !wasMoving)
            {
                // Movement just started - step immediately rather than waiting out a full interval.
                PlayStep();
                stepTimer = stepInterval;
            }
            else if (isMoving)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f)
                {
                    PlayStep();
                    stepTimer = stepInterval;
                }
            }
            else
            {
                stepTimer = 0f; // stopped - next movement start steps immediately again, no carryover
            }

            wasMoving = isMoving;
        }

        private void PlayStep()
        {
            if (footstepClips == null || footstepClips.Length == 0) return;

            AudioClip clip = footstepClips[nextClipIndex];
            if (clip != null) audioSource.PlayOneShot(clip, volume);
            nextClipIndex = (nextClipIndex + 1) % footstepClips.Length;
        }
    }
}

// Implementation Steps:
// 1. Nothing to place manually - NightBossSpawner adds this component to the boss instance at
//    spawn time and assigns footstepClips from its own Inspector-configured BossFootstepClips.
