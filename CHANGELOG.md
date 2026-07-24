# Changelog

## v0.7.14 — Phase 3 Exit Evidence (2026-07-24)

### Changed
- **Core reward outcomes**: `RunState.ApplyReward` now owns deterministic
  option-to-recipient application for damage, attack range, move range, max HP
  plus current HP, and new abilities; `RewardScreen` delegates to this contract.
- **Recipient confirmation clarity**: reward cards keep their exact before→after
  previews while the title visibly restates the selected recipient's name.

### Evidence
- Behavior-first Core coverage proves every outcome, selected-recipient isolation,
  ability application, identical-input final snapshots, and meaningfully different
  deterministic two-run build histories from different seeds and choices.
- Focused suites: **71/71 EditMode** and **5/5 RunLoop PlayMode** tests passed.
- Full suites: **230/230 EditMode** and **31/31 PlayMode** tests passed.
- Project baseline validation passed; `git diff --check` is clean.

### Scope
- Phase 3 exit criteria are closed. Thorns content correctness remains separate
  Phase 4 work and is unchanged.

## v0.7.13 — Production Ability Exclusions (2026-07-24)

### Added
- **Observable normal-tier ability choices**: Power Strike, Mend, and Regeneration
  now have authored reward definitions alongside Fireball in the normal pool.
- **Reciprocal specialization rules**: Power Strike excludes Fireball and Fireball
  excludes Power Strike; Mend excludes Regeneration and Regeneration excludes Mend.
  Existing canonical full ability identity drives recipient filtering.

### Scope
- Elite and boss pools remain deterministic stat-specialization tiers.
- Thorns and War Aura have no exclusion metadata because Thorns targeting is not yet
  verified and that pairing was not approved.

### Verification
- Focused `RewardScreenTests`: **25/25** passed against real production assets.
- Full suites: **221/221 EditMode** and **31/31 PlayMode** tests passed.
- Project baseline validation passed; `git diff --check` is clean.

## v0.7.12 — Recipient-Compatible Rewards (2026-07-24)

### Changed
- **Recipient-first rewards**: the Reward scene now presents alive units before
  generating seeded authored offers for the selected recipient; choosing a card
  applies it directly to that unit and continues through the existing scene flow.
- **Deterministic compatibility**: incompatible authored ability definitions are
  filtered with the canonical gameplay signature, then compatible definitions fill
  available slots without duplicate picks or replacement. Exhausted pools may show
  fewer cards or advance without a reward when none remain, while null or empty
  authored pools retain the deterministic fallback.
- **Recipient-specific previews**: reward cards show exact current-to-result values
  for the selected unit without mutating run state before selection.

### Scope
- Production reward assets intentionally define no incompatibility groups; synthetic
  definitions and recipients prove the mechanic without establishing balance policy.

### Verification
- Focused suites: **19/19 RewardScreen EditMode** and **5/5 RunLoop PlayMode** tests passed.
- Full suites: **215/215 EditMode** and **31/31 PlayMode** tests passed.
- Project baseline validation passed; `git diff --check` is clean.

## v0.7.11 — Distinct Reward Tiers (2026-07-24)

### Changed
- **Observable tier identity**: normal offers remain modest, elite offers combine
  stronger damage and vitality with movement, and boss offers combine the strongest
  damage and vitality boosts with attack range.
- **Conservative reward effects**: all tier distinctions use existing stat,
  max-HP, and ability effects; selection plumbing and legacy fallback are unchanged.

### Verification
- Focused `RewardScreenTests`: **15/15** passed, including real authored pool
  distinction and same-seed determinism.
- Full suites: **211/211 EditMode** and **31/31 PlayMode** tests passed.
- Project baseline validation passed.

## v0.7.10 — Authored Reward Pools (2026-07-24)

### Added
- **Encounter-tier reward pools**: separate authored normal, elite, and boss
  `RewardPoolData` assets are selected from the retained encounter type.
- **Deterministic pool sampling**: seeded selection does not repeat indices; null
  or empty authored pools use the deterministic legacy inline fallback.

### Changed
- **Assembly ownership**: Unity-independent reward domain models now live in
  `Game.Core`, preserving the existing Core/Data/Unity assembly boundaries.

