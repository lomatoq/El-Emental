# Earth Animation Wave A2 integration

## Scope and commits

Wave A2 is implemented by three commits, based on accepted Gate-1 integration `9d9f46e8cc450208de8855fa92dba44f1fcdbf00`:

- `6949c27f262e8d844184812666504a512977c43b` — deterministic pure transition rules and subordinate bounded queue;
- `530e7e9d5cf46e300c0ab3c59b2a7e673b6bed06` — authored transition profiles, director integration, diagnostics, and canonical runtime coverage;
- `7816834f5b755e750ba346d1c956216cade1635d` — deterministic 51-clip catalog, metadata editor, transition preview, validators, and catalog tests.

This wave does not implement A3 motion matching, edit shipping scenes or prefabs, commit a generated Animator controller, change FBX metadata, or change Packages/ProjectSettings. The truthful-content follow-up updates the existing controller builder; integration must review its deterministic generated controller diff.

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

The catalog builder deterministically unions controller-referenced clips with `EarthHumanoidMotionSetup.CuratedPaths`, the two existing catalog-library FBXs `Rig_Medium_MovementAdvanced.fbx` and `Rig_Medium_MovementBasic.fbx`, and exact-name selectors for the five pre-existing KayKit magic clips in `Rig_Medium_CombatRanged.fbx`. It deduplicates by GUID plus local file ID, sorts by that identity, and fails unless the result is exactly 51 existing clips. To retain that accepted bound, four unused library-only identities are excluded explicitly: MovementAdvanced `T-Pose`, `Crawling`, and `Crouching`, plus MovementBasic `T-Pose`. The builder fails if any exclusion becomes controller-reachable. A failure includes a per-source imported/new count and subasset-name inventory. Missing, ambiguous, or duplicate provenance fails loudly; no replacement clips are invented or downloaded.

Each presentation-layer clip profile records the clip/GUID/local-file-ID/source path and license provenance, semantic and authored action, average planar speed/direction/yaw, stance/style, the exact existing eight curves (`LeftFootContact`, `RightFootContact`, `LeftFootPhase`, `RightFootPhase`, `LandContact`, `CanExit`, `PelvisCompression`, `RootEffort`), landing phase, safe-exit/cancel/recovery windows, hand occupancy, mirroring, and environment/action tags. Complete imported curve sets are copied into the catalog only when every key time/value is finite. Missing or non-finite sets are derived through the existing non-mutating kinematic analyzer, whose output is checked before constructing catalog-local curves, and stored only in the catalog asset; the builder never writes model importers or FBX metadata.

Pure enums and phase-window math live in `Elemental.Simulation` with only `System` and `Unity.Mathematics`. `AnimationClip`, `AnimationCurve`, and `ScriptableObject` wrappers live in `Elemental.Presentation.Animation`, preserving the repository dependency boundary.

`EarthMotionMetadataEditor` provides deterministic rebuild/validation, immutable provenance display, searchable clip selection, manual correction groups that survive rebuilds, normalized timeline sampling, and live display of all eight curves. `EarthTransitionDebugWindow` previews pair resolution and bounded runtime diagnostics; in Play Mode its only mutation is a director request. Validators reject catalog count/schema/hash/provenance/order/curve/kinematic/window/semantic faults and malformed or duplicate transition selectors.

## Runtime controller binding slice

Catalog schema 3 also stores a deterministic controller-state table generated from every non-empty Animator state and its recursively traversed blend-tree clips. Each entry records layer, full-path hash, semantic state/category/role, and catalog profile indices. Runtime lookup verifies the exact active `AnimationClip` when one is supplied and fails if that clip is not a member of the authored state; state-primary lookup is explicit and is used only before a destination starts. The builder and validator fail unless every locomotion, cast, hit/impact, and recovery state has catalog-backed GUID/local-file-ID provenance.

`EarthTransitionDirector` remains the sole state writer. Its thin read-only binding adapter samples the current clip on each controller layer using one preallocated scratch list under `Elemental.Character.MotionCatalogBinding`; bounded diagnostics report verified/unresolved layers, lifetime resolutions/misses, the base-layer profile, and source/destination profile indices for the last authored transition pair. A missing catalog changes no Animator behavior and is visible as unconfigured diagnostics rather than silently pretending verification.

`EarthMotionTransitionCatalogResolver` combines the existing pair-specific profile lookup with verified source/destination state profiles. `EarthRecoveryCatalogResolver` delegates front/back closest-pose choice to the existing pure `EarthRecoveryPoseMatcher`, then verifies the selected state against the recovery catalog role and passes its authored markers through unchanged. `EarthPhysicalAnimationCoordinator` and the physical-animation stack remain the sole feet/control/exit owners; no recovery or transition owner is added here.

