# Production arena restoration overlay

Status: source frozen; **not integrated**. Coordinator owns Assets and Unity. This staging directory contains exact `before/` sources, `after/` replacements/new C# files, SHA256 `baseline.json`, unified patch, verification and offline compile reports. No scene, physics tuning, mesh, UI layout or material is changed.

## Defect and contract

The production UI regression observed an actual registration failure in `EarthArenaRoundSnapshot.Restore`, not an expected warning. Its old generic message cannot identify the failing record. Three source defects are addressed: global registry lookup across additive scenes; replacing a lazily initialized registry in Awake; and storing every readable pooled record in the authored snapshot, including consumed/dormant/transient representations that ordinary registration cannot resurrect.

The owning scene resolves its own registry, and registration rejects a foreign-scene identity. Awake retains an already initialized registry. Snapshot records must belong to captured authored structure/decor nodes and represent live non-dormant matter. Whole-match replacement still retires all old local lifetimes and clears transient pools; consumed shells/dust are not respawned as authored rocks. This is not a general save/load of an arbitrary mid-match dust ledger.

Before mutation, restore verifies ownership. Retired-handle release failures are checked. Re-registration failures include hierarchy path, owning scene/kernel scene, baseline/current identity and phase, representation, volume, mass, occupancy/capacity and failure code captured before diagnostics can overwrite it. No failure is ignored. If a different production failure remains, the detailed report must be used to fix it; do not add LogAssert.Expect for the error.

## Verification

Offline Roslyn compiled Simulation, Runtime, EditMode and PlayMode with exit0. Seven existing Runtime deprecation warnings are outside this patch; no new warning was emitted by staged sources. Actual files still match the baseline hashes at handoff. **Unity execution is pending**, so compilation is not production acceptance.

After integration and completed import:

- Elemental > QA > Production Arena Restore Edit: six policy cases.
- Elemental > QA > Production Arena Restore Play: actual saved EarthCoreSlice loaded alongside another live world, two EndMatch/Main/BeginBot cycles, real arena-column pluck and terrain edit, ordinary KO/respawn retaining destruction, exact restored terrain bytes and intact column, full health/zero scores for a new game, foreign registry/identity/mass/phase preserved. Also tests registration before an inactive kernel host awakens.
- Existing Stone Skin Play / FeelFollowupUiPlayTests: real Won/Lost/Draw labels after ordinary respawn; no whole-match restart between lives.

Production proof writes BuildReports/ProductionArenaRestore/evidence.json and damaged/new-game PNGs. Scene snapshots are not saved by tests. The production damage uses real gameplay APIs to isolate lifecycle; it is not proof of mouse routing, thrown hits or online combat.

Coordinator should publish actual test results in the project tracker, retain failure logs and complete fresh protocol3 player verification after this gate passes. No push authorized.

## Review additions

Armor pool roots and the surf board/cut chips/released stones now move to their controller scene immediately on creation, before components. They remain unparented world bodies; motion and physics parameters are unchanged. This closes the known additive prewarming routes exposed by strict scene ownership.

Production proof explicitly activates armor and fires one plate to exercise deferred canonical registration; merely checking prepared visuals would miss the failing path. It checks the plate registry scene and then uses normal Disabled cleanup to avoid a projectile interfering with lifecycle assertions. Surf prewarmed board ownership is also checked.

To make dropping all baseline records fail the test, a SceneLoaded hook (after authored Awake, before frontend Start/baseline capture) binds the existing saved **Light Push Boulder** through EarthMatterRuntimeBridge using its actual collider volume and Rigidbody mass, or retains its existing valid record. No substitute stone or fake physics object is created. Each reset checks live registration, new lifetime, same mass/volume/material/shape/source provenance, valid collider/body representation, and successful real vector-field grip/cancel after starting the new game. The report includes source name, preserved mass/volume, new IDs and registered armor count.

Latest manifest covers five existing and four new C# files. Compilation rerun passed all four assemblies; Unity execution remains pending.
