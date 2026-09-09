using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NTGD124;

// Per-NPC shop: item-for-item barter (not an abstract currency), offers are this
// instance's own list (not a shared table) - both resolved in GameplayRoadmap.md's Trade
// section. Spend/receive reuses the same IResourceContainer/ResourcePool pattern
// BaseSlotExpander already uses for its own cost-checking (Inventory/Hand wired the same
// way, by UInavigator.Start()) rather than rebuilding cost logic. Deliberately NOT routed
// through ItemPlacementTracker - that's a double-click destination router for
// inventory/hand/boost, trading is a third-party exchange, a different shape entirely (see
// GameplayRoadmap.md Trade blind spot #4).
//
// UI is the hand-built "UI-Trade" panel (mirrors the player's own inventory panel: a
// PanelInventoryT grid of pre-built InventoryIconT slots plus one shared
// ObjectDescriptionT(TMP) label), found by name the same way InventoryManager finds its own
// ObjectDescription(TMP) - nothing to wire in the Inspector. Only the first Offers.Count
// slots get a working ShopSlotUI; any extra pre-built slots beyond that stay hidden. Single
// click on a slot shows that offer's required items in the description label; double click
// attempts the trade, showing "Not enough resources" there instead if it fails.
//
// Triggered by E (InteractableHotspot.OnInteract) on the named NPC object, toggling the
// shop open/closed - deliberately a different event than WizardNpcBehavior's own
// OnPlayerEnterRange-driven miasma dialogue, so the two coexist on the same hotspot without
// fighting over Cursor/SetInputLocked state; ToggleShop still checks
// WizardNpcBehavior.IsConversing so E can't open the shop mid-dialogue.
public class TradeManager : MonoBehaviour
{
    [Serializable]
    public class TradeOffer
    {
        public string CostItemName = "Wood";
        public int CostQuantity = 5;
        [Tooltip("Extra cost items beyond the one above, for recipes that need more than one ingredient (e.g. Skin+Bone+Meat+Crystallized Soul -> SW). Leave empty for a simple single-item trade.")]
        public List<BaseSlotExpander.ItemRequirement> ExtraCosts = new();
        public string RewardItemName = "Food";
        public int RewardQuantity = 1;

        // The primary cost plus any ExtraCosts, as one combined list - lets TryBuy/RefreshSlots
        // treat every offer the same whether it's a single-item trade or a multi-item recipe.
        public List<BaseSlotExpander.ItemRequirement> AllCosts()
        {
            List<BaseSlotExpander.ItemRequirement> all = new()
            {
                new BaseSlotExpander.ItemRequirement { ItemName = CostItemName, Quantity = CostQuantity }
            };
            all.AddRange(ExtraCosts);
            return all;
        }
    }

    [Header("Setup")]
    [Tooltip("Name of the NPC GameObject this shop belongs to - matches NPCManager's npcLabel.")]
    public string npcObjectName = "Wizard";

    [Header("UI (auto-found by name, same trick InventoryManager uses for its own panel)")]
    [Tooltip("Name of the Canvas GameObject to show/hide when the shop opens/closes.")]
    public string tradeCanvasName = "UI-Trade";
    [Tooltip("Name of the panel holding one InventoryIconT-named slot per offer - only the first Offers.Count of them get wired; any extra pre-built slots stay hidden.")]
    public string slotsParentName = "PanelInventoryT";
    [Tooltip("Name of the shared TMP label that shows the selected offer's required items, or \"Not enough resources\" after a failed double-click buy.")]
    public string descriptionObjectName = "ObjectDescriptionT(TMP)";

    [Header("Offers")]
    public List<TradeOffer> offers = new()
    {
        new TradeOffer { CostItemName = "Wood", CostQuantity = 5, RewardItemName = "Food", RewardQuantity = 1 },
        new TradeOffer { CostItemName = "Rock", CostQuantity = 8, RewardItemName = "Helmet", RewardQuantity = 1 },
        new TradeOffer { CostItemName = "Rock", CostQuantity = 12, RewardItemName = "Chestplate", RewardQuantity = 1 },
        new TradeOffer { CostItemName = "Rock", CostQuantity = 6, RewardItemName = "Boots", RewardQuantity = 1 },
        new TradeOffer { CostItemName = "Rock", CostQuantity = 8, RewardItemName = "Pants", RewardQuantity = 1 },
        new TradeOffer
        {
            CostItemName = "Skin", CostQuantity = 1,
            ExtraCosts = new List<BaseSlotExpander.ItemRequirement>
            {
                new() { ItemName = "Bone", Quantity = 1 },
                new() { ItemName = "Meat", Quantity = 1 },
                new() { ItemName = "Crystallized Soul", Quantity = 1 },
            },
            RewardItemName = "SW", RewardQuantity = 1
        },
        new TradeOffer { CostItemName = "Miasma Stick", CostQuantity = 1, RewardItemName = "Magic Stick", RewardQuantity = 1 },
    };

