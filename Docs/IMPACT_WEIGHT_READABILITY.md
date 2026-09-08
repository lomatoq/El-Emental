# Stone impact weight and body recovery

The torso-spring presentation below is superseded by
[local physical response](LOCAL_PHYSICAL_HIT_RESPONSE.md), implemented September 6
with **24/24 Edit + 3/3 Play** and inspected production captures. The mass normalization and root shove
gain documented here remain in use.

## Request and scope

The requested result is a visibly larger bend when stones strike a character,
smooth viscous balance recovery, and approximately 40% more perceived stone
weight. Preserve the existing mass/speed normalization: small stones must remain
weaker than large stones, and this presentation change must not increase damage
or change the health authority for death.

## Implementation

- `Assets/Elemental/Simulation/Characters/EarthInertialBodyMotion.cs`: the impact
  spring frequency changes from 30 to 11 rad/s, damping ratio from 0.72 to 0.78,
  and the impact angular excursion ceiling from 9 to 18 degrees. Locomotion
  inertia retains its previous tuning. The existing bounded 240 Hz substeps,
  velocity cap, finite-input selection, and ragdoll reset remain in use.
- `Assets/Elemental/Simulation/Combat/EarthCharacterImpact.cs`: a shared weight
  transfer multiplier is 1.4 for LooseStone, ArmorProjectile, BotProjectile and
  StonePunch, and 1 for other sources. Normalized stone impulse, damage, response
  classification, and cluster accumulation are unchanged.
- `Assets/Elemental/Runtime/Characters/EarthCharacterImpactTarget.cs`: apply that
  gain to the requested physical shove after impact resolution and before the
  existing launch budget/limiter. At the launch ceiling the final shove can be
  less than 40% greater. `LastEffectiveVelocityChange` still reports the existing
  calibrated resolution value before this gain and the launch limiter.
- `Assets/Elemental/Presentation/Animation/HumanoidProceduralBodyResponse.cs`:
  apply the same gain to the angular kick before its existing cap. Distribute
  impact flexion 38% to Spine and 62% to Chest/UpperChest when a valid spine
  ancestor exists; otherwise retain the chest fallback. Hips and feet are not
  written. The production procedural spring is the single owner of flinch and
  stagger; full ragdoll retains ownership of knockdown/death.

No scene, animation asset, profile asset, or legacy localized ragdoll tuning was
changed for this request. No new allocation was added to the update path.

## Expected verification

EditMode filters:

- `Elemental.Tests.EditMode.EarthCharacterImpactSolverTests`: existing mass,
  speed, damage and classification coverage, plus source gain, mass ordering
  and retained launch bounds.
- `Elemental.Tests.EditMode.EarthProceduralAnimationAndImpactTests`: existing
  ownership, repeat-hit composition, single overshoot and convergence tests;
  new 30/60/120 FPS cases require a 130 degrees/s kick to peak at 4.5–5.1 degrees
  after 0.08–0.14 seconds, retain more than 0.8 degrees at 0.3 seconds, and settle
  below 0.002 degrees by two seconds.

PlayMode filters:

- `Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.ProductionStoneStaggerBendsAndRecoversWithCaptures`:
  dedicated production character verification of one medium stone stagger,
  peak above 2 degrees, residual recovery above 0.15 degrees at 0.3 seconds,
  and settling below 0.05 degrees at 1.2 seconds without full ragdoll. It saves
  `BuildReports/StoneStagger/{Before,Peak,Recovery,Settled}.png` and `Angles.csv`.
  This presentation fixture requests zero damage; the separate health tests
  exercise actual normalized damage and physical transfer.
- `Elemental.Tests.PlayMode.EarthDuelHealthPlayTests`: the real rigidbody stone
  route must produce 1.82 m/s from the previous 1.3 m/s shove while retaining
  normalized reaction severity 2 and the Stagger outcome. Existing pebble,
  boulder, duplicate-damage and health-authority cases remain included.
- `Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.SurfWaveAndBotProjectileUseTheSharedVisibleKnockoutPipeline`:
  the production rig must show a stagger peak above 1.5 degrees, use one
  procedural owner, recover from a living heavy-stone hit, and use full ragdoll
  for a health-depleting finishing hit.

The encounter fixture now waits for the actual production scene readiness gate
and combat readiness. Its finishing-hit branches explicitly arrange wounded
fighters (20 HP for surf, 16 HP for wave, 20 HP before three projectiles), since
physical severity alone no longer decides death. It starts scene unload in a
`finally` block; `UnityTearDown` waits for cleanup after a failed assertion as well
as after success. This prevents a failed encounter from contaminating subsequent
scene-based tests.

## Evidence status

Final coordinated evidence: **53/53 Edit + 6/6 Play passed**; Play completed
08:48:08 UTC. The dedicated production stagger peaks at 2.434 degrees around
0.10s, has a visible recovery tail and reaches zero by 1.2s. Mass ordering,
health authority, +40% physical shove and living heavy-hit get-up all pass.
The old get-up check now waits up to 0.3s for the catalog's actual contact
window rather than asserting contact in its initial unweighted segment.
The earlier intermediate failures below are retained as diagnosis history.

- Targeted source `git diff --check`: passed.
- Root reported all four health PlayMode cases passing in
  `BuildReports/ImpactSonarRepairPlay.xml`, including the new 40% shove case.
- That combined run's encounter failed before its first accepted impact because
  the old fixture attacked during production loading. Its leaked scene caused
  three following sonar setup failures. Readiness and cleanup fixes above are
  now implemented; the standalone encounter rerun is pending.
- The next run, `BuildReports/ImpactReadabilityPlay.xml`, passed all four health
  cases and reached authored get-up, where the old encounter expected immediate
  foot contact at a fixed 0.8 seconds after impact. The action catalog explicitly
  leaves IK off for the first 18% of the incoming recovery clip. The assertion
  now waits at most 0.3 seconds while recovery remains active for the authored
  contact phase, then retains the original contact requirement. No runtime
  contact invariant or support behavior was changed to address this timing.
- Root reported all 53 EditMode tests passing in
  `BuildReports/ImpactReadabilityEdit.xml` and reviewed the spring/substeps,
  spine ancestry check, and post-resolution gain with retained launch limiter.
  Dedicated production stagger/capture and final encounter reruns are pending;
  numerical coverage alone is not visual acceptance of the requested feel.

This change was implemented without agent-side Unity calls. The coordinating
root owns Unity compilation, test execution, and production visual verification.
