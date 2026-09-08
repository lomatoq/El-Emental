# Alpha frontend resources and ownership

The frontend is uGUI + TextMesh Pro bundled with Unity UI2.5.0; combat remains UI Toolkit. Both use the saved ElementalUITheme. Existing EarthCoreSlice arena and authored player are reused; no animation assignments replaced.

## Authoring
- Elemental > UI > Install Alpha Frontend: targeted installation on the existing open arena.
- Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset: palette, fonts, logo/detail, seven audio roles, interaction timings.
- Alpha Frontend / CinematicMenuCamera: Dutch10 degrees, FOV38 degrees, character screenheight55 percent. Actual character and Cinemachine brain.
- FrontendFlowController owns readiness/round start; HUD does not start a round. After world readiness, a four-second 4–3–2–1 precedes combat. The camera starts far away at 150 mm, continuously approaches while widening to the gameplay lens, and aligns with gameplay in the final 1.5 seconds. Menu exit fades over 0.85 seconds.
- Settings save per local user: master/UI volume, pointer camera sensitivity, Reduced Motion. Authored camera profile remains intact; runtime clone holds user overrides.
- Theme menuFontScale and hudFontScale are relative to the existing layout. HUD keeps its previously increased type at scale 1. All combat HUD text, including numbers, uses Varose per latest user instruction. Room code/error roles retain the readable fallback.
- Menu navigation selects the first active control for keyboard input. Button compression takes 70 ms and release 120 ms; selection and pointer input use the same controls.
- The cinematic camera frames the rendered helmet/plume silhouette. Small foreground occluders are hidden only for the presentation and restored with their original rendering flags. Physics and arena transforms stay intact. DOF fades during the final 1.5 seconds of the countdown.
- Elemental > Build > Build Local Alpha From Saved Arena builds a non-development Windows player directly from the saved EarthCoreSlice. It does not run the arena generator or overwrite scene/project settings.

## Assets and rights
- DonGraffiti.otf: display/title TMP atlas with readable fallback.
- Varose-Regular.otf: English menu labels and all in-game HUD labels/numbers. Supplied ReadMe limits it to PERSONAL USE. Prepared for the requested LOCAL prototype; public distribution requires a suitable license or replacement. No public distribution rights inferred.
- LiberationSans SDF: TMP Essentials, room codes/errors and missing-glyph fallback.
- ElementalLogo.png: original supplied image; unchanged pixels/proportions. Assigned to default and Standalone PlayerSettings icon sizes by **Elemental > UI > Apply Elemental App Icon**. The executable needs rebuilding to embed the new icon; changing PlayerSettings does not modify an existing executable.
- ButtonFX: seven dry CC0 cues mapped in SourceAssets/Frontend/ButtonFX-selection.json. Hover, Press, Confirm, Back, Error, Copy, Connect; hover rate limit100ms.
- CraftPix RPG & MMO UI4: only CharacterCreate_Separator and Slider_Horizontal_Bar_Background textures extracted as optional details. No demo scenes,170 scripts or prefabs imported.
- Original archives remain in SourceAssets/Frontend outside Unity build resources.

## Verification
AlphaFrontendPlayTests checks Main/Settings/Combat, actual character presentation clock and camera, repeat clicks/entry and three aspect ratios. Captures under BuildReports/AlphaFrontend. Existing HUD layout and its+12 percent font baseline retained.
Host/Join stay explicitly unavailable until genuine online integration passes; no mock codes.

AlphaFrontendPlay passed 1/1 at 2026-09-06 16:02:40 UTC, including keyboard entry, repeated transitions, DOF restoration and preserved timer font size. Main/Settings/Combat captures were inspected; the hidden-layout font-size regression was fixed using explicit theme roles (timer 38.1 px, score 51.5 px). Frame recorder numbers in metrics.txt are whole-editor frame samples, not isolated UI cost.

2026-09-06 17:08:19 UTC: updated AlphaFrontendPlay passed 1/1 with countdown, all-HUD Varose, local pause/resume/settings/end-match and repeated match entry. Use Elemental > UI > Select In-Game HUD Theme for fonts, size roles and colors. Pause uses real unscaled UI while local world time is zero; online pause suppression is prepared separately and does not stop the shared world.
