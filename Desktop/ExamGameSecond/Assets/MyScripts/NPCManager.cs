using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NTGD124;

// Oversees which NPCs exist, when they arrive, and (via NPCai.OnTalkedTo)
// their dialogue/quest hooks. Not auto-wired like most other managers in this
// project - attach it by hand to an empty GameObject in the scene (see
// Implementation Steps at the bottom), since it needs prefab references
// assignable in the Inspector, and components added at runtime via
// AddComponent can't have those pre-set the way UInavigator's tree/rock
// prefab arrays are.
//
// The first base/land slot unlocking (GameEvents.OnBaseSlotUnlocked, slot 0 -
// the same event MainQuestProgress completes the first quest on) spawns the
// girl NPC arrivalDelay seconds later, spawnDistanceBehindPlayer units behind
// wherever the player is currently facing (a one-time spawn, guarded by
// girlHasArrived). Accepting her dog quest (GameEvents.OnDogQuestAccepted)
// spawns the dog the same way, but it roams a fixed DogRoamplace marker until
// the quest is turned in (GameEvents.OnDogFound), at which point it
// reappears as a companion roaming the same unlocked base-slot platforms as
// the girl - actual mesh footprints (see TryGetUnlockedSlotBounds/NPCai.
// SetRoamSlots), not an approximated circle, so neither of them ever wanders
// into the gaps/terrain between slots. Once the girl's own dog-delivery
// dialogue actually CLOSES (GameEvents.OnDogDeliveryConversationEnded - later
// than OnDogFound, which fires while her panel is still open) the wizard
// spawns wizardSpawnDistanceBehindPlayer units behind the player - see
// WizardNpcBehavior for what he does once he arrives (follows the player,
// opens a dialogue on contact).
public class NPCManager : MonoBehaviour
{
    [Header("NPC Prefabs (assign once prefabs exist)")]
    public GameObject girlPrefab;
    public GameObject wizardPrefab;
    public GameObject dogPrefab;

    [Header("Arrival")]
    [Tooltip("Delay after the trigger event before the NPC actually spawns.")]
    public float arrivalDelay = 1f;
    [Tooltip("How far behind the player the girl/dog spawn, in units.")]
    public float spawnDistanceBehindPlayer = 3f;
    [Tooltip("How far behind the player the wizard spawns, in units - deliberately farther than the girl/dog so he has room to actually follow and approach rather than starting right on top of the player.")]
    public float wizardSpawnDistanceBehindPlayer = 10f;
    [Tooltip("Tag on the Terrain GameObject - matches AdjustTerrainOnSlot/WaterFloater's convention. Keeps a spawned NPC on the ground regardless of terrain height at the spawn point.")]
    public string terrainTag = "GameTerrain";

    [Header("Dog Roaming")]
    [Tooltip("Name of the invisible marker the dog roams around while the quest is still active - only its position is used (whatever collider it has, e.g. a mesh collider, is just there to help you place it in the Editor, not read at runtime).")]
    public string dogRoamAreaObjectName = "DogRoamplace";
    [Tooltip("How far from the marker's position the dog wanders while the quest is active.")]
    public float dogRoamRadius = 6f;

    private PlayerController player;
    private BaseSlotExpander baseSlotExpander;
    private NPCai girlNpc;
    private NPCai dogNpc;
    private NPCai wizardNpc;
    private bool girlHasArrived;
    private bool dogQuestPhase;
    private bool wizardHasArrived;

    void OnEnable()
    {
        GameEvents.OnBaseSlotUnlocked += HandleBaseSlotUnlocked;
        GameEvents.OnDogQuestAccepted += HandleDogQuestAccepted;
        GameEvents.OnDogFound += HandleDogFound;
        GameEvents.OnDogDeliveryConversationEnded += HandleWizardArrival;
    }

    void OnDisable()
    {
        GameEvents.OnBaseSlotUnlocked -= HandleBaseSlotUnlocked;
        GameEvents.OnDogQuestAccepted -= HandleDogQuestAccepted;
        GameEvents.OnDogFound -= HandleDogFound;
        GameEvents.OnDogDeliveryConversationEnded -= HandleWizardArrival;
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        baseSlotExpander = FindAnyObjectByType<BaseSlotExpander>();
    }

