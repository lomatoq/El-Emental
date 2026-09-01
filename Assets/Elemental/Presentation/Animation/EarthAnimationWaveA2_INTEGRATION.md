# Earth Animation Wave A2 integration

## Scope and commits

Wave A2 is implemented by three commits, based on accepted Gate-1 integration `9d9f46e8cc450208de8855fa92dba44f1fcdbf00`:

- `6949c27f262e8d844184812666504a512977c43b` — deterministic pure transition rules and subordinate bounded queue;
- `530e7e9d5cf46e300c0ab3c59b2a7e673b6bed06` — authored transition profiles, director integration, diagnostics, and canonical runtime coverage;
- `7816834f5b755e750ba346d1c956216cade1635d` — deterministic 51-clip catalog, metadata editor, transition preview, validators, and catalog tests.

This wave does not implement A3 motion matching, edit shipping scenes or prefabs, change Animator controllers or FBX metadata, or change Packages/ProjectSettings.

## Defaults, ownership, and fallback

- `EarthTransitionProfile.UseTransitionProfile` defaults to `false`.
- `EarthTransitionProfile.UseTransitionQueue` defaults to `false` and is effective only when the profile flag is enabled.
- No transition-profile or motion-catalog asset is assigned to a shipping scene or prefab in this branch.
- `EarthTransitionDirector` remains the only Animator/state writer. `EarthTransitionQueue` stores semantic requests only; the debug window can submit a request only through `EarthTransitionDirector.RequestTransition`.
- With a null or disabled profile, the director executes the exact legacy policy path.
- With an enabled profile and no matching pair, the only fallback is a finite generic fixed crossfade. It emits one development/editor warning per actually executed source/destination pair; merely resolving or previewing a pair does not emit.
- Foot-release requests remain delegated to `EarthFootContactController`; authored body masks and half-lives are passed to the existing A1 inertialization graph. Gameplay root, planted-foot, active-hand, and ragdoll ownership exclusions remain unchanged.

## Transition contract

An authored pair records source/destination state and/or category selectors, transition family, priority, inertia half-life, fixed fallback duration, gait-phase rule, contact policy, cancel policy, protected/cancel windows, destination phase, body mask, foot-release policy/delay, and queue-on-block behavior.

The pure policy supports wrap-aware normalized windows, protected/cancel gating, source phase preservation/opposite contact/fixed target/contact alignment, and an explicit mapping from every contact policy to the sole foot-lock owner: preserve uses the authored release policy, destination/landing matching defaults to destination-contact release, and pre-release/ignore release immediately. The fixed-capacity queue has a hard maximum of 32 entries, stable priority/FIFO ordering, deterministic duplicate replacement even when the configured capacity is full, source-staleness rejection, protected-window cancellation, and no managed allocation after construction. Queue processing remains subordinate to director state and is wrapped by the existing `Elemental.Character.TransitionQueue` profiler marker.

## Motion catalog contract and tooling

The catalog builder deterministically unions controller-referenced clips with `EarthHumanoidMotionSetup.CuratedPaths` and the two existing catalog-library FBXs `Rig_Medium_MovementAdvanced.fbx` and `Rig_Medium_MovementBasic.fbx`, deduplicates by GUID plus local file ID, sorts by that identity, and fails unless the result is exactly 51 existing clips. Unity EditMode import observed 31 unique identities before the two catalog-library paths; those paths contain 24 imported clip subassets and contribute 20 new identities after deduplication, producing the required 51. A failure includes a per-source imported/new count and subasset-name inventory. Missing, ambiguous, or duplicate provenance fails loudly; no replacement clips are invented or downloaded.

