# Project Charter — El-Emental

Updated: 2026-08-14  
Current gate: Vertical Slice

## Non-negotiable promise

> The player feels like a powerful, physical earthbender by pulling mass from a living tiny planet, shaping it with gestures, and hurling or breaking it while gravity, inertia, fracture and camera composition make every action readable and heavy.

## Player and product

- Audience: players who want expressive elemental action and systemic physics.
- Platform: Windows PC, keyboard and mouse first.
- Target hardware: reference 1080p PC defined in `Docs/performance-budgets.md`.
- Session length: 10–25 minute sandbox sessions during vertical-slice development.
- Business model/release window: not locked; prove the toy first.

## Core toy

- Verb: draw, raise, grip, push, fracture, repair.
- Target: local planet surface, intact earth structures, rocks and fracture pieces.
- Modifiers: charge duration, cursor gesture, mass and technique modifier.
- Constraint: local spherical gravity, limited readable screen space, real mass/cohesion.
- Consequence: structures emerge, move, collide, fracture and leave persistent physical debris.
- Ten-second signature moment: raise a wall from a drawn chord, catch its fractured pieces, then throw the cluster through another structure.

## Product identity

1. Earth has weight: motion anticipates, accelerates and settles.
2. Magic is spatial: cursor gesture and camera composition expose cause and result.
3. Destruction remains controllable: fracture is physical material, not disposable confetti.

- Must not become: a generic spell hotbar, a voxel-tech demo, or a black-space physics sandbox with unreadable primitives.
- Visual thesis: stylized warm stone against a saturated blue atmospheric sky; clear silhouettes and restrained particles.
- Audio/motion thesis: low-frequency mass, short sharp fracture transients, visible anticipation and recovery.

## Current milestone

- Decisive uncertainty: can stable foundations make wall emergence, camera framing, sky and character motion immediately readable?
- Golden path: launch `EarthCoreSlice`, explore, raise a wall, move while the Mage animates, observe stable root/collider and blue daylight.
- Acceptance: wall root drift <0.02 m; collider enables without planet penetration; Explore camera 7.0–7.8 m and 3.4–4.2 m high; daylight stars = 0; real KayKit clips/Avatar/controller pass the asset gate.
- Kill criteria: any solution that reintroduces physics-root transform animation, direct device reads outside the input adapter, or silent placeholder animation data.
- Explicit cut line: V2 Phase 2+ abilities (quick cast, redesigned wave, surf) wait until Phase 0/1 evidence passes.

## Technical envelope

- Engine: Unity 6000.5.7f1, URP 17.5.0.
- Target: 60 FPS / 16.7 ms at 1080p Native High; zero managed allocations in declared hot loops.
- Authority: pure simulation contracts and typed events; MonoBehaviour adapters at runtime boundaries.
- Required fallback: procedural/runtime fracture fallback remains debug-only; sky keeps a deterministic shader fallback.
- Unsupported: runtime planet resize, HDRP, VFX Graph, ECS and third-party physics.

## Decision rules

1. Fix authority and collision ownership before adding spectacle.
2. Prefer one proven playable path over breadth without evidence.
3. Preserve deterministic simulation and pooled/non-alloc runtime paths.

## Done for this gate

- [ ] Player evidence: post-change golden-path capture is readable.
- [ ] Technical evidence: EditMode, PlayMode, Development and Release pass.
- [ ] Art/readability evidence: day, wall-rise and locomotion captures beat the recorded baseline.
- [ ] Performance evidence: relevant markers exist and no budget regression is observed.
