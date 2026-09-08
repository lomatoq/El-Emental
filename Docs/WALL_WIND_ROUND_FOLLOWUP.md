# Wall, surface wind and round restoration follow-up

**Latest user correction (02:11 UTC): arena restoration is once per whole game/session, not after each life. Damage/debris must persist across ordinary KO/respawn; restore on full-match victory, return to Main, restart/new game. Earlier per-life reset acceptance below is historical and does not satisfy this corrected requirement. Corrected lifecycle Play2/2 passed02:17:13 UTC.**

September 7, working tree on `1235579`, no push authorized. User scope: matching interior/exterior wall surface, no daylight gaps between adjacent beveled cells, true depth-varying 3D fracture, rigid upward emergence with millimetre tremor and foundation debris, continuous surface wind dust concentrated around settled stones, RMB launch of a wall cell at quick-shot speed, restored arena between whole games (latest user correction supersedes the earlier per-life interpretation), and real online verification.

## Wall surface and junctions

The shared Rumble shader previously replaced green-classified cut faces with a different fracture palette even when both renderer slots referenced the same material. `Match Fracture to Exterior` removes that palette/depth override and uses the exterior face-tone class. The saved sandstone and arena materials enable it; the separate arena interior asset copies the exterior's full properties. The authoring generator preserves this policy.

**Elemental > VFX > Match Wall Fractures to Exterior** reapplies this focused material change without regenerating the scene. Existing exterior colours are retained.

Wide chamfers meeting at a junction expose holes through an otherwise exact partition. Each rendered cell now includes a shallow unchamfered backing using one shared wall-space depth contraction. Adjacent backings remain a partition. Backing is cosmetic and can extend beyond the individual oblique cell hull; physical collider, volume, mass and bonds remain unchanged. The same prepared render geometry is used intact and detached. Renderers use one sandstone slot for the merged submesh, avoiding an extra draw of the same mesh.

**Wall Surface Edit 7/7 passed at 01:27:09 UTC**, including a ray through the junction of four wide chamfers, metric bevel width, conservation of the original physical boundary and deterministic variation. This precedes the later 3D bake; final production visual evidence is recorded below when available.

## Rise and ambient dust

The former rise curve added a fixed lateral offset and centimetre-scale pulses then zeroed them on the last frame. Rise now uses only local up, rigid scale/rotation, and a 2.5 mm maximum vertical tremor fading at both endpoints. Existing bounded material-feedback particles emit along the foundation. **Wall Rise Continuity Play 1/1 passed at 01:28:01 UTC**, including tilted up, entire rise and five settled frames, foundation-plane emission and actual smoke/chip counts. Metrics: `BuildReports/WallRise/continuity.json`.

Continuous wind dust is configured in **Elemental > VFX > Edit Surface Wind Dust**. The saved default budget is 192 live particles, 12/s over the ground plus 36/s near settled stones, 1.45 m/s tangent drift. It follows real support normals; cosmetic particles add no rigidbodies. **Surface Wind Dust Edit 2/2 passed at 01:28:47 UTC**. Production visual Play **1/1 passed at 01:55:46 UTC**: 110 live particles, 94 ground / 264 stone-wake emissions, 1.169 m mean travel over 0.8 s; CPU marker mean 0.126 ms / peak 0.476 ms. Same-frame particle on/off comparison changes 7,754 pixels. No GPU claim. Production intact/plucked wall images are `BuildReports/SurfaceWindDust/05-wall-sealed-intact.png` and `06-wall-interior-and-volume.png`; foreground stones and extraction dust partly occlude the detached piece. Final opacity is 0.32.

## Depth, launch and restored rounds — updated acceptance

**Wall Depth Partition Edit 2/2 passed at 01:38:50 UTC.** Production baking now distributes close pairs through the depth of the thin wall. Tests require a closed, volume-conserving 40-cell partition, at least eight cells spanning less than 85% of total thickness, different actual depth spans and oblique internal depth faces. Existing default solver outputs remain unchanged. The existing `EarthWallFracture.asset` was rebaked at 01:41:49 UTC (source revision 2); the arena was not regenerated.

