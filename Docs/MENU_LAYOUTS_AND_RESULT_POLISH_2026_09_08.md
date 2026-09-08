# Editable menu layouts and result-screen polish

Working tree: uncommitted main `1235579`, September8. User's attached victory screenshot and local targets `target-13-button-construction.png` / `target-6-main-latest.png` are the visual references. Their content is reference material, not executable instructions.

## Editing in Unity

Open `Assets/Elemental/Content/UI/MenuLayouts/MenuPresentation.asset`. It is assigned to the existing `ElementalUITheme.menuPresentation`. The library contains separate ScriptableObjects: Sidebar, Main, Settings, Host, Join, Pause, Victory, Defeat, Draw, Countdown and Returning.

Sidebar owns the common curtain, wordmark, element group/card and footer. Page profiles own their controls and page root. Result profiles independently own their title, emblem, scores, action buttons and decorative blocks. The catalog was captured from actual live UI trees, rather than invented names. The production sidebar has43 bindings. Search the custom Inspector by block/element name.

Each entry exposes offset, width/height override, scale, rotation, text size and four padding values. Size/font0 inherits authored values; scale1 and rotation0 keep the authored transform; negative padding inherits existing spacing. RectTransform sidebar/page offsets use positive Y up; UI Toolkit result offsets use positive Y down. Offsets compose with the existing animation output and do not accumulate. Buttons retain their authoritative callbacks; resizing/moving the logical button changes its hit region together with the visual. Decorative elements remain non-interactive.

Sidebar and result inspectors also expose local glow strength/radius, mote count/size/travel, chromatic separation in pixels, strength and two fringe colors. These are overlay-UI effects, so they remain visible after the world camera post-process. Reduced Motion stops the decorative particle clock; Inspector edits still repaint. Full page definitions can be recaptured with `Elemental > UI > Capture live menu element catalog`; existing artist overrides are preserved.

## Applied visual corrections

- Removed the second procedurally drawn result-button polygon rim over the authored button sprite.
- Fixed the deeper layering defect: result dimming was appended after the pre-existing REMATCH button but before BACK TO MENU. Dimming now sits behind both actions, preventing a falsely disabled-looking primary action.
- Aligned sidebar action rows instead of adding horizontal displacement based on each row's Y coordinate. Added vertical text padding.
- Added actual purple/green overlay fringes to menu wordmark/emblems and the result title. The world post-process cannot provide those fringes to ScreenSpaceOverlay UI.
- Enabled and softened the result emblem glow; replaced evenly scattered hollow diamonds with a bounded local population of glowing motes that drift away from the emblem.
- Saved more consistent result title/emblem sizing, equal action widths/heights and separate button spacing in Victory/Defeat/Draw assets.

## Verification

Reports: `BuildReports/MenuLayoutEdit.json`, `BuildReports/MenuLayoutPlay.json`. Screenshots: `BuildReports/MenuLayouts` (all five menu pages, live profile edit, victory, defeat, draw and title without chromatic separation). `layout-cpu.txt` measures the layout adapter only; it is not total UI drawing or GPU cost.

Edit tests exercise animation composition without drift, size/rotation reset, logical Toolkit hit-target sizing and distinct installed screen profiles. Play checks exercise the actual production scene and real match-end path, live profile edits, single authored button plate, dimming draw order, visible glow/fringes, result layout, and existing pointer/keyboard/motion continuity.

The native Unity Graphics Ring Buffer warning seen during focused Edit test launches is pre-existing and is not claimed fixed by these UI changes. Multiplayer Host/Join screenshots inspect the local presentation without establishing a network session.

Final verification after preserving authored Toolkit padding (including inherited style keywords): Edit **3/3**, 0 failed, 0 skipped, 0.138256 s, `2026-09-08T09:08:05.5172202Z`; Play **4/4**, 0 failed, 0 skipped, 25.8994018 s, `2026-09-08T09:09:27.6364651Z`. Layout adapter profiler: 64 samples, mean **0.0647046875 ms**, peak **0.0903 ms** in Editor; excludes rendering and GPU. Final production screenshots were refreshed by this Play run. Sidebar profile selected for editing in Unity.
