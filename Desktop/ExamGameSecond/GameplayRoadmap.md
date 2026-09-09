# Gameplay Roadmap

Everything in the original build-out (quest chain, items/resources, trade, base expansion,
quest beacon, armor/healing/miasma sub-quests, UI/menu, player/wizard model + combat rebalance,
and this session's polish pass) is done. Two things remain before the core gameplay loop is
finished:

- [ ] **Finish the wizard quest chain end-to-end**: buy SW at the wizard's shop -> fell the
      miasma tree (`MiasmaTree.cs`, on `tree_1 Big`) -> receive the Miasma Stick -> turn in at
      the wizard -> Magic Stick trade. Confirm the miasma fog hazard/cloak immunity, the two
      pinned side quests, and the tree's hit feedback all play correctly along the way.
- [ ] **Tower defense / boss fight**: play through `NightBossSpawner`/`NightBossAi` (Monster35,
      spawns once player level 6+ / the wizard's miasma quest is turned in / night begins) -
      confirm the base-slot/fence-wall siege behavior, NPC-threatening behavior, and that
      defeating its 100-hit health bar completes the quest. Base defense/tower-defense mechanics
      beyond this single boss encounter are still undesigned - needs its own planning session if
      more is wanted here.

Base-slot furnishing (dressing up each unlocked slot with props) is no longer being pursued -
removed from this list as complete/out of scope.

Companion docs (don't duplicate, cross-reference): `ResourceEconomyDesignNotes.txt` (Trade/
Configure economy shape), `InventorySystemNotes.md` (inventory system's own open items/bugs).
