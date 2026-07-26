# Tactical Prototype Development Roadmap

The project already proves the complete run loop. The next objective is not to add more mechanics; it is to turn the prototype into a clear, stable, and distributable vertical slice.

## Current baseline

- Playable flow: `SampleScene -> Map -> Combat -> Reward -> Map -> Boss -> GameOver`.
- Hex combat with movement, attacks, abilities, mana, buffs, passives, and defeat conditions.
- Normal, elite, and boss encounters.
- Procedural map and persistent run progression.
- Unity-independent `Game.Core` domain layer.
- 181 passing EditMode tests, 31 passing PlayMode tests, and green focused click/input/terminal suites in the final verified Phase 1 run.

The main gaps are player feedback, Unity integration quality, PlayMode coverage, and content presentation.

## Delivery principles

1. Protect the working loop before expanding it.
2. Make every game state understandable without developer instructions.
3. Prefer a small polished slice over more unfinished content.
4. Keep game rules in `Game.Core`; keep presentation and input in the Unity layer.
5. Every phase ends with a playable, testable increment.

## Phase 0 — Reproducible baseline ✅ Complete

**Outcome:** the current loop is stable enough to build on safely.

### Completed deliverables

- [x] Consolidate scripts, scenes, assembly definitions, assets, and `.meta` files.
- [x] Validate required scenes, enemy pools, prefabs, and serialized references.
- [x] Record the run seed so maps, encounters, and rewards can be reproduced.
- [x] Add PlayMode smoke coverage for victory/reward/map return, defeat/restart, and a complete production flow through the boss to victory.
- [x] Package a Linux development build from `SampleScene`.

### Verified exit criteria

- [x] The project opens and compiles without errors.
- [x] All EditMode and PlayMode tests pass.
- [x] A complete loop produces no Console errors.
- [x] The standalone player initializes managed code, starts `RunManager`, and loads `Map`.

### Reproduce the Linux build

In Unity, run **Tools → TacticalRogue → Build → Linux Development**. The validated,
clean build is written to `Builds/Linux/TacticalPrototype.x86_64`.

For batch or CI execution, keep the Unity log outside disposable staging:

```bash
mkdir -p Builds/Logs
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PWD" \
  -executeMethod StandaloneBuildAutomation.BuildLinuxDevelopmentBatch \
  -logFile "$PWD/Builds/Logs/linux-build.log"
```

The command validates the project and exact scene order through the pre-build guard, builds
into isolated staging, verifies packaging artifacts, and only then promotes to `Builds/Linux`.
The previous build survives failed builds and failed promotion. Packaging status is recorded
separately from runtime smoke status in `Builds/Logs/linux-packaging-status.txt`.
Existing `Builds/Linux/player-smoke.log` evidence is copied to a unique timestamped file under
`Builds/Logs/` before output replacement.

### Reproduce the Linux runtime smoke

In Unity, run **Tools → TacticalRogue → Smoke → Linux Runtime**, or use the existing build in CI:

```bash
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PWD" \
  -executeMethod StandaloneBuildAutomation.RunLinuxRuntimeSmokeBatch \
  -logFile "$PWD/Builds/Logs/linux-smoke-editor.log"
```

The smoke runner removes `SDL_IM_MODULE`, `QT_IM_MODULE`, and `XMODIFIERS` only from the child
player environment. This isolates the diagnosed `SDL_Fcitx_Init` host crash; it does not change
the game or its player settings. Success requires managed initialization, a started run, and Map
loading in the Player log, with either a clean exit or survival until the controlled 20-second timeout.

**Completion evidence (2026-07-18):** all 142 EditMode tests and all three PlayMode tests passed;
their retained summaries are `Builds/Logs/editmode-results.xml` and
`Builds/Logs/playmode-results.xml`. The full-run PlayMode smoke uses production node selection,
real combat startup and engine outcomes, reward UI submission, and reaches the boss victory
GameOver state while preserving the manager and fixed seed.

