# Shared subtle material camera feedback

User rule: major abilities, heavy impacts/high falls and large structures emerging or sliding receive consistent, very subtle camera feedback. No audio changes.

`EarthChargeCameraLookdevV2.ConfigureMicroShake(EarthMaterialFeedbackHub)` explicitly binds the shared material event bus; saved field `materialFeedback` must reference the camera's scene hub. No scene search is introduced. The existing reversible SRP begin/end render pose owns this effect, preserving simulation and aim camera transforms between renders.

`EarthMaterialMicroShake` filters footsteps, rolls, repair seating, tiny contacts and low impacts. The existing Land strength is fall speed / 7: below 7 m/s produces no micro-shake; 14 m/s is stronger. Emergence, assembly, fracture, impact, release, extraction, wave contacts/bursts, repair completion and large sliding friction share distance falloff (full near 2 m, zero at 24 m).

Events merge by maximum strength, independent of particle counts/source station count. An 80 ms global pulse spacing and decaying envelope prevent eight wall friction stations or a radial wave adding eight/thirty-six shakes. The full envelope is bounded to 2.5 mm translation amplitude and 0.055 degrees rotation amplitude per axis. It is max-composed with existing charge feedback rather than added. Existing camera ShakeIntensity scales it; Reduced Motion, pause and match gating clear it. Heavy ragdoll landings remain eligible even while charging is disabled.

Focused Edit fixture: `Elemental.Tests.EditMode.EarthMaterialMicroShakeTests` (9 cases). Covers station aggregation, simultaneous abilities, heavy vs low landing, distance, invalid inputs, exact rest and accessibility. Included in the final LandingRowEdit regression (136/136, 2026-09-08T21:25:03Z). The production Play fixture verifies saved hub binding and a real emitted emergence cue reaching the camera envelope.
