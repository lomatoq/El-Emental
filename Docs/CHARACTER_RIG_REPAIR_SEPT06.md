# Linebreaker clothing and secondary motion — September 6

Scope: source clothing/head weights, helmet hair ownership and runtime belt/plume
motion. Gameplay, Humanoid mapping, primary animation, foot IK and respawn remain
owned by their existing systems.

## Reproduced defects

- Helmet/hair islands retained weights on upper-arm twist bones. Helmet flaps
  contained approximately 3.0 and 10.2 total vertex-weight units assigned to arms;
  arm motion therefore changed the helmet silhouette and exposed hair.
- The prior secondary paint pass visited only vertices already assigned secondary
  weights, leaving most of the combined hair islands untreated.
- The waist belt retained thigh weights while the two strips were hip children.
  This created inconsistent movement across overlapping garment surfaces.
- Runtime motion used only root linear acceleration. Constant-speed gait and
  animated head/hip turns could leave the accessories still. There was no runtime
  collision solve, and execution order 250 preceded final body/foot pose owners.

## Implemented contract

`HumanoidSecondaryMotion` now runs at order 2900, after the final body/foot/hand
passes and before the existing 3100 pose probe. Each chain samples its actual
animated anchor position and rotation. Filtered acceleration, angular inertia and
small velocity drag excite the existing bounded damped springs.

Rotation-only, three-pass projections preserve bone lengths while keeping belt
tips outside torso/thigh capsule envelopes and the skirt front plane. The plume
uses a helmet sphere that follows the head. Volumes derive from the configured
Humanoid, use explicit local references and do not create physics bodies or affect
movement/collision authority. Steady state has no new managed allocations.

Hair remains rigid under `Secondary_HairLock`. Ragdoll suspends pose writes;
discontinuities and re-enabling clear dynamic history. Reset reuses arrays.
Tuning remains serialized: spring frequency/damping, maximum angles, turn inertia,
velocity drag and collision clearance. Profiler marker:
`Elemental.SecondaryMotion.Step`.

The deterministic Blender pass rigidly locks all head/hair surfaces except the
black plume, removes arm leaks, and matches the belt/torso weight envelope. It
retains topology, UVs, material and the 50-bone rest skeleton. Because metal and
plume share a mesh island, the authored albedo separates cool dark plume faces
from warm helmet faces; the crown itself never receives spring weights.

## Source and reproduction

- Original `LinebreakerRigged.blend` and previous `LinebreakerRigged_weighted.blend`
  remain intact.
- New source: `ArtSource/Characters/Linebreaker/LinebreakerRigged_clothing.blend`.
- Run `Tools/Blender/audit_linebreaker_clothing.py` on the weighted source first,
  then `Tools/Blender/repair_linebreaker_clothing.py` on that same source.
- Candidate export: `BuildReports/CharacterRigRepair/Linebreaker.fbx`; promote to
  the existing runtime FBX while preserving its `.meta` and references.
- Both scripts were executed through the connected Blender MCP. The CLI-specific
  MCP entry lacked `BLENDER_PATH`; the connected MCP launched its own installed
  Blender binary in a background process. The unsaved interactive startup scene
  was left intact.

## Evidence and remaining gates

Blender report: `BuildReports/CharacterRigRepair/repair-report.json`. There are
3365 vertices; the maximum normalized-weight error is below `4.5e-8`.
Front/back captures cover neutral, forward/backward bend, kick/turn, head turn and
maximum secondary bend. Twelve PNGs were inspected. The first backward-bend
candidate exposed a jagged waist seam; matching belt/torso weights removed it.
Initial spatial plume selection also moved metal; albedo-aware selection removed
that deformation. FBX round-trip comparison retains world bounds within `1e-6`,
50 bones and the same rest transforms/material names.

Prepared focused validation:

- `SecondaryBoneSpringSolverTests`: existing finite/stable springs plus capsule,
  degenerate sphere, skirt plane and 30/60/120 Hz oscillation cases.
- `LinebreakerSecondaryMotionAssetTests`: actual imported Humanoid chains at
  30/60/120 Hz, visible head/hip response, preserved local positions, rigid hair
  and discontinuity reset.
- `HumanoidSecondaryMotionRuntimeTests`: 120 rendered frames on the shipping
  actor, actual ordered LateUpdate, controlled head/hip stress and JSON telemetry.

The repaired FBX is promoted and Unity import succeeds with the existing metadata
and Avatar. Import caught a unit-convention mismatch hidden by Blender's world
round-trip normalization; explicitly matching original `FBX_SCALE_ALL` removed
the warning without changing the Avatar. Fresh focused EditMode is **13/13**
at `2026-09-06T01:38:46Z`, `BuildReports/CharacterRigRepairEdit.{json,xml}`.
Measured imported-chain peaks at 30/60/120 Hz are tail **5.043/5.073/5.063°**
and belt **2.926/2.945/2.951°**; hair stays rigid, local bone positions stay exact,
and discontinuity reset returns to bind. Production Play passes **1/1** at
`2026-09-06T01:40:04Z`, `BuildReports/CharacterRigRepairPlay.{json,xml}`.
The actual ordered LateUpdate pass over 120 rendered frames measures **37.922°**
total tail travel, **13.998°** belt travel, **4.345°** maximum individual joint
angle and **119** collision corrections; hair remains rigid. Details are in
`BuildReports/CharacterRigRepair/runtime-proof.json`. This is controlled animated
anchor stress on the shipping actor, not a measurement of ordinary walking gait.

Additional integration checks executed in the same coordinated Editor window:
Charge Feedback Edit **17/17** and September Premium Edit **37/37**. Existing
`EarthChargeCameraLookdevV2.duel` was bound to the scene's duel controller and
saved. The shipping scene was restored clean, out of Play, after testing.

The initial Edit launch waited on Unity's native scene-save modal. A separate
scene copy proved that the only new dirty delta was camera FOV float formatting
`60.000004 → 60`. After preserving that copy and reviewing the exact diff, the
safe save was accepted and all tests completed. No approval remains blocked.
Blender captures are source-pose evidence, not proof that every
production motion or extreme ragdoll pose is intersection-free. Collision uses
bounded analytic garment envelopes rather than full mesh cloth/self-collision.
The imported-asset and runtime gates above pass; broad gameplay-camera movement
QA remains with the integrating task. Runtime CPU/GC profiling has a dedicated
marker but no measured CPU budget is asserted by this report.
