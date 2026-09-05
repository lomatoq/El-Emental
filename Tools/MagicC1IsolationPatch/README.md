# Gravity pre-contact C1 isolation

Staged outside Unity `Assets`; Unity has not imported or run it.

The direct source audit proves slot 6 is bounded at native sampling and reaches
about 54-61 degrees with the production clock, while the complete graph produces
repeatable 171-176 degree steps with hand IK disabled. This patch adds a narrow QA
seam that bypasses only generic C1 while keeping the real AnimatorController,
Humanoid retarget, A/B buffer, layer and clock.

Run after copying the matching files into `Assets`:

`Elemental/QA/Animation Gravity C1 Isolation Runtime Audit`

Expected report:

`BuildReports/AnimationGravityC1IsolationPlay.json`

Disabled mode passes exact controller input through the job and continually resets
the C1 state to that input. Re-enabling cannot restore a stale offset. The two
tests use controlled `Time.captureDeltaTime` at 30 and 60 Hz. A recorder installed
before activation captures only normalized `.145-.35`, requires a rate-derived
minimum number of real final-pose samples, verifies hand IK remains zero, and
records the final skeleton's maximum upper-body step. A zero-sample run fails.
