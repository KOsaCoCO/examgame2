using System.Collections.Generic;
using UnityEngine;

namespace NTGD124
{
    // Appends the Miasma Protection Cloak trade recipe to the wizard's own TradeManager at
    // runtime instead of hand-editing its Offers list in the Inspector - keeps TradeManager
    // itself generic/reusable for any NPC (per its own doc comment), since this recipe is
    // wizard-specific. Runs in Awake() (not Start()) so the offer is already in the list before
    // TradeManager's own Start() wires/refreshes its UI slots off Offers.Count.
    //
    // Recipe: one of each Night plant category (Night Mushroom + Night Flower) and one of each
    // Day plant category (Day Mushroom + Day Flower) - 4 ingredients total -> 1 Miasma Protection
    // Cloak. See HarvestablePlant/PlantSpawnerBase for where these are gathered, and
    // MiasmaFogArea/PlayerHealth.IsImmuneToMiasma for what wearing the cloak actually does.
    // Formerly rewarded a drink-once "Elixir" - immunity now comes from wearing this cloak in the
    // Chestplate boost slot instead (see PlayerHealth), so equipping it (and unequipping it) is
    // all that's needed; there's nothing left to double-click/consume.
    [RequireComponent(typeof(TradeManager))]
    public class MiasmaCloakTradeOffer : MonoBehaviour
    {
        private const string CloakItemName = "Miasma Protection Cloak";

        void Awake()
        {
            TradeManager tradeManager = GetComponent<TradeManager>();
            if (tradeManager == null) return;

            bool alreadyPresent = tradeManager.offers.Exists(offer => offer.RewardItemName == CloakItemName);
            if (alreadyPresent) return;

            tradeManager.offers.Add(new TradeManager.TradeOffer
            {
                CostItemName = "Night Mushroom",
                CostQuantity = 1,
                ExtraCosts = new List<BaseSlotExpander.ItemRequirement>
                {
                    new() { ItemName = "Night Flower", Quantity = 1 },
                    new() { ItemName = "Day Mushroom", Quantity = 1 },
                    new() { ItemName = "Day Flower", Quantity = 1 },
                },
                RewardItemName = CloakItemName,
                RewardQuantity = 1
            });
        }
    }
}

// Implementation Steps:
// 1. Add this component to the same GameObject the wizard's TradeManager lives on (requires
//    TradeManager already present there).
// 2. Nothing else to configure - on Awake() it appends the cloak recipe to that TradeManager's
//    own Offers list (skipped if a Miasma Protection Cloak offer is already present, so it's
//    safe if re-run).
// 3. Miasma Protection Cloak shows up as a normal offer in the wizard's shop from then on -
//    double-click it to trade the 4 ingredients, then double-click it in your inventory or hand
//    to equip it into the Chestplate boost slot (same as any other armor piece) - immunity is
//    granted for as long as it stays equipped there (PlayerHealth.IsImmuneToMiasma), and lost the
//    moment it's unequipped.
