# Earth material pass — implementation and evidence

Scope: the approved 11-part pass, September 4, 2026. Worktree on
`codex/environment-aware-motion-matching-spike`, base HEAD
`d2174eded114dd022e4a9c442abadda7a0e44555`. Uncommitted user changes are preserved.
This is not a whole-project acceptance report.

## Integration

Use `Elemental/Setup/Integrate Earth Material Pass (Preserve Scene)` with
`EarthCoreSlice` open and Play stopped. This only wires the existing scene,
adds the material-feedback root and deterministic scatter, and updates this pass's
generated mesh cache. It never recreates the arena or changes authored transforms,
light color/intensity/direction, camera/DOF, or user animation choices.

Use `Elemental/Animation/Bake Backward Run EAMM Library` to import the existing full
Humanoid backward-run take, append `(0,-6)` without replacing other tree entries,
and rebake the database already referenced by both fighters. EAMM is not disabled
to make the new tree entry appear to work.

## Checklist

| Item | Implemented contract | Verification status |
|---|---|---|
| Dust color | Assigned material supplies RGB; particle tint preserves alpha only; missing references repaired; fracture uses its own configured material/layer | Fresh Play emits 140 dust + 28 chips; aged Game capture shows a visible puff |
| Common effects | Typed causes, eight local groups, 1.5 m merging, 256/64 budgets, per-event overrides and overflow counters; radial particle gravity | Eight separate production contacts keep all locations and emit 256 dust/64 chips while wall pieces move |
| Backward run | Signed MoveX/MoveY, 0.10 s filtering; existing clip/recipe; corrected schema-2 collapsed-space retarget bake | Actual EAMM weight 1, Active, upright candidate checked for 12 frames; MoveY below -6, displacement over 3 m |
| Armor | 44 m/s, convergent point aim, 2.5 degree directed cone; no tail attenuation | All 96 actual single shots measured at 44 m/s within tolerance; aimed volley solver checked |
| Impact breakup | Small/medium/huge 0/4/3 physical parts plus 24/64/140 dust and 8/16/28 chips; bounded depth, sibling immunity | Both medium and huge split tests preserve mass/targetability after cosmetic lifetime; duplicate and full-pool rejection checked |
| Bevels | Prepared real geometric chamfers for missing generic, fragment, armor, pillar/wave, meteor, surf and scatter meshes; collider sources unchanged | Geometry/cache tests pass; new board visible in Game capture. Existing imported arena chamfers are conservatively preserved, not re-authored |
| Technique cues | Extraction, emergence, wave, armor, fracture/repair, contact/friction, ground steps/roll/land hooks | Code paths integrated, including released wave landings and bot/subthreshold impacts; exhaustive technique-by-technique visual review remains open |
| Board | 12 irregular chamfered stones, 3 protected core, 9 outer; dust/chip wake; detached stones survive recast | Real release/recast test passes; production 12-stone release moves over 0.5 m; assembled/released captures |
| Gravity grip | Mouse/wheel distance ownership, tap/charge, release dynamics before velocity, no fast-scroll return | Actual production wall releases 3 pieces at about 31 m/s and over 1.2 m displacement after two physics ticks; raw button routing not physically replayed |
| Shadows | Camera shadows restored; suppression removed; cast/receive enabled; correct URP high-soft variants | Game shadows visible; narrow 5-pose temporal-pan metric passes (MAE 0.733/255); nine bias captures retained, user Sun unchanged |
| Planet scatter | Seeded large/medium interactive stones and combined cosmetic clusters; real surface/exclusion checks | 24/24 large, 160/160 medium, 128/128 clusters (1515 cosmetic stones), zero rejected slots with 8 placement attempts |

## Current evidence

- `BuildReports/EarthMaterialPassEdit.json`: 46/46, UTC
  `2026-09-04T00:40:55.8104476Z`, 1.690 s. Includes three actual-Linebreaker
  collapsed-bind round trips, signed movement, configurable split policy and bevel checks.
- `BuildReports/EarthMaterialPassPlay.json`: 5/5, UTC
  `2026-09-04T00:45:07.5481212Z`, 16.369 s. Includes persistent medium/huge breakup,
  old accretion-to-shatter regression, actual gravity release and board recast.
- `BuildReports/EarthMaterialPass/Latest.json`: actual production particle counts,
  first/last armor velocities, board displacement, gravity velocities/displacements,
  EAMM status/native job weight and eight-contact stress budgets.
  Latest production UTC `2026-09-04T00:44:51.8242191Z`: 30 wave cells,
  24 observed emergence batches and a successful physical launch-pillar action.
- `BuildReports/EarthMaterialPass/`: fresh Game screenshots for fracture, board,
  backward motion, gravity release and separated-contact stress. They are screenshots,
  not a recorded motion video.
- `BuildReports/RenderingAB/Mvp01-shadow-temporal-pan-metrics.json`, UTC
  `2026-09-04T00:14:27.9968869Z`: five poses, narrow column ROI only; not a global
  no-aliasing certificate. Bias matrix did not overwrite the user's Sun settings.

