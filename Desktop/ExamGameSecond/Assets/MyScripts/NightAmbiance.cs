using System.Collections;
using UnityEngine;

// Loops an ambient soundtrack for as long as it's night, easing out when day begins - the same
// binary GameEvents.OnNightBegan/OnDayBegan pair DayPlantSpawner/NightPlantSpawner already key
// off. Own AudioSource, no manual Play On Awake/Loop checkboxes needed - set here in code. Fades
// in/out over fadeDuration (long and smooth by default) rather than an abrupt Play()/Stop() cut.
[RequireComponent(typeof(AudioSource))]
public class NightAmbiance : MonoBehaviour
{
    public AudioClip ambianceClip;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Seconds spent easing in when night begins and easing out when day begins.")]
    public float fadeDuration = 10f;

    private AudioSource audioSource;
    private Coroutine fadeCoroutine;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = ambianceClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // ambiance - always 2D, never positional
        audioSource.volume = 0f;
    }

    void OnEnable()
    {
        GameEvents.OnNightBegan += HandleNightBegan;
        GameEvents.OnDayBegan += HandleDayBegan;
    }

    void OnDisable()
    {
        GameEvents.OnNightBegan -= HandleNightBegan;
        GameEvents.OnDayBegan -= HandleDayBegan;
    }

    void Update()
    {
        // Keeps the slider live-adjustable in the Inspector while holding steady (not mid-fade),
        // same responsiveness as SoundScapeMusic's own volume field.
        if (fadeCoroutine == null && audioSource.isPlaying) audioSource.volume = volume;
    }

    private void HandleNightBegan()
    {
        if (audioSource.clip == null) return;

        audioSource.volume = 0f;
        audioSource.Play();
        StartFade(volume, stopOnComplete: false);
    }

    private void HandleDayBegan()
    {
        StartFade(0f, stopOnComplete: true);
    }

    private void StartFade(float target, bool stopOnComplete)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(target, stopOnComplete));
    }

    private IEnumerator FadeRoutine(float target, bool stopOnComplete)
    {
        float start = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }

        audioSource.volume = target;
        if (stopOnComplete) audioSource.Stop();
        fadeCoroutine = null;
    }
}

// Implementation Steps:
// 1. Add this component to any one GameObject in the scene (e.g. alongside SoundScapeMusic
//    under World Operators) and assign ambianceClip in the Inspector - nothing else to wire.
