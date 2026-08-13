# ADR-0006: Mass-aware charged magic push

Status: accepted  
Date: 2026-08-12

## Context

Earth interaction needs a simple force verb that works before a player learns the three authored gestures. A continuous force while RMB is held gives weak release timing and tends to erase the perceived difference between a loose stone and an anchored wall.

## Decision

- RMB press starts a bounded charge; release performs one targeted push along the camera ray.
- A tap starts at 18% charge, a 0.7 second hold exceeds 65%, and a three second hold exceeds 98%.
- Dynamic rigidbodies receive a true impulse. The same impulse therefore produces less velocity change as target mass increases.
- A wall has no Rigidbody. Its equivalent mass is computed from committed length, height, thickness and an earth density constant. It responds through a bounded damped offset and lean, preserving its anchored-wall role. A serialized 12.0 wall-leverage coefficient converts the same caster effort into a readable anchored shove without removing the mass division.
- Target acquisition uses a bounded non-allocating 0.65 m sphere cast around the cursor ray. This preserves mass behaviour but makes a thin wall selectable without pixel-perfect aiming; a miss emits actionable HUD text.
- `MagicPushEvent` carries charge, target mass, resulting velocity change and target kind. Presentation consumes it for dust, HUD state and a camera impulse weaker than a heavy collision.
- Charge vibration is presentation-only and never mutates canonical simulation state.

## Consequences

Tap and charged release share one predictable input, loose objects remain more responsive than heavy construction, and feedback communicates stored force without turning the camera into simulation authority. Target selection remains a bounded non-allocating raycast.
