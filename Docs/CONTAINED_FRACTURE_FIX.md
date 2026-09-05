# Convex-volume fracture follow-up — 2026-09-04

## Latest: oblique stone fractures (UTC 12:48)

Rectangular arena sources now split along branch-dependent oblique planes, with
broader contained bevels (18% of the minimum cell dimension, edge cap 22%). This
supersedes the axis-aligned cuts and small bevel described in earlier evidence.
Exact primary arena detachment remains unchanged. Only genuine splitting creates
the new angled children, including recursively broken large stones.

Fresh `ContainedFractureEdit.json`: **12/12 passed**, UTC 12:43:59.
Final `ContainedFracturePlay.json`: **3/3 passed**, UTC 12:48:31, including real
column damage/detachment and persistent recursive debris. Minimum collision fill
**97.62%**, minimum visible fill **89.03%** in the thin rotated/recursive fixture;
all containment and mass gates pass. Four cached full splits: maximum **0.2482 ms**,
**0 managed bytes**; measured cold preparation **2.181 ms**. Editor-only scoped
measurements, not whole-frame/player certification. Final preview rendered from
the actual cache: `BuildReports/LooseStones/ObliqueFragments.png`.

Related resting/grab corrections: [LOOSE_STONE_FIX.md](LOOSE_STONE_FIX.md).

## Earlier evidence

Current source supersedes the earlier inscribed-template implementation documented below. Fresh validation: `ContainedFractureEdit` **8/8 passed** at UTC `2026-09-04 10:10:37`; final `ContainedFracturePlay` **3/3 passed** at UTC `2026-09-04 10:17:57`.

## Current geometry contract

- Arena detachment still preserves the exact authored cell. Genuine splitting now derives closed convex children from the parent mesh's convex hull rather than fitting unrelated stones inside it.
- `EarthConvexPartitionSolver` is pure data: deterministic incremental hull, merged coplanar face polygons, recursive equal-volume plane cuts, closed caps. Every child is shrunk to 99.2% linearly around its interior center, retaining approximately **97.62%** of the parent's convex volume in aggregate with disjoint seams.
- Collider and render both derive from each clipped child. Render adds a small relative bevel; any acute-corner expansion from the shared bevel builder is constrained back into that child’s own convex planes during cold preparation. The runtime regression requires at least **95% collision fill** and **85% render fill**, independently of canonical mass checks.
- Children keep the source orientation at spawn so they reconstruct its silhouette. Seeded angular velocities provide subsequent tumble. Initial unrelated rotations would violate both containment and filled volume.
- Split radius now comes from the child's actual volume and transform determinant, not the parent's enclosing radius or rotated AABB. Canonical masses and reserved provenance volumes partition by the normalized geometric volume fractions.
- `EarthConvexFragmentCache` owns prepared collider/render meshes. `Physics.BakeMesh` runs during explicit cold preparation at arena initialization, loose-fragment acquisition, decor startup and pool setup. It caches size-class plans and policy-selected second-level children. Unforeseen source/count combinations have a one-time preparation cost under `Elemental.Earth.Fracture.PrepareConvexCells`; they are not represented as zero-allocation cold paths.
- Hot cached splits bind prepared meshes to existing pool bodies. Unknown/non-readable sources and sheared transform frames retain the parent with an explicit rejection. Cache ownership is local to the debris pool and destroyed with it.
- Reusing a consumed split shell for cosmetic accretion restores the original normalized template, preventing the prior parent's cell scale leaking into another effect.

## Follow-up tests and measurements

`EarthConvexPartitionTests`: thin tetrahedron 2/3/4-way closed/contained/nonoverlapping volume checks and recursive cube fill. `EarthContainedFractureRuntimeTests`: rotated thin tetrahedra and recursion, analytic containment, collision/render volume fill, canonical mass, Stopwatch/managed allocation samples. Production combat and persistent-matter regressions remain required.

`BuildReports/ContainedFracturePerformance.json`, UTC `2026-09-04T10:17:55.2345904Z`, records:

- Minimum collision fill **97.61914%**, minimum render fill **96.39024%** across rotated thin parents and recursion; original strict containment assertions passed.
- Four successful cached full splits: **0.2045, 0.2480, 0.1309, 0.1177 ms**, maximum **0.248 ms**; **0 main-thread managed bytes** for every measured call.
- Explicit fixture source preparation/cooking **2.1673 ms**; fixture cache **11 plans / 82 native meshes**. Pool construction is outside this preparation timer. These figures describe this Editor fixture, not total production startup, every possible mesh complexity or a player-build certification.

