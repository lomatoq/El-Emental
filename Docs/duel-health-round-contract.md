# Duel health and round contract

Implementation checkpoint: 2026-09-06, working tree based on main. This document
describes the new contract, not completed visual/gameplay acceptance.

`EarthDuelMatchState` is the pure owner of two health values, opponent scores,
readiness and a 300-second gameplay clock. Health defaults to 100, does not
regenerate, and returns to full on respawn. Damage emits a death transition once
per life; simultaneous deaths award both opponents one point. Expired rounds
reject damage and preserve the final scores. Restart resets the actors' match
state without rebuilding terrain.

`EarthMvpDuelController` retains the existing 3.5-second physical respawn adapter.
Its public HUD seam is `PlayerHealth`, `BotHealth`, `MaximumHealth`, `PlayerScore`,
`BotScore`, `RoundRemainingSeconds`, `IsRoundOver`, `CombatAllowed`,
`PlayerTransform`, `BotTransform`, `StateChanged` and `RoundRestarted`.
`SetRoundReady(bool)` is the explicit loading/entrance gate; the fixed gameplay
clock cannot run before readiness. `RestartRound()` retains readiness and resets
actors and score. Runtime test fixtures using `Configure` start ready.

`EarthCharacterImpactTarget` applies damage only after its existing support and
duplicate-hit filters accept an impact. Nonlethal physical knockout severity now
means recoverable knockdown when bound to a duel. HP death performs the single
ragdoll handoff through the duel owner. Fatal fall verdicts deliberately remain
lethal, routed through that same health/death authority. Unbound presentation
fixtures can still perform recoverable physical responses.

The duel's serialized damage table defaults to base 8 for ordinary stones and bot
shots, base 10 for armor pieces, 20 for surf/crest, and 16 for pillar waves. Ordinary
physics uses the smaller of closing and reaction speed, subtracts a 2 m/s safety
threshold, multiplies by 4 and caps damage at 25. Supported floor contacts remain
filtered before this calculation. Typed projectiles accept an optional explicit
damage override, which also survives collision callback ordering and clears when
the projectile returns to its pool.

Stone normalization follow-up (2026-09-06, uncommitted on main `1235579`):
`ApplyStoneImpact` is the common physical stone adapter for thrown fragments,
quick shots, bot stones, armor, moving decor boulders and debris. The pure impulse
is reduced mass `m*M/(m+M)` times incoming normal contact speed times 0.3.
Contacts at or below 0.75 m/s are harmless; the speed input is capped at 60 m/s.
This makes severity increase with mass and speed without an unbounded boulder
impulse. Stone reaction has no source-name knockout floor. Root movement uses
0.65 of the reaction velocity with a 3 m/s cap and existing launch limits.
Stone HP damage (including combo base overrides) scales by reaction velocity / 2,
capped at 3 times the base. Hits below the 0.65 m/s reaction threshold neither
damage nor add a ragdoll-cluster hit. HP remains the sole combat death authority.

Collision adapters use incoming contact velocity, avoiding post-solve stopped
Rigidbody velocity. Dormant pooled typed-projectile components do not override
ordinary loose-stone classification. Moving decor rocks and debris carry their
stable stone identity instead of the generic-physics knockdown cap.
Focused new pure and PlayMode tests are added in `EarthCharacterImpactSolverTests`
and `EarthDuelHealthPlayTests`; fresh execution evidence is pending integration.

Verification: `TestResults/DuelAcceptancePlay5.xml` passes all nine focused
production checks, including accepted-hit duplicate filtering, single death
scoring, readiness control restoration, and the saved HUD's health, mana, score,
round completion and restart. The seven pure match checks also passed the earlier
Unity EditMode run in `TestResults/DuelFeaturesEdit.xml`. The steady match clock
has the `Elemental.Duel.MatchTick` profiler marker; no match-only timing claim is
made from pure tests.

Integration guard follow-up: readiness snapshots survive HUD-before-duel Awake
ordering. MagicInputController binds the duel explicitly and checks match/life
permission at routed input, buffered fixed-tick fire, and public mutating command
entrypoints. This remains authoritative if physical recovery re-enables the
component. A focused PlayMode regression covers readiness capture before Awake;
it passes in the production Play5 run.
