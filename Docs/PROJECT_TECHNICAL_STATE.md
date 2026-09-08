# El-Emental project technical state

**September 8 final wall charge/release clarification (base main `56e7d9e1`, same authorized main snapshot):** Ctrl+RMB click launches an ordinary shove on mouse release; holding charges a stationary wall, then RMB release while Ctrl stays down launches the stronger shove. Full charge1s, impulse×2.5, bounded released speed14..28m/s. Ctrl-first release/pause/stun cancels without launch. This supersedes continuous held drive. Final **Edit99/99** at `2026-09-08T15:40:19.5359569Z`; **WallPushPowerPlay3/3** at `2026-09-08T15:41:47.5587137Z`. Actual same-floor/same1668.881kg wall travels1.4347m on click versus7.2525m charged release; the wall stays still while charging. Real paired mouse release fires once, retains physical obstacle response, and preserves mass. Final held-push marker peak0.0767ms. Together with the earlier independent11 passing cases and final repair2/2 below, all14 distinct selected runtime scenarios have passing evidence across focused runs. [Final controls and measured limits](WALL_PUSH_TAP_HOLD_2026_09_08.md).
**September 8 earth interaction fixes (base main `56e7d9e1`, user-authorized main push):** Held LMB+RMB rows now allocate free pool capacity while earlier rows remain anchored. Airborne held Shift+Space commits one physical crater/ejecta/radial wave on arena FloorBase or planet; default2m drop/7.5m/s thresholds and a saved LandingSlam profile are bound to both actors. Walls rebuild fracture partitions in physical dimensions, begin with a seamless exterior and reveal bevel seams on first interaction. Ctrl+RMB latches one occlusion-tested wall and applies finite mass-dependent impulse; PhysX owns translation, support probes follow real ground, release preserves inertia and dust/chips emit near actual contact. Clockwise held-MMB recognizes rim-start circles and completes actual repaired seams after all fragments seat. UI movement/hover/press audio starts concurrently, preserving original source assets and music envelopes.

Final **Edit95/95** at `2026-09-08T15:19:17.3229473Z`; broad **Play11/13** at15:14:20 followed by corrected wall/repair **Play2/2** at `2026-09-08T15:20:37.7206176Z`. All13 distinct selected runtime cases have passing evidence, not a claimed single13/13 run. Real wall:1.304m forward,3.74mm side,14.83mm maximum floor penetration,895kg unchanged,0.0613ms held-push peak. Real MMB rebuilds exactly once. Grounded wall screenshot inspected, final console0 errors/0 warnings. Cold dimension-build Acquire19.045ms and synchronous slam peaks11.914–37.367ms floor/28.160ms planet remain one-off spike risks; no standalone build, online pair or GPU timing claimed. Updated semantic input bit2048 requires matching peers. User menu layouts/material settings untouched. [Wall motion](WALL_HELD_PUSH_STABILITY_2026_09_08.md), [wall geometry](WALL_GEOMETRY_PUSH_2026_09_08.md), [repair](CLOCKWISE_WALL_REPAIR_2026_09_08.md), [rows](PILLAR_ROW_INPUT_2026_09_08.md), [slam](LANDING_SLAM_2026_09_08.md), [audio onset](FRONTEND_AUDIO_ONSET_2026_09_08.md).
**September 8 frontend lifecycle/audio/camera follow-up (base main `2f0fbe86`, user-authorized main push):** Main and whole-match rematch restore the arena and authored day; local Main/settings/countdown/results freeze world physics while Update-based geometry restoration can finish. Ordinary life loss still preserves arena damage. Actual HUD Rematch reopens readiness; repeated closed gates re-disable controls without losing original flags. Platform drawing now has a readable cream contour and size-rejection feedback; real mouse input creates exactly one platform. The three supplied MP3s are bound through FrontendAudio.asset with quiet sidebar movement, smooth context changes and DSP-scheduled overlapping loops. Main and Pause use an unscaled character camera, restore gameplay policies on return, and follow actor relocation. Latest user-marked composition is saved at viewport(.78,.58), compensated for roll/aspect. User menu layouts and shared preview material unchanged. Final Edit **29/29** at `2026-09-08T13:15:16.9271438Z`; combined Play **10/11** at13:09:56 had only a camera fixture waiting0.8s against the authored0.85s transition. Corrected timing and latest portrait/audio Play **3/3** at `2026-09-08T13:16:24.6880489Z`; all11 distinct selected runtime cases have passing evidence. Main/Pause/restored screenshots inspected; zero console errors on final save. No new standalone build/online pair or full-length original-music seam listening claimed. [Lifecycle](MATCH_LIFECYCLE_RESET_2026_09_08.md), [input](PLATFORM_INPUT_REPAIR_2026_09_08.md), [audio/tuning](FRONTEND_AUDIO_2026_09_08.md), [camera/framing](MENU_CAMERA_CLOCK_REPAIR_2026_09_08.md).

**September 8 current-main snapshot, explicitly requested by user:** captures the accumulated project working state over `1235579`, including user-authored settings. Latest stone correction separates admitted health damage from flinch, restores armor attribution to detached ragdolls, and measures sustained pinning independently of recovery-timer blocking. Stone Edit **14/14**, final Play **5/5** at `2026-09-08T12:23:02.2237136Z`; real pile kills once, removal stops damage. Pin marker mean0.0281ms/peak0.2293ms in this focused fixture. [Physics evidence](STONE_LETHALITY_FOLLOWUP_2026_09_08.md). Debris closing momentum corrected; production/authoring pools enlarged72→256 with finite-budget and mass safeguards; fracture Edit **2/2**, Play **1/1** at `2026-09-08T12:23:57.1924010Z`. [Fracture details](DECOR_FRACTURE_FOLLOWUP_2026_09_08.md). UI final evidence below. Historical online/build/GPU limitations are not newly tested by this snapshot. Local backup archive and npm cache stay outside Git.

**September 8 notification correction (working tree main `1235579`, pending user-authorized snapshot):** WIN/LOSS/DRAW all use the existing light plate and dark text, uniformly reduced 12%. Returning is now centered display-font text without a sprite, over a 48% dark scene veil. Complete sprite rendering preserves artwork proportions; actual layout centering prevents margin drift. Twelve user MenuLayouts assets unchanged. Edit **5/5**, final production Play **2/2** at `2026-09-08T12:02:00.9360617Z`, 27.6821s; final screenshot inspected. Earlier dark LOSS and Returning-plate descriptions are superseded. [Evidence](HUD_NOTIFICATION_PLATES_2026_09_08.md).

**September 8 procedural dust revision (uncommitted main `1235579`):** user rejected static curved dust domes. Replaced them with shallow asymmetric deforming wisps, independent widths/lengths/speeds/lift and breathing, stable per-birth shader phases, two-phase UV advection, interpolated velocity/orientation/support, and alpha-only retirement without lifetime/atlas jumps. Surface probes no longer pull wisps onto abrupt obstacle tops. Removed hard-coded ordinary-dust tint; RumbleDustLit now drives live Tint/alpha/Brightness/night fill for mixed soft/impact/fracture layers. User's WallcoeurGroundDust.mat remains byte-identical. Edit **2/2** at `2026-09-08T11:18:55.7564446Z`; final Play **3/3** at `2026-09-08T11:23:49.3034901Z`, 24.4971s. Production sample: 352 wisps, speeds .376–2.741m/s, widths .431–4.377m, 42541 changed pixels; whole-adapter CPU .9973ms mean /1.6353ms peak, GPU unmeasured. Shader errors=false; no material assets edited. [Contract, superseded approach and evidence](CURVED_SURFACE_DUST_2026_09_08.md).

**September 8 bot-start transition repair (uncommitted main `1235579`):** replaced the short sidebar drift plus fade with a complete opaque 0.54s departure. A brief dark cover conceals countdown-camera reframing; the local countdown begins after the 0.82s intro. User layout/material settings unchanged. Reduced Motion uses 0.18s fade. `BotStartTransitionPlay` **3/3 passed** at `2026-09-08T10:55:46.7301500Z`, 59.6992s; captures verify moving opaque sidebar, camera reveal and countdown, plus subsequent match results. The initial regression measured the sidebar before stagger completion; its settle wait was corrected. [Details](BOT_START_TRANSITION_2026_09_08.md).

**September 8 dense dust/clouds and procedural UI (uncommitted main `1235579`):** sidebar panel-first reveal and staggered buttons/icons added, pause now presents pressed feedback before dispatch, Returning uses dark button art with reversible fade/scale, round and match results gain reveal/exit motion. Existing user layout ScriptableObjects and staircase remain intact. Ground dust profile is denser and larger, with seam wisps and soft alpha-only atlas interpolation/deformation; original contact dust mix retained. Fire gains bounded embers and 3px heat haze. Clouds: 22 banks, including 8 overhead, plus 52 image particles; soft proxy occlusion attenuates shafts. Edit **21/21** passed at `2026-09-08T10:34:37.6501386Z`; Play **11/11** passed at `2026-09-08T10:37:15.7131391Z` (95.0015s). Production dust: 286 alive, 113988 changed pixels, 431 gap births; CPU adapter mean **1.175ms**, peak **4.2314ms** (increased from prior .234ms; GPU transparency/raymarch cost unmeasured). UI layout-only mean .1871ms. Fire allocation sample 0 bytes/128 ticks. Final modified shader scan found no errors; EarthCoreSlice restored nonplaying/clean. Motion-vector atlas interpolation and exact volumetric cloud shadows are not implemented. [Research, settings, evidence and limits](DENSE_DUST_CLOUD_FIRE_RESEARCH_2026_09_08.md).

**September 8 button-group rollback:** user reported coordinate jumps while moving the newly introduced whole-button group and requested reverting it. Removed the `Buttons` hierarchy wrapper, its quick Inspector controls and its five saved profile entries. Restored buttons directly under their original page; the authored staircase, readable labels and dust changes remain. The prior whole-group acceptance is superseded by this rollback.

**September 8 dust/UI authoring follow-up (uncommitted main `1235579`):** replaced animated dust coverage with the real fire atlas, accelerated one-shot playback, retained the original soft dust, corrected black night shading and strengthened ground wind/stone wakes. Menu Inspector now has Russian labels and an independent whole-button-group transform for each sidebar page. Restored the user's required authored button staircase (`x=y/3`); earlier straight-row alignment is superseded. Edit **14/14**, combined production **6/6**, final staircase/group-scale Play **4/4** passed. [Evidence and tuning contract](DUST_FLOW_AND_MENU_NAMES_2026_09_08.md).

**September 8 editable menu/result presentation (uncommitted main `1235579`):** installed 11 per-screen ScriptableObject profiles, including 43 live sidebar bindings and independent Victory/Defeat layouts. Added animation-composed transforms, dimensions, font/padding overrides and searchable Inspector. Removed duplicate result-button rim, corrected dimming layer over REMATCH, aligned sidebar rows and added overlay purple/green fringes with local emblem glow/motes. Focused Edit 3/3 and Play 4/4 checks passed; final padding reset rechecked in Edit. Screenshots and measured adapter-only CPU cost are recorded in [menu implementation report](MENU_LAYOUTS_AND_RESULT_POLISH_2026_09_08.md). Native Graphics Ring Buffer warning remains outside this change.

