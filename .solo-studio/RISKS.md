# Risk Register — El-Emental

Updated: 2026-08-30

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

### R-006 — Procedural foot contact passes the arena but fails general terrain

- Category: animation/technical
- Probability: high until the terrain corpus is captured
- Impact: high
- Leading indicator: simultaneous locomotion locks, repeated lock transitions without
  a swing re-arm, applied-IK step above 0.30, knee discontinuity, or anchor error after
  support ID/generation changes.
- Mitigation: one pure per-foot stance FSM, per-foot weight/hint blending, raw frame
  telemetry and the flat/slope/step/ridge/moving-support/sphere corpus in ADR 0033.
- Fallback: restore authored locomotion with ordinary foot IK at zero while retaining
  cast/surf locks and all diagnostics.

### R-007 — Shadow cleanup flattens geometry or hides contact

- Category: technical art
- Probability: medium
- Impact: medium
- Leading indicator: bright detached vertical faces, missing cast-shadow shapes,
  peter-panning at feet/rocks, or noisy AO bands in quiet stone areas.
- Mitigation: authored arena normals, restrained side-shadow fade, bounded light bias,
  High soft-shadow filtering, clean contact AO and before/after 1920×1080 captures.
- Fallback: retain authored normals/SMAA and disable only contact AO; never restore
  radial normals on architecture to conceal shadow acne.

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
