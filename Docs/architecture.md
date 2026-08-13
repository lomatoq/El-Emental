# Elemental Planets architecture

## Product boundary

The first playable proof is the Earth Core Slice:

1. Walk around a small planet with stable local up.
2. Draw a line to raise an Earth wall.
3. Pull one bounded terrain volume into one pooled rigidbody fragment.
4. Flick the fragment into terrain or a character.
5. Persist terrain edits and replay the command stream.

Arena, co-op catastrophes, puzzles, other elements, active ragdoll, networking, and Web Magic Lab build on that proof. They are not part of M0.

## Dependency direction

Elemental.Core

- Math, stable IDs, time, serialization primitives, deterministic helpers.
- No UnityEngine object model.

Elemental.Simulation

- Gravity, materials, fields, voxel state, magic, combat, clocks.
- Pure data and explicit world ownership.
- References Core, Mathematics, Collections, and Burst-compatible data only.

Elemental.Runtime

- World lifecycle, PhysX adapters, character runtime, scene bootstrap.
- Owns Unity Rigidbody handles and converts Unity data at the boundary.

Elemental.Presentation

- URP, camera, VFX, audio, animation, and UI.
- Reads snapshots and typed events. Never becomes gameplay authority.

Elemental.Input

- Input System actions, gesture sampling, intent gates, and replayable input commands.
- `EarthInputAdapter` is the only runtime action/device boundary. Earth gesture consumers use
  viewport-normalized samples and never query physical input devices directly.

Elemental.Authoring

- ScriptableObject authoring assets, validators, bakers, and editor tools.
- Runtime state is baked or copied into explicit world state.

## Simulation clocks

- Physics clock: 60 Hz.
- Field clock: initially 20 Hz.
- Thermal clock: initially 10 Hz.
- Structural work: event-driven and budgeted.
- Presentation: frame rate dependent and read-only with respect to canonical state.

## World ownership

- GravityWorld owns registered gravity providers and composition.
- PlanetRuntime will own local coordinates, chunk store, edit history, and gravity source.
- Physics adapters own Unity component references only.
- MagicExecutor will be the single authority that applies compiled ability operators.
- MagicExecutor also owns the single-target Earth vector-field session; runtime target adapters expose explicit acquire/release hooks and never become global registries.
- MagicExecutor owns the bounded Earth-only MMB gravity-well session. It queries a fixed-size collider buffer and admits only explicit Earth target adapters; presentation rings, motes and camera pulses remain consumers of its read-only state.
- BendSessionState owns continuous bending lifecycle data. Runtime Bending adapters own only
  Rigidbody handles and apply the pure BendForceSolver result in the physics clock.
- ReplayRecorder will own the ordered command stream and seeds.
- Structural authoring assets own immutable meshes and baked definitions. A runtime Earth structure copies those definitions into caller-owned bounded buffers; `EarthBondDamageSolver` and `EarthIslandSolver` own no Unity objects and allocate no storage.
- Structural provenance is stable across proxy switches and pooling: structure, piece and bond IDs are canonical, while Rigidbody handles are replaceable adapters. World/foundation bonds use the explicit `-1` endpoint rather than a scene-object reference.

No subsystem may locate these owners through a global static registry.

## Coordinates and units

- Meters, seconds, kilograms, and Celsius unless an API explicitly states otherwise.
- Method and type names identify World, PlanetLocal, ChunkLocal, or Cell space.
- Gameplay planet radius starts at 24 meters and is a profile value.
- Surface gravity starts at 14 m/s² and is a feel parameter, not literal astrophysics.

## Commands and events

Commands contain intent, geometry, intensity, tick, stable IDs, modifiers, and seed. They do not contain precomputed outcomes or mesh references.

Earth input resolution validates the source and active manipulation context before running any
gesture templates. Recognition uses a fixed-count normalized path and returns an N-best result
with confidence and ambiguity. Replays carry the resolved intent plus quantized geometry,
source ID/generation, charge, wheel parameter, modifiers, ticks, seed, and optional digest;
raw pointer streams and screen pixels are not authoritative.

Simulation emits typed past-tense events such as TerrainEdited, ImpactOccurred, AbilityRejected, and CharacterStateChanged. Presentation may route those events to effects without feeding visual state back into simulation.

Dense same-frame Earth impact bursts are presentation-batched after the typed event boundary. The accumulator caps particles and camera requests while retaining an impulse-weighted contact frame, maximum energy and deterministic seed. It cannot merge, suppress or rewrite the canonical impact, bond damage or replay stream that produced those events.

Earth destruction uses explicit tiers. Repairable structural pieces retain provenance, scale and collision until repair, reabsorption or bounded pool reuse; they never disappear through an implicit timer. Gameplay chips and cosmetic debris may keep radial gravity, momentum and collision while their visible scale decays, and leave simulation only when the pooled object reaches zero scale. Particle adapters use the same local planet direction rather than Unity's global gravity modifier.

Fracture is cause-driven. Structure-local impact batches damage baked bonds through directional tension, shear and compression; deterministic connected components then identify foundation-supported and dynamic islands. Canonical fracture state never comes from GameObject activation, renderer visibility or Rigidbody sleep.

Reassembly is provenance-aware and physical. A repair session selects only pieces owned by one baked structure, stages them in a deterministic bounded cloud, and seats them in graph order with mass-aware PD and active collisions. Bonds pass through reforming and repaired phases only after a pose/velocity settle gate. Full repair restores the intact proxy; missing pieces remain an explicit partial result, and interruption returns unwelded pieces to ordinary dynamics.

Internal Earth verbs (`RaisePlatform`, `VectorFieldPush`, and `LandingCushion`) are selected by the unified input grammar and do not occupy hotbar slots.

## Terrain authority

Canonical terrain is an analytic base SDF plus an ordered edit log and optional snapshots. Render meshes and collision meshes are bounded caches rebuilt through separate queues. Input handling never performs synchronous unbounded meshing or collider baking.

## Error policy

Authoring validation is loud and actionable. Runtime commands reject invalid input with a reason code. Numerical safety clamping is counted in debug telemetry. Unsupported platform paths use a documented fallback or are rejected.

## No-warnings policy

New compiler warnings, package resolution warnings, leaked native allocations, or unexpected test logs fail a task. A pre-existing warning must be documented with owner and removal milestone before work continues on the affected subsystem.
