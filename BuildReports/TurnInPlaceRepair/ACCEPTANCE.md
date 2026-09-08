# Turn-step evidence — 2026-09-06

## Accepted turn proof: 18:50:13 UTC

`ProductionTurnStepsCompleteShortTapsAndSustainedHalfTurns` passed **1/1 with all four scenarios**, including unchanged strict floor thresholds across every turn, Capture and exit sample. Five pure sequence tests previously passed. Turn animation and floor ownership are accepted for this production scenario. Separate irregular-slope work remains pending in the foot lane; this result does not claim its acceptance.

| Scenario | Clip weight | Actual yaw | Whole-sequence min ankle | Whole-sequence min toe | Released-Swing min ankle |
|---|---:|---:|---:|---:|---:|
| Left tap | 1.000 | 23.8° | 35.0 mm | −1.5 mm | 37.0 mm |
| Left 180° | 1.000 | 180.2° | 35.0 mm | 10.5 mm | 37.0 mm |
| Right tap | 1.000 | 13.6° | 25.2 mm | −12.4 mm | 37.0 mm |
| Right 180° | 1.000 | 180.2° | 21.9 mm | 3.1 mm | 37.0 mm |

All samples retain the same ankle >=15 mm / toe >=−20 mm acceptance margins. The reported small negative toe values remain inside the predeclared solver tolerance; this is not a claim of mathematically zero toe penetration. Released-Swing toe minima are positive (9.0–26.2 mm). Clip weight reaches 1.0, actual horizontal swing remains, the short tap completes after key-up, both sustained turns reach 180°, and all four return to idle. No controller, original FBX, walk/run assignments or model assets were replaced.

The bounded foot fix follows the actual pose owner: zero-contact Swing uses the authored bone trajectory, while partial contact uses the current Animator IK goal blended toward the existing support target. Geometric safety projects only upward with a 2 mm solver skin and the existing 220 mm cap. Captured anchors and contact weight ramps remain unchanged. Earlier goal/bone telemetry isolated why either source alone missed a different phase; no arbitrary animation timing or threshold relaxation was used.

Visual review of fresh frames0004/0011/0039/0048/0054/0059/0064 confirms visible transfer, knee articulation, free foot lift and the removal of the previously buried rear shoe during exit. The frames nearest the worst right-side clearance are0039/0054. Foreground arena geometry partly obscures the left sustained turn in frames0018–0034, so those individual views are not unobstructed foot proof; its final bone-to-floor measurements still pass across the whole scenario. This limitation is retained rather than treating numeric yaw as visual evidence.

Final artifact: `Proof/TurnSteps.mp4`, **74 fresh original PNGs, about 7.91 s, 640×800 H.264, 884559 bytes**, complete decode PASS. `VideoFrames.json` maps sampled timing without interpolation. `TurnStepFrames.json` and refreshed `FloorFrames.csv` contain final IK-goal/bone/floor telemetry. All current PNGs are from the 18:50 run. Earlier evidence is preserved in `ProofBeforeSwingFloor`, `ProofSwingGuardOnly`, `ProofCaptureExitFirst` and `ProofGoalDiagnostic`.

## Diagnostic history

## Whole-sequence follow-up: 18:33:30 UTC

The Capture/exit extension removes the previously deep penetration in the measured left-tap case: minimum final ankle is now **+5.352 mm**, toe **+2.220 mm**. The stricter ankle margin of 15 mm still fails at .361750 s (left Stance, contact .34, geometric correction zero). This is a small remaining final-IK clearance discrepancy, not the earlier −86 mm sink. Fresh PNGs0003/0004/0011 show both shoes instead of the buried rear shoe. Only frames0000–0013 are fresh; the remaining PNGs and MP4 still belong to the prior four-case run and must not be presented as this result.

The test originally stopped after that first scenario. The pending next proof defers whole-sequence floor assertions until all four cases are recorded, preserving the exact 15 mm ankle / −20 mm toe thresholds. Foot lane is investigating the difference between physical-bone position and Animator IK goal during acquisition. No acceptance or animation-clip change is inferred from this partial run.

## Latest floor proof: 18:16:40 UTC

Production TurnSteps passed 1/1 with all four directions/durations and the geometric swing safeguard. Authored steps themselves are accepted: full clip weight, visible articulation, completed tap steps, both real 180° turns and return to idle. Original manual animation assignments are unchanged. **Final contact acceptance remains open:** the strict released-Swing gate passes, but Capture and the transition to idle still contain visible shoe penetration.

