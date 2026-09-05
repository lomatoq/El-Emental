# Day and night restoration — 2026-09-04

The final horizon/celestial production-camera matrix completed at
**2026-09-05T13:31:09.8627771Z** in Unity **6000.5.7f1** with all **9/9** live
numeric contracts passing and transient camera/time/owner state restored. The
nine inspected PNGs have no atmosphere-shell chord, horizontally stretched
cloud strip, latitude-pole pinwheel or surface-visible system planet. They show
the Moon at crescent, quarter and full phases; the full view has stable crater
and maria detail across the face, and the low-horizon full Moon takes on a warm
amber tint from the dusk atmosphere. The system planet is absent from the
surface frame and appears only after leaving the atmosphere, at recorded
visibility **0 / 0.4999993 / 1**. Evidence is
`BuildReports/EnvironmentAnimationRescue/SkyHorizonCelestial/20260905T133108125Z/CaptureReport.json`
and its nine PNGs. This is scoped visual acceptance at the captured production
camera poses, not a claim about every viewpoint.

The matching pure atmosphere-envelope gate passed **3/3 EditMode tests** at
**2026-09-05T13:10:03.9278868Z** in **0.1009606 seconds**, with zero failures,
skips or inconclusive tests. `BuildReports/AtmosphereEnvelopeEdit.json/xml`
checks the fixed physical height, legacy/invalid-value fallback, monotonic
system-body reveal and exact hidden/half/full thresholds.

Final runtime verification on 2026-09-05 passed **3/3 PlayMode tests** at
**2026-09-05T09:22:30.4836373Z** in **8.8616995 seconds** after the night
readability correction. Evidence is `BuildReports/DayNightSkyPlay.json/xml`.
The actual production Game camera matrix completed and restored at
**2026-09-05T09:23:46.6470552Z**. `PlayerNight.png` visibly contains the arena,
outer columns, both characters and their feet as dark readable silhouettes;
`PlayerDusk.png` is pink/orange and `PlayerNoon.png` remains daylight-bright.
`Night.png`, `StarsUp.png`, both sun-horizon frames and the scoped curved-column
frame were also inspected. Evidence and telemetry are in
`BuildReports/EnvironmentAnimationRescue/VisualFinal/CaptureReport.json` and its
eleven PNGs. This closes the earlier pending game-camera visual check for these
fixed viewpoints; it is not a claim about every possible camera position.

Implementation on the dirty `d2174ed` workspace, Unity6000.5.7f1. Focused
EditMode verification passed **7/7** at **2026-09-04T16:28:49.5242450Z** in
**1.606182 seconds**, with zero failures/skips/inconclusive tests. Evidence:
`BuildReports/DayNightSkyEdit.json` and `.xml`.
Focused PlayMode verification passed **3/3** at
**2026-09-04T16:51:53.8701888Z** in **24.738432 seconds**, with zero
failures/skips/inconclusive tests. Evidence: `BuildReports/DayNightSkyPlay.json`
and `.xml`. This was scoped solver, authoring and runtime evidence. Its then-pending
game-camera check is superseded by the September 5 captures above; an exhaustive
GPU performance survey is not claimed.

Root integration has baked `EqualAreaStars.cubemap` and `DayNightSky.mat`
(asset timestamps16:04:52–16:04:53 UTC) and saved the existing EarthCoreSlice.
The saved backdrop now has explicit profile, planet, lighting anchor, camera,
Sun, atmosphere and material bindings, with animated authority1 and no mesh-sun
binding. The sky controller references the same profile, camera and material.

The saved EarthCoreSlice backdrop had null profile, camera, planet, light and sky
material references. Its authority was also GameplayLocked. The restoration tool
rewires those existing components in the current saved scene, without rebuilding
the arena, columns, character or lighting settings.

`CelestialSystemProfile` exposes `daylightSeconds = 300` and `nightSeconds = 300`.
The first is sunrise to sunset at the arena's reference latitude. Phase 0 is
sunrise, .25 noon, .5 sunset and .75 midnight. Independent durations remap the
two half-orbits; start phase, pause and time scale remain configurable. A player
walking around the sphere naturally encounters a different local sunrise time.
`DaySeconds` now returns the whole cycle length for compatible consumers. A
versioned migration reads the former whole-cycle `daySeconds` field as two equal
halves (480 becomes240/240), once only. The current approved profile explicitly
uses schema1 and300/300; subsequent user duration edits are preserved.

