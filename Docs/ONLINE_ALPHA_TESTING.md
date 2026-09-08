# Online alpha integration and testing

## Latest protocol3 checkpoint — September7 02:24 UTC

Corrected match-boundary restoration Play2/2 passed02:17:13; per-life destruction persists. Fresh saved arena development build02:21:20 succeeded with0 errors/195 warnings. Protocol3 pair Run-20260907T022200 did not reach probe JSON or networking: both players failed D3D11 device creation (0x887A0005). Both owned processes closed; user restarting crashed Unity. **This build has no new two-player acceptance yet.** Earlier normal/impaired Relay evidence below is historical protocol2. Pending controlled relaunch after graphics recovery; no push. See WALL_WIND_ROUND_FOLLOWUP.md.

Status updated September 7: basic real Relay and impaired Combat scenarios passed in two separate players. Online Edit **35/35 at 00:31:40 UTC** includes the authority-clock regression; prior Play **4/4** remains at September 6 22:22:48 UTC. Scene and profiles are saved; work is uncommitted on `1235579`. Full online combat acceptance is not claimed.

The client now observes a monotonic host tick instead of advancing its own authority tick or rewinding it from older snapshots. The original +12/-180 tick validation window is unchanged. Diagnostics retain rejection reason/ticks/sequence and accepted/rejected counts. Normal `Run-20260907T004408` passed at 00:45:45 UTC: host accepted 447 motor and 532 control commands, zero rejections/gaps, two presses/releases, movement 3.935 m host / 3.832 m client, and normal host leave returned both to Main.

`Run-20260907T004650` passed at 00:48:21 UTC with actual Unity Transport send delay **150 ms**, jitter **30 ms**, loss **3% on each side**. Impairment was enabled after initial world readiness/countdown, so this accepts impaired Combat/input/disconnect only. Read-back confirmed settings; measured current RTT was 400 ms on both players (maximum 456/433 ms). Host accepted all 396 received motor and 524 control commands, with zero authority/epoch/sequence rejections. There were 42 unreliable motor sequence gaps and no reliable control gaps; these are not a measured datagram-loss percentage. Both attack edges reached the host, movement was observed, and normal leave returned both to Main. All test processes were closed.

Both runs used the 00:40:04 UTC Development build (0 errors, 189 warnings); `Elemental.Online.dll` SHA256 `53EEA86445140D0A8C3C1DFD54428C85030E8C26A35850BF8E7D63494AEBD2D4`. Build/hash records are `BuildReports/OnlineProbe/ClockFixBuild.json` and `ClockFixAssembly.json`. One shader line-ending warning from that intermediate build was subsequently corrected.

Stone-combat `Run-20260907T004924` is **partial, not passed**: first real ground-selection attempt reached the throwing step, where compensated aiming fell outside the actual cast-camera frustum. No selected stone hit/kill was observed. Two replicated leg impacts were `Physics`, source ID 0, and cannot count as stone hits. Source review identifies a possible third-person parallax problem: quick shots use the camera ray direction from the stone's different position. This requires a focused aiming/contact check; it is not yet proven as the cause of failed player attacks. Both owned processes were closed. Remaining: attributed thrown hits/kills/respawn, two-way combat, invalid/full code/cancel, broader geometry consistency, footage and GPU acceptance. No push before user review.

Repeatable probe launcher: `Tools/RunOnlineDevelopmentProbe.ps1 -Scenario basic -DelayMs 150 -JitterMs 30 -LossPercent 3`; use `-Scenario stone-combat` for the separate attributed-hit scenario. Only explicit development probes install virtual input or the transport simulator. Reports and process IDs are written to a new timestamped run directory; processes must be closed after results.

Final review build: saved arena rebuilt at **00:56:53 UTC**, success, **0 errors / 188 existing warnings**, including gameplay vignette darkness 0.20 and normalized shader line endings. `Elemental.Online.dll` SHA256 is identical to the normal/impaired tested build above. No redundant Relay rerun is claimed for this presentation-only rebuild.

## Earlier basic checkpoint (superseded above)

Final evidence: `BuildReports/OnlineProbe/Run-20260906T235548/host.json` and `client.json`. Real anonymous Host/Join, matching build/world hashes, initial geometry barriers, countdown and sustained Combat completed. Client supplied real PlayerInput W and two primary clicks. Host observed 3.395 m tangential movement (client 3.275 m) and accepted both presses/releases, sequence 377. Both ended with the same canonical health, 97.931564 for each actor. Host left through normal EndMatch; both returned to Main/Idle, and the client received “The other player disconnected. The online round has ended.” Final outcomes are `host-authority-input-and-disconnect-passed` and `movement-input-and-host-disconnect-passed`. Both owned test processes were closed.

Earlier limits: health reduction did not establish the damaging stone/region. The retained command rejection had no timestamp/subreason and could not be attributed to shutdown. The clock fix and subsequent normal/impaired evidence above supersede that uncertainty; stone-combat acceptance remains pending.

## Packages and ownership

Built-player checkpoint, Run-20260906T234551 (23:47–23:48 UTC, build 23:45:20): the runtime collision-material failure no longer occurred. Client movement/primary input produced live world changes; both sessions remained connected and Combat kept advancing (client tick 2041). Host probe incorrectly ended its report at tick 618 because live geometry ACK temporarily made WorldReady false. Initial world readiness remains required; a transient live-geometry ACK is not itself an ended match. This probe condition is being corrected before verifying host leave. Both owned processes were closed; this checkpoint is not a completed probe.