The hardened Linux development packaging succeeded with zero errors, a valid BuildReport, and
the expected promoted files; its status is `Builds/Logs/linux-packaging-status.txt`. The isolated
runtime smoke passed, remained alive until its controlled timeout, terminated cleanly with exit
code 0, and left no player process. Log
`Builds/Logs/linux-runtime-smoke-20260717-222441744.log` contains `Mono path[0]`,
`Run started (seed=365485381)`, and `Map scene loaded`; status is recorded separately in
`Builds/Logs/linux-runtime-smoke-status.txt`.

## Phase 1 — Combat clarity 🚧 In progress

**Outcome:** a new player can finish combat without external instructions.

### Deliverables

- [x] Replace runtime-generated fallback UI with scene or prefab-based combat UI.
- [x] Show active unit, turn order, HP, mana, available actions, and ability costs.
- [x] Add explicit feedback for movement, attack range, targets, invalid actions, and pass turn.
- [x] Add damage, healing, death, passive, and boss phase feedback, including the persistent boss phase toast.
- [x] Add basic movement, hit, and transition feedback plus click-to-skip input gating. Combat-end delay timing is configured through `CombatEndDelaySeconds`, and the relay is lifecycle-safe.
- [x] Document and communicate the turn rule consistently.

### Product decision ✅ Resolved

A unit may **move or act** per turn. Combat feedback blocks subsequent actions while it plays; a mouse click fast-forwards the feedback and is consumed instead of passing through to the board or UI.

### Exit criteria

- [x] The player always knows whose turn it is and what actions are legal.
- [x] Invalid actions explain why they failed.
- [x] Mouse and keyboard controls are discoverable on screen.
- [ ] A first-time player can complete a normal encounter unaided. Automated regression is green; manual first-time playtesting is the only remaining Phase 1 closure gate.

### Current verification status (2026-07-23)

- The final verified run passed all 177 EditMode tests, all 31 PlayMode tests, and the focused boss-toast test (1/1); click-to-skip, input, and terminal suites are green. The project baseline validator succeeded, and the Unity Console reported zero errors, warnings, or logs.
- A subsequent reliability review found and corrected three click-to-skip edge cases: enemy AI now waits for presentation feedback, terminal feedback can be skipped through normal input, and UI pointer clicks are distinguished from keyboard Submit deterministically.
- The only intermediate failure was a fixture assumption that Player turns were contiguous; `TurnSystem` interleaves Enemy turns by initiative. The fixture was corrected in `Assets/Scripts/Tests/PlayMode/CombatFeedbackPresentationTests.cs` only.
- WU5 completed passive/buff/debuff presentation, the persistent boss phase toast, configured `CombatEndDelaySeconds`, and a lifecycle-safe combat feedback relay.
- Unity MCP is ready for tools and idle, with no compilation, blocking, or stale state.
- Audio cues are intentionally deferred to Phase 4 content polish and are not a Phase 1 blocker.
- Phase 1 remains in progress. The only pending closure work is manual first-time-player usability validation; automated regression is green.

## Phase 2 — Strategic map ✅ Complete

**Outcome:** route selection becomes a readable tactical decision.

### Deliverables

- [x] Draw map connections and distinguish current, available, visited, and blocked nodes.
- [x] Add clear identities and explanations for combat, elite, rest, and boss nodes. Keep Shop labels compatible for future content.
- [x] Show the exact result of resting before returning to the map.
- [x] Remove non-actionable Shop nodes from generation for the vertical slice; retain the node type and labels for future scope.
- [x] Preserve and display route and roster state across scene transitions.

### Map readability slice ✅ Verified (2026-07-24)

The first strategic-map slice is complete: route state is explicit, connections communicate
available/visited/blocked paths, and node labels plus hover text explain each destination.
Focused `MapView` tests pass **4/4**; the full suites pass **181/181 EditMode** and
**31/31 PlayMode** tests.

### Rest-result slice ✅ Verified (2026-07-24)

