# Accepted island rotation only

Two staged sources; actual Assets unchanged. Latest approved RockShapeBuilder stays untouched. Do NOT import abandoned IslandBuoyancyFollowup taper experiment.

DistantBackdrop: deterministic initial island yaw±180/pitch±24/roll±28 degrees, independent motion-seed stream; .035–.075 degrees/second (80–171min full turn), alternating direction and varied mostly-upright axis. Absolute double clock modulo360; no accumulation, no runtime allocations introduced. Compensates base-pivot mesh offset so slow rotation occurs around mesh center. Existing bob/rock settings retained. Reduced motion restores exact authored pose; environment pause retains established pause semantics. Ground rotations and motion unchanged.

Full-spin spherical envelopes retain arena/exclusion/occupied checks; these are deliberately larger than previous fixed orientation boxes. Rebuild may reject additional close landmarks. Review RejectedPlacements and existing Main/Combat readability; preserve approved composition if a slot gets culled rather than bypassing bounds safety. Authored slot coordinates/scales and material are unchanged.

Import after baseline verification, refresh once, run existing Regenerate Same Seed (NO mesh bake required). Run Procedural Valley Geometry Edit: new ProceduralIslandSpinIsAbsoluteCenteredAndReducedMotionRestoresAuthoredPose validates centered orbit, time-order independence, reduced-motion pose restoration. Offline C# compile evidence is in Reports; actual Unity execution/capture remains pending. Inspect current and simulated600s pose plus reducedmotion. No performance claim.
