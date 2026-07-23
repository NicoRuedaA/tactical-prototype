# Project guide for agents

## Baseline

- Use Unity **6000.0.77f1**, the version recorded in `ProjectSettings/ProjectVersion.txt`.
- This is a Unity 6 turn-based tactical roguelite. Keep changes small, reproducible, and compatible with the existing vertical-slice loop.

## Where code belongs

- `Assets/Scripts/Core` (`Game.Core`) contains Unity-independent rules and state. Do not add `UnityEngine` dependencies here.
- `Assets/Scripts/Unity/Data` (`Game.Data`) adapts authored ScriptableObjects and other Unity data to Core models.
- `Assets/Scripts/Unity` (`Game.Unity`) owns scene orchestration, input, UI, board/piece views, map, rewards, and game-over presentation.
- `Assets/Editor` (`Game.Editor`) contains editor-only validation, scene setup, and Linux build/smoke automation.
- EditMode and PlayMode tests live under `Assets/Scripts/Tests/EditMode` and `Assets/Scripts/Tests/PlayMode`.
- Scenes and build order are defined under `Assets/Scenes` and `ProjectSettings/EditorBuildSettings.asset`; preserve their `.meta` files.

## Unity/MCP workflow

When using MCP for Unity, follow a resource-first workflow: inspect `mcpforunity://editor/state` and the relevant scene/project resources before mutating anything. After a script edit, wait for `is_compiling` to become false and inspect the Unity Console for errors before proceeding. Prefer batched operations for independent editor changes.

## Validation

- In the Editor, run **Tools → TacticalRogue → Validate Project Baseline**, then run EditMode and PlayMode suites from Test Runner.
- For batch tests, use the Unity 6000.0.77f1 executable with `-runTests`, `-testPlatform editmode|playmode`, and logs/results under `Builds/Logs/` (see `README.md`).
- The supported standalone workflow is Linux: **Tools → TacticalRogue → Build → Linux Development**, followed by **Tools → TacticalRogue → Smoke → Linux Runtime**. Do not invent alternate build targets or scene orders.

## Change hygiene

- Preserve existing user changes and generated evidence; do not reset, clean, or overwrite unrelated files.
- Never commit credentials, secrets, machine-specific absolute paths, or local editor state. `Builds/` is local/CI evidence and is intentionally ignored.
- Keep documentation and code references aligned with `README.md`, `ROADMAP.md`, asmdefs, scenes, and current validator behavior.
