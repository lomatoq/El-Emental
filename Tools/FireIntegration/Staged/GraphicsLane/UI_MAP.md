# Existing UI integration map — read-only survey

All inspected paths below are relative to El-Emental. No Unity launched and no source, scene, theme or layout edited. Graphics archive contents were not yet present in this lane, so names of incoming PNGs/atlas rects must come from its actual manifest; the role mappings below are project-verified, not invented asset filenames.

## Authoritative saved graph

`Assets/Elemental/Content/Scenes/EarthCoreSlice.unity` contains the real frontend and online backend:
- FrontendFlowController (scene component ID 1968201430) at YAML ~228339: theme GUID `7822648f4accdf140af8daa7073d7a33`; view ID1349256444; audio ID1968201428; menuCamera ID1968201427; HUD ID1957364955; duel ID1613659716; readiness ID246707014; cameraDirector ID1967943506.
- EarthOnlineFrontend at ~119933 explicitly binds that flow ID1968201430, MpsRelaySession ID1063016227, NgoGameplayTransport ID1063016226 and OnlineGameplayBinding ID1063016232.
- Theme is `Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset`; its hudLayout reference is GUID `373ceaee55b6d9d419a33ce7bf2f6f1e`, the existing adjacent `ElementalHudLayout.asset`.
- Layout has real manual overrides, e.g. health.icon position(-22,2.8), scale(1.5,1.5); health.value position(-50.4,32), scale(2.5,2.5). Defaults in the C# constructor are NOT authoritative.

Skin integration should need no scene rebinding: update new visual-role sprite fields on that existing theme asset and use them through current presentation code.

## Two actual UI stacks

1. Menu: runtime uGUI Canvas + TMP, built in `Assets/Elemental/Presentation/UI/FrontendMenuView.cs`. No template/prefab tree supplies these controls. Build() creates Main, Settings, Host, Join, Pause, loading/countdown. Button helper at ~189 creates Image, Button, FrontendButton, installs the existing UnityAction, then creates label and configures interaction.
2. Combat HUD: runtime UI Toolkit tree built in `Assets/Elemental/Presentation/UI/EarthDuelHud.cs` (Build around304), on an existing UIDocument. Existing EarthCoreHud UXML retains reticle/diagnostic roots. `EarthDuelHud.uss` styles generated class names. `EarthDuelHudGeometry.cs` supplies procedural EarthDuelGauge and EarthHologramGlobe.

An archive UI demo backend, UXML or Canvas cannot replace these objects. Native menu Button events and Toolkit HUD events must remain attached to their current owners.

## Minimal visual role mapping

| Incoming role | Current target | Smallest presentation seam |
|---|---|---|
| Stone menu panel | FrontendMenuView.Build: backing `Left veil` / `_panel` | Assign sliced Image sprite on existing backing, preserve RectTransform anchors/placement and content parents. Do not stretch a framed panel across an intentionally full-screen veil unless it is designed for that size. |
| Primary/secondary/quiet button | FrontendMenuView.Button: tier0/1/2 Image before FrontendButton.Configure | Optional theme sprite for each tier, `Image.Type.Sliced`, preserved 470x70 authored box. |
| Hover/pressed/disabled button states | FrontendButton.Update | Optional sprite selection on `_graphic` (the child Image), keep `_hot`, `_pressed`, IsInteractable and unscaled timing. Existing `_graphic.color` tint interpolation still runs; colored source art may require white base tint for the skin path to avoid double coloration. |
| Settings/input frame | FrontendMenuView.BuildJoin `Room code input`, Slider Track/Fill/Handle, BuildSettings Box/Mark | Skin existing Images; retain TMP_InputField/Slider/Toggle and serialized user preferences. |
| HUD panel frame | EarthDuelHud: `health-panel`, `mana-panel`, `planet-panel`, `duel-scoreboard`, `round-result`, `life-result` | Background Sprite on existing VisualElement or one full-size non-picking decoration child. No position/size/scale assignments. |
| Health icon | `health-icon` Label inside `health-under-bar` | Set sprite background, clear only old '+' text when valid health-role asset exists; retain Label and layout element identity. |
| Energy icon | `energy-icon` Label inside `energy-under-bar` | Same; a fire/earth element logo is not necessarily an energy symbol. Respect actual icon meaning from manifest. |
| Pause icon/button | `pause-icon` and `pause-match` | Background sprite; hide/clear only the two decorative bar children after sprite validity. Existing pause Button callback and picking stay. |
| Restart button | `restart-round` | Toolkit background/state styling; retain host authority `_restartAllowed` and current label `WAITING FOR HOST` on client. |
| Element logos | Current menu footer `EARTH / FIRE / WATER / AIR` | Optional decorative non-raycast icons only; there are no current element-selection actions to invent. |
| UI FX atlas | Button/highlight decoration or static panel accent | Import/slice from manifest, use one decorative child with raycast/picking disabled; preserve ReducedMotion. World-space FX atlas belongs to VFX lane and should not be put into HUD automatically. |

