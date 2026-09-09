using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// Hit-counter + drop table for one mob, keyed off its Mobai.DifficultyLevel.
// Reuses HarvestableResource's exact interaction shape (InteractableHotspot's
// E/right-click, gated on an armed tool via PlayerHandManager) but requires
// ToolSubCategory.Weapon (Sword) instead of Cutting/Mining. On death, rolls
// ItemDropRates' difficulty-matched table and adds results straight to the
// inventory (no world pickup), then destroys the mob - MobSpawner's
// aliveMobs.RemoveAll(mob => mob == null) only notices a destroyed mob, not a
// deactivated one, so this can't just SetActive(false) like a spent resource
// node does.
namespace NTGD124
{
    [RequireComponent(typeof(Mobai))]
    [RequireComponent(typeof(Collider))]
    public class MobHealth : MonoBehaviour
    {
        [Header("Hits Required (by difficulty)")]
        public int easyHitsRequired = 10;
        public int mediumHitsRequired = 50;
        public int hardHitsRequired = 100;

        [Tooltip("Set on the NightBossSpawner-spawned Monster35 boss (the Section 1 'Monster' quest step) - Die() raises GameEvents.OnNightBossDefeated instead of just dropping loot, so NightBossSpawner can complete the quest. Every other mob leaves this false.")]
        public bool IsUniqueBoss = false;

        [Header("Health Bar")]
        public float barHeightOffset = 2.2f;
        public float barWidth = 1f;
        public float barThickness = 0.12f;
        public Color healthyColor = Color.green;
        public Color lowHealthColor = Color.red;

        [Header("Interaction Reach")]
        [Tooltip("Radius of a separate trigger Collider added on top of the mob's own body Collider, purely so InteractableHotspot's E/right-click registers from a comfortable distance instead of requiring the player to touch the model's own tight hitbox. The original Collider is untouched and still governs physics/NavMeshAgent obstacle sizing.")]
        public float interactionRadius = 3f;

        private Mobai mobai;
        private InventoryManager inventory;
        private PlayerHandManager hand;
        private BoostSlotManager boostSlots;
        private InteractableHotspot hotspot;
        private Camera mainCamera;
        private PlayerController player; // lazily found - only needed for the knockback direction on a landed hit

        private PlayerController Player => player != null ? player : (player = FindAnyObjectByType<PlayerController>());

        private int hitsRequired;
        private int hitsTaken;
        private bool isDead;

        private Transform healthBarRoot;
        private Transform fillTransform;
        private Renderer fillRenderer;

        void Awake()
        {
            mobai = GetComponent<Mobai>();
            hitsRequired = HitsRequiredForDifficulty(mobai.DifficultyLevel);
            AddInteractionTrigger();
        }

        // Centers the new trigger on the model's existing body Collider (whatever shape it
        // shipped with) rather than the transform origin, so it reads as "around the mob" no
        // matter where each prefab's own pivot happens to sit.
        private void AddInteractionTrigger()
        {
            Collider bodyCollider = GetComponent<Collider>();

            SphereCollider interactionTrigger = gameObject.AddComponent<SphereCollider>();
            interactionTrigger.isTrigger = true;
            interactionTrigger.radius = interactionRadius;
            if (bodyCollider != null) interactionTrigger.center = transform.InverseTransformPoint(bodyCollider.bounds.center);
        }

        void Start()
        {
            // Include inactive hierarchies, same reasoning as HarvestableResource -
            // InventoryManager/PlayerHandManager can be found mid-startup before their
            // canvases are shown.
            inventory = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
            hand = FindAnyObjectByType<PlayerHandManager>(FindObjectsInactive.Include);
            boostSlots = FindAnyObjectByType<BoostSlotManager>(FindObjectsInactive.Include);
            mainCamera = Camera.main;

            hotspot = GetComponent<InteractableHotspot>();
            if (hotspot == null) hotspot = gameObject.AddComponent<InteractableHotspot>();
            hotspot.OnInteract.AddListener(HandleHit);
            hotspot.OnSecondaryInteract.AddListener(HandleHit);

            BuildHealthBar();
            UpdateHealthBar();
        }

        void OnDestroy()
        {
            if (hotspot == null) return;
            hotspot.OnInteract.RemoveListener(HandleHit);
            hotspot.OnSecondaryInteract.RemoveListener(HandleHit);
        }

        void LateUpdate()
        {
            if (healthBarRoot == null) return;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            healthBarRoot.SetPositionAndRotation(transform.position + Vector3.up * barHeightOffset, mainCamera.transform.rotation);
        }


        ///// Action Methods /////

        private int HitsRequiredForDifficulty(MobDifficultyLevel level)
        {
            switch (level)
            {
                case MobDifficultyLevel.Medium: return mediumHitsRequired;
                case MobDifficultyLevel.Hard: return hardHitsRequired;
                default: return easyHitsRequired;
            }
        }

