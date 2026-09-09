using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Controls a mob's avoid/attack rules by difficulty level, plus a night-time rally that advances Easy/Medium mobs on the player's base
namespace NTGD124
{
    public enum MobDifficultyLevel
    {
        Easy,
        Medium,
        Hard
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class Mobai : MonoBehaviour
    {
        // Bool parameter name every kept monster package's AnimatorController was wired with
        // (see MonsterAnimatorSetup.cs, Editor-only) - shared here so the Editor tool and this
        // runtime driver can't drift apart.
        public const string IsMovingAnimatorParam = "IsMoving";

        ///// Component Description /////

        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Drives one mob's behaviour based on its Difficulty Level (matches which list it was spawned from in MobSpawner) and the current day/night cycle read from the SkyTag-tagged object's rotation (found automatically at runtime). By day, mobs avoid stronger mobs and attack according to their difficulty rules, wandering idly on the NavMesh when nothing is nearby - Easy mobs specifically flee from the player until provoked (NotifyAttackedByPlayer, called by MobHealth on a landed hit), then attack back permanently instead. By night, Easy and Medium mobs roll a chance to join a rally advancing on 'Center Orient', a placeholder found under the BaseSlot-tagged object. Reserves a hook for a future PlayerLevel system to force a rally on level-up.";


        ///// Public Variables /////

        [Header("Mob Difficulty")]
        public MobDifficultyLevel DifficultyLevel = MobDifficultyLevel.Easy; // must match the list this mob was spawned from in MobSpawner

        [Header("Day/Night Tracking")]
        public string SkyTag = "Sky"; // tag placed on the sky/skybox GameObject (eg FastSky_Sun) - found automatically at runtime since prefab assets can't hold a direct reference to a scene object
        public float NightStartDegrees = 180f; // X rotation at which night/sunset begins (sky rotates 0 to 360 over the day/night cycle)
        public float DayStartDegrees = 360f; // X rotation at which day/sunrise begins

        [Header("Target Tags & Names")]
        public string PlayerTag = "Player";
        public string BaseSlotTag = "BaseSlot"; // tag placed on the empty parent object holding the base's active children
        public string CenterOrientName = "Center Orient"; // name of the placeholder child mobs target during a night rally

        [Header("Detection Ranges")]
        public float PlayerDetectionRange = 15f;
        public float ThreatDetectionRange = 15f; // range used to spot stronger mobs to avoid, or weaker ones to attack
        public float AttackRange = 4f;

        [Header("Boss Support")]
        [Tooltip("Radius (matches NightBossAi's own default) this mob searches for a wall piece/unlocked base slot to siege once GameEvents.OnNightBossSpawned fires - every mob joins regardless of DifficultyLevel, unlike night rally (Easy/Medium only).")]
        public float StructureDetectionRange = 120f;

        [Header("Easy Mob Player Reaction")]
        [Tooltip("Easy mobs that haven't been attacked yet flee once the player closes to within this distance - see NotifyAttackedByPlayer for the one-time switch to attacking back instead.")]
        public float EasyFleePlayerDistance = 4f;

        [Header("Attack Cooldowns")]
        [Tooltip("Minimum seconds between this mob actually landing a hit on its target, independent of BehaviourCheckIntervalSeconds (which only controls how often behaviour/targeting re-evaluates, not how the attack itself is paced). Doesn't affect how much damage a hit deals or how the mob receives damage - only how often it can land one.")]
        public float EasyAttackCooldownSeconds = 1f;
        public float MediumAttackCooldownSeconds = 1f;
        public float HardAttackCooldownSeconds = 2f;

        [Header("Movement Speeds")]
        public float NormalMoveSpeed = 3.5f;
        public float RallyMoveSpeed = 4.5f;

        [Header("Night Rally Settings")]
        [Range(0f, 1f)] public float RallyJoinChance = 0.3f; // chance this mob joins the rally group each night
        public float BehaviourCheckIntervalSeconds = 1f; // how often behaviour is re-evaluated

        [Header("Idle Wander")]
        public float IdleWanderRadius = 20f; // how far a mob may roam from its current position when it has no threat/player to react to

        [Header("Automatic Behaviour")]
        [Tooltip("When true, EvaluateBehaviour() no-ops every tick - Mobai stays attached (still supplying DifficultyLevel to MobHealth) but a different component owns the NavMeshAgent entirely. Set by NightBossSpawner on the night boss, which uses NightBossAi instead of this script's ring-mob avoid/attack/rally rules.")]
        public bool DisableAutomaticBehaviour = false;

        [Header("Animation")]
        [Tooltip("Minimum squared NavMeshAgent velocity to count as \"moving\" for the Animator's IsMoving bool - small, just enough to ignore floating-point noise while stopped.")]
        public float MovementAnimationThreshold = 0.01f;


        ///// Private Variables /////

        private NavMeshAgent _agent;
        private Animator _animator; // found on a child (the imported FBX's own root) - null on any prefab that doesn't have one yet
        private Transform _centerOrientTarget; // cached rally destination, found under the BaseSlot-tagged object
        private GameObject _sky; // cached sky/skybox object, found via SkyTag
        private BaseSlotExpander _slotExpander; // lazily found only once boss support actually activates
        private bool _isRallying; // true once this mob has committed to advancing on the base
        private bool _hasRolledForRallyTonight; // ensures the rally chance is only rolled once per night
        private bool _hasBeenAttackedByPlayer; // Easy mobs only - permanent once set, see NotifyAttackedByPlayer
        private bool _bossSupportActive; // true from GameEvents.OnNightBossSpawned until OnNightBossDefeated/OnDayBegan
        private float _lastAttackTime = -Mathf.Infinity; // Time.time of this mob's last landed hit, gates PerformAttack by its own cooldown regardless of BehaviourCheckIntervalSeconds

        // Shared across every mob instance - true once any mob has rolled successfully into the
        // rally tonight, so GameEvents.OnNightRallyBegan only ever fires once per night rather
        // than once per mob. Reset by a single game-wide subscription (see
        // ResetRallyAnnouncementOnLoad) registered once at game start, not per-instance.
        private static bool _rallyAnnouncedTonight;

        [RuntimeInitializeOnLoadMethod]
        private static void ResetRallyAnnouncementOnLoad()
        {
            _rallyAnnouncedTonight = false;
            GameEvents.OnDayBegan += () => _rallyAnnouncedTonight = false;
        }


        ///// Unity Methods /////

        // Time-based trigger: runs once automatically when the mob is spawned
        private void Start()
        {
            InitializeMob();
        }

        private void OnEnable()
        {
            GameEvents.OnNightBossSpawned += HandleNightBossSpawned;
            GameEvents.OnNightBossDefeated += HandleBossFightEnded;
            GameEvents.OnDayBegan += HandleBossFightEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnNightBossSpawned -= HandleNightBossSpawned;
            GameEvents.OnNightBossDefeated -= HandleBossFightEnded;
            GameEvents.OnDayBegan -= HandleBossFightEnded;
        }

        // Runs every frame (unlike EvaluateBehaviour's fixed-interval tick) so the Animator
        // reacts immediately to the NavMeshAgent's actual velocity - correct regardless of which
        // script (this one's own behaviour rules, or NightBossAi on the night boss) is currently
        // driving the agent's destination, since it just reflects real movement either way.
        private void Update()
        {
            UpdateAnimator();
        }


        ///// Trigger Methods /////

        // Trigger type: time-based (called from Start)
        private void InitializeMob()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = NormalMoveSpeed;
            _animator = GetComponentInChildren<Animator>();

            LocateCenterOrientTarget();
            LocateSky();
            StartCoroutine(BehaviourLoopRoutine());
        }

