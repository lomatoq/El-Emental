# Production dust day/dusk/night visual QA

This helper is intentionally staged outside `Assets`. It makes no asset or scene
changes and starts only from the explicit menu command.

## Integrate and run

1. Copy `Assets/Elemental/Authoring/Editor/DustProductionVisualQa.cs` from this
   directory to the same path under the repository `Assets` directory.
2. Open the saved `EarthCoreSlice`, enter Play Mode, and wait until the loading
   cover disappears and `EarthSceneReadinessGate.IsReady` is true.
3. Focus a normal visible Game view and run
   `Elemental/QA/Capture Production Dust Day Dusk Night`.

The run leaves Play Mode active and restores the previous camera pose, celestial
phase, time scale, input/UI/camera/bot behaviours, particle transforms, random
seeds, playback state, and prior live particles. It never saves the scene.

## What is captured

The helper uses the actual saved scene's production Main Camera, celestial system,
materials, and live ParticleSystem instances:

- `Material Contact Dust` for throw/contact impact dust;
- `Arena Fracture Dust` for the broad arena/column fracture cloud;
- `Sunlit Air Motes` for ambient motes.

It places the two physical clouds at the base of authored
`FRAME_outer_arch_02`, emits a deterministic bounded layout, advances every
particle to a visible fixed age analytically, and pauses all three systems. No
column or rock is damaged. The helper reads back that settled particle array and
reapplies it immediately before every production-camera render. The production
feedback presenters are suspended after their prior live particles are saved, so
queued events cannot add particles during the capture. Every write is read back
and checked against the same hash. The exact layout is then rendered unchanged at phase
`.25` (day), `.49` (warm dusk), and `.75` (night, including the production moon).

Outputs are written below
`BuildReports/DustProductionVisualQa/<UTC stamp>`:

- `Day.png`
- `Dusk.png`
- `Night.png`
- `CaptureReport.json`

The manifest records sun/moon intensity and color, Trilight ambient colors,
camera/impact coordinates, material and shader names, particle counts, and a hash
of every particle's position, velocity, age, size, and start color. A successful
run has `status: Captured`, `restored: true`, and `sameParticleLayout: true`.

## Acceptance

- Day retains the familiar ordinary sandstone-dust appearance.
- Dusk dust visibly inherits the orange/pink key light.
- Night dust is substantially dimmer while its silhouette remains readable from
  the production ambient light; it must not look emissive.
- Ambient motes follow the same lighting transition and remain restrained.
- All three images use identical camera coordinates, resolution, particle counts,
  and layout hashes.

The helper provides production visual evidence, not a gameplay-impact test. The
existing dust compositing PlayMode test separately proves neutral-reference parity,
day/dusk/night pixel deltas, soft compositing, and opaque geometry occlusion.