        // E or right-click while in range, same dual-binding as HarvestableResource.
        private void HandleHit()
        {
            if (isDead) return;

            bool armed = hand != null && hand.IsArmedWithTool(ToolSubCategory.Weapon);
            if (inventory == null || !armed) return;

            Vector3 sourcePosition = Player != null ? Player.transform.position : transform.position;
            RegisterHit(GetPlayerHitAmount(), sourcePosition);
            CombatSoundEffects.Instance?.PlayPlayerHitMob(transform.position);
        }

        // Extra flat damage a Magic Stick hit deals on top of the normal base+armor amount a
        // sword swing would - makes the AoE burst hit noticeably harder than melee, not just
        // wider-reaching.
        private const int MagicStickBonusDamage = 3;

        // Magic Stick's AoE burst calls this directly - same hit-registration as a sword swing
        // (armor-scaled amount already computed by the caller isn't needed here since
        // GetPlayerHitAmount is tool-agnostic - it only reads equipped armor), just without
        // HandleHit's Sword-specific armed-tool gate, and with MagicStickBonusDamage added on top.
        public void ApplyMagicHit(Vector3 sourcePosition)
        {
            if (isDead || inventory == null) return;

            RegisterHit(GetPlayerHitAmount() + MagicStickBonusDamage, sourcePosition);
            CombatSoundEffects.Instance?.PlayPlayerHitMob(transform.position);
        }

        // Shared by HandleHit and ApplyMagicHit: applies the hit, updates the health bar,
        // provokes the mob, knocks it back away from sourcePosition, and checks for death.
        private void RegisterHit(int amount, Vector3 sourcePosition)
        {
            hitsTaken += amount;
            UpdateHealthBar();
            mobai.NotifyAttackedByPlayer();
            mobai.ApplyKnockback(sourcePosition, 1f);

            if (hitsTaken >= hitsRequired) Die();
        }

        // 1 base hit per swing, boosted by whichever armor slots the player currently has
        // equipped, stacking with however many are worn: Helmet +1, Chestplate +4, Pants +2,
        // Boots +1 (any item in a slot counts, same as PlayerHealth.GetArmorDamageReduction -
        // the Miasma Protection Cloak in Chestplate counts toward its +4 the same as a plain
        // Chestplate would).
        private int GetPlayerHitAmount()
        {
            int amount = 1;
            if (boostSlots == null) return amount;

            if (!boostSlots.GetSlot(ArmorSlotType.Helmet).IsEmpty) amount += 1;
            if (!boostSlots.GetSlot(ArmorSlotType.Chestplate).IsEmpty) amount += 4;
            if (!boostSlots.GetSlot(ArmorSlotType.Pants).IsEmpty) amount += 2;
            if (!boostSlots.GetSlot(ArmorSlotType.Boots).IsEmpty) amount += 1;

            return amount;
        }

        private void Die()
        {
            isDead = true;
            RollDrops();
            PlayerLevel.Instance?.AddExperience(ExperienceForDifficulty(mobai.DifficultyLevel));

            // Destroy(gameObject) only actually removes the object at end of frame - disabling
            // the agent right now (rather than leaving that to the pending destroy) means any
            // Mobai behaviour-loop tick landing in this same frame sees a disabled/off-mesh
            // agent and safely no-ops instead of throwing on SetDestination.
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            if (IsUniqueBoss)
            {
                // The boss gets its own death spectacle (levitate/pulse/flash, then the same
                // kill sound + confetti every regular mob gets) before GameEvents.
                // OnNightBossDefeated fires and the GameObject is actually removed -
                // BossDeathSequence owns all of that timing itself, so this method doesn't need
                // to know about it beyond handing off here and returning early.
                BossDeathSequence deathSequence = GetComponent<BossDeathSequence>();
                if (deathSequence != null)
                {
                    deathSequence.PlayDeathSequence();
                    return;
                }

                // Defensive fallback only, in case the component is ever missing - still raise
                // the win-screen cue rather than leaving the boss "dead" but stuck forever.
                GameEvents.RaiseNightBossDefeated();
            }

            CombatSoundEffects.Instance?.PlayMobKilled(transform.position);
            Destroy(gameObject);
        }

        // Easy = 3 exp, Medium = 5 exp, Hard = 8 exp per kill.
        private int ExperienceForDifficulty(MobDifficultyLevel level)
        {
            switch (level)
            {
                case MobDifficultyLevel.Medium: return 5;
                case MobDifficultyLevel.Hard: return 8;
                default: return 3;
            }
        }