        // Trigger type: time-based (called repeatedly by BehaviourLoopRoutine)
        private void EvaluateBehaviour()
        {
            if (DisableAutomaticBehaviour) return;

            // Overrides everything else, including an in-progress rally - once the boss
            // appears every mob (any DifficultyLevel) drops its individual rules and joins
            // the siege on walls/base slots until the fight ends.
            if (_bossSupportActive)
            {
                HandleBossSupportBehaviour();
                return;
            }

            bool isNight = IsNightTime();
            bool canRally = DifficultyLevel == MobDifficultyLevel.Easy || DifficultyLevel == MobDifficultyLevel.Medium;

            if (!isNight)
            {
                _hasRolledForRallyTonight = false; // reset so a fresh roll happens next night
            }

            if (_isRallying)
            {
                // A rallying mob still reacts to the player crossing its path - attacks
                // directly (bypassing HandleEasyBehaviour's flee-first rule entirely) rather
                // than marching past, no matter its difficulty level.
                Transform rallyThreat = FindPlayerInRange(PlayerDetectionRange);
                if (rallyThreat != null)
                {
                    AttackTarget(rallyThreat);
                    return;
                }

                ContinueNightRally();
                return;
            }

            if (isNight && canRally && !_hasRolledForRallyTonight)
            {
                _hasRolledForRallyTonight = true;
                RollForNightRally();

                if (_isRallying)
                {
                    ContinueNightRally();
                    return;
                }
            }

            HandleRegularBehaviour();
        }