    // Wired automatically by UInavigator.Start() - same pattern as BaseSlotExpander.
    [HideInInspector] public InventoryManager Inventory;
    [HideInInspector] public PlayerHandManager Hand;

    private IResourceContainer[] ResourceSources => new IResourceContainer[] { Inventory, Hand };

    private InteractableHotspot npcHotspot;
    private NPCai npcAi;
    private WizardNpcBehavior wizardDialogue;
    private PlayerController playerController;
    private GameObject tradePanelObject;
    private TMP_Text descriptionText;
    private readonly List<ShopSlotUI> slotUIs = new();
    private bool listenerBound;
    private bool shopOpen;

    // Whether this shop's panel is currently open - lets other systems (UInavigator's
    // cross-panel switching, see GameplayRoadmap.md's UI key-shortcut work) check/close it
    // without needing their own copy of shopOpen.
    public bool IsShopOpen => shopOpen;

    // Fired the instant this shop opens (ToggleShop -> OpenShop) - cue for UInavigator to
    // close whichever other browsing panel (Inventory/Quest Full View/Main Menu) was open,
    // so only one such panel is ever showing at a time. No matching "OnShopClosed" event -
    // CloseShop is public, so anything that wants to close this shop just calls it directly.
    public event Action OnShopOpened;

    void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();

        WireUi();
        RefreshSlots();
        SetShopVisible(false);

