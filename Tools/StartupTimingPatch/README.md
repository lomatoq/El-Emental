# Startup synchronous timing probe

This is measurement-only instrumentation for one cached revision-3 startup sample. It does not change capacity, initialization order, readiness, physics, or visuals.

Apply from the repository root:

```powershell
git apply --whitespace=nowarn Tools/StartupTimingPatch/startup-timing.patch
```

The patch adds `EarthStartupTiming`, ten narrow source scopes, and the `synchronousStartupTiming` string to `StartupSample.json`. Recording stops exactly when `EarthSceneReadinessGate` becomes ready, so later wall/platform casts do not contaminate the sample. The same values appear as `Elemental.Startup.*` Profiler markers and in the gate log.

Run the existing menu item after Unity compiles:

`Elemental/QA/Measure Production Startup Cached`

Expected revision-3 cache invariants:

- `bakedPlans = 1747`
- `preparedPlansAtReady = 0`
- `bakedPlanMissesAtReady = 0`
- `cookedMeshes = scheduledCookedMeshes`
- `preparationDeltaPrimary = 0` and `preparationDeltaSecondary = 0`

Interpret `synchronousStartupTiming` as inclusive totals. `ArenaInitialize` includes `ArenaBevel` and `ArenaMeshPicking`; `PillarPoolAwake` includes its two child scopes; `WallAwake` includes `WallPieceVisuals`. The difference between an inclusive total and its children is component creation, validation, collider assignment, material setup, joints, and other uncategorized work in that owner.

`enterToReadyMs - gateAwakeToReadyMs` remains the pre-gate Editor/domain-reload/asset-deserialization interval. These source scopes intentionally cannot measure it because no scene `Awake` has executed yet.
