# Charge accumulation readability, September 6

Working tree on main `1235579`. This extends the previously accepted nine charge
sources without changing gameplay energy, release timing or camera controls.

LMB earth acquisition/plucking and pending wall/platform drawings accumulate
matter or wall power without entering the RMB-only `BendPhase.Charging` state.
The old camera reader therefore omitted these active accumulation routes.
`MagicInputController.AccumulationCharge01` now exposes that active-session
snapshot using the existing held-time curve. It returns zero after the pending
operation ends; carrying a previously formed stone does not hold the effect on.
The new tenth `Accumulation` channel joins the strongest-source aggregation.

FOV maximum increases from 6.5 to 10 degrees and edge chromatic aberration from
.18 to .30. The monotonic ease-out amplitude starts visibly during a short charge
(20% charge requests 3.6 degrees) and levels off at its bounded maximum. The
existing exponential entry/release, 6.5 mm / .14 degree maximum gentle shake,
accessibility settings, KO/round reset and SRP transform restoration remain.

The production test now verifies URP's actual volume stack consumes chromatic
aberration and the projection matrix consumes the charged FOV, alongside the
existing rendered neutral/charged/released captures and reset assertions.
Pure tests cover all ten sources, simultaneous-source bounds and brief onset.
Shipping scene values were promoted and saved by the coordinating root. Fresh
Space production Play is **1/1 PASS**, 08:37:31 UTC: rendered FOV moves 60 to 70,
URP's consumed chromatic stack and projection gates pass, then release returns to
the current airborne base of 64. Root inspected the actual rendered widening and
green/purple edge separation. The physical-LMB wall fixture separately drives a
real drawable surface with only LMB, checks the new accumulation source without
entering RMB Charging, and verifies release: **1/1 PASS**, 08:41:55 UTC, maximum
FOV offset **9.606 degrees**, release cleared. Its capture is
`BuildReports/ChargeFeedback/04-lmb-wall-accumulation.png`. Pure charge validation
is **19/19 PASS**, 08:38:27 UTC. The Space charged render was also inspected by
the character/charge agent. No gameplay energy values, authored clip assignments
or main-camera control bindings changed.