Built-player checkpoint, Run-20260906T233036 (23:32:14 UTC, build 23:29:57): the early virtual-device setup worked. Client tangential movement was 4.969 m; host observed 5.067 m and accepted one Primary press/release (sequence 335). Both reported non-full health. The next generated-object replication failed with `Collision material is absent from the online catalogue`, before the second click and planned host-leave check. Health reduction alone does not prove which stone caused it. This concrete gameplay failure is being corrected; full probe acceptance is still pending. Both owned processes were closed after retaining reports.

Built-player checkpoint, Run-20260906T231852 (23:20:48 UTC): fresh anonymous profiles created/joined successfully; both worlds became ready, both frontends entered Combat and remained there for ten seconds. The former countdown receive race did not recur. The virtual-input phase then stopped with `Invalid user`, before movement/attack/disconnect acceptance. This is a partial checkpoint, not a passed full probe. Both owned processes were stopped after retaining their logs.

Earlier actual service evidence: rebuilt simultaneous players loaded without the former sharing crash. The restricted runner's initialization failure was PlayerPrefsException while UGS saved its installation ID, not missing cloud configuration. An approved unsandboxed pair created a genuine Relay session, joined as client IDs 0/1, matched build/world hashes, synchronized world and reached round startup. Client then disconnected because a reliable actor/match state arrived before its countdown Update completed. That receive/start ordering race is fixed in the current build; sustained combat/movement acceptance remains pending. Evidence: BuildReports/OnlineProbe/Run-20260906T2254. A later run, Run-20260906T231626, reached Lobby creation but timed out subscribing to Lobby WebSocket events (Command 2 timed out), before a room was usable; no player crash occurred. A bounded retry uses separate anonymous profiles. All processes from those prior attempts were closed.

Unity Multiplayer Services 2.3.1 Sessions/Relay, NGO 2.13.2, Unity Transport 2.7.4. The earlier planned MPS1.2.1/NGO2.7.0 fail against Unity6000.5 EntityId/editor API removals. Official releases resolve that incompatibility without local package-cache patches. [Decision and evidence](adr/0003-network-transport-spike.md).

Sessions owns connection startup. Host simulates Earth commands, impacts, damage, stun, structures and terrain. Client predicts its motor and consumes canonical state. Two actors have separate owner graphs/executors/pools. No fake room codes, host migration or mid-match join.

Runtime EarthPhysicsFeelProfile contact materials have no asset ID. BodySpawn now carries their exact dynamic/static friction, bounciness and combine modes; catalogued materials still use their IDs. Client replicas own and dispose inline material copies. Invalid properties and contradictory inline/catalog IDs are rejected. Packet protocol is version 2 on the stable discovery channel, so an older protocol is rejected explicitly. User physics-profile values are unchanged.

## Setup and checks

1. Save the user's existing EarthCoreSlice. Run **Elemental > Online > Install EarthCoreSlice Online Only** outside Play. The installer operates on the existing arena and preserves the original animation assets; it marks the scene dirty for review rather than saving automatically.
2. Review and save scene/assets. The installer owns a separate Undo transaction. A later validation error reverts its new roots and added components. The generated geometry catalogue is a separate derived asset and may remain updated after an error.
3. **Elemental > QA > Online Stage2 Edit** and **Online Stage2 Play** run SDK wire/authority/input/geometry/identity checks. These do not connect to Relay.
4. **Elemental > Build > Build Online Development From Saved Arena** builds the saved arena only to `Builds/OnlineDevelopment/ElEmental.exe`. Development is required for the optional CLI probe. Build report: `BuildReports/OnlineDevelopmentSavedArena.json`.
5. Launch two instances of that same build with different `--online-profile peer-one` and `--online-profile peer-two`. First Host creates an actual room code; second Join uses it. Each presses Ready. Match countdown starts only after both peers and their exact world/rig/terrain barriers are ready.

Existing project ID: `7cdc3bd5-c779-4150-ada9-0fbb67eaa027`. Observe real SDK authentication/session errors before changing cloud settings. This workflow needs the linked project's Authentication/Lobby/Relay availability; it does not need Ads, Analytics or Multiplay dedicated hosting.

## Optional development smoke probe

Use an existing empty output directory with absolute JSON paths. Host:

```
--online-profile peer-one --online-smoke host --online-result C:\absolute\test-run\host.json
```

Client:

```
--online-profile peer-two --online-smoke join --online-join-file C:\absolute\test-run\host.json --online-result C:\absolute\test-run\client.json
```

This calls real frontend Host/Join/Ready, then records SDK/session state, peer IDs, content hashes, world readiness, countdown and canonical health/tick. After ten uninterrupted seconds of synchronized combat, the client supplies W for one second and two primary clicks through its actual PlayerInput. The host records accepted input edges, tangential movement and health, then leaves through the normal frontend. The client must return to Main with a disconnect message. Host reads `client.json` beside its own report, or an explicit `--online-peer-file` path. Final successful outcomes are `host-authority-input-and-disconnect-passed` and `movement-input-and-host-disconnect-passed`; `damageObserved` is separate and a missed attack does not verify damage. Processes remain alive after the report. Use graphics-capable execution; `-nographics` is not full arena/animation/render acceptance. Ordinary launches do not inject input or apply the probe's 30 fps cap.

The completed normal and impaired probes establish client movement, two accepted primary inputs and host disconnect returning the client to Main. Required broader evidence remains two-way stone combat, attributed hits/score/respawn, geometry/terrain consistency and cancellation/invalid/full code. No fabricated replica health or directly forced readiness may substitute for those checks. Earlier checkpoints are retained as failure history and are superseded by the newest runs for their tested scenarios.
