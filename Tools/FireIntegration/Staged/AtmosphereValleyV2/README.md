# Atmosphere Valley V2 â€” complete first candidate, staging only

## Contract / scope

Hide the blue empty lower sky with a smooth pseudo-fog sea; retain crisp warm arena and near islands; give far opaque rocks continuous air perspective using their unchanged arena material; add a few recognizable soft cloud banks above the veil. No DistantBackdrop/Profile/ProceduralRockMesh changes (geometry agent owns those), camera, light, UI, renderer feature order or material/vignette defaults changed. Actual Assets and Unity untouched during preparation.

Existing feature is one depth-bound RenderGraph fullscreen pass BeforeRenderingPostProcessing, after transparent objects. Existing opaque branch deliberately bypasses midpoint-density fog because that made screen-space altitude bands on the arena. The V2 branch replaces the old atmosphere/cloud-cue composition when enabled; disabling the flag takes the exact original code path. It retains the existing sunlight cue on opaque objects, seismic presentation and final vignette (material _GameplayEdgeDarkness=0.2 untouched). No new fullscreen pass/render texture is introduced.

## Candidate

before/ preserves the sole modified existing file AtmosphereFullscreen.shader (SHA256 in Reports/oracle-numerical.json). after/ adds shader include ValleyAtmosphereV2.hlsl; pure Simulation.Rendering.ValleyAtmosphereMath; presentation profile and explicit frame publisher; typed installer/oracle capture menus; EditMode tests.

Density rho(h)=density*exp(-max(h,0)/falloff), with plane -planetRadius-15m in the authored staging frame. A piecewise exact analytical integral handles crossing/parallel rays and constant density below the plane, without sample marching or plane geometry. For depth-backed surfaces integration ends at reconstructed world depth. Sky integrates analytically to infinity: downward/parallel sky rays become an opaque veil rather than terminating at the camera far plane and leaving a blue hole. Upward rays have a smooth finite exponential extinction, continuous at the horizon. Source alpha is preserved.

Opaque protection: exact zero V2 extinction before300m; smooth300â€“400m transition. Surfaces inside planetRadius+80m remain protected even in an external overview; protection releases smoothly by radius+140m. Far attenuation uses path distance/1800m, maxopacity0.60, combined with analytic low fog. This is a warm-neutral veil, not a blue material repaint. Sunlight cue retains the existing source behavior. Fog top/up/forward are fixed to a planet child in the authored backdrop stagingUp/heroViewDirection frame; matrix strips inherited scale to keep world-metre units.

Clouds use the existing generated RGBA reference-bank-v1.png, but NOT the disabled ten-card component. Three fixed world-space bank planes are sampled analytically in the same compositor after veil. Their authored centers/size (650/1000/1500m depth) form uneven framing, with sky-facing cumulus towers above the lower veil. Three conditional texture samples maximum, bounded sort for far-to-near compositing from reverse/side views, scene depth intersection/fade, rectangle feather, explicit pre-divergence UV gradients for mip sampling. Artwork obeys the same protected opaque masks and finite analytic camera-to-bank transmittance as fog. Small scaled-clock sinusoidal8m/160s drift is computed once per frame in a cached3-element array; no camera follow. Cloud silhouettes remain 2.5D and may reveal rectangles/repetition â€” actual QA must accept/reject this independently.

## Research and rationale

Cyanilux's primary implementation tutorial documents shallow-angle distortion in simple plane-depth subtraction and uses reconstructed positions to improve the result; it also warns that crossing a literal fog plane can remove the effect. We retain its world-space/depth principle while choosing a full-screen analytic halfspace, which has no plane crossing disappearance: https://www.cyanilux.com/tutorials/fog-plane-shader-breakdown/ (updated2026-02-18).

Unity's URP world-position reconstruction documentation specifies sampled scene depth, ComputeWorldSpacePosition with inverse VP, and reversed-Z/non-reversed near-clip handling. V2 uses those conventions and separate orthographic near origins: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/writing-shaders-urp-reconstruct-world-position.html . These sources support the depth conventions; the exponential halfspace integral and artistic defaults are our implementation decisions, not quoted Unity recommendations.

## Verification completed offline

Unity's current Roslyn response files compiled Simulation, Presentation, Authoring and EditMode tests with zero errors. Existing unrelated obsolete-authoring warnings remain in logs; no new warning introduced by these files. A separate equation-level Python translation compared54 cases to independent20,000-sample midpoint quadrature; maximumrelativeerror9.0814e-9. Actual existing shader still exactly matched before snapshot. This is not GPU shader validation or execution of NUnit tests.

Staged NUnit:6 quadrature cases + shallow downward/upward asymptote + far gradient monotonicity/clear zone + invalid numeric guard (12 cases). Revision R1 adds protected reverse-view cloud composition, fog attenuation to each bank, and explicit pre-branch mip gradients. No Unity launch/calls/captures/GPU/GC measurement occurred.

## Exclusive owner integration / review

1. Verify sole existing shader still matches before, copy after/Assets, refresh. Run Elemental/QA/Run Valley Atmosphere V2 Tests and require12/12; inspect actual shader errors and console.
2. Execute Elemental/Graphics/Install Valley Atmosphere V2 in nonplaying EarthCoreSlice. Installer uses existing explicit planet/backdrop/material/generated art, creates only owned frame/profile and disables only typed rejected ValleyCloudStrata. Its original active state is stored. It never reenables10CloudBankCards. Leaves scene dirty for review. Original atmosphere restoration menu restores prior volume active state and switches V2 off without deleting content.
3. In a ready production Play session, Elemental/Graphics/Capture Valley Atmosphere V2 Oracles captures day/night Ã— gameplay/lookdown/overview/under/shallow Ã— original/veil/clouds/alpha (40 frames). Flags, camera pose, targetTexture, time and lighting authority restore in finally. Capture does not alter shipping postprocess, so existing custom blur may still obscure cloud detail; alpha oracle includes final vignette/seismic by design.
4. Reject if near arena/planet color changes, distant opaque gradient has moving altitude bands, depth edges halo, shallow/downward sky shows blue holes, old volume stacks with veil, cloud rectangles/repetition dominate, or camera-relative swimming appears. Inspect clouds separately from fog using the factorial images. Use off/original only as a sky reference with rejected volume disabled during QA, not as proof of original scene equivalence.
5. Measure whole feature GPU delta and publisher CPU/GC at1080p. Profiler marker Elemental.ValleyAtmosphere.Publish plus existing Elemental Atmosphere Fullscreen. No budget or art acceptance claimed from source. Sky analytic fog is unbounded spatially on purpose, while work per pixel is bounded/no marching; cloud sampling remains3 banks.

Source frozen for handoff. Parent owns public tracker updates and actual Unity window.