        TryBindToNpc();
    }

    void OnDestroy()
    {
        if (npcHotspot != null)
        {
            npcHotspot.OnInteract.RemoveListener(ToggleShop);
            npcHotspot.OnPlayerExitRange.RemoveListener(HandlePlayerExitRange);
        }
    }

    void Update()
    {
        if (!listenerBound) TryBindToNpc();
    }

    // Finds the hand-built "UI-Trade" panel by name (tradeCanvasName/slotsParentName/
    // descriptionObjectName), the same way InventoryManager finds its own PanelInventory/
    // ObjectDescription(TMP) - nothing to drag in the Inspector.
    private void WireUi()
    {
        tradePanelObject = GameObject.Find(tradeCanvasName);
        if (tradePanelObject == null)
        {
            Debug.LogWarning($"TradeManager: no '{tradeCanvasName}' object found - the trade panel won't show/hide.");
        }

        GameObject slotsParentObject = GameObject.Find(slotsParentName);
        if (slotsParentObject == null)
        {
            Debug.LogWarning($"TradeManager: no '{slotsParentName}' object found - no offer slots to wire.");
        }
        else
        {
            WireSlots(slotsParentObject.transform);
        }

        GameObject descriptionObject = GameObject.Find(descriptionObjectName);
        if (descriptionObject != null) descriptionText = descriptionObject.GetComponent<TMP_Text>();
    }

    // Only the first Offers.Count slots (matched by name, same "InventoryIcon"-prefix
    // convention as InventoryManager) get a working ShopSlotUI - any extra pre-built slots
    // beyond that are hidden rather than left sitting there blank and clickable.
    private void WireSlots(Transform slotsParent)
    {
        int wired = 0;
        for (int i = 0; i < slotsParent.childCount; i++)
        {
            Transform child = slotsParent.GetChild(i);
            if (!child.name.StartsWith("InventoryIconT")) continue;

            if (wired >= offers.Count)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            ShopSlotUI slotUI = child.GetComponent<ShopSlotUI>();
            if (slotUI == null) slotUI = child.gameObject.AddComponent<ShopSlotUI>();
            slotUI.Init(wired, this);

            slotUIs.Add(slotUI);
            wired++;
        }
    }

    // The NPC doesn't exist until NPCManager spawns it, so this keeps retrying each frame
    // (cheap no-op once bound) the same way GirlNpcBehavior/WizardNpcBehavior/
    // DogNpcBehavior all do. wizardDialogue is optional - null for any NPC without a
    // WizardNpcBehavior (e.g. a future tradeable NPC that has no conversation of its own),
    // in which case the mid-dialogue guard in ToggleShop is simply never true.
    private void TryBindToNpc()
    {
        GameObject npcObject = GameObject.Find(npcObjectName);
        if (npcObject == null) return;

        InteractableHotspot foundHotspot = npcObject.GetComponent<InteractableHotspot>();
        if (foundHotspot == null) return;

        npcHotspot = foundHotspot;
        npcAi = npcObject.GetComponent<NPCai>();
        wizardDialogue = npcObject.GetComponent<WizardNpcBehavior>();

        npcHotspot.OnInteract.AddListener(ToggleShop);
        npcHotspot.OnPlayerExitRange.AddListener(HandlePlayerExitRange);

        listenerBound = true;
    }

    // Walking out of range while shopping closes it - same reasoning as
    // DogNpcBehavior.HandlePlayerExitRange: no one left in range to press E and close it.
    private void HandlePlayerExitRange()
    {
        if (shopOpen) CloseShop();
    }

    private void ToggleShop()
    {
        if (wizardDialogue != null && wizardDialogue.IsConversing) return;

        if (shopOpen) CloseShop();
        else OpenShop();
    }

    private void OpenShop()
    {
        shopOpen = true;
        SetShopVisible(true);

        if (npcAi != null) npcAi.BeginForcedPause();
        if (playerController != null) playerController.SetInputLocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        OnShopOpened?.Invoke();
    }

    // Public so UInavigator's cross-panel switching (I/Esc closing whatever else is open,
    // see GameplayRoadmap.md) can close this shop the same way its own OpenShop-triggering
    // hotspot (E key) already does internally via ToggleShop.
    public void CloseShop()
    {
        shopOpen = false;
        SetShopVisible(false);

        if (npcAi != null) npcAi.EndForcedPause();
        if (playerController != null) playerController.SetInputLocked(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Single click - shows what this offer actually is (the reward item's full DisplayName,
    // since the slot itself only has room for its placeholder letter, e.g. "SW"/"Mt") plus
    // what it costs (also by DisplayName, not the raw ItemName/icon letter), in the shared
    // description label - same spot InventoryManager.OnSlotClicked shows an owned item's
    // name/category.
    public void OnOfferClicked(int offerIndex)
    {
        if (descriptionText == null || offerIndex < 0 || offerIndex >= offers.Count) return;

        TradeOffer offer = offers[offerIndex];
        string costText = string.Join(", ", offer.AllCosts().ConvertAll(c => $"{c.Quantity} {GetDisplayName(c.ItemName)}"));
        descriptionText.text = $"{GetDisplayName(offer.RewardItemName)} - Requires: {costText}";
    }

    private static string GetDisplayName(string itemName) => ItemCatalog.GetByName(itemName)?.DisplayName ?? itemName;

    // Double click - attempts the trade; on failure (not enough resources), replaces the
    // description label with that instead of leaving the last-shown requirement text up.
    public void OnOfferDoubleClicked(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= offers.Count) return;

        if (!TryBuy(offerIndex) && descriptionText != null) descriptionText.text = "Not enough resources";
    }

    // Spends every cost item (CostItemName plus any ExtraCosts - inventory first, then hand,
    // see ResourceSources) for RewardQuantity of RewardItemName. Costs are only actually
    // removed once every one of them is confirmed available AND the reward is confirmed to
    // fit - if the inventory turns out to be full, nothing is spent, no items lost for
    // nothing. Same all-or-nothing shape as BaseSlotExpander.HasRequiredItems/
    // ConsumeRequiredItems for a multi-item cost. Returns whether the trade went through.
    public bool TryBuy(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= offers.Count) return false;
        TradeOffer offer = offers[offerIndex];

        if (Inventory == null) Inventory = FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
        if (Inventory == null) return false;

        ItemDefinition reward = ItemCatalog.GetByName(offer.RewardItemName);
        if (reward == null)
        {
            Debug.LogWarning($"TradeManager: no ItemCatalog entry named '{offer.RewardItemName}'.");
            return false;
        }

        List<BaseSlotExpander.ItemRequirement> costs = offer.AllCosts();
        foreach (BaseSlotExpander.ItemRequirement cost in costs)
        {
            if (!ResourcePool.HasItems(ResourceSources, cost.ItemName, cost.Quantity)) return false;
        }

        foreach (BaseSlotExpander.ItemRequirement cost in costs)
        {
            ResourcePool.TryRemoveItems(ResourceSources, cost.ItemName, cost.Quantity);
        }

        if (!Inventory.AddItem(reward, offer.RewardQuantity))
        {
            foreach (BaseSlotExpander.ItemRequirement cost in costs)
            {
                ItemDefinition costItem = ItemCatalog.GetByName(cost.ItemName);
                if (costItem != null) Inventory.AddItem(costItem, cost.Quantity);
            }
            return false;
        }

        ResourceEventLog.LogToTrade(offer.RewardItemName, offer.RewardQuantity);
        GameEvents.RaiseItemTraded(offer.RewardItemName, offer.RewardQuantity);

        if (offerIndex < slotUIs.Count)
        {
            slotUIs[offerIndex].PlayPurchasePulse();

            // Gear is a one-per-player purchase (Helmet/Chestplate/Pants/Boots/SW) - once
            // bought, its slot disappears from the shop for the rest of the session instead of
            // staying purchasable forever. Deactivating the slot GameObject directly (rather
            // than removing the offer from the offers list) avoids desyncing every later
            // offer's index against slotUIs, which is a flat 1:1 mapping fixed at WireSlots time.
            if (IsOneTimeItem(reward)) slotUIs[offerIndex].gameObject.SetActive(false);
        }

        return true;
    }

    private static bool IsOneTimeItem(ItemDefinition item) => item.Category == ItemCategory.Armor || item.ItemName == "SW" || item.ItemName == "Magic Stick";

    // Shows what you'd RECEIVE in each slot - the reward item's icon/placeholder letter (plus
    // quantity if more than 1), same spot InventoryManager.RefreshSlot shows an owned item's
    // icon/letter/count, just reward instead of on-hand.
    private void RefreshSlots()
    {
        for (int i = 0; i < slotUIs.Count && i < offers.Count; i++)
        {
            TradeOffer offer = offers[i];
            ItemDefinition reward = ItemCatalog.GetByName(offer.RewardItemName);
            if (reward == null) Debug.LogWarning($"TradeManager: offer {i} has no ItemCatalog entry named '{offer.RewardItemName}'.");
            slotUIs[i].SetDisplay(reward, offer.RewardQuantity);
        }
    }

    private void SetShopVisible(bool visible)
    {
        if (tradePanelObject != null) tradePanelObject.SetActive(visible);
    }
}

