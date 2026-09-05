# Hidden-Player visual evidence repair

The first fresh Windows Player produced valid readiness timings but both
`ScreenCapture.CaptureScreenshot` files were black. The D3D11 device initialized;
the hidden Player swapchain is therefore not a reliable visual evidence source.

The staged loader renders the actual enabled Bootstrap or production camera into a
temporary offscreen RenderTexture, reads that target, writes the PNG, and records
luminance/range evidence. Camera target and active RenderTexture are restored in a
`finally` block. This does not expose a window or alter the scene. The runner now
rejects black cover frames and flat/black production frames instead of accepting
file existence.

Integration targets:

- `after/Assets/Elemental/Runtime/Bootstrap/EarthBootstrapSceneLoader.cs`
- `Run-FreshPlayerStartup.after.ps1` →
  `Tools/BootstrapLoadingPatch/Run-FreshPlayerStartup.ps1`

After compile/build, run the existing fresh Player script. Accept only a Ready report
with both capture `success` values true, nonblack luminance gates passing, and manual
review of both PNGs. `RenderPipeline.StandardRequest`/`SubmitRenderRequest` captures
the actual URP base-camera stack offscreen, while the timing/state machine proves the
loading cover ownership; it does not claim to rasterize IMGUI text.
