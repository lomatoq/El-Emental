# Stylized material feedback follow-up — staged, not visually accepted

Scope: eight files under `after/Assets`; `before/Assets` records live inputs. No simulation/event contracts changed. Parent owns integration and Unity execution.

## Art decision and primary sources

- [Cyanilux: Fire/Flame Shader Breakdown](https://www.cyanilux.com/tutorials/fire-shader-breakdown/) describes camera-facing warped ellipse/teardrop silhouettes, stable particle random offsets, three stylized bands, and the overdraw cost of stacked transparent quads. Decision: preserve the existing broad column body branch and flow-aligned billboard, lower micro noise, strengthen filled coverage, reduce spawn rate while increasing parcel size. This is a project-specific adaptation, not copied source code.
- [Federico Bellucci: procedural fire shader](https://blog.febucci.com/2019/05/fire-shader/) separates silhouette alpha from two inner color thresholds using upward scrolling noise and a gradient. Decision: thin warm-red silhouette, orange body, broad yellow core with controlled existing HDR boost. Body previously ignored `_CoreEmission` entirely; now uses it.
- [Unity / Sørb: Ignitement fire breakdown](https://unity.com/blog/real-time-fluid-simulation-fire-vfx-ignitement-breakdown) is a richer fluid-simulation reference. Existing project flow/collision solver is preserved. This patch does not claim to implement fluid-field advection or a new fluid simulation; importing that would be outside the bounded visual follow-up.

Impact dust is partitioned deterministically into 40% contact grit, 40% main mass, 20% long residual. Event count/capacity still comes from current tuning. Broad sizes/lifetimes come from the existing fracture profile. Contact is rapid and tangential; cloud is wider/slower; residual is low-opacity and settles more slowly. A shared expansion curve builds mass after impact. Shared dust shader and night lighting remain parent-owned and untouched.

Cosmetic chips now use eight existing low-poly recipes. Particle count is bounded to min(authored capacity,128). Seed bits carry contact count; one swept Physics raycast per moving chip per frame, two dissipative contacts then stop, angular damping, final-life alpha fade. Up to eight secondary dust puffs per presenter frame. No Rigidbody, object instantiation, dictionaries, or arrays allocated in steady-state integration. Serialized presenter restitution/friction/secondary threshold/mask expose tuning.

## Checks and remaining evidence

- Staged source inspection: sonar alpha and column billboard bound clamp preserved; no gameplay files changed.
- Existing EditMode `EarthStoneImpactDustTests` mesh validation updated to eight variants.
- Existing PlayMode `EarthStoneImpactDustRuntimeTests` now requires all eight native particle mesh indices and all three impact layers; residual minimum lifetime must exceed longest contact lifetime. Existing mass-response and lifecycle assertions preserved.
- Unity tests have NOT been executed by specialist (parent editor owner). Native mesh random selection, compilation, shader import, terrain bounce and depth compositing require parent execution.
- Capture .05/.2/.65/1.5 seconds after heavy drop and wall emergence, daylight and night, plus a wide/close column fire shot and sonar. Compare in grayscale: one mass and hot base should read before any micro-detail.
- Measure `Elemental.Earth.MaterialParticles` CPU and GC at 128 active chips. Raycast cost is a known unmeasured risk, not a promised performance improvement. Stopped particles skip rays. Long frames need a sweep check. Stationary cosmetic chips do not follow moving support transforms; this is presentation-only and short-lived.
- The shared dust shader is not replaced with a new thickness shader in this slice. Actual volume/color depends on existing dust material and parent lighting fixes. No visual acceptance claim is made from code changes.
