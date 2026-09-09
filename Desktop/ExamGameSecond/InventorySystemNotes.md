# Inventory System - Session Notes

Summary of the inventory work done so far, for picking this back up in a future session.
No real item art yet - everything uses capital-letter TMP placeholders (H/C/B/P/W/R/F).

## Scene changes (Assets/Scenes/SampleScene.unity)

- Fixed the 28 `InventoryIcon` slots under `PanelInventory` so they form a clean 7x4
  reading-order grid (left-to-right, then wrap down) - previously their anchored
  positions were scrambled relative to their hierarchy names.
- Removed the `Image` child GameObject from each of the 28 icons (it was rendering on
  top of and hiding the `Text (TMP)` child). Kept the icon's own base Image/Button.
- Cleared the default `m_text: Button` placeholder on each icon's `Text (TMP)` so slots
  start blank and only show a letter once an item is placed there.

## Item data (Assets/MyScripts/Items/)

- `ItemEnums.cs` - `ItemCategory` (Armor/Resource/Tool), `ResourceSubCategory`
  (Edible/NonEdible), `ToolSubCategory` (Building/Cutting, not used yet),
  `ArmorSlotType` (Helmet/Chestplate/Boots/Pants).
- `ItemDefinition.cs` - plain class (not a ScriptableObject) describing one item type:
  name, category, subcategories, armor slot, max stack size, placeholder letter.
- `ItemCatalog.cs` - the 7 test items, hardcoded:
  - Armor (stack 1): Helmet(H), Chestplate(C), Boots(B), Pants(P)
  - Resource (stack 99): Wood(W, NonEdible), Rock(R, NonEdible), Food(F, Edible)
- `ItemDropRates.cs` - empty template for drop-rate/rarity data, not wired up to
  anything yet (no resource nodes exist to drop items).

## Inventory core (Assets/MyScripts/Inventory/)

- `InventorySlotData.cs` - plain `{ ItemDefinition Item; int Quantity; }` + `IsEmpty`.
- `InventorySlotUI.cs` - runtime component on each icon; shows the placeholder
  letter/count in black, reports single vs double clicks.
- `StackingHelper.cs` - the shared "stack onto a matching slot first, then fall
  back to an empty one" placement logic, used identically by both
  `InventoryManager.AddItem` and `PlayerHandManager.TryPlaceItem`. Added after
  they drifted apart: inventory already stacked onto matching items, but hand
  only ever looked for an empty slot, so moving items into hand never joined
  an existing stack there (reported bug). `TryPlace` pre-checks total capacity
  before touching anything, so a call either fully succeeds or changes nothing
  at all - closes a latent duplication risk where a move that didn't fully fit
  could still partially apply while reporting failure to the caller deciding
  whether to remove the source.
- `InventoryManager.cs` - the 28-slot inventory, attached automatically to
  `PanelInventory`. Stacks to 99 via `StackingHelper`, keeps armor to one item
  per slot, updates `ObjectDescription(TMP)` on single-click. Has a
  `[ContextMenu] Debug: Add Test Items` helper for quick testing in Play mode.