| Scenario | Released-Swing min ankle | Released-Swing min toe | All-frame min ankle | All-frame min toe | Largest floor lift |
|---|---:|---:|---:|---:|---:|
| Left tap | 35.0 mm | 7.4 mm | −86.1 mm | −82.6 mm | 184.8 mm |
| Left 180° | 35.0 mm | 22.0 mm | −58.6 mm | −61.4 mm | 89.2 mm |
| Right tap | 35.1 mm | 21.4 mm | −56.1 mm | −74.3 mm | 56.8 mm |
| Right 180° | 35.0 mm | 24.7 mm | −51.9 mm | −48.6 mm | 133.2 mm |

Exact problem samples in `Proof/FloorFrames.csv` / `TurnStepFrames.json`: left-180 at .573626 s, right foot Capture/contact .17; right-180 at .840538 s, left Capture/contact .17. Left-tap at 1.160995 s has already left the turn (step false, clip weight zero) while the right foot remains Swing/contact zero. Right-tap at 1.156708 s has right Stance/contact .13174 during acquisition. These samples are outside the current genuine full-weight Swing assertion, so its green result does not establish clean whole-sequence contact.

Visual inspection confirms the measurements: frames0011 and0048 show a rear shoe entering the floor on exit; frame0059 shows a clearly lifted swing foot with the support shoe entering the stone. Frame0019 shows the left-turn acquisition pose. The independent foot lane owns the remaining Capture/exit geometric safeguard; do not dilute or replace the accepted turn clip to hide it.

Latest `Proof/TurnSteps.mp4` contains 74 original PNGs, 640×800 H.264, about 7.84 s, 903014 bytes; full decode succeeds. `VideoFrames.json` preserves sampled timing (no interpolation). Previous 17:27 evidence and video are archived intact under `ProofBeforeSwingFloor`. The video is diagnostic proof, not final clean-contact acceptance.

## Previous accepted turn sequence

Root applied the step-completion patch and new sources. Five pure tests passed as part of LatestEdit 15/15. Production `ProductionTurnStepsCompleteShortTapsAndSustainedHalfTurns` passed **1/1 at 17:27:47 UTC**, covering all four cases below. Source: `BuildReports/AlphaTurnStepsPlay.xml` and `Proof/TurnStepFrames.json`.

| Scenario | Turn clip weight | Final foot vertical separation span | Actual body yaw | Released contact |
|---|---:|---:|---:|---:|
| Left short tap | 1.000 | 0.1642 m | 17.0° | 0.000 |
| Left sustained turn | 1.000 | 0.1083 m | 181.6° | 0.000 |
| Right short tap | 1.000 | 0.1265 m | 13.6° | 0.000 |
| Right sustained turn | 1.000 | 0.1987 m | 180.2° | 0.000 |

All four completed the step and returned to idle. Actual turn clip length is 1.0333334 s. Recorded presentation multiplier and Animator speed were both 1.0 throughout. Real EAMM override yielded to the turn; the clip reaches full weight instead of fading to idle on key release.

Visual review of frames 0000/0005/0010/0012/0018/0027/0042/0054/0061/0068 confirms full-body framing and actual foot articulation: frame0042 shows a lifted forward foot; frame0061/0068 show transfer and heel lift during the opposite turn. These are rendered feet, not only state or yaw telemetry. Manual controller/FBX/walk/run assignments remain unchanged.

**Contact limitation discovered in that review:** frames0005/0010/0012 and0018 show parts of a shoe entering the stone floor during released authored swing. Root-relative foot separation alone can increase from sinking; it is insufficient for nonpenetration acceptance. The independent foot lane now supplies an upward-only floor safeguard. The enhanced pending proof separately raycasts the actual final ankle and toes against the floor, requires ankle clearance >=15 mm and toe >=−20 mm during genuine released Swing (`Reason == Swing`, unlocked, contact weight<.2), preserves >25 mm horizontal swing travel, and records the geometric correction. That stricter proof must pass before claiming clean turn contacts.

Video: `Proof/TurnSteps.mp4`, 640×800 H.264, 7.76 s, 73 recorded PNGs plus the final concat hold frame. Original sampled timing is retained within each scenario; pauses between scenarios are trimmed. No interpolation or generated poses. `Proof/VideoFrames.json` maps every frame to its scenario/timestamp. The MP4 decodes completely without errors. Encoder script: `encode-proof.py` using existing C:/Python312 imageio_ffmpeg.

Earlier 17:19:47 run failed the arbitrary 2.3 s test deadline at normalized turn phase **0.994880855**. Phase advanced monotonically at approximately .43548 cycles/s, consistent with a .45 clock, but that run did not record the relevant clock properties. The fresh run records 1.0 and passes without a runtime clock patch. The earlier timing cause remains unproven; do not describe a speculative Animator/playable clock fix as implemented.
