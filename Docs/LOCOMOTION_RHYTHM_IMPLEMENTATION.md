# Motor-authoritative locomotion rhythm

Updated 2026-09-06. Current user locomotion clips and controller assignments are preserved. The motor owns physical displacement; EAMM, Animator and foot IK consume measured motion without moving the character root.

## Runtime contract

Each fixed tick publishes support-relative actual and requested tangent velocity, support identity, grounded state, distance and phase through `LocomotionMotionSample`. Ordinary grounded movement receives a 0.95–1.05 phase pulse. Mantle, external launch, roll and impact stun keep their existing ownership. Actual feedback, requested movement and phase distance use the same support plane. Traction counters tangential gravity inside the existing acceleration budget. Production motor capsules use the existing `CharacterFrictionless` material.

The active clip's measured contact interval supplies cycle distance. Distance phase advances from physical travel; source changes seed the shared phase, then bounded cadence feedback aligns animation without pose jumps. Playback remains 0.8–1.25 and residual stride 0.9–1.1. Effective cycle distance includes stride scale. Stride changes only free swing targets; support anchors stay in support-local coordinates. Actual visible leg length normalizes the measured reference rig rather than multiplying by the avatar's transform scale blindly. Held intent against an obstruction returns to idle after 0.2s of insufficient actual movement.

Foot contact metadata uses measured height and planar travel: low forward return is swing, including flat shuffles. Each foot has independent rising-edge contact phase. Moving capture uses 0.10s with a maximum 0.17 weight increment per rendered frame; idle remains 0.40s/0.12. Released swing has zero terrain-contact weight. Impacted legs release contact before the localized physics owner runs. Acquisition is measured separately from a settled stance; a 10% capture blend is not treated as a planted foot.

Geometric pelvis compensation accounts for diagonal leg reach within the unchanged 0.22m drop cap. A locomotion plant that remains outside the captured rest-chain length even at that cap releases through the existing 0.12s hysteresis. Its fixed anchor does not slide, and the next tick cannot immediately recapture it. This keeps motor-authoritative travel from stretching a shin toward an impossible trailing anchor.

Authored turn/start/stop and idle transitions use a collision floor through Swing, Capture and partial stance. At zero contact the current bone trajectory is the authored input; with partial contact the Animator's internal IK goal is the blend input. The normal contact blend is formed first, then only an upward floor correction is added. Ankle and toe probes use a 2mm solver skin and a 0.22m maximum correction. Above-ground travel, original curves, rotation weighting and support anchors remain. Full-authority stance, active EAMM lower-body motion, flight and localized-hit ownership are excluded from this correction.

EAMM retargeting accounts for intermediary helper-bone rotations. Short looping clips have derived repeated cycles with a full prediction horizon. Queries have explicit per-user-clip tags, safe stale-database detection and exact tagged search if adaptive sampling misses a short valid interval. Invalid pose index -1 cannot enter inertialization. Fractional transitions start from actual evaluated output and target the same fractional pose that will render, including overlapping blends.

A hot reload can preserve the initialized flag while native query buffers are gone. Runtime readiness and search now validate both managed state and native allocations. Missing state suspends callbacks, reports unavailable readiness and logs once. A fresh scene entry performs normal initialization; this guard does not attempt an unbounded shared-database/rig rebuild in the middle of Play.

Current source clips contain isolated large foot angular changes. The visible base bounds only LeftFoot/RightFoot rotation at 1200 degrees/s using one budget per rendered frame. Hips, thighs and knees are not filtered. Finite, upright and bone-step gates remain active; correction counts and peak raw/corrected angles are reported. This does not edit the original clips.

`EarthAnimationDriver.SetPresentationClockMultiplier(0.45f)` slows the authored playable and EAMM presentation clock for menus; restoring 1 resumes normal presentation. It does not change simulation time or root motion authority.

## Authoring and persistence

- **Elemental → Character → Install Locomotion Rhythm Metadata** measures the current Locomotion blend children into a separate catalog and installs adapters. Source clips, importers and controller assignments are unchanged.
- **Elemental → Character → Bake EAMM From Current User Locomotion** replaces only derived idle/locomotion recipes with those assignments. Other action, pivot and recovery recipes remain. Synthetic root travel uses a fixed sampled direction; every baked velocity must match measured nominal speed. Readback verifies every query tag and a full cycle of prediction-valid frames before reporting success.
- **Elemental → Character → Repair Locomotion Capsule Materials** restores the existing frictionless material on scene motor capsules with Undo and marks the scene dirty. Save the scene after authoring or repairing references.

The catalog's in-place speed estimate uses measured foot excursion and clip duration. The live EAMM adapter uses the selected database clip's measured speed/contact interval. Missing or stale derived data is explicitly reported; it is not accepted as a ready production source.

## Accepted evidence

Unity 6000.5.7f1; targeted bake/readback passed at 16:43:45 UTC; combined Edit batch 42/42 passed at 16:44:01 UTC. Final production cadence and presentation-clock Play tests passed 2/2 at 19:03:38 UTC after the reach and floor changes. Reports are in `BuildReports/AlphaCadencePlay.xml` and `BuildReports/LocomotionRhythm/`.

