# True depth-varying wall fracture

Source checkpoint 2026-09-07, uncommitted main `1235579`; Unity bake/tests/rendered acceptance remain coordinator-owned.

The previous metric 3D Voronoi sites varied in Z, but their XY spacing was larger than the wall's entire .55 m depth. Therefore most cells still crossed both front and back surfaces. The production wall now opts into 32 distributed centres with eight nearby front/back competitors, retaining the existing 40-cell budget. Shared oblique 3D bisector planes cut partial-depth chunks. All source clipping planes, canonical volume calculation, bonds and foundation ownership still use the same solver. Default solver callers keep exactly the previous output.

The production gate checks actual cell bounds: at least eight cells below 85% of full depth, at least 20% normalized depth-span variation, closed topology, positive volume and at most 255 collider triangles. Median metric aspect remains <=3.5. Maximum aspect is <=8 for this preset because partial-depth pieces are naturally thinner than full-depth blocks; this does not change the default solver's prior tests. The independent SciPy recipe probe found 17 partial-depth cells and a .606 depth-span spread, median aspect3.45 / maximum7.246; these are independent numerical predictions, not Unity acceptance.

Run **Elemental > QA > Wall Depth Partition Edit** (two tests): production clipped-volume conservation/depth variation/oblique interior faces, and unchanged default solver output.

Then run **Elemental > Fracture > Bake Production Earth Wall**. Source revision2 invalidates only the old fracture asset. It validates the complete replacement plan before removing old piece mesh subassets, then rebuilds only `Assets/Elemental/Content/Fracture/EarthWallFracture.asset`; no arena regeneration or animation reassignment. The coordinator must verify startup-cache freshness after replaced collider subassets and capture the actual runtime wall pool.

The coordinator separately owns the metric bevel backing, renderer material-slot alignment and matching fracture surface shader. Depth generation does not edit those files.

Accepted production checkpoint: depth Edit 2/2 passed 2026-09-07 01:38:50 UTC; existing fracture asset rebaked at01:41:49, online geometry catalog refreshed afterward. Actual saved-scene wall pool visual Play 1/1 passed01:55:46 using the baked asset; the pool reads its asset data directly. See WALL_WIND_ROUND_FOLLOWUP.md for images and visual limitations.
