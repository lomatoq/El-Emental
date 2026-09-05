# All 11 Magic Visual QA (staged)

This helper stays outside `Assets` until the Unity owner opens a stable compile window.

1. Copy `AllMagicVisualQaDriver.cs` to `Assets/Elemental/Authoring/Editor/`.
2. Open `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity` in Edit Mode.
3. Run **Elemental > QA > Capture All 11 Magic Animation Matrix**.
4. Inspect `BuildReports/EnvironmentAnimationRescue/AllMagicVisualQA/<UTC>/AllMagicVisualManifest.json`
   and the 36 PNGs (three stages for all eleven slots plus a second complete QuickStonePunch cycle).

The driver uses the live production camera, Animator, EAMM/IK stack and presentation profile. It disables
only player locomotion input, bot/duel gameplay driving and debug UI, restoring each saved value in cleanup.
It does not disable animation modifiers. This matrix invokes semantic presentation directly; physical
short-LMB quick stone and simultaneous LMB/RMB routing remain the responsibility of the dedicated runtime tests.