        // Each drop table entry is rolled independently against its own chance -
        // 1f (Meat) always hits, 0.5f (Skin/Bone) is a coin flip, 0.6f (Crystallized Soul,
        // Hard only) is uncommon. Successes are added straight to the inventory. Only Bone is
        // tagged with this mob's difficulty tier (see InventorySlotData.IconVariant) so it can
        // show a different icon per tier via ItemIconCatalog - the boss shares "Hard" since
        // it's Hard-tier too. Every other drop (Meat/Skin/Crystallized Soul) stays untagged so
        // it stacks identically regardless of which mob tier dropped it - the only difference
        // between mobs is meant to be the bones, not separate non-merging piles of everything else.
        private void RollDrops()
        {
            if (inventory == null) return;

            List<DropRateEntry> table = GetDropTable(mobai.DifficultyLevel);
            if (table == null) return;

            string tierVariant = mobai.DifficultyLevel.ToString();
            foreach (DropRateEntry entry in table)
            {
                if (Random.value > entry.DropChance) continue;
                string variant = entry.ItemName == "Bone" ? tierVariant : null;
                inventory.AddItem(ItemCatalog.GetByName(entry.ItemName), 1, variant);
            }
        }

        private List<DropRateEntry> GetDropTable(MobDifficultyLevel level)
        {
            switch (level)
            {
                case MobDifficultyLevel.Medium: return ItemDropRates.MediumMobDrops;
                case MobDifficultyLevel.Hard: return ItemDropRates.HardMobDrops;
                default: return ItemDropRates.EasyMobDrops;
            }
        }

        // Builds a simple green->red bar from two unlit Quads (background + fill), parented
        // to this mob so it's destroyed automatically with it. Billboarded to the camera in
        // LateUpdate rather than parented rotation, since the mob's own transform rotates
        // with its movement/NavMeshAgent.
        private void BuildHealthBar()
        {
            healthBarRoot = new GameObject("HealthBar").transform;
            healthBarRoot.SetParent(transform, worldPositionStays: false);
            healthBarRoot.localPosition = Vector3.up * barHeightOffset;

            Transform background = CreateBarQuad("Background", Color.black);
            background.SetParent(healthBarRoot, worldPositionStays: false);
            background.localPosition = new Vector3(0f, 0f, 0.02f);
            background.localScale = new Vector3(barWidth, barThickness, 1f);

            fillTransform = CreateBarQuad("Fill", healthyColor);
            fillTransform.SetParent(healthBarRoot, worldPositionStays: false);
            fillTransform.localPosition = Vector3.zero;
            fillTransform.localScale = new Vector3(barWidth, barThickness, 1f);
            fillRenderer = fillTransform.GetComponent<Renderer>();
        }

        private Transform CreateBarQuad(string quadName, Color color)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = quadName;

            Collider quadCollider = quad.GetComponent<Collider>();
            if (quadCollider != null) Destroy(quadCollider);

            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            Material material = new Material(shader) { color = color };
            renderer.material = material;

            return quad.transform;
        }

        // Shrinks the fill from its right edge (left edge pinned) and lerps green -> red as
        // hitsTaken approaches hitsRequired.
        private void UpdateHealthBar()
        {
            if (fillTransform == null || fillRenderer == null) return;

            float remainingFraction = hitsRequired > 0 ? Mathf.Clamp01(1f - (float)hitsTaken / hitsRequired) : 0f;

            fillTransform.localScale = new Vector3(barWidth * remainingFraction, barThickness, 1f);
            fillTransform.localPosition = new Vector3(-(barWidth * (1f - remainingFraction)) / 2f, 0f, 0f);
            fillRenderer.material.color = Color.Lerp(lowHealthColor, healthyColor, remainingFraction);
        }
    }
}

// Implementation Steps:
// 1. Add this component to each mob prefab that already has Mobai on it - no other setup
//    needed, it adds its own SphereCollider trigger (sized by interactionRadius) on Awake for
//    InteractableHotspot's proximity trigger, on top of - not replacing - the model's own
//    Collider, the same "add a trigger if the interaction needs one" convention HarvestablePlant/
//    MiasmaFogArea use.
// 2. Tune easy/medium/hardHitsRequired per prefab if 10/50/100 needs adjusting, and
//    interactionRadius if 3 units doesn't feel right for a particular mob's model size.
// 3. Make sure a "Sword" item (ToolSubCategory.Weapon) is armed in a hand slot - hits are
//    silently ignored otherwise, same "no rejection UI yet" behaviour as HarvestableResource.
//    Each landed swing counts for more than 1 hit if the player has armor equipped - see
//    GetPlayerHitAmount (Helmet +1, Chestplate +4, Pants +2, Boots +1, stacking).
// 4. Drop tables live in ItemDropRates.cs (EasyMobDrops/MediumMobDrops/HardMobDrops) - edit
//    there to change what a difficulty drops or at what chance.
