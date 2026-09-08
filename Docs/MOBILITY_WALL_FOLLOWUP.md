# Mobility and wall follow-up — 2026-09-06

Working tree on main `1235579`; no scene/controller rebuild was run.

- Held LMB+RMB line: `EarthPillarWaveProfile` Inspector now starts with
  `Линия столбиков — ЛКМ + ПКМ / Смещение от земли, м`. Positive values raise its
  placement, negative values bury it; zero preserves current placement. This
  affects crest physics and rendering together, retaining actual ground for
  contact feedback. `Размер столбиков по высоте, м` remains a separate control.
- Space still charges the pillar jump. Preparation uses a looping full-body
  half-crouch blend, not the held end frame of the finite PillarJump release clip.
  Charge presentation requires support and releases into normal airborne motion.
  `EarthPillarChargeAuthoring.Install` adds only its state/parameter/blend tree;
  existing manually authored locomotion children remain untouched.
- Landing cushion brakes over available travel, follows actual feet and uses
  irregular prewarmed stones. High-speed contacts fracture the same visible
  stones after cushioning. These pieces are cosmetic, not grabbable matter.
- Surf bounds its lead over the rider and ends when the rider can no longer accept
  moving support or leaves the carry range.
- Arena construction probes real irregular floor facets independently from
  locomotion slope classification. Source-side boundaries remain strict; adjacent
  connected arena surfaces can continue a stroke, gaps cannot. All four base
  corners must admit a common embedded plane inside actual support geometry.
- Cracked wall cells retain their matching collider volumes with irregular
  chipped chamfers. Canonically supported cells follow one rigid wall frame;
  disconnected cells become dynamic. Source bonds resist more damage than inner
  bonds. Explicit source bonds are breakable; full disassembly releases all cells.

Focused verification is launched with `Elemental/QA/Mobility Wall Followup Edit`
and `Elemental/QA/Mobility Wall Followup Play`. Reports use the same names under
`BuildReports`. Initial failures on two real six-metre arena strokes exposed
steep tiny top facets being classified as side faces; the new construction query
addresses this without relaxing the six-metre regression requirement.

Final evidence: **30/30 EditMode** at 01:11:50 UTC and **6/6 PlayMode** at
01:18:40 UTC. The Play run covers live graph charge playback/hips compression,
vertical release, line collider/render offset, rigid cracked cells and full
disassembly, surf carry/lost rider, sideways cushioning and production high fall.
Inspected captures: `BuildReports/MobilityWallFollowup/ChargeHalfCrouch.png`,
`BuildReports/LandingCushion/Compression.png` and `BrokenStones.png`.
The active animation driver reads AnimatorControllerPlayable; raw Animator state
queries report the dormant controller and are not valid production assertions.
The cushion toy uses its actual contact-normal plane because its disabled motor
does not update LocalUp. Existing unrelated CS0618 editor/runtime warnings remain;
these changes introduce no new compiler warning sites.

This scope does not establish global performance or approval of the concurrent
locomotion, respawn, costume, HUD and camera work being handled in another task.

Requested cushion VFX follow-up: twelve medium stones replace eight without
enlarging the assembled cushion. Fracture requests 220 dust/64 small chips, then
a second 150 dust/48 chip billow at 80 ms with 0.85 strength. The existing global
per-frame budgets still apply; landing physics is unchanged. Both cushion checks
pass in `BuildReports/SeptemberPremiumPlay.xml` at 01:42 UTC: sideways compression
without impact damage and the production high fall through cushioning/breakup.
This is scoped evidence for the VFX follow-up, not the whole concurrent suite.
