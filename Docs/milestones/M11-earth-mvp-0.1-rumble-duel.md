# M11 — Earth MVP 0.1 Rumble Duel

Status: rebuilt and accepted in focused EditMode, PlayMode, runtime and visual QA

## Player outcome

The shipping Earth scene is now a compact repeatable duel on a visibly larger
55.1 m-radius planet. The red player and a blue rival use the same Mixamo X Bot
Humanoid, Avatar and curated animator controller. The rival has no player-input
ownership, but approaches, telegraphs and casts a physical Earth-stone projectile.

A projectile hit hands the currently rendered X Bot pose to an 11-body skeletal
ragdoll, launches the visible body and stone-fades it after 3.5 seconds. Player and
rival use the same `HumanoidRagdollRig`; the duel controller atomically restores
their root, bones, velocities, colliders, Animator and control adapters on respawn.

The fight sits inside a small open amphitheatre made from two low sandstone slab
rings. Existing player Earth magic, thrown stones, walls, platforms, fracture and
physical impacts remain the counterplay.

## Golden path

- Shipping scene: `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity`.
- Rebuild menu: `Elemental/Setup/Create M3 Earth Core Slice`.
- Rebuild entrypoint: `Elemental.Authoring.Editor.M3EarthCoreSetup.Configure()`.
- Generated scenes are not hand-edited; source/profile changes are followed by the
  same editor rebuild command.
- `PlanetWorldProfile.radius = 55.1`; runtime transform scaling is not used.
- NativeHigh and NativeLow are the M11 targets. WebLab retains its documented
  smaller-world limits and is outside this acceptance pass.

## Rumble visual integration

- Planet ground uses `RumbleGround`; loose stone, bot projectiles, walls and all
  26 amphitheatre slabs use `RumbleSandstone` and the
  `Elemental/Graphics V5/Rumble Rock Lit` shader.
- Thrown, debris and push rocks use centered, unit-normalized physics copies of
  the approved V5 boulder/pebble/wedge library. The original lookdev meshes remain
  base-grounded and unchanged.
- The amphitheatre is two regular open rings: 12 inner V5 slabs at 7.15 m and
  14 outer V5 slabs at 8.55 m. Both tiers stay low so the court and camera remain
  readable.
- The bot presentation applies a strong blue tint while preserving the exact
  player mesh/Avatar/controller. Its charge line is cyan and its windup/cast pose
  uses the shared magic animation set.
- The player stays red, so identical silhouettes are readable as opponents without
  introducing a second character rig.
- One persistent directional light drives the scene, but Broken Crown disables its
  realtime shadow map in Game view because the architecture produced travelling
  cascade bands. Restrained DepthNormals SSAO and analytic recess form provide
  stable contact depth; transient feedback lights remain event-driven.
- ADR 0032 makes the existing one-blit depth-aware atmosphere the sole fog authority.
  NativeHigh Explore and ordinary locomotion use custom dual-subject Bokeh;
  NativeLow uses Gaussian, while unsafe motion/Web stay sharp. The same atmosphere pass
  carries a two-sample sky-only cloud cue; sunlit air motes are capped at 64/32/0
  without noise, collision, trails or lights.
- The final animation rebuild is the curated Earth motion tree rather than the
  imported clip graph. Runtime capture confirms both characters leave T-pose and
  enter locomotion/cast presentation.

## Duel contract

`EarthMvpBotPlanner` remains a deterministic pure fixed-tick state machine. It owns
Approach → Windup → one-tick Strike → Recover → Cooldown, arena guards, target
availability and body-state rejection.

`EarthMvpBotController` is the thin Unity adapter. It samples explicit references,
feeds the existing planet motor and converts the strike pulse into one pooled
`EarthFragment`. `EarthMvpMagicProjectile` owns only flight lifetime, target hit,
KO velocity change and reintegration. The profiler marker remains
`Elemental.MvpBot.FixedTick`.

`EarthDuelRespawnSolver` is the pure-data KO timer. It rejects invalid timing,
holds a fighter in `KnockedOut`, emits exactly one respawn pulse at the deadline
and never permits negative remaining time. It also exposes the final bounded
stone-fade interval. `EarthMvpDuelController` adapts that contract to two identical
visible Humanoid rigs at 3.5 seconds.