The physical directional Sun, shader solar disc and atmosphere all consume the
same unrotated solar direction and shared solar hue. Global lighting references
the arena's stable radial anchor, so orbiting the third-person camera cannot
change world illumination. The camera's radial horizon is computed separately.
Solar intensity fades to zero below the lighting anchor's radial horizon, while
the visible disc is occluded by the actual planet sphere/depth: an elevated camera
can see below the tangent plane without the sun disappearing above the planet edge.
Trilight remains the low-level night ambient, with the existing
`nightAmbientIntensity = 1.05`. Actual SH evaluation showed that this alone was
too weak after convolution and ACES for the dark arena material. The existing
`moonlightIntensity` profile setting is therefore now used by a runtime-only,
shadowless directional moon fill; the current saved profile and new-profile
default are **0.8**, with the authored `moonColor`. It follows the lunar azimuth
and uses a small 0.28 local-up altitude floor only for the readability light, so
a moon-below-horizon phase cannot make gameplay fully black. The visible moon
continues to use its true ephemeris direction. `RumbleRockLit` now evaluates URP
additional-light diffuse, making the fill deterministic on the arena and outer
columns instead of depending on URP choosing it as the main light. The generated
fill is not saved into the scene and is disabled under gameplay-locked lighting.
Existing Sun shadow enable, strength, bias, cascade and distance settings remain
unchanged; the night fill casts no shadows. The former mesh sun is retained but
disabled to prevent a doubled solar disc. QA phase seeking no longer turns the
sun or moon toward the camera.

The atmosphere profile now combines its authored radius multiplier with a fixed
minimum physical height. The current base radius is **55.1 m**, the minimum
height is **8 m**, and the effective outer radius is therefore **63.1 m**. The
final normal-camera capture was at radius **60.5795 m**, inside that fixed shell;
the shell does not follow the camera. This removes the former dark curved chord
that appeared when the ordinary arena camera was incorrectly treated as being
in space. System-body visibility uses the same pure envelope policy and fades
over 12 percent of atmosphere thickness only after the observer crosses the
outer radius.

Fullscreen clouds now sample the existing noise through a softly blended
cube-style world-direction projection. It keeps finite two-dimensional angular
variation at the horizon and has no latitude/longitude pole, so neither the old
single stretched horizon strip nor the intermediate triangular pole pinwheel is
present in the final yaw and Moon frames. Coverage, opacity, motion, dusk tint,
night fade and the sky-only geometry boundary remain unchanged.

The Moon stays visible through atmosphere while other system planets are hidden
from the surface. Its transparent premultiplied pass does not write foreground
scene depth, letting the later atmosphere scatter over it while opaque world
geometry still occludes it. The physical Sun direction supplies its terminator
and real crescent/quarter/full phases. A tidally locked presentation rotation
keeps the detailed hemisphere facing the planet; object-space maria, varied
crater rims and fine grain remain stable through the orbit. The legacy blue base
material is neutralized for lunar stone, while low-altitude Mie/twilight color
warms the Moon near the horizon. A nearly new Moon fades with illumination
instead of becoming an opaque black disc.

`EarthSkyProfile` exposes star count, deterministic seed, exposure, dusk pink,
sky colors and solar size/glow. Change count or seed, then rerun the restore tool
to rebake. Stars use independent uniform-sphere samples with varied brightness
and temperature, baked into a 1024px-per-face cubemap with angular Gaussian spots
and mipmaps. Each mip is baked from the same continuous spherical radiance function
with an angular pixel filter and energy compensation, rather than clamping
neighbouring-face contributions during per-face mip downsampling. Star spots
straddling a cube edge contribute to both faces. The linear RGBA32 cubemap is
about32MiB including mips; the baker discards its runtime CPU pixel copy. The
native YAML asset occupies67,110,003 bytes on disk because pixel bytes are encoded
as text; this is distinct from GPU memory. Runtime readability/memory checks are
included in the passing PlayMode suite. Rebaking
builds a fresh texture in memory then copies it into the existing asset, keeping
references stable. There is no latitude grid, Fibonacci spiral or thresholded
Cartesian cell field. Runtime sky shading uses one cubemap lookup. Its rotation affects
stars and the subdued galactic band only; solar direction and radial horizon do
not rotate with the star map. The existing atmosphere remains sky-only, retaining
the earlier fix for moving bands over opaque terrain.