**Wall RMB Remote Input Play 1/1 passed at 01:51:33 UTC.** Force press/release passes through the actual input adapter/router, detaches one cell, uses the shared QuickStone speed policy, keeps the wall root fixed and produces real travel. Initial validation found the front cell colliding with the deeper packed cells. Launch now temporarily suppresses only its own wall-sibling pairs until it leaves the source wall bounds (maximum 0.5 s); world and character collisions remain enabled. The test also checks that those pairs are restored. This is the authoritative remote-input branch, not proof of two-player NGO RMB delivery.

**Arena Round Reset Play 2/2 passed at 01:50:15 UTC.** Two consecutive lives restore canonical terrain, authored decor integrity and parent/pose, retire old matter generations, clear pending extraction and respawn both fighters with full health while retaining score. Pause/resume cannot bypass the reset gate. The second test rejects a future world ACK and prevents an old ACK completing the current checkpoint. Runtime restoration failure holds the gate with a recorded error instead of resuming a partial arena. The first fixture attempt had an insufficient frame-count wait; its replacement uses a bounded real-time deadline and retains all assertions and gameplay timings.

Online reset uses the existing world checkpoint with a monotonic revision and reliable restored poses; gameplay packet protocol is now 3, explicitly incompatible with older builds. Fresh two-player acceptance is still required below.

**Online Edit 35/35 passed at 01:52:34 UTC** after protocol/reset integration. EarthCoreSlice was explicitly reloaded, online catalog refreshed and the existing scene saved after QA. Fresh build and two-player validation follow; earlier Relay evidence predates protocol 3.

## Paired mouse correction

A second mouse button pressed after the 80 ms chord window was refused because the single-button router already owned an active session. The router now permits a handoff only while that single-button gesture has not captured a body, started a vector field/extraction or begun drawing. Both held buttons can then start the chord when only the second has a fresh press edge. Existing held objects/committed casts retain ownership. **Paired Mouse Gesture Edit 12/12 passed02:12:08 UTC; Wall RMB Remote Input Play3/3 passed02:12:48 UTC.** Runtime cases verify the late second-button and simultaneous ground gestures create five real columns and advance their rise; standalone RMB still launches a cell with actual travel.

## Final match-boundary restoration

**Arena Round Reset Play2/2 passed02:17:13 UTC, superseding the earlier per-life implementation.** Ordinary KO uses the original respawn and retains terrain damage, debris, score and world revision. The fixture checks actual persistent damage after respawn, then three restoration boundaries: Main/leave, full match timer completion and new game. Restarted fighters spawn after collider restoration and online ACK; Main updates Play availability as restoration completes. Failure retains the closed gate and error. Runtime snapshot is reused; no scene regeneration. The explicit development probe now checks KO/respawn without an arena reset and restoration on menu return. Two-player evidence remains separate.

## Saved build and GPU interruption

Existing EarthCoreSlice saved explicitly after final corrected lifecycle checks. Development build succeeded at2026-09-07 02:21:20 UTC: **0 errors /195 warnings**,183.43s, report `BuildReports/OnlineDevelopmentSavedArena.json`. Warning count is not described as clean; observed warnings include existing inference compute shader variants and legacy EarthTriplanar FamilyHeight initialization. No claim that all195 were individually triaged.

Fresh protocol3 stone-combat attempt `BuildReports/OnlineProbe/Run-20260907T022200` failed **before probe JSON/network startup** on both players with D3D11 device removed0x887A0005 and resolution setup failure. It provides no gameplay/online acceptance. Verified owned PIDs28652/30952 were closed. User reported Unity crash and is restarting it; further game launches were stopped. Scene and completed build are retained. Next step after editor recovery: controlled sequential player launch and real Host/Join/combat/leave validation; no push.
