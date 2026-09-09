using UnityEngine;

// Pins two side quests naming exactly what the SW (Special Axe) and Miasma Protection Cloak
// recipes need, the moment the wizard's miasma quest is accepted (GameEvents.
// OnWizardQuestAccepted) - read live from the wizard's own TradeManager.offers so the pinned
// text always matches whatever's actually for sale instead of a second hardcoded copy of the
// recipe (Miasma Cloak's own recipe is appended to that same TradeManager at runtime by
// MiasmaCloakTradeOffer, so by the time the quest is accepted its offer is already present).
// Each side quest clears itself independently the instant that item is actually bought
// (GameEvents.OnItemTraded), same extension pattern as every other quest step in
// GameplayRoadmap.md Section 1 (one typed event, hooked straight to QuestManager.AddQuest/
// RemoveQuest).
public class MiasmaSubQuestTracker : MonoBehaviour
{
    private const string SpecialAxeQuestPrefix = "Collect materials for the Special Axe";
    private const string MiasmaCloakQuestPrefix = "Collect materials for the Miasma Protection Cloak";

    [Tooltip("The wizard's TradeManager - read its Offers to build the pinned side-quest text. Auto-found at Start if left empty.")]
    public TradeManager WizardTrade;

    void Awake()
    {
        if (WizardTrade == null) WizardTrade = FindAnyObjectByType<TradeManager>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        GameEvents.OnWizardQuestAccepted += HandleWizardQuestAccepted;
        GameEvents.OnItemTraded += HandleItemTraded;
    }

    void OnDisable()
    {
        GameEvents.OnWizardQuestAccepted -= HandleWizardQuestAccepted;
        GameEvents.OnItemTraded -= HandleItemTraded;
    }

    private void HandleWizardQuestAccepted()
    {
        if (WizardTrade == null) WizardTrade = FindAnyObjectByType<TradeManager>(FindObjectsInactive.Include);
        if (WizardTrade == null)
        {
            Debug.LogWarning("MiasmaSubQuestTracker: no TradeManager found - can't pin recipe text for the Special Axe/Miasma Cloak side quests.");
            return;
        }

        PinRecipeQuest(SpecialAxeQuestPrefix, "SW");
        PinRecipeQuest(MiasmaCloakQuestPrefix, "Miasma Protection Cloak");
    }

    private void PinRecipeQuest(string questPrefix, string rewardItemName)
    {
        TradeManager.TradeOffer offer = WizardTrade.offers.Find(o => o.RewardItemName == rewardItemName);
        if (offer == null)
        {
            Debug.LogWarning($"MiasmaSubQuestTracker: no trade offer found for '{rewardItemName}' yet - side quest not pinned.");
            return;
        }

        string costText = string.Join(", ", offer.AllCosts().ConvertAll(c => $"{c.Quantity} {DisplayName(c.ItemName)}"));
        QuestManager.Instance?.AddQuest($"{questPrefix}: {costText}", isMainQuest: false);
    }

    private void HandleItemTraded(string itemName, int quantity)
    {
        if (itemName == "SW") RemoveQuestStartingWith(SpecialAxeQuestPrefix);
        else if (itemName == "Miasma Protection Cloak") RemoveQuestStartingWith(MiasmaCloakQuestPrefix);
    }

    private void RemoveQuestStartingWith(string prefix)
    {
        if (QuestManager.Instance == null) return;

        Quest match = QuestManager.Instance.quests.Find(q => q.questName.StartsWith(prefix));
        if (match != null) QuestManager.Instance.RemoveQuest(match.questName);
    }

    private static string DisplayName(string itemName) => ItemCatalog.GetByName(itemName)?.DisplayName ?? itemName;
}

// Implementation Steps:
// 1. Add this component to any GameObject in the scene (alongside the wizard's TradeManager
//    is simplest) - WizardTrade auto-finds the scene's TradeManager if left blank.
// 2. Nothing else to wire: the moment the wizard's miasma quest is accepted, two side quests
//    appear naming exactly what SW and the Miasma Protection Cloak currently cost (read live
//    from TradeManager.offers/MiasmaCloakTradeOffer's appended recipe) - each disappears on
//    its own the instant that item is actually bought.