        // Trigger type: external/event-based, called by MobHealth.HandleHit the first time a
        // landed sword hit registers. Easy mobs flee from the player by default (see
        // HandleEasyBehaviour) - this is the one-way switch to attacking back instead, and it
        // never resets, so a mob that's been provoked stays hostile for the rest of its life.
        // No-op for Medium/Hard, which already always attack regardless.
        public void NotifyAttackedByPlayer()
        {
            _hasBeenAttackedByPlayer = true;
        }

        // Trigger type: external/event-based, reserved for a future PlayerLevel script to call on level-up
        public void OnPlayerLeveledUp()
        {
            bool canRally = DifficultyLevel == MobDifficultyLevel.Easy || DifficultyLevel == MobDifficultyLevel.Medium;
            if (!canRally || _centerOrientTarget == null) return;

            _isRallying = true;
            Debug.Log($"{gameObject.name} rallies immediately - player leveled up.");
            ContinueNightRally();
        }

        private void HandleNightBossSpawned()
        {
            _bossSupportActive = true;
            if (_slotExpander == null) _slotExpander = FindAnyObjectByType<BaseSlotExpander>();
        }

        private void HandleBossFightEnded()
        {
            _bossSupportActive = false;
        }


        ///// Action Methods /////

        // Finds the BaseSlot-tagged parent and caches its "Center Orient" child as the rally target
        private void LocateCenterOrientTarget()
        {
            GameObject baseSlotObject = GameObject.FindGameObjectWithTag(BaseSlotTag);
            if (baseSlotObject == null)
            {
                Debug.LogWarning($"Mobai on {gameObject.name} could not find an object tagged '{BaseSlotTag}'.");
                return;
            }

            Transform centerOrient = baseSlotObject.transform.Find(CenterOrientName);
            if (centerOrient == null)
            {
                Debug.LogWarning($"Mobai on {gameObject.name} could not find a child named '{CenterOrientName}' under the base slot.");
                return;
            }

            _centerOrientTarget = centerOrient;
        }

        // Finds the sky/skybox object via SkyTag - looked up at runtime instead of a direct Inspector reference,
        // since prefab assets can't hold a reference to a scene-only object
        private void LocateSky()
        {
            _sky = GameObject.FindGameObjectWithTag(SkyTag);
            if (_sky == null)
            {
                Debug.LogWarning($"Mobai on {gameObject.name} could not find an object tagged '{SkyTag}'. Night rally will never trigger.");
            }
        }

        // Reads the linked sky's X rotation and returns whether it currently falls in the night range
        private bool IsNightTime()
        {
            if (_sky == null) return false;

            float rotationX = _sky.transform.eulerAngles.x;
            return rotationX >= NightStartDegrees && rotationX < DayStartDegrees;
        }

        // Rolls this mob's one-time chance to join tonight's rally group
        private void RollForNightRally()
        {
            if (_centerOrientTarget == null) return;
            if (Random.value > RallyJoinChance) return;

            _isRallying = true;
            Debug.Log($"{gameObject.name} joins the night rally toward {_centerOrientTarget.name}.");

            if (!_rallyAnnouncedTonight)
            {
                _rallyAnnouncedTonight = true;
                GameEvents.RaiseNightRallyBegan();
            }
        }

