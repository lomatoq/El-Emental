# ADR-0001: Engine and initial stack

Status: accepted  
Date: 2026-08-12

## Context

The project needs local gravity, editable planet geometry, physical characters, systemic elements, native desktop builds, and a later reduced Web Magic Lab.

## Decision

- Unity 6000.5.7f1, exact patch pinned.
- Universal Render Pipeline.
- Built-in PhysX rigidbodies and colliders with custom gravity.
- Pure C# simulation with Mathematics, Collections, Jobs, and Burst where profiling justifies it.
- GameObject runtime adapters for physics, camera, animation, and authoring.
- Input System for keyboard, mouse, and future controllers.
- Unity Test Framework for EditMode and PlayMode fixtures.
- Native Windows and macOS first. WebGL2 is a later reduced capability profile. WebGPU is experimental only.

## Not selected now

Full Entities/ECS, Netcode, Havok, VFX Graph, an external voxel package, and a generic no-code ability graph are deferred until a measured task requires them.

## Consequences

Core and Simulation stay independent of Unity object lifecycle. PhysX replay is deterministic enough only within tolerances; canonical commands and terrain edits remain serializable. Every new package or engine-version change requires another ADR with rollback path.
