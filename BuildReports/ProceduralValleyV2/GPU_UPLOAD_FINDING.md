# Mesh authoring GPU refresh regression

Actual Unity comparison, September 7 2026: runtime-generated Pillar seed 13771 and saved Preview/Pillar_00.asset had exactly equal CPU vertex arrays (228 vertices, 144 triangles), yet rendered different shapes with the same camera and material. `08-transient-pillar-front.png` shows the new segmented geometry; `09-baked-pillar-front.png` shows the previous trunk geometry. `live-descriptor.txt` records loaded assembly identity and two part Y ranges.

Cause localized to updating an existing mesh asset with EditorUtility.CopySerialized. Updated ProceduralValleyAuthoring.SaveMesh to clear and explicitly assign vertices, normals, colors, triangle indices and bounds, then UploadMeshData(false). Asset identity is retained. A regenerated preview now visibly shows the expected segmented geometry in `10-correct-gpu-twelve-no-fog.png`, `11-correct-gpu-pillar-close-no-fog.png`, and `12-correct-gpu-pillar-underside-no-fog.png`.

Images 03–05 are stale GPU geometry and must not be used to judge the refinement. Images 06–07 use fresh transient group meshes and are valid. Temporary preview roots and capture objects have been removed. Production camera, light and materials were not edited.
