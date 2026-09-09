using System.Collections;
using UnityEngine;

// Drives the dog's own AudioSource.volume by distance to the player, linearly - not
// Unity's built-in 3D rolloff (logarithmic by default, awkward to tune for "very quiet
// but still audible from way out at the base, growing steadily as the player closes in").
// Lives directly on the dog prefab (self-contained: finds its own AudioSource and the
// player, no NPCManager/other-script wiring needed) so it works for both the quest-phase
// roaming dog and the post-quest companion dog automatically.
//
// Min/max distance comes from soundRange, a dedicated trigger SphereCollider living on its
// own CHILD GameObject rather than this one: NPCai.EnsureInteractionTrigger treats ANY
// existing trigger collider on its own GameObject as "the E-prompt trigger already exists"
// and skips adding its own - putting soundRange on a child sidesteps that check entirely, so
// this collider is pure sound data and never affects the dog's own physics/NavMesh/E-prompt
// interaction range. Its edge is silence, its center is the loudest point.
//
// Phase control (see SetPhaseOneActive) is a straight AudioSource.enabled toggle, driven
// by NPCManager at the two phase transitions: phase one (out at DogRoamplace, still lost)
// plays and loops automatically via Loop/Play On Awake; phase two (reactivated as the base
// companion - see NPCManager.HandleDogFound) just disables the component outright rather
// than stopping playback in code.
[RequireComponent(typeof(AudioSource))]
public class DogSoundProximity : MonoBehaviour
{
    [Tooltip("Dedicated trigger SphereCollider (on a child GameObject, not this one - see class comment) marking the dog's audible range: silent at its edge, loudest at its center. isTrigger is forced true at Awake regardless of how it's authored, so it never physically obstructs anything.")]
    public SphereCollider soundRange;

    [Tooltip("Multiplier applied to the AudioSource's own authored Volume to get the loudest volume (played at soundRange's center) - the original clip alone read as too quiet.")]
    public float centerVolumeMultiplier = 3f;

    [Tooltip("Bark clips played one at a time on a timer (see barkIntervalSeconds), cycling through in order and wrapping around - same playlist shape as SoundScapeMusic's tracks, just for a single-clip bark today.")]
    public AudioClip[] barkClips;
    [Tooltip("Seconds between one bark ending and the next one starting, while phase one is active.")]
    public float barkIntervalSeconds = 20f;

    private AudioSource dogAudio;
    private PlayerController player;
    private float maxVolume; // AudioSource's own authored Volume x centerVolumeMultiplier, clamped to 1 (Unity's volume ceiling), captured before Update ever touches it
    private Coroutine barkCoroutine;
    private int nextBarkIndex;

    void Awake()
    {
        dogAudio = GetComponent<AudioSource>();
        maxVolume = Mathf.Clamp01(dogAudio.volume * centerVolumeMultiplier);
        // Manual distance control below takes over entirely - spatialBlend 0 stops Unity's
        // own 3D falloff from compounding with (and fighting) our linear volume curve.
        dogAudio.spatialBlend = 0f;
        // Periodic re-triggering (BarkRoutine) replaces continuous looping - a bark plays once
        // every barkIntervalSeconds instead of the clip looping back-to-back forever.
        dogAudio.loop = false;
        dogAudio.playOnAwake = false;

        if (soundRange != null) soundRange.isTrigger = true; // data-only - never a physical obstacle, regardless of how it's authored
        else Debug.LogWarning($"[{name}] DogSoundProximity has no soundRange assigned - bark stays silent.");
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
    }

    void Update()
    {
        if (dogAudio == null || !dogAudio.enabled || soundRange == null) return;

        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;
        }

        Transform rangeTransform = soundRange.transform;
        Vector3 center = rangeTransform.TransformPoint(soundRange.center);
        Vector3 scale = rangeTransform.lossyScale;
        float maxDistance = soundRange.radius * Mathf.Max(scale.x, scale.y, scale.z);

        float distance = Vector3.Distance(center, player.transform.position);
        float closeness = maxDistance > 0f ? Mathf.Clamp01(1f - distance / maxDistance) : 0f;
        dogAudio.volume = maxVolume * closeness;
    }

    // Phase one (true): enables the AudioSource and starts the periodic bark coroutine. Phase
    // two (false): disables the AudioSource (silences it immediately, mid-bark or not) and
    // stops the coroutine so it doesn't keep ticking - and potentially re-enabling a disabled
    // AudioSource - in the background.
    public void SetPhaseOneActive(bool active)
    {
        if (dogAudio == null) dogAudio = GetComponent<AudioSource>();
        dogAudio.enabled = active;

        if (active)
        {
            if (barkCoroutine == null) barkCoroutine = StartCoroutine(BarkRoutine());
        }
        else if (barkCoroutine != null)
        {
            StopCoroutine(barkCoroutine);
            barkCoroutine = null;
        }
    }

    private IEnumerator BarkRoutine()
    {
        while (true)
        {
            if (barkClips != null && barkClips.Length > 0 && dogAudio != null)
            {
                dogAudio.clip = barkClips[nextBarkIndex];
                dogAudio.Play();
                nextBarkIndex = (nextBarkIndex + 1) % barkClips.Length;
            }

            yield return new WaitForSeconds(barkIntervalSeconds);
        }
    }
}

// Implementation Steps:
// 1. Add this component to the dog prefab itself (same object as its AudioSource - assign the
//    bark clip(s) to barkClips, not the AudioSource's own Clip field; Loop/Play On Awake are set
//    in code, so they don't need to be hand-checked).
// 2. Give the dog a child GameObject with a SphereCollider and drag it onto soundRange -
//    its radius (accounting for the child's own scale) is the audible distance, its center
//    the loudest point. Tune loudness via the AudioSource's own Volume field and
//    centerVolumeMultiplier together (Volume x centerVolumeMultiplier, capped at 1, is what
//    plays at soundRange's center).
// 3. Nothing else to wire up - NPCManager.SetDogSoundPhaseOne toggles this component's
//    AudioSource on/off at the two phase transitions automatically.
