# Graphics UI staging review — 2026-09-07

Scope: staging only, no Unity calls, no actual Assets edits. Ready for root import. Reviewed against GraphicsLane/UI_MAP.md and supplied manifest. Offline Unity Roslyn Presentation, Authoring.Editor, Tests.EditMode and Tests.PlayMode all exit 0; this does not claim importer or Play execution. All 89 runtime PNG dimensions match manifest headers.

Concrete refinements:
- ElementalStoneSkin.HudBinding owns snapshots per live HUD. Removing the optional skin or individual icon restores previous label glyph, background and pause strokes. Manual layout and hierarchy are untouched. EarthDuelHud owns the binding; no shared asset references to live UI trees.
- Installer validates unique paths/IDs, dimensions, borders, pivots, linear masks and required roles, then preflights every runtime importer before first mutation.
- Added StoneSkinContractTests: reversible partial skin/manual layout/child identity, original manifest accepted, escaping path and oversized border rejected.
- Added separate StoneSkinInstallerIdempotenceTests: requires installed skin, runs installer twice, checks bytes of all imported assets/metas, theme, user HUD layout and Elemental scenes plus theme JSON and skin GUID. Not executed during staging review.

Run after importing:
1. Elemental/UI/Install Stone Artwork
2. Elemental/QA/Stone Skin Edit (2 contract tests)
3. Elemental/QA/Stone Skin Installer Idempotence (1 explicit mutating verification)
4. Elemental/QA/Stone Skin Play (existing production flow/layout/feel fixtures and captures)

Integration audit:
- Actual FrontendFlowController/network backend bindings are retained; artwork attaches to existing frontend controls.
- Button resting sprite is set before Configure transfers the Image to Press Visual; state changes use that child target. Existing callbacks/rect interaction stay in place.
- Saved theme receives only optional stoneSkin reference. No broad frontend installer, layout rewrite or scene replacement.
- HUD icon roles use existing named nodes; health/energy gauges and globe remain procedural original elements.
- Packed masks remain linear/default textures without alpha-transparency processing; Environment uses mipmaps/repeat; UI sprites use full rectangle, custom pivot, exact manifest L/B/R/T slicing, no mipmaps and uncompressed RGBA. FX files are imported assets, not a claim that every atlas already drives animations.

Capture watchpoints (not measured failures): assess light text against primary earth and disabled button art; check 64px button side caps at actual 470x70 size; code field padding relative to 30px borders; curtain fit against original veil at widescreen; ribbon narrow 4px center stretch at score panel. Judge real 16:9 and narrow captures; no invented contrast/performance metrics.