`HumanoidRagdollRig` owns 11 serialized X Bot bone bodies: pelvis, chest, head,
upper/lower arms and upper/lower legs. Animator ownership and PhysX ownership never
overlap. The old player proxy remains a hidden gameplay/impact adapter only while
the visible skeleton owns KO; the bot no longer falls as one capsule.

`EarthMvpBotPresenter` consumes planner phase and motor state. It drives the shared
Humanoid animator, blue material property blocks and charge telegraph; it owns no
hit timing, movement authority or player input.

## Wall and arena proportions

- Shipping wall height is 1.5–4.0 m, maximum chord is 14 m and base thickness is
  0.95 m before the bounded gesture thickness multiplier.
- Wall emergence uses the smoother settled lift path and the same Rumble sandstone
  family as loose/projectile stone.
- The planet surface remains the collision authority; seating is a small visual and
  collision boundary, not destructible stadium simulation.

## Performance rescue

The 187 ms cast hitch was the platform path building and cooking every fracture
cell in the cast frame. The shipping pool now authors six persistent hidden
platform roots and 288 empty piece shells in the scene. A cast reuses one dynamic
bevelled visual `Mesh` and one pre-created walkable `BoxCollider`; it creates no
GameObject, Rigidbody, MeshCollider or piece collider and performs no collider
cooking in the cast frame.

Fracture topology is solved off the main thread and accepted only after emergence.
Exactly one cell mesh/collider is prepared per rendered frame under
`Elemental.Platform.PrepareFractureCell`. An early impact is retained as pending
state and replayed after `FractureReady`. Settled platforms publish zero surface
velocity and stop repeating rider collision work.

Preview mesh/raycast work is cached by stroke geometry and capped at 30 Hz.
Presentation/grounding adds an 110 ms unsupported hysteresis window, so a one-frame
support miss cannot restart landing. Cast layer release is movement-interruptible
and settles over 220 ms; locomotion and fixed state transitions remain 65–80 ms.

The 36 m planet remains at 128 runtime chunks. Render and collider queues remain
bounded, and no rescue path reduces planet scale or voxel fidelity.

## Acceptance gates

- Compile/import: zero compiler errors or warnings and no missing serialized scripts.
- EditMode: KO/fade, bot planner, organic idle, support/preparation budget and dust
  material contracts.
- PlayMode: radius 36, 20–32 Rumble amphitheatre pieces, one bot, shared player
  Avatar/controller, no player ownership on the bot, centered V5 physics rocks,
  two 11-body visible ragdolls with atomic reset, finite sampled bot command,
  drained 128-chunk queues and one live sun.
- Cold startup: render queue peak below 30 ms and total-frame p95 below one 30 Hz
  frame.
- Runtime: movement, two pooled platforms, deferred fracture, early-impact replay,
  both KO paths and stone-fade respawn.
- Visual: fresh 1920×1080 camera renders show the larger court, both same-model
  fighters, low two-tier seating, transparent dust and four-cascade shadows.

## Evidence — 2026-08-26

- Focused rescue EditMode: `80/80` passed in `0.674 s`.
  Report: `BuildReports/RescueFinalEditMode.xml`.
- Focused rescue PlayMode: `11/11` passed in `24.924 s`. The gate includes
  staged armor, shared visible player/bot ragdoll and respawn, surf/wave/projectile
  impact routes, and platform lift/walk/pillar-jump/landing continuity.
  Report: `BuildReports/RescueFinalPlayMode.xml`.
- Latest radius-36 Editor cold run: 127 frames, frame p95 `17.84 ms`, render queue
  peak `11.70 ms`, collider queue peak `1.30 ms`, and 128 chunks drained. The
  `108.94 ms` maximum is scene/Test Runner activation, not a platform cast.