### Scope
- Starter normal, elite, and boss pools intentionally share conservative reward
  definitions while the authored integration is validated.

### Verification
- Focused `RewardScreenTests`: **14/14** passed.
- Full suites: **210/210 EditMode** and **31/31 PlayMode** tests passed.
- Project baseline validator passed with **0 failures, 0 inconclusive, and 0 skipped**.

## v0.7.9 — Pass-Turn Mana Recovery (2026-07-24)

### Added
- **Pass recovery**: legal no-target Pass actions restore a configurable default
  of **1 mana**, clamped to each unit's `MaxMana`, before advancing the turn.
- **Action feedback**: `CombatActionResult` exposes `ManaBefore`, `ManaAfter`, and
  `ManaDelta` so presentation can communicate the recovery without reproducing Core rules.
- **Safety and compatibility**: recovery clamps before addition to prevent integer overflow,
  and the original two-parameter `CombatEngine` constructor remains available.

### Changed
- **API compatibility**: existing `Pass()` and typed action entry points remain valid;
  the recovery value is configured centrally by `CombatEngine` in `Game.Core`.

### Verification
- Focused pass-recovery tests: **6/6** passed.
- Full suites: **203/203 EditMode** and **31/31 PlayMode** tests passed.
- Unity Console: **0 errors and 0 warnings**; `EditorSettings.m_EnterPlayModeOptions`
  restored to baseline `0` after PlayMode verification.

## v0.7.8 — Reward Determinism Audit (2026-07-24)

### Changed
- **Deterministic reward audit**: verified reward options consume the derived
  `RunRandomStream.RewardOptions` seed and use the platform-stable `DeterministicRandom`
  sampler.
- **Reproducibility coverage**: `RunReproducibilityTests` verifies same-seed/progress
  reward snapshots and stable, independent reward streams.

### Scope
- Documentation-only audit; no production C# code changed.

### Verification
- Full suites: **198/198 EditMode** and **31/31 PlayMode** tests passed.
- Unity Console: **0 errors and 0 warnings**.

## v0.7.7 — Effective Max HP Audit (2026-07-24)

### Changed
- **Rules/UI audit**: verified production C# has no direct `.MaxHp` reads outside
  the base-property definition/implementation and explanatory comments; runtime
  rules and UI consistently use `EffectiveMaxHp`.

### Scope
- Documentation-only audit; no production C# code changed.

### Verification
- Full suites: **198/198 EditMode** and **31/31 PlayMode** tests passed.
- Unity Console: **0 errors and 0 warnings**.

## v0.7.6 — Ability Reward Deduplication (2026-07-24)

### Added
- **Exact duplicate protection**: reward abilities are deduplicated by normalized,
  case-insensitive `DisplayName` plus the complete `IAbilityData` gameplay signature.
- **Distinct definitions preserved**: same-name abilities with different behavior are
  allowed; null or blank display names are rejected safely.

### Scope
- Incompatible-ability rules remain deferred because the domain has no compatibility
  metadata yet.

### Verification
- Focused RunState/ability tests: **38/38** passed.
- Full suites: **198/198 EditMode** and **31/31 PlayMode** tests passed.
- Unity Console: **0 errors and 0 warnings**; `EditorSettings` restored to baseline.

## v0.7.5 — Reward Previews (2026-07-24)

### Added
- **Inline recipient previews**: after selecting a reward card, each alive unit's
  button shows the pending description and exact effective-stat or HP/max HP
  current→after values before confirmation.
- **Ability learn preview**: ability rewards display an explicit learn label without
  mutating the recipient until selection is confirmed.
- **Pure formatting**: preview formatters are read-only and covered by EditMode tests.

### Verification
- Focused `RewardScreenTests`: **7/7** passed.
- `git diff --check`: clean.
- Unity compilation: clean; no C# errors.
- Full suites were not rerun for this slice; previous retained evidence remains
  **190/190 EditMode** and **31/31 PlayMode** tests.

## v0.7.4 — Player-Selected Reward Recipients (2026-07-24)

### Added
- **Two-step reward selection**: after choosing a reward card, the player selects
  an alive unit from deterministic runtime-generated recipient buttons.
