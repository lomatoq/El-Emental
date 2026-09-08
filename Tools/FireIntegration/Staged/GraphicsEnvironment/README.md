# Distant stone environment — source-backed integration patch

Adapted from the three actual C# files and `DistantStoneURP.shader` in the user-supplied `EL_Emental_StoneUI` archive. Unchanged originals remain under `Tools/FireIntegration/Reference/StoneUI/EL_Emental_StoneUI`. No image was regenerated or downloaded by this lane.

## Import map

Copy `after/Assets/` to the matching Assets paths after the Unity owner grants source integration. The eight files add three runtime presentation sources, one shader, one editor installer, EditMode and PlayMode test sources and a QA launcher. No existing game source needs replacement. Namespace is `Elemental.Presentation.DistantScenery`; using `.Environment` would collide with existing unqualified `System.Environment` callers.

Run **Elemental > Environment > Install Distant Stone Backdrop** with the existing EarthCoreSlice scene open. The installer reads its existing VoxelPlanetBehaviour radius (55.1 in this checkout), adds one `Distant Stone Backdrop` root and marks the scene dirty. Coordinator owns save and visual review. A second run selects the existing root and preserves its generated content and authored settings; it does not rebuild the arena. Assets are created only when absent under `Assets/Elemental/Content/Environment/DistantStone/`.

The material uses the user's unchanged detail texture at `Assets/Elemental/Content/UI/Stone/Art/Environment/distant_stone_detail.png`, supplied by the separate UI lane. If that texture is not yet imported, the shader's neutral-gray default still renders, but the detail assignment requires a later explicit material edit.

## Adaptation decisions

- Uses source mesh construction with broader mountain bases and tapering upper masses. Six massif and four island mesh variants each have a lower-detail companion. Island variants remain separate from land variants.
- Seeded bounded rejection sampling replaces regular angular spacing; squared size sampling produces more modest masses and fewer large ones. Layer distances/height ranges stay based on the supplied artistic profile, scaled from the original 36 m reference to the existing radius. A clear view corridor remains editable.
- Pivots include the actual planet center and surface radius. Stored world positions/rotations prevent moving a parent or camera from dragging the scene around. There is no per-frame regeneration or camera following.
- Mountain masses are stationary, with no repeated transform writes unless their parent moved. Islands use the supplied 85–165 s periods, independently seeded phases and bounded 0.65–2.2 m reference displacement. Scaled clock is default so gameplay pause pauses them. The installer binds the existing FrontendFlowController preferences owner explicitly; LateUpdate reads its ReducedMotion value without modifying Flow. Reduced motion restores the absolute baseline. Standalone fixtures can still call SetReducedMotion directly.
- Opaque color/depth rendering uses the existing main light, SH ambient and shared `_ElementalNight01` for its minimum ambient term. The supplied extra distance haze was removed. Existing `AtmosphereFullscreenFeature` remains the only fog/aerial owner. No additional color-space conversion, fog setting, volume, sun, camera setting or shadow caster was added.
- No colliders, rigidbodies, network components, terrain edits, navmesh or gameplay authority.

## Verification

`python validate.py` compiled the final overlay with Unity 6000.5.7f1 Roslyn/current Bee response files. Presentation, Authoring.Editor, EditMode and PlayMode test assemblies all returned **0**. Logs are in `compile/`; shader compilation is not covered by this C# check.

Six authored EditMode tests cover radius/no physics, deterministic owned-root rebuild, absolute/bounded motion and reduced motion, world anchoring under parent movement, stationary land masses without transform writes, and finite outward triangle normals with bounded topology. They have **not run** in Unity in this lane. Run **Elemental > QA > Distant Backdrop Edit**. The Unity owner must import/compile the shader, execute tests, install/save once, then capture the existing gameplay view and a camera sweep in daylight/night/paused/reduced-motion states. Actual profiler cost, LOD transitions, atmosphere appearance and artistic acceptance remain unmeasured/unreviewed.

After installation and save, run **Elemental > QA > Distant Backdrop Production Play**. Its single source fixture loads a fresh additive EarthCoreSlice, waits for the real readiness gate, enters local Bot combat through existing Flow, then uses the actual bound gameplay camera. It checks the bound preference owner, no descendant physics/network components, real two-level LOD meshes, and visible changed gameplay pixels by toggling only backdrop renderers. It calls the existing CelestialSystemBehaviour QA APIs for day/night instead of inventing shader/light state. Captures include normal daytime, backdrop-disabled comparison, three 90-degree sweep directions, night, paused and preference-driven reduced motion. It brackets 256 warmed ApplyTime calls for exact thread GC and scoped mean CPU timing; this is not a whole-frame/GPU benchmark. Outputs are `BuildReports/DistantBackdrop/`. Teardown restores preferences without saving PlayerPrefs, camera pose, celestial phase/authority, renderer flags and time scale, then unloads only the added scene. The fixture is compiled but **unrun** here.

No actual Assets or Unity state was changed by this lane. Original read-thread recovery notes predate receipt of the local archive and do not describe the current source availability.
