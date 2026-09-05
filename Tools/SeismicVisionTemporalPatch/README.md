# Seismic vision temporal front patch (staged)

This folder is a candidate replacement for the two matching production files. It
does not modify `Assets` and has not been imported or run in Unity.

## Confirmed source contracts

- The inactive branch returns `source` before the first depth sample, so ordinary
  scene pixels are unchanged by the seismic include.
- The current five-tap normal uses the nearer one-sided neighbour on each axis.
  This avoids mixing a foreground surface with far/background depth at ordinary
  silhouettes. Thin sub-two-pixel geometry remains a visual edge case.
- The production pulse expands `22 m` in `2.2 s`, or `10 m/s`. Its fixed `0.32 m`
  transition is crossed in `0.96` frame at `30 Hz`; `fwidth` only measures spatial
  screen derivatives and cannot make that transition temporally continuous.

## Candidate correction

`EarthSeismicVision.cs` publishes the distance each live radius advanced since its
previous rendered update. `EarthSeismicVision.hlsl` averages the pulse response at
the previous and current radii, a two-sample temporal box filter. A 30 Hz world point
which would jump from baseline to peak is shown at half temporal coverage first,
without permanently broadening a stationary front. Slot reuse resets travel to zero,
loss of foot support clears the array, and the existing five-pulse/no-allocation shape
is retained.

## Required acceptance before integration

1. Run the existing `SeismicVisionEdit` 7-test and `SeismicVisionPlay` production
   ground/launch/resume gates.
2. Add a controlled 30/60/120 Hz rendered sequence which tracks a fixed visible
   opaque patch as one front crosses it. At 30 Hz, no tracked patch may go directly
   from baseline to peak without at least one intermediate luminance sample.
3. Capture ordinary day and night with `_EarthSeismicVision = 0` and compare the
   actual render target byte-for-byte against the pass disabled. This protects the
   early-return contract.
4. Recheck foreground silhouettes and geometry thinner than the two-pixel normal
   baseline in the production Game camera. Do not call the effect visually accepted
   from the existing single `NightGrounded.png` frame.

`SeismicVisionTemporalPixelTests.cs` and `SeismicTemporalPixelTest.shader` implement
the deterministic GPU portion of items 2 and 3. The test renders the extracted
production pulse helper at 30/60/120 Hz, writes temporal strip PNGs, and requires an
intermediate luminance sample before peak. Its inactive checks render the same fixed
day/night source texture through a direct branch and `ApplyEarthSeismicVision` with
the global disabled, then compare every `Color32` exactly. These are actual shader
pixels with no live camera, clock, particles, TAA or scene motion, so a difference is
owned by the include rather than capture nondeterminism. Production Game-camera
silhouette inspection remains a separate visual gate.

The deterministic CPU evaluation of the same `smoothstep` expression predicts the
specific 30 Hz regression sequence `0,255,189,0` for current-radius-only sampling
and `0,128,222,94` for the candidate. The GPU test asserts the reproduced first jump
and the candidate's half-coverage value with 8-bit quantization tolerance; 60/120 Hz
retain their own intermediate-sample assertions rather than inheriting the 30 Hz
fixture.

Integration mapping is explicit: copy `EarthSeismicVision.hlsl` to
`Assets/Elemental/Content/Shaders/`, `EarthSeismicVision.cs` to
`Assets/Elemental/Presentation/VFX/`, the `.shader` to an imported test-shader
folder, and both test `.cs` files to `Assets/Elemental/Tests/EditMode/`. Then invoke
`Elemental.Tests.EditMode.SeismicVisionTemporalTestLauncher.Run()`. The focused JSON
and XML report names are `BuildReports/SeismicVisionTemporalPixel`, while the six
temporal/reference strips are under `BuildReports/SeismicVisionTemporal/`.

`EarthTriplanar.remove-unused-seismic.diff` removes three legacy declarations that
have no reads anywhere in that shader. The fullscreen include remains the sole
seismic presentation owner.
