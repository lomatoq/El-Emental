# El-Emental project technical state

Updated: 2026-09-05

**Latest verified rescue evidence, 15:28 UTC:** the gameplay camera correction is
active at runtime and saved in `EarthCoreSlice`. Focused camera/animation EditMode
passes **7/7**; the production full-body neutral -> magic -> return PlayMode scenario
passes **1/1** at 15:18:50 UTC. This is scoped pose/camera evidence. Final airborne
mantle acceptance and complete armor coverage remain pending.

The isolated SONIC experiment now has an accepted production-actor preview at
15:23:30 UTC in
`BuildReports/SonicPrototype/ProductionActorPreview/20260905-152326-358`:
**252 retargeted frames in 2.554 s**, four rolling plans, hand ranges **0.302/0.282 m**,
zero root drift and preserved production foot ownership. The captured PNG was
visually inspected. SONIC remains opt-in experimental code and does not own poses
in the production scene.

The outer-ring interior palette mismatch is corrected. `OuterArch_06` exposes
**4.3884 m²** of cut surface, which made the stale light interior material especially
visible; all ring structures now use the arena exterior/interior palette and the
dedicated lit fracture-dust response is restored. Fresh `OuterStoneRingPlay` passes
**8/8** at 15:28:20 UTC. Charged Surf+Space physical input and ordinary-pillar
regression pass **2/2 PlayMode** at 14:31 UTC. Sky acceptance includes nine reviewed
captures and day/night **3/3 PlayMode**.

The latest Player run accepts all **1939** baked fracture plans with **0 cache
misses** and a ready planet. The strict first-cover visual gate still awaits the
fresh final build after the cover presentation adjustment. This evidence does not
establish a cold-disk benchmark or prove that every startup stall is removed.

**Historical 13:31 UTC snapshot, superseded by the evidence above:** ordinary armor/jump/centered-aim corrections pass 5 Edit
and 1 physical Play test plus a four-frame visual proof with a 19-frame jump
audit. Armor has a separate encumbrance multiplier (~83% to ~75% movement speed)
and retains ordinary pose/mantle ownership. Semantic magic regression **8/8**
rerun at 13:20. Final horizon/Moon/system-planet matrix **9/9**, pure atmosphere
envelope **3/3**; see DAY_NIGHT_RESCUE. Player cache serialization omission has
been reproduced and fixed by clearing persistent-mesh DontSave flags: tiny
standalone-target bundle now preserves all three control meshes (3/3), and
new cache revision `867eb2621fa1419990940e4b51fd43cf` contains 1939 plans.
Full fresh-Player validation remains pending. Surf+Space is being refined per
the latest user request into a hold/release charged long jump along the tilted
pillar itself. SONIC remains an opt-in experiment, not a production pose owner.

**Current verification, 11:30 UTC:** see the [execution tracker](PROJECT_EXECUTION_TRACKER.md)
for fresh gate results. Semantic magic is **21/21 Edit + 8/8 Play**, dual-mouse
30/60/120 Hz **3/3**, C1 angular velocity **6/6**, ground-wave physical commit **1/1**.
The prior 176-degree arm flip and paced-clip early timeout are fixed. All-eleven
visual capture now completes **36/36** with head pitch within -21.46/+28.00 degrees.
Player compilation succeeds after correcting Editor-only gizmo guards in the
vendored motion packages; gameplay feature decoding is unchanged.

**Current remaining verification:** airborne mantle behavior, complete armor
coverage and the strict first-cover visual result from one fresh final Player build.
The earlier 26-miss Player snapshot and unaccepted SONIC preview are superseded by
the 1939/0-miss Player result and the scoped accepted preview above. See
[startup evidence](STARTUP_CACHE_RESCUE.md). No cold-OS-cache claim is made, and
the startup rescue is not declared complete until the first-cover gate passes.

**Sky:** day/night PlayMode **3/3** and nine accepted production-camera captures
show readable dark silhouettes, warm pink/orange dusk, changing solar shadows and
irregular stars. A runtime shadowless moon fill uses the existing profile (0.8); Rumble
rocks now receive URP additional diffuse lights. Main solar shadows remain owned
by the Sun. [Visual evidence and scope](DAY_NIGHT_RESCUE.md).

**Dust / seismic vision, 11:00 UTC:** physical dust now uses real light and SH.
The focused GPU Play suite passes **2/2** with day/night delta **0.39619595**,
night visibility **0.1160493** and neutral-reference footprint error **0.001231**.
An actual production Game-camera capture holds one exact particle layout across
Day/Dusk/Night; all three images were inspected and night dust is visibly dimmer.
This is common-material evidence, not all-technique visual acceptance. Local earth
vision composes monochrome expanding waves over opaque scene depth: **1/1**
production ground/launch/resume passes, while the temporal GPU filter passes
**5/5** at 30/60/120 Hz and leaves inactive day/night pixels byte-exact. This is
not an all-transparent-material or through-wall perception contract.

**Historical animation failures, superseded by the 15:18 evidence above:** magic Edit **11/12** (09:30 UTC)
has an outdated fixed-time clock expectation under slower clip limits; controlled
FPS **0/3** (09:32 UTC) exposes delayed release tails plus one router failure;
held aim **0/1** (09:34 UTC) records an actual 167-degree arm/hand flip at low IK
weight. Those failures were not accepted at the time. The corrected saved runtime
now passes the scoped **7/7 Edit + 1/1 full-body Play** gate; this does not close the
separate airborne-mantle or complete armor-coverage acceptance.

**Outer stone ring, 2026-09-04 (dirty `d2174ed`):** seven artist-edited columns
are imported as independent content and placed around the existing arena with
2.5 m final clearance and buried foundation caps. Existing arena materials and
fracture/grip/repair runtime are reused: **85 structural cells + 8 loose stones**.
Focused **2/2 EditMode + fresh 8/8 PlayMode passed** (latest UTC 15:28:20 on
September 5); all cells complete grab/repair cycles, unsupported islands release,
foundation-connected cells remain seated, fast post-separation impacts work, and
the corrected `OuterArch_06` cut surface uses the arena interior palette. Its
measured cut area is **4.3884 m²**. Dedicated fracture dust is restored through the
lit physical-dust path. Original scene geometry and gameplay settings are retained.
[Pipeline, scope and evidence](OUTER_STONE_RING.md). Wider M11 acceptance unchanged.

Current branch: `codex/environment-aware-motion-matching-spike`

Current implementation: dirty working tree on `d2174eded114dd022e4a9c442abadda7a0e44555`.

Historical foundation evidence below belongs to `8c2e6245a0e4fe1e169b63998e918ce800477b36`,
not the current dirty tree. Current scoped evidence is tracked separately.

## Earth material pass — September 4

