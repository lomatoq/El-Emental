# Sequential material repair and heavy downward stone contact

Working-tree implementation, 2026-09-06. No commit claimed. AlphaLatestEdit passed
26/26 at 17:29:44 UTC, including the initial repair/crush pure tests. The first
physical run at 17:52:37 UTC passed five contact/lifetime cases and failed two
repair cases. Repair2 corrects those observed failures and adds actual HP/death
and destroyed-skeleton lifetime assertions. Repair2 passed 8/9 at 18:22:19 UTC;
the remaining initial-oblique-hit fixture precondition was corrected using the
already validated physical overhead crush. RepairVisual passed 4/4 at 18:30:59
UTC. Numerical acceptance is separate from the wall capture correction below.

## Material repair ownership

Arena repair previously called `ReattachPiece` directly from gesture progress,
which immediately replaced every requested cell's position and rotation. Progress
now requests a count; `EarthArenaStructure.TickMagicRepair` flies one available
foundation-connected cell at a time. Flight takes at least 0.28 seconds, extending
by distance at 8 metres/second, with smooth acceleration/deceleration. Only arrival
commits that cell's original pose and graph bonds. Missing/shattered cells are not
manufactured. Releasing the middle-button session cancels the pending count and
returns every reserved, unwelded cell to a collidable dynamic body at its current
pose. Waiting cells are held at their captured poses, preventing them from falling
and shattering before their turn; only the active cell travels.

`MagicExecutor` must not run its attraction field while the gesture requests
repair. Otherwise its next fixed update reacquires waiting arena pieces and adds
a second motion owner. `IsRepairActive` includes pending arena repair so input and
presentation see the actual operation. Wall reassembly preserves the existing
dynamic PD final alignment and settle gate after each bounded flight. All selected
unwelded cells are reserved up front; capture events identify the single active
flight, and the next flight waits for the previous weld. PD compares world COM
against the transformed rest COM, rather than confusing COM with Rigidbody origin.
Platform repair likewise holds waiting cells and advances one flight at a time.

The wall bevel profile has separate `WallWidthMeters` (0.0525 m) and
`WallMaxLocalEdgeFraction` (0.25) fields. Intact, cracked and ejected cell render
meshes share these values in metric space. Generic stone bevels retain 0.02 m and
0.08. The installer `Elemental/Setup/Apply 52.5mm Wall Bevels` saves only the two
wall fields on the existing shared asset.

## Heavy downward contact

The ordinary reduced-mass stone impulse remains unchanged. Its bounded transfer
means a slowly falling huge stone can remain below the normal thrown-stone
knockdown threshold. Crushing is now an explicit load case: source mass at least
100 kg and twice the receiving root mass, contact above the receiver's centre of
mass, incoming direction downward by at least 0.65 dot, and downward closing speed
at least 2.5 m/s. It requests recoverable full ragdoll; health still independently
determines death. It adds no damage multiplier, and the existing 1.4 displacement
multiplier still applies exactly once after normalized resolution.

Uncached structural/decor collision direction is oriented by the receiving
contact normal, using incoming collision velocity rather than the rebound body
velocity. Arena, wall and platform dynamic cells resolve as loose stone sources.
The existing full-ragdoll handoff releases the motor capsule and enables the
eleven physical bone colliders, preserving the established single pose owner.

The manual huge-piece report exposed two additional callback-order hazards.
`MagicExecutor.HandleFragmentImpact` could publish the body's rebound direction
before the receiving collision callback, causing the valid incoming hit to be
deduplicated afterward. It now uses the fragment's stored incoming velocity.
Actual wall/arena/platform cells and decor/debris now call
`EarthStoneCharacterContact.Deliver` before their own fracture/deactivation path.
The receiver retains the shared damage policy and deduplication. Earth targeting
validity is deliberately not a condition for receiving damage: characters remain
ungrabbable while still receiving physical stone contacts.

## Settings map