// Implementation Steps:
// 1. Add an empty GameObject anywhere in the scene and attach this script - nothing else is
//    required as long as the "UI-Trade" panel (see tradeCanvasName/slotsParentName/
//    descriptionObjectName) already exists, same shape as the player's own inventory panel:
//    a PanelInventoryT grid of InventoryIconT slots (icon/text child + Button, no script
//    needed - ShopSlotUI gets added automatically) and one shared ObjectDescriptionT(TMP)
//    label. Rename tradeCanvasName/slotsParentName/descriptionObjectName if yours use
//    different names.
// 2. Edit the Offers list in the Inspector to taste - each entry is a cost/reward pair (add
//    ExtraCosts entries for a multi-item recipe, e.g. the built-in Skin+Bone+Meat+Special
//    Item -> SW offer), no code changes needed to add/remove/reprice offers. Still mostly a
//    testing-ground default list otherwise - swap in the real offers whenever they're decided.
//    Only the first Offers.Count pre-built InventoryIconT slots get wired/shown - any extra
//    ones in a 28-slot-sized panel stay hidden.
// 3. Press Play, get within E range of the wizard and press E - the panel opens, each wired
//    slot shows the reward item's letter. Single-click a slot to see its required items in
//    the description label; double-click to attempt the trade - it silently succeeds (spends
//    the cost, adds the reward) if affordable, or the description label shows "Not enough
//    resources" if not. Press E again (or walk out of range) to close. Pressing E while his
//    own miasma dialogue is open does nothing - the two don't fight over Cursor/
//    SetInputLocked.
// 4. npcObjectName defaults to "Wizard" - point this at a different NPC's object name to
//    give any other NPC their own shop with a separate TradeManager instance/offer list (and
//    its own UI panel, named to match new tradeCanvasName/slotsParentName/
//    descriptionObjectName values).