Scene baseline before integration: arena `(0,54.12,0)`, player
`(-0.26,56.82,-0.11)`, opponent `(-0.27,56.67,3.55)` (rounded display values).
Sun color `(1,.91,.78)`, intensity `1.28`; these were not recolored or relit.

## Manual settings

All profiles live in `Assets/Elemental/Content/Profiles` unless stated otherwise.

- `EarthEffectsTuningProfile`: particle materials and layer sizes/lifetimes/speeds;
  `Material Events` controls budgets, event-family intensity and per-event counts/size.
  Count -1 retains the size-class/event request. `Fracture/Dust` controls fracture
  lifetime/size/speed; `Impact/Dust` controls ordinary contact puffs. Dust RGB is
  in the assigned `RumbleDustLit` material, not a second brown tint in code.
- Armor profile: aimed projectile speed, directed spread and existing fire cadence.
- `EarthRockProfile`: size classes, breakup thresholds, medium/huge part counts,
  maximum split depth (hard cap 2) and small-impact speed; physical pool capacity
  remains a bounded resource, not permission to steal airborne pieces.
- `EarthStoneBevelProfile`: chamfer width and short-edge fraction. Run the narrow
  integration menu after changing the cached scatter mesh settings.
- Surf profile: assembly, wake density, nose angle and release behavior.
- Gravity-well profile: focus distance, charge duration and radial release speed.
- `EarthPlanetRockScatterProfile`: seed, counts, sizes, separation and placement
  attempts. Regenerated at Play startup, under its own generated root only.
- Shadows: `Sun` (softness/strength/bias), Game Camera URP `Render Shadows`,
  individual Renderer `Cast Shadows`/`Receive Shadows`, and the active URP asset.
  Arena material `_SideShadowFade=0` allows side-face reception.

### Dust and shard authoring follow-up — September 4

The user's current `Sun` was saved from Edit mode: Soft shadows, strength `0.554`,
bias `0.05`, normal bias `0.4`, near plane `0.2`, pipeline shadow settings enabled.
The scene builder now uses the same strength. No scene rebuild was needed.

In `EarthEffectsTuningProfile`, expand `Materials / Impact Rubble` to open
`Assets/Elemental/Content/Materials/LooseEarthChipVfx.mat` for the small shard material.
Expand `Impact / Rubble` for particle size, lifetime, speed and tint;
`Impact / Maximum Rubble Count` caps ordinary impact counts. For individual
techniques use `Material Events / Events`: `Kind`, `Chip Count` (-1 inherits the
event count, 0 disables chips) and `Particle Size Scale`. `Pillar` controls the
lift chips and wave rubble. `Materials / Pillar Chips` is their source material.
The separate Tier-C draw is on `Earth Magic Runtime / Earth Indirect Debris Renderer`:
`Material`, `Mesh`, `Maximum Lifetime`, `Visual Gravity`.

Cosmetic mesh particles, pillar chips, surf cut chips, indirect debris and the
lookdev demo now draw before dust without writing scene depth. Owned runtime
material copies retain their authored source assets and are disposed by their
presenters. Physical/targetable stone and structural chunks still occlude effects.

Fresh scoped evidence on the same dirty `d2174ed` worktree:
- `BuildReports/EarthEffectsTuningEdit.json`: **5/5**, UTC `2026-09-04T07:35:53Z`.
- `BuildReports/DustCompositingPlay.json`: **2/2**, UTC `2026-09-04T07:37:29Z`,
  14.516 s, pixel regression plus production material smoke.
- `BuildReports/DustCompositing/Latest.json`: real D3D11/URP pixels at 96x96;
  dust behind a mesh particle changes its center RGB by mean `0.151`, and behind
  the pillar mesh by `0.275`; opaque foreground wall difference is `0` for both.
- Production smoke emits and ages **140 dust / 28 chips**, handles eight separate
  contacts within **256/64** budgets and observes max hub/presenter marker times
  **0.0246/0.1395 ms**. These are scoped observations, not p95 or zero-GC evidence.
  Existing eight orphan armor-component warnings still appear during scene load.

Re-run via `Elemental/QA/Run Earth Effects Tuning EditMode Tests` and
`Elemental/QA/Run Dust Compositing PlayMode Tests`.

## Known boundaries

- Existing saved-scene armor orphan warnings predate this pass: eight incomplete
  inactive root objects lack required components. They were not deleted or used
  as a reason to claim a warning-free project. Runtime armor builds its own pool.
- One manual probe was invalidated by script reload during Play (`_session` lost);
  only fresh-Play test runs count as runtime evidence.
- No new full regression suite, native build or 720-frame performance certification
  is claimed. The user requested a short focused pass.
- Implementation is integrated with focused runtime evidence. Overall **full visual
  acceptance is not claimed**: a continuous motion recording, exhaustive all-technique
  feel review, and raw mouse/button-edge playthrough remain open. Do not relabel
  screenshots or component-ready flags as those missing checks.
