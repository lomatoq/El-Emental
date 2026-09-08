# Grounding and turn follow-up — staged, unverified in Unity

2026-09-08. All implementation files are under `after/Assets/`; no live Assets changed by this agent. Run `stage.py` only before integrating: it recopies current live sources and applies this bounded patch.

## Actual owner path / findings

1. `PlanetMotor.UpdateGrounding` and `EarthFootContactController.ProbeFoot` share `CharacterSupportRuntimeAdapter.Classify` then `CharacterSupportAuthority.Select`. The adapter rejects **all** Rock/WallPiece/PlatformPiece targets before checking their actual physical state; the selector independently refuses both DynamicDebris and ReleasedFracture. Capsules still collide with these bodies. Even anchored decor was excluded. This is an explicit old policy, not missing foot-ray tuning.
2. `CharacterSupportAuthority.IsPreferred` gives arena proxies priority before distance. Thus an arena floor can win over a moving platform 40 cm above it. Previous tests cover only coincident seams and generic debris rejection, not an actual sleeping Earth rock bearing the actor.
3. Motor and each foot use eight unsorted NonAlloc hits; dense piles can fill those buffers before the actual top is returned.
4. `EarthTurnStepSequence.Step` requires >=0.5 degrees **per render sample** of uncommanded yaw. At 20 degrees/sec it triggers at 30 Hz, but never at 60/120 Hz. The bot/aim-driven yaw path has no `Move.x` to bypass this condition. `HumanoidCharacterPresentation` already computes actual yaw per frame; only the pure threshold used the wrong units.

## Staged implementation

- New `SettledMatter` kind admits anchored decor and sleeping released Earth rocks/fracture, preserving target stable ID/generation. Initial velocity alone never admits a thrown apex. Prior admitted support survives minor contact wakes <=0.15 m/s and <=0.35 rad/s; movement/grab phase or kinematic holding releases it. Foot adapters can use the motor's matching support as evidence after a physical contact wake; they retain independent support-local anchors.
- Unknown dynamic Rigidbody, armor and active/unsettled debris retain rejection. No teleport, root-height correction, collider disabling or animation reassignment in runtime.
- Select nearest eligible geometric band first (2 cm), then semantic proxy priority within that band. Separate nearest pass avoids order-dependent fuzzy comparisons. Existing legitimate-support hysteresis remains.
- Fixed query arrays increased from 8 to 32. This remains bounded; 32+ hits are still a known saturation limit, not a universal dense-world guarantee.
- Turn trigger compares angular speed using render delta time and the existing presentation fallback's 7 degrees/sec threshold, preserving the accumulated 5-degree trigger and authored cycle completion.

## Evidence ready for parent

- `SettledMatterSupportTests` — four Edit tests: sleeping vs apex, minor wake vs throw/grab/spin, actual rock above arena, actual platform above arena.
- Added three rate cases to `EarthTurnStepSequenceTests.SlowRealBotYawTriggersAtTheSameAngleAcrossRenderRates` at 30/60/120 Hz. Existing tests retained.
- `PlanetMotorPlayModeTests.MotorUsesActualAnchoredRockTopInsteadOfFloorBelow` exercises real PhysX capsule/motor and actual decor target. Its unrelated fracture component Start is disabled to avoid introducing an unconfigured debris-pool dependency in this isolated fixture; support classification remains the production adapter. This is **not** a production character/feet visual acceptance.
- `PlanetMotorPlayModeTests.ReleasedEarthRockSupportsAfterSleepAndKeepsIdentityAcrossContactWake` exercises actual decor physical target/sleep/wake/throw and stable generation through the runtime adapter.
- No Unity runs were made because the parent owns editor/test lifecycle. No measured profiler or visual improvement claimed. Parent still needs real released pile + both production characters standing/walking/turning on it, foot clearance/reach/support identity, and 30/60/120 turn sequences.

## Still needs an empirical turn probe

Saved `KayKitMage.controller` has the same Left Turn FBX on -1/+1, with the +1 child mirrored. `EarthFootContactController.ResolveClipContactMetadata` reads custom LeftFootContact/RightFootContact and LeftFootPhase/RightFootPhase without a mirror mapping. Unity documents humanoid pose mirroring, but that does not prove custom named parameter curves swap. No speculative swap is staged.

Parent probe: sample production controller at `Turn=-1` and `Turn=+1`, same normalized phases (e.g. .2,.4,.6,.8), with EAMM disabled and foot IK omitted **only for sampling raw pose**. Record actual left/right foot local positions/heights and all four custom parameters. If pose sides swap while parameters do not, reproduce the current mismatch through production solver before fixing metadata ownership. Also inspect transition frames because BlendTree mirroring may add phase offsets. Primary docs: https://docs.unity3d.com/6000.0/Documentation/Manual/BlendTree-AdditionalOptions.html and https://docs.unity3d.com/6000.0/Documentation/Manual/AnimationCurvesOnImportedClips.html .

User clip/controller/importer assignments are untouched. The typo 'рожново' cannot be confidently interpreted from code and was not treated as a new technical requirement.
