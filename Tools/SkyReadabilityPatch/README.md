# Sky readability staged patch

Copy the mirrored `Assets` tree over the project `Assets` tree, then refresh Unity.
The patch keeps the saved 300 second daylight and 300 second night durations and
does not change the directional light's authored shadow settings.

PowerShell integration from the Unity project root:

```powershell
Copy-Item -Path 'Tools/SkyReadabilityPatch/Assets/*' -Destination 'Assets' -Recurse -Force
```

Run the existing DayNightSky EditMode and PlayMode fixtures, then enter Play after
the Earth readiness gate passes and invoke:

```text
Elemental.Authoring.Editor.EnvironmentVisualQa.Run()
```

The report must contain `PlayerNoon`, `PlayerDusk`, `PlayerNight`, both unobstructed
sun-horizon shots, `StarsUp`, and `ColumnCurveDetail`. Inspect the PNGs; numeric
ambient and twilight telemetry is a guard, not visual acceptance.

Production changes:

- arena-anchor solar altitude drives both skybox and fullscreen atmosphere twilight;
- observer radial-up still drives the geometric sky horizon and planet occlusion;
- low-sun Rayleigh no longer saturates over Mie, and clouds retain warm dusk color;
- night Trilight and sky luminance have separately exposed, legacy-safe strengths;
- noon light, day/night duration, sun path, light shadows, stars and post processing
  remain under their existing owners.

The animation QA files make the existing driver capture the production player as
the required subject. The production bot remains in the scene and is restored, but
cannot invalidate a player frame after moving behind the live camera. The Wall-only
magic sequence remains Wall-only evidence; it does not accept all magic slots.
