# Earth Physical Animation Wave P2 Integration

## Ownership contract

- `CharacterPhysicalController` and `CharacterPhysicalMode` remain the sole
  physical-mode authority. `EarthPoweredPhysicalAssist` owns only bounded
  response history, response decay, and one-shot semantic-request state.
- `EarthWorldResponseEvent.ResponseId` is the accepted-hit identity. The fixed
  16-entry history rejects a duplicate response while allowing distinct hits
  and deterministic wrap/eviction.
- Light (`Flinch`) resolves to `AgentAInertialResponse`; P2 changes no mode and
  emits no impulse. Medium (`Stagger`) resolves to `PoweredPhysicalAssist` only
  after canonical-mode acceptance, `PlanetMotor.HasStableSupport`, valid foot
  bindings, at least one fresh planted-foot sample, a valid planted-contact
  hull, and `CharacterPhysicalController.TryRequestPoweredAssist` acceptance.
  Rejection returns ownership to Agent A without consuming the response ID, so
  a later eligible handoff of that same accepted event is deterministic. Heavy
  (`RecoverableKnockdown`/`Knockout`) resolves to
  `ExistingFullRagdoll`; P2 emits no ragdoll command and no additive kick.
- P1 full-ragdoll/recovery interruption, live-support, pose-match, marker, and
  ownership handoff contracts are unchanged.

## Feature gate and exact fallback

- `EarthPhysicalAnimationProfile.UsePoweredPhysicalAssist` serializes `false`.
  The accepted Gate 1 profile asset is not modified by Wave P2.
- With the flag off, `ActiveRagdollPuppet` uses the original support probe,
  original joint drives, original balance torque, and original impact path.
  `ReceiveAcceptedWorldResponse` is a no-op.
- With the flag on, powered balance combines `PlanetMotor.HasStableSupport`
  with fresh semantic planted-foot samples. It constructs an allocation-free
  convex hull from the full longitudinal and lateral footprint of only the
  planted contacts; a swing foot never enlarges the polygon. It suppresses the
  legacy root-body balance torque. No powered behavior applies force or torque
  to the pelvis/root body.

## Behaviors and output seam

- `MaintainBalance` evaluates COM against the radial support polygon.
- `StaggerStep` selects left/right recovery foot and emits only
  `EarthPhysicalActionKind.AuthoredRecoveryStep`.
- `BraceAgainstSurface`, `FallArrest`, and `ReachForSupport` accept only finite,
  semantically classified, non-self hits within 1.35 metres from fixed-size
  non-allocating sphere-cast buffers. `ProtectHead` is a muscle/semantic request,
  never an Animator write.
- `ActiveRagdollPuppet.PhysicalActionRequested` is the sole outward request
  seam. Agent A's transition owner decides whether and when to play an authored
  recovery step. Agent A's foot IK remains the final foot/contact owner.

## Muscle profiles

The pure profile library contains `Stable`, `Reactive`, `Stagger`,
`FallProtect`, `Ragdoll`, and `Recovery`. Every `Pelvis`, `Spine`, `Chest`,
`Head`, `Arm`, and `Leg` entry carries frequency, damping, torque cap, angular
limit, drive weight, transfer weight, and time-normalized recovery rate.

Every canonical and custom profile is sanitized to zero leg frequency,
damping, torque cap, drive weight, and transfer weight. Powered joint targets
therefore cannot fight authored leg animation or foot IK in any mode. The
energy estimator clamps work to the authored torque cap and angular limit.
`Ragdoll` has zero drive in every region.

The zero-leg and unassigned fail-closed paths clear spring, damping, and maximum
force on all six `ConfigurableJoint` drive properties (`x`, `y`, `z`, angular X,
angular YZ, and slerp); switching drive mode cannot expose a stale powered drive.

Every `ActiveRagdollJoint` serializes as `EarthBodyRegion.Unassigned`. Powered
assist is disabled with one actionable error until every joint has an explicit
validated Pelvis/Spine/Chest/Head/Arm/Leg binding. The feature-off legacy drive
path does not read the P2 region and remains unchanged.

Built-in profiles are the explicit default. Enabling custom profiles requires
exactly one entry for every profile used at runtime; a missing or duplicate ID
raises an actionable configuration error instead of silently substituting data.

## Director wiring after review

1. Keep the existing `EarthPhysicalAnimationProfile` gate off until a Director
   validation fixture is ready.
2. Assign every `ActiveRagdollJoint` an explicit `EarthBodyRegion`. The
   serialized default is deliberately `Unassigned`; incomplete bindings log an
   error and keep the powered layer disabled while the feature-off legacy path
   remains exact.
3. Call `ActiveRagdollPuppet.ConfigurePoweredPhysicalAssist` with the profile and
   read-only humanoid left/right foot, head, and hand transforms.
4. During the presentation/contact pass, call
   `SetPoweredFootContactState(leftPlanted, rightPlanted)` every rendered frame.
   The adapter stores only the semantic booleans and expires stale samples; it
   does not capture anchors or write IK.
5. Route each accepted `EarthWorldResponseEvent` once through
   `ReceiveAcceptedWorldResponse`. For a returned `PoweredPhysicalAssist` owner,
   suppress Agent A's procedural angular response for that same response ID.
   A rejected medium returns `AgentAInertialResponse` with `Accepted == false`
   and a typed rejection reason; execute the Agent A fallback and do not suppress
   it. Continue routing `Flinch` only to Agent A and heavy results only to the
   existing `HumanoidRagdollRig` path.
6. Subscribe Agent A's transition owner to `PhysicalActionRequested`. A recovery
   step request is semantic; the transition owner may reject it and P2 must not
   play an Animator state directly.

No shipping scene, canonical prefab, Animator Controller, transition director,
or foot-IK controller is changed in this commit.

## Evidence contract

- EditMode: severity ownership; response-ID duplicate/wrap/eviction; rejected
  medium ownership without ID consumption; unstable support, missing feet,
  missing planted contact, Recovery/FullRagdoll and controller rejection;
  offset-stance and single-planted-foot hulls; COM-outside step; unreachable
  brace; semantic brace/reach/fall probes; every profile's zero leg drive;
  explicit joint-binding failure; canonical interruption consistency;
  30/60/120 response and muscle recovery; bounded energy; and zero steady-state
  managed allocation.
- PlayMode: default-off no-op, explicit canonical joint bindings, missing-contact
  fallback followed by same-ID eligible claim, stable-support medium activation,
  duplicate rejection, no added velocity, no root torque, and zero leg drive.
- Runtime telemetry: `LastPoweredImpactDecision`, `LastPoweredAssistOutput`,
  `PoweredActionRequestCount`, joint error/torque estimate, and profiler marker
  `Elemental.ActiveRagdoll.PoweredAssist`.

Unity execution and runtime visual/game-feel claims remain unproven until the
Director runs the focused EditMode/PlayMode suites and captures profiler and
playable-scene evidence on the integrated branch.

## Wave P3 boundary

Trajectory warping, action timelines, magic choreography, main-controller
changes, and shipping content wiring are outside Wave P2 and are not started.
