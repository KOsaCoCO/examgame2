using UnityEngine;

// Centralized chop/mine/pluck one-shot sounds - one shared volume slider covers all three.
// Same singleton shape as CombatSoundEffects.Instance/PlayerLevel.Instance/QuestManager.Instance -
// HarvestableResource/HarvestablePlant call into it directly rather than each finding/caching
// their own reference, and rather than wiring a clip onto tree_1.prefab and all 11 rock-node
// prefabs individually. Uses its own dedicated 2D AudioSource (PlayOneShot) rather than
// AudioSource.PlayClipAtPoint - PlayClipAtPoint always spawns a 3D-positioned temp AudioSource
// with standard distance rolloff, which read as too quiet even standing right at a resource
// node; a plain 2D source plays at the slider's actual volume with no falloff. volume can go
// past 1 (true amplification, not just clamped to the clip's native level) for source clips
// that are just quiet to begin with.
[RequireComponent(typeof(AudioSource))]
public class HarvestSoundEffects : MonoBehaviour
{
    public static HarvestSoundEffects Instance { get; private set; }

    [Tooltip("Axe landing on a tree node (HarvestableResource, ToolSubCategory.Cutting).")]
    public AudioClip chopClip;
    [Tooltip("Pickaxe landing on a rock node (HarvestableResource, ToolSubCategory.Mining).")]
    public AudioClip mineClip;
    [Tooltip("Picking a plant (HarvestablePlant).")]
    public AudioClip pluckClip;

    [Range(0f, 3f)] public float volume = 2f;

    private AudioSource audioSource;

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
        audioSource.spatialBlend = 0f; // 2D - no distance falloff, PlayOneShot below just uses this source's transform as a carrier
    }

    public void PlayChop(Vector3 position) => Play(chopClip);

    public void PlayMine(Vector3 position) => Play(mineClip);

    public void PlayPluck(Vector3 position) => Play(pluckClip);

    // position is accepted for call-site symmetry with the rest of this project's sound
    // helpers (CombatSoundEffects, MobAlertUI) but unused - this plays 2D, always at full
    // clarity regardless of where the hit happened.
    private void Play(AudioClip clip)
    {
        if (clip != null) audioSource.PlayOneShot(clip, volume);
    }
}

// Implementation Steps:
// 1. Add this component to any one GameObject in the scene (e.g. alongside SoundScapeMusic/
//    CombatSoundEffects under World Operators) - nothing to wire, HarvestableResource/
//    HarvestablePlant call HarvestSoundEffects.Instance directly.
// 2. Assign chopClip/mineClip/pluckClip in the Inspector.