The final three PlayMode scenarios cover volume/containment/recursion, production armor and exact arena detachment, and persistent matter/pool-full retention. The earlier failure correctly exposed an outward acute bevel; constraining only offending render vertices resolved it without weakening containment or fill gates. Historical numbers below validate the replaced fitting path only.

---

# Contained fracture geometry — 2026-09-04

Source change on dirty `d2174eded114dd022e4a9c442abadda7a0e44555`.
Validation: coordinating task ran `ContainedFractureEdit` (4/4 passed, UTC 2026-09-04 09:25:52) and `ContainedFracturePlay` (3/3 passed, UTC 2026-09-04 09:32:24–09:32:31). The Play report covers geometry/recursion, production combat and persistent matter/pool exhaustion.

## Contract

- Detaching an arena cell retains its original baked render mesh, existing render bevel, collider mesh, materials and fracture mapping. Detachment never swaps it for a generic rock.
- Actual secondary breakup uses the common pre-cooked ground-stone variants. Child positions and scales come from the real parent convex collider, not a sphere estimated from its rotated bounding box or from the impact position.
- A chord through the convex interior defines three or four disjoint slabs. Each child receives a seeded roll and a cell-sized aspect, then a bounded homothetic search fits every render and collision vertex into both its slab and the parent collider. Every triangle and convex hull therefore remains contained at spawn. Angular velocity remains random on all three axes.
- Two collider raycasts intersect the line through the convex vertex barycenter along the longest local shape axis; using closest support vertices can produce an edge-adjacent chord and microscopic children in thin tetrahedra. Cell aspect uses the transformed local shape axes, not the inflated rotated world AABB.
- Wall natural visuals likewise use the authored cell's convex face planes instead of only its AABB. The one-slot sandstone material fix is retained.
- Canonical mass/provenance partition and pool-exhaustion retention are unchanged. Invalid/unsupported parent geometry rejects the split with an explicit reason and retains its representation and mass.

This is a bounded stone-fitting representation, not an exact volumetric tessellation: rounded children intentionally leave visual seams/gaps while their canonical masses sum to the parent. No new mesh cooking or rigidbody creation occurs on impacts. Fit work is event-driven and uses cached child vertices plus one pool-owned reusable parent-vertex list.

## Regression entry points

- `Elemental.Tests.EditMode.EarthContainedFractureLayoutTests`: disjoint chord cells, invalid inputs and independent tetrahedron half-space validation of wall visuals.
- `Elemental.Tests.PlayMode.EarthContainedFractureRuntimeTests.RotatedThinConvexAndRecursiveChildrenNeverGrowOutsideTheirParent`: three rotated/scaled thin tetrahedra, deliberately inflated legacy radii and remote hit points; independent analytic parent-plane checks; recursive split containment and canonical mass.
- `Elemental.Tests.PlayMode.EarthCombatMobilityRuntimeTests.ArmorCollisionsAccumulateAndReleasedColumnSplitsWithoutLosingMass`: production armor damage, exact original mesh/collider identity after detach/pluck, physical secondary breakup, sandstone wall material slots.
- Existing `EarthPersistentBreakRuntimeTests` verifies persistence, duplicate split rejection and pool-full retention.

Profiler marker: `Elemental.Earth.Fracture.ContainedChildren`. The marker covers chord preparation and all child fits before canonical split. The fresh scoped Editor measurement below supplements the marker; broader production performance acceptance remains separate.

The PlayMode geometry regression writes `BuildReports/ContainedFracturePerformance.json`: four main-thread Stopwatch / managed-allocation samples around complete `TryEmitBreak`, including first-use cost. This is an Editor measurement, not a marker-exclusive or player-build claim.

## Fresh scoped measurement

`BuildReports/ContainedFracturePerformance.json`, UTC `2026-09-04T09:32:28.9616386Z`: four successful full split calls took 0.5475, 0.6496, 0.5231 and 0.5084 ms; maximum **0.6496 ms**, main-thread managed allocation **0 bytes** in every sample. These are Editor observations for the thin-convex/cube-template regression. They do not certify all production mesh complexities or the whole-frame budget.

The first Play run exposed an edge-adjacent chord producing overly small recursive children. The final ray-through-barycenter/local-axis implementation passes with the original strict containment tolerances and independent assertions unchanged.
