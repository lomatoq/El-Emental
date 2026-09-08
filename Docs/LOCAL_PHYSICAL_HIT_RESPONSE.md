# Local physical hit response

2026-09-06, uncommitted working tree on `main` / `1235579`. This contract replaces
the torso-spring presentation described in `IMPACT_WEIGHT_READABILITY.md`; the
existing mass/speed normalization and post-normalization root shove remain.

## Ownership

`EarthCharacterImpactTarget` resolves one normalized impact and one typed world
response ID, applies health/outcome, then delivers local flinch/stagger to
`HumanoidLocalizedPhysicsResponse`. Weak reactions do not interrupt movement or
authored actions. Medium staggers gate motor commands and cancel routed abilities
for 0.24 seconds. Heavy outcomes retain the existing recoverable/full ragdoll
and health-owned death paths.

The local response prewarms eleven dynamic bone proxies and eleven kinematic
animation-target anchors per Humanoid, with `ConfigurableJoint` linear and Slerp
drives. Proxies have explicit mass/inertia and no collision participation. Only
the nearest struck region and its parent (0.4 transfer) become dynamic. An actual
`AddForceAtPosition` impulse supplies translation and angular displacement;
targets follow the sampled animation pose. Drive strength stays weak for 0.12s,
then recovers over 0.5s. The motor's translating/rotating frame transports the
small physical response so running does not leave it behind in world space.

Animator/EAMM, locomotion and foot contact evaluate first. A struck leg's foot
lock is released before IK. The local physics adapter writes the bounded final
physical offset at execution order 2500; accessories follow at 2900. It restores
its preceding local offset before the next animation evaluation. The old
`HumanoidRagdollRig` AngleAxis pose writer and procedural torso impact kick are
removed from this ownership path. Existing authored clips are unchanged.

Heavy handoff retains the currently rendered pose instead of resampling Animator
at time zero. Animated target velocity and active local proxy velocity feed the
eleven real ragdoll bodies before local proxies suspend. Teleports clear the
local simulation. Recovery returns the adapter to animation-target following.

## Tuning and installation

`Elemental/Setup/Install Local Physical Hit Response` binds the shared impact
profile to Humanoid targets in the active scene, adds the response adapter, and
saves that scene. Runtime prewarming happens before the first hit.

`Elemental/Tuning/Impacts & Stones` edits the actual impact profile, wall profile,
and rock splitting profile. Local drive strength/damping, release/recovery times,
parent transfer, stun duration, displacement/angle ceilings and full ragdoll
launch limits and separate head/torso/arm/leg ceilings are exposed. Mass policy
is now the editable `EarthMatterMassPolicy.asset`, explicitly bound through the
world kernel and pools; the calculator previews that actual asset with m³, kg,
kg/m³, m/s and N·s labelled. See `SHARED_STONE_MASS_POLICY.md` for installation,
creation versus child-conservation boundaries, and accepted 7 Edit/3 Play tests.

## Verification status

Focused tests added: `EarthLocalizedPhysicsResponseTests` (envelope, graph,
stun/severity policy), `EarthLocalizedPhysicsRuntimeTests` (actual PhysX and
visible displacement in all eleven regions, duplicate event rejection, bounded
return, weak/medium control gate). The existing production stone-stagger capture
test now measures the physical owner and verifies pose/velocity handoff to heavy
ragdoll. It writes `BuildReports/StoneStagger` including `HeavyHandoff.png`.

The frontend-aware test helper enters the real Play-vs-Bot transition after
readiness, before test input/bot overrides. Scoped whitespace checks pass.
Coordinated Unity 6000.5.7f1 validation: `AlphaLocalImpactsEdit` **24/24 passed**
(14:31:54 UTC, 0.145s suite duration); `AlphaLocalImpactsPlay` **3/3 passed**
(14:33:32 UTC, 16.515s suite duration), with no logged warning/error in that Play
report. These suite durations are not runtime frame-cost measurements.

The production Before/Peak/Recovery/Settled/HeavyHandoff images were inspected.
This strong torso/pelvis stagger visibly bends and recovers without observed
mesh tearing or handoff pose snap. Measured local physical angle peaks at
27.384 degrees at 0.181s, retains 18.533 degrees at 0.305s, and settles below
0.05 degrees by 0.574s. The impact includes the existing root shove; its movement
in the fixed capture framing must not be mistaken for pure bone displacement.
The eleven-region fixture proves body/bone displacement, while these images
visually validate the particular torso/pelvis hit, not every region's appearance.
Profiler markers: `Elemental.Character.LocalPhysicsStep` and
`Elemental.Character.LocalPhysicsPose`. Physics bodies/joints/arrays are
preallocated. The separate 96-frame production performance window passed:
mean 0.008851ms LocalPhysicsStep (p95 0.0362ms) and 0.080747ms LocalPhysicsPose
(p95 0.1103ms) for both fighters; 96 FixedUpdate and 162 LateUpdate allocation
brackets measured **0 bytes**. Active responses occupied 91 of 96 measured frames.
Native PhysX solver work is outside these callback markers.
Whole-frame/editor allocation is reported separately and is not attributed to
these callbacks. See `BuildReports/LocalPhysicsAcceptance/Performance.txt`.
Regional thrown-stone capture acceptance remains separate. The preserved collision
relative velocity is oriented against both bodies' cached pre-physics motion,
so either callback supplies the same incoming direction without rebound motion.
The earlier run caught a leg projectile bouncing off the arena before arrival.
The corrected fixture uses five regions (head and both arms/legs), resets the real production actor on
a test-only physical platform above arena obstacles, and verifies grounded support
and a clear sphere flight corridor. It retains actual projectile/collider contact,
exact anatomical region assertions, single event, visible displacement and recovery.
CSV includes response severity and first surface, with fixed full-body framing.
`BuildReports/LocalPhysicsAcceptancePlay.json` **2/2 passed**, 16:19:49 UTC on
Unity 6000.5.7f1. The corrected fixture isolates canonical Animator skinned meshes,
asserts 40�80% screen height with unclipped head/feet, and records all five actual
contacts. The recaptured head/arm/leg peak and head before/settled frames were
inspected. Five MP4s encode the original 48 frames at 30 FPS without interpolation.
The canonical event now carries EarthHitRegion; physics consumes that typed region,
with nearest-bone resolution retained only for legacy Unspecified events.
AlphaLocalImpactsPlay also passed 3/3 at 16:12:40 with the final incoming-velocity
orientation and shared mass policy, including full-ragdoll handoff.
Frames, CSV and encoded MP4 files are under `BuildReports/LocalPhysicsAcceptance`.
This adds actual projectile contact and recovery proof to the eleven-region
synthetic PhysX checks. Full-body footage uses a test-only clear physical platform
inside the production scene, not the ordinary obstacle-filled arena floor.
