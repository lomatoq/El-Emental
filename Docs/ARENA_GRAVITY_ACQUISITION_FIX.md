# Arena gravity acquisition follow-up — 2026-09-04

Working tree: dirty `d2174eded114dd022e4a9c442abadda7a0e44555`.
This follows the user's report that the previous loose-stone fix was incomplete.

## Reproduced cause

In the live shipping scene, rays towards released `FR_arena_gate_P012` and
`FR_arena_wall_east_P001` selected `Arena_FloorBase_INTACT` instead. The resolver
already correctly excludes Gravity/Repair from the protected floor, but
`MagicInputController.TryFindGravityFocus` requested **Surface** as an alternative
capability. That admitted the floor anyway. `MagicExecutor.TryBeginGravityWell`
then returned true even though the grip contained zero stones.

Previous tests passed the stone collider directly to the executor, bypassing this
screen-point selection boundary. The new regression uses shipping floor geometry,
a genuinely plucked shipping column cell, and the scene's configured input adapter.
It moves that picking fixture away from combat and confirms both floor and stone
intersect the ray before exercising the public screen-point entry point.

Baseline after correcting the test's input setup: **5/7 PlayMode passed**, UTC
`13:19:23`; the two failures are empty-session acceptance and the floor stealing
the screen-point grip (expected one stone, got zero).
Reports preserved in `BuildReports/ArenaGravity/Before.json` and `Before.xml`.

## Change

- MMB focus requires Gravity or Repair capability. Surface/Draw alone cannot
  intercept this operation. Construction-surface queries keep their own filters.
- Pure `EarthGravityGripSolver.CanBeginSession` permits a captured physical target
  or a manipulable structure waiting for a circle gesture. Empty non-target
  sessions are cancelled and report failure, including unsupported terrain hits.
- Input feedback distinguishes a captured stone from structure-circle control.
- Added profiler marker `Elemental.Input.GravityAcquire` around complete
  screen-point acquisition. The runtime regression measures the first full press,
  including first-use binding and acquisition feedback, separately from idle loops.

Pure admission cases: `BuildReports/GravityAcquisitionEdit.json`, **4/4 passed**,
UTC `13:21:34`. Runtime acceptance additionally checks lift and three repeated
release/reacquire cycles. Saved wave profile still matches
`BuildReports/ArenaGravity/UserWaveProfile.asset`, SHA256
`CDF2D3C2B546204AE088EAAFD1E4156B51B2DAC4947BCF0FAF436FA30B82BE1F`.

## Wider exploratory run

`ArenaGravity/WiderExploration.json` (UTC 13:22:52): 6/8 passed. The new floor/grab
test passed all four presses and lift (first press 1.8616 ms / 0 managed bytes in
Editor). The older all-in-one BrokenCrown test failed at its legacy requirement
that Game-camera realtime shadows be disabled, before reaching gravity checks.
That test has no try/finally unload at this assertion, leaving the production
scene loaded; the following independent rest fixture then had 1/3 sleeping stones.
The old shadow assertion and the user's saved shadow settings are unchanged.
The focused run checks the relevant intact-structure disassemble/repair contract
inside the new test's guaranteed-cleanup scene fixture instead.

## Final focused result

`BuildReports/LooseStonePlay.json`, UTC **13:25:48**: **7/7 passed**, including
the original five regressions. Shipping floor/cell screen-point acquisition lifts
the selected piece and survives three further release/reacquire cycles. An intact
column still starts its zero-capture circle session, releases at least two cells
at disassembly phase .55 and fully reassembles at repair phase 1. The independent
rest fixture again reports 3/3 sleeping and zero drift after the scene is cleaned up.
First complete screen-point press measured **2.4525 ms / 0 managed bytes** in
Editor; this cold press measurement is not a frame budget or player-build claim.

Together with **4/4 EditMode**, this closes the reproduced floor-interception and
empty-success defects. It is not a claim that every possible pointing/occlusion
case or the unrelated wider shadow test has passed.

## Raw middle-button clarification (UTC 13:31)

The user clarified that the remaining concern is the middle-button gravity grip.
Added `MiddleMousePressHoldReleaseAndRepressReachArenaGravityGrip`: queues actual
Input System MouseState events for the Middle button into the shipping PlayerInput,
then checks EarthInputAdapter, action-router ownership, active grip, pointer-driven
lift, release and three more press/release cycles. It never calls the acquisition
helper directly in this mode. `LooseStonePlay.json` **8/8 passed**, UTC 13:31:42.
This clean-start scenario did not reproduce the remaining failure. The live
capture below identifies the missing prerequisite: previously wearing armor.

## Live armor-to-gravity handoff failure (UTC 13:49)

`ArenaGravity/ArmorOwnershipLiveFailure.txt` preserves the user's failed presses:
the adapter held MMB and the router owned Gravity, but the executor had no active
grip. Releasing plain MMB repeatedly emitted the armor-release status even though
armor was already inactive. Editor-polled raw mouse coordinates/state are not a
runtime pointer trace and were not used to change cursor handling.

