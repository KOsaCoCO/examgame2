using UnityEngine;
using UnityEngine.AI;

// Section 1's "Monster" quest step (GameplayRoadmap.md): a one-time boss encounter gated on
// three conditions all being true - player level >= RequiredPlayerLevel, the wizard's miasma
// quest fully turned in (GameEvents.OnWizardQuestCompleted), and the day/night cycle flipping to
// night (GameEvents.OnNightBegan). Each condition can become true in any order, so this listens
// to all three and re-checks every time one of them fires. Spawns once, from the same outer ring
// MobSpawner already uses for its Hard mobs, then adds Mobai/MobHealth/NightBossAi to the raw
// prefab clone at runtime (BossPrefab itself needs none of those pre-attached). Adds the quest on
// spawn and removes it on GameEvents.OnNightBossDefeated - the same AddQuest/RemoveQuest shape
// MainQuestProgress/DogQuestProgress already use for their own quest steps.
namespace NTGD124
{
    public class NightBossSpawner : MonoBehaviour
    {
        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Spawns the one-time night boss (Monster35) once the player is level 6+, the miasma quest is fully turned in, and night begins - whichever of those three happens last triggers the spawn. Adds the Monster quest and removes it once the boss is defeated (MobHealth's 100-hit default, same as any Hard mob).";


        ///// Public Variables/Editor Properties /////

        [Header("Trigger Conditions")]
        public int RequiredPlayerLevel = 6;

        [Header("Boss Prefab")]
        [Tooltip("Monster35's prefab (any color variant) - a bare mesh/animator clone, no Mobai/MobHealth/Collider needed on it. All of that is added here at spawn time.")]
        public GameObject BossPrefab;

        [Header("Quest")]
        public string QuestName = "Defeat the monster threatening the base";

        [Header("Boss Sound")]
        [Tooltip("Cycled through by the boss's own BossFootsteps component while it's moving, same shape as the player's PlayerFootsteps.")]
        public AudioClip[] BossFootstepClips;
        [Tooltip("pulse_bass.wav - played (first 3 seconds only) by BossDeathSequence during the boss's levitate/pulse death spectacle.")]
        public AudioClip BossDeathPulseMusicClip;
        [Tooltip("Flash_blue_purple.prefab - spawned by BossDeathSequence on every pulse of the death spectacle.")]
        public GameObject BossDeathFlashPrefab;


        ///// Private Variables /////

        private MobSpawner _mobSpawner;
        private DayNightTimeCycle _dayNightCycle;
        private bool _miasmaQuestCleared;
        private bool _hasSpawned;


        ///// Unity Methods /////

        private void Start()
        {
            _mobSpawner = FindAnyObjectByType<MobSpawner>();
            _dayNightCycle = FindAnyObjectByType<DayNightTimeCycle>();

            GameEvents.OnWizardQuestCompleted += HandleWizardQuestCompleted;
            GameEvents.OnPlayerLevelChanged += HandlePlayerLevelChanged;
            GameEvents.OnNightBegan += HandleNightBegan;
            GameEvents.OnNightBossDefeated += HandleBossDefeated;
        }

        private void OnDestroy()
        {
            GameEvents.OnWizardQuestCompleted -= HandleWizardQuestCompleted;
            GameEvents.OnPlayerLevelChanged -= HandlePlayerLevelChanged;
            GameEvents.OnNightBegan -= HandleNightBegan;
            GameEvents.OnNightBossDefeated -= HandleBossDefeated;
        }


        ///// Trigger Methods /////

        private void HandleWizardQuestCompleted()
        {
            _miasmaQuestCleared = true;
            TryTriggerBossSpawn();
        }

        private void HandlePlayerLevelChanged(int newLevel) => TryTriggerBossSpawn();

        private void HandleNightBegan() => TryTriggerBossSpawn();

        private void HandleBossDefeated()
        {
            QuestManager.Instance?.RemoveQuest(QuestName);
            Debug.Log("[NightBossSpawner] Night boss defeated - Monster quest step complete.");
        }


        ///// Action Methods /////

        private void TryTriggerBossSpawn()
        {
            if (_hasSpawned) return;
            if (!_miasmaQuestCleared) return;
            if (PlayerLevel.Instance == null || PlayerLevel.Instance.CurrentLevel < RequiredPlayerLevel) return;
            if (_dayNightCycle == null || _dayNightCycle.IsDaytime()) return;

            SpawnBoss();
        }

        private void SpawnBoss()
        {
            if (BossPrefab == null || _mobSpawner == null)
            {
                Debug.LogWarning("[NightBossSpawner] Missing BossPrefab or no MobSpawner found in the scene - cannot spawn.");
                return;
            }

            if (!_mobSpawner.TryGetSpawnPointInHardRing(out Vector3 spawnPoint))
            {
                Debug.LogWarning("[NightBossSpawner] Could not find a valid NavMesh spawn point in the Hard ring.");
                return;
            }

            _hasSpawned = true;

            GameObject boss = Instantiate(BossPrefab, spawnPoint, Quaternion.identity);
            boss.name = "NightBoss";

            NavMeshAgent agent = boss.GetComponent<NavMeshAgent>();
            if (agent == null) agent = boss.AddComponent<NavMeshAgent>();
            agent.Warp(spawnPoint);

            // Same guard MobSpawner.SpawnMobsInRing already has - fails loudly and lets the next
            // trigger retry instead of leaving a permanently frozen, silently broken boss standing
            // around (this is exactly what happened before MonsterNavMeshBaker existed: no NavMesh
            // baked for the boss's own agent type meant Warp always failed here with no visible sign).
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning("[NightBossSpawner] Boss warped but is not on the NavMesh for its agent type - destroying and will retry on the next trigger. Run Tools > World > Bake Monster NavMeshes if this keeps happening.");
                Destroy(boss);
                _hasSpawned = false;
                return;
            }

            if (boss.GetComponent<Collider>() == null)
            {
                CapsuleCollider collider = boss.AddComponent<CapsuleCollider>();
                collider.radius = 1.5f;
                collider.height = 4f;
                collider.center = new Vector3(0f, 2f, 0f);
            }

            // Difficulty must be set before MobHealth is added - MobHealth.Awake() reads it once,
            // at add-time, to cache hitsRequired (100 for Hard, matching the "100 hit health bar" spec).
            Mobai mobai = boss.GetComponent<Mobai>();
            if (mobai == null) mobai = boss.AddComponent<Mobai>();
            mobai.DifficultyLevel = MobDifficultyLevel.Hard;
            mobai.DisableAutomaticBehaviour = true;

            MobHealth health = boss.GetComponent<MobHealth>();
            if (health == null) health = boss.AddComponent<MobHealth>();
            health.IsUniqueBoss = true;

            if (boss.GetComponent<NightBossAi>() == null) boss.AddComponent<NightBossAi>();

            BossFootsteps footsteps = boss.GetComponent<BossFootsteps>();
            if (footsteps == null) footsteps = boss.AddComponent<BossFootsteps>();
            footsteps.footstepClips = BossFootstepClips;

            BossDeathSequence deathSequence = boss.GetComponent<BossDeathSequence>();
            if (deathSequence == null) deathSequence = boss.AddComponent<BossDeathSequence>();
            deathSequence.pulseMusicClip = BossDeathPulseMusicClip;
            deathSequence.flashFxPrefab = BossDeathFlashPrefab;

            // Bridges from WizardNpcBehavior's own "prepare for the boss" quest (added the
            // moment the miasma quest is turned in) to this one, so the quest log reads as one
            // continuous line rather than both sitting active at once.
            QuestManager.Instance?.RemoveQuest(WizardNpcBehavior.BossPrepQuestName);
            QuestManager.Instance?.AddQuest(QuestName, isMainQuest: true);
            GameEvents.RaiseNightBossSpawned();
            Debug.Log("[NightBossSpawner] Night boss spawned - Monster quest step started.");
        }
    }
}

