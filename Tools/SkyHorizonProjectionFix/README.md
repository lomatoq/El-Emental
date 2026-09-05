# Sky horizon and celestial-composition fix

This is a staged patch. It does not modify `Assets`, materials, profiles, scene
YAML, or the accepted 300-second day / 300-second night clock.

## Confirmed causes

The production Game-view captures show cloud noise compressed into long
horizontal smears parallel to the horizon. `AtmosphereFullscreen.shader`
produces that shape with `ray.xz / rayDenominator`: inside its denominator clamp,
changes in ray elevation have almost no UV derivative, so one noise patch is
stretched along the horizon. The explicit cloud band makes the projection error
more visible. The baked star cubemap is unrelated.

Scaled celestial spheres currently render as opaque geometry and write depth.
The later fullscreen atmosphere returns the source unchanged for geometry depth,
so the Moon/system planet appears in front of the atmosphere. This accounts for
the black circular body visible in recent surface captures and prevents daytime
or warm horizon scattering from coloring the Moon.

The first integrated capture also measured the normal arena camera at radius
`61.035 m`, outside the old atmosphere radius `58.1305 m` (`55.1 * 1.055`).
That produces the hard curved shell/chord boundary across the upper sky and
opens the system-planet visibility gate during ordinary play. The staged profile
therefore defines an 8 m minimum physical atmosphere height: its outer radius is
`max(radius * multiplier, radius + minimumHeight)`, fixed in world space and
still escapable. It does not follow the camera.

## Staged behavior

- `AtmosphereFullscreen.shader` uses a softly blended cube-style projection of
  normalized world direction. It has neither the old planar horizon stretch nor
  the latitude/longitude pole singularity found by the first nine-shot QA. The
  latter appeared as a triangular pinwheel beside the full Moon.
- Existing cloud coverage, band, opacity, tint, dusk response, night fade, Sun,
  shadows, ambient/moon fill, stars, seismic composition, and cycle durations are
  unchanged.
- `ScaledCelestialBody.shader` draws after the skybox, does not write scene
  depth, and uses premultiplied coverage. Opaque arena/planet geometry still
  occludes it, while the existing later atmosphere pass scatters over it.
- The Moon keeps the existing ephemeris direction and physical Sun terminator.
  It remains visible from the surface (including daytime), gains varied object-
  space crater rims, broad maria and fine grain, has no opaque black new-moon
  disc, and takes on the atmosphere's warm low-horizon color. Its legacy icy-blue
  material is neutralized in the Moon branch so atmospheric scattering, rather
  than the base material, supplies zenith and horizon tint.
- The currently authored system body, `Ringed Ember Planet`, is exactly hidden
  at/below the authored atmosphere outer radius. It fades in over 12% of the
  atmosphere thickness after the observer exits. Future system bodies can reuse
  the same renderer property policy; no unreferenced planets are invented here.
- The effective atmosphere encloses the authored terrain/camera envelope. For
  the current planet it ends at `63.1 m`, so the `61.035 m` arena camera is inside
  while an observer beyond `63.1 m` genuinely enters space.

## Integration

After reviewing the no-index diff, copy these staged files to their matching
production paths:

1. `AtmosphereProfile.cs` ->
   `Assets/Elemental/Runtime/World/AtmosphereProfile.cs`
2. `AtmosphereFullscreen.shader` ->
   `Assets/Elemental/Content/Shaders/AtmosphereFullscreen.shader`
3. `ScaledCelestialBody.shader` ->
   `Assets/Elemental/Content/Shaders/ScaledCelestialBody.shader`
4. `CelestialSystemBehaviour.cs` ->
   `Assets/Elemental/Presentation/Rendering/CelestialSystemBehaviour.cs`

No material, profile, texture, renderer-feature, or scene rewrite is required.

## Required verification

1. Refresh/compile and run DayNightSky Edit/Play without changing the existing
   duration or lighting assertions.
2. Capture the production-camera noon and dusk horizon at the former view and
   after roughly 90 degrees of yaw. Individual cloud forms must retain visible
   angular width and height, with no long stretched strip or longitude seam.
3. From the arena surface, verify `Ringed Ember Planet` is absent while the Moon
   is visible through blue daytime and warm dusk atmosphere. There must be no
   foreground black celestial disc.
4. Place a QA camera just inside, exactly at, halfway through, and beyond the
   outer-atmosphere reveal interval. System-planet visibility must be
   `0, 0, approximately 0.5, 1` and restore after QA.
5. Capture new, crescent, quarter, and full phases. The terminator must follow
   the Sun, the full face must be readable with restrained surface detail, and a
   horizon Moon must inherit the orange/pink atmospheric tint.
6. Reconfirm opaque scene brightness, readable night silhouettes, irregular
   stars, dusk color, and moving Sun shadows. The atmosphere pass must still
   return opaque geometry unchanged.

`validate_mapping.py` is a pure test of the causal projection geometry and the
absence of a pole singularity in the final softly blended cube projection.
`validate_celestial_policy.py` pins the altitude gate, new/full center-opacity
policy, and staged render-state contracts. Their JSON reports are not substitutes
for shader compilation or the Game-view captures above.

`SkyHorizonCelestialVisualQa.cs` is a temporary Editor helper for those captures.
Copy it into `Assets/Elemental/Authoring/Editor`, compile, enter a ready unpaused
EarthCoreSlice Play session, then invoke **Elemental > QA > Capture Sky Horizon
and Celestials (Ready Play Mode)**. It captures two horizon yaws, actual ephemeris
crescent/quarter/full Moon views, a full Moon at the warm horizon, and system-
planet visibility at the surface/half-reveal/outside points. It records live MPB
and viewport values, restores camera/time/owners in `finally`, and never saves or
exits Play Mode. The helper passed a standalone Roslyn compile against the current
Unity Editor response on 2026-09-05; Game execution remains pending integration.
