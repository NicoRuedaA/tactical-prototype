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

## Phase 2 — Strategic map

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

- Every visible node performs a complete, understandable action.
- The player can compare route risk and reward.
- Combat return preserves the correct run and map state.

## Phase 3 — Meaningful progression

**Outcome:** rewards create distinct, intentional character builds.

### Deliverables

- Define rewards as data assets rather than text-driven behavior.
- [x] Let the player choose which unit receives a reward.
- [x] Show current values and the exact post-reward result before confirmation.
- [x] Remove text parsing for Max HP and use explicit reward effects.
- [x] Use `EffectiveMaxHp` consistently in rules and UI.
- [x] Prevent exact duplicate abilities with normalized, case-insensitive display-name
  and complete gameplay-signature matching; incompatible-ability rules remain deferred
  pending compatibility metadata.
- Define mana recovery and reward pools for normal, elite, and boss progression.
- Make rewards deterministic when using the same run seed.

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

### Exit criteria

- Two runs can produce meaningfully different builds.
- Every reward states what changes and who receives it.
- Reward outcomes have deterministic Core tests.

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
