# Charge feedback — September 6

Working-tree implementation; test execution and visual acceptance are recorded below
only after the coordinated Unity run. This change does not claim game-wide acceptance.

## Result

`EarthChargeCameraLookdevV2` now reads the directed player's actual charge owners.
The previous V2 explicitly forced its charge lens offset to zero and only read
`BendCharge01` / stored `BendAmount01`; several independent abilities therefore
could not build tension, while merely carrying matter could keep it active.

All supported charge sources use one bounded envelope: maximum +6.5 degrees
vertical FOV, 6.5 mm position noise, 0.14 degrees rotation noise and 0.18 stock URP
chromatic aberration. The latter separates colour toward the screen edges while
keeping the centre clear. FOV and chromatic maxima are serialized on the existing
camera component. Charge attack/release time constants are 100/130 ms, and maximum
charge chooses the strongest overlapping source rather than summing camera effects.

The existing Cinemachine virtual lens remains the authored base. The post-Cinemachine
offset is bounded and never accumulated into that base. Shake is applied only during
the existing SRP render callback pair and its transform is restored after each render.
The existing camera accessibility profile controls shake and FOV motion; reduced
motion also disables chromatic aberration. Existing atmosphere and sonar code is untouched.

## Technique coverage

| Technique / input | Canonical presentation source | Release / cancellation |
| --- | --- | --- |
| Charged held earth, throw, charged construction | `BendPhase.Charging` + `BendCharge01` | Other phases excluded, including remembered charge in Holding |
| Standalone RMB push | Existing per-frame `PushChargeChanged` | Zero event or expiration after one missed input frame |
| Directed ground-wave gesture | Existing `UpdateGroundWavePreview` charge event | Commit/cancel zero event and the same expiration |
| Held vector field | `MagicExecutor.IsVectorFieldActive` + `VectorFieldCharge` | Invalid/inactive target excluded |
| MMB cluster compression / throw | `IsGravityClusterThrowCharging` + `GravityClusterThrowCharge01` | Charging flag clears |
| Split held-boulder compression / throw | `IsHeldFractureThrowCharging` + new read-only `HeldFractureThrowCharge01` | Charging flag clears |
| Resonance stone volley | `EarthResonanceController.IsCharging` + `Charge01` | Volley/release is not charge |
| Ordinary held Space pillar jump | `EarthPillarMobility.IsCharging` + `Charge01` | Release, cancellation or disabled owner |
| Surf + held Space directed pillar jump | Same canonical pillar owner | Same cancellation path |
| Radial pillar wave | `EarthPillarWaveAbility.IsCharging`, max sector/power charge | Release/cancel clears charging |
| Held LMB+RMB pillar crest | Read-only `DualMouseEarthGestureSolver.CrestCharge01`, forwarded by router | Canonical tracked crest length; zero outside chord and after commit/cancel |

Armor wheel phase, passive held matter, surf cruising, landing cushion and the quick
tap combo do not have an accumulating release-charge contract. They do not sustain
the charge envelope merely because an object or pose remains active. Their existing
commit/impact presentation remains under its original owner.

Input suspension, disabled motor, external ragdoll authority and a blocked bound duel
immediately reset the camera envelope. The adapter exposes `BindDuel` and listens to
`RoundRestarted`; the saved camera's `duel` reference must point to the scene's existing
duel controller. There is no presentation dependency from runtime gameplay. Local
player references come from `EarthCameraDirector.Player`, with no new world search.

## Validation entry points

`Elemental/QA/Charge Feedback Edit` runs `EarthChargeFeedbackTests`: 9 source
channels, three frame rates, strongest-source aggregation, accessibility/neutral,
invalid snapshots, and crest commit/cancel. Output: `BuildReports/ChargeFeedbackEdit.xml`.

`Elemental/QA/Charge Feedback Play` runs a production-scene physical Input System
Space hold. It checks actual camera FOV and stock URP chromatic intensity, positive
shake amplitudes, SRP pose restoration, adapter disable/re-enable, ordinary release,
input suspension and zero allocation during 10,000 read-only source samples. It
writes `BuildReports/ChargeFeedbackPlay.xml` plus neutral/charged/released PNGs and
sampling timing under `BuildReports/ChargeFeedback`. This is a physical test of the
shared pillar path, not physical-input proof of every technique in the table.

The existing `Elemental.Presentation.Clarity` profiler marker includes the new charge
sampling/envelope. A sampling-only CPU measurement must not be reported as full post
processing GPU cost. No new shader or render pass is introduced.

Verified September 6: `SeptemberTransitionsEdit` 29/29 passed at 02:02 UTC,
including all 17 charge cases and 12 transition cases. The charge production case
passed in `SeptemberPremiumRemainingPlay` (3/3 scoped cases, 02:01 UTC).
`ChargeFeedback/runtime-evidence.json` records neutral 60 degrees, charged 66.5,
released authored airborne 64, and 10,000 source samples with zero allocated bytes
in 2.033 ms. The release expectation uses the current Cinemachine lens. Suspension
uses `duel.SetRoundReady(false)` because the puppet legitimately restores allowed
component flags each physics tick. Neutral/charged images were inspected: wider
framing and restrained peripheral colour separation retain a clear aim centre.
