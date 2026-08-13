# ADR 0017: Six Earth technique contract

Status: accepted
Date: 2026-08-13

## Context

Earth Core already contained physical implementations for held rocks, walls, platforms, mobility pillars, traveling pillar waves and structure reassembly. They were reached through several historical input routes and did not share a replay-safe vocabulary for rejection, timing or presentation. Adding another spell dispatcher would duplicate authority and risk diverging preview, replay and physics.

## Decision

- The shippable Earth vocabulary is fixed to Grip, Wall, Platform, Pillar, Ground Wave and Repair before optional techniques are considered.
- `EarthTechniqueRouter` is a pure context resolver. It consumes semantic buttons, source category, grounding, gesture topology and validity gates; it never reads devices or scene objects.
- Every accepted technique produces an `EarthTechniqueCommand` with quantized primary/secondary parameters, stable source identity, seed and geometry digest. Rejection uses a closed reason enum and never mutates simulation.
- All six techniques share Intent, Anticipation, Release, Impact, Settle and Complete presentation stages. `EarthTechniquePresentationProfile` authors their timing, pose effort, bracing, camera impulse/look-ahead and dust/chip/rumble response independently of canonical physics.
- Existing physics remains authoritative: LMB manipulates or forms Earth, wheel selects the contextual parameter, RMB supplies force, MMB selects repair/gravity interaction and Space supplies the grounded pillar verb.
- RMB plus an LMB terrain sweep now reaches the existing bounded moving-crest wave pool. It uses normalized gesture recognition, a ground-only gate and an explicit grounded/runtime rejection rather than a hotbar slot.
- Wall wheel input controls height; Shift plus wheel controls thickness. Platform wheel controls lift height and Shift plus wheel records the tilt parameter for the presentation/geometry layer.

## Consequences

The six techniques can be tested and replayed without constructing Unity input devices. Presentation work can subscribe to one stable lifecycle and profile instead of inferring pose or camera state from GameObject names. Existing wall, platform, gravity-grip and repair physics are reused, so the task does not introduce a second damage, fracture or reassembly authority.

## Rollback

The pure contract and profile can remain as telemetry even if a live contextual route is disabled. The legacy Shift+Space wave and direct public test bridges remain available during migration.
