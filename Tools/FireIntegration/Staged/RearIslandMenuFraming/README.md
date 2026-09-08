# Menu rear island framing candidate

Stage only: one existing DistantBackdrop.cs file, three numeric changes in the rear airborne branch. Authored-frame center changes from (-100,140,-450) to (20,55,-650), retaining the same bounded jitter, mesh, scale, material, exclusion checks, motion, ground and forward/combat transforms. The candidate lowers the island and moves it inward and farther away to expose its full closed silhouette and air below in Main.

The existing rear island's approximate bottom/top elevations are 10–29 degrees above the menu eye at a 450m depth, which can crop its top with the 38-degree vertical field of view. New center reduces elevation and angular extent. This is source-derived framing evidence, not a live projection or accepted screenshot: the active GameView aspect, menu roll and camera pose must be measured by root after current tests.

After matching the before hash, import and regenerate the owned decorative backdrop. No mesh bake or profile/material changes are needed. Verify actual renderer corners projected through the Main camera: full silhouette within visible viewport, no left UI overlap, clear character and visible underside air. Then review Combat to confirm existing positive-Z placement unchanged. Save only reviewed owned changes. No physics/network components are added.
