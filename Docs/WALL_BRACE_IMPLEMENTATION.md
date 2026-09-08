# Natural wall contact — implementation and acceptance

2026-09-06, working tree on main. Physical PlayMode acceptance passed in
`TestResults/DuelAcceptancePlay5.xml` (nine-test integration run, 9/9).
Visual recapture and marker measurements are queued separately.

`EarthWallBraceState` is pure presentation admission: forward intent above .25,
relative forward speed below .65 m/s, reachable wall contact sustained .12 s.
Weight enters over .15 s and releases over .12 s. This state cannot change
physics, input, damage or automatic mantle admission.

`EarthWallBracePresenter` queries a fixed 12-hit buffer per arm, rejects self,
triggers, floors and oblique faces. Captured hand points stay in collider-local
space. Unreachable or destroyed contacts cannot keep an arm anchored. The
existing stable `EarthAnimationRigBridge` arm constraints own the contacts;
Humanoid IK is used only when that rig is absent. Casting, surf, jump, mantle,
impact and ragdoll retain priority. Actual shoulder-to-wall measurements exposed
the guarded body's asymmetric reach: approximately .508/.646 m versus .50/.52 m
available arm reach. A body constraint now evaluates before the arms, aligns the
shoulder line within 30 degrees of yaw and leans the spine forward at most 18
degrees. It reads the current authored stream each evaluation, with no cumulative
offset, root translation, bone stretch or separate head aim. Candidate probes
include .18 m of torso reach; hand weights still require real anatomical reach.
The existing foot ownership is untouched. Marker:
`Elemental.Character.WallBrace`; no steady-state managed collections are created.

Added seven EditMode admission/release cases in `EarthWallBraceStateTests` and
the production-Humanoid physical PlayMode test
`BlockedForwardMovementBracesRealWallAndReleasesHands`. The physical test passed
with both hands within the unchanged .25 m wall-contact gate, moving-wall contact
retained, and full release after movement intent stopped. It now captures the
actual gameplay camera to `BuildReports/WallBrace/Braced.png` and `Released.png`,
plus a 30-frame marker/contact sidecar. Final recapture passes 1/1 in
`TestResults/DuelWallVisualFinal.xml` at 23:29:32 UTC September 5. Both images
were inspected: coherent body/head and hands return to the resting pose after
release. Actual left/right hand distances are 0.02153/0.02907 m. The existing
wall-step marker averages 21.39 microseconds over 30 measured samples, maximum
56.0 microseconds; this excludes the rest of the animation graph and rendering.

The prior moving-platform mantle hand-distance failure is separate. Production
already transforms its ledge anchor through the support collider. The untracked
`Tools/AirborneMantleFix/hand-reach` proposal additionally changes airborne
approach trajectory; it was inspected but has not been copied into production by
this change. It requires its own physical clearance and hand-contact validation.