Each presentation-layer clip profile records the clip/GUID/local-file-ID/source path and license provenance, semantic and authored action, average planar speed/direction/yaw, stance/style, the exact existing eight curves (`LeftFootContact`, `RightFootContact`, `LeftFootPhase`, `RightFootPhase`, `LandContact`, `CanExit`, `PelvisCompression`, `RootEffort`), landing phase, safe-exit/cancel/recovery windows, hand occupancy, mirroring, and environment/action tags. Complete imported curve sets are copied into the catalog only when every key time/value is finite. Missing or non-finite sets are derived through the existing non-mutating kinematic analyzer, whose output is checked before constructing catalog-local curves, and stored only in the catalog asset; the builder never writes model importers or FBX metadata.

Pure enums and phase-window math live in `Elemental.Simulation` with only `System` and `Unity.Mathematics`. `AnimationClip`, `AnimationCurve`, and `ScriptableObject` wrappers live in `Elemental.Presentation.Animation`, preserving the repository dependency boundary.

`EarthMotionMetadataEditor` provides deterministic rebuild/validation, immutable provenance display, searchable clip selection, manual correction groups that survive rebuilds, normalized timeline sampling, and live display of all eight curves. `EarthTransitionDebugWindow` previews pair resolution and bounded runtime diagnostics; in Play Mode its only mutation is a director request. Validators reject catalog count/schema/hash/provenance/order/curve/kinematic/window/semantic faults and malformed or duplicate transition selectors.

## Tests and integration procedure

Worker validation was intentionally static because the integration director owns Unity execution. The commits include:

- EditMode pure transition rule tests for pair fields, protected/cancel windows, target phase, foot release, malformed finite input, and strict 30/60/120 equivalence;
- EditMode queue tests for deterministic ordering, duplicate replacement, cancellation, source staleness, protected blocking, capacity, and zero hot-path managed allocation;
- EditMode profile tests for default-off legacy behavior, specificity, complete field round-trip, explicit generic fallback, body masks, validator behavior, and zero hot-path managed allocation;
- EditMode catalog tests for exactly 51 unique GUID/local-file-ID entries, all eight curves, provenance/semantic completeness, validator success, consecutive rebuild identity/order stability, manual-correction preservation, zero-allocation lookup, and the no-Unity-object Simulation source boundary;
- PlayMode canonical director coverage for authored pair execution, body-mask/half-life delivery to A1, warning only on an actually used fallback, bounded diagnostics, and queued-request draining.

Integration should cherry-pick the three commits in order, allow Unity to import/compile, run all EditMode and PlayMode tests, then run **Elemental Suite > Character > Rebuild Earth Motion Catalog**. The build must report exactly 51 profiles and the validator must report no issues. Keep the generated catalog and any transition profile unassigned until authored pair review is complete; opt into the profile and queue flags independently. Capture the transition and transition-queue profiler markers under the production 720-frame Gate-1 scenario and confirm zero managed allocation in steady-state request resolution, queue processing, and catalog lookup.

## Known unproven items

- The catalog discovery correction has not been rerun in Unity by this worker; the integration director must confirm the corrected 51-clip AssetDatabase build and validator result. The prior Unity run compiled A2 and observed 31 identities before the omitted catalog-library paths were added.
- All 112 `IsFinite(curve.GetKey(0).value)` assertions in that prior log occurred while Unity imported the pre-existing KayKit `Rig_Medium_General.fbx`, before catalog rebuild. A source scan found all 4,080 serialized custom-curve time/value scalars finite, so this is importer-side reduction noise rather than a catalog-generated curve. A2 does not edit that importer or FBX metadata. Catalog copied and derived curves now have independent finite-key guards, so a successful catalog validation distinguishes catalog output from that import warning.
- No production `EarthMotionCatalog.asset` or `EarthTransitionProfile.asset` is committed, because A2 is default-off and shipping scene/prefab wiring is outside this branch.
- Pair authoring for the complete production transition matrix and visual/timing approval remains an integration/content task. Generic fallback diagnostics identify any pair that is still missing.
- A2 introduces no pose search, trajectory query, KD-tree, motion database, or runtime motion matching. Those remain A3 work and must reuse the director/graph/catalog ownership boundaries above.
