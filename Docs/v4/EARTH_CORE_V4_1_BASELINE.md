# Earth Core V4.1 — Gate 0 evidence baseline

Status: frozen on 2026-08-19 before V4.1 production changes.

This file is the comparison point for the V4.1 roadmap. It does not claim that the
V3 implementation already satisfies V4.1. A gate is green only when its own new
evidence exists; older V3 results are regression evidence, not proof for new code.

## Authoritative roadmap

- Source: `El-Emental_Earth_Core_V4.1_Technical_Audit_and_Roadmap (1).md`
- Source SHA-256: `96D1F9A8EC7AED9CB256F538A0FEF29D7E2F7370983FC4CB2D94D52161F817C3`
- Unity: `6000.5.7f1`
- Render pipeline: URP
- Working branch: `codex/earth-core-polish`

The roadmap is treated as a sequence of falsifiable gates. Gate 0.5 must be green
before Matter Kernel or combo work can become production-authoritative.

## Last green V3 regression court

| Court | Evidence | Result | Time |
|---|---|---:|---:|
| EditMode | `BuildReports/CameraArmor-20260819-Edit.xml` | 238 / 238 passed | 2.20 s |
| PlayMode | `BuildReports/CameraArmor-20260819-Play.xml` | 92 / 92 passed | 157.52 s |

The XML was produced from an isolated verification copy. It predates V4.1 geometry,
motion diagnostics and support-frame work and therefore cannot certify those changes.

## Frozen scene/profile hashes

| Asset | SHA-256 |
|---|---|
| `EarthPolishLab.unity` | `D1CA5D3EDC994B7D3186BBE3D58961960A519FA4BE4A8B46D4035FA06B4033F9` |
| `EarthArmorProfile.asset` | `C675561B5CB85C8EB284E89598602DD36A5CBCF00062B29214930460C49A305E` |
| `EarthArmorShellDefinition.asset` | `113215F9DFA7CD47269511E1F372114DF59BC0A6413EBC139BB5329AB1198047` |
| `EarthStructureFractureProfile.asset` | `137F33981BAC512BE5CB9CD23E42C47725DBC224FA46DC5008F857366487284F` |
| `EarthGestureProfile.asset` | `3FBDC8FA37F1BF0FF5E5D1AE9AD0565CFBF2A704F7A7645D4D5A99C1C7135A96` |
| `EarthCameraProfile.asset` | `8618B446CE814E78E3E4A4BA03AE7F300F707317B2E2B9C264D3D7C8894A16A5` |
| `EarthSurfProfile.asset` | `F2AAD28819218739A1B6EBE5223100431F9D4644A5933DD496AEF0B1A48649EB` |
| `EarthResonanceProfile.asset` | `5E1B3D527CD0CB2A49D1CD1422B9DB7887221EA23AE99B33574B1F2980C7497A` |

Future seed sweeps record the profile hash and generator seed in every failure row.

## P0 / P1 inventory at freeze

Severity follows the V4.1 contract: P0 blocks a playable or shippable court; P1 is a
high-impact systemic defect with a viable workaround but no acceptable production proof.

| ID | Severity | State at freeze | Owner / required evidence |
|---|---|---|---|
| GEO-001 | P0 gate risk | Procedural armor/wave meshes were published without one shared topology, winding and normal court. A user-visible inverted-normal incident already occurred. | Geometry Integrity Validator, runtime publication gate, 10k deterministic seed sweep. |
| MOT-001 | P0 gate risk | Grounding, moving support, casting and ragdoll recovery do not yet share the V4 authoritative motion-state graph or invariant telemetry. Historical failures included frozen locomotion and unsupported falling. | Motion fault events, deterministic repro bundle, locomotion matrix and soak. |
| SUP-001 | P1 | Moving surfaces expose only point velocity/up/emerging. Rotation, angular velocity, generation and discontinuity are implicit, so double velocity and stale support cannot be disproved. | Unified `SupportFrameSnapshot`, generation checks and support transition tests. |
| CAM-001 | P1 monitored | Dense armor previously pushed the camera into the player. The V3 render-only sightline guard has focused regression coverage, but the V4 camera readability matrix is not yet present. | V4 camera requests plus eight-direction armor/combo capture matrix. |
| MAT-001 | P1 architecture | Detached earth has no universal identity/provenance/volume ledger and cannot atomically reintegrate into its origin. | Matter Kernel and single-stone return court. |
| UX-001 | P1 architecture | Input resolves discrete ability islands; it has no normalized scroll accumulator, ranked intent continuity, or data-driven follow-up graph. | Gesture/scroll corpus with miscast rate and combo continuity evidence. |

No issue is closed by prose. It closes only with a deterministic test, captured repro and
the relevant scene/profile/seed hashes.

## Evidence contract for V4.1

Every gate must leave:

1. NUnit XML for focused EditMode and PlayMode courts.
2. A machine-readable JSON summary with commit/worktree identity, Unity version,
   scene hash, profile hashes, seed, test counts and failure IDs.
3. A visual capture for claims involving animation, camera, mesh normals, VFX or readability.
4. A compact `EarthReproBundle` for every motion/geometry P0 or P1 failure.
5. P50/P95/P99 frame evidence for performance claims; averages are insufficient.
6. A clean `git diff --check` for the touched scope.

V4.1 is not complete while any known P0/P1 remains open, any generated hero mesh can
bypass the integrity gate, any authoritative loop allocates in steady state, or return
destroys the physical body before SDF commit confirmation.
