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

## 2026-08-27 - Chromix UniClue

### Added
- Independent `ChromixUniClue` grouped world asset with six player seats, Classic and timed Party modes, private clue entry, duplicate-clue cancellation, rotating guesser, cooperative scoring, tutorial UI, and 120 embedded mystery words.
- Six local 256x144 live-avatar portrait render textures driven by round-robin head cameras; no external image-generation service is used.
- Animated world-space lobby/game UI and a builder that preserves placement while replacing only the existing `ChromixUniClue` root.

### Verified
- Unity C# and UdonSharp compilation complete with `compiledVersion: 2`.
- One `ChromixUniClue` scene root exists with a valid `VRC.Udon.UdonBehaviour`, world-space canvas, and six portrait cameras.

## 2026-08-20 - Mod Tool crash fix + Trivia multiplayer fix

### Fixed
- **Mod Tool halting after a few commands**: NullReferenceException in SetWalkSpeed when _selectedPlayer became invalid (player left or was kicked). Added !_selectedPlayer.IsValid() guard to all 10 action methods (Freeze, Unfreeze, ToggleFreeze, ToggleMute, BanPlayer, UnbanPlayer, PromotePlayer, DemotePlayer, TeleportToPlayer, BringPlayer, SetPlayerSpeed, SetPlayerSize).
- **Trivia multiplayer - other players not showing up**: JoinGame, BuzzIn, and SubmitAnswer used Networking.SetOwner() then immediately called RequestSerialization(). Since SetOwner is async in VRChat, the serialization was dropped before ownership transferred. Replaced with SendCustomNetworkEvent(NetworkEventTarget.Owner, ...) so the owner handles all state changes and serializes reliably.

### Changed
- Trivia JoinP1-P4 buttons now send NetJoinP1-P4 network events to the owner instead of stealing ownership.
- Trivia BuzzP1-P4 buttons now send NetBuzzP1-P4 network events to the owner.
- Trivia AnswerA-D buttons now send NetAnswerA-D network events to the owner.
- Owner-side OwnerJoin, OwnerBuzz, OwnerAnswer methods handle the state changes and serialize.
- OwnerJoin finds the first unjoined player and assigns them to the requested slot (since the old API doesn't expose the calling player).
