# Real two-process development probe

The scene installer adds an explicit OnlineDevelopmentProbe binding. Normal launches do nothing. Only Editor or Development builds recognize the opt-in flags; release player has no test polling or file IO.

Use two instances of the same Development build with different auth profiles. Create an existing empty output directory and use distinct absolute JSON output files. The host options are:

```
--online-profile peer-one --online-smoke host --online-result C:\absolute\run\host.json
```

The client options are:

```
--online-profile peer-two --online-smoke join --online-join-file C:\absolute\run\host.json --online-result C:\absolute\run\client.json
```

The host creates a real Sessions/Relay room through Main > Host. The client reads that actual code and calls Main > Join. Both use the frontend Ready action. Neither advances the readiness barrier or supplies fake hashes. Reports update twice per second by atomic file replacement, with actual SDK session state, host peer count, local NGO ID, build/world hashes, world readiness, shared countdown remainder, canonical tick, health and frontend state. A successful outcome is `connected-world-and-combat-10s` only after ten uninterrupted seconds of real running combat and synchronized world. The processes remain alive for manual gameplay and disconnect checks. A four-minute timeout or real service error is a failure; do not label it success.

This is connectivity/world-start smoke evidence only. Real movement, stone damage, wall/terrain geometry, host-loss behavior, latency/loss, visuals and performance still require their own checks. Do not replace those with JSON field assertions.

Source/launch preparation is not execution evidence. No service request or standalone process has run from this lane yet.


Root build menu: Elemental > Build > Build Online Development From Saved Arena. It builds only the saved EarthCoreSlice into Builds/OnlineDevelopment/ElEmental.exe with BuildOptions.Development and writes BuildReports/OnlineDevelopmentSavedArena.json. Save reviewed installation first. It does not regenerate authoring or overwrite LocalAlpha.

Grouped scene-independent tests: Elemental > QA > Online Stage2 Edit / Online Stage2 Play. Existing focused launcher saves/restores the scene and writes fresh JSON/XML; dispatch is not pass evidence.

Project settings already contain cloud project ID 7cdc3bd5-c779-4150-ada9-0fbb67eaa027. The legacy UnityConnect m_Enabled=0 does not establish whether current UGS auth/Relay works. Observe actual initialization/auth/create responses first; enable required Authentication/Lobby/Relay services in the linked project only if the live response requires it. Do not enable analytics, ads or Multiplay hosting for this Relay client-host workflow.


## Updated scenario after the real Relay start race (2026-09-06)

The current source continues beyond the initial ten seconds. The client uses temporary virtual Keyboard/Mouse devices paired to the installed local PlayerInput: W for one second, then two short primary clicks through the original gesture/executor path. The host counts actual routed Primary press/release frames and their sequence. Both report tangential displacement from a baseline captured after the ten-second settling period, health/score, and whether any damage was observed. A missed attack remains `damageObserved: false`; no hit is forced.

After the client completes its input phase, the host invokes the normal frontend EndMatch. The client must return to Main with a disconnect reason. The host reads `client.json` beside its own result by default; use `--online-peer-file C:\absolute\test-run\client.json` for a different filename. Final successful outcomes are `movement-input-and-host-disconnect-passed` (client) and `host-authority-input-and-disconnect-passed` (host). Separate boolean fields describe what passed. Neither outcome establishes a successful damaging hit, full two-way gameplay, geometry/performance acceptance, or delay/loss tolerance.

Report writers retain complete pending JSON and retry atomic replacement when an external reader temporarily omits delete sharing. Probe readers use FileShare.ReadWrite | FileShare.Delete. Ordinary player launches have no virtual input, frame cap, polling, or report I/O.

On this Windows host, the restricted runner could not save the UGS installation ID through PlayerPrefs; approved normal-user execution was required. The subsequent actual pair created and joined a real room, synchronized world and reached startup, exposing the receive/countdown race. That race now buffers up to 256 reliable match/impact packets after validated Begin and applies them after local BeginRound. Current source still needs its next built-player verification.
