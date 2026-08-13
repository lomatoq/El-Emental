# Repository instructions

## Required reading

Before a gameplay change, read Docs/architecture.md, the current file in Docs/milestones, and relevant ADRs.

## Architecture rules

- Elemental.Core and Elemental.Simulation must not depend on MonoBehaviour, Animator, VFX, UI, AudioSource, or mutable ScriptableObject state.
- Commands enter simulation; typed events leave it.
- Canonical state uses stable typed IDs, not GameObject instance IDs.
- Do not introduce global service locators or hidden FindObjectOfType dependencies.
- Do not add Entities, Netcode, Havok, VFX Graph, or another framework without an ADR and measured need.
- Meshes and colliders are caches. Base SDF plus ordered edit batches are terrain authority.
- Never create one Rigidbody per voxel.
- No managed allocations are allowed in documented steady-state hot loops.

## Task protocol

Implement one milestone concern at a time. Each completed task needs:

1. A pure-data contract and unit tests.
2. A thin runtime adapter where Unity integration is required.
3. A debug or playable scene.
4. EditMode and PlayMode evidence.
5. Relevant profiler marker and measured result.
6. Updated docs when a public contract changes.

Do not leave TODO stubs or silent fallbacks in a completed task. Unsupported capabilities must fail with an actionable message.

## Validation

No new compiler warnings. Run the scripts in Scripts or equivalent Unity batch commands. Report exact test output and known limitations.
