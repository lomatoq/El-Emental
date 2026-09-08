# Main and Combat landmark composition

STAGING ONLY. Three existing source files: DistantBackdrop, profile, existing ProceduralValleyAuthoring. Accepted full/short pillar meshes, materials, UI, cameras, fog, physics and networking remain unchanged.

## Composition

An explicit `viewComposition` profile option replaces the previous randomly placed floating objects with six authored slots: three for Main and three for Combat, at approximately550/800/1100m camera depth with clearly different angular sizes. Actual closed short-pillar meshes remain unchanged. Each floating bottom stays above the opaque fog transition; no flat platform or cone is introduced.

Six additional SINGLE tall pillars reuse the accepted Preview/Pillar_00,04,08,02,06,10 mesh assets. These rise from authored base−155 through the fog at depths390/570/760m, with varied tops and visible heights. Existing far valley groups stay, so these are foreground-to-background depth landmarks, not a denser surrounding ring. Ground cap increases narrowly from12 to18 for this user-requested view composition; floating cap remains6. Hard limit24 decorative placements total. Six new ground pillars use their small approved high mesh (roughlyhundreds of triangles), not a fake identical LOD. Existing far groups/islands retain their actual LOD banks.

Original ground placement streams are preserved. Curated slots use the same full mesh+motion envelope checks, 175.1m arena exclusion and explicit exclusion volumes, with at most24 attempts and only20m lateral/depth retries. Authored ground slots are processed before floating slots so every island is checked against both existing and new terrain bounds. No per-frame allocation or placement work is added.

## Analytical evidence

`project_landmarks.py` uses supplied Main position(-.26,56.86,6.39), forward(.28,0,-.96),38°FoV,10°Dutch; Combat(.76,58.91,-11.33), forward(0,-.13,.99),38°FoV. It reads ACTUAL baked mesh bounds and projects all eight oriented corners in the authored frame.16:9 and the observed1681x785 Main aspect are included in analytical-projections.json.

At16:9 the three Main island image rectangles are approximately:

- Large: x.521–.656, y.136–.396.
- Medium: x.856–.948, y.162–.336.
- Small: x.760–.822, y.068–.190.

All clear the left UI region ending aroundx.44. The small island sits above the avatar head region; the larger two flank it. Combat rectangles likewise keep three separated silhouettes in the upper frame. Actual camera pose/aspect and renderer occlusion MUST be verified; these are analytical candidates, not rendered acceptance.

`check_occupied.py` reads the saved Unity hierarchy and existing mesh GUID bounds. All six proposed island anchors clear the12 saved ground group AABBs and six new pillar bounds, even with a conservative5m island padding. Nearest new pillar bound is over327m from the planet center, above the175.1m exclusion requirement. The source helper emits positions; re-running build_stage.py resets the staged snapshot, so do not rerun it after further edits without deliberate review.

## Import and actual QA

Match before hashes, import mapping entries, refresh. Run **Elemental/Environment/Procedural Valley/5 Compose Main And Combat Views**. This binds existing accepted preview meshes and regenerates only the owned backdrop; no mesh bake is needed. It preserves edited nonempty landmark arrays on later calls and does not save the scene.

In real Main and Combat at1920x1080, replay InspectActualProjectedBounds.cs and capture the unchanged gameplay camera. Expect three fully visible floating silhouettes at distinct apparent sizes in each view, with open air under them and no logo/avatar overlap. Inspect actual solid occlusion by canonical arena architecture and all ground pillars, not only bounding boxes. Ground bottoms must remain under fog and varying tops must read as separate depth layers. Check reduced motion and actual accepted placement count, LOD transitions and console; save only the reviewed scene. Four offline assembly compiles pass; actual Unity QA remains pending the lease.
