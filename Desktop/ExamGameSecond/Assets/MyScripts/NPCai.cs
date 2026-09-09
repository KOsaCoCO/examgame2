using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NTGD124;

// Per-NPC behavior: wander-around-spawn-point roaming (circle or mesh-bounds - see
// SetRoamArea/SetRoamSlots) or, while a follow target is set (SetFollowTarget), walking
// straight at it instead - plus an interactable dialogue hook. Reuses InteractableHotspot
// (same E-key + trigger-Collider pattern as ButtonHUB and harvestable trees/rocks) rather
// than building a separate interaction system - talking to an NPC is conceptually the same
// "walk up, press E" action, and InteractableHotspot's OnPlayerEnterRange doubles as a
// touch/proximity trigger for NPCs that open dialogue on contact instead (see
// WizardNpcBehavior).
//
// Talk() itself is still just a placeholder log line - actual dialogue UI hangs off
// OnTalkedTo instead (see GirlNpcBehavior for the girl's quest conversation), so this file
// never needs to know what quest is what. See NPCManager for the spawn side of "connect
// to the quest" (the girl's arrival is itself triggered by the same
// GameEvents.OnBaseSlotUnlocked the first quest completes on).
public class NPCai : MonoBehaviour
{
    [Tooltip("Display name for this NPC - set by whatever spawns it (see NPCManager), or by hand in the Inspector for a manually-placed NPC.")]
    public string npcName = "NPC";

    [Header("Roaming")]
    [Tooltip("How far from its roam center this NPC wanders - only used in circle mode (see SetRoamArea, otherwise its own spawn point); ignored once SetRoamSlots is active.")]
    public float roamRadius = 6f;
    [Tooltip("Movement speed while roaming.")]
    public float roamSpeed = 1.5f;
    [Tooltip("Movement speed while following a target (see SetFollowTarget) - deliberately faster than the player's own moveSpeed by default, so a following NPC can actually catch up instead of the player being able to outrun it forever.")]
    public float followSpeed = 7f;
    [Tooltip("How long to pause at each roam point before picking a new one.")]
    public float pauseSeconds = 2f;
    [Tooltip("How far above its own position to start the downward ground-check raycast from - must clear the tallest thing it can stand on (e.g. a base slot's platform).")]
    public float groundCheckHeight = 10f;

    [Header("Dialogue (placeholder - no UI yet)")]
    [TextArea(2, 4)]
    public string[] dialogueLines = { "..." };
    [Tooltip("Fired whenever the player talks to this NPC (E, in range). Quest logic can listen here later.")]
    public UnityEvent OnTalkedTo = new UnityEvent();

    [Header("Boss Threat")]
    [Tooltip("While GameEvents.OnNightBossSpawned is active, this NPC steers directly away from the nearest Mobai/NightBossAi within this radius instead of roaming/following.")]
    public float bossThreatDetectionRadius = 10f;
    [Tooltip("Multiplier applied to whichever speed (roam or follow) is currently active while the boss threat is active.")]
    public float bossThreatSpeedMultiplier = 2f;

    private Vector3 spawnPoint;
    private Vector3 roamTarget;
    private Vector3? roamCenterOverride;
    private List<Bounds> roamSlotBounds;
    private Transform followTarget;
    private float pauseTimer;
    private bool paused = true;
    private bool forcedPause;
    private bool bossThreatActive;
    private InteractableHotspot hotspot;
    private Animator animator;

    private float EffectiveRoamSpeed => bossThreatActive ? roamSpeed * bossThreatSpeedMultiplier : roamSpeed;
    private float EffectiveFollowSpeed => bossThreatActive ? followSpeed * bossThreatSpeedMultiplier : followSpeed;

    void Start()
    {
        spawnPoint = transform.position;
        PickNewRoamTarget();

        // Lives on the model's own child hierarchy, not this container - null for any
        // NPC whose model has no rig/controller yet (e.g. wizard/dog), in which case
        // SetAnimatorSpeed/LoopAnimatorIfFinished below are just no-ops.
        animator = GetComponentInChildren<Animator>();

        EnsureInteractionTrigger();

        hotspot = GetComponent<InteractableHotspot>();
        if (hotspot == null) hotspot = gameObject.AddComponent<InteractableHotspot>();
        hotspot.OnInteract.AddListener(Talk);
    }

    void OnDestroy()
    {
        if (hotspot != null) hotspot.OnInteract.RemoveListener(Talk);
    }

    void OnEnable()
    {
        GameEvents.OnNightBossSpawned += HandleNightBossSpawned;
        GameEvents.OnNightBossDefeated += HandleBossThreatEnded;
        GameEvents.OnDayBegan += HandleBossThreatEnded;
    }

    void OnDisable()
    {
        GameEvents.OnNightBossSpawned -= HandleNightBossSpawned;
        GameEvents.OnNightBossDefeated -= HandleBossThreatEnded;
        GameEvents.OnDayBegan -= HandleBossThreatEnded;
    }

