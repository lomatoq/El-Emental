# Arena lookdev stale test expectations
One staged EditMode test file, no production assets or Unity writes.

FacetContrast expected .16 -> .12: both current exterior/interior materials serialize .12, and parent confirms latest explicit softer-shading authorization. All exact tolerance and cross-material parity checks retained.

SavedArenaPreservesEveryAuthoredChildTransform renderer expectations Off/receiveFalse -> On/receiveTrue. This is NOT weakening to accept a new regression:
- committed HEAD 1235579 BrokenCrownArenaRendererPostprocessor.cs ContractVersion2 sets On + receiveTrue for every imported arena renderer, unchanged in actual working tree.
- committed HEAD BrokenCrownArenaSceneIntegrator.cs has the same On/true policy in renderer setup and scene refresh paths.
- ADR0033 Shading/shadows/aliasing explicitly requires real main-light shadows in Scene/Game/standalone.
- Docs/ARENA_GRAVITY_ACQUISITION_FIX.md lines48-53 already documents old Off assertion failing before this graphics work, with user-saved shadow settings deliberately retained.
- All authored transform, arena embed, bot seating and >=375 child comparisons remain untouched. New expectations strictly enforce committed policy.

The separate PlayMode BrokenCrownArenaRuntimeTests line145 still has legacy Off assertion; not edited in this scoped stage. Root may update in a separate evidenced pass if executing that suite. No tests run in Unity here; parent owns fresh execution.