        // Keeps a rallying mob moving toward the Center Orient target
        private void ContinueNightRally()
        {
            _agent.speed = RallyMoveSpeed;
            SafeSetDestination(_centerOrientTarget.position);
        }

        // Paths to and destroys the nearest wall piece/unlocked base slot, exactly like the
        // boss itself (see SiegeTargeting, shared with NightBossAi) - active for every mob,
        // any DifficultyLevel, for as long as _bossSupportActive is true. Idles if nothing's
        // in range rather than falling back to its own individual rules.
        private void HandleBossSupportBehaviour()
        {
            Transform structureTarget = SiegeTargeting.FindNearestStructure(transform.position, StructureDetectionRange, _slotExpander);
            if (structureTarget == null)
            {
                TryIdleWander();
                return;
            }

            _agent.speed = RallyMoveSpeed;
            SafeSetDestination(structureTarget.position);

            float distance = Vector3.Distance(transform.position, structureTarget.position);
            if (distance <= AttackRange) SiegeTargeting.DamageStructure(structureTarget, _slotExpander, AttackDamageForDifficulty());
        }

        // Runs this mob's normal (non-rally) avoid/attack rules for its difficulty level
        private void HandleRegularBehaviour()
        {
            switch (DifficultyLevel)
            {
                case MobDifficultyLevel.Easy:
                    HandleEasyBehaviour();
                    break;
                case MobDifficultyLevel.Medium:
                    HandleMediumBehaviour();
                    break;
                case MobDifficultyLevel.Hard:
                    HandleHardBehaviour();
                    break;
            }
        }

        // Easy rule: avoid Medium/Hard mobs first (unchanged); otherwise its relationship with
        // the player depends on NotifyAttackedByPlayer - untouched, it's skittish and flees once
        // the player closes to EasyFleePlayerDistance; once provoked, it attacks back like any
        // other mob for the rest of its life instead.
        private void HandleEasyBehaviour()
        {
            Transform threat = FindNearestMobOfLevel(MobDifficultyLevel.Medium, ThreatDetectionRange);
            if (threat == null)
            {
                threat = FindNearestMobOfLevel(MobDifficultyLevel.Hard, ThreatDetectionRange);
            }

            if (threat != null)
            {
                MoveAwayFrom(threat.position);
                return;
            }

            if (_hasBeenAttackedByPlayer)
            {
                Transform player = FindPlayerInRange(PlayerDetectionRange);
                if (player != null)
                {
                    AttackTarget(player);
                    return;
                }
            }
            else
            {
                Transform nearbyPlayer = FindPlayerInRange(EasyFleePlayerDistance);
                if (nearbyPlayer != null)
                {
                    MoveAwayFrom(nearbyPlayer.position);
                    return;
                }
            }

            TryIdleWander();
        }

        // Medium rule: avoid Hard mobs first, otherwise attack the player or Easy mobs
        private void HandleMediumBehaviour()
        {
            Transform threat = FindNearestMobOfLevel(MobDifficultyLevel.Hard, ThreatDetectionRange);
            if (threat != null)
            {
                MoveAwayFrom(threat.position);
                return;
            }

            Transform player = FindPlayerInRange(PlayerDetectionRange);
            if (player != null)
            {
                AttackTarget(player);
                return;
            }

            Transform easyMob = FindNearestMobOfLevel(MobDifficultyLevel.Easy, ThreatDetectionRange);
            if (easyMob != null)
            {
                AttackTarget(easyMob);
                return;
            }

            TryIdleWander();
        }

        // Hard rule: never avoids, attacks the player or any other mob it detects
        private void HandleHardBehaviour()
        {
            Transform player = FindPlayerInRange(PlayerDetectionRange);
            if (player != null)
            {
                AttackTarget(player);
                return;
            }

            Transform anyMob = FindNearestMobOfLevel(MobDifficultyLevel.Easy, ThreatDetectionRange);
            if (anyMob == null)
            {
                anyMob = FindNearestMobOfLevel(MobDifficultyLevel.Medium, ThreatDetectionRange);
            }
            if (anyMob == null)
            {
                anyMob = FindNearestMobOfLevel(MobDifficultyLevel.Hard, ThreatDetectionRange);
            }

            if (anyMob != null)
            {
                AttackTarget(anyMob);
                return;
            }

            TryIdleWander();
        }