**September 8 animation/UI/blue fog implementation (uncommitted main `1235579`):** fixed phase-clock braking, zero-slot cancel recovery, retained outgoing timing and moving A/B recovery; installed authored torso-only Living Hold additive on existing controller. UI panel/button retargets stay continuous and preserve paused input/Reduced Motion. Shared spatial gusts installed on existing ground dust. Fog uses protected1800–3200m closure, transmittance-attenuated far artwork, stronger saved blue palette, and exact clear-depth classification (the old1e-5 threshold misclassified far opaque stone as sky). Edit **27/27 +33/33**, Play **6/6 +5/5** passed; final Play00:23:41Z. Actual far black/white pixel difference0, hidden4000/4500m depth difference0; near contrast737960. Production scene restored nonplaying/clean; fog shader errors=false. Wind whole-adapter mean .2263ms/peak .9070ms in this capture; no GPU or added-effect-only budget claim. This is torso motion, not new authored hand choreography. [Implementation, evidence and limits](ANIMATION_MATERIAL_FOG_RESEARCH_2026_09_08.md).

**September 8 Wallcoeur fire/dust and AO (uncommitted main `1235579`):** black atlas filtering edges removed and rendered; original soft dust now mixed with animated dust within the existing event budget. Fire combines authored3x3 shapes, opposing noise flow, core HDR emission1.4 and separate depth-aware2px heat haze after atmosphere. Image clouds16→28 across side/low/high views. Contact AO retains full resolution/geometry normals and bright material floors. Latest Edit23/23 at00:25:20Z, combined Play12/12 at00:21:14Z, final production2/2 at00:26:36Z. Heat on/off pixel difference93910; sonar heat difference0. Editor recovered, current error console empty. The earlier Graphics Ring Buffer warning is not claimed fixed; isolated heat-pass GPU cost remains unmeasured. [Details](WALLCOEUR_FIRE_DUST_AO_2026_09_08.md).

**September 8 visual/physics follow-up (working tree main `1235579`):** reproduced and corrected buried-floor support selection, mirrored foot-channel mapping, pivot-anchor monopoly and ragdoll recovery escaping sustained load. Crush Edit38/38+Play5/5; footing/turn Edit50/50+Play5/5. Flame, dust, cloud/sonar, rock form-lighting and compact reference-art HUD changes pass final Edit22/22+Play10/10 (22:38:52 UTC); final visual evidence is recorded in [follow-up report](VISUAL_PHYSICS_FOLLOWUP_2026_09_08.md). GPU/full-game and arbitrary moving-pile acceptance are not implied. Earlier blanket turn acceptance is superseded by these reproduced defects and fresh focused results.

**Fire / stone / graphics integration in progress (2026-09-07, uncommitted main1235579):** Fire Play4/4; eight-High1080p cosmetic CPU p95 .9711ms,0B GC still misses .8ms target; GPU unavailable. Stone physical4cm heavy-drop/compositing/wall contracts pass. Production arena restore2/2 and UI4/4 pass after scene ownership and baseline-before-physics fixes. Basic online Run-20260907T093340 passed; selected-stone combat remains partial, next build includes event attribution trace. Old main-menu Left veil removed in actual code and verified live (BuildReports/MenuBackingRemoval). Latest user requires reference matching, including UI animations and procedural valley: optional UI V2 is under actual Unity validation; original font/layout assets retained. Geometry refinement14/14 and atmosphere V2 equations12/12 pass Edit tests; combined visual/performance gates remain open. [Contracts, sources and evidence](FIRE_STONE_GRAPHICS_INTEGRATION.md).

**Latest saved handoff:** corrected whole-match arena restore Play2/2 passed02:17:13; paired mouse Edit12/12+Play3/3 passed. Development build02:21:20 saved,0 errors/195 warnings. New protocol3 pair aborted before networking due D3D11 device removed; owned players closed and user restarting Unity. Further launches paused for recovery. No push. Details: [wall/wind/input/match follow-up](WALL_WIND_ROUND_FOLLOWUP.md).

**Latest user correction (02:11 UTC): arena restoration is once per whole game/session, not after each life. Damage/debris must persist across ordinary KO/respawn; restore on full-match victory, return to Main, restart/new game. Earlier per-life reset acceptance below is historical and does not satisfy this corrected requirement. Corrected lifecycle Play2/2 passed02:17:13 UTC. Paired mouse Edit12/12 +actual row/RMB Play3/3 also passed. Fresh build/network run follows.**

**September 7 wall/wind/round follow-up:** saved matching wall interior palette and sealed wide-bevel junctions; production depth-varying 3D partition rebaked without arena regeneration. Surface Edit 7/7, depth Edit 2/2, rise Play 1/1, wind Edit 2/2 + production Play 1/1, actual routed RMB launch Play 1/1, arena reset Play 2/2 and online Edit 35/35 passed. Protocol 3 requires a fresh built pair; current online acceptance pending. No push. [Evidence and settings](WALL_WIND_ROUND_FOLLOWUP.md).

Updated: 2026-09-07

**Permanent gameplay vignette, 00:42:57 UTC (uncommitted `1235579`):** independent of charge; soft peripheral blur plus darkness **0.20** after user requested a darker preview. Centre is unchanged in the same-frame production capture. Production visual Play **1/1** passed. **Elemental > VFX > Edit Gameplay Vignette** opens the saved material controls. [Evidence and limits](GAMEPLAY_VIGNETTE_REVIEW.md).

**Online authority clock and impaired Combat, 00:48:21 UTC:** client tick observation is monotonic and does not advance from local physics; original authority window unchanged. Edit **35/35**, normal Relay Run004408 and actual 150 ms delay / 30 ms jitter / 3% send loss per peer Run004650 passed with zero command rejections, accepted movement/attack edges and normal host disconnect. Impairment begins after initial world/countdown readiness. Stone Run004924 did not verify a thrown hit: compensated shot aim left the camera frustum; observed Physics/source0 damage is not attributed to a stone. Possible camera/stone parallax requires a focused check. Processes closed; full combat acceptance and user review before push remain pending. [Latest reports and tested DLL hash](ONLINE_ALPHA_TESTING.md).

**Per-life results and feel follow-up, 22:21 UTC (uncommitted `1235579`):** user HUD/theme edits were saved and old serialized settings compared unchanged. Added per-death Won/Lost/Draw with a 0.12 s simultaneous-death window and separate editable layout; menu press now scales only its centered visual and stays held. UI Edit **5/5**, production UI Play **2/2** passed. Capture/extraction smoke and chips, charge vignette and fake dusty sunlight installed and saved; VFX Edit **3/3**, particle Play **1/1**, production visual Play **1/1** passed at 22:18:37. Night shafts difference is zero; charge and arena frames inspected, decor capture partially occluded. [UI controls](FEEL_FOLLOWUP_UI.md), [VFX evidence and limits](POWER_VFX_FOLLOWUP.md).

**Basic Relay scenario accepted, 23:57:42 UTC:** MPS 2.3.1 / NGO 2.13.2 / Transport 2.7.4 and the second actor graph remain installed in the saved arena without regeneration or animation reassignment. Edit **34/34**, Play **4/4**, Development build **0 errors / 188 warnings** (23:55:14 UTC). Shared dataset reads, countdown receive ordering and dynamic Earth collision-material replication are corrected. Run235548 completed actual Host/Join, world sync, countdown, sustained Combat, client movement (~3.3 m), two host-accepted primary presses/releases, matching observed health and host leave with client Main/disconnect message. Both processes closed. A retained command rejection status has no recorded timestamp/subreason; delay/loss, attributed hits/kills and full Stage 2 acceptance/commit remain pending. [Evidence and limits](ONLINE_ALPHA_TESTING.md).

**Editable HUD layout, 20:55:57 UTC (uncommitted main `1235579`):** saved theme now references `ElementalHudLayout.asset`; **Elemental > UI > Edit HUD Layout** exposes separate screen groups and nested bar/readout/icon/value, navigation/globe and pause transforms. Live Inspector changes work while paused; entrance animation preserves authored pivots/rotation. Edit serialization **1/1** at 20:53:22 and isolated production-HUD Play **1/1** at 20:55:57 passed at three aspect ratios, including transformed pause pointer/picking. Captures inspected; one event-driven layout apply peaked at **0.1149 ms** in Editor (no claim about total UI renderer cost). Current online Host/Join remain disabled; pending Stage 2 is not integrated. [Settings and evidence](HUD_LAYOUT_TUNING.md).

**Locomotion, turns and world contact accepted, 19:03:38 UTC (uncommitted):** user clip/controller assignments remain unchanged. Final cadence/clock Play 2/2 passed: ten-cycle mean actual/requested player 0.999379, bot 0.996191 (both inside 1%); both feet reach settled stance. Targeted Edit 32/32 passed at 18:59:00, including anatomical reach release and reload safety. Four turn scenarios passed strict whole-sequence floor gates and visual review at 18:50:13. The real pit/hump/slope matrix passed both actors at controlled 30/60/120 Hz at 19:01:18: maximum settled drift 13.090 mm and absolute normal gap 1.304 mm, under unchanged limits. The runtime reach-release counter is not serialized by the surface report; no exact count is claimed. This supersedes earlier pending locomotion/slope checkpoints; final build/commit remain coordinator-owned. [Final contract, menus, evidence and limitations](LOCOMOTION_RHYTHM_IMPLEMENTATION.md).

**Shared editable stone mass policy, uncommitted main `1235579`:** initial mass
creation now has one per-world immutable policy across decor, arena, hero/quick/
bot stones, meteors and constructed physical earth. Canonical volume is stored
separately; held/debris/wall/platform children inherit conserved parent shares.
Coordinator compiled, installed and saved the asset/scene. **7/7 Edit** at
15:42:36 UTC and **3/3 Play** at 15:44:36 UTC passed.
[Contract and exact menus](SHARED_STONE_MASS_POLICY.md).

**Local physical impacts, 14:33 UTC, uncommitted main `1235579`:** eleven-region
prewarmed PhysX proxies now own flinch/stagger; medium hits cancel actions and
gate movement for 0.24s. Heavy ragdoll preserves sampled physical pose/velocity.
**24/24 Edit + 3/3 Play**, production captures inspected, local return measured
at 0.574s. Additional **2/2 Play** passed at 16:11:46 UTC: actual head/both-arm/
both-leg thrown contacts/recovery metrics, plus 96-frame profiling. Video review
found incorrect capture bounds; corrected framing and footage acceptance remain
pending. Mean
step/pose cost across both fighters is 0.011976/0.087081ms; 96 fixed and 149 pose
callback allocation brackets measured zero bytes. Regional footage uses a clear
test-only physical platform in the production scene.
[Physical impact contract and tuning](LOCAL_PHYSICAL_HIT_RESPONSE.md).

**Readability correction, September 6:** restored the original HUD with 12%
larger type; exact beveled cell assembly now supplies intact/cracked wall shape,
and the paired-mouse pillar line is saved 0.35m lower. Fixed sonar's persistent
five-entry GPU array truncation with versioned sixteen-entry globals; **3/3 Play**
passes. Actual short turns and skinned accessory motion pass; authored locomotion
is preserved. Charge includes live LMB accumulation with stronger visible lens
feedback: **19/19 Edit + Space/LMB 2/2 Play**, actual URP output inspected.
[Current repair contract](READABILITY_REPAIR_2026_09_06.md).
Stone shove/presentation transfer is 40% stronger after mass normalization;
the torso bends over spine/chest with slower viscous return. **53/53 Edit +
6/6 Play** pass, including production stagger and recoverable heavy hits.

