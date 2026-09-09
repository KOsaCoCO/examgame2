using System.Collections;
using UnityEngine;

// Background music playlist for the "SoundScape" object (under World Operators). Own
// AudioSource plays through Tracks in order, one at a time: waits firstTrackDelay once at
// game start, fades in, plays the clip to the end, fades out, waits trackGapDelay, then
// moves to the next track - wrapping back to index 0 after the last one, from then on only
// ever waiting trackGapDelay between tracks (firstTrackDelay never fires again). Volume is a
// [Range] field so it's a drag-slider right on the component in the Inspector - takes effect
// live, even mid-track. The instant the boss spawns, this interrupts whatever's currently
// playing and switches to bossMusicClip until the fight ends (GameEvents.OnNightBossSpawned/
// OnNightBossDefeated/OnDayBegan) - a real track swap, not just a volume duck, and it plays
// through this same AudioSource/GameObject rather than a separate positional source on the
// boss itself.
[RequireComponent(typeof(AudioSource))]
public class SoundScapeMusic : MonoBehaviour
{
    [Header("Playlist")]
    [Tooltip("Music tracks, played in order and looped back to the start once the last one finishes.")]
    public AudioClip[] tracks;

    [Header("Timing")]
    [Tooltip("Seconds to wait after the game starts before the first track begins - only ever happens once.")]
    public float firstTrackDelay = 120f;
    [Tooltip("Seconds of silence between one track ending and the next one beginning, including the wrap from the last track back to the first, and after the boss fight ends before the playlist resumes.")]
    public float trackGapDelay = 300f;

    [Header("Fade")]
    [Tooltip("Seconds spent fading in at the start of a track and fading out at the end of it.")]
    public float fadeDuration = 2f;

    [Header("Boss Music")]
    [Tooltip("Interrupts whichever regular track is playing the instant the boss spawns, plays looped until the fight ends, then the regular playlist resumes where it left off.")]
    public AudioClip bossMusicClip;
    [Tooltip("Deliberately shorter than fadeDuration - an interrupt should feel snappier/more urgent than a normal track transition.")]
    public float bossMusicFadeDuration = 2f;

    [Header("Volume")]
    [Range(0f, 1f)]
    [Tooltip("Target music volume once faded in - drag this to check/adjust game volume. Takes effect live, even mid-track. Boss music obeys this same slider.")]
    public float volume = 1f;

    public static SoundScapeMusic Instance { get; private set; }

    private AudioSource audioSource;
    private int trackIndex;
    private Coroutine playlistCoroutine;
    private Coroutine bossMusicCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // background music - always 2D, never positional
        audioSource.volume = 0f;
    }

    void Start()
    {
        if (tracks != null && tracks.Length > 0) playlistCoroutine = StartCoroutine(PlaylistLoop());
    }

    void OnEnable()
    {
        GameEvents.OnNightBossSpawned += HandleNightBossSpawned;
        GameEvents.OnNightBossDefeated += HandleBossFightEnded;
        GameEvents.OnDayBegan += HandleBossFightEnded;
    }

    void OnDisable()
    {
        GameEvents.OnNightBossSpawned -= HandleNightBossSpawned;
        GameEvents.OnNightBossDefeated -= HandleBossFightEnded;
        GameEvents.OnDayBegan -= HandleBossFightEnded;
    }

    // Real interrupt, not a duck - stops the regular playlist coroutine outright (whatever track
    // was mid-fade/mid-sustain just gets abandoned there, same trackIndex, so resuming later
    // naturally replays it from the start) and starts the boss track instead.
    private void HandleNightBossSpawned()
    {
        if (playlistCoroutine != null)
        {
            StopCoroutine(playlistCoroutine);
            playlistCoroutine = null;
        }

        if (bossMusicCoroutine != null) StopCoroutine(bossMusicCoroutine);
        bossMusicCoroutine = StartCoroutine(PlayBossMusic());
    }

    // Cleared by OnNightBossDefeated/OnDayBegan, whichever fires first - same convention Mobai's
    // siege mode and NPCai's flee-from-boss mode already use for this exact event pair.
    private void HandleBossFightEnded()
    {
        if (bossMusicCoroutine != null)
        {
            StopCoroutine(bossMusicCoroutine);
            bossMusicCoroutine = null;
        }

        if (playlistCoroutine != null) StopCoroutine(playlistCoroutine);
        playlistCoroutine = StartCoroutine(ResumeAfterBoss());
    }

    private IEnumerator PlayBossMusic()
    {
        yield return FadeVolume(audioSource.volume, 0f, bossMusicFadeDuration);
        audioSource.Stop();

        if (bossMusicClip == null) yield break;

        audioSource.clip = bossMusicClip;
        audioSource.loop = true;
        audioSource.volume = 0f;
        audioSource.Play();

        yield return FadeVolume(0f, volume, bossMusicFadeDuration);

        // Keep tracking the live volume field for as long as the boss music keeps looping (not
        // just the moment the fade-in finished) so a mid-fight slider drag is heard right away -
        // same reasoning as PlayTrack's own sustain loop.
        while (true)
        {
            audioSource.volume = volume;
            yield return null;
        }
    }

    private IEnumerator ResumeAfterBoss()
    {
        yield return FadeVolume(audioSource.volume, 0f, bossMusicFadeDuration);
        audioSource.Stop();
        audioSource.loop = false;

        yield return new WaitForSeconds(trackGapDelay);
        yield return PlaylistBody();
    }

    private IEnumerator PlaylistLoop()
    {
        yield return new WaitForSeconds(firstTrackDelay);
        yield return PlaylistBody();
    }

    // Split out from PlaylistLoop so the post-boss resume path can re-enter the playlist
    // directly, without re-triggering firstTrackDelay (which must only ever fire once, at true
    // game start) or restarting from track 0 - trackIndex only advances after a track actually
    // finishes, so resuming here naturally continues from wherever the boss interrupted it.
    private IEnumerator PlaylistBody()
    {
        while (true)
        {
            AudioClip clip = tracks[trackIndex];
            if (clip != null) yield return PlayTrack(clip);

            trackIndex = (trackIndex + 1) % tracks.Length;
            yield return new WaitForSeconds(trackGapDelay);
        }
    }

    private IEnumerator PlayTrack(AudioClip clip)
    {
        audioSource.clip = clip;
        audioSource.volume = 0f;
        audioSource.Play();

        yield return FadeVolume(0f, volume, fadeDuration);

        // Keep tracking the live volume field for the rest of the track (not just the moment
        // the fade-in finished) so a mid-track slider drag is heard right away.
        float sustainDuration = Mathf.Max(0f, clip.length - fadeDuration * 2f);
        float elapsed = 0f;
        while (elapsed < sustainDuration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = volume;
            yield return null;
        }

        yield return FadeVolume(volume, 0f, fadeDuration);
        audioSource.Stop();
    }

    private IEnumerator FadeVolume(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            audioSource.volume = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        audioSource.volume = to;
    }
}

// Implementation Steps:
// 1. Nothing to place manually - Tracks, the timing fields, bossMusicClip, and the AudioSource
//    on "SoundScape" are already wired up in the scene. Drag the Volume slider in the Inspector
//    to check/adjust it, live, while playing (boss music obeys the same slider).
