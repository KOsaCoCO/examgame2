using UnityEngine;
using NTGD124;

// Central hub that finds all four UI canvases and wires up how they get
// shown/hidden. Attach this script to any one GameObject in the scene -
// everything else (QuestManager, the UI scripts, key bindings) is found
// or created automatically at runtime.
public class UInavigator : MonoBehaviour
{
    [Header("UI canvas names (must match the GameObject names in the scene)")]
    public string questCanvasName = "UI-Quest";
    public string inventoryCanvasName = "UI-Inventory";
    public string questFullViewCanvasName = "UI-QuestFUllView";
    public string promptActionHubCanvasName = "ButtonHubText";
    public string mainMenuCanvasName = "UI-MainMenu";
    public string healthAmountTmpName = "HealthAmountTMP";
    public string inventoryPanelName = "PanelInventory";
    public string playerViewPanelName = "player view in inventory";
    public string handToolsPanelName = "HandTools";

    [Header("Resource node spawning (set in the scene - these components aren't runtime-created, so their serialized values can be edited directly, unlike ResourceNodeSpawner's own fields)")]
    [Tooltip("tree_1.prefab - scattered across the terrain as Wood/Cutting nodes.")]
    public GameObject[] treePrefabs;
    public int treeCountPerPrefab = 200;
    [Tooltip("All 11 rock prefab variants - scattered across the terrain as Rock/Mining nodes.")]
    public GameObject[] rockPrefabs;
    public int rockCountPerPrefab = 10;

    private UIQuest uiQuest;
    private UIInventory uiInventory;
    private UIQuestFullView uiQuestFullView;
    private MainMenuController mainMenu;
    private TradeManager tradeManager;
    private GirlNpcBehavior girlDialogue;
    private WizardNpcBehavior wizardDialogue;
    private DogNpcBehavior dogBehavior;
    private PlayerController playerController;

    void Start()
    {
        EnsureQuestManagerExists();

        uiQuest = FindUIComponent<UIQuest>(questCanvasName);
        uiInventory = FindUIComponent<UIInventory>(inventoryCanvasName);
        uiQuestFullView = FindUIComponent<UIQuestFullView>(questFullViewCanvasName);
        mainMenu = FindUIComponent<MainMenuController>(mainMenuCanvasName);
        // Continue's own click handler closes the menu directly (MainMenuController.Close()),
        // bypassing HandleEscape's RefreshCursorLock() call entirely - without this, the cursor
        // stayed unlocked/visible after Continue until some other panel toggle happened to run
        // RefreshCursorLock() next. Harmless to also fire on an Esc-triggered close - just a
        // second, idempotent re-read of the same panel states.
        if (mainMenu != null) mainMenu.OnClosed += RefreshCursorLock;
        FindUIComponent<UIPromptActionHub>(promptActionHubCanvasName); // 3D text on ButtonHUB's face, always visible
        FindUIComponent<HealthAmountUI>(healthAmountTmpName); // numeric HP readout, UI's/UI-Player/Healthbar

        girlDialogue = FindAnyObjectByType<GirlNpcBehavior>(FindObjectsInactive.Include);
        wizardDialogue = FindAnyObjectByType<WizardNpcBehavior>(FindObjectsInactive.Include);
        dogBehavior = FindAnyObjectByType<DogNpcBehavior>(FindObjectsInactive.Include);
        if (wizardDialogue != null) wizardDialogue.OnDialogueOpened += HandleWizardDialogueOpened;

        InventoryManager inventoryManager = FindUIComponent<InventoryManager>(inventoryPanelName);
        BoostSlotManager boostManager = FindUIComponent<BoostSlotManager>(playerViewPanelName);
        PlayerHandManager handManager = FindUIComponent<PlayerHandManager>(handToolsPanelName);

        ItemPlacementTracker tracker = GetComponent<ItemPlacementTracker>();
        if (tracker == null) tracker = gameObject.AddComponent<ItemPlacementTracker>();

        if (GetComponent<MainQuestProgress>() == null) gameObject.AddComponent<MainQuestProgress>();

        tracker.Inventory = inventoryManager;
        tracker.Hand = handManager;
        tracker.Boosts = boostManager;

        if (inventoryManager != null) inventoryManager.Tracker = tracker;
        if (boostManager != null) boostManager.Tracker = tracker;
        if (handManager != null) handManager.Tracker = tracker;

        // BaseSlotExpander (the 9 unlockable base/land slots) isn't auto-created like the
        // managers above - it's placed and configured by hand on "BaseSlots" in the scene -
        // so only its Inventory reference gets wired here, not the component itself.
        BaseSlotExpander baseSlotExpander = FindAnyObjectByType<BaseSlotExpander>();
        if (baseSlotExpander != null)
        {
            baseSlotExpander.Inventory = inventoryManager;
            baseSlotExpander.Hand = handManager;
        }

        // Same story as BaseSlotExpander - placed and configured by hand (its shop UI is
        // scene-specific), only Inventory/Hand get wired here. Not every scene necessarily
        // has one yet (wizard's shop is the first), so this is just a no-op until it does.
        tradeManager = FindAnyObjectByType<TradeManager>();
        if (tradeManager != null)
        {
            tradeManager.Inventory = inventoryManager;
            tradeManager.Hand = handManager;
            tradeManager.OnShopOpened += HandleShopOpened;
        }

        SpawnResourceNodes("TreeSpawner", treePrefabs, treeCountPerPrefab, "Wood", ToolSubCategory.Cutting);
        SpawnResourceNodes("RockSpawner", rockPrefabs, rockCountPerPrefab, "Rock", ToolSubCategory.Mining);

        playerController = FindAnyObjectByType<PlayerController>();

        if (uiInventory != null) uiInventory.Hide();
        if (uiQuestFullView != null) uiQuestFullView.Hide();
        if (mainMenu != null) mainMenu.Close();

        if (playerController != null)
        {
            playerController.onToggleInventory.AddListener(HandleToggleInventory);
            playerController.onToggleQuestFullView.AddListener(HandleToggleQuestFullView);
            playerController.onEscape.AddListener(HandleEscape);
        }

        if (uiQuest != null) uiQuest.Show();

        RefreshCursorLock();
    }

