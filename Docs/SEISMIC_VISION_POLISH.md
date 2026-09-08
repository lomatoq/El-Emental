# Seismic vision polish — 2026-09-06

Implemented in the uncommitted working tree on main `1235579`.

## Player-visible behavior

- V blends the world and hostile reveal over one second in either direction,
  using smoothstep. Reversing a transition preserves its current progress.
- Running uses 2 m between step pulses instead of 0.72 m (about 2.8 times fewer
  movement-triggered pulses). The existing 0.68 s automatic heartbeat remains.
- Front half-width is 0.06–0.12 m and edge feather 0.16 m, both half their prior
  values. Temporal integration remains enabled at 30/60/120 Hz.
- Ordinary front gain is 0.70 standing and 0.28 moving, smoothly changing over
  0.18 s. The revealed environment still provides its existing shape shading.
- The opponent's actual animated mesh is drawn over all occluders only when
  the shared wave/afterglow reaches it. It is brighter than the world waves;
  there is no permanent or pre-wave silhouette.
- Saved night ambient RGB and moon fill are 5% lower; daylight settings are unchanged.
- Ground support remains authoritative: airborne, mantle or non-Earth state
  immediately clears perception. Landing starts fresh pulses and fades back in.

## Integration

The existing presentation composition root binds the duel's explicit player and
bot references to `EarthSeismicCameraTargets`. Mesh/submesh lists are built only
on scene setup. The existing atmosphere RenderGraph pass draws the hostile mesh
after the fullscreen recolour using its second shader pass (`ZTest Always`,
no depth writes). No additional fullscreen pass, cloned avatar or gameplay state
is introduced. The hostile shader and world shader share `EarthSeismicField`.

This is scoped to the current duel's opponent. Independent split-screen vision
sources are not supported by the pre-existing global wave uniforms.

## Fresh evidence

- `BuildReports/SeismicVisionEdit.json`: **10/10 EditMode**, 00:04:25 UTC;
  contact authority, wave lifetime, fade progress/reversal at 30/60/120 Hz.
- `BuildReports/SeismicVisionTemporalPixel.json`: **5/5 GPU EditMode**, 00:18:06 UTC;
  thin-front temporal sampling and byte-exact inactive day/night output.
  The fixture now tests half-coverage peaks for the thinner shell at low FPS,
  rather than requiring the former thick shell's full-white plateau.
- `BuildReports/SeismicVisionPlay.json`: **2/2 production PlayMode**, 00:16:44 UTC;
  fade/reversal, actual movement attenuation, launch/landing and hostile occlusion.
- Through an opaque QA wall, **4814** rendered pixels change when the wave reveals
  the actual opponent. Before-wave and mode-off images match the disabled overlay
  byte-for-byte. `EnemyThroughWall.png` and its control image were visually inspected.
- `NightWalking.png`, `NightFadeOut.png` and `NightNormal.png` were inspected in
  `BuildReports/EnvironmentAnimationRescue/SeismicVision`.
- Existing `Elemental.SeismicVision.Publish` marker remains allocation-free in
  its code path; recorded last idle frame was 700 ns across publishers. This is
  not an active GPU benchmark or a claim about full-game performance.

The initial expanded lifecycle test checked launch before the next presentation
Update and failed. Synchronizing the launch at end-of-frame fixed the fixture;
the production contact cutoff was unchanged. Independent code review found no
blocking defect for the current duel.

## Running visibility and persistent shader capacity (2026-09-06)

The running diagnostic exposed a separate editor-lifetime failure after expanding
from five to sixteen pulses: Unity retained each global array's original capacity
through domain reloads. `HudSonarReadabilityPlay.xml` logged `16 vs 5` truncation
for waves, strengths and radius travel. New pulses in slots 5–15 never reached
the shader; CPU mode/pulse checks still passed. The explicit player camera capture
showed a newest uploaded wave already 1.27 seconds old despite the 0.68-second
automatic cadence.

All three uniforms now use capacity-versioned `...16` names in the publisher,
shared HLSL and GPU fixtures, so the existing editor session allocates all sixteen
slots. Wave brightness, width and frequency are unchanged. The running regression
checks all uploaded array lengths, a recent wave, and actual pixel contribution
against the same player camera with wave strengths disabled. Captures and camera,
player and pulse coordinates are written under the existing SeismicVision report
folder. Fresh Unity validation is coordinated by the main task.