`UpdateArmorInput` clears `_armorOwnsField` when armor ends and returns true to
consume the terminal input frame. Its caller assigned that return value back to
the persistent ownership field, undoing the release. Subsequent plain MMB presses
therefore ran the inactive armor handler instead of gravity acquisition.

The caller now keeps frame consumption in a local `armorConsumesFrame` variable.
Persistent ownership changes only with armor lifecycle; an inactive controller
also clears stale ownership. Release still consumes its own frame, so it cannot
start a gravity action with the same release input. This applies to normal release,
volley, final-plate firing and overscroll termination at their shared call site.

Added actual paired Keyboard/Mouse input coverage for Shift+MMB armor, release,
then plain MMB capture of a shipping arena cell, lift and repeated reacquisition.
Before the fix, this test reached the plain MMB press with routing correct and
failed because the grip was inactive. Baseline `ArenaGravity/ArmorHandoffBefore`
JSON/XML: **7/9 passed**, UTC 13:49:31. A second test also failed on a later
reacquisition count; that baseline failure is preserved without attributing it
to the armor latch. Pure router handoff plus admission cases: **5/5 passed**, UTC
13:50:56, `GravityAcquisitionEdit.json`.

## Restored held area field (user clarification)

The user clarified that MMB has always meant a held field collecting a group of
stones. The earlier single-target/no-overlap assumption and its test encoded the
wrong interaction. MMB now captures nearby loose physical Earth targets within
the existing gravity-profile radius (7.5 m by default, cap 48), then samples the
held field every .1 seconds for additional loose stones. Capture pauses during
repair or throw charging; the existing captured group remains owned until release.
Intact structures and supported/kinematic cells are not plucked by area overlap.
Directly aimed targets retain priority; nearest candidates then stable IDs order
the bounded group. One fixed 1024-collider query buffer and 48 candidate slots are
reused. The physics buffer is a bounded query, not an unbounded world search.

If the ray misses a gravity/repair target, a surface hit can anchor the field in a
gap between stones. An empty surface still cannot claim a successful capture.
The former `MmbPressLocksOneExplicitTargetAndNeverSweepsNearbyBodies` test is
replaced by nearby-group and new-arrival coverage. The actual keyboard/mouse
armor-handoff scenario now uses three released arena cells.

The first armor fix run was **8/9 passed**, UTC 13:52:45, with the armor handoff
passing. `ArmorHandoffFirstFix` preserves it. The remote picking fixture put a
large floor between camera and cell, then pulled the cell across it into the
shipping hold-distance range. Its collision could destroy the selected cell,
which is unrelated to input acquisition. The fixture now ignores only that
artificial obstacle/body contact (and contacts within the three-cell input fixture)
while retaining the floor collider for picking. Physical lift remains simulated.
Synthetic Keyboard/Mouse pairing also disables automatic device switching so
live editor input cannot change this test's controls.

The user further specified **no artificial gaps** inside the group. Runtime field
forces now converge on the common center rather than camera-side orbit slots.
Normal rigidbody collisions remain enabled and determine contact packing; stones
are not teleported or made mutually noncolliding in gameplay. The area-field
regression checks that each of three ordinary physical bodies touches a neighbour
within 4 cm and that contact penetration stays below 4 cm after settling.
The existing profile assets (including orbit values) are preserved; camera-side
orbit offsets no longer determine held-field separation.

`AreaFieldContractTransition.json/xml` (UTC 14:10:25) preserved the first restored
field run: **7/9 passed**. The three-cell armor handoff and the new arrival/contact
packing test passed; the two failures were obsolete `captured == 1` assertions in
the picking/rest tests (actual 2 and 3). The rest fixture now requires its complete
three-stone group; the shipping picking fixture allows additional nearby targets
while still requiring the aimed cell to lift and survive repeated releases.
Area-field begin measured **0.0640 ms / 0 managed bytes** in that Editor fixture.
This is a scoped acquisition measurement, not a player-build frame-budget claim.

## Final restored-field verification

Dirty `d2174ed`, Unity 6000.5.7f1, 2026-09-04:

- `GravityAcquisitionEdit.json`: **9/9 passed**, UTC 14:08:23.
- `LooseStonePlay.json/xml`: **9/9 passed**, UTC 14:14:22 (53.7993 s).
- Actual Shift+MMB armor → release → MMB field captures three shipping arena cells,
  lifts them, releases and reacquires on three further presses.
- Nearby stones and a later arrival are collected; three ordinary physical bodies
  settle into contact without disabled collisions; release empties the session.
- Protected-floor picking, intact-column disassemble/repair, sleeping stones,
  repeated group wake/lift and support removal all pass the focused suite.
- User wave-profile SHA256 still matches
  `CDF2D3C2B546204AE088EAAFD1E4156B51B2DAC4947BCF0FAF436FA30B82BE1F`.

These results supersede the earlier single-target MMB assumptions. Wider project
acceptance is unchanged; the pre-existing authored EarthArmorPiece required-
component warnings and legacy shadow test limitation above remain separate.
