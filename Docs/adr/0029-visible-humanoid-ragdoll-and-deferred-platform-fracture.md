# ADR 0029: Visible Humanoid ragdoll and deferred platform fracture

Status: accepted (2026-08-26)

## Context

MVP 0.1 had two false handoffs. Player KO moved a hidden proxy while the rendered
X Bot remained outside the ragdoll, and the rival was one capsule. Platform cast
also synchronously built and cooked up to 48 fracture colliders, producing a
measured 187 ms main-thread stall. Billboard dust used opaque/no-alpha materials,
so its quad boundary rendered as black squares.

## Decision

- Both fighters use the same `HumanoidRagdollRig` on the rendered X Bot skeleton.
  Eleven authored bodies cover pelvis, chest, head, upper/lower arms and legs.
- Animator owns every bone while the bodies are kinematic and their colliders are
  disabled. KO captures the current pose, disables control and Animator ownership,
  enables bone physics atomically and applies the launch to the visible skeleton.
- The hidden player puppet remains an impact/gameplay adapter, but is suspended
  while the visible rig owns KO. It never writes the visible ragdoll bodies.
- After 3.5 seconds the presentation uses a bounded stone fade, then resets root,
  bones, joints, velocities, collision state and Animator in one operation.
- `EarthPlatform` has explicit Emerging, Stable, PreparingFracture,
  FractureReady/Fractured and Failed phases. Cast only updates the reusable solid
  mesh and walkable collider.
- Six platform roots and 48 piece shells per root are scene-authored. Runtime cast
  does not create GameObjects, Rigidbodies or MeshColliders.
- Pure fracture topology runs off the main thread. The main thread reuses one mesh
  per shell and prepares exactly one convex cell per frame. An early impact is
  retained and replayed when preparation completes.
- Stable platforms publish zero surface velocity. Rider registration/collision
  work is bounded to the emergence/grace window; preview updates are cached by
  stroke geometry.
- Platform carry canonicalizes every overlap to one `PlanetMotor`, so the hidden
  physical-assist limbs cannot multiply the same carry impulse. Collision returns
  only after the root capsule is inside its ground-probe band with no separating
  velocity; sibling assist colliders remain isolated from that solid shell.
- Exact-contact grounding has a bounded `RaycastNonAlloc` fallback because PhysX
  `SphereCast` does not report every cast that starts touching a mesh. This prevents
  a settled platform from creating a false airborne/landing frame.
- Billboard dust uses transparent premultiplied URP particle material with alpha,
  depth soft-particle fade, ZWrite off and no ShadowCaster. A validator rejects an
  opaque or no-alpha billboard assignment.
- Native High and Low/Web use separate URP assets. Native High is 4096/four/soft at
  48 m; Low/Web is 2048/two/no-soft.

## Consequences

Rendered skin and physics can no longer disagree during KO, and player/bot reset
through one pipeline. Platform fracture is temporarily unavailable during bounded
preparation, but impacts are never lost. Scene size increases by inactive pooled
shell components in exchange for eliminating cast-time object creation and the
187 ms collider burst.

Final rescue evidence is `80/80` focused EditMode and `11/11` focused PlayMode
with a clean Console. The 720-frame gameplay report records total/CPU P95 of
`14.00/14.00 ms`, an `AcquireSolid` peak of `3.62 ms` and a fracture-cell peak of
`0.68 ms`. GPU frame timing is unavailable in this batch Editor capture and is not
reported as zero performance cost.

## Rollback

The visible rig can be disabled and the old proxy presentation restored without
changing `EarthDuelRespawnSolver`. Deferred fracture can fall back to a non-
fracturable solid platform, but eager multi-cell cooking in the cast frame is not
an accepted rollback.
