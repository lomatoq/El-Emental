# UI handoff, 2026-09-07 12:48 local

Unity/actual Assets lease returned to root, editor explicitly stopped. No world shader edits or camera orbit edits were made.

Actual latest UI source is compiled (Presentation.dll 12:45:59). It contains colored emblem restoration, common centered header/element block, compact 78px buttons / 12px gaps, 4% hover/focus visual enlargement, 1.24x active magic with stronger halo, cream selected native sprite, selected panel texture, bright slider centers, improved footer, and runtime-owned Main-only vignette Volume (intensity .48). Original HUD layout/theme font assets remain retained; reference profile remains optional. Vignette weight is zero outside real FrontendState.Main and affects camera postprocessing before overlay UI. This latest group has NOT received fresh Play visual acceptance.

Last validated run: BuildReports/StoneSkinPlay.json UTC10:40:44, 4/4 Play tests; all ten PNGs in BuildReports/StoneSkin. These show earlier hybrid layout, corrected timer inside plate, raised opaque-backed wheel, and all actual round outcomes. They precede the last logo/cream/panel/vignette revisions. Initial V2 before latest hybrid is preserved under BuildReports/StoneReferenceV2/FirstPass.

Runtime asset bindings persisted: referenceSelected GUID01ff413229fad0e498e6379ef2bb988e (native cropped Sprite asset); referenceCurtain GUIDb5bfe1b7fa841bb40b5fed3a8a693001. Curtain PNG was replaced with parent-provided panel-selected-alpha-v1, preserving GUID. It is the selected v1 style, not rejected v5. Original image data was copied unchanged. Generated art provenance is owned by ReferenceUIArt lane.

## Small staged followup, not actual Assets
Tools/FireIntegration/Staged/UIFinalFollowup contains fresh before/after of FrontendMenuView and StoneSkinPlayTests only. Adds authored element_select easing to the 1.24x selected-magic final size. Strengthens tests for emblem presence, fixed 12px row gaps, 4% visual growth with unchanged hit rect, and vignette zero in Combat. Settings capture now opens the real Settings callback so Main-only vignette is correctly zero there; Host/Join remain presentation inspection without creating network sessions. Offline Presentation and Tests.PlayMode compilation passed; no Unity call was made after lease return.

Next import those two small diffs only after before hashes match. Run Elemental/QA/Stone Skin Play once, inspect fresh Main/Settings/Combat immediately, then remaining outcomes. Do not count metadata or test pass as visual match. Main orbit candidate from MenuForegroundFraming remains unapplied and needs its separate A/B.

UIFinalFollowup now has THREE files: added StoneReferenceHudPresentation fix from inspected Victory/Defeat/Draw captures. Hides existing combat child visibility only during actual round-over, including wheel (previously covering Back action), top clock/logo and per-life toast. Restores original visibility on restart. Tests assert both hidden result state and restored combat state. Offline Presentation/Play compilation passed. No actual Assets/Unity touched.
