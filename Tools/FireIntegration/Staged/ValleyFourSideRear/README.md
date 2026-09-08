# Short floating pillars and menu-side continuation

STAGING ONLY. Six existing C# files, before/after mapping. Despite the historical directory name, **no four-sided basis change is included**. The latest direct user instruction overrides the older external-source suggestion: keep the accepted pictured pillars; floating forms are those same pillars, two to two-and-a-half times shorter.

## Changes

- Ground pillar geometry and ground groups retain their existing clipping, compound patterns and seed streams. `Pillar(..., heightRatio=1)` remains the same geometry.
- Floating families now contain one accepted compound pillar (2–4 intersecting masses), with a closed underside. The previous broad supporting slab and collection of miniature towers are removed from floating variants only.
- Shortening is solved before geometry bevel/chip cuts. Finished vertices are not vertically squashed: reference part scales and rotations remain unchanged, and the cutting distances retain their existing width. Two bounded coarse evaluations compensate for inclined caps. Measured height ratios across 96 seed/LOD cases are 0.41339–0.48531 of the full pillar.
- Floating placement uses a separate reference scale of 220–380, because the mesh already contains the shortening. Reusing the old 55–100 group scaling would shrink the islands twice.
- `rearContinuation` is an explicit profile opt-in. It adds up to three ground groups and one floating pillar at authored negative Z, independently of the existing positive-Z combat placement stream. Accepted totals are capped at twelve ground and six floating objects. The same 24-attempt full mesh/motion envelopes, arena exclusion and explicit exclusion volumes apply. Existing forward transforms are retained.
- Editor menu `Elemental/Environment/Procedural Valley/4 Enable Rear Continuation` enables the profile field and regenerates only the owned backdrop. It does not save the scene.

## Import and visual gate

Import only mapping.json entries after matching before hashes. Do not run build_stage.py again: it was a one-time baseline constructor, not the final implementation generator.

Existing SaveMesh GPU upload fix is preserved in the authoring baseline: explicit Clear/set vertex attributes/indices/bounds/UploadMeshData(false), not CopySerialized. Preserve all existing asset GUIDs and exact arena material. Bake Group Meshes updates the existing bank, then Enable Rear Continuation regenerates the owned root. No gameplay transforms, physics, UI, camera or atmosphere owner changes.

First capture one full pillar beside each short family with the same material and scale. Then verify actual Main (negative-Z view) and Combat (positive-Z view), not just a diagnostic camera from 2200m away. Confirm rear objects are visible, unobtrusive and within exclusions; floating motion respects reduced motion. Clear preview before tests/save. These artistic and Unity acceptance steps remain pending the next lease.

## Validation

All four actual Unity assembly response files compile offline with this source overlay, no warnings. Pure geometry oracle covers 672 seed/group/LOD cases, convex closure, positive volume, LOD transforms/containment and strict normalized triangle area. Additional 96 floating cases check same part count/scales and actual 0.4–0.5 height interval. See compile/report.json and oracle reports for exact results.

New Unity Edit tests check floating form contract and rear continuation deterministic preservation, accepted budgets, exclusion rejection and absence of physics components. They compile; execution remains pending Unity access. No runtime/frame-performance or final reference-match claim is made.
