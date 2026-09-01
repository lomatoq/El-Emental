# ADR — VNext Wave 2 interface freeze

Date: 2026-09-01

Status: accepted

Base: `codex/vnext-integration` at `9d9f46e`

## Context

Gate 1 is accepted. Wave 2 adds capsule contact shadows and shading invariance, authored transition/catalog infrastructure, and powered medium-hit behaviours. These systems meet at presentation and support state, so Wave 2 must not reintroduce duplicate writers.

## Branches

| Track | Branch | Worktree |
| --- | --- | --- |
| R2 | `codex/vnext-rendering-wave2` | `ElEmentalVNext/rendering` |
| A2 | `codex/vnext-animation-wave2` | `ElEmentalVNext/animation` |
| P2 | `codex/vnext-physical-wave2` | `ElEmentalVNext/physical` |

The Wave 1 branches remain historical evidence and are not rebased or rewritten.

## Ownership

- R2 owns capsule/proxy shadow data, rendering passes, shared lighting includes, material-family shader integration, shading diagnostics, and rendering tests.
- A2 owns transition profiles, pair overrides, the deterministic transition queue, motion catalog metadata/build/validation/editor tooling, and `EarthTransitionDirector` integration.
- P2 owns deterministic powered-physical behaviours, muscle profiles, COM/support reasoning, semantic surface probes, physical diagnostics, and physical tests.
- The Director owns shipping scenes, prefabs, setup code, ProjectSettings, packages, final renderer wiring, cross-track contract resolution, Unity execution, captures, and Gate 2 records.

## One-writer rules

- `EarthTransitionDirector` remains the sole Animator state writer. P2 may emit a semantic recovery-step request but cannot play/crossfade an authored step itself.
- Existing foot contact/IK remains the sole visible foot/contact writer. P2 can choose/request a recovery foot but cannot write foot, knee, or leg transforms or competing leg drives.
- `CharacterPhysicalMode` remains the sole physical-mode authority. Powered behaviours consume it; they do not create a second mode machine.
- A medium hit has one accepted impact owner. It may produce inertial animation plus bounded powered deviation according to the severity contract, but never a duplicate impulse.
- R2 owns only rendering representations. It may consume actor/rock bounds and generation IDs but cannot mutate gameplay physics or fracture lifecycle.
- Shadow-caster/proxy replacement is generation-atomic; stale pooled representations cannot reactivate.

## Shared seams

- P2 exposes a small immutable semantic step-request value/interface. The Director integrates it with A2 after both reviews; P2 does not edit A-owned transition files.
- A2 transition metadata exposes contact and foot-release policy. It does not acquire IK bone ownership.
- R2 consumes existing material property blocks and world-projection frames. It does not replace fracture materials or normals during lifecycle changes.
- Any required shared scene, prefab, renderer asset, or setup edit is reported as an integration instruction and performed only after the review ring.

## Gate 2 rejection conditions

Reject integration if SSAO-off large form is unreadable, fracture exterior shading changes, capsule shadows extend beyond contact, transition profile/catalog are inactive, a medium reaction loses support, an impact or leg writer is duplicated, a stale collider/caster survives, or a declared hot path allocates.

## Review ring

R reviews A, A reviews P, and P reviews R. A worker commit does not enter `codex/vnext-integration` until the reviewer returns APPROVE or APPROVE WITH REQUIRED FIXES with no unresolved Gate 2 blocker.
