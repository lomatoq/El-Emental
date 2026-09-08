> Integration checkpoint 2026-09-06 21:34 UTC: 54 new files and 15 exact runtime/frontend patches imported into Assets. Protected scene/theme/layout assets were backed up and left unchanged. Package installation, SDK compile, scene installer and real two-process verification are still pending at this checkpoint. Older wording about pending-only source below describes the preparation history.

# Stage 2 integration

See ACTOR_SLICE.md for the current concrete implementation, composition and remaining validation. Earlier interface-only and shared-pool proposals are superseded: owner pools/executors are separate; planet, matter kernel and surface service are shared.

All work remains under Stage2Pending until the alpha commit. New files and RuntimeBindings.patch must be imported together. The patch generator checks anchors and writes only pending copies/diffs; it preserves unrelated mass tuning and geometry edits.

Verified documented package releases for Unity 6000.5.7: com.unity.services.multiplayer@1.2.1, com.unity.netcode.gameobjects@2.7.0, com.unity.transport@2.6.0. Let MPS resolve Authentication/Core; verify the resolved lockfile and APIs after install. Official references: [MPS](https://docs.unity3d.com/Packages/com.unity.services.multiplayer@1.2/manual/index.html), [NGO](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/index.html), [Transport](https://docs.unity3d.com/Packages/com.unity.transport@2.6/manual/index.html), [MPS lifecycle](https://docs.unity.com/mps-sdk/faq), [session API](https://docs.unity3d.com/Packages/com.unity.services.multiplayer@1.2/api/Unity.Services.Multiplayer.IMultiplayerService.html).

Project already linked: 7cdc3bd5-c779-4150-ada9-0fbb67eaa027. Authentication profiles must differ for two local development processes and be selected before global UGS initialization. Sessions WithRelayNetwork owns NGO startup; UI must not call StartHost/StartClient again. Exactly one NetworkManager with UnityTransport, no automatic NetworkObject player. Dispose the disconnected old manager before a fresh scene reload.

Stable UI APIs:
- MpsRelaySession.HostAsync(), JoinAsync(code), CancelAsync(); Phase, Code, Status, Busy, Connected; Changed and ConnectionLost.
- NgoGameplayTransport.Configure(session,binding), SetReady(bool), Stop(); Running, Status; StatusChanged and RoundStarted.
- EarthOnlineGameplayBinding.IsOnlineAuthority and RequiresFreshWorld; CanHandshake versus WorldReady.
- EarthMvpDuelController.HasSimulationAuthority for host-only New Round controls.

CancelAsync observes the superseded SDK connect task and any late created session before completion. The SDK has no cancellation token; Busy remains true while that cleanup is pending. Disconnect has no host migration and requires a new room.

Host negotiation begins only after two real NGO peers connect. Initial world transfer begins after the client's compatible handshake response, avoiding early packet loss before its epoch is known. Start requires both user Ready states plus arena/rig gates, complete capabilities, matching build/world/catalog identities, initial world ACK and terrain geometry readiness. BuildHash and InitialWorldHash must be derived from the actual authored build/arena; never insert constant placeholder values to bypass readiness.

Wire uses bounded explicit primitive serialization, protocol 1, host epoch, per-kind sequence and authenticated NGO sender-to-actor mapping. Client kinds are motor, semantic controls, optional canonical command and world ACK; clients cannot publish damage/terrain/spawns/results. RegionState carries accepted action presentation; CombatState includes exact accepted hit region. Terrain transactions and world mesh chunks use reliable ordered messages. Only pose/input snapshots are transient. Node/mesh limits fail with useful errors instead of silently dropping canonical geometry.

Integration filters and evidence are in VALIDATION.md. No live two-peer, SDK compilation or performance acceptance is claimed yet.
Countdown steering (2026-09-06): host sends Begin with a shared NGO ServerTime deadline after both-ready/world barriers. Transport raises CountdownStarted without enabling binding input or advancing round time; BeginRound occurs only at deadline. Frontend reads SecondsUntilStart each frame for 4–3–2–1 and final 1.5-second camera blend. Late/invalid expired deadlines fail explicitly. Prepared source only: SDK compile and delayed-network two-player proof still required. Reference: https://docs-multiplayer.unity3d.com/netcode/1.4.0/advanced-topics/networktime-ticks/
