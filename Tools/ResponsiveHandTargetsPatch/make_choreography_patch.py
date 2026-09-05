from pathlib import Path
import difflib


project = Path(__file__).resolve().parents[2]
relative = Path("Assets/Elemental/Presentation/Animation/EarthChoreographyDirector.cs")
source_path = project / relative
original = source_path.read_text(encoding="utf-8")
modified = original

old_using = "using Elemental.Simulation.Characters;\nusing Unity.Profiling;"
new_using = "using Elemental.Simulation.Characters;\nusing Unity.Mathematics;\nusing Unity.Profiling;"
if modified.count(old_using) != 1:
    raise RuntimeError("Choreography using seam changed")
modified = modified.replace(old_using, new_using, 1)

old_target = """            EarthChoreographyPoseOffset target = EarthChoreographyVisualSolver.Solve(
                CurrentRequest.Technique,
                CurrentRequest.Phase,
                CurrentSample.Dialect,
                CurrentRequest.Effort01,
                CurrentSample.StanceWidth01,
                CurrentRequest.Grounding01,
                CurrentRequest.Precision01,
                CurrentRequest.LeftDominant);
            float responseSeconds = CurrentRequest.IsActive ? 0.065f : 0.10f;"""
new_target = """            EarthChoreographyPoseOffset target = EarthChoreographyVisualSolver.Solve(
                CurrentRequest.Technique,
                CurrentRequest.Phase,
                CurrentSample.Dialect,
                CurrentRequest.Effort01,
                CurrentSample.StanceWidth01,
                CurrentRequest.Grounding01,
                CurrentRequest.Precision01,
                CurrentRequest.LeftDominant);
            if (presentation != null && presentation.HasResponsiveSustainedAim)
            {
                // Chest already belongs to this late choreography pass. Consume
                // the hand solver's body-local aim here rather than adding another
                // transform writer or rotating the gameplay root/head.
                float3 chest = target.ChestEuler;
                chest.y = math.clamp(
                    chest.y + EarthResponsiveHandTargetSolver.ResolveTorsoYawDegrees(
                        presentation.ResponsiveSustainedLocalAim,
                        presentation.ResponsiveSustainedAimWeight),
                    -EarthChoreographyVisualSolver.MaximumChestDegrees,
                    EarthChoreographyVisualSolver.MaximumChestDegrees);
                target = new EarthChoreographyPoseOffset(
                    chest,
                    target.HeadEuler,
                    target.LeftShoulderEuler,
                    target.RightShoulderEuler);
            }
            float responseSeconds = CurrentRequest.IsActive ? 0.065f : 0.10f;"""
if modified.count(old_target) != 1:
    raise RuntimeError("Choreography target seam changed")
modified = modified.replace(old_target, new_target, 1)

from_name = f"a/{relative.as_posix()}"
to_name = f"b/{relative.as_posix()}"
lines = [f"diff --git {from_name} {to_name}\n"]
lines.extend(difflib.unified_diff(
    original.splitlines(keepends=True),
    modified.splitlines(keepends=True),
    fromfile=from_name,
    tofile=to_name,
    n=3,
))
Path(__file__).with_name("EarthChoreographyDirector.integration.patch").write_text(
    "".join(lines), encoding="utf-8", newline="\n")