- Final 720-frame gameplay session: total frame p95 `13.99 ms`, CPU p95
  `13.99 ms`; `AcquireSolid` peak `5.30 ms` and the one-cell fracture slice peak
  `0.84 ms`. The sampled `AcquireSolid` value is a batch-Editor peak rather than a
  percentile; the former 187 ms collider-cooking hitch is absent and no platform
  cast frame breaches the 16.67 ms gameplay budget. GPU frame timing returned no
  samples in batch Editor and is explicitly left unclaimed. Raw report:
  `BuildReports/Mvp01Profiler.json`.
- The pending-impact evidence reached 36 prepared cells, changed to `Fractured`,
  activated 28 local pieces and cleared pending state; stable platform velocity was
  `0`.
- Fresh 1920×1080 frames: `BuildReports/Mvp01AcceptedGold.png`,
  `-two-platforms.png`, `-armor-staged.png` and `-dual-ko.png`.
- Final Unity gameplay and focused test runs contain no compiler, shader or
  gameplay exceptions. The kinematic-body armor spam (17,019 linear plus 17,019
  angular warnings in the failing trace) is eliminated; batch licensing messages
  are external to the project gate.
- Wall blocks, platforms, launch pillars, surf boards, wave stones and armor use
  explicit face/bevel geometry and vertex classes. The authored bevel silhouette
  and top/bottom edge light are retained. Only near-vertical internal facet seams
  suppress bevel light and received self-shadow, removing the false vertical
  stripes without flattening the rocks.
- Amphitheatre slabs and landmark stones are projected slightly into the spherical
  surface at authoring time, so they read as weight-bearing stone rather than
  hovering props. The blue rival tint is reapplied through ragdoll, fade and
  respawn property-block transitions.
- Full repository-wide EditMode/PlayMode regression suites were not rerun in this
  focused rebuild; the expanded M11-focused gates are green.

## Follow-up rescue evidence — 2026-08-26

- Dual-mouse input now preserves the original press point for ordinary wall and
  platform drawing. The quick chord performs a 0.28 s rise, 0.25 s readable hover,
  boxer-punch release and straight crosshair launch from a prewarmed typed pool.
  Held upward strokes use the stabilized peak gesture and build an overlapping
  nearest-to-farthest pillar crest.
- Runtime extraction restores its commit subscription after scene load. The
  end-to-end pull/flick gate proves that a committed edit exposes one held fragment
  instead of leaving only a terrain hole.
- High-speed cushion landings suppress fall ragdoll/KO. Surf and wave still force
  visible ragdoll, with vertical launch capped to an approximately 3.8 m rise.
- All stone sources apply a nearest-bone localized reaction first. Three distinct
  impacts concentrated within 0.72 s and 0.72 m escalate to full ragdoll/KO.
  Controlled and released armor stones use the same rule against a non-owner.
- The rebuilt main scene contains 32 anchored, destructible and grabbable authored
  decor rocks. They detach and shatter through the existing pooled debris path;
  wave, surf and swept stone contacts can damage them.
- Focused EditMode: `65/65` passed in `0.396 s`.
  Report: `BuildReports/Mvp01FocusedEdit.json`.
- Focused PlayMode: `8/8` passed in `24.534 s`, including punch timing/aim without
  pool growth, committed pull/flick, cushion landing, local stone clustering and
  capped surf/wave KO.
  Report: `BuildReports/Mvp01FocusedPlay.json`.
- Accepted rescue scenario: `BuildReports/Mvp01RescueCurrent.json` reports success.
  The rebuilt scene has zero missing scripts and the final Console has zero errors
  and warnings.
- Fresh 720-frame profile: total-frame p95 `10.8053 ms`, CPU p95 `10.9746 ms`, GPU
  p95 `1.87904 ms`, `AcquireSolid` peak `2.3774 ms`, fracture-preparation peak
  `0.4750 ms`. Report: `BuildReports/Mvp01Profiler.json`.
- Fresh 1920×1080 evidence is `BuildReports/Mvp01RescueCurrent.png`,
  `-two-platforms.png`, `-armor-staged.png` and `-dual-ko.png`.

## Armor, gesture and character-physics pass — 2026-08-26