Rest nodes now capture the configured heal percentage and exact clamped HP change
for every alive piece. MapView presents that result once in the runtime status on
return to the Map scene, then restores the normal route prompt. A dedicated modal
remains future UX; route and roster preservation remain separate deliverables.

Focused rest-result tests pass **12/12**; the full suites pass **184/184 EditMode**
and **31/31 PlayMode** tests. The project baseline validator reports **0 errors**,
and the Unity Console reports **0 errors and 0 warnings**.

### Route/roster state slice ✅ Verified (2026-07-24)

MapView now derives a deterministic roster summary from `RunManager.CurrentRun.Pieces`
on every rebuild, showing each piece's name, current HP/effective max HP, and
defeated state. Route state remains graph-derived and is preserved by the existing
RunManager/RunState references across scene transitions; no persistence changes were
needed.

Focused MapView tests pass **8/8**; the full suites pass **186/186 EditMode** and
**31/31 PlayMode** tests. The Unity Console reports **0 errors and 0 warnings**.

### Exit criteria

- [x] Every visible node performs its complete action and preserves the run flow. Final player-facing explanation and visual polish are deferred to the UI/content polish phase.
- [x] The player can compare route risk and reward through node identities, labels, hover text, and route state.
- [x] Combat return preserves the correct run and map state.

**Closure note:** Phase 2 is closed on technical and flow criteria. Improving first-time comprehension and visual communication of node actions remains future UI/content polish work.

## Phase 3 — Meaningful progression ✅ Complete

**Outcome:** rewards create distinct, intentional character builds.

### Deliverables

- [x] Define rewards as data assets rather than text-driven behavior.
- [x] Let the player choose which unit receives a reward.
- [x] Show current values and the exact post-reward result before confirmation.
- [x] Remove text parsing for Max HP and use explicit reward effects.
- [x] Use `EffectiveMaxHp` consistently in rules and UI.
- [x] Prevent exact duplicate abilities and filter incompatible ability reward groups
  with normalized, case-insensitive display-name and complete gameplay-signature matching.
- [x] Define mana recovery and reward pools for normal, elite, and boss progression.
- [x] Make rewards deterministic when using the same run seed.

### Player-selected reward recipients slice ✅ Verified (2026-07-24)

Reward selection is now a deterministic two-step interaction: the player first
chooses a reward card, then chooses from runtime-generated buttons for each alive
unit. Each recipient button shows the unit name, current HP, and effective max HP;
the pending reward is applied only to the selected unit before the run advances.
Controls are created under the existing Canvas and cleaned up on rebuild/disable.
If the runtime UI cannot be created or no recipient is alive, the existing seeded
legacy recipient fallback is used.

Focused reward tests pass **4/4**; the full suites pass **190/190 EditMode** and
**31/31 PlayMode** tests. The project baseline `CollectErrors` check is empty, and
the Unity Console reports **0 errors and 0 warnings**.

### Explicit reward effects slice ✅ Verified (2026-07-24)

`RewardOption` now carries an explicit `RewardEffectKind`, including `MaxHpBoost`;
reward descriptions are UI-only and are no longer parsed to apply gameplay effects.
The reward recipient flow is covered by the player-selected reward recipients
slice above.

### Reward preview slice ✅ Verified (2026-07-24)

Inline recipient buttons now show the pending reward description and exact
current→post-reward values: effective stat changes for stat rewards, HP/effective
max HP changes for vitality rewards, and an explicit ability learn label. Pure,
non-mutating formatters keep the preview read-only until the recipient confirms.

Focused `RewardScreenTests` pass **7/7**; `git diff --check` is clean, and Unity
compilation is clean with no C# errors. The previous slice's retained full-suite
evidence remains **190/190 EditMode** and **31/31 PlayMode**; those suites were not
rerun for this bounded preview slice.

### Ability reward deduplication slice ✅ Verified (2026-07-24)