- **Recipient details**: each button shows the unit name, current HP, and effective
  max HP; only the selected unit receives the pending reward.
- **Safe fallback**: the existing seeded legacy recipient selection remains active
  only when recipient UI creation fails or no unit is alive.

### Verification
- Focused reward tests: **4/4** passed.
- Full suites: **190/190 EditMode** and **31/31 PlayMode** tests passed.
- Project baseline `CollectErrors`: empty; Unity Console: **0 errors and 0 warnings**.

## v0.7.3 — Explicit Reward Effects (2026-07-24)

### Changed
- **Reward effects**: `RewardOption` now carries explicit `RewardEffectKind` metadata,
  including `MaxHpBoost`; descriptions remain UI-only and are not parsed for gameplay.

### Scope
- Player-selected reward recipients are delivered in v0.7.4 above.

### Verification
- Focused reward tests: **4/4** passed.
- Full suites at this slice: **188/188 EditMode** and **31/31 PlayMode** tests passed.
- Project baseline `CollectErrors`: empty; Unity Console: **0 errors and 0 warnings**.

## v0.7.2 — Route/Roster State Display (2026-07-24)

### Added
- **Roster summary**: Map rebuilds derive a deterministic runtime summary from the
  persisted player pieces, showing names, current HP/effective max HP, and defeated
  state alongside the route.

### Scope
- Route and roster state continue to come from the existing RunManager/RunState
  references across scene transitions; this slice does not change persistence.

### Verification
- Focused `MapView` tests: **8/8** passed.
- Full suites: **186/186 EditMode** and **31/31 PlayMode** tests passed.
- Unity Console: **0 errors and 0 warnings**.

## v0.7.1 — Rest Result Presentation (2026-07-24)

### Added
- **Rest-result status**: returning to the Map scene presents the configured heal percentage and exact clamped HP deltas for each alive piece once, then falls back to the normal route prompt.

### Scope
- No dedicated rest modal is introduced in this bounded slice; route and roster preservation remain separate Phase 2 deliverables.

### Verification
- Focused rest-result tests: **12/12** passed.
- Full suites: **184/184 EditMode** and **31/31 PlayMode** tests passed.
- Project baseline validator: **0 errors**; Unity Console: **0 errors and 0 warnings**.

## v0.7 — Strategic Map Readability (2026-07-24)

### Added
- **Route-state presentation**: current, available, visited, and blocked nodes are visually distinct, with matching connection states.
- **Map guidance**: node labels identify combat, elite, rest, shop, and boss destinations; hover text explains each node's route state.

### Changed
- **Shop scope**: Shop remains a future transaction node; procedural generation no longer creates Shop nodes for the vertical slice while the `MapNodeType.Shop` enum and MapView labels remain compatible.

### Verification
- Focused `MapView` tests: **4/4** passed.
- Focused `MapGraph` tests: **3/3** passed.
- Full suites: **181/181 EditMode** and **31/31 PlayMode** tests passed.

## v0.6 — Combat Clarity (2026-07-23)

### Added
- **Serialized combat HUD**: displays the active unit, turn order, HP, mana, available abilities with costs, and discoverable controls.
- **Typed action legality and feedback**: move, attack, ability, and pass actions share a typed legality contract with target highlighting and explicit invalid-action rejection.
- **Combat feedback presentation**: movement, attack, healing, mana, buff, debuff, and death feedback can be click-to-skipped; the skip click is consumed instead of passing through to the board or UI.
- **Persistent boss-phase toast**: shows the current phase, phase ability, and damage bonus.

### Changed
- **Action economy**: each unit may move **or act** once per turn; moving and then attacking in the same turn is not allowed.
- **Combat-end lifecycle**: `CombatEndDelaySeconds` is configurable, and the terminal feedback relay is lifecycle-safe and emits only once.
- **Reproducible runs**: deterministic run seeds remain available for repeatable map, encounter, and reward verification.

### Verification
- **177/177 EditMode** tests passed.
- **31/31 PlayMode** tests passed, including the focused boss-toast test (**1/1**).
- Project baseline validator succeeded; the Unity Console reported **0 errors, 0 warnings, and 0 logs**.
- Audio cues are deferred to **Phase 4**. First-time-player usability remains the **Phase 1 closure gate**.

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
