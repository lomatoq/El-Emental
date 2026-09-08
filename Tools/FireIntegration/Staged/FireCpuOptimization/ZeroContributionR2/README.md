# Zero-contribution field optimization — equivalence verified, staging only

One replacement source: `Assets/Elemental/Presentation/Fire/FireCpuField.cs`. Actual Assets matches the exact before snapshot; no source import or Unity execution occurred during this validation.

Scope remains two existing candidate early-outs:

1. After the shell-specific support override, a node with exactly zero weight skips swirl, six noise sin/cos calls, speed limiting and zero accumulation.
2. Contact steering checks existing relative outward motion before constructing its tangent, sin/cos fallback and outward direction. An already-outgoing particle returned unchanged before and still does.

No particle count, appearance parameter, lifetime, budget, grouping, gameplay or network changes. Capsule, Shell and Vortex formulas retain the same arithmetic when contributing.

## Executed validation

`python Tools/FireIntegration/Staged/FireCpuOptimization/ZeroContributionR2/verify.py` now supplies and copies Unity6000.5's actual `UnityEngine.MathematicsModule.dll`, which the facade `Unity.Mathematics.dll` forwards to. This resolved the standalone oracle's missing type dependency without changing production source.

- Actual Unity Presentation response-file compile: exit0.
- Standalone oracle compiles the literal before and after FireCpuField source plus actual FireDomainState, FirePresentationSnapshot and FireContactMath. The oracle supplies only the verified fixed FireWorld capacity constants6/8 to instantiate snapshots; it does not replace any tested field/contact arithmetic.
- 250000 field samples, including58394 nonzero outputs, bit-for-bit identical target and response.
- 250000 contact steering samples, including40226 actual changed velocities, bit-for-bit identical results.
- All baseline outputs finite. Coverage includes1–6 nodes, all3 shapes, inactive/zero-density nodes, zero/degenerate axes, shell support boundaries, zero/front/recovery contact limits, angular surface velocity, incoming/outgoing particles, degenerate tangent and dt0.
- Test coordinates/time/parameters span finite representative gameplay magnitudes. No equivalence claim for NaN/Infinity/overflow inputs outside the validated contract.

Results are in `Reports/oracle-result.txt`; source baseline equality is checked by verify.py after execution. This verifies managed arithmetic equivalence, **not Burst output, actual visual equivalence or elapsed performance**. The existing eight-High standalone p95.9711ms remains above the.8ms target until a new actual benchmark demonstrates otherwise. After import, run the existing Fire presentation Play QA and identical standalone benchmark without particle/art changes.
