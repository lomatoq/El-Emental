# Turn-in-place step completion repair — accepted

Final production proof passed 1/1 at **2026-09-06 18:50:13 UTC**, covering short left/right taps, both sustained 180° turns and all-frame floor clearance through Capture/exit. Five pure tests passed. See `ACCEPTANCE.md` for exact margins, visual findings, the limited foreground occlusion in the left sustained view and separate slope work still pending. Final video: `Proof/TurnSteps.mp4` (74 actual frames, about 7.91 s).

No live Assets, scene YAML, controller or FBX importer was changed directly by this lane. Root applied the narrow `TurnSteps.patch` plus the three new files under `Assets/` and owns all Unity execution/saving/captures. Staged sources are retained for traceability; do not reapply the initial patch to already-modified live files.

Confirmed cause: the existing controller already contains the actual `Left Turn` clip and its mirrored right counterpart. Turn In Place is a one-dimensional blend with idle at zero. HumanoidCharacterPresentation drove both this blend's amplitude and immediate exit with the decaying yaw filter. A short released command therefore diluted the leg motion and returned to locomotion before the one-second step reached its planted finish. The earlier production test proved only state ownership and zero EAMM override. It did not prove completion, foot lift or foot release.

The new pure `EarthTurnStepSequence` commits a full directional step and waits for the actual sampled Animator turn clock to cross its cycle boundary. Held turning repeats; direction changes apply at the next footstep boundary. Real consistent body yaw can request a step without translation/input; alternating support corrections cannot. Translation, lost support, surf/pillar, protected actions and impact stun cancel immediately. No motor/yaw/translation/root motion changes. No locomotion clips, controller thresholds, avatars, importer assignments, EAMM leg mapping or slope solver changes.

HumanoidCharacterPresentation exposes AuthoredTurnStepActive/Direction for evidence, selects the existing turn state while that step is active, and sends full signed turn weight to the existing tree and foot-contact intent. Reset clears the sequence. A blend into the turn uses the next state's actual clock; an outgoing blend cannot masquerade as a newly started turn cycle.

Initial pre-application validation: `Compile-Pending.ps1` compiled Simulation, Presentation, EditMode tests and PlayMode tests against actual Unity editor references, zero errors or C# warnings. `TurnSteps.patch` passed `git apply --check` before root applied it. A direct invocation of the compiled pure policy proved a released tap stays active at 90% and finishes at the next cycle boundary. Root's subsequent Unity tests and visual proof are accepted above; the original compile script is a pre-application utility, not a required current rerun.

Freshness note: a subsequent rerun after the independent live `EarthBodyTargetFilter.cs`/test addition compiled Simulation and Presentation, then stopped on the new unrelated EditMode test because Library/ScriptAssemblies/Elemental.Runtime.dll predates that new class. Root's next Unity compile resolves the runtime reference; this does not supersede the earlier complete pending compile or count as a turn test failure.

Required filters:
- EditMode: `Elemental.Tests.EditMode.EarthTurnStepSequenceTests` (five cases).
- PlayMode: `Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionTurnStepsCompleteShortTapsAndSustainedHalfTurns` (one production sequence: short left/right taps, sustained actual left/right 180°, release).
- Existing regression: `ProductionIdleTurnsAndStopsKeepFinalPoseAndAuthoredTurnOwnership` and locomotion cadence tests after relevant changes only.

The new production proof uses the actual player, controller and final rendered skeleton. It requires turn clip weight >.85, progression beyond .8 normalized time, final left/right vertical separation span >18mm, at least one released foot contact, real 180° yaw without translation, and return to idle. It captures framed full-body PNGs at roughly 10 Hz to `BuildReports/TurnInPlaceRepair/Proof/frame-%04d.png` and writes `TurnStepFrames.json` even on assertion failure. Frame records include scenario, actual clock, clip weight, final foot heights/contact weights and EAMM weight. Root must inspect/encode those frames; angle-only passing is insufficient.

Possible next diagnostic, not an implemented assumption: the right side mirrors Left Turn bones while authored custom left/right contact curves may need explicit side swapping. The proof records both sides to expose this if it occurs; do not change locomotion/slope metadata without that evidence.
