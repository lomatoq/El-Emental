# Loading-scene registration follow-up

Against the actual imported ProductionRestore baseline. One runtime file only; scene lookup/registry ownership is unchanged. The 09:01:11 production failure occurs during MagicExecutor/EarthMatterReturnController Awake, before the additive scene is marked loaded. The scene is nevertheless valid and already owns the component.

Use the same validity rule as Unity Scene.GetRootGameObjects: reject invalid scenes, and require isLoaded only outside Play Mode. Unity reference: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/SceneManager/Scene.cs (ValidateGetRootGameObjects). This keeps scene-local root enumeration during runtime loading and preserves the Edit Mode error. No global lookup, cross-scene fallback, registry reset or exception suppression is added.

The unchanged actual ProductionArenaRestorePlay fixture already reproduces the failing loading path; rerun it after this import. Prior policy Edit6/6 remains valid. Runtime compile is recorded under compile; no Unity execution performed by this task. Actual Assets unchanged pending coordinator integration.
