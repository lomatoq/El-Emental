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
  when the runtime adapter has `PlanetMotor.HasStableSupport` and both authored
  foot transforms. Heavy (`RecoverableKnockdown`/`Knockout`) resolves to
  `ExistingFullRagdoll`; P2 emits no ragdoll command and no additive kick.
- P1 full-ragdoll/recovery interruption, live-support, pose-match, marker, and
  ownership handoff contracts are unchanged.

## Feature gate and exact fallback

- `EarthPhysicalAnimationProfile.UsePoweredPhysicalAssist` serializes `false`.
  The accepted Gate 1 profile asset is not modified by Wave P2.
- With the flag off, `ActiveRagdollPuppet` uses the original support probe,
  original joint drives, original balance torque, and original impact path.
  `ReceiveAcceptedWorldResponse` is a no-op.
- With the flag on, powered balance reads `PlanetMotor.HasStableSupport`, builds
  a four-point polygon from the two authored feet, and suppresses the legacy
  root-body balance torque. No powered behavior applies force or torque to the
  pelvis/root body.

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

`Reactive` and `Stagger` use zero leg drive and zero leg torque. Powered joint
targets therefore cannot fight authored leg animation or foot IK during a
medium response. The energy estimator clamps work to the authored torque cap
and angular limit. `Ragdoll` has zero drive in every region.

Built-in profiles are the explicit default. Enabling custom profiles requires
exactly one entry for every profile used at runtime; a missing or duplicate ID
raises an actionable configuration error instead of silently substituting data.

## Director wiring after review

1. Keep the existing `EarthPhysicalAnimationProfile` gate off until a Director
   validation fixture is ready.
2. Assign every `ActiveRagdollJoint` an explicit `EarthBodyRegion`; the serialized
   migration default is `Chest` so an old prefab remains valid but is not ready
   for powered production tuning.
3. Call `ActiveRagdollPuppet.ConfigurePoweredPhysicalAssist` with the profile and
   read-only humanoid left/right foot, head, and hand transforms.
4. Route each accepted `EarthWorldResponseEvent` once through
   `ReceiveAcceptedWorldResponse`. For a returned `PoweredPhysicalAssist` owner,
   suppress Agent A's procedural angular response for that same response ID.
   Continue routing `Flinch` only to Agent A and heavy results only to the
   existing `HumanoidRagdollRig` path.
5. Subscribe Agent A's transition owner to `PhysicalActionRequested`. A recovery
   step request is semantic; the transition owner may reject it and P2 must not
   play an Animator state directly.

No shipping scene, canonical prefab, Animator Controller, transition director,
or foot-IK controller is changed in this commit.

## Evidence contract

- EditMode: severity ownership, response-ID duplicate/wrap/eviction, stable
  support, COM-outside step, unreachable brace, semantic brace/reach/fall
  probes, no leg drive, canonical interruption consistency, 30/60/120 response
  and muscle recovery, bounded energy, and zero steady-state managed allocation.
- PlayMode: default-off no-op, stable-support medium activation, duplicate
  rejection, no added velocity, no root torque, and zero leg drive.
- Runtime telemetry: `LastPoweredImpactDecision`, `LastPoweredAssistOutput`,
  `PoweredActionRequestCount`, joint error/torque estimate, and profiler marker
  `Elemental.ActiveRagdoll.PoweredAssist`.

Unity execution and runtime visual/game-feel claims remain unproven until the
Director runs the focused EditMode/PlayMode suites and captures profiler and
playable-scene evidence on the integrated branch.

## Wave P3 boundary

Trajectory warping, action timelines, magic choreography, main-controller
changes, and shipping content wiring are outside Wave P2 and are not started.
