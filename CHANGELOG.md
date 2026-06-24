# Changelog

## v0.5 — Boss & Elite Encounters (2026-06-24)

### Added
- **IEnemyAI strategy interface** (Game.Core): pluggable AI architecture — DefaultEnemyAI, BossEnemyAI, EliteEnemyAI.
- **BossEnemyAI** (Game.Core): phase-aware boss AI with HP threshold enrage (new ability + damage buff at ≤40%), AoE on schedule every 3 turns.
- **EliteEnemyAI** (Game.Core): ability-priority AI that delegates to DefaultEnemyAI after attempting elite passives.
- **CombatEngine.TurnCount + OnTurnStart**: turn counter and event for time-based AI triggers.
- **BossData / EliteData** (Unity): ScriptableObjects extending CharacterData with phase ability, damage buff, phase threshold, and elite passive.
- **TeamRoster pool**: RunManager selects enemy teams by MapNodeType (Boss → boss team, Elite → elite team, Combat → normal team).
- **GameOver scene**: VICTORY/DEFEAT screen with Main Menu return button. Boss node completion triggers victory; queen death triggers defeat.
- **CombatRunner AI dispatch**: inspects CharacterData type at runtime to assign correct AI strategy.
- **BD_InfernalKing.asset**: boss with 30 HP, 5 damage, Fireball + PowerStrike.
- **ED_ShadowAssassin.asset**: elite with 12 HP, 3 damage, Thorns passive.
- **SceneSetup.CreateGameOverScene() + CreateBossEliteAssets()**: editor tooling for scene and data asset generation.

### Changed
- **EnemyTurnAI static class**: extracted to DefaultEnemyAI behind IEnemyAI interface, reducing legacy coupling.
- **CharacterData.CreatePiece**: made virtual so BossData/EliteData can inject AI and passives.

### Tests
- 32 new tests (Boss AI 9, Elite AI 5, Phase 4 integration 8, Data layer 8, Phase 1 core 2)
- All 119 tests passing (103 legacy + 32 new, 0 regressions)

---

## v0.4 — Map System (2026-06-24)

### Added
- **MapNodeType enum + MapNode class** (Game.Core): node types (Combat, Elite, Boss, Rest, Shop) with identity, grid position, and adjacency tracking.
- **MapGraph** (Game.Core): graph data structure with DFS path validation, node visitation, available node queries, and completion detection.
- **MapGenerator** (Game.Core): StS-style procedural graph generation — rows of branching nodes, minimum 3 distinct paths to Boss, retry on invalid graphs.
- **MapView** (Unity): scrollable UI with colored node buttons by type (blue/orange/red/green/yellow), LineRenderer connection paths, disabled non-available nodes.
- **RunManager.MapPhase**: new state machine phase integrating Map→Combat→Reward→Map loop.
- **OnNodeSelected()**: dispatches to Combat/Rest/Shop scenes based on node type.
- **Piece.HealPercentEffective()**: percentage healing from EffectiveMaxHp (for RestNode).
- **Map.unity**: new scene at build index 3 with camera, canvas, event system.
- **SceneSetup.CreateMapScene()**: editor script for full ScrollRect/Content wiring.

### Changed
- **RunState**: replaced linear `CombatIndex`/`TotalCombats` with `MapGraph` + `CurrentNodeId` for graph-based run progression.
- **RunState.AdvanceCombat()**: now walks the graph to the next available node.
- **Piece.Heal()**: fixed to clamp at `EffectiveMaxHp` instead of base `MaxHp`.

### Tests
- 35 new tests (MapGraph, MapGenerator, Piece.HealPercent, RunState graph navigation)
- All 82 tests passing (47 original + 35 new)

---

## v0.3 — Run Loop (2026-06-24)

### Added
- **RunState** (Game.Core): pure C# domain model for run persistence — holds team composition, HP across combats, combat index tracking, ability acquisition, and stat boosts. 26 unit tests.
- **RunManager** (Unity): DontDestroyOnLoad singleton orchestrating scene flow — Combat → Reward → Combat → RunEnd. Handles enemy team configuration per combat index.
- **RewardScreen** (Unity): post-combat reward selection UI — 3 random cards from 6-option pool (stat boosts or new abilities). Applies reward to random alive piece.
- **Piece.AddAbility()**: runtime ability acquisition support for run progression.
- **Piece bonus stats**: `_bonusDamage`, `_bonusHp`, `_bonusAttackRange`, `_bonusMoveRange` fields with `AddBonus*()` methods.
- **Reward.unity**: new scene with Canvas, card buttons, EventSystem, Camera.
- **Scene registration**: Combat.unity (index 1) and Reward.unity (index 2) registered in Build Settings.

### Changed
- **CombatRunner**: refactored into `Initialize(RunState, int)` primary entry point + `InitializeDemo()` fallback for direct scene editing. Exposes `CombatEnded` event for RunManager consumption.
- **CombatView**: removed `DontDestroyOnLoad` from victory banner to prevent scene-leak on transitions.
- **Piece.Heal()**: fixed to clamp at `EffectiveMaxHp` instead of base `MaxHp`, enabling HP boosts to work correctly.

### Tests
- 31 new tests (5 Piece.AddAbility/bonus stats + 26 RunState behavior)
- All 47 tests passing (16 original + 31 new)

---

## v0.2 — Ability System (2026-06-22)

### Added
- CharacterData / AbilityData ScriptableObjects
- Active and passive abilities (Fireball, Heal, PowerStrike, Regen, Thorns, WarAura)
- Buff/debuff system with duration tracking
- Aura system (War Aura: +1 damage to nearby allies)
- Mana bar UI
- Input 1/2/3 for ability activation
- 16 tests for abilities, buffs, and passive triggers

---

## v0.1 — Core Combat Engine (2026-06-20)

### Added
- Hex grid with axial coordinates
- Piece system (Queen + Pawn per team)
- Combat engine with turn ordering by initiative
- Movement, attack, and action economy
- Basic AI (attack when in range, approach when out)
- Win condition (Queen death ends combat)
- View layer: CombatView, PieceView, TileView, HexLayout
- PlayerInputController (mouse/keyboard)
- 9 tests covering hex math, BFS, combat, and AI
