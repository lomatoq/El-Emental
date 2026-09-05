# Surf pillar visual stability

The prior visual test selected a direction with obstacle/support raycasts but began on
the arena's fractured floor. During the 0.72 s real Shift+W+Space charge, crossing a
collider seam legitimately emits `SupportTransfer` integrity damage. This explains
the nondeterministic 5, 8, 11 or 12 attached cells before release; it is unrelated to
the pillar-jump input or launch contract.

The staged replacement changes only the PlayMode QA setup. It creates a temporary
32 m x 12 m curved mesh at a constant radius eight metres above the production
planet and moves the complete rider Rigidbody hierarchy onto its rear third. The
surface is one collider with sub-degree facets and uses an existing Earth material.
The shipping PlayerInput, Shift+W surf, held Space charge, released Space edge,
board integrity, physical stone scatter, tilted pillar and rider motor all remain
active. The fixture is in the additive test scene and is destroyed/unloaded; no scene
or production source is saved.

Apply while Unity is idle:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/SurfVisualStability/Apply-StagedPatch.ps1
```

Then run the existing focused `SurfPillarJumpVisualQa` PlayMode launcher. Keep all
existing acceptance gates: at least 8 attached cells before release, at least 8
framed break stones, one pillar event, 18-28.1 degree tilt, forward travel >0.35 m,
rise >0.35 m and peak up-speed >2.5 m/s. Inspect all three PNGs for the visible board,
finite scatter, tilted pillar and airborne rider.

`Validate-StagedPatch.ps1` compiles the replacement against the current Bee response
file without opening Unity. Runtime execution and captures remain pending root.
