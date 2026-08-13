# ADR 0016: Contextual Earth input grammar

Status: accepted
Date: 2026-08-13

## Context

Earth bending previously read Unity input actions and physical devices inside multiple gameplay components. Gesture thresholds were expressed in pixels, wall/platform topology used a separate classifier, and ability digits could affect live presentation. That made behaviour depend on resolution and allowed a gesture classifier to run before the game knew whether the pointer represented terrain, a held rock, an intact structure, or broken Earth.

## Decision

- `EarthInputAdapter` is the sole device/action boundary. Runtime consumers observe the semantic actions `BendPrimary`, `BendForce`, `BendField`, `BendModifier`, `JumpOrStomp`, `BendParameter`, `Cancel`, pointer and locomotion values.
- Pointer strokes are stored in viewport coordinates with time and pressure. Hover never starts a stroke. Near-duplicate points are removed, the path is lightly smoothed, and recognition always operates on the profile's fixed resample count (32 by default).
- Input resolution order is source validity, active session, button/modifier context, simple features, relevant templates, N-best confidence and ambiguity, preview, then release commit.
- Active manipulation, vector force, gravity grip, and repair resolve before template matching. Only a terrain primary stroke admits wall/platform templates.
- Recognition returns best and second-best candidates, confidence, ambiguity gap, a complete feature vector, and a quantized geometry digest. Low-confidence or ambiguous results do not mutate simulation.
- Replay/network input stores resolved intent, stable source identity, quantized geometry, charge, wheel parameter, modifiers, ticks, seed and optional gesture digest. Raw pointer samples and screen pixels are not authoritative data.
- Digits remain editor/development forcing for non-Earth labs and never form the shipping Earth control grammar.
- The preview presenter owns render geometry while `MagicInputController` remains the temporary orchestration facade for legacy tests and other elemental labs.

## Consequences

The same physical gesture resolves identically across common resolutions and aspect ratios. Adding a target category or template requires an explicit context gate instead of expanding a monolithic classifier. The normalized feature extractor and recognizer use caller-owned buffers and allocate no managed memory after warm-up.

The legacy pixel recognizer remains available for the historical authored corpus until Air/Water input migrates. New Earth behaviour must use the contextual normalized pipeline.

## Rollback

The legacy `PointerPathSampler`, `GestureRecognitionPipeline`, and public screen-path helpers remain intact as compatibility adapters. Reverting the live Earth route to them requires no simulation or replay-schema migration, but would give up resolution-independent ambiguity handling.
