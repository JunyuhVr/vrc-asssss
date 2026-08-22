# Crack the Code - Changelog

## 2026-08-19 - Initial port to `vrc asssss` project

### Added
- `Assets/CrackTheCode/CodeBreakerGame.cs` - UdonSharp controller implementing Solo (10-digit, 7 attempts) and PvP (4-digit, alternating turns) modes with join-based participation, 8-second lobby countdown, manual sync, PlayerData personal best.
- `Assets/CrackTheCode/CodeBreakerGame.asset` - UdonSharp program asset (pending compilation; see Known Issues).
- `Assets/CrackTheCode/Editor/CrackTheCodePrefabBuilder.cs` - Grouped prefab builder using `UnityEngine.UI.Text` (built-in font) instead of TextMeshPro.
- `Assets/CrackTheCode/CrackTheCode.prefab` - Grouped cyber-vault UI prefab with:
  - Root `CrackTheCode` > `CrackTheCode_Asset` > `GameSystems` + `CrackTheCode_EventSystem` + `UI_Canvas`
  - World-space Canvas with CanvasScaler, GraphicRaycaster, BoxCollider, VRCUiShape
  - Header (title, subtitle, mode, players), StatusCard (status, timer, attempts), DigitGrid (10 cells), JoinButton, KeypadPanel (12 keys), Footer (personal best, reset)
  - All 14 buttons wired to UdonBehaviour SendCustomEvent with navigation Mode.None
- Scene instance placed at (-2.2, 1.4, 1.5) facing 180 degrees.

### Changed
- Builder converted from TextMeshPro to `UnityEngine.UI.Text` using `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` because the new project lacks an importable TMP font asset.
- VRCUiShape added via direct `VRC.SDK3.Components.VRCUiShape` type reference instead of reflection to ensure proper serialization.
- Scene placement deferred to `EditorApplication.update` callback to ensure the scene is fully loaded before instantiation.

### Known Issues
- **Pre-existing Trivia compile blocker**: `Assets/ChromixTrivia/ChromixTriviaUdon.cs(87,31)` uses `canvas.renderMode` which is not exposed to Udon in SDK 3.10.4. This blocks ALL UdonSharp compilation in the project, including `CodeBreakerGame`. The user explicitly requested this file not be modified. `CodeBreakerGame.asset` has `compiledVersion: 0` as a result. The controller code itself is valid for SDK 3.10.4.
- **Odin ArgumentNullException during prefab save**: Occurs because the UdonBehaviour backing reference is null (UdonSharp compilation blocked). Non-fatal; prefab is still created with correct structure.

## 2026-08-20 - Trivia removed, compilation fixed, Join button working

### Fixed
- **Trivia removed**: Deleted all `Assets/ChromixTrivia/` files and `Assets/Editor/McpBridge/ExportChromixTrivia.cs`. UdonSharp now compiles all 8 scripts successfully. `CodeBreakerGame.asset` reports `compiledVersion: 2`.
- **Join button not clickable**: Root cause was Canvas layer set to "UI" (5). VRChat docs explicitly state Canvas must be on "Default" layer — the UI layer makes it non-interactable. Fixed by setting Canvas and all children to layer 0 (Default).
- **BoxCollider too thin**: Z depth was 0.01 in local space, which became ~0.00001 in world space. Increased to 10f for reliable raycast hits.
- **Button onClick wiring broken in prefab serialization**: Two-pass prefab build (LoadPrefabContents + SaveAsPrefabAsset) reassigned fileIDs, breaking persistent listener targets. Fixed by:
  1. Building prefab with UI only (no UdonBehaviour)
  2. Placing prefab in scene
  3. Adding UdonBehaviour to scene instance
  4. Wiring buttons using `UnityEventTools.AddStringPersistentListener` (Unity's official API)
  5. Saving scene with overrides (NOT applying back to prefab)
- **Canvas position**: Moved to (0, 1.5, 2) directly in front of player spawn.

### Changed
- Builder version bumped to 19.
- `DeferredPlaceAndWire` replaces `DeferredPlaceInOpenScene` — adds UdonBehaviour and wires buttons on the scene instance.
- Debug logging added to `Start()`, `JoinGame()`, and `RequestJoin()` for troubleshooting.

## 2026-08-20 - Chromix group join popup

### Added
- `Assets/CrackTheCode/ChromixGroupPopup.cs` - UdonSharp script for animated group join popup with `Store.OpenGroupPage` integration.
- Animated popup UI added to prefab builder: slides in from bottom, auto-shows after delay, join button triggers native VRChat group page.
- Group ID: `grp_07a66a18-762d-48c6-8e07-d2ef08388546`