// Implementation Steps:
// 1. Create an empty GameObject in the scene (e.g. "NightBossSpawner") and attach this script -
//    same "must actually be placed in the scene" reminder as WizardNpcBehavior/TradeManager this
//    session (both were built but forgotten in-scene until caught).
// 2. Drag one of Monster35's 3 color-variant prefabs (Assets/FreeAssets/Stylized3DMonster/
//    Monster35_FreeTrial/) into BossPrefab. Do NOT run MonsterRosterSetup's Wire Up tool on this
//    specific prefab reference first - this script adds Mobai/MobHealth/NightBossAi itself at
//    spawn time, so a prefab that already has them will just have those calls no-op onto the
//    existing components instead (harmless either way, just redundant).
// 3. Requires a MobSpawner already in the scene (used only for its Hard-ring spawn-point math)
//    and a DayNightTimeCycle already in the scene (used to gate the spawn to actual nighttime and
//    to detect the day->night transition, see GameEvents.OnNightBegan).
// 4. Nothing else to wire - the three trigger conditions (player level >= RequiredPlayerLevel,
//    GameEvents.OnWizardQuestCompleted, GameEvents.OnNightBegan) are all event-driven already.
// 5. CapsuleCollider/NavMeshAgent/detection ranges on the spawned boss are untuned defaults - see
//    NightBossAi.cs's own Implementation Steps for the same caveat the rest of the monster roster
//    has (GameplayRoadmap.md Section 6).
