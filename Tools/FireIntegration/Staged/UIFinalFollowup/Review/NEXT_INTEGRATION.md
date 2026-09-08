# Next UI integration and visual review

Latest task scope: regular colored emblem + centered serif wordmark/element block; compact equal button gaps; active magic visibly larger/brighter; cream hovered/keyboard-selected buttons with subtle motion; clean true round result; Main-only background vignette. This review made no Unity calls or actual Assets changes.

## Ready patch

UIFinalFollowup now contains FOUR source files. Baselines were rechecked against actual Assets and still match. Offline Presentation and Play tests compilation passes.

- FrontendMenuView: adds authored selection easing to final 1.24 active-magic scale. Current actual header/banner/elements share approximately x280 center within the logical 560px content column; caption center x285. Wordmark now x30+500/2 =280. Colored emblem + eyebrow are balanced as a combined top row.
- FrontendButton: separates pointer-inside from keyboard-focus state. Previously OnDeselect unconditionally canceled a still-hovered button; now either hover OR focus retains cream/1.04 visual state. No layout/hit-rect mutation.
- StoneReferenceHudPresentation: hides existing combat decorations only while real IsRoundOver. Actual Victory/Defeat/Draw captures showed wheel covering Back to Menu and duplicate life outcome/clock. Visibility restores on restart; life-result state and gameplay remain intact.
- StoneSkinPlayTests: emblem/12px stable-gap/4% hover assertions, hover surviving deselection, real OpenSettings callback (so vignette is correctly OFF), Combat vignette0, result overlap/restore assertions.

Do not reimport the entire old V2 lane: it is historical and would miss the new followup. Verify sha256.json before each copy; if any baseline changed, rebase only that diff.

## Next window after environment releases Unity

1. Finish and save the environment work to the intended scene. Confirm Play stopped. Copy only the four after source files after hash checks.
2. Refresh once and wait for new Presentation/Play DLL timestamps and console0; a Refresh tool acknowledgement alone did not prove recompilation earlier. Do not launch a second QA task while one is pending. Ensure Unity has focus if compilation is deferred.
3. Run Elemental/UI/Install Stone Artwork. Verify the selected-panel texture is the parent-provided panel-selected-alpha-v1 source, referenceSelected/native cropped Sprite exists, and referenceCurtain points to that existing texture GUID. No V5 fallback as accepted art. Run Stone Skin Installer Idempotence once because new raster binding/import paths changed.
4. Run Elemental/QA/Stone Skin Play once. While it runs, inspect only files/console, not compiled RunCommand diagnostics. It produces Main/Settings/Host/Join/Pressed/Combat/Pause/Victory/Defeat/Draw at1920x1080. Send Main/Settings/Combat as soon their fresh mtimes appear. Host/Join are UI inspection without opening network sessions; Settings uses real callback.
5. Inspect cream caption/icon dark readability; regular colored emblem; element block common center; Earth1.24 relative to70px peers with bright halo; consistent12px gaps before hover and no row movement during hover; non-overhanging wordmark and banner; Main vignette stronger in background but not on UI; Settings/Combat vignette zero. Inspect all three results for no wheel, duplicate clock/logo or life notice and working restart/menu. Existing tests preserve original theme.hudLayout JSON and node identities.
6. Secondary framing check at user's1600x745 and narrower1366x768 after primary acceptance. Height-matched Canvas scales reference positions proportionally, so ultrawide should not rearrange rows; evaluate panel/text edge fit rather than incorrectly comparing logical pixel width with screenshot pixel width. At 4% hover, each78px row grows1.56px above/below around its center, leaving8.88px visual space when both neighbors enlarged.

## Remaining visual risks that require fresh frames

The final panel has a1024x1536 source, displayed900x1080, hence horizontal stretching relative to its native720px full-height width. This was chosen to keep interior content readable, but it is not pixel-faithful panel geometry; inspect the selected art's edges before claiming match. Main font/emblem positions, cream state and new panel have not yet been captured together. The gauge outline and scoreboard side wings remain more minimal than reference1; prioritize latest explicit Main issues first, then decide from real combat capture. New vignette Volume is runtime-owned, priority100, Main-only weight1 and0 elsewhere; it affects URP camera postprocessing before ScreenSpaceOverlay, not UI. No atmosphere shader change was made.

## Menu orbit35→0 candidate

Reviewed source confirms presentationAzimuth only participates in non-countdown menu framing; countdownAzimuth85, fieldOfView38, characterScreenHeight.55 and Dutch10 remain. It is shared by Main/Settings/Host/Join presentation, not strictly only Main. Canonical heavy/light stones occupy sectors around35 and-40 degrees, so0 is a justified candidate between them; this still needs live A/B and must not be called visually accepted from bounds.

Review/SetMenuOrbitCandidateReviewed.cs adds required result.RegisterObjectModification BEFORE the existing Undo/serialized edit. It refuses Play, wrong scene, ambiguous owner or baseline not35/0; does not save. Test fixture reloads the saved scene, so a dirty-only candidate will not affect its capture. To accept it, preserve the current post-environment scene baseline, apply the one property, review a real Main capture, then save only if accepted. No physics/mesh/collider/render-suppression/FOV adjustments as a workaround.

Review/menu_camera_exception.py is an opt-in narrow preservation predicate. It accepts only record1968201427, type114, exact CinematicMenuCamera scriptGUID8310ec71fcbc56943bc2c082548b525c, and exactly presentationAzimuth35→0 with every other byte unchanged. Six checks pass, including rejection of lens/countdown/owner changes. Integrate the predicate into verify_graphics_preservation.py only after candidate acceptance, recording its explicit exception separately. Do not exempt all camera records. Script default35 may stay so other scenes keep their authored defaults.

## Latest screenshot correction (supersedes earlier panel sizing notes)

UIFinalFollowup now FIVE files including ElementalStoneSkinInstaller. Latest screenshot7aec1534 reviewed. Cream texture metadata had nPOTScale1 (default rescaling) while its native crop assumes2007x783. Installer now forces NPOTScale.None, validates native dimensions, constructs the same correct crop(27,273,1952,290), and compares/copies Sprite serialized data as needed while retaining existingGUID. PNG itself untouched. Play contract verifies native dimensions and vertical UV range after import. Actual visible correction still needs fresh capture; do not assume changing crop y fixes wrong import scaling.

Panel now1.3x localScale aboutleft/mid pivot, with persistent base offset(-130,0), allowing top/bottom/left bleed. Content x54 instead84 shifts brand/element/banner/buttons together30logicalpixelsleft, preserving their commoncenter. Panel motion adds to its baseoffset instead of resetting it. Bottom-right footer nowone430x68block with highlighted actual selected element, goldrule, bright diamondcore and softglow, replacing mismatched normalized anchors. Equal12px rowspacing remains unchanged.

Import latest five source afterfiles afterSHAchecks, wait actualcompile, THEN rerun Install Stone Artwork so NPOT+Sprite reconstruction occurs before capture. Run installer idempotence because this migration must be stable acrossreruns. Existing test fixture now checks panel1.3 and croppedSpriteUVs in addition to behavioral assertions. Offline Presentation/Authoring.Editor/Tests.PlayMode compile0. NoactualAssets/Unitycall during thisrevision.