    void Update()
    {
        if (bossThreatActive && TryFleeFromNearbyThreat())
        {
            LoopAnimatorIfFinished();
            return;
        }

        if (followTarget != null) Follow();
        else Roam();

        LoopAnimatorIfFinished();
    }

    private void HandleNightBossSpawned() => bossThreatActive = true;

    private void HandleBossThreatEnded() => bossThreatActive = false;

    // Steers directly away from the nearest Mobai/NightBossAi within bossThreatDetectionRadius,
    // at EffectiveRoamSpeed (already boosted by bossThreatSpeedMultiplier). Returns false (does
    // nothing) if paused or no threat is in range, so Update falls through to normal Follow/Roam.
    private bool TryFleeFromNearbyThreat()
    {
        if (forcedPause) return false;

        Collider[] hits = Physics.OverlapSphere(transform.position, bossThreatDetectionRadius);
        Transform nearestThreat = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.GetComponent<Mobai>() == null && hit.GetComponent<NightBossAi>() == null) continue;

            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearestThreat = hit.transform;
        }

        if (nearestThreat == null) return false;

        Vector3 fleeDirection = transform.position - nearestThreat.position;
        fleeDirection.y = 0f;
        if (fleeDirection.sqrMagnitude < 0.0001f) fleeDirection = -transform.forward;
        fleeDirection.Normalize();

        transform.position += fleeDirection * EffectiveRoamSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(fleeDirection);
        SetAnimatorSpeed(EffectiveRoamSpeed);

        SnapToGround();
        return true;
    }

    // The imported walk clip's own "Loop Time" import setting defaults to off, so
    // left alone the Animator plays it once and holds the last frame - manually
    // wrapping back to the start here loops it regardless of that import setting.
    // No-op while paused (animator.speed is 0, so normalizedTime isn't advancing).
    private void LoopAnimatorIfFinished()
    {
        if (animator == null) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.normalizedTime >= 1f)
        {
            animator.Play(state.fullPathHash, 0, state.normalizedTime % 1f);
        }
    }

    // Checks specifically for an existing trigger Collider, not just any Collider - a
    // prefab with its own solid body/mesh Collider (e.g. the dog) still needs this added,
    // since that collider isn't a trigger and won't fire InteractableHotspot's proximity
    // events. Only a prefab that already has its own trigger Collider sized for
    // interaction skips this.
    private void EnsureInteractionTrigger()
    {
        foreach (Collider existing in GetComponents<Collider>())
        {
            if (existing.isTrigger) return;
        }

        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 3f;
        trigger.center = new Vector3(0f, 1f, 0f);
    }

    // Called by dialogue UI (see GirlNpcBehavior) while a conversation is up - stays fully
    // frozen (no roam ticking, no automatic un-pause) until EndForcedPause is called.
    public void BeginForcedPause()
    {
        forcedPause = true;
        SetAnimatorSpeed(0f);
    }

    // Eases back into roaming with a normal pauseSeconds beat rather than immediately
    // darting off, the same as finishing any other roam leg.
    public void EndForcedPause()
    {
        forcedPause = false;
        paused = true;
        pauseTimer = pauseSeconds;
    }

    // Recenters roaming on a single circular area (e.g. a fixed marker like DogRoamplace)
    // instead of this NPC's own spawn point. Clears any mesh-bounds roaming set via
    // SetRoamSlots - only one mode is ever active at a time. Immediately picks a new target
    // inside the new area too - without this, a target already chosen under the OLD area
    // stays live and gets walked toward first, which looks like the NPC "bugging out" back
    // to its previous roam zone.
    public void SetRoamArea(Vector3 center, float radius)
    {
        roamCenterOverride = center;
        roamRadius = radius;
        roamSlotBounds = null;
        PickNewRoamTarget();
    }

    // Recenters roaming on the actual mesh footprint of one or more areas (e.g.
    // NPCManager's unlocked base-slot platforms) instead of an approximated bounding
    // circle - every roam target lands somewhere on one of these Bounds (each one a
    // Renderer.bounds, so it matches the real platform geometry, not a guessed radius),
    // never in the gaps between them. Clears any circle-mode roaming set via SetRoamArea.
    public void SetRoamSlots(List<Bounds> slotBounds)
    {
        roamSlotBounds = slotBounds != null && slotBounds.Count > 0 ? slotBounds : null;
        roamCenterOverride = null;
        PickNewRoamTarget();
    }

    // Every read site below goes through this instead of a bare "roamSlotBounds != null" -
    // SetRoamSlots already normalizes null-or-empty to null at assignment time, but reading
    // Count defensively too costs nothing and guarantees PickNewRoamTarget's indexer can never
    // be handed a 0-length list, whatever the actual path that got it there.
    private bool HasSlotBounds => roamSlotBounds != null && roamSlotBounds.Count > 0;

    // Switches to continuously walking toward a target Transform (e.g. the player - see
    // WizardNpcBehavior) instead of wandering - takes priority over roam mode in Update
    // while non-null. Pass null to stop following and fall back to whatever roam mode
    // (SetRoamArea/SetRoamSlots) was last set, the same as ending any other roam leg.
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    // No pause/target-picking beat like Roam - just walks straight at the target every
    // frame it isn't forced-paused, since the point is to actually catch up and trigger
    // whatever the follower is there for (see WizardNpcBehavior's collision-driven
    // dialogue), not to wander near it.
    private void Follow()
    {
        if (forcedPause) return;

        Vector3 toTarget = followTarget.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.01f)
        {
            SetAnimatorSpeed(0f);
            return;
        }

        Vector3 direction = toTarget.normalized;
        transform.position += direction * EffectiveFollowSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction);
        SetAnimatorSpeed(EffectiveFollowSpeed);

        SnapToGround();
    }

    private void Roam()
    {
        if (forcedPause) return;

        if (paused)
        {
            SetAnimatorSpeed(0f);
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f) paused = false;
            return;
        }

        // Safety net: if this NPC has somehow ended up outside its own roam area (e.g. a
        // large frame-time overshoot, or a stale target left over from before the last
        // SetRoamArea/SetRoamSlots/EndForcedPause) rather than only picking a new target
        // once it happens to reach the old one, this re-centers immediately instead of
        // letting it keep drifting further out.
        if (!IsWithinRoamArea(transform.position))
        {
            PickNewRoamTarget();
        }

        Vector3 toTarget = roamTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.05f)
        {
            paused = true;
            pauseTimer = pauseSeconds;
            PickNewRoamTarget();
            return;
        }

        Vector3 direction = toTarget.normalized;
        transform.position += direction * EffectiveRoamSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction);
        SetAnimatorSpeed(EffectiveRoamSpeed);

        SnapToGround();
    }

    // NPC_Locomotion (see NPCManager) only has a single Walk state - no separate
    // idle clip exists yet, so instead of switching states this freezes the walk
    // cycle mid-pose during a pause (animator.speed 0) and resumes it while
    // roaming, rather than the model sitting in its bind pose (a T-pose) either
    // way. No-op for NPCs whose model has no Animator/controller yet.
    private void SetAnimatorSpeed(float speed)
    {
        if (animator == null) return;
        animator.speed = speed;
    }

    private void PickNewRoamTarget()
    {
        if (HasSlotBounds)
        {
            Bounds chosen = roamSlotBounds[Random.Range(0, roamSlotBounds.Count)];
            float x = Random.Range(chosen.min.x, chosen.max.x);
            float z = Random.Range(chosen.min.z, chosen.max.z);
            roamTarget = new Vector3(x, chosen.center.y, z);
            return;
        }

        Vector3 center = roamCenterOverride ?? spawnPoint;
        Vector2 offset = Random.insideUnitCircle * roamRadius;
        roamTarget = center + new Vector3(offset.x, 0f, offset.y);
    }

    // Mode-aware bounds check backing the safety net in Roam() - mesh-bounds mode counts
    // as "inside" only when actually within one of the given platforms (XZ-only, so the
    // NPC's own height above/below a platform's thin Y extent doesn't matter); circle mode
    // is the plain distance-from-center check it always was.
    private bool IsWithinRoamArea(Vector3 position)
    {
        if (HasSlotBounds)
        {
            foreach (Bounds bounds in roamSlotBounds)
            {
                Vector3 flatPosition = new Vector3(position.x, bounds.center.y, position.z);
                if (bounds.Contains(flatPosition)) return true;
            }
            return false;
        }

        Vector3 center = roamCenterOverride ?? spawnPoint;
        Vector3 toCenter = center - position;
        toCenter.y = 0f;
        return toCenter.sqrMagnitude <= roamRadius * roamRadius;
    }

    // Raycasts straight down from above the NPC and snaps to whatever the ray hits first -
    // terrain or a base slot's own platform mesh, whichever is higher at this XZ position -
    // so it stands on top of a slot instead of clipping through it. Trigger colliders (its
    // own interaction Collider, other NPCs') are ignored so they can't be mistaken for ground.
    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundCheckHeight;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckHeight * 2f, ~0, QueryTriggerInteraction.Ignore)) return;

        Vector3 position = transform.position;
        position.y = hit.point.y;
        transform.position = position;
    }

    // Placeholder dialogue - no UI yet, just a log line and an event quest
    // logic can hook into per-NPC later.
    private void Talk()
    {
        string line = dialogueLines != null && dialogueLines.Length > 0 ? dialogueLines[0] : "...";
        Debug.Log($"[NPC] {npcName}: {line}");
        OnTalkedTo?.Invoke();
    }
}
