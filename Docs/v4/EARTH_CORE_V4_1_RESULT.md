# Earth Core V4.1 — implementation and verification result

Status: **core implementation green through Gate 7; Native Gate 8 green; release-candidate Gate 8.5 not claimed**.

Date: 2026-08-19

Unity: `6000.5.7f1`, URP

Branch: `codex/earth-core-polish`

Baseline HEAD: `7bdce0dac855007ec14e1f7114a6af37e563614a`

Worktree: contains the V4.1 implementation and is not represented by a final commit yet.

## Outcome

The V4.1 roadmap was implemented as a coherent Earth core rather than another set of isolated abilities:

- mandatory runtime geometry validation, safe publication and deterministic seed sweeps;
- unified motion/support state, fault evidence and moving-surface contract;
- Earth Matter identity, provenance, representation tier and return/reintegration lifecycle;
- one input owner with normalized gesture/scroll tokens and ranked intent resolution;
- bounded combo history and follow-up graph;
- data-driven pose requests, full-body choreography, Animation Rigging bridge and fallback presentation;
- twelve-family rock grammar, eight-family wall grammar and local anti-repeat;
- volumetric fracture, support islands, local damage and reconstruction;
- causal VFX/audio/camera layers that consume confirmed simulation events;
- combat dummy states, trap, seismic counter and projectile redirect;
- fixed-ring player telemetry and an opt-in standalone soak runner.

The `$ultimate-solo-game-creator` skill affected the implementation by keeping authority boundaries explicit: animation, VFX, camera and audio improve readability, but never decide hit, fracture, support, mass or return.

## Gate table

| Gate | Result | Evidence |
|---|---|---|
| 0 — baseline | Green | `Docs/v4/EARTH_CORE_V4_1_BASELINE.md` |
| 0.5 — geometry/motion rescue | Green in automated corpus | integrity gate, 10k seed sweep, motion fuzz/repro tests |
| 1 — Matter Kernel | Green | identity/registry/provenance/representation tests |
| 2 — return/reintegration | Green | captured → subsurface → commit/reverse/jam tests |
| 3 — input grammar | Green | tokenizer, scroll, ranked intent and router tests |
| 4 — combo graph | Green | bounded move history and follow-up resolver |
| 5 — character/choreography | Green | pose solver, rig bridge, Animator/ragdoll regression |
| 6 — visual/audio/camera | Green functionally; manual art review remains | shape grammar, VFX bridge, indirect debris, audio and camera tests |
| 7 — combat sandbox | Green | dummy state scale, defence follow-up, trap/counter/redirect |
| 8 — performance/shipping | Native green; WebLab/GPU capture incomplete | 10-minute player stress and Windows builds |
| 8.5 — zero-known-critical RC | Not claimed | requires 60-minute soak, manual camera sweep and severity sign-off |
| 9 — other materials/elements | Intentionally not started | roadmap says only after Earth release gate |

## Final automated court

| Court | Result | Duration | Artifact |
|---|---:|---:|---|
| Full EditMode | `305/305` | `5.368 s` | `BuildReports/V4-Final6-FullEdit.xml` |
| Full PlayMode | `95/95` | `161.786 s` | `BuildReports/V4-Final6-FullPlay.xml` |
| Combat runaway regression | `3/3` | `0.047 s` | `BuildReports/V4-RunawayGuard.xml` |
| Bidirectional winding regression | `1/1` | `0.076 s` | `BuildReports/V4-WindingFocused2.xml` |

The full EditMode court includes deterministic 100k-sample motion/redirect fuzz and the existing allocation-free hot-loop checks. The runtime courts cover platform carry/jump, wall and platform fracture/repair, MMB targets, armor, resonance, surf, surface construction, material projection, celestial behaviour and Humanoid recovery.

## Player stress evidence

The final standalone run used the real Windows Development player at 1920×1080, D3D11, Ultra, with a diagnostic `120 FPS` cap. For `602.13 s` it repeatedly executed locomotion, jump, wave, armor spread/fire/release, resonance charge/fire and surf.

Hardware:

- CPU: AMD Ryzen 7 5700G;
- GPU: NVIDIA GeForce RTX 4070;
- OS: Windows 11;
- Unity: `6000.5.7f1`.

Result:

- soak terminal: `failed=False`;
- geometry repair/block events: `0`;
- non-finite/runaway bodies: `0`;
- unhandled exceptions/assertions/crashes: `0`;
- final rigidbodies: `48`;
- final Hero Physical matter: `33`;
- observed working set stabilized near `328 MB`.

Timing:

| Metric | P50 | P95 | P99 | Max |
|---|---:|---:|---:|---:|
| CPU frame | `8.333 ms` | `8.341 ms` | `8.356 ms` | `181.579 ms` |
| main thread | `0.813 ms` | `1.981 ms` | `2.642 ms` | `181.570 ms` |

