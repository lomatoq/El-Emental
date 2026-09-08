# Paired-button earth stone combo

Implementation checkpoint: 2026-09-06, working tree based on `main`.
Implementation is installed in the saved production scene; scoped acceptance evidence is recorded below.

## Contract

The shipping paired-button route remains `EarthActionRouterBehaviour` â†’
`EarthDualMouseAbilityController.CastStompStone`. The separate double-LMB
extraction session is unchanged. One paired command admits one beat; at most one
additional command is buffered while that beat completes. Additional spam is
rejected, not replayed later. Completed beats run first punch, second punch,
left kick, right kick, spin kick, then repeat. A pause longer than 0.65 seconds
after recovery resets the sequence. Cancellation clears both progress and buffer.

The pure `EarthQuickStoneCombo` owns sequence, timing and clip-contact mapping.
Gameplay release remains a runtime clock decision; animation callbacks never
spawn a projectile. Presentation reads the same action time. The first two beats
now show boxing throughout preparation instead of showing pillar/pull clips.
Kick stones travel to the live Humanoid foot socket; release records origin,
socket and aim for validation. Aim is sampled from the camera pointer independently
of the visual body turn. A spin requests one bounded hop through `PlanetMotor`.

Semantic technique IDs 38â€“40 map to appended Humanoid slots 12â€“14. Original eleven
magic slots retain their paths and timings. Existing A/B clocks retain the outgoing
rendered pose. An independent, zero-default-weight full-body layer exposes the
legs and baked body orientation only during kicks. Its two states use the same
A/B clip clocks and crossfade boundaries as the upper-body layer; synchronizing
the masked upper-body layer was rejected after production tests showed hidden legs.
Foot IK releases the active leg, and both legs for the airborne finisher. Authored kick hands receive no
persistent aim IK. Duel binding cancels queued actions on death or round end.

Projectile damage is editable on the dual controller (defaults 8/12/18).
`StoneShotCommitted(float)` emits visual mana expenditure only after an actual
launch (4/10/20). Pool failure and rejected input produce no expenditure event.

## Installation and proof

`EarthQuickStoneComboAuthoring.ConfigureSavedController()` extends the saved
Animator and motion profile in place and installs the full-body mask. It requires
a baked Humanoid spinning clip and fails explicitly if absent. No scene rebuild
or source model regeneration is required.

Pure tests: `EarthQuickStoneComboTests` covers sequence, bounded queue, cancellation,
timeout, semantic slots, contact clock and invalid clock input. Asset tests:
`EarthQuickStoneComboAssetTests` requires both fourteen-child buffers, mirrored
kicks, dedicated non-looping Humanoid finisher and a correctly masked independent layer.

Production PlayMode tests `ProductionPairedStoneSeries30Fps`, `60Fps`, `120Fps`
exercise the saved scene controller with moving aim, require exactly five releases
and correct mana costs, validate foot release origin, visible bilateral kicking
and head clearance, and capture each contact to `BuildReports/QuickStoneCombo`.
These three rate-target tests drive the accepted-command adapter directly.
`PhysicalRapidPairedClicksReachSpin` separately queues actual paired mouse events
through the shipping Input System/router every 0.17 seconds, checks the first five
release beats and bounded post-release drain. Existing pure gesture tests remain
the input grammar gate. Contact and intermediate-turn captures supplement the
numerical source-handedness, foot-release, head-clearance and rendered-turn gates.

## Accepted scoped verification, 2026-09-06

`TestResults/DuelAcceptancePlay5.xml` passes **9/9** production PlayMode tests,
including five combo-specific gates:

- `PhysicalRapidPairedClicksReachSpin`: real queued LMB+RMB device events traverse
  the shipping Input System/router, reach the five-beat sequence and drain at
  most one accepted pending shot after button release. No stale tail replay.
- `ProductionPairedStoneSeries30Fps`, `60Fps`, `120Fps`: all pass five exact releases,
  correct mana expenditure, foot-origin launch, alternate leg lift, head clearance,
  bounded gameplay-root heading and the visible full-body revolution.
- `ShippingDualMouseChordStartsPromptlyAndDoesNotReplayAfterRelease`: original
  physical-input regression passes. Its unchanged **<0.14 s** post-release contact
  budget is now measured from the actual `StoneShotCommitted` event, rather than
  from the beginning of the newly visible boxing anticipation.

The latest accumulated rendered hips turns are **350.06° / 360.04° / 361.18°**
for the 30/60/120 FPS targets respectively. Exact records:
`BuildReports/QuickStoneCombo/{30fps,60fps,120fps}/4-SpinKick-yaw.txt`.
Each rate directory also contains the five contact PNGs, two mid-turn PNGs and
contact telemetry sidecars. Full-body weight reaches approximately 0.92; active
kick-foot IK is zero, and both foot weights are zero during the finisher.

Fresh 30/120 target left/right contact and mid-turn captures were reviewed after
Play5, supplementing the earlier 60-target review. They show distinct raised
attacking legs, coherent torso/head poses and the body turn. The authored spin
remains a baked variation of the licensed local MMA performance, not a claim of
new motion capture. Frame-rate values are requested test targets, not a measured
sustained hardware-FPS benchmark or blanket proof of every possible combat state.

Earlier failures are superseded: synchronized upper-body masking was replaced
by independent full-body A/B states; the physical-input fixture now admits device
events without editor focus and restores its settings safely. The accepted Play5
run includes actual paired input, rather than treating those fixture failures as
production gameplay evidence. Final EditMode gate results are recorded in the
project-wide acceptance report/tracker.