- Armor release/cancel is now an explicit lifecycle independent of router permission.
  Input release, cancel, KO and component disable all end the active session through
  `EndArmor`; defensive collision changes are idempotent and no longer reapply 96
  collider-ignore sets every physics tick.
- Body anchors and the complete plate pool warm before the first cast. Assembly keeps
  compliant flight, while a completed compact shell is evaluated directly from current
  Humanoid bone anchors in `LateUpdate`. Dome and orbit retain the intended spring lag.
- The 80 ms dual-button window replays the complete buffered primary pointer path.
  Open strokes longer than 0.012 viewport always resolve to walls. A platform requires
  a closure of at most 0.025 viewport, at least 0.10 path length and at least 0.0012
  viewport-squared enclosed area.
- Dual-button crests accept a full 360-degree pointer vector. Their overlapping pillars
  are ordered from the endpoint nearest the caster. Stomp stone keeps the authored
  0.28 s extraction and 0.25 s hover, follows the moving chest socket in air/on surf,
  and resolves its straight launch ray from the latest cursor point at contact.
- Every character impact is bounded by the shared response profile: full-ragdoll launch
  is capped to a 2 m ballistic rise and 4 m/s tangent speed, while an individual stone
  can move the character root by at most 0.8 m/s. Stone clusters require three distinct
  source IDs in 0.72 s/0.72 m and at least 5.5 m/s cumulative effective velocity change.
- A large single stone now resolves to `RecoverableKnockdown`, not KO: the visible
  humanoid receives one bounded physical handoff, aligns recovery at its settled pelvis,
  plays the authored Falling-To-Roll recovery, then restores control without incrementing
  the KO counter or entering respawn. Three distinct concentrated stone sources retain
  the existing full KO path.
- Player and rival share the same authored locomotion/landing controller and independent
  `EarthFootContactController`. Jump/fall flight windows disable contact IK; moving roll,
  hard-land brace and get-up use explicit authored contact windows. A final critically
  damped upper-body pass composes acceleration, turn and directional impact without a
  sinusoidal rebound and never writes hips, knees or feet.
- Each accepted character impact also emits one canonical `EarthWorldResponseEvent`.
  Dust, pooled debris, scars, audio and camera consume its shared response ID through the
  existing Earth-impact stream; this presentation fan-out cannot apply gameplay damage.
- Local stone response is now a preallocated multi-bone inertial layer: hit bone 1.0,
  parent 0.55 and torso support 0.25, with bounded attack/recovery and no Animator
  ownership handoff. Full-ragdoll escalation inherits the final animated pose without
  an extra root impulse.
- Hand IK has one explicit state machine and an atomic reset. Cancel, locomotion
  interruption, KO and disable clear rig weight, Animator layer, pose weights and both
  targets in the same frame; wrist rotation weight is limited to 0.45.
- Expanded focused EditMode gate: `99/99` passed in `0.747 s`.
  Report: `BuildReports/Mvp01FocusedEdit.json`.
- Focused PlayMode plus accepted evidence gate: `9/9` passed in `36.558 s`,
  including scene runtime, hover/punch, concentrated local-hit escalation,
  cushioned high landing, armor/KO/respawn capture and the 720-frame profile.
  Report: `BuildReports/Mvp01FocusedPlay.json`.
- Accepted evidence status is `success: true` in
  `BuildReports/Mvp01RescueCurrent.json`. Fresh 1920×1080 captures are
  `Mvp01RescueCurrent.png`, `-two-platforms.png`, `-armor-staged.png` and
  `-dual-ko.png`.
- Fresh 720-frame profile: total-frame p95 `11.3065 ms`, CPU p95 `11.4000 ms`,
  GPU p95 `3.1273 ms`, `AcquireSolid` peak `2.3798 ms` and fracture-preparation
  peak `0.5544 ms`. Report: `BuildReports/Mvp01Profiler.json`.
- Final compile and Unity Console check contain zero errors and zero warnings.

## Primary input regression rescue — 2026-08-26

- Terrain drawing now queries the canonical planet collider directly. Dense arena
  dressing can no longer fill a broad raycast buffer and randomly hide the planet
  from a near or distant wall stroke; curved terrain samples are reprojected onto
  the sphere rather than being dragged across the first tangent plane.
