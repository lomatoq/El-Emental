# Risk Register — El-Emental

Updated: 2026-08-14

## Active

### R-001 — Physics activation still intersects noisy planet surface

- Category: technical
- Probability: medium
- Impact: high
- Leading indicator: wall root moves laterally/vertically at collider activation or receives an impact event without an external hit.
- Mitigation: foundation-aware collider inset, non-alloc overlap and `Physics.ComputePenetration` gate, PlayMode acceptance on a planet collider.
- Fallback: keep structure kinematic/collider-disabled and emit a validation error rather than depenetrating explosively.
- Status: mitigated in Phase 1; keep regression coverage active.

### R-002 — Elevated camera harms precision gesture drawing

- Category: design
- Probability: medium
- Impact: high
- Leading indicator: surface ray misses or straight gesture classification worsens in wall tests/playtest.
- Mitigation: bounded pointer dead zone, existing normalized gesture projection, scene corpus and cursor-extreme tests.
- Fallback: state-specific DrawStructure composition without reverting Explore.

### R-003 — Imported animation appears valid but does not communicate locomotion/casting

- Category: art/technical
- Probability: medium
- Impact: medium
- Leading indicator: valid Avatar/controller yet no visible speed/state change in runtime capture.
- Mitigation: fail-fast asset validator plus runtime Speed/state test and locomotion capture.
- Fallback: retain the existing primitive pose driver only as a development fallback.

### R-004 — Sky ownership conflicts with celestial/atmosphere presentation

- Category: technical/art
- Probability: low
- Impact: medium
- Leading indicator: black frame, duplicated material instances, or stars visible in full daylight.
- Mitigation: `EarthSkyController` is the single `RenderSettings.skybox` owner and exposes test state.

### R-005 — Final action readability remains below showreel target

- Category: art/design
- Probability: high
- Impact: medium
- Leading indicator: wall still reads as a monolithic dark slab and the Mage back silhouette hides hand motion in QA captures.
- Current evidence: `BuildReports/EarthV2/PostPhase1/wall-rise.png` and `locomotion-cast.png`.
- Mitigation: preserve as explicit Phase 4/8 work; do not hide it with particles before kinetic wall and action poses exist.