**MMB held field restored (dirty `d2174ed`, UTC 14:14):** live user input exposed
stale armor ownership after release; frame consumption no longer relatches the
armor flag. The user clarified that MMB is an area field, superseding the earlier
single-target assumption. It collects nearby loose stones and new arrivals inside
the existing radius, capped at 48, and packs them by physical contact at a common
center without camera-side slot gaps. **9/9 Edit + 9/9 focused Play passed**,
including three arena cells after armor, repeated releases, new arrivals, contact
packing and resting-stone wake. Scene/profile settings retained.
[Live evidence and contract changes](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Raw MMB follow-up (dirty `d2174ed`, UTC 13:31):** added Input System middle-button
press/hold/pointer-move/release/repress coverage through the shipping adapter and
router. Focused PlayMode **8/8 passed**. This is verification, not another gameplay
fix: the user's remaining SКМ concern needs a matching reproduction, and is not
declared universally resolved. [Details](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Arena cursor/grip follow-up (dirty `d2174ed`, UTC 13:25):** user playtesting exposed
another acquisition failure after the earlier collider-direct tests passed. The
MMB caller incorrectly admitted Surface-only protected arena floor, masking loose
cells; the executor then reported success with no captured stone. Removed Surface
from that caller's capability mask and added pure session admission plus truthful
stone/structure/failure feedback. **4/4 Edit + 7/7 focused Play passed**, now through
screen-point picking against shipping floor/cell geometry, with lift, repeated
reacquisition and intact-column circle disassembly/repair. First press 2.4525 ms /
0 managed bytes in Editor. [Evidence and wider-test limitation](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Loose stones and oblique fracture (dirty `d2174ed`, UTC 12:48):** fixed gravity
target rejection inherited from a camera-hidden arena parent, perpetual waking
under radial gravity, and duplicate wall/platform fragment gravity. Quiet supported
stones sleep and wake on grip/support removal. Fracture uses angled cuts and broad
contained bevels; exact primary detachment is retained. **12/12 Edit + 5/5 loose
stone Play + 3/3 production fracture Play passed**. Thin recursive fill: 97.62%
collision / 89.03% visible; cached split max 0.2482 ms / 0 managed bytes in Editor.
User wave retuning is preserved (rise .4, settle .07994324, hold 0, retreat .2398297,
speed 8.54), SHA256 `CDF2D3C2B546204AE088EAAFD1E4156B51B2DAC4947BCF0FAF436FA30B82BE1F`.
[Rest/grab evidence](LOOSE_STONE_FIX.md), [fracture evidence](CONTAINED_FRACTURE_FIX.md).

**Overlapping wave + fitted head armor (dirty `d2174ed`, UTC 12:21):** supersedes
the propagation constraint below. Delay is strictly distance/speed; rows never
wait for another row's retreat. The Inspector now exposes **Длина фазы волны, м**
by proportionally editing the visible rise/settle/hold/retreat seconds. The saved
wave asset is unchanged (SHA256 `AA430FC5F0629920B549CD00E2F0EA8537A6F8A02ADD931C3FF2EA180EBF7FAD`).
Head plates fit 2,093 skinned vertices instead of the neck-radius estimate and
follow the final animated Head pose. Sixteen extra small seam stones join the
normal orbit/projectile pool (96 base + 16 fillers, capacity 112); body settings
and original anchor indices remain unchanged.

Fresh **23/23 EditMode** (`WaveContactEdit.json`, UTC 12:17), **5/5 PlayMode**
(`WaveHeadArmorPlay.json`, UTC 12:20:58): 93 simultaneously rising cells at the
authored 6.04 m/s, 76,822 pair checks with zero penetration, 1,246,647 projected
vertex checks with zero drift. Head coverage passes five exterior render-mesh
directions; all 16 fillers orbit and launch. Head follow error < .000004 m;
compact-follow marker .4998 ms, wave-contact marker max 5.8477 ms in this heavy
Editor audit. These are scoped Editor measurements, not shipping performance.
[Wave contract](WAVE_CONTACT_FIX.md), [head contract and known wider-suite failures](HEAD_ARMOR_FIX.md).

**Wave placement/propagation correction (dirty `d2174ed`, UTC 11:43):** immutable
meshes alone missed lateral snapping when the lowest support vertex changed.
Wave seating now changes height only, uses one cast tangent frame and contains
render bevels within each cell. Whole-cast reservation prevents partial topology
replacement; earlier anchored columns block overlapping new casts. The crest now
travels through all rows and leaves earlier rows retreating. Long authored phases
reduce actual speed (the slider is a maximum); user phase curves/seconds are unchanged.
Shared sand dust uses URP Particles/Unlit with its saved tint and soft alpha blending.
Fresh **21/21 EditMode**, **2/2 PlayMode**: 21,646 pair checks with zero penetration,
1,249,221 projected-vertex checks with zero drift, three advancing crest samples,
703 descending samples and zero dense-dust lighting delta. Contact marker max
1.9917 ms in Editor. [Evidence and prior failed baselines](WAVE_CONTACT_FIX.md).

**Filled fracture and stable-contact follow-up:** actual convex partitions replace
inscribed generic stone templates. Fresh thin/rotated/recursive measurements retain
**97.62% collision volume / 96.39% visible volume**, with exact arena detachment
preserved. Four cached splits cost at most **0.248 ms / 0 managed bytes** in the
fixture; cold preparation is measured separately. `ContainedFractureEdit` **8/8**
(UTC `10:10:37`), `ContainedFracturePlay` **3/3** (`10:17:57`).

Wave casts no longer reuse live cells, and their horizontal fracture outline stays
constant through the lifted depth. Contact-plane dust/chip streams and emergence/
retreat bursts follow each cell; terrain extraction is substantially denser. Small
wall collider hulls follow visible stones within PhysX limits, and gravity packing
uses actual geometric size instead of mass. The saved user profile now includes
3-second rise/retreat, speed 6.04 and push 115, all preserved. Latest production
`WaveContactPlay` **4/4** (`10:22:37`): 2,784 immutable mesh checks, 93 contact sources,
5,037 contact cues / 379 bursts, 40 matching wall hulls, contact error <2 micrometres.
Contact marker maximum **5.9642 ms** in the Editor is a scoped observation, not a
whole-frame certification. See [current details and evidence](WAVE_CONTACT_FIX.md)
and [convex fill](CONTAINED_FRACTURE_FIX.md). Earlier fitting results below are historical.

**Wave and contained-fracture repair (same dirty `d2174ed`):** arena detachment
retains the exact baked mesh/collider/material. Secondary stones fit inside their
actual parent convex, including rotated thin shapes and recursive splits; wall
stone visuals also use convex planes. The simplified wave Inspector exposes six
curves, five phase durations and five main controls. Bounded wave cells and restored
unit crest meshes remove the oversized geometry caused by sparse Voronoi territory
and pool reuse. User scene/profile settings were preserved.

Fresh scoped evidence: **24/24 wave EditMode** at UTC `09:26:48`, **1/1 wave
PlayMode** at `09:28:29`, **4/4 containment EditMode** at `09:25:52`, and **3/3
containment PlayMode** at `09:32:31`. Six repeated waves/crests produced 5,857
visible samples, maximum polygon span **2.133 m**. Four measured physical splits
cost at most **0.6496 ms**, **0 managed bytes** in the test scope. Wave launch
preparation still peaked at **168.865 ms** in the Editor; startup frame-time
performance remains a risk, not certified by the geometry checks.
See [wave repair](WAVE_REPAIR.md) and [contained fracture](CONTAINED_FRACTURE_FIX.md).

**Wall material correction:** fixed an excess `RumbleClay` renderer slot that
survived the natural-stone override. Natural wall chunks now have exactly one
sandstone material; saved interior reference and setup defaults match. The earlier
slot-zero assertion did not detect this bug; regression now checks all slots.

**Combat/mobility follow-up:** saved the user's latest scene/profile state before
editing (`BuildReports/CombatMobilityFixes/Before/`). Armor now routes structural
damage; walls, columns and platforms accumulate weak hits. Released arena pieces
split through the persistent debris/matter path on impacts. Natural stone render
variants, seeded shard rotation, denser surf/pillar feedback, cushion roll
suppression and smooth tunable wave phases are integrated. See
[settings and focused verification](COMBAT_MOBILITY_FIXES.md).
Final focused checks pass **9/9 EditMode + 4/4 PlayMode** (UTC `08:49:26`).

**Dust/shadow follow-up (same dirty `d2174ed` tree):** saved the user's Soft Sun
shadows at strength `0.554` and matched the scene-builder default. Cosmetic shards
now draw before dust without writing scene depth; physical stone still occludes
effects. Focused authoring tests **5/5** and pixel/production PlayMode tests **2/2**
pass at UTC `2026-09-04T07:37:29Z`. Settings, captures, profiler observations and
scope are in the [material-pass checklist](EARTH_MATERIAL_PASS_CHECKLIST.md#dust-and-shard-authoring-follow-up--september-4).

Implemented the typed spatial material-feedback path, material-authoritative dust RGB,
signed backward locomotion, 44 m/s armor, persistent mass-preserving size-class breakup,
cached render-only bevels, irregular surf stones, physical gravity release, restored
shadows and seeded whole-planet scatter. The narrow integration menu preserves scene
transforms, user light/material values, authored motion choices and camera/DOF.

The EAMM bake had mismatched direct-rest and collapsed-parent animation frames.
Schema 2 now records rest and sampled poses in the same collapsed-parent frame;
the invalid-pose guard remains enabled. A fresh production run reports Active.
Physical split children keep canonical matter identities and remain targetable beyond
cosmetic lifetime; pool exhaustion retains the parent instead of losing matter.

See [per-item contracts, settings, reports and remaining gates](EARTH_MATERIAL_PASS_CHECKLIST.md).
These focused tests do not supersede historical whole-project failures or establish
a 720-frame performance/zero-GC certification.

Companion tracker: [`Docs/PROJECT_EXECUTION_TRACKER.md`](PROJECT_EXECUTION_TRACKER.md)

## Purpose and truth protocol

This is the canonical short technical entry point for gameplay work. It explains what the
project is, where authority lives, how the layers connect, and which technical claims are
actually supported. It does not replace the detailed architecture, milestone, ADR, or raw
evidence files.

Use these labels when updating this file:

- **Fact** — visible in source, serialized data, an accepted ADR, or a named report.
- **Measured** — includes the exact report, conditions, and important caveats.
- **Decision** — accepted design/architecture intent; it may still lack implementation evidence.
- **Unknown** — must not be silently promoted to complete.

Freshness rule: after a material public-contract, package, scene-generator, performance, or
authority change, update only the affected sections and set the header to the tested branch and
commit. A report predating that commit is historical evidence, not validation of the new commit.
The live gate and risk state belongs in the [execution tracker](PROJECT_EXECUTION_TRACKER.md).

## Canonical source order

When sources disagree, prefer the narrowest current evidence in this order:

1. Current source/serialized assets at the tested implementation commit.
2. Fresh test/build/profiler report produced from that same commit and worktree.
3. Accepted ADR for architectural intent.
4. Current milestone for product scope and acceptance criteria.
5. Older milestone, README, or `.solo-studio` narrative for history only.

Start with this file and the tracker, then follow links instead of rereading the repository:

- [`Docs/architecture.md`](architecture.md) — stable layering, clocks, terrain authority, errors.
- [`Docs/milestones/M11-earth-mvp-0.1-rumble-duel.md`](milestones/M11-earth-mvp-0.1-rumble-duel.md)
  — current slice and detailed history.
- [`Docs/adr/ADR-0001-engine-stack.md`](adr/ADR-0001-engine-stack.md) — engine baseline.
- [`Docs/adr/0014-fracture-and-reassembly-v2.md`](adr/0014-fracture-and-reassembly-v2.md) — structural truth.
- [`Docs/adr/0021-semantic-earth-intents-and-shared-surfaces.md`](adr/0021-semantic-earth-intents-and-shared-surfaces.md)
  — input and surfaces.
- [`Docs/adr/0026-earth-core-v4-matter-grammar-and-choreography.md`](adr/0026-earth-core-v4-matter-grammar-and-choreography.md)
  — target matter/technique chain.
- [`Docs/adr/0029-visible-humanoid-ragdoll-and-deferred-platform-fracture.md`](adr/0029-visible-humanoid-ragdoll-and-deferred-platform-fracture.md)
  and [`Docs/adr/0030-mvp-01-earth-input-impact-and-matter-follow-up.md`](adr/0030-mvp-01-earth-input-impact-and-matter-follow-up.md)
  — current MVP runtime handoffs.
- [`Docs/adr/0031-broken-crown-arena-and-meteor-floor.md`](adr/0031-broken-crown-arena-and-meteor-floor.md)
  — arena authoring and damage contract.
- [`Docs/adr/0032-performance-aware-atmosphere-and-focus.md`](adr/0032-performance-aware-atmosphere-and-focus.md)
  — rendering policy.
- [`Docs/adr/0033-animation-contact-and-arena-rendering-rehabilitation.md`](adr/0033-animation-contact-and-arena-rendering-rehabilitation.md)
  — proposed animation/contact acceptance gate.

## Product and golden path

**Fact:** El-Emental is a Unity physics-action prototype about manipulating matter on small,
destructible planets. The non-negotiable Earth promise is readable, heavy, spatial magic:
shape or pull stone, move it under local spherical gravity, and make physical impact,
fracture, repair, animation, camera, and feedback agree about the result. The fuller product
charter is in [`.solo-studio/PROJECT_CHARTER.md`](../.solo-studio/PROJECT_CHARTER.md).

**Current golden-path scene:**
`Assets/Elemental/Content/Scenes/EarthCoreSlice.unity`.

**Rebuild authority:** `Elemental.Authoring.Editor.M3EarthCoreSetup.Configure()` in
`Assets/Elemental/Authoring/Editor/M3EarthCoreSetup.cs`, exposed as
`Elemental/Setup/Create M3 Earth Core Slice`. The scene is generated; change profiles,
source assets, importer/integrator code, or the setup code, then rebuild. Do not treat a
hand-edited generated scene as durable source.

**Current representative loop:**

1. Launch the 55.1 m-radius Earth world and move a Humanoid on local spherical gravity.
2. Draw/raise Earth structures or acquire physical matter through the routed mouse grammar.
3. Move, launch, fracture, disassemble, or repair provenance-bearing matter.
4. Fight the deterministic rival in Broken Crown; impacts produce localized response,
   recoverable knockdown, or bounded ragdoll/KO depending on the accepted hit contract.
5. Recover/respawn and replay without growing the prewarmed pools or losing terrain/matter truth.

The exact `55.1` profile value is serialized in
`Assets/Elemental/Content/Profiles/PlanetWorldProfile.asset`. M11 still contains older
radius-36 performance/gate prose; treat those passages as historical until the milestone is
reconciled. MVP 0.1 intentionally excludes HP bars, score, rounds, victory UI, progression,
NavMesh, behavior trees, and a second enemy class.

## Engine and package baseline

**Fact at the snapshot commit:**

- Unity `6000.5.7f1` (`ProjectSettings/ProjectVersion.txt`).
- URP `17.5.0`, Forward renderer; built-in PhysX plus custom local gravity.
- Input System `1.20.0`, Cinemachine `3.1.7`, Animation Rigging `1.4.1`.
- Burst `1.8.30`, Collections `6.5.0`, Mathematics `1.4.0`.
- Unity Test Framework `1.7.0`.
- MiniBokeh is a Git dependency pinned to commit
  `faa491907b7a580ef2ddfebfbef4590d1d3c6628`; the project-local dual-subject DOF path is
  the current NativeHigh owner and MiniBokeh is disabled in the latest profiler audit.
- VFX Graph `17.5.0`, AI Assistant `2.18.0-pre.2`, and AI Inference `2.6.1` are present in
  `Packages/manifest.json`. Their presence is not evidence that they own canonical gameplay.

Native Windows/macOS are the engine-stack priority. WebGL2 is a reduced `WebLab` capability
profile. A production online transport is not selected.

## Layer map and dependency direction

```text
device input
  -> normalized frame / semantic route
  -> command or bounded runtime intent
  -> pure simulation policy + canonical state
  -> Unity runtime adapters / PhysX / scene lifecycle
  -> typed events, snapshots, read-only state
  -> camera / animation / VFX / audio / UI / diagnostics

authored profiles + imported/baked assets
  -> validators and scene generator
  -> copied/baked runtime data (never mutable authoring state as gameplay truth)
```

| Layer | Owns | Important entry points | Dependency rule |
|---|---|---|---|
| `Elemental.Core` | Stable IDs, deterministic helpers, simulation tick primitives | `Assets/Elemental/Core/` | Assembly has `noEngineReferences: true`; no Unity object lifecycle. |
| `Elemental.Simulation` | Gravity, voxel/SDF state, magic/combat policies, matter/provenance, support, fracture/reassembly, networking contracts | `GravityWorld`, `VoxelPlanetState`, `EarthActionRouter`, `EarthMatterRegistry`, `EarthMvpBotPlanner`, `EarthDuelRespawnSolver` | Source scan at this commit finds no UnityEngine dependency, but the asmdef does **not** set `noEngineReferences: true`; discipline/tests currently enforce the boundary rather than assembly metadata. |
| `Elemental.Input` | Input System device boundary, viewport-normalized gesture sampling, semantic routing | `EarthInputAdapter`, `EarthActionRouterBehaviour`, gesture pipeline under `Assets/Elemental/Input/Gestures/` | Only `EarthInputAdapter` may read physical actions/devices. Consumers receive normalized data or resolved intent. |
| `Elemental.Runtime` | Scene/session lifecycle, PhysX handles, pools, world queries, command execution, fighter/terrain adapters | `MagicExecutor`, `VoxelPlanetBehaviour`, `PlanetMotor`, `EarthSurfaceQueryService`, `EarthArenaStructure`, `EarthMvpDuelController` | Converts pure contracts to Unity operations; must not let Rigidbody, collider, renderer, or GameObject state become canonical truth. |
| `Elemental.Presentation` | Camera, Humanoid/IK/secondary motion, URP, VFX, audio, UI, capture/telemetry | `EarthCameraDirector`, `EarthFootContactController`, `EarthCinematicDepthOfFieldController`, `CelestialSystemBehaviour`, `EarthPerformanceTelemetry` | Reads state/events. Animation/VFX completion may not decide damage, KO, terrain, matter, or repair. |
| `Elemental.Authoring` | Profiles, ScriptableObjects, validators, importers, bakers, editor menus, generated scenes | `M3EarthCoreSetup`, `BrokenCrownArenaImporter`, `EarthFractureBaker`, `ElementalProjectValidator` | Authoring data is validated, then copied/baked into bounded runtime state. |
| Tests/tooling | Pure unit tests, scene/runtime tests, build and evidence runners | `Assets/Elemental/Tests/`, `Scripts/Test-Unity.ps1`, `Mvp01FocusedTestLauncher`, `Mvp01EvidenceRunner` | Test duration is regression evidence, not a frame-time measurement. Reports must identify commit and conditions to validate current code. |

The assembly references currently enforce the broad direction
`Core <- Simulation <- Runtime`, with Input, Presentation, and Authoring depending on the
runtime/domain layers as needed. Presentation also references Input for camera/interaction
context; this is acceptable only while presentation remains non-authoritative.

## State ownership and data flow

### Input to Earth action

`EarthInputAdapter` reads Unity Input System actions and emits normalized viewport/device
state. `EarthActionRouterBehaviour` buffers the short dual-button decision window and feeds the
pure `EarthActionRouter`; the resolved owner/phase decides whether primary magic, force,
vector field, gravity, armor, resonance, surf, or movement consumes the input. Gesture code
resolves stable surface/target handles before runtime execution. `MagicExecutor` and dedicated
bounded controllers adapt accepted intent to the world. Replays store resolved commands, not
raw pixels.

The non-shipping Rumble lookdev shortcuts also route through optional actions on
`EarthInputAdapter`; presentation components no longer read `Keyboard.current` directly. The
physical-device boundary source scan is green in the 2026-08-31 full EditMode run.

Primary risk: input, camera ray projection, dense arena colliders, and delayed chord replay meet
at this seam. This is why physical-device PlayMode tests are acceptance gates rather than an
optional unit-test supplement.

### Terrain and matter

`VoxelPlanetState` owns the analytic base SDF plus ordered `SdfEdit` history.
`VoxelPlanetBehaviour` owns Unity-side chunk/render/collider queues; meshes and colliders are
caches. A terrain extraction remains transactional until `EditCommitted`; only then may the
reserved fragment become visible/held. `EarthMatterRegistry` owns stable matter identity and
provenance across terrain, fragment, structure, armor, and return transitions.
`EarthMatterMassPolicy` plus `EarthMatterMassRuntime` convert authored/collider scale to shared
gameplay mass for the runtime sources that are wired to it.

### Structures, fracture, and repair

`EarthFractureAsset` and `EarthArenaFractureCatalog` contain validated authoring topology.
Pure bond damage, island, ordering, and pose solvers own canonical structural decisions.
`EarthWall`, `EarthPlatform`, and `EarthArenaStructure` own bounded Unity representations and
proxy switching. Stable structure/piece/bond IDs survive pooling. The Broken Crown floor is
ordinary-damage immune and may swap to its 36-piece representation only for typed meteor
impact; walls, columns, gate, and loose rocks use their narrower contracts.

Platform casting prewarms six roots and up to 48 shells per root. The solid walkable collider
is immediate; fracture topology/preparation is deferred one cell per rendered frame, and early
impact is retained until `FractureReady`.

### Characters, support, impact, and duel

`PlanetMotor` owns canonical movement/grounding. Presentation contact IK must follow the motor
and stable support handles; it may not move gameplay truth. At the tested implementation commit,
`CharacterSupportRuntimeAdapter` classifies bounded non-alloc SphereCast/Raycast candidates for
the pure `CharacterSupportAuthority`, and `PlanetMotor` consumes the selected support with a
small retention bias. Arena and voxel surface providers expose explicit support classification;
released pieces and dynamic debris are rejected. The targeted runtime debris/support regression
passes in `BuildReports/FoundationWorkingTreePlay-20260831.xml`.

The Linebreaker runtime FBX now retains the original 48-bone Humanoid hierarchy and adds only
`Secondary_HelmetAnchor` and `Secondary_HairLock`. The authored weighted source distributes the
plume across the three tail bones plus the hair lock and weights both two-bone belt chains. The
generated shipping scene was rebuilt through `M3EarthCoreSetup.Configure()` and the targeted
scene test confirms that player and rival configure helmet/hair, tail, and both belts before
scene unload.

`EarthMvpBotPlanner` owns deterministic rival phases. `EarthMvpBotController` adapts them to the
motor and pooled projectile. Impact solvers decide local reaction, recoverable knockdown, or KO.
`HumanoidRagdollRig` owns the visible 11-body physics handoff, while
`EarthMvpDuelController` owns KO timing, fade, reset, and respawn. Animator and PhysX must never
own the same bones simultaneously.

The current dirty working tree also contains the feature-flagged EAMM production animation pass.
`EarthAnimationGraph` is the only Animator/EAMM pose-composition graph and
`EarthInertializationJob` owns cached per-bone rotational continuity while excluding gameplay
root translation and planted feet/toes. The authored base controller is a 2D tangent-space
locomotion tree; front/back recovery, bounded slope response and bounded magic reach remain
explicit action/presentation lanes. This implementation is not accepted evidence: PlayMode,
30/60/120 capture, visual A/B and zero-GC profiling were intentionally not run in that pass.

### Presentation and lighting

Typed events fan out through presentation adapters to camera, dust, debris, scars, audio, and
animation. `EarthWorldResponseEvent` identifies one accepted gameplay outcome; presentation
must not reapply it. `EarthCameraDirector` owns semantic composition requests, not targeting or
damage. `CelestialSystemBehaviour` uses `CelestialLightingClockPolicy`: gameplay defaults to a
locked authored key/ambient state, with animated ephemeris reserved for explicit QA/lookdev.
URP atmosphere, DOF, SSAO, shadows, and motes are degradable presentation paths.

### Persistence and replay

`VoxelSaveCodec` is a version-2 binary codec for voxel base parameters and ordered edits and can
read version 1. `MagicReplayRecorder` is an in-memory, tick-ordered command list. Tests exercise
both, but no runtime code calls `VoxelSaveCodec.Write/Read`; atomic disk save, backup/recovery,
whole-session schema, cloud conflict, and migration fixtures beyond voxel v1 are therefore
**not shipping-complete**.

### Networking

`Elemental.Simulation.Networking` defines transport-independent commands, authority decisions,
terrain replication, snapshots, relevance, correction, and a deterministic
`SimulatedTransport<T>`. ADR 0003 accepts this for the M8 spike only. There is no selected or
wired shipping socket/relay transport, and the M11 golden path is local.

## Current strengths

- Explicit typed IDs, commands, events, provenance, and bounded pure solvers give most important
  state a named owner and a unit-test seam.
- Terrain and structure representation separate canonical state from meshes, colliders, VFX,
  and pooled views.
- The project has wide automated coverage: 129 C# test files and 590 `[Test]`/`[UnityTest]`
  annotations at this snapshot, plus focused physical-input, scene, animation, and profiler gates.
- Hot paths use fixed buffers/non-alloc physics queries and named profiler markers; capability
  profiles make many hard capacities explicit.
- Generated-scene, Blender, import, fracture, animation, material, and project validators turn
  repeated solo-production work into reproducible tooling.
- Experimental rendering and online work have explicit fallback/removal seams rather than owning
  gameplay truth.

## Weaknesses and technical debt

- The tested implementation has no complete green acceptance run. A fresh full EditMode run passes
  `586/587`; its sole failure is the zero-warning build-evidence gate because the existing Native
  Windows report records 186 warnings. The two new foundation PlayMode regressions pass `2/2`,
  but the complete focused/broad PlayMode suites remain pending. See the tracker for exact scope.
- `M11-earth-mvp-0.1-rumble-duel.md` mixes accepted claims, radius-36 history, radius-55.1 state,
  and a previously partial `139/142` + `10/17` snapshot. Status cannot be inferred from its title.
- `M3EarthCoreSetup.cs` (~3,506 lines), `MagicExecutor.cs` (~2,151 lines), and the generated
  `EarthCoreSlice.unity` (~175,029 lines) are large integration surfaces. They accelerate one-click
  reconstruction but amplify merge risk, hidden coupling, and broad regression blast radius.
- The Simulation source is currently Unity-free, but its asmdef does not enforce
  `noEngineReferences`; one accidental Unity reference could erode the domain boundary.
- `BuildReports` contains tracked baselines plus overwritten/untracked working evidence. A
  filename containing `Latest` or `Accepted` is not proof unless its timestamp/commit and result
  match the claim.
- Some newest foundation policies are not equally integrated. Shared mass is used by arena
  pieces/decor, celestial clock policy is wired, and classified support now feeds `PlanetMotor`;
  full focused PlayMode and profiler evidence for their combined scene remains incomplete.
- Shipping persistence, production networking, controller/accessibility coverage, external-player
  comprehension, and release operations remain incomplete or unproven.

## Technology trade-offs and limits

| Choice | Benefit | Limit / required fallback |
|---|---|---|
| Unity 6 + URP + PhysX | Mature editor, authoring, Humanoid, rendering, and rigidbody integration | PhysX outcomes are tolerance-deterministic, not bitwise cross-platform replay; preserve commands/provenance and reconcile instead of serializing scene objects. |
| Analytic SDF + ordered edits | Compact authoritative terrain and replayable changes | Meshing/collision are asynchronous caches; startup/edit queues can hitch if budgets or stale-result rejection regress. |
| Authored 3D prefracture | Stable topology, art direction, provenance, predictable limits | Requires Blender/import validation and many precreated shells; active Rigidbody/piece budgets remain hard limits. |
| Fixed arrays and prewarmed pools | Low-GC, bounded behavior | Capacity exhaustion must reject loudly or degrade by profile; it cannot silently drop canonical matter/impact. |
| Animator + procedural IK + visible ragdoll | Readable authored motion with physical KO | Update-clock and ownership handoffs are fragile; motor, Animator, IK, and PhysX need explicit interruption/reset tests. |
| One-pass screen-space atmosphere and semantic DOF | Predictable cost and readable capability fallbacks | Not physical volumetrics; target-device GPU evidence and dual-subject/camera-motion validation remain necessary. |
| Git MiniBokeh and prerelease/AI packages | Fast access to specialized tooling/experiments | Pin, license, platform, build-size, and maintenance risk must be reviewed; none may become an unwrapped gameplay dependency. |
| Transport-independent network spike | Tests authority payloads without early package lock-in | Not a shipping online implementation; disconnect, relay, NAT, security, service failure, and real multi-machine performance are unknown. |
| Voxel codec + in-memory replay | Demonstrates versioned terrain and ordered command contracts | Does not yet provide atomic full-game saves, backup/recovery, durable replay files, or cloud conflict policy. |

## Performance and test evidence

The live pass/fail table is in the [execution tracker](PROJECT_EXECUTION_TRACKER.md). The strongest
performance artifact currently present is `BuildReports/Mvp01Profiler.json`, captured
2026-08-31 05:06 UTC in a 1920x1080 D3D11 standalone run on an RTX 4070. Across 720 samples it
reports CPU/total p95 `8.335 ms`, zero measured steady-state GC, `AcquireSolid` peak `2.2064 ms`,
fracture preparation peak `0.4800 ms`, and a passing render audit. GPU timing was unavailable and
explicitly waived; this is not a GPU-budget proof. It also predates the snapshot commit.

The later editor diagnostic `BuildReports/Mvp01ProfilerEditorDiagnosticLatest.json` reports total
p95 `19.1631 ms`, GPU p95 `10.0344 ms`, and fails CPU, foot-contact, pipeline/camera, and overall
gates. It is non-authoritative editor evidence but cannot be cited as a pass.

`BuildReports/NativeWindows.json` reports a successful Windows build but also 186 warnings. That
fails the repository's zero-warning acceptance rule. No current-HEAD performance, warning-free
build, or complete PlayMode pass is claimed here.

Fresh evidence produced through the connected Unity Test Runner from the tested implementation is:

- `BuildReports/FoundationWorkingTreeEdit-20260831.xml`: full EditMode `586/587`; only
  `FinalBuildEvidenceAndCapabilityMatrixAreGreen` fails because the older Native Windows report
  contains 186 warnings.
- `BuildReports/FoundationWorkingTreePlay-20260831.xml`: targeted foundation PlayMode `2/2`,
  covering the rebuilt helmet/tail/belt configuration and rejection of closer dynamic debris as
  character support.

### Runtime animation/camera rescue evidence (2026-09-03)

The connected Unity Game view now reports both player and `Rumble Linebreaker Bot` on the baked
local-space EAMM path (`Active`, `ready-baked-local-space`) with upright visible-pose guards passing.
The same live audit reports camera wiring valid at `7.650 m / 60.00 degrees`, dual-subject DOF active
with a `6.34..13.00 m` sharp envelope, and radial gravity operational for both actors at about
`2.46 m/s^2` with Unity gravity intentionally disabled. The captured frame is
`BuildReports/RuntimeRescue/GameView_RuntimeRescue_Final.png`; the Unity Console had zero errors or
warnings after capture.

`BuildReports/AnimationTransitionsVNextEdit.json` passes `43/43`. The fresh
`BuildReports/EarthMagicExpansionPlay.json` passes `16/17`; the remaining blocker is
`PlatformDrawnUnderPlayerCarriesWithoutFractureOrRagdoll`, where the first post-release pillar-jump
vertical speed is `0.416 m/s` against a `>0.5 m/s` gate. No complete 30/60/120 FPS matrix or fresh
720-frame performance acceptance is claimed.

### Landing-amplitude follow-up (2026-09-03, uncommitted)

Working branch: `codex/environment-aware-motion-matching-spike`, base HEAD
`d2174eded114dd022e4a9c442abadda7a0e44555`; this note describes the dirty worktree,
not the historical tested snapshot above.

**Fact:** the ordinary `Land` and `Hard Land` states both use `Hard Landing.fbx`.
Changing only the landing style therefore did not reduce the soft landing's pose amplitude.
`EarthLandingPoseStrength` now maps observed airborne drop and impact speed to a normalized
pose weight; `HumanoidCharacterPresentation` tracks that evidence and passes the weight through
`EarthAnimationDriver` into `EarthAnimationGraph`. The graph blends the authored landing with
grounded locomotion before inertialization; clip playback speed and gameplay gravity are unchanged.
Initial support acquisition is not a jump/landing episode.

**Observed before this patch:** connected-Editor diagnostics showed operational radial gravity
(about `13.8 m/s^2`), grounded motors, animated ownership and zero dynamic visible-ragdoll bodies.
This supersedes the earlier `2.46 m/s^2` runtime snapshot for this session, but does not prove
the visual jump/landing issue resolved.

**Player feedback:** short jumping was subsequently confirmed fixed; startup falling and
occasional low-height rolls remained reproducible to the user.
No test suites, camera edits or scene rebuild were requested for this follow-up.
**Measured:** after `Assets/Refresh` through Unity MCP, compilation/import was idle and the
Console returned zero errors/warnings. Play mode was not started for this check.

**Follow-up implementation:** `PlanetMotor.Start` queries the existing classified support
before the first rendered animation frame, without movement, force or input consumption.
Editor ray diagnostics found both authored capsules only `0.055 m` above the static arena floor.
`EarthHardLandingRagdollBridge` now ignores initial support acquisition regardless of load
duration. Ordinary fall-only recovery stays in the landing animation instead of forcing a
physical knockdown and a second get-up roll. The catastrophic-fall KO gate (at least `5.5 m`
and `11 m/s` downward) and separate combat knockdowns remain unchanged.
Motor-only actors now freeze physics-driven capsule rotation while the motor owns orientation,
restoring the previous rotation constraints on disable; the player puppet keeps its existing
ownership. This addresses the authored bot's previously unconstrained root without freezing
its separate visible ragdoll bones.

The earlier roll revision required observed prior support plus at least `0.10 s` airborne, and one of:
measured drop `>2 m`, deliberate jump with at least `6.5 m/s` forward/backward takeoff speed,
or at least `4 m/s` external airborne velocity change after subtracting ordinary gravity.
Eligible rolls use full pose weight; small ordinary landings retain amplitude blending.
That revision selected a reversed authored clip with a forward-running state clock for backward travel.
These follow-up changes still require user visual verification; the earlier clean compile
is not acceptance evidence for them.

**Follow-up verification:** connected Unity compiled the new policy and authoring code with
no `error CS` entries. After the user stopped Play, the reverse asset was generated through
`EarthHumanoidMotionSetup.UpgradeController`: `humanMotion=true`, `138` float curves,
duration `0.7043334 s`; `Moving Land Back` references it with speed `1` and two exit transitions.
The user had entered Play during script reload; that interrupted session emitted
`MotionMatchingController` cache `NullReferenceException`s. No clean new Play run or test suite
was started by the agent, so runtime acceptance is still open. Source/doc whitespace checks
passed; the Unity-written controller retains the serializer's empty-value trailing spaces.

### Startup-pose and excessive-roll correction (2026-09-03, uncommitted)

The user subsequently rejected the startup behavior and synthetic backward roll. Two explicitly
armed five-second scene observations were run through Unity MCP, with no Test Runner, simulated
input, camera edit or scene rebuild. The Editor-only `EarthCharacterStartupProbe` records motor,
action, impact, ragdoll and visible hip orientation without controlling the game.

**Reproduced cause:** `BuildReports/RuntimeRescue/CharacterStartupProbe-Before.log` shows both
fighters grounded with zero hits and no ragdoll, yet hips-up projection is -0.25 when EAMM first
becomes Active. The bot repeats the fold after its first cast; the old post-output sanity guard
allowed three bad frames before rejecting the pose. This is a retargeting defect, not evidence
of stones hitting the characters or gravity turning off.

**Changed:** `EAMMBasePoseBridge` predicts candidate head/foot positions in cached target hierarchy
matrices before writing graph targets or enabling weight. Invalid candidates immediately fall
back to authored motion. No visible transform writes, clone skeleton or per-frame allocations
are used by this preflight. Original EAMM calibration is still invalid and is not claimed fixed.

`EarthLandingRollPolicy.FastJumpSpeed` is now 9 m/s: the previous 6.5 gate was below normal
7.2 m/s running speed and admitted ordinary running hops. The >2 m drop and >=4 m/s external
airborne delta-speed exceptions remain; first takeoff sampling is excluded. Backward travel
temporarily cannot select a roll until a genuine backward clip is supplied. The authoring setup
and regenerated controller use ordinary `Hard Landing` for `Moving Land Back`, with existing
height-scaled landing blending; the rejected synthetic asset is preserved but unused by that
state. `EarthCharacterImpactTarget` filters classified static support contacts so floor contact
does not enter the combat impulse path. This latter issue was found in code, not proven as the
startup cause. Real projectile/debris/wall impacts and catastrophic KO remain separate.

**Observed result:** new `BuildReports/RuntimeRescue/CharacterStartupProbe.log` has 87 samples
through 5.064 seconds; hips-up projection stays 0.61–0.82. Both start at 0.69 and the bot remains
upright after its first cast. EAMM reports `candidate-head-below-upright-envelope` instead of
presenting the bad pose. At 4.438 seconds two accepted player hits cause actual combat recovery;
that is not a startup pose regression. Console reports zero errors after the run. The diagnostic
session was stopped. Small/fast/backward jump visuals and movement across surfaces have not
been accepted by the user after this patch. No test suites were run for this correction.

`Docs/ANIMATION_INVENTORY.md` records imported Mixamo/KayKit clip names verified through the
connected Editor, missing desired actions and current download settings.

## Important entry points

| Need | Entry point |
|---|---|
| Open representative scene | `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity` |
| Rebuild generated Earth slice | Menu `Elemental/Setup/Create M3 Earth Core Slice`; `M3EarthCoreSetup.Configure()` |
| Full local EditMode/PlayMode | `Scripts/Test-Unity.ps1` with `UNITY_EDITOR_PATH` or `-UnityPath` |
| Focused MVP gates | `Assets/Elemental/Tests/EditMode/Mvp01FocusedTestLauncher.cs` menus |
| Accepted evidence/captures | `Assets/Elemental/Authoring/Editor/Mvp01EvidenceRunner.cs` and `Mvp01SceneCapture.cs` |
| Windows/macOS/Web builds | `Assets/Elemental/Authoring/Editor/ElementalBuildPipeline.cs` |
| Project validation | Menu `Elemental/Diagnostics/Validate Project`; `ElementalProjectValidator.cs` |
| Performance budgets | [`Docs/performance-budgets.md`](performance-budgets.md) |
| Third-party inventory | [`Docs/THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md), `Packages/manifest.json` |

## Known unknowns

### Mobility correction — 2026-09-04 (working tree on `d2174ed`)

- Supersedes the early roll acceptance below: the stronger test found the live
  Moving Land exit at 0.4145 with a 0.5594-second blend skipped the actual tumble.
  That transition alone is now 0.92 / 0.18 seconds; presentation protects the
  current and incoming roll through its outgoing blend. Other authored states,
  clips, Blend Tree entries and user material/arena settings were not rebuilt.
- Roll travel defaults are now 1.4 seconds / 7.5–9.5 m/s. Cast brace no longer
  cancels the motor travel while presentation is still protecting the roll.
- Narrow, idempotent `Repair Surf And Launch Pillar Bindings` restored missing
  surf profile/material/planet and launch feedback references in the existing
  scene. Serialized launch chip buffers are recreated on enable.
- Wave ground queries start only 0.45 m above the candidate support and use a
  thin ray (the former sphere overlap could report a zero-position hit). The
  production gate-floor query now returns the floor rather than the arch roof.
- User-requested `EarthPillarWaveProfile.FoundationBurialRatio = 0.20` lowers
  both physical and visual wave columns by 20% of their mesh height along local
  surface up. Applies to polygon/legacy columns on arena and planet. Change to
  0.15 for 15%; the profile asset was saved through Unity, not scene YAML edits.
- Evidence: `LandingRollMotionEdit.json`, UTC `2026-09-03T22:15:02.1808454Z`,
  **21/21**. Includes three orientations for the 20% foundation test.
  `IdleFootOrientationPlay.json`, UTC `2026-09-03T22:16:03.6356957Z`, **3/4**:
  idle, stop and production mobility passed. Roll plays through phase 0.901/0.895
  with torso-up -0.562/-0.567 and travels 2.458/1.786 m (player/bot), but the bot
  fails the requested test's 2 m minimum. Its ground acceleration is 18 versus
  player 48; ignoring actor collisions did not change the result. Do not report
  the combined regression green. Further bot-distance tuning remains open.
- `BuildReports/MobilityVisuals/LaunchPillar.png` and `SurfMaterial.png` show
  the restored pillar and sandstone board. These are not a full wave-motion
  visual gate. No fresh 720-frame/performance gate; pre-existing EarthArmorPiece
  required-component warnings still occur during EditMode scene hooks.

### Landing-roll travel follow-up — 2026-09-03

- **Fact (working tree on `d2174ed`):** `EarthLandingRollMotion` collects fixed-tick
  landing evidence using the existing roll eligibility policy. `PlanetMotor` owns
  confirmed roll travel and exposes the decision to presentation. No animation event,
  root-motion transform write, scene rebuild or clip replacement is involved.
- The motor substitutes a forward quadratic-decay velocity target for ordinary
  input braking during the short roll window. Normal acceleration limits, support
  velocity, radial gravity and PhysX collisions remain active. Jump, lost support,
  surf, casting/brace and physical knockdown interrupt the travel.
- Tuning: `PlanetMotorFeelProfile` / **Landing roll travel**: 0.72 seconds,
  minimum 4.5 m/s and maximum 7.2 m/s. These are desired speeds, not guaranteed
  displacement through obstacles; the ideal minimum-speed budget is 1.08 m.
- **Measured:** `LandingRollMotionEdit.json` at `2026-09-03T21:30:22.5616566Z`
  passes 11/11 (30/60/120 Hz decay integration, eligibility, once-only start,
  interruption). `LandingRollMotionPlay.json` at `2026-09-03T21:30:59.3446339Z`
  passes 1/1 in the production scene: both actors actually enter the authored roll,
  travel forward 0.651/0.459 m after a 3.5 m drop and settle below 0.001 m/s.
- **Regression:** the combined `IdleFootOrientationPlay.json` run at
  `2026-09-03T21:32:59.3335687Z` passes 3/3 in 22.345 s, retaining idle orientation
  and forward/back stop checks. Game screenshots are in `BuildReports/LandingRollMotion/`;
  the opponent is partially occluded by the player, so these stills alone are not
  a full visual roll-motion gate. EditMode still emits the pre-existing armor
  required-component warnings documented below; no new compiler warning was observed.
- **Unknown:** comprehensive obstacle/slope/platform roll and performance matrices
  remain unmeasured. This narrow change does not close the EAMM acceptance gates.

### Stop / support-release target backlog correction — 2026-09-03

- **Fact (working tree on `d2174ed`):** the free-foot filter previously capped
  absolute support-local motion at 1.5 m/s. It now filters the surface correction
  relative to the authored foot reference. A locked anchor remains support-local;
  lock ownership changes and invalid contacts reset filter history. Thus authored
  locomotion is not subject to the 2.5 cm/60-Hz contact-correction speed cap.
- **Fact:** `EarthFootContactController.LateUpdate` is now telemetry-only. The
  post-IK ankle-position and root-relative rotation clamps were removed: changing
  only the ankle after the Humanoid solve could stretch the shin and retain a
  stale pose after release. Target/normal and weight filtering remain before IK.
- **Measured:** seven new regressions failed before this fix: forward/backward
  6 m/s travel accumulated 4.30/4.41/4.47 m of target lag at 30/60/120 Hz; loss of
  support retained a target 1.975 m from the authored fallback. After correction,
  `BuildReports/AnimationTransitionsVNextEdit.json` at
  `2026-09-03T21:16:41.3800060Z` passes 50/50. The suite also emitted existing
  `EarthArmorPiece` missing-required-component warnings before and after the fix.
- **Measured runtime:** `BuildReports/IdleFootOrientationPlay.json` at
  `2026-09-03T21:20:53.3573992Z` passes 2/2. Both fighters move before forward/back
  stops (measured speed >0.2 m/s), then sample for 60 frames per direction. Peak
  free-target tangential lag is 0.0047 m backward / 0.0050 m forward. The test
  bounds ankle translation by the Avatar's allowed leg-stretch budget; the first
  test version incorrectly treated all Humanoid length adjustment as an error.
  Idle toe orientation remains passing at 30/60/120 caps. Actual platform-edge
  traversal and the broad performance/contact corpus are not covered by this run;
  support loss is covered by the pure regression above.
- This supersedes the unchanged-final-smoothing statement in the earlier idle
  orientation entry below. User clips, Blend Tree positions, and profile tuning
  are not changed by this correction.

### Earlier idle foot IK orientation correction — 2026-09-03

- **Fact (uncommitted working tree on `d2174ed`):** `EarthFootContactController`
  passed a skeleton foot-bone rotation to a Humanoid IK goal. These bases differ
  on Linebreaker. The adapter now reads `Animator.GetIKRotation(goal)` before
  applying the support-normal delta; authored clips and user Blend Tree entries
  are unchanged. Contact position, weight, and final smoothing policy are unchanged.
- **Measured:** live on/off/on A/B on both fighters reproduced upward toe
  directions of 0.97–0.99 along local up with the old controller, versus forward
  feet without it. `BuildReports/IdleFootOrientationPlay.json`, UTC
  `2026-09-03T21:09:52.6993029Z`, passes 1/1 after the correction. The regression
  repeats idle contact ramps at target frame-rate caps 30/60/120, checks 60 samples
  per cap and both feet of both actors with IK weight >0.8; peak toe-up dot was
  -0.0638/-0.0639/-0.0639. Caps are not proof of achieved frame pacing.
- **Scope:** this closes the reproduced idle toe inversion only. EAMM still
  reported `PoseRejected` in the live diagnostic; complete movement/slope/recovery
  and performance acceptance remain separate. No scene or motion-library rebuild.

- Which focused and repository-wide gates pass after rebuilding the scene at snapshot HEAD.
- Whether the new classified support policy will be wired into runtime support selection, and
  which existing owner it will replace or constrain.
- Current-head GPU p95/frame pacing on the reference PC and on minimum target hardware.
- Whether the 55.1 m world and current Broken Crown placement are accepted across every physical
  input, KO/respawn, fracture/repair, surf, camera, and visual scenario.
- Player evidence: no fresh external playtest proves comprehension, feel, dominant strategies,
  or that the repeatable duel is enjoyable without developer explanation.
- Whole-game persistence/recovery, production online transport, accessibility/controller matrix,
  supported hardware floor, store/business model, and release window.
- Whether all present third-party/AI packages and generated/imported assets pass final licensing,
  provenance, build-size, and platform review.
