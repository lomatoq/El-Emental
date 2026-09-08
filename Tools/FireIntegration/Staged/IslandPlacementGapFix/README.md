# Island placement gap fix

One staged source file, no actual Assets writes or Unity operations by this lane.

- Airborne authored transforms use world-up yaw. Existing ground staging orientation is retained.
- Full-time spin clearance now uses horizontal circumradius and original vertical extent plus bounded drift padding. Rotation cannot tilt, so a height-sized sphere was unnecessary.
- Ground occupancy is split into welded connected mesh components. Empty air between disconnected pillars in one combined mesh is no longer treated as a solid ground-group box.
- Arena sphere, explicit exclusions, other islands, and real ground component bounds remain checked.
- Mesh array reads and connected-component cache construction happen only during Rebuild, not animation.
- Authored 32 slots and their approved positions/scales remain unchanged in this first pass.

Validation: offline Elemental.Presentation Roslyn compilation succeeded with zero errors. Actual accepted island count and Main/Combat visual coverage require root-owned Unity rebuild. Do not claim missing objects restored before that result.
