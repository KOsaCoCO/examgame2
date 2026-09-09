using System.Collections.Generic;
using UnityEngine;

namespace NTGD124
{
    // Appends the Runed Rock trade recipe to the wizard's own TradeManager at runtime, same
    // reasoning/pattern as MiasmaCloakTradeOffer - keeps TradeManager itself generic, since
    // this recipe is wizard-specific. Runs in Awake() so the offer is already in the list
    // before TradeManager's own Start() wires/refreshes its UI slots off Offers.Count.
    //
    // Recipe: 3 Crystallized Souls (the only source is a 50/50 drop off Hard mobs - see
    // ItemDropRates.HardMobDrops - so this doubles as proof of having hunted 3 Hard mobs) plus
    // 2 of each Night plant category (Night Mushroom + Night Flower) -> 1 Runed Rock, base
    // slot 8's unlock cost (see BaseSlotExpander).
    [RequireComponent(typeof(TradeManager))]
    public class RunedRockTradeOffer : MonoBehaviour
    {
        private const string RunedRockItemName = "Runed Rock";

        void Awake()
        {
            TradeManager tradeManager = GetComponent<TradeManager>();
            if (tradeManager == null) return;

            bool alreadyPresent = tradeManager.offers.Exists(offer => offer.RewardItemName == RunedRockItemName);
            if (alreadyPresent) return;

            tradeManager.offers.Add(new TradeManager.TradeOffer
            {
                CostItemName = "Crystallized Soul",
                CostQuantity = 3,
                ExtraCosts = new List<BaseSlotExpander.ItemRequirement>
                {
                    new() { ItemName = "Night Mushroom", Quantity = 2 },
                    new() { ItemName = "Night Flower", Quantity = 2 },
                },
                RewardItemName = RunedRockItemName,
                RewardQuantity = 1
            });
        }
    }
}

// Implementation Steps:
// 1. Add this component to the same GameObject the wizard's TradeManager lives on (requires
//    TradeManager already present there) - same object MiasmaCloakTradeOffer sits on.
// 2. Nothing else to configure - on Awake() it appends the Runed Rock recipe to that
//    TradeManager's own Offers list (skipped if a Runed Rock offer is already present).
// 3. Runed Rock shows up as a normal offer in the wizard's shop from then on - double-click it
//    to trade 3 Crystallized Souls + 2 each of Night Mushroom/Night Flower for it, same as any
//    other offer.
