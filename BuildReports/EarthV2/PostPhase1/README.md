# Earth Core V2 Phase 1 evidence

Captured: 2026-08-14  
Unity: `6000.5.7f1`  
Player: fresh Windows Release, 1280×720, D3D11, RTX 4070

## Automated gates

- EditMode: 195 passed, 0 failed, 0 skipped in 1.300 s (`TestResults/EarthV2-EditMode-Final3.xml`).
- PlayMode: 66 passed, 0 failed, 0 skipped in 127.972 s (`TestResults/EarthV2-PlayMode-Final3.xml`).
- Development: 171,138,506 bytes, 76.496 s, 0 warnings/errors.
- Release: 106,015,551 bytes, 55.817 s, 0 warnings/errors.
- Release EXE SHA-256: `D05C5848F7CC2EB32FF6016F54762EE3C666A69B862BE297DF2390830C1EA8CF`.

## Captures and SHA-256

- `day-explore.png` — `400428F399199C3FAB0399ACB59505574EB6AAE6DCE1B01897175CB95E38E7D7`
- `wall-rise.png` — `C20912DAF7D7BC92F4A1BFABFA353C68FB027A4581F8442757D804D49530C5F5`
- `locomotion-cast.png` — `19E76485B8D652CAAE3ED82F071AF497929EEE77FE5BCD8F056A416DE70E3048`
- `dawn.png` — `277BB00B9B7B70BDDB077FCC05DB26B2726002533DD7071AD455D80FE179B9AA`
- `night.png` — `327E7EEC4A8C1305C462825230BC9A539F101235C9FB78B94162224F358EFBBA`
- `earth-material.png` — `F8BE8FAD2156483655116C2A372F5F06DF2D5BF30ED4229E0A5B45DE59D79BFB`

## Visual verdict

- Pass: daylight is blue instead of black and stars are absent.
- Pass: dawn and night have distinct readable grading; night restores stars and scaled-space bodies.
- Pass: elevated camera reveals substantially more surface in front of the caster.
- Pass: wall emergence no longer uses the Rigidbody root; automated root drift is below 0.02 m and safe collider activation is proven.
- Remaining: wall silhouette/material is still too monolithic for the final action-showreel target; this belongs to later kinetic wall and shader phases.
- Remaining: the Mage cloak/hat dominates the back silhouette and needs technique-specific full-body animation/presentation in later phases.

## Performance note

The automated Release frame-timing request returned no `FrameTimingManager` samples, so this evidence does not claim a measured steady-state CPU/GPU result. Cold-start voxel queue peak ranged from 65.22 to 70.44 ms with zero pending work after settling; it is startup evidence only.