- The complete primary path buffered by the 80 ms dual-button chord advances the
  live Earth state machine during replay. A quick press-drag-release therefore
  commits its wall or closed platform instead of falling through to extraction.
- Loose stones and pluckable structure cells no longer steal LMB on pointer-down.
  Pointer motion owns wall/platform grammar immediately; a stationary hold acquires
  existing matter or starts extraction after the decision window.
- Budgeted extraction keeps its reserved fragment and buffered second-click launch
  alive until the terrain transaction commits. Cancelling an in-flight transaction
  no longer leaves an ownerless hole/fragment pair.
- Crest, wave and surf character impacts use cast-scoped duplicate suppression and
  their bounded launch profiles, preventing sequential pillars from repeatedly
  launching one target.
- Final focused gates: `109/109` EditMode and `12/12` PlayMode. Accepted evidence
  reports success, total-frame p95 is `11.3045 ms`, and the Unity Console contains
  zero errors and zero warnings.

## Broken Crown runtime integration — 2026-08-29

- The imported Broken Crown catalog now drives eight runtime structures and 90
  authored pieces. Intact render/collision proxies remain authoritative until a
  bounded release; dormant fracture renderers are never treated as gameplay state.
- Ordinary impacts release only the nearest one to three cells. Vector/pluck releases
  one selected cell. Counter-clockwise gravity disassembly releases progressively;
  clockwise repair snaps the same provenance-bearing pieces back to their authored
  rests and restores the intact proxy only at complete repair.
- The arena floor retains its meteor-only contract. Ordinary LMB/stone impacts cannot
  erase the court, while a typed meteor impact may release all 36 floor cells.
- Released arena cells implement the shared `IEarthPhysicalTarget` path, so hold,
  vector field, gravity capture, collision damage and radial gravity reuse the same
  bounded gameplay authority as ordinary Earth matter.
- The enlarged world is synchronized at `55.1 m` across the voxel planet, point
  gravity source and planet collision proxy. Both character motors and all 13
  destructible decor rocks are reseated from collider support points; rock support
  is `-0.060 m` from the surface. The floor's hidden foundation thickness remains
  embedded intentionally.
- Feature-specific PlayMode acceptance passes weak-hit rejection, two-cell ordinary
  impact, meteor-only floor protection, one-cell vector pluck, progressive gravity
  disassembly and complete repair. The current aggregate focused snapshot is
  `139/142` EditMode and `10/17` PlayMode; remaining failures are in the independently
  edited MiniBokeh expectations, camera-dependent input visibility, enlarged-world
  movement tuning and cold-start evidence. Unity compilation and Console contain no
  errors or warnings.

## Approved court placement and lens/gravity follow-up — 2026-08-31

- `EarthCoreSlice` now serializes the approved Broken Crown root at
  `(0, 54.12, 0)` and the rival spawn at
  `(-0.26751554, 58, 3.5498571)`. The generator restores both after every rebuild;
  all imported child transforms remain identical to the source FBX.
- The arena shell is intentionally embedded into the planet instead of being
  lifted by a minimum-vertex seating solve, so no empty underside is visible.
- Native DOF stays active during all camera motion states and keeps one padded
  sharp envelope around both fighters. Web remains the explicit off fallback.
- Released wall and platform cells use the shared spherical `GravityBody` path;
  fracture no longer creates zero-gravity debris.

## Scope boundary

MVP 0.1 still has no HP bars, score, victory screen, rounds, waves, loot,
progression, NavMesh, behavior tree or second enemy class. KO is intentionally an
instant physical ring-out loop with automatic respawn. If the slice must shrink,
remove the outer seating ring first; keep the shared humanoid caster, physical KO,
single-material rock grammar and bounded startup pools.

## Rollback

Set `PlanetWorldProfile.radius` back to 24, remove the M11 duel/amphitheatre calls
from `M3EarthCoreSetup`, restore the previous wall profile and eager pool warm-up,
then run `Elemental/Setup/Create M3 Earth Core Slice`. The isolated
`RumbleLookdevLab` remains unchanged.