**Character/premium repair, 02:04 UTC, uncommitted main `1235579`:** final
`SeptemberPremiumPlay` **10/10 passed** (48.824 s): live/KO respawn for both actors,
authored pivot, actual backward arena descent, three cancellable Mixamo transitions,
HUD, charge camera and both coordinated cushion regressions. Edit evidence:
37/37 support/pivot/respawn + 29/29 charge/short transitions. Costume evidence:
13/13 Edit + 1/1 Play, repaired clothing/head weights and constrained belt/plume
motion. Native HUD and actual transition captures inspected. Manual locomotion
assignments and other-task wall/sonar changes preserved. [Contract, provenance and
scoped measurements](SEPTEMBER_PREMIUM_ANIMATION_REPAIR.md).

**Mobility/wall correction, 01:18 UTC, working tree on main `1235579`:** line
placement offset is exposed separately from stone size. Held Space uses a live
half-crouch; cushion/surf follow the rider; high-impact cushion breaks into visual
stones. Long walls pass on actual uneven arena geometry, while finite source
bounds remain enforced. Chipped matching wall cells stay rigid while supported,
with stronger breakable source anchors. **30/30 Edit + 6/6 Play pass**, and charge,
compression and breakup captures were inspected. [Contract/evidence](MOBILITY_WALL_FOLLOWUP.md).
Concurrent locomotion/respawn/HUD/costume/camera work is a separate acceptance.

**Gameplay follow-up, 00:43 UTC, uncommitted main `1235579`:** full 122-frame kick
source, preserved user locomotion assignments, visible held-LMB+RMB line-height
control, 40 m sonar, fitting connected wall fracture, finite-face wall bounds/MMB
anchoring and mass/speed-scaled stone impacts are integrated. **64/64 Edit + 7/7
Play**, with final finisher **1/1 Play**, pass. [Details](GAMEPLAY_TUNING_FOLLOWUP.md).

**Seismic vision follow-up, uncommitted on main `1235579`:** one-second toggle
fades, half-width/dimmer waves, 2 m running pulse spacing, and wave-timed brighter
opponent silhouettes through occluders are implemented. Night fill/ambient are
5% lower. Fresh **10/10 pure Edit + 5/5 GPU Edit + 2/2 production Play** pass;
opaque-wall proof changes 4814 hostile pixels and remains byte-exact before the
wave/when off. Captures inspected. [Contract and evidence](SEISMIC_VISION_POLISH.md).

**Duel HUD / combo follow-up, working tree based on main `1235579`:** new HUD is
installed in the saved scene with explicit references, real HP100 and a separate
300-second round clock. Final EditMode passes **35/35** and production PlayMode
passes **9/9**, including physical paired-input progression, contact-synced foot
shots, visible bilateral kicks/spin, wall contact/release, health/scoring and
round restart. Actual gameplay HUD and four resolution captures were inspected;
HUD update CPU p95 is 0.04240ms (not total rendering cost). Canonical kick and
spin retract to guard, and an independent full-body layer preserves leg motion.
Runtime hips turns measure 350.06/360.04/361.18 degrees at 30/60/120 FPS targets,
without rotating the gameplay root off aim. Wall bracing now aligns the spine
before the existing arm solver, preserving the original 0.25m hand-contact gate.
Final wall recapture also passes 1/1, with actual hand distances 0.02153/0.02907m;
both contact/release images were inspected. Wall-step CPU averages 21.39 microseconds.
Evidence: `DuelAcceptanceEditFinal.xml` / `DuelAcceptancePlay5.xml` under
`TestResults`, at 23:27:58 / 23:24:29 UTC September 5 (September 6 locally).
See [current implementation/evidence](DUEL_HUD_IMPLEMENTATION.md).
Earlier outstanding mantle, armor and strict startup-cover gates remain separate.

**Latest verified rescue evidence, 15:28 UTC:** the gameplay camera correction is
active at runtime and saved in `EarthCoreSlice`. Focused camera/animation EditMode
passes **7/7**; the production full-body neutral -> magic -> return PlayMode scenario
passes **1/1** at 15:18:50 UTC. This is scoped pose/camera evidence. Final airborne
mantle acceptance and complete armor coverage remain pending.

The isolated SONIC experiment now has an accepted production-actor preview at
15:23:30 UTC in
`BuildReports/SonicPrototype/ProductionActorPreview/20260905-152326-358`:
**252 retargeted frames in 2.554 s**, four rolling plans, hand ranges **0.302/0.282 m**,
zero root drift and preserved production foot ownership. The captured PNG was
visually inspected. SONIC remains opt-in experimental code and does not own poses
in the production scene.

The outer-ring interior palette mismatch is corrected. `OuterArch_06` exposes
**4.3884 m²** of cut surface, which made the stale light interior material especially
visible; all ring structures now use the arena exterior/interior palette and the
dedicated lit fracture-dust response is restored. Fresh `OuterStoneRingPlay` passes
**8/8** at 15:28:20 UTC. Charged Surf+Space physical input and ordinary-pillar
regression pass **2/2 PlayMode** at 14:31 UTC. Sky acceptance includes nine reviewed
captures and day/night **3/3 PlayMode**.

The latest Player run accepts all **1939** baked fracture plans with **0 cache
misses** and a ready planet. The strict first-cover visual gate still awaits the
fresh final build after the cover presentation adjustment. This evidence does not
establish a cold-disk benchmark or prove that every startup stall is removed.

**Historical 13:31 UTC snapshot, superseded by the evidence above:** ordinary armor/jump/centered-aim corrections pass 5 Edit
and 1 physical Play test plus a four-frame visual proof with a 19-frame jump
audit. Armor has a separate encumbrance multiplier (~83% to ~75% movement speed)
and retains ordinary pose/mantle ownership. Semantic magic regression **8/8**
rerun at 13:20. Final horizon/Moon/system-planet matrix **9/9**, pure atmosphere
envelope **3/3**; see DAY_NIGHT_RESCUE. Player cache serialization omission has
been reproduced and fixed by clearing persistent-mesh DontSave flags: tiny
standalone-target bundle now preserves all three control meshes (3/3), and
new cache revision `867eb2621fa1419990940e4b51fd43cf` contains 1939 plans.
Full fresh-Player validation remains pending. Surf+Space is being refined per
the latest user request into a hold/release charged long jump along the tilted
pillar itself. SONIC remains an opt-in experiment, not a production pose owner.

**Current verification, 11:30 UTC:** see the [execution tracker](PROJECT_EXECUTION_TRACKER.md)
for fresh gate results. Semantic magic is **21/21 Edit + 8/8 Play**, dual-mouse
30/60/120 Hz **3/3**, C1 angular velocity **6/6**, ground-wave physical commit **1/1**.
The prior 176-degree arm flip and paced-clip early timeout are fixed. All-eleven
visual capture now completes **36/36** with head pitch within -21.46/+28.00 degrees.
Player compilation succeeds after correcting Editor-only gizmo guards in the
vendored motion packages; gameplay feature decoding is unchanged.

**Current remaining verification:** airborne mantle behavior, complete armor
coverage and the strict first-cover visual result from one fresh final Player build.
The earlier 26-miss Player snapshot and unaccepted SONIC preview are superseded by
the 1939/0-miss Player result and the scoped accepted preview above. See
[startup evidence](STARTUP_CACHE_RESCUE.md). No cold-OS-cache claim is made, and
the startup rescue is not declared complete until the first-cover gate passes.

**Sky:** day/night PlayMode **3/3** and nine accepted production-camera captures
show readable dark silhouettes, warm pink/orange dusk, changing solar shadows and
irregular stars. A runtime shadowless moon fill uses the existing profile (0.8); Rumble
rocks now receive URP additional diffuse lights. Main solar shadows remain owned
by the Sun. [Visual evidence and scope](DAY_NIGHT_RESCUE.md).

**Dust / seismic vision, 11:00 UTC:** physical dust now uses real light and SH.
The focused GPU Play suite passes **2/2** with day/night delta **0.39619595**,
night visibility **0.1160493** and neutral-reference footprint error **0.001231**.
An actual production Game-camera capture holds one exact particle layout across
Day/Dusk/Night; all three images were inspected and night dust is visibly dimmer.
This is common-material evidence, not all-technique visual acceptance. Local earth
vision composes monochrome expanding waves over opaque scene depth: **1/1**
production ground/launch/resume passes, while the temporal GPU filter passes
**5/5** at 30/60/120 Hz and leaves inactive day/night pixels byte-exact. This is
not an all-transparent-material or through-wall perception contract.

**Historical animation failures, superseded by the 15:18 evidence above:** magic Edit **11/12** (09:30 UTC)
has an outdated fixed-time clock expectation under slower clip limits; controlled
FPS **0/3** (09:32 UTC) exposes delayed release tails plus one router failure;
held aim **0/1** (09:34 UTC) records an actual 167-degree arm/hand flip at low IK
weight. Those failures were not accepted at the time. The corrected saved runtime
now passes the scoped **7/7 Edit + 1/1 full-body Play** gate; this does not close the
separate airborne-mantle or complete armor-coverage acceptance.

**Outer stone ring, 2026-09-04 (dirty `d2174ed`):** seven artist-edited columns
are imported as independent content and placed around the existing arena with
2.5 m final clearance and buried foundation caps. Existing arena materials and
fracture/grip/repair runtime are reused: **85 structural cells + 8 loose stones**.
Focused **2/2 EditMode + fresh 8/8 PlayMode passed** (latest UTC 15:28:20 on
September 5); all cells complete grab/repair cycles, unsupported islands release,
foundation-connected cells remain seated, fast post-separation impacts work, and
the corrected `OuterArch_06` cut surface uses the arena interior palette. Its
measured cut area is **4.3884 m²**. Dedicated fracture dust is restored through the
lit physical-dust path. Original scene geometry and gameplay settings are retained.
[Pipeline, scope and evidence](OUTER_STONE_RING.md). Wider M11 acceptance unchanged.

Current branch: `codex/environment-aware-motion-matching-spike`

Current implementation: dirty working tree on `d2174eded114dd022e4a9c442abadda7a0e44555`.

Historical foundation evidence below belongs to `8c2e6245a0e4fe1e169b63998e918ce800477b36`,
not the current dirty tree. Current scoped evidence is tracked separately.

## Earth material pass — September 4

