# Finite wall foundations and middle-button grip

Working tree: uncommitted changes based on main `1235579`, 2026-09-06.

Constructed-face input still locks the selected draw plane, including columns and
wall/platform sides. Before acquisition, `EarthWallSurfaceFit` trims the requested
base strip to a contiguous supported interval on that original face. It reduces
thickness when the face cannot contain the requested width, applies a 1.2 cm edge
margin, and checks the original stable ID, generation, face and plane normal.
Neighbouring structures cannot substitute for missing support. Base strips below
25 cm long or 5 cm thick are rejected with input feedback.

The source provider explicitly supplies its construction collider. The four base
corners are seated at a verified depth from 6 cm down to approximately 1 mm, rather
than inheriting the ground-wall minimum embedding depth. This accommodates thin
support meshes. The optional verified depth reaches `EarthWall.Initialize` through
the pool; existing planet construction and legacy direct callers retain their
previous seating behavior. The volume check assumes the existing validated closed
construction colliders and verifies absence of an entry from each embedded corner
back toward its already verified front face.

MMB no longer captures an entire `EarthWall`, nor cells that remain structurally
supported. Circle disassembly/repair sessions remain available, and detached cells
use the existing loose-body grip. LMB/vector wall-push behavior is unchanged.

Focused verification added (not yet run by the implementation agent):

- `EarthWallSurfaceFitTests`: horizontal, tilted and vertical faces, rotated stroke,
  thin 2 cm support, and stale-generation rejection; actual collider containment
  is asserted for all four base corners.
- `EarthGravityGripSessionTests.MiddleMouseKeepsWholeWallAnchoredAndStillAllowsStructureGestures`:
  intact-wall circle session starts with zero captured bodies and a kinematic wall.

`Elemental.Earth.Wall.SurfaceFit` measures bounded construction-time fitting;
no per-frame allocations or new recurring physics query are introduced. Root
coordinates Unity compilation, focused execution and final evidence. No runtime
or performance acceptance is claimed by this source-only note.