    // Pushes the current dialogue-key restrictions to PlayerController every frame (see
    // SetModalDialogueState) - girl/wizard block E too (neither needs a further press once
    // open), the dog's own pickup-confirm only blocks I/Q, since its second E press is what
    // actually completes the pickup.
    void Update()
    {
        if (playerController == null) return;

        bool blockToggleKeys = IsModalDialogueActive();
        bool blockInteractKey = (girlDialogue != null && girlDialogue.IsConversing) ||
                                 (wizardDialogue != null && wizardDialogue.IsConversing);

        playerController.SetModalDialogueState(blockToggleKeys, blockInteractKey);
    }

    // Mouse-look (PlayerController.Look) only runs while the cursor is locked - so
    // the cursor unlocks and becomes visible whenever a panel the player needs to
    // click on (inventory, full quest view, a Trade shop, the Main Menu, or a modal NPC
    // dialogue - girl/wizard/dog) is open, and re-locks once all of them are closed.
    // Called after every panel open/close. Modal dialogues matter here too: they set
    // Cursor.lockState/visible directly themselves (see WizardNpcBehavior.BeginConversation),
    // but anything that calls RefreshCursorLock() afterward (e.g. HandleWizardDialogueOpened)
    // would otherwise immediately stomp that back to Locked/invisible without this check.
    private void RefreshCursorLock()
    {
        bool uiOpen = (uiInventory != null && uiInventory.gameObject.activeSelf) ||
                      (uiQuestFullView != null && uiQuestFullView.gameObject.activeSelf) ||
                      (tradeManager != null && tradeManager.IsShopOpen) ||
                      (mainMenu != null && mainMenu.IsOpen) ||
                      IsModalDialogueActive();

        Cursor.lockState = uiOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = uiOpen;
    }

    // Opens/closes the full quest view, and hides the corner quest HUD
    // while it's open so the two don't overlap on screen.
    public void ToggleQuestFullView()
    {
        if (uiQuestFullView == null) return;

        bool isOpening = !uiQuestFullView.gameObject.activeSelf;
        uiQuestFullView.Toggle();

        if (uiQuest != null)
        {
            if (isOpening) uiQuest.Hide();
            else uiQuest.Show();
        }
    }

    // True while an NPC dialogue that requires a direct answer (Girl/Wizard's Yes/No
    // exchanges, the dog's pickup confirm) owns the player's input/cursor - I/Q/Esc
    // deliberately leave these alone rather than popping a browsing panel over them or
    // canceling a forced exchange mid-flow (E is still how each of these gets answered).
    private bool IsModalDialogueActive()
    {
        return (girlDialogue != null && girlDialogue.IsConversing) ||
               (wizardDialogue != null && wizardDialogue.IsConversing) ||
               (dogBehavior != null && dogBehavior.IsBusy);
    }

    private void CloseInventoryIfOpen()
    {
        if (uiInventory != null && uiInventory.gameObject.activeSelf) uiInventory.Hide();
    }

    private void CloseQuestFullViewIfOpen()
    {
        if (uiQuestFullView == null || !uiQuestFullView.gameObject.activeSelf) return;
        uiQuestFullView.Hide();
        if (uiQuest != null) uiQuest.Show();
    }

    private void CloseTradeShopIfOpen()
    {
        if (tradeManager != null && tradeManager.IsShopOpen) tradeManager.CloseShop();
    }

