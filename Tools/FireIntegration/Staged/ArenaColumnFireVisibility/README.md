# Decorative flame visibility

Observed night image shows red dim flames. Existing CPU shader is already unlit; the likely structural cause is rendering before fullscreen atmosphere, which sees background scene depth at flame pixels and veils them as distant scenery. Decorative-only shader retains exact existing vertex/flame HLSL but uses the existing post-fog geometry renderer list. Mandatory sampled opaque-depth rejection prevents flames showing through stones (that pass has no depth attachment). No additional pass/RT and no change to gameplay FireCpuMesh shader/material.

Installer clones a separate Fire_ColumnDecor material: warm HDR gold core, amber body, higher opacity;70 births/s, widths.32–.60m, same256capacity/one substep. Four-light cap unchanged; day.3/night14 intensity, range12m, warmer amber light instead of saturated red. Existing solar Night01 drives this. Approx245–441live across7fires. No global night ambient/light settings changed.

Import then rerun Install Arena Column Fires. Require same-night before/after image; actual shader compile, occlusion behind stones, >=1visiblewarmstone light patch, enabledlights<=4, and performance measurement. C#4assemblies compile0. No Unity validation yet. Preparation script predates final mandatory manual-depth fix; do not rerun prepare.py over staged final sources.