- `PlayerHandManager.cs` - 4 hand-tool slots (was a single "held item" slot with its
  own runtime-created placeholder box; that's gone now). Attaches to the real
  `HandTools` panel (`UI-Player/HandTools`) and wires its 4 existing children -
  `Hand`/`Hand1`/`Hand2`/`Hand3` - the same way `BoostSlotManager` wires the 4 boost
  panels. Those 4 children had no `Text (TMP)` child in the scene, so `HandSlotUI`
  creates one at runtime per slot (mirrors `BoostSlotManager.CreateLabel`).
  `TryPlaceItem` stacks onto a matching slot first via `StackingHelper`, same as
  inventory; double-clicking a filled slot returns that specific slot's item to
  the inventory (`ItemPlacementTracker.HandleHandDoubleClick(slotIndex,
  moveWholeStack)`). Hand slots can now hold a stack (not just quantity 1) - see
  "Stack move hotkey" below. Runtime-created labels (here and in
  `BoostSlotManager`) have `raycastTarget = false` so they
  can't become a second click target layered on top of the slot's Image.
- `InventoryHotkeys.cs` - the one place inventory-related hotkeys are read from the
  keyboard (currently just `MoveWholeStackHeld()` for Shift). Uses the new Input
  System (`Keyboard.current`) since this project's Active Input Handling is set to
  "Input System Package (New)" only - legacy `Input.GetKey` would silently no-op.
- `BoostSlotManager.cs` / `BoostSlotUI.cs` - the 4 armor "boost" slots next to the
  player preview, attached to the existing `player view in inventory` GameObject.
  Mapped by position: `PanelBoost2`(top-left)=Helmet, `PanelBoost`(top-right)=Chestplate,
  `PanelBoost3`(bottom-left)=Boots, `PanelBooost1`(bottom-right)=Pants. (Note: that
  object is misspelled "PanelBooost1" in the scene - left as-is.)
- `ItemPlacementTracker.cs` - the single source of truth for where an item currently
  lives (inventory slot / hand / boost slot). All the managers above only expose
  simple primitives (`GetSlot`, `TryPlace`, `RemoveFromSlot`); the tracker is the
  only place that decides to move an item, and only clears the source once the
  destination confirms it accepted the item. This was added specifically to fix a bug
  where double-clicking could leave items duplicated or stuck.
  Double-click routing by category now goes through an explicit `categoryRoutes`
  dictionary (built in `Awake`) instead of a single Armor-vs-everything-else
  ternary. That ternary had a real bug: `item.Category == Armor && Boosts != null
  ? ... : Hand...` would send Armor to the Hand if `Boosts` were ever null, because
  `&&` short-circuits the whole condition to false. The dictionary form fails
  closed per category instead (missing manager -> move refused, not misrouted).

## Wiring (Assets/MyScripts/UInavigator.cs)

Everything auto-wires at Play - no manual component attaching needed in the Editor.
`UInavigator.Start()` now also finds/creates `InventoryManager` (on `PanelInventory`),
`BoostSlotManager` (on `player view in inventory`), `PlayerHandManager` (on
`HandTools`), and `ItemPlacementTracker`, and cross-wires their references.

## Current behavior

- Press **I** to toggle the inventory panel (unchanged, pre-existing).
- Double-click a filled Wood/Rock/Food (or future Tool) slot -> item moves to the
  first empty HandTools slot (`Hand`/`Hand1`/`Hand2`/`Hand3` under `UI-Player`).
- Double-click a filled Helmet/Chestplate/Boots/Pants slot -> item moves to its
  matching boost slot instead of the hand.
- Double-click a filled hand slot or boost slot -> item returns to the first
  available inventory slot.
- Single-click any filled slot -> updates `ObjectDescription(TMP)` with the item's
  name and category.
- **Stack move hotkey**: plain double-click always moves exactly 1 unit (armor is
  unaffected either way, since it never stacks past 1). Hold **Shift** while
  double-clicking a stacked inventory slot to send the whole stack to the first
  empty hand slot in one go; hold Shift while double-clicking a stacked hand slot
  to send the whole stack back to the inventory in one go. Read via
  `InventoryHotkeys.MoveWholeStackHeld()`.

## Known open items / things to revisit

- Reported bug: items placed in a hand slot weren't returning to the inventory on
  double-click. Couldn't reproduce by running the game (no Unity Editor access this
  session), so this was investigated by static review only. Found and fixed one real
  bug that could cause mis-routing (the `&&` short-circuit above) and one defensive
  fix (disabled `raycastTarget` on the runtime-created labels so they can't act as a
  second click target layered over the slot). `HandleHandDoubleClick`'s own logic
  looked correct on inspection. If the problem persists after this, the next thing to
  check in-editor is whether the open `PanelInventory`/`UI-Inventory` panel visually
  and functionally overlaps `HandTools` (bottom-right of screen) when both are shown
  at once - if `UI-Inventory` draws on top, its background could be eating the click
  before it reaches the hand slot underneath, the same class of bug already flagged
  below for `playerViewInsert`/boost slots.
- `HandTools`'s 4 slots have no per-item icon art either (same TMP-letter placeholder
  as everything else) - `HandSlotUI` creates a black-on-transparent label at runtime
  for each, matching `BoostSlotManager`'s fallback.
- Nothing currently limits Resources (Wood/Rock/Food) from filling all 4 hand slots -
  by design for now (confirmed: "any [non-armor] item can go into hand tool"), but
  worth revisiting once real Tool items exist and hand slots are meant to feel more
  tool-specific.
- No hotbar key bindings (1-4, scroll, etc.) exist yet to select/equip a specific hand
  slot - the 4 slots are just 4 independent storage spots for now, filled/emptied only
  via double-click.
- `playerViewInsert` is the last child under `player view in inventory`, so it renders
  on top of the 4 boost panels. If its bounds overlap them, it could block boost-slot
  clicks the same way the old per-icon Image blocked text - not yet confirmed either
  way in-editor.
- `playerViewInsert` is currently just an empty flexible placeholder box (per explicit
  choice to keep it simple for now) - no 3D player model/camera render hooked up yet.
- Boost slot -> armor type mapping (top-left/top-right/bottom-left/bottom-right) was
  an arbitrary but reasonable choice; easy to remap in `BoostSlotManager.SlotMap` if
  a different layout is wanted.
- `ItemDropRates.cs` is an empty template - needs real entries once resource-gathering
  nodes exist.
- No real icon art/sprites yet, only capital-letter TMP placeholders.