    // The first slot unlocking is this session's cue for the girl NPC to arrive -
    // guarded so the spawn itself only ever fires once. Every later slot unlock (and
    // the first one, once she's actually spawned) instead grows her roaming area.
    private void HandleBaseSlotUnlocked(int slotIndex)
    {
        if (slotIndex == 0 && !girlHasArrived)
        {
            girlHasArrived = true;
            StartCoroutine(SpawnGirlAfterDelay());
            return;
        }

        UpdateNpcRoamArea(girlNpc);
        if (!dogQuestPhase) UpdateNpcRoamArea(dogNpc);
    }

    private IEnumerator SpawnGirlAfterDelay()
    {
        yield return new WaitForSeconds(arrivalDelay);
        girlNpc = SpawnBehindPlayer(girlPrefab, "Girl", spawnDistanceBehindPlayer);
        UpdateNpcRoamArea(girlNpc);
    }

    // Cue: player accepted the girl's dog quest (GirlNpcBehavior.AnswerQuest). Unlike the
    // girl (who always arrives behind the player), the dog spawns directly at the
    // DogRoamplace marker itself - a deliberately separate spawn path (SpawnAtLocation,
    // not SpawnBehindPlayer) so it's actually out at its own location to be found, not
    // wherever the player happened to be standing when they accepted the quest.
    private void HandleDogQuestAccepted()
    {
        StartCoroutine(SpawnDogAfterDelay());
    }

    private IEnumerator SpawnDogAfterDelay()
    {
        yield return new WaitForSeconds(arrivalDelay);

        GameObject marker = GameObject.Find(dogRoamAreaObjectName);
        if (marker == null)
        {
            Debug.LogWarning($"NPCManager: no '{dogRoamAreaObjectName}' marker found - dog will spawn behind the player instead.");
            dogNpc = SpawnBehindPlayer(dogPrefab, "Dog", spawnDistanceBehindPlayer);
        }
        else
        {
            dogNpc = SpawnAtLocation(dogPrefab, "Dog", marker.transform.position);
        }

        if (dogNpc == null) yield break;

        dogQuestPhase = true;
        dogNpc.SetRoamArea(marker != null ? marker.transform.position : dogNpc.transform.position, dogRoamRadius);
        SetDogSoundPhaseOne(dogNpc, true);
    }

    // The bark/hint sound only makes sense while the dog is still lost (quest phase, out
    // at DogRoamplace) - see DogSoundProximity.SetPhaseOneActive for what the AudioSource
    // enable/disable toggle itself does.
    private void SetDogSoundPhaseOne(NPCai npc, bool phaseOneActive)
    {
        if (npc == null) return;

        // GetComponentInChildren, not GetComponent - if the AudioSource/DogSoundProximity
        // live on a child (e.g. a separate audio or model child) rather than the same
        // object NPCai is on, a plain GetComponent would silently miss it and the toggle
        // would never fire, leaving the sound stuck playing.
        DogSoundProximity sound = npc.GetComponentInChildren<DogSoundProximity>(true);
        if (sound != null) sound.SetPhaseOneActive(phaseOneActive);
        else Debug.LogWarning($"[Dog] No DogSoundProximity found on '{npc.name}' or its children - could not {(phaseOneActive ? "start" : "mute")} the sound.");
    }

    // Cue: the dog was handed back to the girl (GirlNpcBehavior.TurnInDog). Reactivates the
    // very same dog that was deactivated when picked up (see DogNpcBehavior.CompletePickup)
    // - dogNpc still references it, it was never destroyed - rather than instantiating a
    // second, unrelated copy. Placed at the base-slot area itself (not just bound to roam
    // it) so it visibly "arrives" there instead of popping into existence wherever it was
    // last standing out at DogRoamplace. Falls back to a fresh SpawnBehindPlayer only if
    // that reference is somehow gone (e.g. the dog prefab reference changed at runtime).
    private void HandleDogFound()
    {
        dogQuestPhase = false;

        if (dogNpc != null)
        {
            if (TryGetUnlockedSlotBounds(out List<Bounds> slotBounds))
            {
                Vector3 position = slotBounds[0].center;
                SnapToGround(ref position);
                dogNpc.transform.position = position;
                dogNpc.SetRoamSlots(slotBounds);
            }

            dogNpc.gameObject.SetActive(true);

            // Still forced-paused from the pickup confirm (see DogNpcBehavior.
            // BeginPickupConfirm) - that flag survives deactivation, so without this the
            // reactivated companion would just stand frozen forever instead of roaming.
            dogNpc.EndForcedPause();
        }
        else
        {
            dogNpc = SpawnBehindPlayer(dogPrefab, "Dog", spawnDistanceBehindPlayer);
            UpdateNpcRoamArea(dogNpc);
        }

        SetDogSoundPhaseOne(dogNpc, false);
    }