        // Finds the nearest other mob of a given difficulty level within range, or null if none is found
        private Transform FindNearestMobOfLevel(MobDifficultyLevel level, float range)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, range);
            Transform nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (Collider hit in hits)
            {
                Mobai otherMob = hit.GetComponent<Mobai>();
                if (otherMob == null || otherMob == this || otherMob.DifficultyLevel != level) continue;

                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = hit.transform;
                }
            }

            return nearest;
        }

        // Finds the player's transform if it is tagged PlayerTag and within range, otherwise null
        private Transform FindPlayerInRange(float range)
        {
            GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player == null) return null;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            return distance <= range ? player.transform : null;
        }

        // Moves this mob directly away from a threatening position
        private void MoveAwayFrom(Vector3 threatPosition)
        {
            Vector3 fleeDirection = (transform.position - threatPosition).normalized;
            Vector3 fleeDestination = transform.position + fleeDirection * ThreatDetectionRange;

            _agent.speed = NormalMoveSpeed;
            SafeSetDestination(fleeDestination);
        }

        // Moves toward a target and attacks once within AttackRange
        private void AttackTarget(Transform target)
        {
            _agent.speed = NormalMoveSpeed;
            SafeSetDestination(target.position);

            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            if (distanceToTarget <= AttackRange)
            {
                PerformAttack(target);
            }
        }

        // SetDestination throws if the agent is disabled or not currently on a baked NavMesh
        // (e.g. spawned/wandered outside the baked surface, or mid-destroy after dying) - every
        // call site routes through this instead of calling it directly, same guard TryIdleWander
        // already used on its own.
        private void SafeSetDestination(Vector3 destination)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.SetDestination(destination);
        }

        // Called by MobHealth.HandleHit on every landed sword hit - a one-shot pushback away
        // from the player so a hit reads as a hit. NavMeshAgent.Move is built for exactly this
        // (an instant positional nudge that stays NavMesh-constrained), unlike SetDestination -
        // no pathing/coroutine needed. The mob's own behaviour loop naturally closes the
        // distance and attacks again on its next tick, so nothing else has to be done here.
        public void ApplyKnockback(Vector3 fromPosition, float distance)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

            Vector3 direction = transform.position - fromPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;
            direction.Normalize();

            _agent.Move(direction * distance);
        }

        // Touch-range attack, checked every BehaviourCheckIntervalSeconds while within
        // AttackRange (not a raw OnTriggerEnter/Stay) but actually landing a hit is separately
        // throttled by AttackCooldownForDifficulty - standing in a mob's range deals damage on
        // that cooldown's own cadence, not the (possibly faster) behaviour tick rate.
        private void PerformAttack(Transform target)
        {
            if (Time.time - _lastAttackTime < AttackCooldownForDifficulty()) return;
            _lastAttackTime = Time.time;

            PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(AttackDamageForDifficulty());

                // Same visual feedback as MiasmaFogArea's damage tick - a quick tilt-and-back
                // camera jolt, so a mob's hit reads clearly on screen beyond just the health bar.
                PlayerController playerController = target.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.TriggerCameraJolt();
                    playerController.ApplyKnockback(transform.position, 1f);
                }
                CombatSoundEffects.Instance?.PlayMobHitPlayer(transform.position);
                return;
            }

            Debug.Log($"{gameObject.name} attacks {target.name}.");
        }

        // Easy = 1 hit, Medium = 3 hits, Hard = 8 hits per touch - "hit" is the same unit
        // PlayerHealth.maxHealth (50) and MobHealth's hitsRequired are counted in.
        private int AttackDamageForDifficulty()
        {
            switch (DifficultyLevel)
            {
                case MobDifficultyLevel.Medium: return 3;
                case MobDifficultyLevel.Hard: return 8; // 6 base + 1.5, rounded
                default: return 1;
            }
        }

        // Easy/Medium = 1 second between landed hits, Hard = 2 seconds - independent of
        // AttackDamageForDifficulty and of BehaviourCheckIntervalSeconds.
        private float AttackCooldownForDifficulty()
        {
            switch (DifficultyLevel)
            {
                case MobDifficultyLevel.Medium: return MediumAttackCooldownSeconds;
                case MobDifficultyLevel.Hard: return HardAttackCooldownSeconds;
                default: return EasyAttackCooldownSeconds;
            }
        }

        // Reflects the NavMeshAgent's actual current velocity onto the Animator's IsMoving bool -
        // each kept monster package's controller (see MonsterAnimatorSetup.cs) transitions
        // Idle<->Walk purely off this one parameter, no exit-time auto-loop anymore.
        private void UpdateAnimator()
        {
            if (_animator == null || _agent == null) return;

            bool isMoving = _agent.isOnNavMesh && _agent.velocity.sqrMagnitude > MovementAnimationThreshold;
            _animator.SetBool(IsMovingAnimatorParam, isMoving);
        }

        // Adapted from Unity's AI Navigation sample RandomWalk.cs: once the current path finishes, pick a new
        // random on-mesh point within IdleWanderRadius so the mob keeps moving when nothing else needs its attention
        private void TryIdleWander()
        {
            if (_agent.pathPending || !_agent.isOnNavMesh || _agent.remainingDistance > _agent.stoppingDistance)
                return;

            Vector3 randomPoint = transform.position + Random.insideUnitSphere * IdleWanderRadius;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, IdleWanderRadius, NavMesh.AllAreas))
            {
                _agent.speed = NormalMoveSpeed;
                SafeSetDestination(hit.position);
            }
        }


        ///// Coroutines /////

        // Repeats EvaluateBehaviour on a fixed interval instead of every frame
        private IEnumerator BehaviourLoopRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(BehaviourCheckIntervalSeconds);
                EvaluateBehaviour();
            }
        }
    }
}

