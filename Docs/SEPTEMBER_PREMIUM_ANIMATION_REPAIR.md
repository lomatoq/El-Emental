# September 6 character, respawn and presentation repair

Working tree on main `1235579`. Coordinated with the simultaneous mobility/wall
task; its pillar charge, cushion, wall and sonar changes remain intact. No full
humanoid setup or scene regeneration was run. Existing manually assigned
locomotion motions were serialized before the incremental import and verified
unchanged afterward.

## Physical and animation fixes

- A round restart before the first ragdoll previously restored uninitialized
  defaults for the live actor's collider/Animator/physics settings. The rig now
  captures its live animated state before that reset.
- `PlanetMotor.ResetAfterTeleport()` clears support/carry, jump/roll, mantle,
  velocity history and facing history. Its sequence invalidates presentation
  and foot-contact history for both duel actors after authoritative relocation.
- Pivot contact no longer ignores authored foot lift, sole clearance and the
  anchor reach limit. A turning foot releases its anchor instead of pinning a
  crossed or airborne leg.
- Foot probes abandon an old planted anchor when the authored foot has moved
  more than 30 cm away. A backward step can discover the lower floor instead of
  repeatedly probing the arena top behind its current foot position.
- Landing prediction rejects PhysX initial-overlap sentinel hits with zero point
  and distance. Those are not future floor contacts and previously predicted the
  world origin while the capsule still intersected the old ledge.

## Mixamo inventory and integration

Downloaded September 6 through the user's signed-in Mixamo session. Source:
[Mixamo](https://www.mixamo.com/), X Bot, FBX for Unity, Without Skin, 60 fps,
no keyframe reduction. The complete downloaded sources remain in the project;
the importer selects short spans at the source's original playback speed.

| Gap | Downloaded source | New controller state | Duration |
| --- | --- | --- | ---: |
| Settled idle into forward movement | Start Walking | Start Walk Transition | .32 s |
| Grounded release/cancellation of crouch | Crouch To Stand | Crouch Exit Transition | .38 s |
| Short step from a ledge, including backwards | Jumping — Male Jumping Down From2ftHighPlatformWithOneFoot | Step Down Transition | .36 s |

Existing useful sources already cover left turn, backward walk/run, strafe,
standing-to-crouch, falling, soft/hard landing, falling-to-roll and get-up from
front/back; these were preserved rather than replaced with duplicate downloads.

`EarthShortTransitionAuthoring.Install()` imports only the three named FBXs,
uses the shared Humanoid Avatar, extracts root motion and samples all eight
contact curves. `BuildReports/ShortTransitions/import-report.txt` records exact
source frame spans. It adds three unique states without replacing existing
motions, transitions or blend-tree children.

The pure `EarthShortTransitionPolicy` chooses bounded, cancellable slots. Motor
input remains immediate. Actual support, jumps, casts, mantle, ragdoll and landing
rolls retain priority. Predicted contact does not plant feet. The EAMM bridge
yields during each short state and its outgoing blend so it cannot overwrite the
imported animation.

## Parallel work

- [Native HUD design and screenshots](HUD_PREMIUM_POLISH.md): shared visual
  tokens, stronger text, readable bar values and timer, maintained local UI Toolkit
  components. Superdesign CLI was unavailable; this result uses the local system.
- [Clothing weights and accessory constraints](CHARACTER_RIG_REPAIR_SEPT06.md):
  Blender source/export evidence, head/waist weight repair, belt/plume spring
  simulation and analytic garment/head collision constraints.
- [Charge source coverage and camera envelope](CHARGE_FEEDBACK_SEPT06.md):
  smooth +6.5 degree maximum FOV, restrained camera shake and edge aberration,
  with cancellation/reset and existing accessibility settings respected.

## Evidence

**Final acceptance: `SeptemberPremiumPlay` 10/10 passed, 02:03:58 UTC,
48.824 s, Unity 6000.5.7f1.** This final combined run includes respawn, turn,
actual arena descent, all three new transitions, charge, HUD and both cushion
regressions after the contact forecast fix. Final `SeptemberTransitionsEdit`
29/29 passed at 02:02:34 UTC; source and scoped whitespace checks passed.

- `SeptemberPremiumEdit`: 37/37 passed (pivot 30/60/120 Hz, support authority,
  respawn solver).
- Initial `SeptemberPremiumPlay`: 5/7 passed, including live and knocked-out
  production respawn, HUD, turn/stop, and both other-task cushion regressions.
  Two follow-ups were isolated: stale idle-FOV expectation after a real airborne
  launch, and a descent fixture placed inside the north entrance arch with
  unscaled capsule seating/root-only teleport. Neither failure is hidden.
- `SeptemberTransitionsEdit`: initial 29/29 passed for charge and short-slot policy.
- Rig acceptance is recorded separately: 13/13 Edit and 1/1 production Play.

Focused follow-up at 02:01 UTC: `SeptemberPremiumRemainingPlay` 3/3 passed
(charge, corrected stand-up crop, physical short drop). The other focused run
passed ordinary full-keyboard start/return/reverse and actual arena descent.
The latter measured 1.129 m descent, 3.234 m outward travel and zero airborne
foot locks over 400 rendered samples. Short-drop trace proves a real lower-floor
candidate, active StepDown and immediate release at actual physical support.

Inspected actual gameplay captures: `ShortTransitions/01-start-walk.png`,
`04-grounded-charge.png`, corrected `05-crouch-exit.png`, `07-backward-step-down.png`,
`08-step-contact.png`, and `SeptemberPremium/BackwardEdge-Descent.png`.
Editor-only preview experiments under `ShortTransitions/Poses` failed retargeted
preview evaluation and are not visual acceptance evidence.

Final shared presentation marker during physical start/loop: 46 samples, mean 63.120 us,
p95 78.1 us, peak 79.6 us. This measures the two actors' presentation CPU marker,
not total frame or GPU cost. HUD and charge measurements are in their linked reports.
Whole-game visual perfection is not inferred from these scoped automated checks.
