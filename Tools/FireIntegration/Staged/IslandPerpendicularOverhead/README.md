# Additional perpendicular and overhead islands

Staging only. Two files: DistantBackdrop.cs (bounded cap guards only) and ProceduralValleyAuthoring.cs (new idempotent installation menu). No scene/profile asset, cloud/UI, or Unity tool mutation by this agent.

Adds12 slots to existing32, preserving every existing position, scale, variant and motion. Budget44 authored slots /32 airborne, with existing24-ground cap. Ground count does not increase.

- East (+X): near and far upright island at680/980m plus two close satellites.
- West (-X): unequal near and far island at680/960m plus two close satellites.
- Above arena: three isolated small islands at heights360/500/630 plus a smaller accent410 high, with asymmetric horizontal offsets. This is an overhead constellation with large open gaps, not a uniform ring/ceiling.
- Approved closed core mesh families and arena material reused. Existing yaw-only animation applies unchanged.

Analytical +/-X views use camera center(0,59,0), pitch-4.57 degrees,60-degree FOV,1676/776 aspect. Near islands cover ~20% screenheight and far~13-14%, between x.37-.68/y.08-.32. Satellites~3-5%. A straight-up camera has all four overhead pieces in-frame, spread x.32-.83/y.10-.68. Exact padded full-yaw rectangles in analytical-projections.json.

Validation: all12 new swept envelopes are mutually disjoint; all12 clear the43 saved existing ground/island renderer AABBs (including conservative combined ground bounds). Minimum clearance beyond175.1m arena exclusion is190.9m. Saved scene checks are conservative placement evidence, not actual runtime visual acceptance. Current scene had43 generated roots/2 rejected before additions; expected55 if existing outcomes unchanged and all12 additions accepted.

Both Elemental.Presentation and Elemental.Authoring.Editor offline compilation succeeded0errors (existing obsolete API warnings only).

Import after FX owner releases Unity: integrate_stage.py IslandPerpendicularOverhead --apply; refresh; execute Elemental/Environment/Procedural Valley/9 Add Perpendicular And Overhead Islands. No rebake necessary. Menu saves profile asset but does not save scene. Repeating menu preserves existing names/no duplicates. Earlier authoring menu guard budget is widened consistently to44 so it does not throw simply because extras exist.

Run InspectPerpendicularCoverage.cs via root-owned Unity_RunCommand after regeneration: read-only actual renderer projection for East,West,Overhead; writes actual-coverage.csv, reports added count and world-up dot. Root should then capture rotated gameplay +/-X and look-up views, checking clear silhouette gaps, particles/fog occlusion and ReducedMotion before saving reviewed scene.