// Implementation Steps:
// 1. Bake a NavMesh over your terrain (Window > AI > Navigation) so mobs can pathfind - this script requires a NavMeshAgent.
// 2. Add this Mobai script to each mob prefab used in MobSpawner, and set Difficulty Level to match which prefab
//    list it belongs to (EasyMobPrefabs -> Easy, MediumMobPrefabs -> Medium, HardMobPrefabs -> Hard).
// 3. In Edit > Project Settings > Tags and Layers, add a "Sky" tag, then tag your sky/skybox object (eg FastSky_Sun)
//    with it. Mobai finds this by tag at runtime (SkyTag field) instead of a direct Inspector reference, since a
//    prefab asset can't hold a reference to a scene-only object.
// 4. In Edit > Project Settings > Tags and Layers, add a "BaseSlot" tag. Put it on the empty parent object that
//    holds your base's active children, then add an empty child object under it named "Center Orient" to act as
//    the rally target point.
// 5. Make sure the Player GameObject is tagged "Player" (or update Player Tag to match).
// 6. Adjust detection ranges, move speeds, Rally Join Chance, Behaviour Check Interval Seconds, and Idle Wander
//    Radius per prefab as needed - mobs wander randomly on the NavMesh whenever no threat/player/rally needs them.
// 7. Once a PlayerLevel script exists: call OnPlayerLeveledUp() on mobs when the player levels up.
// 8. Easy mobs flee once the player closes to EasyFleePlayerDistance, until MobHealth.HandleHit
//    calls NotifyAttackedByPlayer on a landed hit - permanent from then on, that mob attacks
//    back like Medium/Hard already do instead of fleeing.
// 9. Landing a hit on the player also triggers PlayerController.TriggerCameraJolt() - the same
//    camera tilt-and-back feedback MiasmaFogArea uses for its damage tick. Actually landing a
//    hit (not just being in range) is throttled by Easy/Medium/HardAttackCooldownSeconds, kept
//    separate from BehaviourCheckIntervalSeconds and from AttackDamageForDifficulty - tune
//    those three independently per prefab if 1/1/2 seconds needs adjusting.
