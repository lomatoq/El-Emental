# Local alpha settings and acceptance

Saved scene: `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity`.

## Settings map

| Setting | Location |
| --- | --- |
| HUD group/child anchors, position, size, scale, rotation | **Elemental > UI > Edit HUD Layout**; `ElementalHudLayout.asset`. Health/Energy > Under Bar moves icon + number; Icon/Value edit each child. Navigation and Pause have independent child transforms. See `Docs/HUD_LAYOUT_TUNING.md`. |
| Body hit response, stones, wall links | Unity menu **Elemental > Tuning > Impacts & Stones** |
| Local PhysX pose drive and regional limits | `Assets/Elemental/Content/Profiles/CharacterImpactResponseProfile.asset`: weaken 0.12 s, recovery 0.5 s, parent transfer 40%, medium stun 0.24 s |
| Shared stone mass | `Assets/Elemental/Content/Profiles/EarthMatterMassPolicy.asset`: density 2300 kg/m³, physical reference 230 kg, gameplay reference 120 kg, exponent 0.68, fresh-object limits 12–1800 kg. Inherited fragments conserve parent mass. |
| Wall strength and surface retention | `Assets/Elemental/Content/Profiles/EarthWallProfile.asset` and its fracture profile, reachable from the tuning window |
| Two arena boulders | `Magic Push Boulders/Light Push Boulder` and `Heavy Push Boulder`, EarthDestructibleDecorRock; Initially Anchored remains false |
| Height of the TWO-MOUSE-BUTTON pillar line relative to ground | `Assets/Elemental/Content/Profiles/EarthPillarWaveProfile.asset`, **Смещение от земли, м**, saved **−0.35 m**. This is the placement offset, separate from pillar height. |
| User animation controller | `Assets/Elemental/Content/Animation/KayKitMage.controller`; user clip assignments retained |
| Derived EAMM catalog | `Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset`; targeted bake reads the current controller children |
| Menu/HUD fonts, sizes, palette, timings, sounds; **Elemental > UI > Select In-Game HUD Theme** | `Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset` |
| Menu camera framing | Scene **Alpha Frontend / CinematicMenuCamera**: 10° Dutch, 55% character height, FOV 38°; countdown side azimuth 85°, elevation 18°, starting focal length 150 mm; continuous approach over 4–3–2–1, final 1.5 s aligns with gameplay |
| User audio/sensitivity/Reduced Motion | Main > SETTINGS; saved per local user without editing the authored camera profile |

## Current evidence

- SharedMassPolicyEdit: 7/7; SharedMassPolicyPlay: 3/3 (production extraction/accretion/splitting, boulders/walls and debris).
- AlphaLocalImpactsEdit: 58/58. AlphaLocalImpactsPlay: 3/3 at 16:12:40 UTC.
- LocalPhysicsAcceptancePlay: 2/2 at 16:19:49 UTC. Five actual thrown-stone regional videos and CSV are in `BuildReports/LocalPhysicsAcceptance`; head/arm/leg peak and recovery frames inspected.
- Local physics callback CPU mean: 0.008851 ms step, 0.080747 ms pose; current-thread allocation brackets: 0 B. Native PhysX solver time is outside those markers.
- Four wall/boulder Play cases passed at 16:01:04 UTC. Their combined suite's separate cadence case was not yet passing. Narrow/wide wall frames inspected.
- Historical AlphaFrontendPlay 1/1 at 17:08:19 UTC did not prove countdown visibility: the test checked text changes but missed inherited CanvasGroup alpha. Fixed by separating menu contents from the countdown sibling. The 17:45:59 run passed visible alpha for all four digits, actual pointer pause, preserved round state and repeated entry. All HUD text explicitly uses Varose; 38.1 px timer and 51.5 px scores retained. The subsequent 150 mm continuous dolly change is undergoing actual output-camera and framing checks; the earlier pass does not cover it.
- AlphaPhysicsRhythmEdit: 42/42 at 16:44:01 UTC; AlphaCadencePlay 2/2 at 16:51:11 UTC. Ten-cycle actual/requested ratios player 0.999438, bot 0.999798, both settled feet accepted. Slope clearance and visible turn completion remain separate unresolved gates.
- Windows LocalAlphaSavedArena build succeeded at 16:27:08 UTC, 464631344 bytes, 78.7 s, zero errors. Its build report has 185 package shader warnings, seven pre-existing obsolete-API warnings and one new unused HUD field warning; the latter was removed afterwards and requires the final incremental rebuild.
- Standalone window smoke test is pending: Computer Use app authorization timed out before launch.

Stage 1 is not declared complete until the remaining slope/turn/physics steering, final build and commit are handled. Stage 2 implementation is staged outside Assets in `BuildReports/AlphaImplementation/Stage2Pending`; SDK installation, real room/two-player tests and its own commit remain pending. Host/Join are explicitly unavailable in the local alpha. No fake rooms or acceptance claims.

Camera checkpoint 2026-09-06 18:12:14 UTC: AlphaFrontendPlay 1/1 passed actual lens, continuous inward distance, all four visible digits, fighter viewport bounds, pause and repeated entry. Digit samples moved inward 29.73 → 25.84 → 20.28 → 14.70 m with focal lengths 136.16 → 113.74 → 81.61 → 49.42 mm after the exact 150 mm initial virtual lens. Fixed two competing lens writers: the legacy rig no longer writes FOV while externally driven, and additive loading no longer installs a legacy charge effect alongside V2. Screenshot review found one foreground column partially obscuring the bot; both-subject occlusion sampling is being checked separately.

Resource inventory and local-use font restriction: `Docs/ALPHA_FRONTEND_RESOURCES.md`.

Latest physics steering accepted: wall-only bevel target 0.0525 m (3.5× former 0.015 m); shared whole/cracked/ejected geometry. AlphaRepairVisualPlay 4/4 (18:30:59 UTC) covers bevel continuity and actual repeated stone damage through the detached rig to HP 0 / KO / score. AlphaWallRepairPlay 1/1 (18:37:48 UTC) captures 40/40 cells physically returning in sequence over 20.433 s after real disassembly. Arena repair, both heavy crushes, safe cancelled-stone recycling and destroyed-rig rebind passed in the earlier 8/9 run; its sole death-fixture setup failure is superseded by the later accepted death case. Earth body-control exclusion pure cases pass. Details/video limitations: Docs/SEQUENTIAL_REPAIR_AND_HEAVY_CRUSH.md.

18:15:09 UTC follow-up: AlphaFrontendPlay 1/1 PASS after both-subject occlusion sampling. Updated first countdown frame inspected: both fighters are fully visible. Camera changes are saved in source; final executable rebuild remains pending.

Turn checkpoint 18:50:13 UTC: AlphaTurnStepsPlay 1/1 PASS with all four short-tap/180-degree scenarios, original clip ownership, horizontal step travel, both genuine Swing and all-frame Capture/exit floor gates. Actual final minimum ankle clearances 35.0/35.0/25.2/21.9 mm; toe minima −1.5/+10.5/−12.4/+3.1 mm. Slope reach remains a separate open gate. AlphaLatestEdit 29/29 at 18:24:25; AlphaCadencePlay 2/2 at 18:26:13 precedes the final contact refinements and will receive a targeted regression check.
