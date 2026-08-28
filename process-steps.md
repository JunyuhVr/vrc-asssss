# Crack the Code - Process Steps

## Project: `vrc asssss`
- **Unity**: 2022.3.22f1
- **VRChat SDK**: com.vrchat.base 3.10.4, com.vrchat.worlds 3.10.4
- **Scene**: Assets/Scenes/VRCDefaultWorldScene.unity
- **MCP bridge**: C:/Users/junyu/Downloads/vrc asssss/McpBridge

## Step 1: Project identification
- Confirmed Unity window title: `vrc asssss - VRCDefaultWorldScene`
- MCP `get_project_info` returned productName `vrc asssss`, activeScene `Assets/Scenes/VRCDefaultWorldScene.unity`
- MCP `ping_bridge` confirmed Unity 2022.3.22f1

## Step 2: Scope verification
- Existing scene objects: VRCWorld, Main Camera, Directional Light, Floor, ChromixTriviaCanvas
- Existing Trivia files: Assets/ChromixTrivia/ChromixTriviaUdon.cs, ChromixTriviaRelay.cs
- User instruction: leave Trivia untouched, build only under Assets/CrackTheCode

## Step 3: Controller port
- Copied CodeBreakerGame.cs from original Chromix project
- Verified SDK 3.10.4 API compatibility:
  - `VRC.SDK3.UdonNetworkCalling.NetworkCalling` exists in VRCSDK3.dll
  - `VRC.SDK3.Persistence.PlayerData` for personal best
  - `[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]` for manual sync
  - `[NetworkCallable]` for parameterized network calls
- Game modes: Solo (10-digit, 7 attempts, personal best), PvP (4-digit, alternating turns, green/red only)

## Step 4: Builder port and font fallback
- Original builder used TextMeshPro (TMP_FontAsset, TextMeshProUGUI)
- New project lacks importable TMP font source
- Converted to UnityEngine.UI.Text with `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`
- Matches existing Trivia project convention (built-in Text)

## Step 5: VRCUiShape serialization fix
- Reflection-based `AddComponent(type)` did not serialize VRCUiShape into the prefab
- Fixed by adding `using VRC.SDK3.Components;` and calling `target.AddComponent<VRCUiShape>()` directly
- Verified via MCP `find_object`: UI_Canvas has VRC.SDK3.Components.VRCUiShape component

## Step 6: Scene placement fix
- `PlaceInOpenScene` called during `[InitializeOnLoad]` domain reload failed because:
  1. The temporary root named "CrackTheCode" was still alive, causing `GameObject.Find` to return early
  2. The scene may not be fully loaded during domain reload
- Fix: destroy temporary root before scheduling placement, defer placement to `EditorApplication.update` callback
- Verified: "[Crack The Code] Placed instance into active scene." logged successfully

## Step 7: Verification
- MCP `list_hierarchy` confirmed CrackTheCode root in scene with full grouped hierarchy
- MCP `find_object` confirmed UI_Canvas has: RectTransform, Canvas, CanvasScaler, GraphicRaycaster, BoxCollider, VRCUiShape
- MCP `find_object` confirmed GameSystems has: CodeBreakerGame, VRC.Udon.UdonBehaviour
- Prefab YAML confirmed: 14 buttons wired with SendCustomEvent (m_Mode: 4), events include JoinGame, ResetGame, Digit0-9, EraseDigit, ConfirmEntry
- Trivia canvas and all other existing scene objects unchanged

## Step 8: UdonSharp compilation status
- CodeBreakerGame.asset has compiledVersion: 0 (not compiled)
- Root cause: pre-existing Trivia error `canvas.renderMode` not exposed to Udon blocks ALL UdonSharp compilation
- User explicitly declined fixing the Trivia script
- CodeBreakerGame.cs itself uses valid SDK 3.10.4 APIs and has no known Udon exposure issues
- Once Trivia error is resolved, UdonSharp will compile CodeBreakerGame automatically

## Remaining
- Optional: VRChat Build & Test with two clients for PvP validation
- The controller will become functional once the Trivia compile blocker is resolved

## Chromix UniClue
- Built as a separate `ChromixUniClue > ChromixUniClue_Asset` hierarchy without modifying other game roots.
- Uses owner-authoritative parameterized network calls for joining, leaving, starting modes, submitting clues, guessing, skipping, and resetting.
- Uses six local portrait cameras and persistent render textures instead of unavailable VRChat profile-thumbnail access.
- Portrait cameras update round-robin and render only Player/PlayerLocal layers to reduce rendering cost.
- Builder removes only duplicate `ChromixUniClue` roots, preserves the first instance position/rotation, and creates one replacement.
- Verified `ChromixUniClueGame.asset` has `compiledVersion: 2` and the scene `GameSystems` object has both the UdonSharp component and backing `VRC.Udon.UdonBehaviour`.

## 2026-08-20 - Mod Tool crash + Trivia multiplayer

### Mod Tool halting
1. **Symptom**: Mod Tool stops working after a few commands.
2. **Diagnosis**: Editor.log showed UdonBehaviour will be halted with NullReferenceException in SetWalkSpeed. The _selectedPlayer reference became invalid when a player left or was kicked, but the code only checked for 
ull, not IsValid().
3. **Fix**: Added !_selectedPlayer.IsValid() to all 10 action method guards using eplace_all.

### Trivia multiplayer
1. **Symptom**: Other players join Trivia but don't show up on the panel.
2. **Diagnosis**: JoinGame called Networking.SetOwner(local, gameObject) then immediately modified synced fields and called RequestSerialization(). In VRChat, ownership transfer is async — the serialization happens before ownership transfers, so it's dropped.
3. **Fix**: Replaced direct ownership-steal with SendCustomNetworkEvent(NetworkEventTarget.Owner, ...) for Join, Buzz, and Answer buttons. The owner handles all state changes and serializes reliably. OwnerJoin iterates all players to find the first unjoined one (the old API doesn't expose the calling player identity).