## Integration and verification

1. Compile, then invoke
   `Elemental.Authoring.Editor.DayNightSkyRestore.RestoreCurrentScene()` in Edit
   mode with EarthCoreSlice active. It first resolves unique existing objects and
   validates shader/asset paths, then saves the completed sky assets and validated
   scene. M3's sky-generation block now uses the same material builder and authors
   an explicit stable north-pole anchor because arena integration happens later.
   No full scene rebuild is needed for the current integration.
2. Invoke `Elemental.Tests.EditMode.DayNightSkyTestLauncher.RunEdit()` and
   `RunPlay()`. Reports are `BuildReports/DayNightSkyEdit.json/xml` and
   `BuildReports/DayNightSkyPlay.json/xml`.
3. Seven day/night EditMode tests cover exact300-second daylight, independent night duration,
   wrapping/seeking/pause, radial illumination, legacy migration, elevated-camera
   horizon visibility, cubemap basis edge/corner continuity and equal-area stars.
   Runtime tests cover production bindings/authority, progressing clock, shared
   solar directions/color at four phases, warm dusk, dim night ambient, star fade,
   discarded CPU pixel memory, the shadowless profile-driven moon fill and
   invariant world lighting under camera orbit.
   These three runtime tests include the CPU measurement test below.
   The legacy fixture imports an actual temporary native asset containing old
   `daySeconds:480`, verifies240/240, then saves/reimports authored300/120 and
   verifies that migration does not overwrite them. An earlier raw Editor JSON
   fixture failed6/7; it was replaced with this native-asset boundary test without
   changing the working300/300 profile or production migration code.
   Runtime setup waits for `EarthSceneReadinessGate.IsReady` using a130-second
   unscaled deadline before testing clock progression; it does not bypass the
   gate or force `Time.timeScale`.
   Three additional pure EditMode tests cover the fixed atmosphere envelope and
   system-planet reveal policy; their focused menu is
   `Elemental/QA/Run Atmosphere Envelope EditMode Tests`.
4. The runtime fixture measures `Elemental.Celestial.Update` over 40 warmed
   Editor frames and reports CPU mean/maximum. Existing `Elemental.Earth.Sky.Update`
   remains a nested marker. This does not establish GPU cost or frame-time targets.
5. The earlier actual-game capture covers dawn, noon, sunset, night, upward stars,
   both low-sun horizons and a curved-column detail. Night silhouettes, warm
   twilight and non-rowed stars pass at those viewpoints. The implementation uses
   an artistic atmospheric approximation, not a full physical scattering simulation.
   The later nine-shot celestial matrix separately covers two noon horizon yaws,
   three lunar phases, a warm horizon Moon and surface/half/space system-planet
   visibility; its final inspected run is the `20260905T133108125Z` folder above.

## Research

- [PBRT uniform sphere sampling](https://www.pbr-book.org/4ed/Sampling_Algorithms/Sampling_Multidimensional_Functions)
  derives equal-area sphere sampling. The implementation uses independent random
  inputs to that mapping, not evenly spaced latitude angles.
- [Unity cubemap pixel layout](https://docs.unity3d.com/cn/6000.0/ScriptReference/Cubemap.SetPixels.html)
  specifies bottom-left, row-major face data used by the baker.
- [Unity shadow resolution and distance](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shadow-resolution-urp.html)
  explains the shadow-map density tradeoff. This patch preserves authored values.
- [Unity ambient modes](https://docs.unity3d.com/cn/6000.0/ScriptReference/RenderSettings-ambientMode.html)
  documents sky/equator/ground ambient fill used for night readability.

The former production gameplay-lock policy in broad project documents is
superseded by the user's explicit animated day/night request for this scene.

The source-level edge/corner test establishes face-coordinate consistency, not
pixel-perfect GPU sampling. The root completed the texture bake and scene repair;
the final PlayMode report and production-camera PNGs establish the scoped runtime
and visual results above rather than inferring them from saved artifacts.