## Truthful authored-content pass

The sole existing controller builder now binds the imported KayKit identities verbatim: `Ranged_Magic_Raise`, `Ranged_Magic_Shoot`, `Ranged_Magic_Spellcasting`, `Ranged_Magic_Spellcasting_Long`, and `Ranged_Magic_Summon`. Missing exact identities abort controller rebuild instead of selecting a similarly named clip. The semantic slots use those compatible sources for gather/summon, lift/raise, sustain, and release/shoot. Pull, push, and slam remain explicitly generic fallbacks because no imported clip carries those authored semantics.

`EarthAnimationContentAudit` joins source identity, catalog provenance, and controller binding into a read-only report. Its pure Simulation result distinguishes runtime-playable fallback from verified authored coverage and fails closed at missing source, catalog, or binding evidence. Left pivot is exact and right pivot is the controller's authored mirror of `Left Turn`. Dedicated directional start/stop, front/back recovery, and flip are not claimed: start/stop use the locomotion loop as fallback, both recovery sides use `Falling To Roll` as fallback, and flip has no source.

## Tests and integration procedure

Worker validation was intentionally static because the integration director owns Unity execution. The commits include:

- EditMode pure transition rule tests for pair fields, protected/cancel windows, target phase, foot release, malformed finite input, and strict 30/60/120 equivalence;
- EditMode queue tests for deterministic ordering, duplicate replacement, cancellation, source staleness, protected blocking, capacity, and zero hot-path managed allocation;
- EditMode profile tests for default-off legacy behavior, specificity, complete field round-trip, explicit generic fallback, body masks, validator behavior, and zero hot-path managed allocation;
- EditMode catalog tests for exactly 51 unique GUID/local-file-ID entries, all eight curves, provenance/semantic completeness, validator success, consecutive rebuild identity/order stability, manual-correction preservation, zero-allocation lookup, and the no-Unity-object Simulation source boundary;
- PlayMode canonical director coverage for authored pair execution, body-mask/half-life delivery to A1, warning only on an actually used fallback, bounded diagnostics, and queued-request draining.

Integration should cherry-pick the three commits in order, allow Unity to import/compile, run all EditMode and PlayMode tests, then run **Elemental Suite > Character > Rebuild Earth Motion Catalog**. The build must report exactly 51 profiles and the validator must report no issues. Keep the generated catalog and any transition profile unassigned until authored pair review is complete; opt into the profile and queue flags independently. Capture the transition and transition-queue profiler markers under the production 720-frame Gate-1 scenario and confirm zero managed allocation in steady-state request resolution, queue processing, and catalog lookup.

## Known unproven items

- The exact 51st-identity correction has not been rerun in Unity by this worker; the integration director must confirm the corrected 51-clip AssetDatabase build and validator result. Edit3 compiled A2 and observed 50 identities after adding the two omitted catalog-library paths, with the exact per-source counts documented above.
- All 112 `IsFinite(curve.GetKey(0).value)` assertions in that prior log occurred while Unity imported the pre-existing KayKit `Rig_Medium_General.fbx`, before catalog rebuild. A source scan found all 4,080 serialized custom-curve time/value scalars finite, so this is importer-side reduction noise rather than a catalog-generated curve. A2 does not edit that importer or FBX metadata. Catalog copied and derived curves now have independent finite-key guards, so a successful catalog validation distinguishes catalog output from that import warning.
- No production `EarthMotionCatalog.asset` or `EarthTransitionProfile.asset` is committed, because A2 is default-off and shipping scene/prefab wiring is outside this branch.
- Pair authoring for the complete production transition matrix and visual/timing approval remains an integration/content task. Generic fallback diagnostics identify any pair that is still missing.
- The current controller has one looping locomotion blend state but no dedicated authored locomotion start or stop states. `Left Turn` provides exact left pivot and mirrored right pivot; directional starts/stops remain generic locomotion fallback until real clips and states exist.
- KayKit magic raise, shoot, spellcasting, long spellcasting, and summon are cataloged and bound, but source naming supports only compatible gather/lift/sustain/release claims. Exact pull/push/slam clips and family-specific timing remain absent.
- No pre-existing curated clip name identifies a locomotion/combat flip, and the recovery profile remains default-off with no production front/back pose samples. The resolver proves deterministic front/back closest-pose and marker propagation in EditMode, but shipping front/back get-up/flip coverage requires authored samples and clips rather than relabeling `Falling To Roll`.
- A2 introduces no pose search, trajectory query, KD-tree, motion database, or runtime motion matching. Those remain A3 work and must reuse the director/graph/catalog ownership boundaries above.
