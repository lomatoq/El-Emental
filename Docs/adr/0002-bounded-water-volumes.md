# ADR 0002: Bounded proxy volumes for authoritative water

Status: accepted for M6

## Context

The product needs conserved gameplay mass, reversible phase changes, deterministic replay, local pressure impulses, and a Web-capable degradation path. A globally continuous particle or grid solver would make those guarantees expensive and would couple canonical state to presentation density.

## Measured spike

Three representations were evaluated against the M6 gate:

| Candidate | Canonical state cost | Phase/mass bookkeeping | Presentation flexibility | Decision |
|---|---:|---:|---:|---|
| 32³ shallow cells per region | 32,768 cells/region | Good | Good | Rejected for the first slice: sparse scenes still pay grid cost |
| One gameplay particle per droplet | Scales with visual density | Fragile under merge/split | Excellent | Rejected: presentation count becomes authority |
| Bounded proxy volumes | O(active volumes), cap 64 | Explicit and testable | Excellent through ribbons/spray/steam proxies | Accepted |

The acceptance fixture uses 64 thermal regions, a 16-region query cap, and conserved transfers across 10,000 operations. This keeps expensive work budgeted while visual particle counts may scale independently.

## Decision

`WaterWorld` owns at most 64 coarse `WaterVolume` records. Each record carries stable identity, owner, bounded shape, motion and canonical `PhaseState` (material, phase, temperature, mass, latent progress). Meshes, ribbons, spray and steam particles only read these records through a presentation bridge.

## Consequences

- Rivers and oceans must be authored as multiple coarse regions or receive a later measured shallow-cell upgrade.
- Volume merging/splitting remains explicit and can preserve mass exactly.
- Save/network authority stays compact and independent of GPU or VFX state.
- The capability profile can reduce visual particles without changing gameplay hashes.