Exact duplicate abilities are rejected using a normalized, case-insensitive
`DisplayName` plus the complete `IAbilityData` gameplay signature. Abilities that
share a name but differ in behavior remain distinct, while null or blank display
names are rejected safely. Incompatible-ability rules are not implemented because
the domain currently has no compatibility metadata; that decision remains follow-up
work.

Focused RunState/ability tests pass **38/38**; the full suites pass **198/198
EditMode** and **31/31 PlayMode** tests. The Unity Console reports **0 errors and
0 warnings**, and `EditorSettings` was restored to its baseline Enter Play Mode
options after verification.

### Effective max HP audit ✅ Verified (2026-07-24)

An audit of production C# found no direct `.MaxHp` reads outside the base-property
definition/implementation and explanatory comments. Runtime rules and UI use
`EffectiveMaxHp` for healing, combat thresholds, previews, HUD values, map roster
summaries, and combat feedback. This is a documentation-only audit; no production
code changed.

The retained full-suite evidence remains **198/198 EditMode** and **31/31
PlayMode** tests, with **0 Console errors and 0 warnings**.

### Reward determinism audit ✅ Verified (2026-07-24)

`RewardScreen.GenerateRewardOptions` consumes the deterministic
`RunRandomStream.RewardOptions` seed derived from the run seed and combat progress;
the reward pool is sampled through `DeterministicRandom.PickDistinctIndices`. The
`RunReproducibilityTests` suite verifies identical reward snapshots for the same
seed/progress and stable, independent reward streams. This is a documentation-only
audit; no production code changed.

The retained full-suite evidence remains **198/198 EditMode** and **31/31
PlayMode** tests, with **0 Console errors and 0 warnings**.

### Pass-turn mana recovery slice ✅ Verified (2026-07-24)

Pass remains a legal no-target action. When a unit passes, the Core engine restores
the configurable pass recovery amount (default **1 mana**), clamps the result to
`MaxMana`, then advances the turn. `CombatActionResult` exposes `ManaBefore`,
`ManaAfter`, and `ManaDelta` for presentation feedback, while the existing
`Pass()` and typed action APIs remain compatible. Recovery is overflow-safe for extreme
configuration values, and the original two-parameter `CombatEngine` constructor remains
available for binary compatibility.

Focused pass-recovery tests pass **6/6**; the full suites pass **203/203 EditMode**
and **31/31 PlayMode** tests. The Unity Console reports **0 errors and 0 warnings**,
and `EditorSettings.m_EnterPlayModeOptions` was restored to its baseline value of
`0` after verification.

### Authored reward pools slice ✅ Verified (2026-07-24)

The Reward scene now uses separate authored normal, elite, and boss
`RewardPoolData` assets selected from the retained encounter type. Seeded sampling
remains deterministic and does not repeat indices; null or empty authored pools use
the deterministic legacy inline fallback. Unity-independent reward domain models now
live in `Game.Core`, preserving the existing assembly boundaries.

The authored offers now communicate tier identity using only supported effects:
normal rewards remain modest, elite rewards provide stronger offense/survivability
plus movement, and boss rewards provide the strongest stat boosts plus attack range.
Focused `RewardScreenTests` pass **15/15**; the full suites pass **211/211 EditMode**
and **31/31 PlayMode** tests. The project baseline validator passed.

### Recipient-compatible rewards slice ✅ Verified (2026-07-24)

The Reward scene now starts with alive recipients in deterministic roster order.
Selecting one generates that recipient's seeded authored offers; incompatible ability
definitions are excluded using the same canonical gameplay signature as duplicate
prevention. Compatible definitions fill the authored slots without replacement and
may produce fewer cards when exhausted; zero compatible definitions advance without
granting a replacement reward. Null or empty authored pools retain the deterministic
inline fallback. Reward cards preview the selected unit's exact result without
mutation, and selecting one applies it directly before the existing Map or GameOver
transition.

Synthetic definitions and recipients prove exclusion, same-seed determinism, no
replacement, exhaustion, recipient-specific previews, and scene progression.