    // Cue: the dog quest fully completed (same event HandleDogFound reacts to - multiple
    // independent subscribers to one GameEvents event is the established pattern, see
    // DogQuestProgress too). Spawns the wizard behind the player, same shape as the girl's
    // own arrival - everything after that (following the player, the collision-triggered
    // dialogue) is WizardNpcBehavior's own concern, not NPCManager's, the same way
    // DogNpcBehavior owns the dog's pickup logic independently of how it got spawned.
    private void HandleWizardArrival()
    {
        if (wizardHasArrived) return;
        wizardHasArrived = true;
        StartCoroutine(SpawnWizardAfterDelay());
    }

    private IEnumerator SpawnWizardAfterDelay()
    {
        yield return new WaitForSeconds(arrivalDelay);
        wizardNpc = SpawnBehindPlayer(wizardPrefab, "Wizard", wizardSpawnDistanceBehindPlayer);
    }

    private NPCai SpawnBehindPlayer(GameObject prefab, string npcLabel, float distance)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"NPCManager: no prefab assigned for '{npcLabel}' yet - skipping spawn.");
            return null;
        }

        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("NPCManager could not find the player - skipping spawn.");
            return null;
        }

        Transform playerTransform = player.transform;
        Vector3 spawnPosition = playerTransform.position - playerTransform.forward * distance;
        SnapToGround(ref spawnPosition);

        Quaternion facing = Quaternion.LookRotation(FlattenForward(playerTransform.forward), Vector3.up);

        NPCai npc = SpawnNpc(prefab, npcLabel, spawnPosition, facing);
        if (npc != null) Debug.Log($"[NPC] '{npcLabel}' arrived behind the player.");
        return npc;
    }

    // Separate from SpawnBehindPlayer on purpose - the quest-phase dog needs to spawn out
    // at its own DogRoamplace location to actually be found, not wherever the player was
    // standing when the quest was accepted (see HandleDogQuestAccepted).
    private NPCai SpawnAtLocation(GameObject prefab, string npcLabel, Vector3 position)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"NPCManager: no prefab assigned for '{npcLabel}' yet - skipping spawn.");
            return null;
        }

        SnapToGround(ref position);

        NPCai npc = SpawnNpc(prefab, npcLabel, position, Quaternion.identity);
        if (npc != null) Debug.Log($"[NPC] '{npcLabel}' arrived at its designated location.");
        return npc;
    }

    private NPCai SpawnNpc(GameObject prefab, string npcLabel, Vector3 position, Quaternion rotation)
    {
        GameObject instance = Instantiate(prefab, position, rotation);
        instance.name = npcLabel;

        NPCai npc = instance.GetComponent<NPCai>();
        if (npc == null) npc = instance.AddComponent<NPCai>();
        npc.npcName = npcLabel;

        return npc;
    }

    // Collects the actual mesh footprint (Renderer.bounds - real world-space geometry, not
    // an approximated circle) of every currently unlocked base slot's platform - called
    // once an NPC spawns/arrives and again on every later slot unlock, so its wander area
    // grows along with the base instead of staying pinned to slot 0. Shared by the girl
    // (always), the dog (only once its quest is turned in - see dogQuestPhase), and
    // HandleDogFound (to place the reactivated companion dog on an actual slot, not just
    // bound its roaming to one). NPCai.SetRoamSlots then only ever picks targets that land
    // on one of these platforms, never in the gaps/terrain between them.
    private bool TryGetUnlockedSlotBounds(out List<Bounds> slotBounds)
    {
        slotBounds = new List<Bounds>();

        if (baseSlotExpander == null) baseSlotExpander = FindAnyObjectByType<BaseSlotExpander>();
        if (baseSlotExpander == null) return false;

        foreach (BaseSlotExpander.BaseSlot slot in baseSlotExpander.Slots)
        {
            if (slot == null || !slot.IsUnlocked || slot.SlotObject == null) continue;

            Renderer slotRenderer = slot.SlotObject.GetComponent<Renderer>();
            if (slotRenderer == null) slotRenderer = slot.SlotObject.GetComponentInChildren<Renderer>();
            if (slotRenderer == null) continue;

            slotBounds.Add(slotRenderer.bounds);
        }

        return slotBounds.Count > 0;
    }

    private void UpdateNpcRoamArea(NPCai npc)
    {
        if (npc == null) return;
        if (!TryGetUnlockedSlotBounds(out List<Bounds> slotBounds)) return;

        npc.SetRoamSlots(slotBounds);
    }

    private void SnapToGround(ref Vector3 position)
    {
        GameObject terrainObject = GameObject.FindGameObjectWithTag(terrainTag);
        Terrain terrain = terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
        if (terrain == null) return;

        position.y = terrain.transform.position.y + terrain.SampleHeight(position);
    }

    // PlayerController itself only yaws (pitch is applied to a child camera
    // rig), so this is mostly a safety net - flattens forward so a spawned
    // NPC faces level instead of tilted into the ground/sky.
    private Vector3 FlattenForward(Vector3 forward)
    {
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    [ContextMenu("Debug: Spawn Girl")]
    private void DebugSpawnGirl()
    {
        girlNpc = SpawnBehindPlayer(girlPrefab, "Girl", spawnDistanceBehindPlayer);
        UpdateNpcRoamArea(girlNpc);
    }

    [ContextMenu("Debug: Spawn Wizard")]
    private void DebugSpawnWizard() => SpawnBehindPlayer(wizardPrefab, "Wizard", wizardSpawnDistanceBehindPlayer);

    [ContextMenu("Debug: Spawn Dog (quest phase)")]
    private void DebugSpawnDog()
    {
        GameObject marker = GameObject.Find(dogRoamAreaObjectName);
        dogNpc = marker != null
            ? SpawnAtLocation(dogPrefab, "Dog", marker.transform.position)
            : SpawnBehindPlayer(dogPrefab, "Dog", spawnDistanceBehindPlayer);

        if (dogNpc == null) return;

        dogQuestPhase = true;
        dogNpc.SetRoamArea(marker != null ? marker.transform.position : dogNpc.transform.position, dogRoamRadius);
    }
}

