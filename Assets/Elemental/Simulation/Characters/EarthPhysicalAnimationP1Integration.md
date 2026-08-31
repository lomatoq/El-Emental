# Earth physical animation P1 integration

## Contract and fallback

- `EarthPhysicalAnimationCoordinator` is the pure owner of the eight-mode VNext
  contract: Animated, PhysicalAssist, Stagger, BalanceRecovery, Brace,
  FallProtect, FullRagdoll and GetUp. P1 drives only Animated, FullRagdoll and
  GetUp; powered P2 behaviours are intentionally absent.
- `HumanoidRagdollRig`, `ActiveRagdollPuppet`, `ActiveRagdollJoint` and
  `CharacterPhysicalController` remain the existing physical stack. P1 adds no
  second ragdoll or transition state machine.
- `EarthPhysicalAnimationProfile.UsePoseMatchedRecovery` is serialized `false`
  in `Assets/Elemental/Content/Profiles/EarthPhysicalAnimationProfile.asset`.
  With the flag off, `HumanoidRagdollRig.RecoverToAnimated` executes the prior
  live-pelvis recovery path. An enabled but incomplete profile logs one
  actionable warning and falls back to that path.
- Active-ragdoll impacts retain the legacy `ApplyHandoff` behaviour because
  `RagdollHandoff` has no canonical response ID. Ownership and repeated recovery
  requests are idempotent; distinct later physical impacts are not blindly
  discarded.

## Director wiring

1. Populate the profile with sampled, valid entries for Front, Back, Left and
   Right. Each sample requires a stable non-zero clip ID, existing full Animator
   state path, entry phase, clip-root pelvis offset, six pose features, and ordered
   feet/control/exit markers.
2. Call `HumanoidRagdollRig.ConfigurePhysicalAnimation` for both fighters.
   Pass the same profile and three non-empty, non-authoritative owner groups:
   - feet: the existing `EarthFootContactController`/foot-IK writer;
   - controls: existing motor/input/controller adapters that the recovery marker
     may restore;
   - procedural: existing procedural body, secondary motion, organic idle and rig
     writers that must not write full-ragdoll bones.
3. Keep existing `disabledDuringRagdoll` wiring. P1 captures every group's prior
   enabled state before the single Animator-to-PhysX handoff and restores only
   originally enabled behaviours.
4. Only after steps 1-3 validate cleanly, set `UsePoseMatchedRecovery` true in the
   integration preset/scene. Do not enable the empty default asset.

The selected result exposes orientation, clip ID, animation-state ID, closest
valid entry phase, match cost, live pelvis, root pose, radial up/facing, clearance
kind/lift/success, feet marker, controls marker, exit marker and facing-fallback
diagnostic. The root is reconstructed from the current physics pelvis plus the
selected clip offset; no saved pre-hit position participates.

## Tests and evidence required

- `EarthPoseMatchedRecoveryTests`: four-way classification, closest valid sample,
  zero-allocation steady-state matching, live-pelvis/no-pre-hit alignment,
  degenerate facing, no 180-degree flip, clearance fallback, idempotent ownership,
  support-gated markers, 100 drift-free repeats, and default-off profile.
- `ActiveRagdollRuntimeTests.HumanoidRecoveryPreservesLegacyFallbackAndMarkersOwnPoseMatchedHandoff`:
  same-scene flag-off legacy recovery followed by enabled marker ownership and a
  repeated recovery request.
- Director must run focused EditMode and PlayMode sequentially in Unity, then
  capture one front, back, left and right recovery with the shipping camera.
  Runtime animation quality, marker timing, root jump and yaw discontinuity remain
  unproven until those runs. The profiler marker is
  `Elemental.Character.PoseMatchedRecovery`.

## Known Unity integration risks

- The default profile intentionally contains no clip samples. Agent A/director
  must provide real sampled recovery entries; placeholder vectors are not
  production content.
- P1 performs the selected one-shot recovery state/phase handoff on the existing
  Animator after the existing `AuthoredRecoveryBegan` semantic event. During
  integration, confirm Agent A's transition director does not overwrite that
  entry phase on the following frame.
- The non-alloc support ray uses `PlanetMotor.GroundMask` and the profile distance.
  Feet and controls remain disabled indefinitely if the authored root has no valid
  support, by design. Validate arena floor, spherical terrain, platforms and
  support-generation swaps.
- Behaviour groups must not contain a writer that is required to evaluate the
  recovery clip itself. Animator ownership is managed separately by the rig.

## Wave P2 prerequisites

Do not begin powered behaviours until P1 compiles and the focused runtime test is
green with real profile samples, marker owners, four-orientation captures, root
jump below 0.12 m, yaw discontinuity below 12 degrees, and no warning/error logs.
P2 must consume this coordinator mode contract and must not add another ragdoll,
IK writer, hit owner or transition system.
