# Reproducible V2 visual gates

These Tools-only C# files are replay inputs for Unity_RunCommand.Code. They are not imported into Assets and do not run automatically. Both compile offline against the actual Unity editor response-file references with a minimal tool-contract stub (compile/report.json). Actual replay remains pending the Unity lease.

## Exact order

1. Run geometry Edit QA before adding preview objects (latest actual result from root:14/14PASS).
2. Menu Elemental/Environment/Procedural Valley/1 Preview Twelve Pillars.
3. Read CaptureTwelvePillars.cs verbatim and pass its contents to Unity_RunCommand.Code. It captures refined12pillars, one isolated pillar close-up and underside. It reuses current meshes/materials/light. CameraType.Preview bypasses the existing atmosphere feature's Game-only pass. Temporary layers/camera/render textures are restored/destroyed in finally; no production light/camera/property edit and no scene save.
4. Clear ONLY preview via new staged Elemental/Environment/Procedural Valley/Clear Preview Only (ValleyPreviewTools), or call Undo.DestroyObjectImmediate on owner.transform.Find("EE_RockPreview_V2") and mark scene dirty. Existing generatedRoot/backdrop remains intact. Do this before any test launcher: root observed test-runner scene saving a previously unsaved preview. If an earlier test already saved the preview, remove its child and let root save the current legitimate scene changes explicitly.
5. Root reviews the12shape images. Only after acceptance replay CaptureTwoGroups.cs: it creates one temporary5-pillar ground group and one4-pillar floating group with closed base, captures front and undersides, writes triangle counts, then destroys temporary geometry/objects in finally. No group mesh asset bank is baked or modified and no valley is populated.
6. Root reviews both group forms. Whole bank bake and valley placement remain a separate later decision.

Output: BuildReports/ProceduralValleyV2/03-refined-twelve-no-fog.png,04-refined-pillar-close-no-fog.png,05-refined-pillar-underside-no-fog.png; later06-ground-front/underside.png and07-floating-front/underside.png,plus two-groups-counts.json.

The source geometry/material is real Unity content; neutral clear background and explicit no-atmosphere camera isolate shape assessment. These images do not prove production camera composition, atmosphere or final gameplay acceptance. Save/reload, exclusion/motion, day/night and fullscene profiler gates follow only after shape approval.