**MMB held field restored (dirty `d2174ed`, UTC 14:14):** live user input exposed
stale armor ownership after release; frame consumption no longer relatches the
armor flag. The user clarified that MMB is an area field, superseding the earlier
single-target assumption. It collects nearby loose stones and new arrivals inside
the existing radius, capped at 48, and packs them by physical contact at a common
center without camera-side slot gaps. **9/9 Edit + 9/9 focused Play passed**,
including three arena cells after armor, repeated releases, new arrivals, contact
packing and resting-stone wake. Scene/profile settings retained.
[Live evidence and contract changes](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Raw MMB follow-up (dirty `d2174ed`, UTC 13:31):** added Input System middle-button
press/hold/pointer-move/release/repress coverage through the shipping adapter and
router. Focused PlayMode **8/8 passed**. This is verification, not another gameplay
fix: the user's remaining SКМ concern needs a matching reproduction, and is not
declared universally resolved. [Details](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Arena cursor/grip follow-up (dirty `d2174ed`, UTC 13:25):** user playtesting exposed
another acquisition failure after the earlier collider-direct tests passed. The
MMB caller incorrectly admitted Surface-only protected arena floor, masking loose
cells; the executor then reported success with no captured stone. Removed Surface
from that caller's capability mask and added pure session admission plus truthful
stone/structure/failure feedback. **4/4 Edit + 7/7 focused Play passed**, now through
screen-point picking against shipping floor/cell geometry, with lift, repeated
reacquisition and intact-column circle disassembly/repair. First press 2.4525 ms /
0 managed bytes in Editor. [Evidence and wider-test limitation](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Loose stones and oblique fracture (dirty `d2174ed`, UTC 12:48):** fixed gravity
target rejection inherited from a camera-hidden arena parent, perpetual waking
under radial gravity, and duplicate wall/platform fragment gravity. Quiet supported
stones sleep and wake on grip/support removal. Fracture uses angled cuts and broad
contained bevels; exact primary detachment is retained. **12/12 Edit + 5/5 loose
stone Play + 3/3 production fracture Play passed**. Thin recursive fill: 97.62%
collision / 89.03% visible; cached split max 0.2482 ms / 0 managed bytes in Editor.
User wave retuning is preserved (rise .4, settle .07994324, hold 0, retreat .2398297,
speed 8.54), SHA256 `CDF2D3C2B546204AE088EAAFD1E4156B51B2DAC4947BCF0FAF436FA30B82BE1F`.
[Rest/grab evidence](LOOSE_STONE_FIX.md), [fracture evidence](CONTAINED_FRACTURE_FIX.md).

**Overlapping wave + fitted head armor (dirty `d2174ed`, UTC 12:21):** supersedes
the propagation constraint below. Delay is strictly distance/speed; rows never
wait for another row's retreat. The Inspector now exposes **Длина фазы волны, м**
by proportionally editing the visible rise/settle/hold/retreat seconds. The saved
wave asset is unchanged (SHA256 `AA430FC5F0629920B549CD00E2F0EA8537A6F8A02ADD931C3FF2EA180EBF7FAD`).
Head plates fit 2,093 skinned vertices instead of the neck-radius estimate and
follow the final animated Head pose. Sixteen extra small seam stones join the
normal orbit/projectile pool (96 base + 16 fillers, capacity 112); body settings
and original anchor indices remain unchanged.

Fresh **23/23 EditMode** (`WaveContactEdit.json`, UTC 12:17), **5/5 PlayMode**
(`WaveHeadArmorPlay.json`, UTC 12:20:58): 93 simultaneously rising cells at the
authored 6.04 m/s, 76,822 pair checks with zero penetration, 1,246,647 projected
vertex checks with zero drift. Head coverage passes five exterior render-mesh
directions; all 16 fillers orbit and launch. Head follow error < .000004 m;
compact-follow marker .4998 ms, wave-contact marker max 5.8477 ms in this heavy
Editor audit. These are scoped Editor measurements, not shipping performance.
[Wave contract](WAVE_CONTACT_FIX.md), [head contract and known wider-suite failures](HEAD_ARMOR_FIX.md).

**Wave placement/propagation correction (dirty `d2174ed`, UTC 11:43):** immutable
meshes alone missed lateral snapping when the lowest support vertex changed.
Wave seating now changes height only, uses one cast tangent frame and contains
render bevels within each cell. Whole-cast reservation prevents partial topology
replacement; earlier anchored columns block overlapping new casts. The crest now
travels through all rows and leaves earlier rows retreating. Long authored phases
reduce actual speed (the slider is a maximum); user phase curves/seconds are unchanged.
Shared sand dust uses URP Particles/Unlit with its saved tint and soft alpha blending.
Fresh **21/21 EditMode**, **2/2 PlayMode**: 21,646 pair checks with zero penetration,
1,249,221 projected-vertex checks with zero drift, three advancing crest samples,
703 descending samples and zero dense-dust lighting delta. Contact marker max
1.9917 ms in Editor. [Evidence and prior failed baselines](WAVE_CONTACT_FIX.md).

**Filled fracture and stable-contact follow-up:** actual convex partitions replace
inscribed generic stone templates. Fresh thin/rotated/recursive measurements retain
**97.62% collision volume / 96.39% visible volume**, with exact arena detachment
preserved. Four cached splits cost at most **0.248 ms / 0 managed bytes** in the
fixture; cold preparation is measured separately. `ContainedFractureEdit` **8/8**
(UTC `10:10:37`), `ContainedFracturePlay` **3/3** (`10:17:57`).

Wave casts no longer reuse live cells, and their horizontal fracture outline stays
constant through the lifted depth. Contact-plane dust/chip streams and emergence/
retreat bursts follow each cell; terrain extraction is substantially denser. Small
wall collider hulls follow visible stones within PhysX limits, and gravity packing
uses actual geometric size instead of mass. The saved user profile now includes
3-second rise/retreat, speed 6.04 and push 115, all preserved. Latest production
`WaveContactPlay` **4/4** (`10:22:37`): 2,784 immutable mesh checks, 93 contact sources,
5,037 contact cues / 379 bursts, 40 matching wall hulls, contact error <2 micrometres.
Contact marker maximum **5.9642 ms** in the Editor is a scoped observation, not a
whole-frame certification. See [current details and evidence](WAVE_CONTACT_FIX.md)
and [convex fill](CONTAINED_FRACTURE_FIX.md). Earlier fitting results below are historical.

**Wave and contained-fracture repair (same dirty `d2174ed`):** arena detachment
retains the exact baked mesh/collider/material. Secondary stones fit inside their
actual parent convex, including rotated thin shapes and recursive splits; wall
stone visuals also use convex planes. The simplified wave Inspector exposes six
curves, five phase durations and five main controls. Bounded wave cells and restored
unit crest meshes remove the oversized geometry caused by sparse Voronoi territory
and pool reuse. User scene/profile settings were preserved.

Fresh scoped evidence: **24/24 wave EditMode** at UTC `09:26:48`, **1/1 wave
PlayMode** at `09:28:29`, **4/4 containment EditMode** at `09:25:52`, and **3/3
containment PlayMode** at `09:32:31`. Six repeated waves/crests produced 5,857
visible samples, maximum polygon span **2.133 m**. Four measured physical splits
cost at most **0.6496 ms**, **0 managed bytes** in the test scope. Wave launch
preparation still peaked at **168.865 ms** in the Editor; startup frame-time
performance remains a risk, not certified by the geometry checks.
See [wave repair](WAVE_REPAIR.md) and [contained fracture](CONTAINED_FRACTURE_FIX.md).

**Wall material correction:** fixed an excess `RumbleClay` renderer slot that
survived the natural-stone override. Natural wall chunks now have exactly one
sandstone material; saved interior reference and setup defaults match. The earlier
slot-zero assertion did not detect this bug; regression now checks all slots.

**Combat/mobility follow-up:** saved the user's latest scene/profile state before
editing (`BuildReports/CombatMobilityFixes/Before/`). Armor now routes structural
damage; walls, columns and platforms accumulate weak hits. Released arena pieces
split through the persistent debris/matter path on impacts. Natural stone render
variants, seeded shard rotation, denser surf/pillar feedback, cushion roll
suppression and smooth tunable wave phases are integrated. See
[settings and focused verification](COMBAT_MOBILITY_FIXES.md).
Final focused checks pass **9/9 EditMode + 4/4 PlayMode** (UTC `08:49:26`).

**Dust/shadow follow-up (same dirty `d2174ed` tree):** saved the user's Soft Sun
shadows at strength `0.554` and matched the scene-builder default. Cosmetic shards
now draw before dust without writing scene depth; physical stone still occludes
effects. Focused authoring tests **5/5** and pixel/production PlayMode tests **2/2**
pass at UTC `2026-09-04T07:37:29Z`. Settings, captures, profiler observations and
scope are in the [material-pass checklist](EARTH_MATERIAL_PASS_CHECKLIST.md#dust-and-shard-authoring-follow-up--september-4).

Implemented the typed spatial material-feedback path, material-authoritative dust RGB,
signed backward locomotion, 44 m/s armor, persistent mass-preserving size-class breakup,
cached render-only bevels, irregular surf stones, physical gravity release, restored
shadows and seeded whole-planet scatter. The narrow integration menu preserves scene
transforms, user light/material values, authored motion choices and camera/DOF.

The EAMM bake had mismatched direct-rest and collapsed-parent animation frames.
Schema 2 now records rest and sampled poses in the same collapsed-parent frame;
the invalid-pose guard remains enabled. A fresh production run reports Active.
Physical split children keep canonical matter identities and remain targetable beyond
cosmetic lifetime; pool exhaustion retains the parent instead of losing matter.

See [per-item contracts, settings, reports and remaining gates](EARTH_MATERIAL_PASS_CHECKLIST.md).
These focused tests do not supersede historical whole-project failures or establish
a 720-frame performance/zero-GC certification.

Companion tracker: [`Docs/PROJECT_EXECUTION_TRACKER.md`](PROJECT_EXECUTION_TRACKER.md)

## Purpose and truth protocol

This is the canonical short technical entry point for gameplay work. It explains what the
project is, where authority lives, how the layers connect, and which technical claims are
actually supported. It does not replace the detailed architecture, milestone, ADR, or raw
evidence files.

Use these labels when updating this file:

- **Fact** — visible in source, serialized data, an accepted ADR, or a named report.
- **Measured** — includes the exact report, conditions, and important caveats.
- **Decision** — accepted design/architecture intent; it may still lack implementation evidence.
- **Unknown** — must not be silently promoted to complete.

Freshness rule: after a material public-contract, package, scene-generator, performance, or
authority change, update only the affected sections and set the header to the tested branch and
commit. A report predating that commit is historical evidence, not validation of the new commit.
The live gate and risk state belongs in the [execution tracker](PROJECT_EXECUTION_TRACKER.md).

## Canonical source order

When sources disagree, prefer the narrowest current evidence in this order:

1. Current source/serialized assets at the tested implementation commit.
2. Fresh test/build/profiler report produced from that same commit and worktree.
3. Accepted ADR for architectural intent.
4. Current milestone for product scope and acceptance criteria.
5. Older milestone, README, or `.solo-studio` narrative for history only.

Start with this file and the tracker, then follow links instead of rereading the repository:

- [`Docs/architecture.md`](architecture.md) — stable layering, clocks, terrain authority, errors.
- [`Docs/milestones/M11-earth-mvp-0.1-rumble-duel.md`](milestones/M11-earth-mvp-0.1-rumble-duel.md)
  — current slice and detailed history.
- [`Docs/adr/ADR-0001-engine-stack.md`](adr/ADR-0001-engine-stack.md) — engine baseline.
- [`Docs/adr/0014-fracture-and-reassembly-v2.md`](adr/0014-fracture-and-reassembly-v2.md) — structural truth.
- [`Docs/adr/0021-semantic-earth-intents-and-shared-surfaces.md`](adr/0021-semantic-earth-intents-and-shared-surfaces.md)
  — input and surfaces.
- [`Docs/adr/0026-earth-core-v4-matter-grammar-and-choreography.md`](adr/0026-earth-core-v4-matter-grammar-and-choreography.md)
  — target matter/technique chain.
- [`Docs/adr/0029-visible-humanoid-ragdoll-and-deferred-platform-fracture.md`](adr/0029-visible-humanoid-ragdoll-and-deferred-platform-fracture.md)
  and [`Docs/adr/0030-mvp-01-earth-input-impact-and-matter-follow-up.md`](adr/0030-mvp-01-earth-input-impact-and-matter-follow-up.md)
  — current MVP runtime handoffs.
- [`Docs/adr/0031-broken-crown-arena-and-meteor-floor.md`](adr/0031-broken-crown-arena-and-meteor-floor.md)
  — arena authoring and damage contract.
- [`Docs/adr/0032-performance-aware-atmosphere-and-focus.md`](adr/0032-performance-aware-atmosphere-and-focus.md)
  — rendering policy.
- [`Docs/adr/0033-animation-contact-and-arena-rendering-rehabilitation.md`](adr/0033-animation-contact-and-arena-rendering-rehabilitation.md)
  — proposed animation/contact acceptance gate.

## Product and golden path

**Fact:** El-Emental is a Unity physics-action prototype about manipulating matter on small,
destructible planets. The non-negotiable Earth promise is readable, heavy, spatial magic:
shape or pull stone, move it under local spherical gravity, and make physical impact,
fracture, repair, animation, camera, and feedback agree about the result. The fuller product
charter is in [`.solo-studio/PROJECT_CHARTER.md`](../.solo-studio/PROJECT_CHARTER.md).

**Current golden-path scene:**
`Assets/Elemental/Content/Scenes/EarthCoreSlice.unity`.

**Rebuild authority:** `Elemental.Authoring.Editor.M3EarthCoreSetup.Configure()` in
`Assets/Elemental/Authoring/Editor/M3EarthCoreSetup.cs`, exposed as
`Elemental/Setup/Create M3 Earth Core Slice`. The scene is generated; change profiles,
source assets, importer/integrator code, or the setup code, then rebuild. Do not treat a
hand-edited generated scene as durable source.

**Current representative loop:**

1. Launch the 55.1 m-radius Earth world and move a Humanoid on local spherical gravity.
2. Draw/raise Earth structures or acquire physical matter through the routed mouse grammar.
3. Move, launch, fracture, disassemble, or repair provenance-bearing matter.
4. Fight the deterministic rival in Broken Crown; impacts produce localized response,
   recoverable knockdown, or bounded ragdoll/KO depending on the accepted hit contract.
5. Recover/respawn and replay without growing the prewarmed pools or losing terrain/matter truth.

The exact `55.1` profile value is serialized in
`Assets/Elemental/Content/Profiles/PlanetWorldProfile.asset`. M11 still contains older
radius-36 performance/gate prose; treat those passages as historical until the milestone is
reconciled. MVP 0.1 intentionally excludes HP bars, score, rounds, victory UI, progression,
NavMesh, behavior trees, and a second enemy class.

## Engine and package baseline

**Fact at the snapshot commit:**

- Unity `6000.5.7f1` (`ProjectSettings/ProjectVersion.txt`).
- URP `17.5.0`, Forward renderer; built-in PhysX plus custom local gravity.
- Input System `1.20.0`, Cinemachine `3.1.7`, Animation Rigging `1.4.1`.
- Burst `1.8.30`, Collections `6.5.0`, Mathematics `1.4.0`.
- Unity Test Framework `1.7.0`.
- MiniBokeh is a Git dependency pinned to commit
  `faa491907b7a580ef2ddfebfbef4590d1d3c6628`; the project-local dual-subject DOF path is
  the current NativeHigh owner and MiniBokeh is disabled in the latest profiler audit.
- VFX Graph `17.5.0`, AI Assistant `2.18.0-pre.2`, and AI Inference `2.6.1` are present in
  `Packages/manifest.json`. Their presence is not evidence that they own canonical gameplay.

Native Windows/macOS are the engine-stack priority. WebGL2 is a reduced `WebLab` capability
profile. A production online transport is not selected.

## Layer map and dependency direction

```text
device input
  -> normalized frame / semantic route
  -> command or bounded runtime intent
  -> pure simulation policy + canonical state
  -> Unity runtime adapters / PhysX / scene lifecycle
  -> typed events, snapshots, read-only state
  -> camera / animation / VFX / audio / UI / diagnostics

authored profiles + imported/baked assets
  -> validators and scene generator
  -> copied/baked runtime data (never mutable authoring state as gameplay truth)
```

| Layer | Owns | Important entry points | Dependency rule |
|---|---|---|---|
| `Elemental.Core` | Stable IDs, deterministic helpers, simulation tick primitives | `Assets/Elemental/Core/` | Assembly has `noEngineReferences: true`; no Unity object lifecycle. |
| `Elemental.Simulation` | Gravity, voxel/SDF state, magic/combat policies, matter/provenance, support, fracture/reassembly, networking contracts | `GravityWorld`, `VoxelPlanetState`, `EarthActionRouter`, `EarthMatterRegistry`, `EarthMvpBotPlanner`, `EarthDuelRespawnSolver` | Source scan at this commit finds no UnityEngine dependency, but the asmdef does **not** set `noEngineReferences: true`; discipline/tests currently enforce the boundary rather than assembly metadata. |
| `Elemental.Input` | Input System device boundary, viewport-normalized gesture sampling, semantic routing | `EarthInputAdapter`, `EarthActionRouterBehaviour`, gesture pipeline under `Assets/Elemental/Input/Gestures/` | Only `EarthInputAdapter` may read physical actions/devices. Consumers receive normalized data or resolved intent. |
| `Elemental.Runtime` | Scene/session lifecycle, PhysX handles, pools, world queries, command execution, fighter/terrain adapters | `MagicExecutor`, `VoxelPlanetBehaviour`, `PlanetMotor`, `EarthSurfaceQueryService`, `EarthArenaStructure`, `EarthMvpDuelController` | Converts pure contracts to Unity operations; must not let Rigidbody, collider, renderer, or GameObject state become canonical truth. |
| `Elemental.Presentation` | Camera, Humanoid/IK/secondary motion, URP, VFX, audio, UI, capture/telemetry | `EarthCameraDirector`, `EarthFootContactController`, `EarthCinematicDepthOfFieldController`, `CelestialSystemBehaviour`, `EarthPerformanceTelemetry` | Reads state/events. Animation/VFX completion may not decide damage, KO, terrain, matter, or repair. |
| `Elemental.Authoring` | Profiles, ScriptableObjects, validators, importers, bakers, editor menus, generated scenes | `M3EarthCoreSetup`, `BrokenCrownArenaImporter`, `EarthFractureBaker`, `ElementalProjectValidator` | Authoring data is validated, then copied/baked into bounded runtime state. |
| Tests/tooling | Pure unit tests, scene/runtime tests, build and evidence runners | `Assets/Elemental/Tests/`, `Scripts/Test-Unity.ps1`, `Mvp01FocusedTestLauncher`, `Mvp01EvidenceRunner` | Test duration is regression evidence, not a frame-time measurement. Reports must identify commit and conditions to validate current code. |

The assembly references currently enforce the broad direction
`Core <- Simulation <- Runtime`, with Input, Presentation, and Authoring depending on the
runtime/domain layers as needed. Presentation also references Input for camera/interaction
context; this is acceptable only while presentation remains non-authoritative.

## State ownership and data flow

### Input to Earth action

`EarthInputAdapter` reads Unity Input System actions and emits normalized viewport/device
state. `EarthActionRouterBehaviour` buffers the short dual-button decision window and feeds the
pure `EarthActionRouter`; the resolved owner/phase decides whether primary magic, force,
vector field, gravity, armor, resonance, surf, or movement consumes the input. Gesture code
resolves stable surface/target handles before runtime execution. `MagicExecutor` and dedicated
bounded controllers adapt accepted intent to the world. Replays store resolved commands, not
raw pixels.

The non-shipping Rumble lookdev shortcuts also route through optional actions on
`EarthInputAdapter`; presentation components no longer read `Keyboard.current` directly. The
physical-device boundary source scan is green in the 2026-08-31 full EditMode run.

Primary risk: input, camera ray projection, dense arena colliders, and delayed chord replay meet
at this seam. This is why physical-device PlayMode tests are acceptance gates rather than an
optional unit-test supplement.

### Terrain and matter

`VoxelPlanetState` owns the analytic base SDF plus ordered `SdfEdit` history.
`VoxelPlanetBehaviour` owns Unity-side chunk/render/collider queues; meshes and colliders are
caches. A terrain extraction remains transactional until `EditCommitted`; only then may the
reserved fragment become visible/held. `EarthMatterRegistry` owns stable matter identity and
provenance across terrain, fragment, structure, armor, and return transitions.
`EarthMatterMassPolicy` plus `EarthMatterMassRuntime` convert authored/collider scale to shared
gameplay mass for the runtime sources that are wired to it.

### Structures, fracture, and repair

`EarthFractureAsset` and `EarthArenaFractureCatalog` contain validated authoring topology.
Pure bond damage, island, ordering, and pose solvers own canonical structural decisions.
`EarthWall`, `EarthPlatform`, and `EarthArenaStructure` own bounded Unity representations and
proxy switching. Stable structure/piece/bond IDs survive pooling. The Broken Crown floor is
ordinary-damage immune and may swap to its 36-piece representation only for typed meteor
impact; walls, columns, gate, and loose rocks use their narrower contracts.

Platform casting prewarms six roots and up to 48 shells per root. The solid walkable collider
is immediate; fracture topology/preparation is deferred one cell per rendered frame, and early
impact is retained until `FractureReady`.

### Characters, support, impact, and duel

`PlanetMotor` owns canonical movement/grounding. Presentation contact IK must follow the motor
and stable support handles; it may not move gameplay truth. At the tested implementation commit,
`CharacterSupportRuntimeAdapter` classifies bounded non-alloc SphereCast/Raycast candidates for
the pure `CharacterSupportAuthority`, and `PlanetMotor` consumes the selected support with a
small retention bias. Arena and voxel surface providers expose explicit support classification;
released pieces and dynamic debris are rejected. The targeted runtime debris/support regression
passes in `BuildReports/FoundationWorkingTreePlay-20260831.xml`.

The Linebreaker runtime FBX now retains the original 48-bone Humanoid hierarchy and adds only
`Secondary_HelmetAnchor` and `Secondary_HairLock`. The authored weighted source distributes the
plume across the three tail bones plus the hair lock and weights both two-bone belt chains. The
generated shipping scene was rebuilt through `M3EarthCoreSetup.Configure()` and the targeted
scene test confirms that player and rival configure helmet/hair, tail, and both belts before
scene unload.

`EarthMvpBotPlanner` owns deterministic rival phases. `EarthMvpBotController` adapts them to the
motor and pooled projectile. Impact solvers decide local reaction, recoverable knockdown, or KO.
`HumanoidRagdollRig` owns the visible 11-body physics handoff, while
`EarthMvpDuelController` owns KO timing, fade, reset, and respawn. Animator and PhysX must never
own the same bones simultaneously.

The current dirty working tree also contains the feature-flagged EAMM production animation pass.
`EarthAnimationGraph` is the only Animator/EAMM pose-composition graph and
`EarthInertializationJob` owns cached per-bone rotational continuity while excluding gameplay
root translation and planted feet/toes. The authored base controller is a 2D tangent-space
locomotion tree; front/back recovery, bounded slope response and bounded magic reach remain
explicit action/presentation lanes. This implementation is not accepted evidence: PlayMode,
30/60/120 capture, visual A/B and zero-GC profiling were intentionally not run in that pass.

### Presentation and lighting

Typed events fan out through presentation adapters to camera, dust, debris, scars, audio, and
animation. `EarthWorldResponseEvent` identifies one accepted gameplay outcome; presentation
must not reapply it. `EarthCameraDirector` owns semantic composition requests, not targeting or
damage. `CelestialSystemBehaviour` uses `CelestialLightingClockPolicy`: gameplay defaults to a
locked authored key/ambient state, with animated ephemeris reserved for explicit QA/lookdev.
URP atmosphere, DOF, SSAO, shadows, and motes are degradable presentation paths.

### Persistence and replay

`VoxelSaveCodec` is a version-2 binary codec for voxel base parameters and ordered edits and can
read version 1. `MagicReplayRecorder` is an in-memory, tick-ordered command list. Tests exercise
both, but no runtime code calls `VoxelSaveCodec.Write/Read`; atomic disk save, backup/recovery,
whole-session schema, cloud conflict, and migration fixtures beyond voxel v1 are therefore
**not shipping-complete**.

### Networking

`Elemental.Simulation.Networking` defines transport-independent commands, authority decisions,
terrain replication, snapshots, relevance, correction, and a deterministic
`SimulatedTransport<T>`. ADR 0003 accepts this for the M8 spike only. There is no selected or
wired shipping socket/relay transport, and the M11 golden path is local.

## Current strengths

- Explicit typed IDs, commands, events, provenance, and bounded pure solvers give most important
  state a named owner and a unit-test seam.
- Terrain and structure representation separate canonical state from meshes, colliders, VFX,
  and pooled views.
- The project has wide automated coverage: 129 C# test files and 590 `[Test]`/`[UnityTest]`
  annotations at this snapshot, plus focused physical-input, scene, animation, and profiler gates.
- Hot paths use fixed buffers/non-alloc physics queries and named profiler markers; capability
  profiles make many hard capacities explicit.
- Generated-scene, Blender, import, fracture, animation, material, and project validators turn
  repeated solo-production work into reproducible tooling.
- Experimental rendering and online work have explicit fallback/removal seams rather than owning
  gameplay truth.

## Weaknesses and technical debt

- The tested implementation has no complete green acceptance run. A fresh full EditMode run passes
  `586/587`; its sole failure is the zero-warning build-evidence gate because the existing Native
  Windows report records 186 warnings. The two new foundation PlayMode regressions pass `2/2`,
  but the complete focused/broad PlayMode suites remain pending. See the tracker for exact scope.
- `M11-earth-mvp-0.1-rumble-duel.md` mixes accepted claims, radius-36 history, radius-55.1 state,
  and a previously partial `139/142` + `10/17` snapshot. Status cannot be inferred from its title.
- `M3EarthCoreSetup.cs` (~3,506 lines), `MagicExecutor.cs` (~2,151 lines), and the generated
  `EarthCoreSlice.unity` (~175,029 lines) are large integration surfaces. They accelerate one-click
  reconstruction but amplify merge risk, hidden coupling, and broad regression blast radius.
- The Simulation source is currently Unity-free, but its asmdef does not enforce
  `noEngineReferences`; one accidental Unity reference could erode the domain boundary.
- `BuildReports` contains tracked baselines plus overwritten/untracked working evidence. A
  filename containing `Latest` or `Accepted` is not proof unless its timestamp/commit and result
  match the claim.
- Some newest foundation policies are not equally integrated. Shared mass is used by arena
  pieces/decor, celestial clock policy is wired, and classified support now feeds `PlanetMotor`;
  full focused PlayMode and profiler evidence for their combined scene remains incomplete.
- Shipping persistence, production networking, controller/accessibility coverage, external-player
  comprehension, and release operations remain incomplete or unproven.

## Technology trade-offs and limits

| Choice | Benefit | Limit / required fallback |
|---|---|---|
| Unity 6 + URP + PhysX | Mature editor, authoring, Humanoid, rendering, and rigidbody integration | PhysX outcomes are tolerance-deterministic, not bitwise cross-platform replay; preserve commands/provenance and reconcile instead of serializing scene objects. |
| Analytic SDF + ordered edits | Compact authoritative terrain and replayable changes | Meshing/collision are asynchronous caches; startup/edit queues can hitch if budgets or stale-result rejection regress. |
| Authored 3D prefracture | Stable topology, art direction, provenance, predictable limits | Requires Blender/import validation and many precreated shells; active Rigidbody/piece budgets remain hard limits. |
| Fixed arrays and prewarmed pools | Low-GC, bounded behavior | Capacity exhaustion must reject loudly or degrade by profile; it cannot silently drop canonical matter/impact. |
| Animator + procedural IK + visible ragdoll | Readable authored motion with physical KO | Update-clock and ownership handoffs are fragile; motor, Animator, IK, and PhysX need explicit interruption/reset tests. |
| One-pass screen-space atmosphere and semantic DOF | Predictable cost and readable capability fallbacks | Not physical volumetrics; target-device GPU evidence and dual-subject/camera-motion validation remain necessary. |
| Git MiniBokeh and prerelease/AI packages | Fast access to specialized tooling/experiments | Pin, license, platform, build-size, and maintenance risk must be reviewed; none may become an unwrapped gameplay dependency. |
| Transport-independent network spike | Tests authority payloads without early package lock-in | Not a shipping online implementation; disconnect, relay, NAT, security, service failure, and real multi-machine performance are unknown. |
| Voxel codec + in-memory replay | Demonstrates versioned terrain and ordered command contracts | Does not yet provide atomic full-game saves, backup/recovery, durable replay files, or cloud conflict policy. |

## Performance and test evidence

The live pass/fail table is in the [execution tracker](PROJECT_EXECUTION_TRACKER.md). The strongest
performance artifact currently present is `BuildReports/Mvp01Profiler.json`, captured
2026-08-31 05:06 UTC in a 1920x1080 D3D11 standalone run on an RTX 4070. Across 720 samples it
reports CPU/total p95 `8.335 ms`, zero measured steady-state GC, `AcquireSolid` peak `2.2064 ms`,
fracture preparation peak `0.4800 ms`, and a passing render audit. GPU timing was unavailable and
explicitly waived; this is not a GPU-budget proof. It also predates the snapshot commit.

The later editor diagnostic `BuildReports/Mvp01ProfilerEditorDiagnosticLatest.json` reports total
p95 `19.1631 ms`, GPU p95 `10.0344 ms`, and fails CPU, foot-contact, pipeline/camera, and overall
gates. It is non-authoritative editor evidence but cannot be cited as a pass.

`BuildReports/NativeWindows.json` reports a successful Windows build but also 186 warnings. That
fails the repository's zero-warning acceptance rule. No current-HEAD performance, warning-free
build, or complete PlayMode pass is claimed here.

Fresh evidence produced through the connected Unity Test Runner from the tested implementation is:

- `BuildReports/FoundationWorkingTreeEdit-20260831.xml`: full EditMode `586/587`; only
  `FinalBuildEvidenceAndCapabilityMatrixAreGreen` fails because the older Native Windows report
  contains 186 warnings.
- `BuildReports/FoundationWorkingTreePlay-20260831.xml`: targeted foundation PlayMode `2/2`,
  covering the rebuilt helmet/tail/belt configuration and rejection of closer dynamic debris as
  character support.

### Runtime animation/camera rescue evidence (2026-09-03)

The connected Unity Game view now reports both player and `Rumble Linebreaker Bot` on the baked
local-space EAMM path (`Active`, `ready-baked-local-space`) with upright visible-pose guards passing.
The same live audit reports camera wiring valid at `7.650 m / 60.00 degrees`, dual-subject DOF active
with a `6.34..13.00 m` sharp envelope, and radial gravity operational for both actors at about
`2.46 m/s^2` with Unity gravity intentionally disabled. The captured frame is
`BuildReports/RuntimeRescue/GameView_RuntimeRescue_Final.png`; the Unity Console had zero errors or
warnings after capture.

`BuildReports/AnimationTransitionsVNextEdit.json` passes `43/43`. The fresh
`BuildReports/EarthMagicExpansionPlay.json` passes `16/17`; the remaining blocker is
`PlatformDrawnUnderPlayerCarriesWithoutFractureOrRagdoll`, where the first post-release pillar-jump
vertical speed is `0.416 m/s` against a `>0.5 m/s` gate. No complete 30/60/120 FPS matrix or fresh
720-frame performance acceptance is claimed.

### Landing-amplitude follow-up (2026-09-03, uncommitted)

Working branch: `codex/environment-aware-motion-matching-spike`, base HEAD
`d2174eded114dd022e4a9c442abadda7a0e44555`; this note describes the dirty worktree,
not the historical tested snapshot above.

**Fact:** the ordinary `Land` and `Hard Land` states both use `Hard Landing.fbx`.
Changing only the landing style therefore did not reduce the soft landing's pose amplitude.
`EarthLandingPoseStrength` now maps observed airborne drop and impact speed to a normalized
pose weight; `HumanoidCharacterPresentation` tracks that evidence and passes the weight through
`EarthAnimationDriver` into `EarthAnimationGraph`. The graph blends the authored landing with
grounded locomotion before inertialization; clip playback speed and gameplay gravity are unchanged.
Initial support acquisition is not a jump/landing episode.

**Observed before this patch:** connected-Editor diagnostics showed operational radial gravity
(about `13.8 m/s^2`), grounded motors, animated ownership and zero dynamic visible-ragdoll bodies.
This supersedes the earlier `2.46 m/s^2` runtime snapshot for this session, but does not prove
the visual jump/landing issue resolved.

**Player feedback:** short jumping was subsequently confirmed fixed; startup falling and
occasional low-height rolls remained reproducible to the user.
No test suites, camera edits or scene rebuild were requested for this follow-up.
**Measured:** after `Assets/Refresh` through Unity MCP, compilation/import was idle and the
Console returned zero errors/warnings. Play mode was not started for this check.

**Follow-up implementation:** `PlanetMotor.Start` queries the existing classified support
before the first rendered animation frame, without movement, force or input consumption.
Editor ray diagnostics found both authored capsules only `0.055 m` above the static arena floor.
`EarthHardLandingRagdollBridge` now ignores initial support acquisition regardless of load
duration. Ordinary fall-only recovery stays in the landing animation instead of forcing a
physical knockdown and a second get-up roll. The catastrophic-fall KO gate (at least `5.5 m`
and `11 m/s` downward) and separate combat knockdowns remain unchanged.
Motor-only actors now freeze physics-driven capsule rotation while the motor owns orientation,
restoring the previous rotation constraints on disable; the player puppet keeps its existing
ownership. This addresses the authored bot's previously unconstrained root without freezing
its separate visible ragdoll bones.

The earlier roll revision required observed prior support plus at least `0.10 s` airborne, and one of:
measured drop `>2 m`, deliberate jump with at least `6.5 m/s` forward/backward takeoff speed,
or at least `4 m/s` external airborne velocity change after subtracting ordinary gravity.
Eligible rolls use full pose weight; small ordinary landings retain amplitude blending.
That revision selected a reversed authored clip with a forward-running state clock for backward travel.
These follow-up changes still require user visual verification; the earlier clean compile
is not acceptance evidence for them.

**Follow-up verification:** connected Unity compiled the new policy and authoring code with
no `error CS` entries. After the user stopped Play, the reverse asset was generated through
`EarthHumanoidMotionSetup.UpgradeController`: `humanMotion=true`, `138` float curves,
duration `0.7043334 s`; `Moving Land Back` references it with speed `1` and two exit transitions.
The user had entered Play during script reload; that interrupted session emitted
`MotionMatchingController` cache `NullReferenceException`s. No clean new Play run or test suite
was started by the agent, so runtime acceptance is still open. Source/doc whitespace checks
passed; the Unity-written controller retains the serializer's empty-value trailing spaces.

### Startup-pose and excessive-roll correction (2026-09-03, uncommitted)

The user subsequently rejected the startup behavior and synthetic backward roll. Two explicitly
armed five-second scene observations were run through Unity MCP, with no Test Runner, simulated
input, camera edit or scene rebuild. The Editor-only `EarthCharacterStartupProbe` records motor,
action, impact, ragdoll and visible hip orientation without controlling the game.

**Reproduced cause:** `BuildReports/RuntimeRescue/CharacterStartupProbe-Before.log` shows both
fighters grounded with zero hits and no ragdoll, yet hips-up projection is -0.25 when EAMM first
becomes Active. The bot repeats the fold after its first cast; the old post-output sanity guard
allowed three bad frames before rejecting the pose. This is a retargeting defect, not evidence
of stones hitting the characters or gravity turning off.

**Changed:** `EAMMBasePoseBridge` predicts candidate head/foot positions in cached target hierarchy
matrices before writing graph targets or enabling weight. Invalid candidates immediately fall
back to authored motion. No visible transform writes, clone skeleton or per-frame allocations
are used by this preflight. Original EAMM calibration is still invalid and is not claimed fixed.

`EarthLandingRollPolicy.FastJumpSpeed` is now 9 m/s: the previous 6.5 gate was below normal
7.2 m/s running speed and admitted ordinary running hops. The >2 m drop and >=4 m/s external
airborne delta-speed exceptions remain; first takeoff sampling is excluded. Backward travel
temporarily cannot select a roll until a genuine backward clip is supplied. The authoring setup
and regenerated controller use ordinary `Hard Landing` for `Moving Land Back`, with existing
height-scaled landing blending; the rejected synthetic asset is preserved but unused by that
state. `EarthCharacterImpactTarget` filters classified static support contacts so floor contact
does not enter the combat impulse path. This latter issue was found in code, not proven as the
startup cause. Real projectile/debris/wall impacts and catastrophic KO remain separate.

**Observed result:** new `BuildReports/RuntimeRescue/CharacterStartupProbe.log` has 87 samples
through 5.064 seconds; hips-up projection stays 0.61–0.82. Both start at 0.69 and the bot remains
upright after its first cast. EAMM reports `candidate-head-below-upright-envelope` instead of
presenting the bad pose. At 4.438 seconds two accepted player hits cause actual combat recovery;
that is not a startup pose regression. Console reports zero errors after the run. The diagnostic
session was stopped. Small/fast/backward jump visuals and movement across surfaces have not
been accepted by the user after this patch. No test suites were run for this correction.

`Docs/ANIMATION_INVENTORY.md` records imported Mixamo/KayKit clip names verified through the
connected Editor, missing desired actions and current download settings.

## Important entry points

| Need | Entry point |
|---|---|
| Open representative scene | `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity` |
| Rebuild generated Earth slice | Menu `Elemental/Setup/Create M3 Earth Core Slice`; `M3EarthCoreSetup.Configure()` |
| Full local EditMode/PlayMode | `Scripts/Test-Unity.ps1` with `UNITY_EDITOR_PATH` or `-UnityPath` |
| Focused MVP gates | `Assets/Elemental/Tests/EditMode/Mvp01FocusedTestLauncher.cs` menus |
| Accepted evidence/captures | `Assets/Elemental/Authoring/Editor/Mvp01EvidenceRunner.cs` and `Mvp01SceneCapture.cs` |
| Windows/macOS/Web builds | `Assets/Elemental/Authoring/Editor/ElementalBuildPipeline.cs` |
| Project validation | Menu `Elemental/Diagnostics/Validate Project`; `ElementalProjectValidator.cs` |
| Performance budgets | [`Docs/performance-budgets.md`](performance-budgets.md) |
| Third-party inventory | [`Docs/THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md), `Packages/manifest.json` |

## Known unknowns

### Mobility correction — 2026-09-04 (working tree on `d2174ed`)

- Supersedes the early roll acceptance below: the stronger test found the live
  Moving Land exit at 0.4145 with a 0.5594-second blend skipped the actual tumble.
  That transition alone is now 0.92 / 0.18 seconds; presentation protects the
  current and incoming roll through its outgoing blend. Other authored states,
  clips, Blend Tree entries and user material/arena settings were not rebuilt.
- Roll travel defaults are now 1.4 seconds / 7.5–9.5 m/s. Cast brace no longer
  cancels the motor travel while presentation is still protecting the roll.
- Narrow, idempotent `Repair Surf And Launch Pillar Bindings` restored missing
  surf profile/material/planet and launch feedback references in the existing
  scene. Serialized launch chip buffers are recreated on enable.
- Wave ground queries start only 0.45 m above the candidate support and use a
  thin ray (the former sphere overlap could report a zero-position hit). The
  production gate-floor query now returns the floor rather than the arch roof.
- User-requested `EarthPillarWaveProfile.FoundationBurialRatio = 0.20` lowers
  both physical and visual wave columns by 20% of their mesh height along local
  surface up. Applies to polygon/legacy columns on arena and planet. Change to
  0.15 for 15%; the profile asset was saved through Unity, not scene YAML edits.
- Evidence: `LandingRollMotionEdit.json`, UTC `2026-09-03T22:15:02.1808454Z`,
  **21/21**. Includes three orientations for the 20% foundation test.
  `IdleFootOrientationPlay.json`, UTC `2026-09-03T22:16:03.6356957Z`, **3/4**:
  idle, stop and production mobility passed. Roll plays through phase 0.901/0.895
  with torso-up -0.562/-0.567 and travels 2.458/1.786 m (player/bot), but the bot
  fails the requested test's 2 m minimum. Its ground acceleration is 18 versus
  player 48; ignoring actor collisions did not change the result. Do not report
  the combined regression green. Further bot-distance tuning remains open.
- `BuildReports/MobilityVisuals/LaunchPillar.png` and `SurfMaterial.png` show
  the restored pillar and sandstone board. These are not a full wave-motion
  visual gate. No fresh 720-frame/performance gate; pre-existing EarthArmorPiece
  required-component warnings still occur during EditMode scene hooks.

### Landing-roll travel follow-up — 2026-09-03

- **Fact (working tree on `d2174ed`):** `EarthLandingRollMotion` collects fixed-tick
  landing evidence using the existing roll eligibility policy. `PlanetMotor` owns
  confirmed roll travel and exposes the decision to presentation. No animation event,
  root-motion transform write, scene rebuild or clip replacement is involved.
- The motor substitutes a forward quadratic-decay velocity target for ordinary
  input braking during the short roll window. Normal acceleration limits, support
  velocity, radial gravity and PhysX collisions remain active. Jump, lost support,
  surf, casting/brace and physical knockdown interrupt the travel.
- Tuning: `PlanetMotorFeelProfile` / **Landing roll travel**: 0.72 seconds,
  minimum 4.5 m/s and maximum 7.2 m/s. These are desired speeds, not guaranteed
  displacement through obstacles; the ideal minimum-speed budget is 1.08 m.
- **Measured:** `LandingRollMotionEdit.json` at `2026-09-03T21:30:22.5616566Z`
  passes 11/11 (30/60/120 Hz decay integration, eligibility, once-only start,
  interruption). `LandingRollMotionPlay.json` at `2026-09-03T21:30:59.3446339Z`
  passes 1/1 in the production scene: both actors actually enter the authored roll,
  travel forward 0.651/0.459 m after a 3.5 m drop and settle below 0.001 m/s.
- **Regression:** the combined `IdleFootOrientationPlay.json` run at
  `2026-09-03T21:32:59.3335687Z` passes 3/3 in 22.345 s, retaining idle orientation
  and forward/back stop checks. Game screenshots are in `BuildReports/LandingRollMotion/`;
  the opponent is partially occluded by the player, so these stills alone are not
  a full visual roll-motion gate. EditMode still emits the pre-existing armor
  required-component warnings documented below; no new compiler warning was observed.
- **Unknown:** comprehensive obstacle/slope/platform roll and performance matrices
  remain unmeasured. This narrow change does not close the EAMM acceptance gates.

### Stop / support-release target backlog correction — 2026-09-03

- **Fact (working tree on `d2174ed`):** the free-foot filter previously capped
  absolute support-local motion at 1.5 m/s. It now filters the surface correction
  relative to the authored foot reference. A locked anchor remains support-local;
  lock ownership changes and invalid contacts reset filter history. Thus authored
  locomotion is not subject to the 2.5 cm/60-Hz contact-correction speed cap.
- **Fact:** `EarthFootContactController.LateUpdate` is now telemetry-only. The
  post-IK ankle-position and root-relative rotation clamps were removed: changing
  only the ankle after the Humanoid solve could stretch the shin and retain a
  stale pose after release. Target/normal and weight filtering remain before IK.
- **Measured:** seven new regressions failed before this fix: forward/backward
  6 m/s travel accumulated 4.30/4.41/4.47 m of target lag at 30/60/120 Hz; loss of
  support retained a target 1.975 m from the authored fallback. After correction,
  `BuildReports/AnimationTransitionsVNextEdit.json` at
  `2026-09-03T21:16:41.3800060Z` passes 50/50. The suite also emitted existing
  `EarthArmorPiece` missing-required-component warnings before and after the fix.
- **Measured runtime:** `BuildReports/IdleFootOrientationPlay.json` at
  `2026-09-03T21:20:53.3573992Z` passes 2/2. Both fighters move before forward/back
  stops (measured speed >0.2 m/s), then sample for 60 frames per direction. Peak
  free-target tangential lag is 0.0047 m backward / 0.0050 m forward. The test
  bounds ankle translation by the Avatar's allowed leg-stretch budget; the first
  test version incorrectly treated all Humanoid length adjustment as an error.
  Idle toe orientation remains passing at 30/60/120 caps. Actual platform-edge
  traversal and the broad performance/contact corpus are not covered by this run;
  support loss is covered by the pure regression above.
- This supersedes the unchanged-final-smoothing statement in the earlier idle
  orientation entry below. User clips, Blend Tree positions, and profile tuning
  are not changed by this correction.

### Earlier idle foot IK orientation correction — 2026-09-03

- **Fact (uncommitted working tree on `d2174ed`):** `EarthFootContactController`
  passed a skeleton foot-bone rotation to a Humanoid IK goal. These bases differ
  on Linebreaker. The adapter now reads `Animator.GetIKRotation(goal)` before
  applying the support-normal delta; authored clips and user Blend Tree entries
  are unchanged. Contact position, weight, and final smoothing policy are unchanged.
- **Measured:** live on/off/on A/B on both fighters reproduced upward toe
  directions of 0.97–0.99 along local up with the old controller, versus forward
  feet without it. `BuildReports/IdleFootOrientationPlay.json`, UTC
  `2026-09-03T21:09:52.6993029Z`, passes 1/1 after the correction. The regression
  repeats idle contact ramps at target frame-rate caps 30/60/120, checks 60 samples
  per cap and both feet of both actors with IK weight >0.8; peak toe-up dot was
  -0.0638/-0.0639/-0.0639. Caps are not proof of achieved frame pacing.
- **Scope:** this closes the reproduced idle toe inversion only. EAMM still
  reported `PoseRejected` in the live diagnostic; complete movement/slope/recovery
  and performance acceptance remain separate. No scene or motion-library rebuild.

- Which focused and repository-wide gates pass after rebuilding the scene at snapshot HEAD.
- Whether the new classified support policy will be wired into runtime support selection, and
  which existing owner it will replace or constrain.
- Current-head GPU p95/frame pacing on the reference PC and on minimum target hardware.
- Whether the 55.1 m world and current Broken Crown placement are accepted across every physical
  input, KO/respawn, fracture/repair, surf, camera, and visual scenario.
- Player evidence: no fresh external playtest proves comprehension, feel, dominant strategies,
  or that the repeatable duel is enjoyable without developer explanation.
- Whole-game persistence/recovery, production online transport, accessibility/controller matrix,
  supported hardware floor, store/business model, and release window.
- Whether all present third-party/AI packages and generated/imported assets pass final licensing,
  provenance, build-size, and platform review.

## Local alpha implementation checkpoint — 2026-09-06

Working tree remains on main at 1235579; no stage commit yet. Saved production
scene now includes local physical hit response, common editable mass policy,
repaired boulders/wall bonds, current-controller EAMM metadata and uGUI/TMP frontend.
Precise settings and current evidence: [ALPHA_SETTINGS_AND_ACCEPTANCE.md](ALPHA_SETTINGS_AND_ACCEPTANCE.md).

Regional actual-contact + performance acceptance passed 2/2 (16:19:49 UTC),
local hit/ragdoll Play passed 3/3, mass Edit/Play passed 7/3 cases, four wall/boulder
Play cases passed, frontend passed at all three target aspect ratios. The latest
cadence run has both feet accepted, player ten-cycle speed in tolerance; bot speed
still fails (~0.9465 actual/requested), so locomotion and Stage 1 remain incomplete.
A Windows local build succeeded (464 MB, zero errors); final incremental rebuild
and standalone smoke remain pending. Computer Use app authorization timed out
before standalone launch. A separate baseline checkpoint permission is pending
because substantial pre-existing uncommitted user work overlaps required files.

Stage 2 source remains outside Assets in Stage2Pending. Sessions/Relay/NGO code,
actor/world/terrain replication and explicit frontend/camera ownership are prepared;
SDK compilation, saved production integration and two actual processes have not
been accepted. Host/Join remain unavailable in the local alpha. No online completion
or fictitious room claims.

## Alpha steering checkpoint — 2026-09-06 17:08 UTC

Supersedes the earlier cadence-failure checkpoint: AlphaCadencePlay is now 2/2 PASS (16:51:11 UTC), player mean ratio 0.999438 and bot 0.999798 over ten cycles. Slope and visible turn-in-place remain open. AlphaFrontendPlay 1/1 PASS (17:08:19 UTC): 4–3–2–1, side/elevated framing with last 1.5-second gameplay blend, all-HUD Varose, local pause/resume/Settings/end-match. Profiles/scene saved. Wall bevel target 52.5 mm saved; latest sequential-repair/heavy-crush/Earth-body-filter requests in progress. Final build and Git commits pending; Stage 2 SDK and real-online acceptance remain pending. HEAD is still 1235579b36960e340211e52b81a17ae8dc5e32e2; no new commit claimed.

## Repair and actual large-stone contact checkpoint — 2026-09-06

Working tree, no new commit claimed. Initial repair/crush pure cases passed within
AlphaLatestEdit 26/26 at 17:29:44 UTC. The subsequent manual huge-piece report led
to a source-first character contact route for actual structural/decor fragments,
pre-rebound MagicExecutor direction, and explicit cancelled-quick-stone lifetime
retirement plus destroyed-kernel reference handling. The added actual large-cell
throws, falling-rock frame captures, sequential repair and lifecycle regressions
are awaiting the coordinator's Unity acceptance. Exact contracts, settings and
scope: [SEQUENTIAL_REPAIR_AND_HEAVY_CRUSH.md](SEQUENTIAL_REPAIR_AND_HEAVY_CRUSH.md).

## Countdown dolly checkpoint — 2026-09-06 18:15 UTC

Working tree only, HEAD unchanged. AlphaFrontendPlay 1/1 PASS at 18:15:09 UTC. Countdown starts at 150 mm, moves continuously inward for all four seconds, and aligns to gameplay over the last 1.5 s. Actual output lens, inward distance, visible alpha of 4/3/2/1, fighter viewport bounds, pointer pause and reentry are checked. Initial 17:08 text-only countdown acceptance missed inherited CanvasGroup visibility and is superseded. Fixed legacy camera FOV ownership and additive legacy-charge installer; inspected updated countdown screenshots with both fighters visible after foreground-occluder handling. Stage 1 remains open for repair, latest turn/slope validation, final build and commit; Stage 2 SDK/real-online acceptance remains pending.


### Sequential repair and real stone damage acceptance — 2026-09-06 18:37 UTC

Repair2 restored both arena and wall completion, held waiting material under one
repair owner, retained physical final wall seating, and kept detached ragdoll bone
contacts connected to the explicit health receiver. Destroyed-skeleton local
physics teardown and explicit rebind passed. Real large authored arena/wall pieces
at 25 m/s inflicted Bot HP loss (100→76 and 100→77.92); actual heavy falling sources
handed both fighters to eleven dynamic colliders with downward displacement.
The repeated physical source acceptance killed the bot in six contacts, HP100→0,
with one KO/point and detached-rig ownership throughout (AlphaRepairVisualPlay4/4,
18:30:59 UTC). The separate full MMB disassembly/reassembly capture passed1/1 at
18:37:48 UTC,40/40 cells in20.433 simulation seconds; inspected individual flight,
closed caps and restored wall extent. Video: BuildReports/SequentialRepair/Wall/
SequentialWallRepair.mp4. Cropped Preview footage excludes some initial airborne
starts and does not establish final gameplay colour grading. See
Docs/SEQUENTIAL_REPAIR_AND_HEAVY_CRUSH.md for exact evidence, limits and settings.


2026-09-07 15:12 UTC visual follow-up (shared main, original baseline HEAD1235579): actual menu alignment/footer selected segment and result styling imported. Main/Victory/Defeat/Draw screenshots refreshed in BuildReports/StoneSkin. UI Play4/4 passed46.05s after fixing culled result darkening and 28px caption sizing. Earlier geometry20/20, currentvisualcontracts10/10, winddustEdit5/5+Play1/1. Exact painted cracked defeat emblem/reference flame trails and fresh standalone GPU/network acceptance remain outstanding; no full visual-identity or performance claim. Neighbor status message rejected by automatic approval review; not sent.


2026-09-07 16:43 UTC continuation (shared main, baseline HEAD1235579): SidebarIconEffects, actual centered RectTransform pivots for preserveAspect sprites, card y154 / symbolx65, staircase y/3 and footer opaque diamond OVER selected segment are actual. Original root button sprite is cleared while retaining transparent raycast surface; passive-shaped rim/glints are suppressed when cream selected artwork is active. User rejected sparse contour bloom artifacts; dense Gaussian soft field replaced rings/core-subtraction. Actual StoneSkin Play4/4 passed16:38:57UTC46.302s; fresh Main/Pause/Settings/Host/Join/Combat/results frames in BuildReports/StoneSkin. Actual pointer raycast hit PLAY VS BOT and dispatched click -> Combat/HasEnteredCombattrue, menu CanvasGroupalpha0 observed; no claim about every physical-device focus condition.
IslandPerpendicularOverhead adds12 actual renderers (55generated,0rejected,worldUpDot1), saved and verified original9983scene records preserved. Actual East/West/Overhead screenshots in BuildReports/IslandPerpendicularOverhead. UIcard/icon geometry reviewed on freshMain. CloudFluffCompanions10analyticbanks+16imageclouds, ColumnFireArtFollowup, groundmeshwindwisps imported. NewDustPlay1/1passed16:40:33UTC21.911s BUT visually weakgroundwisps remain under dedicateddiagnosis; functional pass is notartacceptance. User now reports ordinaryimpactdustregression and requests coherent sunset/nightcloud+lowerfogtints; those are ACTIVE work. Geometry rerun didnotstart whilePlay; prior20/20 remains latest. No fresh standaloneGPU/network acceptance. SourceChatGPT read-onlycheck unchanged updatedAt1788765643.181653.

2026-09-07 17:30Z checkpoint: root owns Unity, but MCP relay disconnected and shut down. No active editor restart or alternate control attempted. Latest actual changes/tests/pending checks: Tools/FireIntegration/Reports/SoftStoneAndCinematicAtmosphere.md. Second ArenaFractureShadingEdit run has no result; do not repeat while unknown. First run14/16 stale expectations now updated. WindPlay1/1 fresh17:12, VisualContracts15/15, Valley20/20. Latest fog/fire/art revisions require fresh captures after reconnect; assets saved, preservationPASS.

2026-09-07 fine halftone follow-up (user reference9f4686b0): actual ValleyAtmosphereV2.hlsl now uses a4px-at1080p staggered grid instead of13.5px random dots. Dot diameter varies continuously .56–2.32px with pre-fog lit-source luminance (smaller in light, larger in shade). Removed arbitrary screen sine and random positive/negative polarity. Low-contrast multiplicative tone retains scene hue; far-depth/near/UI guards unchanged. No new texture samples or render passes. Still before DOF; final visibility and shader compilation require restored Unity MCP and are not claimed verified.

2026-09-07 face-oriented halftone follow-up: rows now rotate with the camera projection of a face tangent derived from reconstructed world-position derivatives. Horizontal faces use a forward tangent fallback; derivatives evaluated before divergent masks. Fine4px grid retained. Bright lit areas gain pale tiny dots while shadows retain tonal dark ink. Far chromatic shift strengthened3.9→5.85px at1080p (cap6), positive-edge mix .72→.95; near/planet/depth guards retained. No extra texture reads or passes. Source applied; Unity shader/runtime verification still pending restored MCP. Grid phase remains screen-space; face orientation follows geometry, not UVs.

**September 8 blue fog / separate-agent research (working tree main 1235579):** saved stronger blue day palette in profile/defaults/installer; added 1800–3200m protected distance closure and attenuated post-fog rock artwork by transmittance. Actual ValleyAtmosphereMath.cs compiled with Add-Type; 12001 distance samples pass monotonicity/exact plateaus, three distant heights become opaque, reverse-view playable planet stays protected. Eight Unity EditMode regression cases added; Unity import timed out after successful initial editor status, focused BlueFogClosureEdit result absent at checkpoint. No fresh shader/PlayMode/capture/GPU acceptance; do not count requested test run as passed or rerun blindly while status unknown. Animation and material/dust recommendations are research, not integrated features. [Research, patch and limits](ANIMATION_MATERIAL_FOG_RESEARCH_2026_09_08.md).