    private void CloseMainMenuIfOpen()
    {
        if (mainMenu != null && mainMenu.IsOpen) mainMenu.Close();
    }

    // I key (PlayerController.onToggleInventory) - closes whichever other browsing panel
    // (Quest Full View, an NPC's Trade shop, the Main Menu) is open before opening Inventory,
    // so only one such panel is ever showing at once. Left alone entirely while a modal NPC
    // dialogue is active.
    private void HandleToggleInventory()
    {
        if (uiInventory == null) return;

        if (!uiInventory.gameObject.activeSelf) // about to open
        {
            if (IsModalDialogueActive()) return;
            CloseQuestFullViewIfOpen();
            CloseTradeShopIfOpen();
            CloseMainMenuIfOpen();
        }

        uiInventory.Toggle();
        RefreshCursorLock();
    }

    // Q key (PlayerController.onToggleQuestFullView) - same cross-panel-closing shape as
    // HandleToggleInventory, just for the full quest view instead.
    private void HandleToggleQuestFullView()
    {
        if (uiQuestFullView == null) return;

        if (!uiQuestFullView.gameObject.activeSelf) // about to open
        {
            if (IsModalDialogueActive()) return;
            CloseInventoryIfOpen();
            CloseTradeShopIfOpen();
            CloseMainMenuIfOpen();
        }

        ToggleQuestFullView();
        RefreshCursorLock();
    }

    // TradeManager.OnShopOpened - a Trade shop opens via E on its NPC's own hotspot, not
    // through this hub, so it can't close the other panels itself the way HandleToggleInventory/
    // HandleToggleQuestFullView do for each other.
    private void HandleShopOpened()
    {
        CloseInventoryIfOpen();
        CloseQuestFullViewIfOpen();
        CloseMainMenuIfOpen();
        RefreshCursorLock();
    }

    // WizardNpcBehavior.OnDialogueOpened - his dialogue is proximity-triggered, independent of
    // this hub, same reasoning as HandleShopOpened above. Also closes the Trade shop (on top of
    // BeginConversation's own guard against opening while the shop is already open) purely for
    // symmetry/defense-in-depth - the two open events shouldn't ever be able to stack.
    private void HandleWizardDialogueOpened()
    {
        CloseInventoryIfOpen();
        CloseQuestFullViewIfOpen();
        CloseTradeShopIfOpen();
        CloseMainMenuIfOpen();
        RefreshCursorLock();
    }

    // Esc key (PlayerController.onEscape). With the Main Menu open: closes it and nothing
    // reopens automatically - each panel only comes back via its own keybind. Otherwise (and
    // only outside a modal NPC dialogue, which Esc leaves alone): force-closes every open
    // browsing panel and opens the Main Menu, all in the one press.
    private void HandleEscape()
    {
        if (mainMenu != null && mainMenu.IsOpen)
        {
            mainMenu.Close();
            RefreshCursorLock();
            return;
        }

        if (IsModalDialogueActive()) return;

        CloseInventoryIfOpen();
        CloseQuestFullViewIfOpen();
        CloseTradeShopIfOpen();

        mainMenu?.Open();
        RefreshCursorLock();
    }

    // Creates (or reuses) a dedicated holder GameObject for a ResourceNodeSpawner
    // configured for one resource type - a separate holder per type rather than
    // multiple ResourceNodeSpawner instances on this same GameObject, so each
    // stays independently findable/configurable via GetComponent.
    private void SpawnResourceNodes(string holderName, GameObject[] prefabs, int countPerPrefab, string resourceItemName, ToolSubCategory requiredTool)
    {
        if (prefabs == null || prefabs.Length == 0) return;

        GameObject holder = GameObject.Find(holderName);
        if (holder == null) holder = new GameObject(holderName);

        ResourceNodeSpawner spawner = holder.GetComponent<ResourceNodeSpawner>();
        if (spawner == null) spawner = holder.AddComponent<ResourceNodeSpawner>();

        spawner.prefabs = prefabs;
        spawner.countPerPrefab = countPerPrefab;
        spawner.resourceItemName = resourceItemName;
        spawner.requiredTool = requiredTool;
    }

    private void EnsureQuestManagerExists()
    {
        if (QuestManager.Instance == null)
        {
            GameObject questManagerObject = new("QuestManager");
            questManagerObject.AddComponent<QuestManager>();
        }
    }

    // Finds the canvas by name and makes sure it has the given UI script on it,
    // adding the script automatically if it's missing.
    private T FindUIComponent<T>(string gameObjectName) where T : Component
    {
        GameObject found = GameObject.Find(gameObjectName);
        if (found == null)
        {
            Debug.LogWarning($"UInavigator could not find a GameObject named '{gameObjectName}' in the scene.");
            return null;
        }

        T component = found.GetComponent<T>();
        if (component == null) component = found.AddComponent<T>();

        return component;
    }
}