These new scalar defaults currently live in the pure contracts, not inspector
assets. `Simulation/Combat/EarthCharacterImpact.cs`, `IsHeavyCrush`, owns minimum
source mass 100 kg, source/receiver mass ratio 2, downward dot 0.65 and minimum
downward closing speed 2.5 m/s. Contact height must be nonnegative relative to the
receiver COM. `Simulation/Structures/EarthRepairFlight.cs` owns minimum flight
duration 0.28 s and distance/duration scale 8 m/s. The existing wall
`EarthRepairProfile` still owns its PD gains, staging distance and settle
tolerances. Editable common mass conversion and body reaction limits remain in
`Elemental/Tuning/Impacts & Stones`; the new crush defaults are not falsely
presented there as editable controls.

## Quick-stone lifetime correction

`EarthMatterIdentity.Configure` compares actual kernel object references rather
than Unity's overloaded null equality. A destroyed previous kernel must not leave
a registry-local handle that aliases the new world's first unrelated record.
Separately, cancelling an unlaunched quick stone explicitly retires its transient
representation before returning the shell. It never submitted a subtractive
terrain transaction. `EarthFragmentPool` skips inactive shells whose canonical
matter remains live; being invisible is not permission to overwrite material.
Consumed shells retain the normal registry recycling path.

## Verification

Repair2 outside-Unity Roslyn compilation passed for complete Simulation and Runtime
and focused Play/Edit source, including both new tests. Runtime reports seven
previously present CS0618 warnings in rescue, pillar mobility and platform pool
files; this delta adds no warning locations. Unity execution is coordinated by the
main task.

Focused acceptance:

- `EarthRepairCrushTests`: continuous minimum-duration travel; mass/speed/direction
  crush eligibility; incoming collision direction.
- `LocalPhysicsProductionAcceptanceTests.HeavyFallingStoneCrushesPlayerIntoDynamicRagdoll`
  and `HeavyFallingStoneCrushesBotIntoDynamicRagdoll`: actual 600 kg falling rock at
  6 m/s, ordinary and typed callback routes, one canonical event, downward handoff,
  eleven dynamic colliders, disabled motor capsule and measured downward rig travel.
- `OuterStoneRingRuntimeTests.RepairFliesOneCellAtATimeAndReleasePreservesCurrentPhysicalPose`:
  actual gesture executor plus authored cells, no immediate weld, one flying piece,
  no field recapture, interrupt without teleport, and full reassembly afterwards.
- `EarthReassemblyRuntimeTests.BakedWallPhysicallyReassemblesAndRestoresIntactProxy`:
  next capture follows previous weld while the existing exact restoration checks
  remain in force. Earlier OuterStoneRing tests now wait for asynchronous travel
  before checking the preserved exact local poses and support bonds.

Additional tests now cover actual large arena and wall cells thrown into the bot,
using their real collider mesh and policy-resolved mass without a mass override;
five consecutive quick-stone cancellations; destroyed-kernel handle aliasing; and
inactive shell ownership. Falling-rock tests now also save 960×540 frame sequences
with canonical skinned-renderer bounds and an initial 40–80% screen-height gate.
Frames are not created inside the separate local-physics performance window.

Runtime arena flight marker: `Elemental.Earth.ArenaRepair.Flight`; existing wall
repair profiler markers remain. New runtime timings have not yet been measured.


## Observed contact evidence and follow-up

`BuildReports/AlphaRepairCrushPlay.json`, 2026-09-06 17:52:37 UTC: 5/7 passed.
Actual policy-mass arena cell (1298.399 kg) and wall cell (130.343 kg), each thrown
at 25 m/s into the production bot, emitted exactly one recoverable-knockdown event.
The 600 kg, 6 m/s QA falling sphere drove the player's physical rig downward
0.7702 m and bot's 0.5517 m; all eleven colliders were dynamic and the root capsule
was disabled. Five cancelled quick-stone cycles safely retired and reused the
same shell. Saved crush images show downward folding, but the projectile is a QA
sphere with EarthFragment physics, not a production-authored rock visual.

