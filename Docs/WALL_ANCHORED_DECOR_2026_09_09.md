# Anchored decor plough and rigid damaged walls

Baseline main 4b592086. Earlier evidence covered an intact shell on a flat floor and free dynamic cubes. The real blocker is EarthDestructibleDecorRock, often initially anchored. It was classified as an immovable heavy obstacle, and fractured wall domains had buried full contact hulls and flexible multi-body constraint chains.

Small decor is processed before generic heavy/static blocking. Qualifying decor weighs less than 65% of the wall and meets a 1.5 m/s forward contact threshold. Every contact normal is considered. Its own production damage/shatter method runs; only successful detachment/shatter permits the plough continuation. One nearby wall domain disconnects (two at high charge) through canonical bond releases. The largest surviving component receives a finite mass-dependent remainder of the incoming momentum. Massive obstacles still block. Decor is excluded from slope-support rays.

Fractured contact hulls trim buried geometry to the existing root contact plane. Rendering keeps the original domain mesh. Entirely buried foundation cells have no exposed contact hull but retain canonical mass and bonds. Trimmed runtime meshes are restored/released for pooled reuse, repair and destruction.

The remaining moving component uses one existing domain Rigidbody as a physical carrier with aggregate mass. Other domains retain canonical identity/mass records; their individual physical bodies are parked while equivalent domain-shaped compound contact/render proxies follow the carrier. Thus no flexible physical link can bend the moving island. The consumed shell remains retired. Independent domain bodies, original masses, colliders and renderers restore before damage, new charge, magic acquisition, repair or pool reset. Contact proxies preserve per-domain physical-target handles.

Collective outward velocity is damped on the carrier. Ordinary plough travel preserves its orientation, matching the intact sliding shell; accepted incoming impacts above 6 m/s of mass-normalized impulse permit rigid tipping. Normal contacts remain enabled. No collision bypass or discarded canonical damage is used. Repeat push and cancellation must preserve this representation and physical mass. Cold carrier setup may allocate/cache domain proxies; steady sliding uses fixed arrays.

Evidence must measure the retained physical body separately from already detached chips: floor clearance of its actual contact shapes, surviving-domain distances, dynamic mass total, repeat travel, and its continued existence over the sampled interval. An absolute center-of-mass comparison of differently fractured islands is not a valid lift metric. Initial joint-only attempts still stretched and were rejected; the compound representation replaces them.
Fracture activation explicitly transfers the shell pose into each Rigidbody before contact-hull trimming and carrier assembly. Interpolated Transform state was not a reliable physical handoff. Pool hiding releases the carrier before reparenting or deactivation. Canonical domain poses are synchronized during sliding, and targeting resolves compound proxies back to their original domain before magic ownership.

## Verified evidence

- EditMode: 138/138, 2026-09-08T23:21:00Z, 2.03 s (`BuildReports/LandingRowEdit.json`).
- Broad wall PlayMode: 11/11, 2026-09-08T23:17:29Z, 152.14 s (`BuildReports/WallPushPowerPlay.json`). This includes emergence, buried foundation, shallow ramp, loose stones, heavy incoming impact, charge feedback, repeated fractured push and production keyboard input.
- Final anchored-decor/domain-mass PlayMode: 1/1, 2026-09-08T23:22:32Z, 28.34 s (`BuildReports/WallDecorRigidPlay.json`). The final domain-mass accessor correction was made after the broad run and is exercised by this stricter focused rerun. This is not a claim of a second final broad run.

The actual anchored decor component shatters, two local wall domains disconnect, and the retained wall travels 4.636 m. Maximum physical hull clearance is 5.283 cm; surviving-domain distance error is 0.00000489 m across 2507 samples, with a carrier present for 109 fixed steps. Dynamic aggregate-plus-chip mass and per-domain targeting mass equal the original wall mass. Cancellation restores the carrier, a second shove moves it again, and magic acquisition restores the chosen original body at the same position. Pool return completes without activation/reparent errors. Rigid-slide marker peak is 0.1442 ms in this editor fixture. Existing clear-floor tap/full-charge ranges remain 4.94/10.24 m.

Scope: the new regression uses production scene wiring and EarthDestructibleDecorRock on a controlled floor with a cube-shaped stone; it is not an exhaustive visual playtest of every arena rock shape, slope, or planet location. Profiler numbers are editor CPU observations, not a target-device frame budget. Wall work did not change material, lighting or audio assets. Subsequent user requests separately restore the live main-menu clock and center the pause-button pivot in the three existing HUD layouts.

Repair/input follow-up: **2/2**, 2026-09-08T23:26:39Z, 46.12 s (BuildReports/RepairPushPlay.json).