The one-off maximum includes startup/scene work and is not a steady percentile. Unity did not return GPU timings through `FrameTimingManager` in the hidden automated player, so GPU is **unavailable**, not `0 ms`. Evidence is in `BuildReports/V4-PlayerStress-600s-Final-Telemetry.json` and the paired log.

The whole player reports managed allocations because Unity/presentation/diagnostic systems allocate outside the canonical loops. The V4 acceptance claim is narrower and test-backed: measured simulation, routing, matter, support and damage hot loops allocate `0 B` after warmup. No claim is made that the entire Development player is globally allocation-free.

## Builds

| Player | Result | Warnings | Errors | Size | Location |
|---|---|---:|---:|---:|---|
| Windows Development | Succeeded | `0` | `0` | `174,310,486 B` | `Builds/Windows/ElEmental.exe` |
| Windows Release | Succeeded | `0` | `0` | `108,122,787 B` | `Builds/WindowsRelease/ElEmental.exe` |
| WebLab | Environment-blocked | `0` | `1` | — | `BuildReports/V4-WebLab-Unsupported.json` |

WebLab did not reach script compilation: the installed Editor lacks WebGL Build Support. Unity Hub downloaded the official `6000.5.7f1` module, but Windows UAC elevation was cancelled or timed out, so the module could not be written under `Program Files`. This must not be reported as a successful Web build.

## Defects found by the final gate

### Inverted procedural wave cells

Both clockwise and counter-clockwise shared footprints could reach the runtime gate with reversed winding. Generation now normalizes winding from signed footprint area before publication. The final 10-minute run produced zero repair warnings.

### Long-run combat Sentinel escape

Repeated surf impacts added `VelocityChange` to the braced Sentinel until a first soak reached roughly `850 m/s`. A deterministic safety solver now:

- applies archetype-specific physical speed limits;
- preserves ordinary in-arena motion;
- adds a smooth inward correction outside the combat arena;
- clamps angular speed;
- never teleports the target.

The original failing 600-second evidence is retained; the final 600-second rerun is green.

### QA scene lost after builds

M0 build setup rewrote Editor Build Settings and removed `EarthPolishLab`, which broke later PlayMode tests. The build pipeline now re-registers the editor-only lab and then explicitly filters it from player scene order. The lab remains testable without shipping inside Native builds.

### Null-device indirect buffer

The Tier-C debris renderer tried to allocate a structured GPU buffer under `-nographics`. It now checks compute/instancing support and disables only the visual layer when unavailable; gameplay authority is unchanged.

## Frozen profile hashes

| Asset | SHA-256 |
|---|---|
| `EarthShapeGrammarProfile.asset` | `B6944B2CAB2E204490A321094177B21BE7387907FF77B0B27F033EFF781D3729` |
| `EarthArmorProfile.asset` | `C675561B5CB85C8EB284E89598602DD36A5CBCF00062B29214930460C49A305E` |
| `EarthStructureFractureProfile.asset` | `137F33981BAC512BE5CB9CD23E42C47725DBC224FA46DC5008F857366487284F` |
| `EarthGestureProfile.asset` | `3FBDC8FA37F1BF0FF5E5D1AE9AD0565CFBF2A704F7A7645D4D5A99C1C7135A96` |
| `EarthCameraProfile.asset` | `8618B446CE814E78E3E4A4BA03AE7F300F707317B2E2B9C264D3D7C8894A16A5` |
| `EarthSurfProfile.asset` | `F2AAD28819218739A1B6EBE5223100431F9D4644A5933DD496AEF0B1A48649EB` |
| `EarthResonanceProfile.asset` | `5E1B3D527CD0CB2A49D1CD1422B9DB7887221EA23AE99B33574B1F2980C7497A` |
| `EarthPolishLab.unity` | `4A178514EACAA7E4498C3102B294DC41B1A2AC1F9A0E66627C1783020A727BF7` |

## Explicit remaining release work

No Native P0/P1 is known in the automated corpus. Nevertheless, Gate 8.5 is not green until all of the following are done:

1. approve the already downloaded WebGL module UAC prompt and rerun WebLab build/smoke;
2. capture real GPU P50/P95/P99 with a visible Profiler session;
3. run the roadmap's 60-minute player soak;
4. perform the manual eight-direction armor/camera/readability review in `EarthPolishLab`;
5. complete the P2 severity/waiver review and archive the contact sheet;
6. commit the final worktree so release reproduction is tied to one immutable commit.

The larger gameplay and technology guide is `Docs/EL_EMENTAL_GAMEPLAY_TECHNICAL_GUIDE.md`.
