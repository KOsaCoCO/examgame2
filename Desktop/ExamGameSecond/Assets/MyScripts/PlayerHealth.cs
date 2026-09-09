using UnityEngine;

// Player's hit-point pool - same "hits" unit MobHealth uses on the mob side, just draining
// instead of filling. Damage comes from Mobai.PerformAttack (touch-range, scaled by
// DifficultyLevel), not a weapon.
public class PlayerHealth : MonoBehaviour
{
    private const string CloakItemName = "Miasma Protection Cloak";

    public int maxHealth = 50;

    private int currentHealth;
    private bool isDead;
    private bool isInvulnerable;
    private BoostSlotManager boostSlots;

    public int CurrentHealth => currentHealth;

    // Short, code-driven damage immunity window - set true by BossDeathSequence for the
    // duration of the boss's levitate/pulse death spectacle (the player is frozen and can't
    // react to other mobs mid-attack while it plays), false the rest of the time. Independent
    // of IsImmuneToMiasma (cloak-based, stays on for as long as the cloak is equipped).
    public void SetInvulnerable(bool invulnerable) => isInvulnerable = invulnerable;

    // Live check, not a permanent flag - true for exactly as long as the Miasma Protection Cloak
    // stays equipped in the Chestplate boost slot (see MiasmaCloakTradeOffer for how it's
    // acquired), false the instant it's unequipped. MiasmaFogArea checks this before applying
    // damage. Was a one-time-drink-for-permanent-immunity Elixir before this session's swap.
    public bool IsImmuneToMiasma
    {
        get
        {
            if (BoostSlots == null) return false;
            InventorySlotData chestSlot = BoostSlots.GetSlot(ArmorSlotType.Chestplate);
            return !chestSlot.IsEmpty && chestSlot.Item.ItemName == CloakItemName;
        }
    }

    // Lazy, retrying lookup rather than a one-time Start() fetch - BoostSlotManager doesn't
    // pre-exist in the scene, it's only created at runtime by UInavigator.Start()
    // (AddComponent<BoostSlotManager>()), and with no ScriptExecutionOrder asset in the project
    // there's no guarantee that runs before this component's own Start(). A one-time fetch that
    // lost that race cached null forever, silently zeroing armor reduction and miasma immunity
    // for the whole session - this retries until it actually finds one.
    private BoostSlotManager BoostSlots => boostSlots != null
        ? boostSlots
        : (boostSlots = FindAnyObjectByType<BoostSlotManager>(FindObjectsInactive.Include));

    void Awake()
    {
        currentHealth = maxHealth;
        GameEvents.RaisePlayerHealthChanged(currentHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (isDead || isInvulnerable) return;

        int mitigatedAmount = Mathf.Max(0, Mathf.RoundToInt(amount * (1f - GetArmorDamageReduction())));
        currentHealth = Mathf.Max(0, currentHealth - mitigatedAmount);
        GameEvents.RaisePlayerHealthChanged(currentHealth, maxHealth);
        if (mitigatedAmount > 0) CombatSoundEffects.Instance?.PlayPlayerDamaged(transform.position);
        if (currentHealth <= 0) Die();
    }

    // Restores hit points, capped at maxHealth - extra healing past the cap is simply
    // dropped, not banked for later. Used by edible items (see ItemPlacementTracker's
    // "Food" double-click special case).
    public void Heal(int amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        GameEvents.RaisePlayerHealthChanged(currentHealth, maxHealth);
    }

    // Fraction of incoming damage each equipped armor slot soaks up, summed across whichever
    // of the 4 boost slots currently hold anything (any item counts, not just a "real" armor
    // piece - the Miasma Protection Cloak occupies the Chestplate slot and counts toward its
    // 0.1 the same as a plain Chestplate would). Applies to every TakeDamage call alike,
    // mob touch-damage (Mobai.PerformAttack) and miasma fog ticks (MiasmaFogArea) both funnel
    // through here already, so neither needs its own copy of this logic.
    private float GetArmorDamageReduction()
    {
        if (BoostSlots == null) return 0f;

        float reduction = 0f;
        if (!BoostSlots.GetSlot(ArmorSlotType.Helmet).IsEmpty) reduction += 0.2f;
        if (!BoostSlots.GetSlot(ArmorSlotType.Chestplate).IsEmpty) reduction += 0.1f;
        if (!BoostSlots.GetSlot(ArmorSlotType.Pants).IsEmpty) reduction += 0.3f;
        if (!BoostSlots.GetSlot(ArmorSlotType.Boots).IsEmpty) reduction += 0.1f;

        return Mathf.Clamp01(reduction);
    }

    private void Die()
    {
        isDead = true;
        Debug.Log($"{gameObject.name} has died.");
        GameEvents.RaisePlayerDied(); // cue for GameEndUI's lose screen
    }
}

// Implementation Steps:
// 1. Add this component to the Player GameObject (the same one PlayerController/CharacterController
//    live on, tagged "Player" - Mobai.FindPlayerInRange looks it up by that tag).
// 2. Tune maxHealth if 50 needs adjusting.
// 3. No UI/death-flow yet on purpose (out of scope for this pass) - currentHealth is exposed via
//    CurrentHealth for whenever a health bar or game-over screen gets built.