| Production actor | Physical cycles | Actual/requested mean | Settled left/right samples | Peak settled anchor error | Peak capture error / duration |
| --- | ---: | ---: | ---: | ---: | ---: |
| Planet Character | 10.011 | 0.999379 | 9 / 43 | 0.0122 m | 0.0673 m / 0.0724 s |
| Rumble Linebreaker Bot | 10.013 | 0.996191 | 24 / 59 | 0.0167 m | 0.0746 m / 0.0733 s |

Both means satisfy the unchanged 1% limit. Both feet reach actual settled IK authority. Capture reach ratios were 1.0527 and 1.0793; acquisition has separate finite time, accumulated blend and anatomical reach gates. The test measures real Rigidbody motion with gravity and production colliders on an isolated support. It disables the bot's arena-return safety only in this elevated test lane: that lane lies outside the arena and otherwise introduces a second radial velocity writer. An assertion verifies that writer remains inactive. Runtime arena safety is unchanged.

Pure checks cover phase/rate/stride bounds, independent contacts, low and flat swing detection, ten-cycle mean, traction force limits across slopes, fractional transition continuity and the two-foot-only angular bound. The presentation test measures the actual EAMM frame clock while simulation time stays unchanged. Real world turn/stop and moving-support regressions have passed. The later targeted Edit batch passed **32/32 at 18:59:00 UTC**, including rest-chain reach, release hysteresis and the stale-reload single-warning regression.

The turn proof passed all four short-tap/sustained-turn scenarios at **18:50:13 UTC**, with unchanged whole-sequence ankle >=15mm and toe >=-20mm gates. Worst ankle clearance was 21.9 mm; worst toe clearance was -12.4 mm, inside the predeclared solver tolerance rather than mathematically zero penetration. Released swing ankles stayed at least 37 mm above the surface. Fresh screenshots confirm repaired rear-shoe exit and visible foot transfer. See `BuildReports/TurnInPlaceRepair/ACCEPTANCE.md` for exact scenarios, video and the partially occluded left-turn views.

The real pit/hump/slope matrix passed **1/1 at 19:01:18 UTC**, covering both production actors at controlled 30/60/120 Hz animation timesteps. These are controlled animation steps, not a performance claim of achieved display FPS. Each entry includes a genuine pit-stop contact; all entries complete the hump, pit and slope.

| Actor | Step Hz | Settled samples | Max drift | Max absolute normal gap | Highest swing clearance |
| --- | ---: | ---: | ---: | ---: | ---: |
| Player |30|12|0.926mm|1.024mm|224.5mm|
| Bot |30|36|0.960mm|1.015mm|340.2mm|
| Player |60|29|0.929mm|1.019mm|303.7mm|
| Bot |60|115|1.015mm|1.023mm|285.9mm|
| Player |120|55|13.090mm|1.065mm|315.6mm|
| Bot |120|195|1.027mm|1.304mm|299.5mm|

All remain inside the existing 15 mm drift, -10..+15 mm normal-gap and 360 mm maximum swing-clearance limits. `BuildReports/SeptemberAnimation/ActualSurfaceControlledSteps.json` retains frame-level targets, final bones, support identity, pelvis and source ownership. The runtime exposes `AnatomicalReachReleaseCount`; this report does not serialize that counter, so an exact release count cannot be reconstructed from this acceptance run.

## Limits and references

In-place speed and contact metadata remain geometric estimates, not semantic animation labels. Rate/stride caps deliberately limit correction when a source gait cannot match physical speed. The clear-lane mean is not a promise to preserve travel through obstacles, high-friction materials or external gameplay forces. Offline tests do not prove network prediction/reconciliation: authoritative motor/support state must remain the replicated source; presentation phase can reconcile from distance without writing body state.

Contact anchors and continuity follow the constraint-oriented motivation of [Kovar, Gleicher and Schreiner, Footskate cleanup for motion capture editing (2002)](https://graphics.cs.wisc.edu/Papers/2002/KSG02/). A common periodic coordinate is motivated by [Holden, Komura and Saito, Phase-Functioned Neural Networks for Character Control (2017)](https://www.pure.ed.ac.uk/ws/files/35467734/phasefunction.pdf); this implementation is a bounded phase adapter, not their neural network. Acquisition and settled stance are separated because [Unity's Animator.SetIKPositionWeight](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.SetIKPositionWeight.html) blends between the authored pose and the IK target. Reach constraints are consistent with the structure documented for [Unity's Two Bone IK constraint](https://docs.unity.cn/Packages/com.unity.animation.rigging%401.0/manual/constraints/TwoBoneIKConstraint.html).
## Earth body targeting and held armor (2026-09-06)

Character root bodies, disabled proxies and detached ragdoll bones are excluded from Earth matter acquisition, gravity capture, vector manipulation and return. `PhysicalImpactTarget` remains a physical damage receiver; implementing that interface no longer makes a character a controllable rock. Explicit independently simulated stone/armor matter remains eligible even when parented to an actor. Four focused Edit cases passed in the 15/15 batch at 17:16:15 UTC. Ordinary impact callbacks and stone acquisition are covered; future Water control is not implemented.

Held armor now reconciles the MMB level as well as the release edge before firing or spreading. Missing a transient release edge cannot leave armor active after the button is up. The production Shift+MMB input, ordinary animation, release and jump test passed in the 4/5 world batch at 17:21:55 UTC. The separate slope matrix subsequently passed at 19:01:18 UTC.