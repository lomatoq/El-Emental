# ReferenceHudExact — staged candidate

Six source files: two exact existing before/after baselines, four new files. No actual Assets/Unity edits by this agent. Parent imports; no scene regeneration required.

- Own final StoneReferenceHudPresentation.cs, starting from ReferenceSpriteFidelity/merge-inputs version. Includes that agent's result-button implementation in full; do not import another copy of this file.
- Add optional `ElementalStoneReferenceProfile.exactHud`, new StoneHudExactProfile, StoneExactHudDecoration, idempotent StoneHudExactInstaller and two Edit tests.
- Installer creates new StoneHudExactProfile.asset / StoneHudExactLayout.asset and points the existing reference profile at them. It never modifies the original StoneReferenceHudLayout asset. Run AFTER Install Stone Artwork. Existing reference mode without exactHud keeps its old wheel/layout code.
- Shared skin dependency: ReferenceSpriteFidelity adds referenceNormal/referenceCard/referenceSelectedInsets and native element glyphs. validate.py overlays its staged skin source during compilation. It is not owned by this lane.
- No camera, lights, world, selection authority, network, damage or input-map changes. Existing live round values, health/mana, selected enum, globe navigation and callbacks remain their source of truth.

Validation: all four real Unity Roslyn assemblies PASS (Presentation, Authoring.Editor, Tests.EditMode, Tests.PlayMode). Edit tests are compiled, not run. Actual Unity screenshot/interaction acceptance remains REQUIRED; see SPEC.md.

Use `python Tools/FireIntegration/Staged/ReferenceHudExact/validate.py` for offline compile. `mapping.json` records exact actual before and staged after hashes. The source is intentionally optional so original layout data remains available for rollback/review.