The failed arena completion exposed unreserved waiting cells shattering before
travel. The wall reached only 5/40 welds in 30 seconds while remote waiting cells
fell farther away. Repair2 addresses these concrete causes; the original full
restoration assertions and timeout remain, and missing material is not invented.

A further code trace found that full ragdoll deliberately unparents the visual
rig. Parent-only collider searches then lost the health receiver for subsequent
hits. `EarthStoneCharacterContact.ResolveTarget` now follows the rig's explicit
motor owner; source fragments, typed collisions and swept fragment contacts use
that resolver. The original character target still owns health policy and dedupe.
The new physical repeated-stone test must reduce bot HP to zero, emit knockout,
and increment score. Large arena/wall tests now also require HP loss.

`HumanoidLocalizedPhysicsResponse` validates its whole bone/proxy lifetime before
pose/physics access, disposes only its owned invalid proxy root, and allows explicit
Configure to rebuild. It does not catch and hide destroyed-skeleton exceptions.
`EarthLocalizedPhysicsRuntimeTests.DestroyedBoneLifetimeStopsPoseWritesAndExplicitConfigureRebuilds`
exercises teardown followed by a fresh bound skeleton and physical response.


## Final numerical contact and repair evidence

`AlphaRepairCrushPlay.json`, 18:22:19 UTC, 8/9: arena repair and complete 40-cell
wall repair passed; destroyed-skeleton teardown/rebind passed; both actual large
structural throws lost bot health (arena 100 to 76; wall 100 to 77.92). Both
falling-crush cases and cancelled-shell recycling remained passing. Only the new
repeated-hit fixture incorrectly assumed an oblique initial sphere/capsule graze
must cause full ragdoll. Its first HP assertion had passed. No runtime threshold
was relaxed to satisfy that fixture.

`AlphaRepairVisualPlay.json`, 18:30:59 UTC, 4/4: the repeated fixture starts with
exactly the accepted physical 600 kg / 6 m/s overhead crush, then launches actual
25 m/s stones toward the current detached rig's chest. Six contacts reduced health
100, 93.27, 69.27, 45.27, 27.54, 3.54, 0. Every contact retained the detached physical
rig; the final response was Knockout, with one bot KO and one player point.
`BuildReports/LocalPhysicsAcceptance/RepeatedActualStoneKill.txt` contains each
accepted direction and HP value. This verifies damage through detached bone
colliders, not only a synthetic reaction event.

The first wall capture restored 40/40 cells in 14.408 simulation seconds but is
**not visual acceptance**: an ordinary first hit correctly left cells nearly in
place, and the isolated Game camera incorrectly included planet atmosphere.
The follow-up capture fixture uses the real full MMB disassembly path, waits for
actual gravity displacement above 0.15 m, and uses the existing compositing-test
Preview camera convention. Actual geometry/materials remain; runtime code is
unchanged. The follow-up passed and was visually inspected as recorded below.


## Accepted visible sequential return

`BuildReports/AlphaWallRepairPlay.json`, 2026-09-06 18:37:48 UTC: 1/1 PASS after
real full MMB disassembly and actual gravity movement. 40/40 cells restored in
20.433 simulation seconds. The first active cell's measured error decreased
3.1322, 2.6235, 1.5152, 0.4334 m before physical seating. The same next-capture-after-
previous-weld assertion remains, and the final intact proxy, all bonds and original
cell parenting were restored.

190 current PNG frames were encoded at 10 fps into
`BuildReports/SequentialRepair/Wall/SequentialWallRepair.mp4`; labels use actual
`Frames.csv` stage, seated count, active piece, phase and simulation time. Visual
inspection of frames 60, 100 and 150 shows separate closed pieces arriving and
building the wall from the lower region upward. Frame 189 restores the full
original wall extent and matching bevel/cap geometry. Initial displaced pieces
extend above this cropped view; entry and individual seating are visible. The
isolated Preview camera preserves actual authored meshes/materials, but is not
acceptance of final in-game colour grading. No runtime changes followed Repair2.
