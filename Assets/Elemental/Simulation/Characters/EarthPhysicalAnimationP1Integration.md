# Earth physical animation P1 integration

## Canonical contract and fallback

- `CharacterPhysicalController` is the sole physical-mode authority.
  `EarthPhysicalAnimationCoordinator` is a pure ownership adapter that accepts
  only canonical `AnimatedMotor`, `FullRagdoll`, and `Recovery` inputs in P1.
  It owns no parallel mode enum or transition machine. Powered P2 modes and
  behaviours are intentionally absent.
- `HumanoidRagdollRig`, `ActiveRagdollPuppet`, `ActiveRagdollJoint`, and
  `CharacterPhysicalController` remain the existing physical stack. The runtime
  adapter asks `ActiveRagdollPuppet` to mutate canonical mode before changing
  Animator, IK, procedural, motor, or PhysX ownership.
- `EarthPhysicalAnimationProfile.UsePoseMatchedRecovery` is serialized `false`
  in `Assets/Elemental/Content/Profiles/EarthPhysicalAnimationProfile.asset`.
  Flag-off recovery executes the legacy live-pelvis path. An enabled profile
  with invalid/misordered markers, no valid pose, blocked clearance, or a state
  missing from Animator layer 0 warns and takes that same legacy path before any
  PhysX-to-Animator ownership mutation.
- A distinct accepted hit interrupts either legacy or pose-matched recovery in
  one call: captured owner groups are restored, canonical mode returns to
  `FullRagdoll`, marker state is cleared, and Animator-to-PhysX ownership enters
  once. Impacts received while already in full ragdoll retain legacy
  `ApplyHandoff` behaviour because `RagdollHandoff` has no response identity.

## Director wiring

1. Author valid Front, Back, Left, and Right entries. Each requires a stable
   non-zero clip ID, an existing full Animator state path, entry phase,
   clip-root pelvis offset, six pose features, and ordered feet/control/exit
   markers. Invalid samples are excluded; no default markers are substituted.
2. Call `HumanoidRagdollRig.ConfigurePhysicalAnimation` for both fighters with
   the same profile and existing owner groups:
   - feet: existing foot-contact/IK writer;
   - controls: existing motor/input/controller adapters;
   - procedural: existing body response, secondary motion, and rig writers.
3. Keep existing `disabledDuringRagdoll` wiring. P1 captures prior enabled state
   before the single ownership handoff and restores only originally enabled
   behaviours.
4. Marker eligibility uses a bounded, non-alloc recovery sampler with the
   existing PlanetMotor capsule, slope, distance, and ground-mask configuration,
   then passes candidates through `CharacterSupportRuntimeAdapter` and the pure
   `CharacterSupportAuthority`. It continues sampling while movement control and
   PlanetMotor updates are disabled. Support loss revokes marker owners and
   blocks exit until live support is reacquired.
5. After steps 1-4 validate cleanly, enable `UsePoseMatchedRecovery` only in the
   integrated preset/scene. Do not enable the empty default asset.

The result exposes orientation, clip/state IDs, closest valid entry phase, match
cost, live pelvis, root pose, radial up/facing, clearance result, feet/control/
exit markers, and facing-fallback diagnostic. Root alignment is reconstructed
from the live physics pelvis; no pre-hit root participates. The selected Animator
state hash and phase are passed through `HumanoidCharacterPresentation` to the
existing `EarthTransitionDirector`, which is the sole selected-state writer.
They are captured immediately after the event and again on the following frame.

## Tests and evidence required

- EditMode: four-way classification, closest pose/phase, live pelvis/no pre-hit
  alignment, degenerate facing/no 180 flip, bounded clearance, invalid marker
  rejection, canonical ownership consistency and interruption, support-loss
  revocation, 30/60/120 Hz threshold and skipped-threshold equivalence, and 100
  drift-free repeated alignments.
- PlayMode:
  `HumanoidRecoveryPreservesLegacyFallbackAndMarkersOwnPoseMatchedHandoff`
  exercises flag-off and flag-on recovery interruption, duplicate recovery
  rejection, state hash/entry persistence, marker owners, and missing Animator
  state fallback.
- Director must run focused EditMode and PlayMode sequentially in Unity and
  capture front/back/left/right recovery with shipping camera. Runtime animation
  quality, marker timing, root jump, yaw continuity, and support-loss visuals
  remain unproven until those runs. Profiler marker:
  `Elemental.Character.PoseMatchedRecovery`.

## Known Unity integration risks

- The default profile intentionally contains no production clip samples.
- The scene Animator must contain every configured full-path state hash. The
  adapter verifies `HasState` before ownership mutation, but director validation
  must confirm the transition director does not overwrite the state on the event
  callback or following frame.
- The recovery sampler intentionally does not apply movement. Validate its live
  classification on arena floor, spherical terrain, platforms, moving-support
  generation swaps, and deliberate support loss/reacquisition.
- Owner groups must not contain the Animator or another writer required to
  evaluate the recovery clip.

## Wave P2 prerequisites

Do not begin powered behaviours until P1 compiles and focused Unity evidence is
green with real samples, marker owners, four-orientation captures, root jump
below 0.12 m, yaw discontinuity below 12 degrees, support-loss recovery, and no
warning/error logs. P2 must consume canonical `CharacterPhysicalMode` and must
not add another ragdoll, IK writer, hit owner, or transition system.
