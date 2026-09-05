# Camera-rendered bootstrap cover

## Diagnosis

The rejected fresh-player report is valid: `BootstrapCover.png` contains almost
only zero-valued pixels (`meanLuminance = 0.00038297`, maximum `1/255`, and
`nonBlackFraction = 0`). The visible loading cover is currently drawn in
`EarthBootstrapSceneLoader.OnGUI`. `RenderPipeline.StandardRequest` renders the
URP camera stack into the offscreen destination but does not composite IMGUI, so
the evidence request photographs the otherwise empty Bootstrap camera. The
shared fullscreen atmosphere feature also runs for every Game camera. Giving the
bootstrap camera real opaque cover geometry makes that pass take its existing
geometry-preserving path and avoids depending on uninitialized atmosphere globals.

Changing the luminance threshold or painting pixels into the captured Texture2D
would only hide the mismatch. The staged fix changes the actual loading render
path instead.

## Patch

Copy `EarthBootstrapSceneLoader.cs` to
`Assets/Elemental/Runtime/Bootstrap/EarthBootstrapSceneLoader.cs`.

At Bootstrap `Awake`, the loader creates a transient camera-child quad using the
already shipped URP Unlit shader and a centered `TextMesh` using Unity's built-in
`LegacyRuntime.ttf`. The quad has opaque depth, no lights/probes/shadows, the
authored `coverColor`, and two-percent frustum overscan. It and the status text
therefore appear in both the visible player and the same URP StandardRequest used
for evidence. The old IMGUI path remains only if the camera, shader, or font is
unavailable; that path is explicit in the report and is rejected by the runner.
All objects, mesh and material are runtime-only `DontSave` data and are destroyed
with the Bootstrap loader. Nothing is saved to the scene.

Replace `Tools/BootstrapLoadingPatch/Run-FreshPlayerStartup.ps1` with the staged
runner after integration. It preserves the existing strict mean/nonblack checks,
requires `bootstrapCoverRenderPath == camera-geometry-and-text`, and adds a
minimum 0.10 luminance range so a flat clear color cannot masquerade as a complete
cover: the captured status glyphs must produce visible contrast.

## Verification

1. Compile the Runtime assembly with no new diagnostics.
2. Build the normal Windows player from the saved Bootstrap first-scene order.
3. Run the staged `Run-FreshPlayerStartup.ps1` against that new executable.
4. Inspect `BootstrapCover.png`: it must show the dark blue authored cover and
   centered real status text. Inspect `PlayableReady.png` separately.
5. Require `status=Ready`, both capture successes, camera render path, cover mean
   above `.004`, nonblack fraction above `.95`, cover range above `.10`, and the
   unchanged playable-image brightness/range gates.

The old `20260905-133538-792` report remains rejection evidence and must not be
relabeled as a pass. A fresh built-player report is required.
