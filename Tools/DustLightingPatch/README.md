# Dust lighting patch (staged outside `Assets`)

## Observed defect

`BuildReports/DustCompositing/Latest.json` records
`denseDustLightingDelta: 0.0`: the production impact dust has the same radiance
with and without the key light. The current PlayMode acceptance test explicitly
requires that result (`<= 1/255`) and requires bright red dust with direct light
disabled, so the test currently protects the reported defect.

The Earth effects profile routes these physical particulate systems through the
same `RumbleDustLit` material:

- `Material Fracture Dust`
- `Chunky Earth Dust`
- `Arena Fracture Dust`
- `Outer Column Fracture Dust`
- `Material Contact Dust`
- `Stone Fade Dust`
- the runtime `Earth Surf Plough` dust renderer

`Sunlit Air Motes` use `LightDustMote`. Both profile materials now use the same
small project shader. Its radiance comes from the active URP main light and the
scene spherical-harmonic ambient probe. Consequently the current celestial
system's real sun color/intensity and Trilight ambient drive day, warm dusk, and
subtle blue night. There is no clock lookup or duplicated day/night curve.

Magic sparks, gravity motes, meteor streaks, surf trails, sonar and mesh debris
retain their existing materials and shaders.

## Shape and cost preservation

- No particle counts, lifetimes, sizes, velocities, simulation modules, pools or
  renderers change.
- Particle RGB and alpha remain the authored texture sample times particle/base color and
  the existing soft-intersection fade. Lighting changes RGB radiance, not cloud
  density.
- `RumbleDustLit` disables the shader's extra procedural circular mask because its
  existing texture already owns the sprite silhouette.
- Its previous URP Particle/Unlit soft-particle range is retained exactly: near `0.12 m`,
  inverse fade distance `0.7246377` (`1 / (1.5 - 0.12)`).
- The custom pass is transparent, premultiplied (`One`, `OneMinusSrcAlpha`), writes
  no depth, and uses one main-light/shadow query plus one ambient SH sample per
  fragment. It does not create runtime materials or managed allocations.
- A neutral white directional light at intensity `1` clamps the combined direct
  and ambient response to the former unlit albedo. Physical dust therefore keeps
  its ordinary-scene appearance instead of getting arbitrarily darker; lower sun
  intensity, colored dusk light, shadows, and night ambient remain observable.

## Files to integrate together

Copy the contents of this patch's `Assets` directory over the repository `Assets`
directory. The shader, material, validator, and both tests form one atomic change:

- `Assets/Elemental/Content/Shaders/LightDustMote.shader`
- `Assets/Elemental/Content/GraphicsV5/Materials/RumbleDustLit.mat`
- `Assets/Elemental/Authoring/Editor/EarthParticleMaterialValidator.cs`
- `Assets/Elemental/Tests/EditMode/EarthEffectsTuningProfileTests.cs`
- `Assets/Elemental/Tests/PlayMode/EarthDustCompositingRuntimeTests.cs`

No scene reserialization or profile rewrite is required: the scene and profile
already reference these two stable material GUIDs.

## Focused verification after integration

Run, in this order, from an idle Editor:

1. `Elemental/QA/Validate Earth Dust Materials`
2. `Elemental/QA/Run Earth Effects Tuning EditMode Tests`
3. `Elemental/QA/Run Dust Compositing PlayMode Tests`

Expected reports:

- `BuildReports/EarthEffectsTuningEdit.xml` and `.json`
- `BuildReports/DustCompositingPlay.xml` and `.json`
- pixel captures and metrics in `BuildReports/DustCompositing/Latest.json`

Acceptance requires:

- every physical dust profile slot and ambient motes use
  `Elemental/Light Dust Mote` with no emission property;
- day dust luminance exceeds dusk, and dusk exceeds night;
- the runtime reconstruction of the exact prior URP Particles/Unlit hidden
  soft-fade state first proves that it is visible against the background;
- the patched neutral-day capture differs from that reference by at most `2/255`
  both over the complete visible sprite footprint and in every RGB channel;
- day/night center-pixel difference is greater than `0.025`;
- dusk receives a measurably warmer red share than night;
- night dust remains subtly visible against the no-dust night capture;
- dust-over-chip and opaque-world occlusion checks remain green.

These tests are an isolated URP pixel gate. Final approval still needs real camera
captures of an arena fracture, a thrown-stone impact, surf/plough dust, stone fade,
and ambient motes at day, sunset, and night. Counts and silhouettes should match the
pre-patch capture while radiance follows the scene lighting.