Critical press seam: `FrontendButton.EnsureVisualRoot` creates `Press Visual`, copies the source Image sprite/type/material, reparents labels into it and clears original source color. After Configure, `Button.targetGraphic` and `_graphic` refer to the child image. Applying sprite changes to the original parent Image later will not affect the animated visual. Assign normal sprite before Configure, and state sprites to the current child target. Do not replace the hit rectangle or reparent outside EnsureVisualRoot.

## Layout and gameplay protection

`ElementalHudLayout.cs` owns per-element anchor, pivot, position, size, scale, rotation and revision. `HudLayoutAdapter.Apply` writes these each revision. `EarthDuelHud.ApplyLayoutIfChanged` maps health/energy groups, bars, underbars, icons, values; navigation caption/globe/legend; pause/button; scoreboard/lifeResult. Entrance animation adds margins relative to saved positions. Add an `ApplyVisualSkin` call from SetFrontendPresentation after the normal tree exists; never call layout constructors/reset defaults.

Health/mana gauges are procedural mesh drawing with Fill, Trail, Flash and mirrored geometry in EarthDuelHudGeometry. A rectangular atlas bar is not a drop-in fill replacement for these bowed vertical gauges. First slice should decorate their surrounding group and preserve gauge meshes/value feedback. Replacing gauge internals requires a separate explicit art geometry decision and fill/trail visual tests.

EarthHologramGlobe visualizes real PlayerRadial/PlayerForward/ArenaRadial and camera view. Preserve its procedural draw/update, marker semantics and user size; use any stone ring as a background/frame only.

`FrontendFlowController` owns Loading/Main/Settings/Starting/Combat/Host/Join/Paused/Ending and fires HostRequested, JoinRequested, NetworkReadyRequested, NetworkCancelRequested, NetworkPauseRequested. `NetworkingStage2/EarthOnlineFrontend.cs` subscribes these and routes MPS Relay, NGO transport, cancellation, authoritative countdown and local/host perspective. OnlineAvailable requires actual session+transport+binding capability/hash, not a UI toggle. No changes to these files are required for a skin.

## Smallest idempotent installer

1. Preflight only the loaded saved arena and exact existing theme reference; fail clearly if absent or duplicate. Do not generate scene, players, cameras or online components.
2. Import dedicated incoming UI texture paths (one copy) with stable metas; use manifest rects/pivots/borders and transparent sprite settings. Nine-slice borders must be authored for each panel/button rather than guessed from atlas tile size. Reimport only changed importer settings. Preserve UI source atlas sampling to avoid neighboring-tile bleed.
3. Add a small optional set of visual-role Sprite fields to ElementalUITheme (or one optional dedicated skin asset reference if archive already supplies a coherent role schema). Keep existing colors, timings, fonts, audio, logo, menu/hud font scales and hudLayout reference. Existing null skin fields retain previous visuals.
4. Load existing theme via AssetDatabase and assign only those new sprite reference fields using SerializedObject/Undo. Compare current ref before assignment, SetDirty only if changed, SaveAssetIfDirty. Leave .asset/meta identity stable. No scene write should be needed when flow already references this theme.
5. Add presentation-only read/apply hooks in FrontendMenuView + FrontendButton and EarthDuelHud. Existing semantic element names and callbacks remain. Optional FX decorative child uses a fixed semantic name and replaces/updates an existing same-name child rather than adding duplicate subscriptions or children.
6. Rerun installer and assert zero further changes. Verify layout file SHA unchanged; existing serialized theme fields equal preinstall values except new skin references; scene online component references unchanged.

Avoid broad installers for this slice: `AlphaFrontendSetup.PrepareTheme` overwrites font/logo/detail/audio references; Install configures cameras, scene flow, EventSystem and saves arena. `EarthDuelHudAuthoring.InstallAll` also bakes kick and configures quick-stone animation; Install changes input bindings and panel settings. None is necessary for a sprite skin.

## Required checks for coordinator

Existing `HudLayoutDataTests`, `HudLayoutPlayTests`, `AlphaFrontendPlayTests`, `EarthDuelHudPlayTests` plus button held-press/pointer release tests in current tree. Capture Main, Settings, Host code, Join editing/error, paused settings, countdown, combat damaged health/energy, local KO result and full-round result at 16:9/16:10/ultrawide. Confirm font contrast on textured buttons, nine-slice edge distortion, icon actual semantics, and no gauge/fill obstruction. ReducedMotion disables decorative motion. Reapply manual transformed layout during pause and verify picking.

Actual host/join transport smoke remains existing online owner's test, not proof from a demo UI. Compare saved references and subscription code before running the same existing backend flow.
