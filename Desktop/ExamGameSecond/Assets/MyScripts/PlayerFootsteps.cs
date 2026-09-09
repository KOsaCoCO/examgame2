using UnityEngine;

// Loops a footstep sound while the player is actively moving (WASD held) - plays immediately
// the moment movement starts, then every stepInterval seconds while it continues, with no
// lingering/delayed step and no sound at all once movement stops (the interval timer resets
// instantly rather than finishing out its current cycle). Cycles through footstepClips in
// order (1 -> 2 -> 3 -> 4 -> 1 -> ...) rather than repeating one clip, for variation - same
// cycling shape as SoundScapeMusic's own playlist.
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Tooltip("Played in order, one per step, wrapping back to the first after the last.")]
    public AudioClip[] footstepClips;
    [Tooltip("Seconds between one footstep and the next while continuously moving.")]
    public float stepInterval = 0.5f;
    [Range(0f, 1f)] public float volume = 1f;

    private AudioSource audioSource;
    private PlayerController player;
    private float stepTimer;
    private bool wasMoving;
    private int nextClipIndex;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // always 2D - it's the player's own footsteps
    }

    void Start()
    {
        player = GetComponent<PlayerController>();
        if (player == null) player = FindAnyObjectByType<PlayerController>();
    }

    void Update()
    {
        if (player == null) return;

        // Gated on IsGrounded too, not just IsMoving - keeps footsteps silent while airborne
        // (space-bar jump/falling) or while WaterFloater is holding the player up over deep
        // water, resuming the instant either ends and the player is grounded again.
        bool isMoving = player.IsMoving && player.IsGrounded;
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

// Implementation Steps:
// 1. Add this component to the Player GameObject (the same one PlayerController lives on) and
//    assign footstepClips in the Inspector, in the order they should cycle - nothing else to wire.