Focused suites pass **19/19 RewardScreen EditMode** and **5/5 RunLoop PlayMode** tests;
the full suites pass **215/215 EditMode** and **31/31 PlayMode** tests. The project
baseline validator passed, and `git diff --check` is clean.

### Production ability exclusions slice ✅ Verified (2026-07-24)

The normal reward pool now offers Power Strike, Fireball, Mend, and Regeneration as
observable build choices. Power Strike and Fireball exclude each other reciprocally,
as do Mend and Regeneration; filtering uses the existing canonical full ability
identity. Elite and boss pools remain deterministic stat-specialization tiers.

Thorns and War Aura are intentionally untouched because Thorns targeting remains
unverified. Real production-asset tests prove each approved reciprocal direction,
stable compatible and unaffected offers, and the exact absence of unapproved
metadata. Focused `RewardScreenTests` pass **25/25**; the full suites pass **221/221
EditMode** and **31/31 PlayMode** tests. The project baseline validator passed, and
`git diff --check` is clean.

### Exit criteria

- [x] Two runs can produce meaningfully different builds.
- [x] Every reward states what changes and who receives it.
- [x] Reward outcomes have deterministic Core tests.

### Exit-evidence closure ✅ Verified (2026-07-24)

`RunState.ApplyReward` now owns the Unity-independent option-to-outcome contract
for damage, attack range, move range, max HP plus current HP, and new abilities.
Behavior-first Core tests prove every outcome, selected-recipient isolation,
ability application, identical-input final snapshots, and meaningfully different
two-run build histories from deterministic seeds and choices.

The recipient-first Reward scene delegates application to that Core contract and
keeps the selected unit's name visible in the reward title while each card retains
its exact before→after preview. Focused suites pass **71/71 EditMode** and **5/5
RunLoop PlayMode** tests; full suites pass **230/230 EditMode** and **31/31
PlayMode** tests. The project baseline validator passed, and `git diff --check` is
clean. Thorns content correctness remains separate Phase 4 work and was not changed.

## Phase 4 — Vertical-slice content

**Outcome:** one short run represents the intended final game experience.

### Deliverables

- A small roster with clear tactical roles.
- An introductory encounter that teaches the core interaction.
- A positioning-focused encounter.
- An elite encounter with a distinct mechanical identity.
- A boss whose phase transition is mechanically and visually clear.
- A compact, balanced ability set with visible synergies.
- Consistent final-direction UI, art, and VFX across the full run.
- **Audio cues** (deferred from Phase 1; revisit during Phase 4 content polish).
- Contextual first-run onboarding.

### Exit criteria

- The complete run demonstrates combat, routing, progression, elite, and boss play.
- Each encounter asks for a different tactical decision.
- No developer explanation or visible placeholder is required.

## Phase 5 — Release quality

**Outcome:** the vertical slice can be handed to an external tester confidently.

### Deliverables

- PlayMode coverage for movement, attack, abilities, rewards, and scene transitions.
- End-to-end victory and defeat tests.
- Resolution, aspect ratio, and UI scaling validation.
- Main menu, pause, restart, and quit flows.
- Minimal audio and control settings.
- Reproducible builds and a smoke-test checklist.
- Lightweight telemetry for encounter duration, damage taken, and reward choices.

### Exit criteria

- The build runs outside the Unity Editor without exceptions or blockers.
- Core, integration, and PlayMode suites pass.
- Another person can install, play, and evaluate the slice without documentation.

## Immediate work order

1. ~~Decide the turn action economy.~~ Completed: move or act.
2. Finish and verify the final-direction combat HUD and feedback.
3. Complete the map and reward decisions.
4. Produce and balance the representative content slice.
5. Validate external playtesting.

## Explicitly deferred

- Additional characters before existing roles are readable and balanced.
- Large content expansion before the map and reward systems are complete.
- A full shop unless economy is confirmed as a core pillar.
- Online services, account systems, and long-term metaprogression before the vertical slice validates the core loop.