// Implementation Steps:
// 1. Create an empty GameObject in the scene (e.g. named "NPCManager") and
//    attach this script to it - it isn't auto-created like InventoryManager/
//    PlayerHandManager/etc. because it needs prefab fields assignable in the
//    Inspector, which only works for components already placed in the scene.
// 2. Once girl/wizard/dog prefabs exist, drag them onto the matching fields.
// 3. Press Play, buy the first base slot (see ResourceEconomyDesignNotes.txt
//    for that flow) - about 1 second later the girl should spawn
//    spawnDistanceBehindPlayer units behind the player. Before a prefab is
//    assigned, this just logs a warning instead of spawning anything.
// 4. Accepting the girl's dog quest (GirlNpcBehavior) spawns the dog the same
//    way, but it roams a fixed point instead of the base-slot area until the
//    quest is turned in: an invisible "DogRoamplace" marker object (see
//    dogRoamAreaObjectName) placed anywhere in the scene, using only its
//    position - whatever collider it has (even a mesh collider, if that's
//    the easiest way to place it against the terrain in the Editor) is never
//    read at runtime, dogRoamRadius controls how far the dog wanders from
//    that point.
// 5. Once the girl's dog-delivery dialogue actually closes (not the instant
//    turn-in starts - see GameEvents.OnDogDeliveryConversationEnded), the
//    wizard spawns wizardSpawnDistanceBehindPlayer units behind the player -
//    see WizardNpcBehavior.cs for what happens from there (he follows the
//    player, then opens a dialogue on contact). "Debug: Spawn Wizard" in the
//    context menu still spawns one manually for testing, but it won't start
//    following on its own that way - see WizardNpcBehavior's own
//    Implementation Steps for the real trigger.
// 6. Each spawned NPC gets an NPCai component (self-added if the prefab
//    doesn't have one) for roaming + a placeholder "press E to talk" - see
//    NPCai.cs. NPCai.OnTalkedTo is the hook point for tying a specific NPC's
//    conversation/interaction to quest progress - see GirlNpcBehavior.cs for
//    the girl's conversation, DogNpcBehavior.cs for the dog's pickup, and
//    WizardNpcBehavior.cs for the wizard's collision-triggered dialogue.
