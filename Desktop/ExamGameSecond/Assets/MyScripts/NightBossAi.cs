using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Drives the one-time night boss spawned by NightBossSpawner (Section 1's "Monster" quest step
// in GameplayRoadmap.md - Monster35, spawned once player level >= 6, the miasma quest is fully
// turned in, and night begins). Deliberately its own component rather than an extra Mobai
// difficulty tier: this boss doesn't avoid/rally like a normal Hard mob, it specifically sieges
// the base (destroying slots and fence walls), attacks the player on sight, and threatens NPCs
// (cosmetically only - NPCs are invulnerable by design, no health system needed for them). Mobai
// is still present on the same GameObject (DisableAutomaticBehaviour=true) purely so MobHealth's
// RequireComponent and its difficulty-keyed hit-count/experience values keep working unchanged.
namespace NTGD124
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class NightBossAi : MonoBehaviour
    {
        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "One-time night boss AI (Monster35). Priority each behaviour tick: attack the player if in range, else path to and destroy the nearest standing wall piece or unlocked base slot, else threaten (cosmetically) the nearest NPC, else advance on Center Orient. Spawned/configured entirely by NightBossSpawner - no manual scene setup needed.";


        ///// Public Variables/Editor Properties /////

        [Header("Detection Ranges")]
        public float PlayerDetectionRange = 20f;
        public float StructureDetectionRange = 120f;
        public float NpcDetectionRange = 12f;
        public float AttackRange = 9f; // tripled alongside the boss's 3x model scale (Monster35_03.prefab)

        [Header("Movement")]
        public float MoveSpeed = 4.5f;

        [Header("Combat")]
        public int PlayerDamagePerHit = 5;

        [Header("Target Tags & Names")]
        public string PlayerTag = "Player";
        public string BaseSlotTag = "BaseSlot"; // tag on the object whose "Center Orient" child is the fallback approach point
        public string CenterOrientName = "Center Orient";

        [Header("Timing")]
        public float BehaviourCheckIntervalSeconds = 1f;


        ///// Private Variables /////

        private NavMeshAgent _agent;
        private BaseSlotExpander _slotExpander;
        private Transform _centerOrientTarget;


        ///// Unity Methods /////

        private void Start()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = MoveSpeed;

            _slotExpander = FindAnyObjectByType<BaseSlotExpander>();
            LocateCenterOrientTarget();

            StartCoroutine(BehaviourLoopRoutine());
        }


        ///// Action Methods /////

        private void LocateCenterOrientTarget()
        {
            GameObject baseSlotObject = GameObject.FindGameObjectWithTag(BaseSlotTag);
            if (baseSlotObject == null)
            {
                Debug.LogWarning($"NightBossAi on {gameObject.name} could not find an object tagged '{BaseSlotTag}'.");
                return;
            }

            _centerOrientTarget = baseSlotObject.transform.Find(CenterOrientName);
        }

        private void EvaluateBehaviour()
        {
            Transform player = FindPlayerInRange(PlayerDetectionRange);
            if (player != null)
            {
                MoveAndAttackPlayer(player);
                return;
            }

            Transform structureTarget = SiegeTargeting.FindNearestStructure(transform.position, StructureDetectionRange, _slotExpander);
            if (structureTarget != null)
            {
                MoveAndAttackStructure(structureTarget);
                return;
            }

            Transform npc = FindNearestNpc();
            if (npc != null)
            {
                MoveTowardAndThreatenNpc(npc);
                return;
            }

            if (_centerOrientTarget != null) SafeSetDestination(_centerOrientTarget.position);
        }

        private Transform FindPlayerInRange(float range)
        {
            GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player == null) return null;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            return distance <= range ? player.transform : null;
        }

        private void MoveAndAttackPlayer(Transform player)
        {
            SafeSetDestination(player.position);

            float distance = Vector3.Distance(transform.position, player.position);
            if (distance > AttackRange) return;

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(PlayerDamagePerHit);
                CombatSoundEffects.Instance?.PlayMobHitPlayer(transform.position);

                // Same camera-jolt/knockback feedback a regular mob's landed hit already gives
                // (Mobai.PerformAttack) - knockback distance doubled (2f vs. the usual 1f) to
                // match the boss's own greater push power.
                PlayerController playerController = player.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.TriggerCameraJolt();
                    playerController.ApplyKnockback(transform.position, 2f);
                }
            }
        }

        private void MoveAndAttackStructure(Transform target)
        {
            SafeSetDestination(target.position);

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > AttackRange) return;

            SiegeTargeting.DamageStructure(target, _slotExpander, PlayerDamagePerHit);
        }

        private Transform FindNearestNpc()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, NpcDetectionRange);
            foreach (Collider hit in hits)
            {
                if (hit.GetComponent<GirlNpcBehavior>() != null ||
                    hit.GetComponent<DogNpcBehavior>() != null ||
                    hit.GetComponent<WizardNpcBehavior>() != null)
                {
                    return hit.transform;
                }
            }

            return null;
        }

        // NPCs are invulnerable per design - this is cosmetic pressure only (path toward them,
        // log a threat), no damage is ever applied.
        private void MoveTowardAndThreatenNpc(Transform npc)
        {
            SafeSetDestination(npc.position);

            float distance = Vector3.Distance(transform.position, npc.position);
            if (distance <= AttackRange)
            {
                Debug.Log($"[NightBossAi] Boss threatens {npc.name} (NPCs are invulnerable - no effect).");
            }
        }


        // SetDestination throws if the agent is disabled or not currently on a baked NavMesh -
        // same guard Mobai.SafeSetDestination already uses, every call site here routes through
        // this instead of calling SetDestination directly.
        private void SafeSetDestination(Vector3 destination)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.SetDestination(destination);
        }


        ///// Coroutines /////

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
// 1. Nothing to place manually - NightBossSpawner adds this component (along with Mobai and
//    MobHealth) to the boss instance at spawn time.
// 2. Tune detection ranges / AttackRange / MoveSpeed / PlayerDamagePerHit in the Inspector on
//    NightBossSpawner's BossPrefab if the defaults don't fit Monster35's actual size/scale once
//    seen in the Editor - same caveat as the rest of the monster roster (GameplayRoadmap.md
//    Section 6): these are untuned defaults, not yet verified against the real mesh/NavMeshAgent.
