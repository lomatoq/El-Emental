# El-Emental project execution tracker

**September 8 no-spring wall correction (base main cb7538ad):** Removed the penetration-to-upward-force spring. Intact roots reject outward bounce while retaining bounded climb along sampled slopes; root depenetration speed capped at0.5m/s. Authored-frame root path also removes outward velocity; detached debris physics unchanged. Full-charge wall starts8cm buried: zero above-floor gap and zero airborne samples over160 fixed steps. Tap/full travel remains4.94/10.24m; ramp6.64m, peak gap5.27cm. Edit **137/137** at21:49:28Z; final Play **10/10** at21:57:04Z (139.89s), production held-push marker peak0.0853ms. [Evidence and scope](WALL_NO_SPRING_2026_09_08.md).

**September 8 grounded wall / local heavy-contact / shared microshake (base main 46b725f5):** Emergence and cracking emit denser dust/chips. Full charge is 1.8x impulse (was 2.5x), with anticipatory support and bounded downward damping. Tap/full-charge travel 4.94/10.24 m; shallow ramp 6.64 m, peak gap 5.27 cm. Heavy frontal contact shifts three linked domains at most 2.5 cm, preserving mass and bonds. Shared render-only microshake covers material abilities, sliding and heavy falls with distance/accessibility gating, explicitly wired to both production cameras. Edit **136/136**; seven broad Play cases pass at21:34:57Z, remaining camera/keyboard cases pass **2/2** at21:40:44Z after fixture corrections; all nine selected cases have passing evidence. Production push marker peak **0.0882 ms**. [Implementation and evidence](WALL_GROUNDING_EMERGENCE_2026_09_08.md), [persistent camera rule](MATERIAL_MICRO_SHAKE_2026_09_08.md).

**September 8 wall charge/feedback and loose-stone contact follow-up (base main eb29e7a7):** Wall push now feeds the existing charge camera envelope. Release has ground-probed burst(s), charged travel has denser/wider dust and chips, including moving fractured children near support. Small outgoing stone contacts use pre-solver wall position/velocity instead of self-damaging the wall; normal collisions and heavy-projectile damage remain. Final Edit **125/125** at 2026-09-08T20:34:51Z; Play **6/6** at 2026-09-08T20:47:56Z (82.58 s). Full charge release produced 1493 dust versus175 on tap in the budgeted fixture; trail10400 versus935. Actual two adjacent66.76kg stones travel11.85m while the1668.88kg wall advances9.90m, stays intact and records two classified contacts; subsequent heavy incoming damage passes. Production keyboard marker peak0.0822ms. Charge screenshot inspected; material/menu assets unchanged. [Details, limits and evidence](WALL_PUSH_FEEDBACK_2026_09_08.md).

**September 8 wall shove strength/repeat follow-up (base main 05cfbb6a):** Ordinary heavy-wall click now travels 4.94 m versus the previous 1.43 m; full charge travels 22.27 m on the same unobstructed physical floor. Second/third clicks travel 5.41–5.52 m after settling. Surviving cohesive damaged walls accept repeated charges through their real child bodies, without healing interior damage or reviving the retired shell. Leading-edge physical support follows shallow slopes; actual arena input advances 2.49 m before a measured solid obstacle, with floor penetration/jitter/limited collision recoil assertions passing. Final Edit **99/99** at 2026-09-08T16:09:20Z, Play **4/4** at 2026-09-08T16:19:37Z (55.81 s). Held-push marker peak 0.0615 ms in that fixture. User layout/material assets unchanged. This supersedes older impulse/range figures below. [Implementation and evidence](WALL_PUSH_REPEAT_2026_09_08.md).

**September 8 final wall charge/release clarification (base main `56e7d9e1`, same authorized main snapshot):** Ctrl+RMB click launches an ordinary shove on mouse release; holding charges a stationary wall, then RMB release while Ctrl stays down launches the stronger shove. Full charge1s, impulse×2.5, bounded released speed14..28m/s. Ctrl-first release/pause/stun cancels without launch. This supersedes continuous held drive. Final **Edit99/99** at `2026-09-08T15:40:19.5359569Z`; **WallPushPowerPlay3/3** at `2026-09-08T15:41:47.5587137Z`. Actual same-floor/same1668.881kg wall travels1.4347m on click versus7.2525m charged release; the wall stays still while charging. Real paired mouse release fires once, retains physical obstacle response, and preserves mass. Final held-push marker peak0.0767ms. Together with the earlier independent11 passing cases and final repair2/2 below, all14 distinct selected runtime scenarios have passing evidence across focused runs. [Final controls and measured limits](WALL_PUSH_TAP_HOLD_2026_09_08.md).
**September 8 earth interaction fixes (base main `56e7d9e1`, user-authorized main push):** Held LMB+RMB rows now allocate free pool capacity while earlier rows remain anchored. Airborne held Shift+Space commits one physical crater/ejecta/radial wave on arena FloorBase or planet; default2m drop/7.5m/s thresholds and a saved LandingSlam profile are bound to both actors. Walls rebuild fracture partitions in physical dimensions, begin with a seamless exterior and reveal bevel seams on first interaction. Ctrl+RMB latches one occlusion-tested wall and applies finite mass-dependent impulse; PhysX owns translation, support probes follow real ground, release preserves inertia and dust/chips emit near actual contact. Clockwise held-MMB recognizes rim-start circles and completes actual repaired seams after all fragments seat. UI movement/hover/press audio starts concurrently, preserving original source assets and music envelopes.

Final **Edit95/95** at `2026-09-08T15:19:17.3229473Z`; broad **Play11/13** at15:14:20 followed by corrected wall/repair **Play2/2** at `2026-09-08T15:20:37.7206176Z`. All13 distinct selected runtime cases have passing evidence, not a claimed single13/13 run. Real wall:1.304m forward,3.74mm side,14.83mm maximum floor penetration,895kg unchanged,0.0613ms held-push peak. Real MMB rebuilds exactly once. Grounded wall screenshot inspected, final console0 errors/0 warnings. Cold dimension-build Acquire19.045ms and synchronous slam peaks11.914–37.367ms floor/28.160ms planet remain one-off spike risks; no standalone build, online pair or GPU timing claimed. Updated semantic input bit2048 requires matching peers. User menu layouts/material settings untouched. [Wall motion](WALL_HELD_PUSH_STABILITY_2026_09_08.md), [wall geometry](WALL_GEOMETRY_PUSH_2026_09_08.md), [repair](CLOCKWISE_WALL_REPAIR_2026_09_08.md), [rows](PILLAR_ROW_INPUT_2026_09_08.md), [slam](LANDING_SLAM_2026_09_08.md), [audio onset](FRONTEND_AUDIO_ONSET_2026_09_08.md).
**September 8 frontend lifecycle/audio/camera follow-up (base main `2f0fbe86`, user-authorized main push):** Main and whole-match rematch restore the arena and authored day; local Main/settings/countdown/results freeze world physics while Update-based geometry restoration can finish. Ordinary life loss still preserves arena damage. Actual HUD Rematch reopens readiness; repeated closed gates re-disable controls without losing original flags. Platform drawing now has a readable cream contour and size-rejection feedback; real mouse input creates exactly one platform. The three supplied MP3s are bound through FrontendAudio.asset with quiet sidebar movement, smooth context changes and DSP-scheduled overlapping loops. Main and Pause use an unscaled character camera, restore gameplay policies on return, and follow actor relocation. Latest user-marked composition is saved at viewport(.78,.58), compensated for roll/aspect. User menu layouts and shared preview material unchanged. Final Edit **29/29** at `2026-09-08T13:15:16.9271438Z`; combined Play **10/11** at13:09:56 had only a camera fixture waiting0.8s against the authored0.85s transition. Corrected timing and latest portrait/audio Play **3/3** at `2026-09-08T13:16:24.6880489Z`; all11 distinct selected runtime cases have passing evidence. Main/Pause/restored screenshots inspected; zero console errors on final save. No new standalone build/online pair or full-length original-music seam listening claimed. [Lifecycle](MATCH_LIFECYCLE_RESET_2026_09_08.md), [input](PLATFORM_INPUT_REPAIR_2026_09_08.md), [audio/tuning](FRONTEND_AUDIO_2026_09_08.md), [camera/framing](MENU_CAMERA_CLOCK_REPAIR_2026_09_08.md).

**September 8 current-main snapshot, explicitly requested by user:** captures the accumulated project working state over `1235579`, including user-authored settings. Latest stone correction separates admitted health damage from flinch, restores armor attribution to detached ragdolls, and measures sustained pinning independently of recovery-timer blocking. Stone Edit **14/14**, final Play **5/5** at `2026-09-08T12:23:02.2237136Z`; real pile kills once, removal stops damage. Pin marker mean0.0281ms/peak0.2293ms in this focused fixture. [Physics evidence](STONE_LETHALITY_FOLLOWUP_2026_09_08.md). Debris closing momentum corrected; production/authoring pools enlarged72→256 with finite-budget and mass safeguards; fracture Edit **2/2**, Play **1/1** at `2026-09-08T12:23:57.1924010Z`. [Fracture details](DECOR_FRACTURE_FOLLOWUP_2026_09_08.md). UI final evidence below. Historical online/build/GPU limitations are not newly tested by this snapshot. Local backup archive and npm cache stay outside Git.

**September 8 notification correction (working tree main `1235579`, pending user-authorized snapshot):** WIN/LOSS/DRAW all use the existing light plate and dark text, uniformly reduced 12%. Returning is now centered display-font text without a sprite, over a 48% dark scene veil. Complete sprite rendering preserves artwork proportions; actual layout centering prevents margin drift. Twelve user MenuLayouts assets unchanged. Edit **5/5**, final production Play **2/2** at `2026-09-08T12:02:00.9360617Z`, 27.6821s; final screenshot inspected. Earlier dark LOSS and Returning-plate descriptions are superseded. [Evidence](HUD_NOTIFICATION_PLATES_2026_09_08.md).

**September 8 procedural dust revision (uncommitted main `1235579`):** user rejected static curved dust domes. Replaced them with shallow asymmetric deforming wisps, independent widths/lengths/speeds/lift and breathing, stable per-birth shader phases, two-phase UV advection, interpolated velocity/orientation/support, and alpha-only retirement without lifetime/atlas jumps. Surface probes no longer pull wisps onto abrupt obstacle tops. Removed hard-coded ordinary-dust tint; RumbleDustLit now drives live Tint/alpha/Brightness/night fill for mixed soft/impact/fracture layers. User's WallcoeurGroundDust.mat remains byte-identical. Edit **2/2** at `2026-09-08T11:18:55.7564446Z`; final Play **3/3** at `2026-09-08T11:23:49.3034901Z`, 24.4971s. Production sample: 352 wisps, speeds .376–2.741m/s, widths .431–4.377m, 42541 changed pixels; whole-adapter CPU .9973ms mean /1.6353ms peak, GPU unmeasured. Shader errors=false; no material assets edited. [Contract, superseded approach and evidence](CURVED_SURFACE_DUST_2026_09_08.md).

**September 8 bot-start transition repair (uncommitted main `1235579`):** replaced the short sidebar drift plus fade with a complete opaque 0.54s departure. A brief dark cover conceals countdown-camera reframing; the local countdown begins after the 0.82s intro. User layout/material settings unchanged. Reduced Motion uses 0.18s fade. `BotStartTransitionPlay` **3/3 passed** at `2026-09-08T10:55:46.7301500Z`, 59.6992s; captures verify moving opaque sidebar, camera reveal and countdown, plus subsequent match results. The initial regression measured the sidebar before stagger completion; its settle wait was corrected. [Details](BOT_START_TRANSITION_2026_09_08.md).

**September 8 dense dust/clouds and procedural UI (uncommitted main `1235579`):** sidebar panel-first reveal and staggered buttons/icons added, pause now presents pressed feedback before dispatch, Returning uses dark button art with reversible fade/scale, round and match results gain reveal/exit motion. Existing user layout ScriptableObjects and staircase remain intact. Ground dust profile is denser and larger, with seam wisps and soft alpha-only atlas interpolation/deformation; original contact dust mix retained. Fire gains bounded embers and 3px heat haze. Clouds: 22 banks, including 8 overhead, plus 52 image particles; soft proxy occlusion attenuates shafts. Edit **21/21** passed at `2026-09-08T10:34:37.6501386Z`; Play **11/11** passed at `2026-09-08T10:37:15.7131391Z` (95.0015s). Production dust: 286 alive, 113988 changed pixels, 431 gap births; CPU adapter mean **1.175ms**, peak **4.2314ms** (increased from prior .234ms; GPU transparency/raymarch cost unmeasured). UI layout-only mean .1871ms. Fire allocation sample 0 bytes/128 ticks. Final modified shader scan found no errors; EarthCoreSlice restored nonplaying/clean. Motion-vector atlas interpolation and exact volumetric cloud shadows are not implemented. [Research, settings, evidence and limits](DENSE_DUST_CLOUD_FIRE_RESEARCH_2026_09_08.md).

**September 8 requested rollback:** removed the recent whole-button-group settings after the user observed jumping coordinates. Original page/button hierarchy and staircase restored. Whole-group scaling is no longer an accepted feature; dust and readable Inspector labels retained.

**September 8 dust/UI authoring follow-up:** fire-atlas dust with faster playback, original soft mixing, dark-readable night lighting and stone wake curls implemented. Russian block names and per-page button-group controls installed; user's original staircase restored and verified at full and 70% scale. Edit **14/14**, combined Play **6/6**, final menu Play **4/4** passed. [Current evidence and limits](DUST_FLOW_AND_MENU_NAMES_2026_09_08.md).

**September 8 menu layout follow-through (uncommitted main `1235579`):** applied authored-button layering/spacing fixes and visible local chromatic/glow effects; installed independent editable profiles for all 11 current menu/result screens. Actual scene captures cover five menu pages, Victory/Defeat/Draw and live layout editing. Focused Edit 3/3 and Play 4/4 passed, with animation/input continuity included. [Contracts, captures, profiler scope and remaining native warning](MENU_LAYOUTS_AND_RESULT_POLISH_2026_09_08.md).

**September 8 animation/UI/blue fog follow-through:** implemented living torso hold and phase/cancel/outgoing-clock repairs, interrupted UI transition/held-press fixes, shared ground-wind gusts and actual far-depth fog closure. Edit **60/60**, Play **11/11** across focused suites; final00:23:41Z. Strict GPU pixel proof now has hidden-source difference0, hidden-depth difference0, near contrast preserved. Scene restored clean; saved blue profile and shader verified. Full-body hand-loop content and total GPU/performance acceptance remain outside these checks. [Exact contracts and reports](ANIMATION_MATERIAL_FOG_RESEARCH_2026_09_08.md).

**September 8 current effects follow-up:** completed mixed original/atlas dust, dark-edge correction, HDR flame core, animated deformation, separate depth-aware heat haze and expanded cloud distribution; localized AO preserved. Edit23/23, combined Play12/12, final production2/2 passed. Rendered heat contribution verified and exactly hidden in sonar. Unity recovered and current console has no errors; prior native Graphics Ring Buffer warning and isolated heat GPU cost remain limitations. [Exact evidence](WALLCOEUR_FIRE_DUST_AO_2026_09_08.md).

**September 8 visual/physics follow-up (working tree main `1235579`):** reproduced and corrected buried-floor support selection, mirrored foot-channel mapping, pivot-anchor monopoly and ragdoll recovery escaping sustained load. Crush Edit38/38+Play5/5; footing/turn Edit50/50+Play5/5. Flame, dust, cloud/sonar, rock form-lighting and compact reference-art HUD changes pass final Edit22/22+Play10/10 (22:38:52 UTC); final visual evidence is recorded in [follow-up report](VISUAL_PHYSICS_FOLLOWUP_2026_09_08.md). GPU/full-game and arbitrary moving-pile acceptance are not implied. Earlier blanket turn acceptance is superseded by these reproduced defects and fresh focused results.

**Fire / stone / graphics integration in progress (2026-09-07, uncommitted main1235579):** Fire Play4/4; eight-High1080p cosmetic CPU p95 .9711ms,0B GC still misses .8ms target; GPU unavailable. Stone physical4cm heavy-drop/compositing/wall contracts pass. Production arena restore2/2 and UI4/4 pass after scene ownership and baseline-before-physics fixes. Basic online Run-20260907T093340 passed; selected-stone combat remains partial, next build includes event attribution trace. Old main-menu Left veil removed in actual code and verified live (BuildReports/MenuBackingRemoval). Latest user requires reference matching, including UI animations and procedural valley: optional UI V2 is under actual Unity validation; original font/layout assets retained. Geometry refinement14/14 and atmosphere V2 equations12/12 pass Edit tests; combined visual/performance gates remain open. [Contracts, sources and evidence](FIRE_STONE_GRAPHICS_INTEGRATION.md).

**Latest saved handoff:** corrected whole-match arena restore Play2/2 passed02:17:13; paired mouse Edit12/12+Play3/3 passed. Development build02:21:20 saved,0 errors/195 warnings. New protocol3 pair aborted before networking due D3D11 device removed; owned players closed and user restarting Unity. Further launches paused for recovery. No push. Details: [wall/wind/input/match follow-up](WALL_WIND_ROUND_FOLLOWUP.md).

**Latest user correction (02:11 UTC): arena restoration is once per whole game/session, not after each life. Damage/debris must persist across ordinary KO/respawn; restore on full-match victory, return to Main, restart/new game. Earlier per-life reset acceptance below is historical and does not satisfy this corrected requirement. Corrected lifecycle Play2/2 passed02:17:13 UTC. Paired mouse Edit12/12 +actual row/RMB Play3/3 also passed. Fresh build/network run follows.**

**September 7 wall/wind/round follow-up:** saved matching wall interior palette and sealed wide-bevel junctions; production depth-varying 3D partition rebaked without arena regeneration. Surface Edit 7/7, depth Edit 2/2, rise Play 1/1, wind Edit 2/2 + production Play 1/1, actual routed RMB launch Play 1/1, arena reset Play 2/2 and online Edit 35/35 passed. Protocol 3 requires a fresh built pair; current online acceptance pending. No push. [Evidence and settings](WALL_WIND_ROUND_FOLLOWUP.md).

Updated: 2026-09-07

**Permanent gameplay vignette accepted visually, 00:42:57 UTC (uncommitted `1235579`):** constant edge blur/darkening, darkness increased to **0.20**, charge independent, same-frame centre difference zero. Production visual Play **1/1**; final frame inspected. Saved tuning through **Elemental > VFX > Edit Gameplay Vignette**. [Evidence](GAMEPLAY_VIGNETTE_REVIEW.md).

**Authority clock / impaired Combat accepted, 00:48:21 UTC:** Edit **35/35**; normal Run004408 and 150 ms delay / 30 ms jitter / 3% send loss per peer Run004650 passed real Relay movement, two attack edges and host leave with zero command rejections. Delay/loss begins after initial readiness/countdown. Stone Run004924 remains partial: throwing aim was outside camera; no attributed stone hit/kill. Next combat check must establish selected stone identity, actual contact and replicated death; Physics/source0 damage cannot substitute. Test processes closed. Full Stage 2 and push remain unaccepted. [Reports, clock fix and limits](ONLINE_ALPHA_TESTING.md).

**Follow-up accepted locally, 22:21 UTC (uncommitted `1235579`):** user UI settings preserved; per-life Won/Lost/Draw and held centered menu press passed **5 Edit + 2 Play** tests. Expanded capture/extraction smoke/chips, pulsing charge vignette and fake dusty sunlight passed **3 Edit + 1 particle Play + 1 production visual Play**. Charge/arena screenshots inspected; decor screenshot has partial foreground occlusion. [UI](FEEL_FOLLOWUP_UI.md), [VFX](POWER_VFX_FOLLOWUP.md).

**Basic online scenario passed, 23:57:42 UTC:** saved second actor/transport graph, MPS/NGO/Transport and runtime collision-material replication are integrated. Online Edit **34/34**, Play **4/4**, Development build **0 errors / 188 warnings** at 23:55:14 UTC. Two actual Relay players completed Host/Join, world sync/countdown, sustained Combat, client movement, two host-accepted primary presses/releases and normal host leave returning both to Main. Observed health matched exactly. Run235548 reports retained; test processes closed. An older command rejection status remains unexplained; delay/loss and attributed stone hits/kills are not accepted by this probe. No full Stage 2 release/commit claim. [Evidence and limits](ONLINE_ALPHA_TESTING.md).

**Editable HUD layout accepted, 20:55:57 UTC (uncommitted main `1235579`):** separate group/child anchors, position, size, scale and rotation are saved in `ElementalHudLayout.asset` through **Elemental > UI > Edit HUD Layout**. Health/Energy Under Bar, Icon and Value; Navigation children; Pause button/icon are independent. Edit **1/1**, Play **1/1**, three aspect ratios and live paused editing/pointer acceptance passed; captures inspected. Event-driven apply peak **0.1149 ms**. No animation/arena regeneration. Online integration still pending, Host/Join disabled. [Map and scope](HUD_LAYOUT_TUNING.md).

**Locomotion, turns and world contact accepted, 19:03:38 UTC (uncommitted):** user clip/controller assignments remain unchanged. Final cadence/clock Play 2/2 passed: ten-cycle mean actual/requested player 0.999379, bot 0.996191 (both inside 1%); both feet reach settled stance. Targeted Edit 32/32 passed at 18:59:00, including anatomical reach release and reload safety. Four turn scenarios passed strict whole-sequence floor gates and visual review at 18:50:13. The real pit/hump/slope matrix passed both actors at controlled 30/60/120 Hz at 19:01:18: maximum settled drift 13.090 mm and absolute normal gap 1.304 mm, under unchanged limits. The runtime reach-release counter is not serialized by the surface report; no exact count is claimed. This supersedes earlier pending locomotion/slope checkpoints; final build/commit remain coordinator-owned. [Final contract, menus, evidence and limitations](LOCOMOTION_RHYTHM_IMPLEMENTATION.md).

**Shared mass-policy gap implementation, uncommitted main `1235579`:** editable
asset, explicit owner bindings, common new-matter conversion and mass/volume
conservation are installed and saved. **7/7 Edit** (15:42:36 UTC) and **3/3 Play**
(15:44:36 UTC) passed under Unity 6000.5.7f1.
[Scope, remaining validation and menus](SHARED_STONE_MASS_POLICY.md).

**Local physical impact implementation, 14:33 UTC, uncommitted main `1235579`:**
**24/24 Edit + 3/3 Play** pass. Eleven-region PhysX displacement/recovery,
deduplication, weak/no-stun, medium/0.24s stun and production heavy handoff are
covered; torso/pelvis captures inspected. Additional **2/2 Play** accepted at
16:11:46 UTC: actual thrown head/both-arm/both-leg regions recover on a clear
physical platform. Five videos require recapture after visual review found
incorrect framing; numeric response acceptance remains valid. Ninety-six normal frames measured both
fighters at 0.011976ms step / 0.087081ms pose mean and zero bytes in 96 fixed /
149 pose callback allocation brackets.
[Contract and actual editable assets](LOCAL_PHYSICAL_HIT_RESPONSE.md).

**Readability follow-up, September 6:** original HUD restored (+12% type),
matching intact/fractured beveled walls, pillar-line offset −0.35m saved.
Sonar array-capacity regression repaired (**3/3 Play**); short turns and actual
belt/plume skin motion verified. Live LMB accumulation now drives charge camera;
**19/19 Edit + 2/2 physical-input Play** with rendered FOV/aberration proof.
This supersedes the premium HUD visual choice below. Manual locomotion retained.
[Repair evidence](READABILITY_REPAIR_2026_09_06.md).
Stone weight/body-return follow-up accepted: **53/53 Edit + 6/6 Play**,
post-normalization transfer ×1.4 and slower distributed torso recovery.

**Character/premium repair accepted, 02:04 UTC:** final production **10/10 Play**,
support/pivot/respawn **37/37 Edit**, charge/short-slot policy **29/29 Edit**.
Blender/secondary-motion lane **13/13 Edit + 1/1 Play**. Corrected live-round
respawn defaults and teleport history, turn contact release, stale ledge probes
and malformed contact forecasts. Installed three original-speed Mixamo bridges
without rebuilding manual locomotion; shared native HUD, bounded charge FOV/shake/
edge aberration and clothing/belt/plume repair are integrated. Evidence and scoped
visual limits: [SEPTEMBER_PREMIUM_ANIMATION_REPAIR](SEPTEMBER_PREMIUM_ANIMATION_REPAIR.md).

**Mobility/wall correction, 01:18 UTC:** **30/30 Edit + 6/6 Play pass** for line
placement offset, held-Space half-crouch, rider-aligned cushioning/breakup, bounded
surf carry, actual six-metre arena strokes, rigid chipped wall cells and stronger
source bonds. Existing manual locomotion was preserved. Scope and inspected
captures: [MOBILITY_WALL_FOLLOWUP](MOBILITY_WALL_FOLLOWUP.md).

**Gameplay follow-up, 00:43 UTC:** scoped **64/64 Edit + 7/7 Play + 1/1 final
combo** pass for full kick, wall fit/connectivity/grip, stone response and sonar.
Held-LMB+RMB line height is now exposed in the custom profile Inspector. Existing
locomotion assignments are protected from automatic base-layer regeneration;
the user is editing them manually. [Evidence](GAMEPLAY_TUNING_FOLLOWUP.md).

**Seismic polish, 00:18 UTC, uncommitted main `1235579`:** accepted scoped checks
are **10/10 Edit + 5/5 GPU Edit + 2/2 Play**. Toggle fade is one second; running
waves use 2 m spacing, half-width and lower gain. Opponents wait for the same
wave/afterglow and then appear brighter through walls. Night lighting is 5%
lower. Actual wall-occlusion and gameplay captures inspected; this does not
upgrade unrelated animation, startup or global performance gates.
[Details](SEISMIC_VISION_POLISH.md).

**New requested HUD / paired-click combat, uncommitted on main `1235579`:**
Saved-scene UI installation and kick/spin source bake succeed. Final checks pass
**35/35 EditMode** (`DuelAcceptanceEditFinal.xml`) and **9/9 production PlayMode**
(`DuelAcceptancePlay5.xml`). HUD health/mana/score, readiness controls, round end
and restart, fast physical LMB+RMB through all five combo beats, 30/60/120 FPS
target sequences, old paired-input responsiveness, and both wall hand contacts
are accepted by these scoped gates. The masked-out kick and unreachable guard
pose were fixed, with the original movement/contact thresholds retained.
Gameplay HUD captures and CPU timing live in `BuildReports/DuelHud`; visible
kick/spin proof is in `BuildReports/QuickStoneCombo`. These frame-rate settings
are not a claim of sustained hardware FPS or exhaustive legacy feature coverage.
Final wall camera/profiler recapture passes another 1/1 in
`DuelWallVisualFinal.xml`, with inspected contact/release images and approximately
2.2/2.9cm hand-to-wall distances in `BuildReports/WallBrace`.
Full status:
[DUEL_HUD_IMPLEMENTATION](DUEL_HUD_IMPLEMENTATION.md).

**Publication checkpoint requested by the user, 2026-09-05:** this snapshot is
intended for `main` and includes the existing local work, source art, assets,
reproducible tools and evidence. It is not an assertion that all checks pass.
Airborne acquisition now physically starts (15:48 Play), but moving-lip hand
contact still fails at 0.925 m versus the 0.35 m gate. Armor coverage still fails
its unchanged distinct-collar-plate gate (3 versus 4); further geometry review is
open. The final native build/strict loading-cover recapture has not yet run.
Camera, outer-ring destruction, charged surf and isolated SONIC acceptances above
remain valid. Large startup cache assets and profiler/cubemap data use Git LFS;
SONIC weights remain reproducibly downloadable under the existing ignore rule.


**Latest verified follow-up, 2026-09-05 15:23 UTC (supersedes older pending notes):**
Gameplay camera fixed in the permanent Cinemachine owner and saved scene: perspective
lens ownership removes physical-gate crop; pitch-distance lift is included in the
arm-height solve. Framing math 7/7 and real neutral/magic/return Play proof 1/1 pass.
Root reviewed the neutral image; both feet have >23% lower-frame margin in this run.
Evidence: `BuildReports/GameplayCameraFraming/20260905T151846308Z`.

SONIC isolated CPU production-actor preview passes at 15:23:30: 252 rendered boxing
samples / 2.554 s, four rolling plans, bilateral hand motion and zero root drift,
with final foot owner retained and camera/UI/bridge/rival state restored. Walk and
boxing PNGs visually inspected. No SONIC component is enabled in saved gameplay.

New user issues under final acceptance: shared interior palette on the heavily cut
sixth outer arch, restored dense fracture cloud, airborne moving-platform catch,
and denser live-bone neck/shoulder/torso armor coverage. These are not accepted
solely from source compilation; focused physical and visual checks are running.


**Latest user corrections, 13:31 UTC:** equipped armor now retains ordinary
locomotion with a separate continuous speed multiplier (~83% compact, ~75%
expanded), without cast stance or disabling automatic mantle. Short Space no
longer selects a premature PillarJump upper-body pose; centered aim removes the
late side bias. New policy **5/5 Edit**, physical input **1/1 Play**, visual proof
**1/1 / four frames**, including 19 rendered jump frames with zero magic layer.
The existing semantic magic regression was rerun: **8/8 at 13:20 UTC**.

Sky final **9/9 captures at 13:31**, envelope math **3/3**: no shell chord or
direction-noise pole, system planet hidden inside the 63.1 m outer atmosphere,
Moon phase/detail and warm horizon color visually reviewed. See DAY_NIGHT_RESCUE.
The initial Surf+Space physical input proof passed (one pillar, 12 stones), but
the user subsequently requested hold-to-charge/release and a physically tilted
column for a forward long jump; that refinement is in progress and supersedes
the instant-launch acceptance. Fresh Player build/cache validation is running.
SONIC anatomical collapse is fixed by retaining Humanoid hips ownership, but
the experimental boxing playback cadence and final framing are still being
reviewed; it remains absent from saved gameplay scenes.

**Current verification, September 5, 11:30 UTC (dirty `d2174ed`):** semantic
magic passes **21/21 EditMode and 8/8 PlayMode**. Real dual-mouse quick stones pass
**30/60/120 Hz, 3/3**. Angular-velocity inertialization passes **6/6**, including
three-axis spatial derivative continuity; the old 176-degree held-arm failure is
fixed and actual held/gravity/vector now passes. Body-relative multi-object carry
passes **1/1**, responsive hand math **7/7**. A real ground-wave commit now emits
its missing presentation event and passes **1/1**. There are eleven shared pose
slots, not eleven total gameplay abilities.

All-eleven current visual matrix **36/36 frames captured at 11:23**, with readable
anticipation/contact/recovery and repeated quick-punch buffers. Head pitch spans
**-21.46 to +28.00 degrees**, valid neck length >=0.1324 m. The excessive source
head tilt is bounded in the existing final body owner; pure head checks **10/10**.
Root reviewed platform, armor and repeated punch contacts. This is scoped visual
and continuity evidence, not a promise of perfect animation in every combination.
The final protected-pose/paused-pose/sonar regression and full movement visual
matrix are still being checked.

Windows Development from saved scenes **builds successfully, zero errors** at
11:26:40 (186 warnings, mainly inference compute variants). Fresh process reports
world ready at **5.589 s** and cover at **2.848 s**; both screenshots were black in
the hidden run, so those files do not establish visual loading acceptance. The
Player also exposes **26 fracture-cache misses**, under investigation. Editor cache
comparison remains **12.75 s cached / 43.74 s uncached**; post-cleanup scene is 6.07 MB.

SONIC remains isolated in `Assets/Experimental/SonicPrototype`, absent from the
saved scene. Unity CPU inference p50/p95 is **79.4/82.3 ms walk**, **75.9/79.3 ms
boxing**. Full Unity/ORT output parity passes (max error ~2.2e-5), G1 math **4/4**.
Humanoid preview is being rechecked after preserving the production PlayableGraph;
inference alone is not visual integration acceptance.

**Environment visual follow-up, 11:00 UTC:** lit dust passes **2/2** focused GPU
Play with day/night delta **0.39619595**, night visibility **0.1160493** and
neutral-reference footprint error **0.001231**. The exact same production particle
layout was captured and visually inspected at Day/Dusk/Night; night is dimmer.
Seismic vision passes **1/1** production lifecycle and **5/5** temporal GPU checks
at 30/60/120 Hz, including byte-exact inactive day/night output. These are scoped
material/effect results, not an all-technique visual acceptance upgrade.

**Outer column content, 2026-09-04 (dirty `d2174ed`):** seven independent damaged
columns placed with 2.5 m arena clearance, buried caps and existing arena materials.
85 structural cells and eight loose stones use the existing physical magic paths.
**2/2 EditMode and fresh 7/7 PlayMode passed**, latest UTC 10:42:17 on September 5;
every cell was grabbed and repaired, loose stones were reacquired, unsupported
islands released and foundation-connected cells stayed seated during partial repair.
[Evidence](OUTER_STONE_RING.md).
This scoped content result does not upgrade broader M11 acceptance.

Current branch: `codex/environment-aware-motion-matching-spike`

Current implementation: dirty working tree on `d2174eded114dd022e4a9c442abadda7a0e44555`.

Historical foundation evidence below belongs to `8c2e6245a0e4fe1e169b63998e918ce800477b36`,
not the current dirty tree. Current scoped evidence is tracked separately.

Technical context: [`Docs/PROJECT_TECHNICAL_STATE.md`](PROJECT_TECHNICAL_STATE.md)

## Current verdict

**MMB field + contact packing, UTC 14:14:** completed the user-clarified group
interaction. Fixed armor-release input latch; restored radius collection while
MMB is held; removed artificial orbit-slot spacing. **9/9 Edit + 9/9 focused Play
passed** on dirty `d2174ed`, including real button input for three arena cells
after armor, arrivals, contact packing, repeated group capture and rest/wake.
This supersedes the single-target assumption below; wider M11 acceptance is
unchanged. [Evidence](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Middle-button clarification, UTC 13:31:** Input System MMB press/hold/move/release
and repeated presses pass through the shipping adapter/router; **8/8 focused Play**.
The user's remaining gravity-grip failure is still unconfirmed in this scenario;
no additional gameplay change or acceptance upgrade is claimed.
[Raw-button evidence](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Arena gravity acquisition, 2026-09-04 (dirty `d2174ed`, UTC 13:25):** fixed an
additional user-reproduced failure: Surface-only arena floor intercepted MMB rays
and produced empty successful sessions. Focus now requires Gravity/Repair; start
requires captured matter or a controllable structure. New shipping-geometry
screen-point regression passes lift and repeated grabs; circle disassembly/repair
is retained. **4/4 EditMode + 7/7 focused PlayMode passed.** User wave/shadow settings
retained. The broader legacy shadow assertion failed and is documented separately;
M11/global acceptance remains unchanged. [Details](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Loose stone fixes, 2026-09-04 (dirty `d2174ed`, UTC 12:48):** camera-hidden arena
parents no longer block valid loose targets; supported stones sleep without drift,
wake for grip and fall on support removal; wall/platform shards have one gravity
authority. Oblique cuts and broad bevels replace rectangular secondary fractures,
while primary arena detachment remains exact. **12/12 Edit, 5/5 loose stone Play,
3/3 production fracture Play passed**. Current saved wave settings retained by
matching backup/profile SHA256. [Evidence and baseline](LOOSE_STONE_FIX.md),
[geometry/Editor measurements](CONTAINED_FRACTURE_FIX.md). This scoped result does
not change wider-suite or M11 acceptance.

**Wave overlap and head seam stones, 2026-09-04 (dirty `d2174ed`, UTC 12:21):**
the previous automatic propagation slowdown is superseded. Each row samples one
travelling pulse at distance/speed; a new top Inspector length field scales the
visible phase times. The user's saved profile has not been retuned. Head armor
uses measured skinned geometry, final-pose attachment and 16 independent small
gap stones included in orbit and projectiles. Base body layout/profile preserved.
**23/23 EditMode and 5/5 focused PlayMode passed.** All 93 wave cells can rise
together without penetration; all 16 fillers expand and launch; head-follow
error < .000004 m. Evidence: `WaveContactEdit.json`, `WaveHeadArmorPlay.json`,
`HeadArmor/Latest.json` and `WaveContact/Latest.json` under BuildReports.
Three wider exploratory-suite failures are recorded in [HEAD_ARMOR_FIX.md](HEAD_ARMOR_FIX.md)
and remain outside this scoped result. M11/global acceptance is not upgraded.

**Wave placement and moving crest, 2026-09-04 (dirty `d2174ed`, UTC 11:43):** fixed
lowest-vertex recentering that snapped pieces sideways and caused neighbour overlap.
Shared cast frame, contained bevels and whole-wave reservation keep the partition
stable; crest height travels outward and older rows retreat. Long saved timings
limit effective speed, now labelled as a maximum in the Inspector. Dust preserves
its authored tint without lighting-driven black smoke. **21/21 Edit + 2/2 Play**:
21,646 collision pairs with zero penetration; 1.25 million projected vertices with
zero drift; advancing crest and descending rows verified; dense dust lighting delta
zero. Saved user profile unchanged. [Scope, baselines and captures](WAVE_CONTACT_FIX.md).

**Filled fracture, stable wave and contact FX, 2026-09-04 (dirty `d2174ed`):**
children now partition the original convex volume (97.62% collision / 96.39% render
fill in the rotated/recursive fixture). Live wave geometry cannot be stolen by
another cast; its ground cross-section stays fixed. Added contour dust/chip streams,
stronger extraction cues, matching small wall collider hulls and geometry-based
gravity packing. User scene, shadows and updated long wave timings are retained.
Contained fracture **8/8 Edit + 3/3 Play** (latest UTC `10:17:57`); production wave/
contact/grip **4/4 Play** (`10:22:37`), with the focused Edit report in
`BuildReports/WaveContactEdit.json`. Cached split maximum 0.248 ms / 0 managed bytes;
wave contact marker maximum 5.9642 ms in Editor. Cold preparation/whole-frame costs
remain distinct. [Details](WAVE_CONTACT_FIX.md); [fill evidence](CONTAINED_FRACTURE_FIX.md).

**Wave/arena fracture follow-up, 2026-09-04 (dirty `d2174ed`):** exact arena
detachment restored; secondary stones and wall visuals fit the real parent convex.
Repeated splitting of thin rotated sources preserves containment and mass. Wave
cells are bounded, pool mesh reuse is corrected, and authoring is reduced to six
curves, five durations and five main controls. Saved user values remain intact.
Scoped checks: **28/28 EditMode + 4/4 PlayMode** (latest UTC `09:32:31`). Physical
split measurement: maximum **0.6496 ms**, zero managed bytes across four calls.
Wave preparation maximum **168.865 ms** in the Editor remains a startup performance
risk. [Wave evidence](WAVE_REPAIR.md); [arena/stone evidence](CONTAINED_FRACTURE_FIX.md).

**Wall material correction:** replaced the entire natural-fracture material array,
removing the retained clay overlay. Saved wall interior and setup defaults now
reference sandstone; the runtime check covers every material slot.

**Combat/mobility follow-up, 2026-09-04 (dirty `d2174ed`):** cumulative armor and
structure damage, persistent secondary column breakup, natural wall stone
visuals, 3D chip rotation, surf/pillar dust, no roll on cushioned landing, and
exposed continuous wave animation are implemented with preserved user scene and
shadow settings. Current scoped evidence: [combat/mobility notes](COMBAT_MOBILITY_FIXES.md).
Final focused checks: **9/9 EditMode + 4/4 PlayMode**, UTC `08:49:26`.

**Dust/shadow follow-up, 2026-09-04 (dirty `d2174ed`):** user Sun settings saved;
cosmetic-shard/dust compositing corrected across the shared effects paths. Fresh
authoring **5/5** and pixel/production PlayMode **2/2** pass (UTC `07:37:29`).
Physical stone depth remains intact; prior orphan armor warnings remain.
See [settings and scoped evidence](EARTH_MATERIAL_PASS_CHECKLIST.md#dust-and-shard-authoring-follow-up--september-4).

**Earth material pass, 2026-09-04:** the approved 11-part implementation is integrated
through a scene-preserving menu. Production measurements confirm 96 armor shots at
44 m/s, actual charged wall-piece movement after physics, persistent medium/huge
split mass and active backward EAMM. Final category-wide bevel and effects validation
is recorded in the [material-pass checklist](EARTH_MATERIAL_PASS_CHECKLIST.md).
Do not interpret scoped smoke checks as completion of previous animation milestones.

**Mobility follow-up, 2026-09-04 (dirty working tree on `d2174ed`):** wave
foundations are lowered 20% along surface up, configurable in EarthPillarWaveProfile;
arena/planet placement and bindings tests pass (21/21 EditMode, UTC 22:15:02 Sep 3).
Narrow saved-scene repair restores the launch pillar and surf material. Combined
production regression at UTC 22:16:03 is **3/4**, not accepted as fully green:
player roll travels 2.458 m and bot 1.786 m; bot misses the 2 m distance gate.
Both now actually tumble and retain the outgoing blend. Idle/stop/mobility pass.
See technical-state follow-up for exact evidence and remaining performance/visual
limits. This supersedes the earlier incomplete roll acceptance below.

**Landing-roll travel, 2026-09-03 (working tree on `d2174ed`):** fixed-clock motor
roll motion now accompanies the authored landing roll with bounded forward decay.
11/11 pure tests and 1/1 dual-fighter production drop test passed; the subsequent
combined foot/stop/roll regression passes 3/3 at `2026-09-03T21:32:59.3335687Z`. Exact timestamps
and distances are recorded in the technical state. Re-run via `Elemental/QA/Run
Landing Roll Motion EditMode Tests` and `Elemental/QA/Run Landing Roll Motion
PlayMode Test`. Existing user clips, Blend Tree, scene and visual profiles were not
regenerated. Broad animation/obstacle/performance acceptance remains open.

**Stop/support-release follow-up, 2026-09-03 (working tree on `d2174ed`):** free
foot targets now filter only the contact correction relative to authored motion;
invalid support and lock hand-offs discard stale filter history. LateUpdate no
longer repositions/rotates ankles after the Humanoid knee solve. Seven added pure
tests reproduced 4.3–4.47 m run/stop target backlog before the fix; the updated
`AnimationTransitionsVNextEdit.json` passes 50/50 at `2026-09-03T21:16:41.3800060Z`.
`IdleFootOrientationPlay.json` passes 2/2 at `2026-09-03T21:20:53.3573992Z`, including
actual forward/back movement and stop on both actors (free-target lag <=5 mm),
Avatar-bounded shin length, and the prior idle-inversion regression. EditMode
emitted pre-existing `EarthArmorPiece` required-component warnings. Platform-edge
visual QA, EAMM pose rejection, and broad performance acceptance remain separate.

**Narrow runtime fix, 2026-09-03 (working tree on `d2174ed`):** idle feet no longer
receive the skeleton-bone orientation as their Humanoid IK goal. The live
on/off/on reproduction isolated the inversion to `EarthFootContactController`.
`BuildReports/IdleFootOrientationPlay.json` at `2026-09-03T21:09:52.6993029Z`
passes 1/1, exercising both fighters and repeated contact ramps at 30/60/120
frame-rate caps. User clips/Blend Tree/material settings are preserved. Re-run via
`Elemental/QA/Run Idle Foot Orientation Regression`. This does not close the
remaining EAMM pose rejection or the broad animation/performance gates.

**M11 is in foundation stabilization/integration, not acceptance-green at the tested commit.**

The branch contains remote base `e401a22` plus tested local integration commit `8c2e624`. The shared
worktree also contains user-owned modified/untracked evidence and Unity recovery scenes;
preserve them. The generated scene has now been rebuilt after the rig/import changes. A fresh full
EditMode run passes `586/587`; the only failure is the stale Native Windows evidence containing 186
warnings. Fresh targeted PlayMode foundation regressions pass `2/2`. These runs validate the new
rig/support/input seams, not the entire M11 focused or broad PlayMode acceptance chain.

Do not start another ability, progression layer, or content expansion until the generated scene
is rebuilt and the M11 acceptance chain is fresh and green.

## Evidence snapshot

| Evidence | Timestamp (UTC) | Result | Interpretation |
|---|---:|---|---|
| `BuildReports/FoundationWorkingTreeEdit-20260831.xml` | 2026-08-31 14:45 | `586/587` passed | Fresh full EditMode working-tree run. Only the zero-warning build-evidence test fails on the existing 186-warning report. |
| `BuildReports/FoundationWorkingTreePlay-20260831.xml` | 2026-08-31 14:48 | `2/2` passed | Fresh targeted scene-secondary-motion and dynamic-debris support regressions after generator rebuild. |
| `BuildReports/Mvp01FocusedEdit.json` | 2026-08-31 09:48 | `274/274` passed | Strong focused pure/editor coverage, but predates HEAD (`11:13 UTC`). Stale for current commit. |
| `BuildReports/Mvp01FocusedPlay.json` | 2026-08-31 09:28 | `8/18` passed, `10` failed | Current working-tree integration evidence is red. See failure groups below. |
| `BuildReports/Mvp01RescueCurrent.json` | 2026-08-31 09:28 | `success: false` | The latest aggregate acceptance marker is red. |
| `BuildReports/BrokenCrownPlay.json` | 2026-08-31 09:49 | `1/1` passed | Later targeted arena contract pass; not a substitute for the focused suite. |
| `BuildReports/SurfFinitePlay.json` | 2026-08-31 06:35 | `3/3` passed | Finite surf contract is locally green on its captured snapshot. |
| `BuildReports/AnimationContactAcceptanceEdit.json` | 2026-08-31 06:22 | `7/7` passed | Pure animation-contact gate is locally green. |
| `BuildReports/AnimationContactMatrixPlay.json` | 2026-08-31 06:21 | `1/1` passed | 30/60/120 matrix artifact passes on its captured snapshot. |
| `BuildReports/CharacterAnimationVisualAuditPlay.json` | 2026-08-31 06:31 | `1/1` passed | Targeted animation audit passes; ADR 0033 still requires the complete same-worktree gate. |
| `BuildReports/AcceptedMvpEvidencePlay.json` | 2026-08-31 05:07 | `1/1` passed | Older than the later red aggregate marker; historical only. |
| `BuildReports/Mvp01Profiler.json` | 2026-08-31 05:06 | standalone 720-frame pass | CPU/total p95 `8.335 ms`, zero measured steady-state GC; GPU unavailable and waived; predates HEAD. |
| `BuildReports/Mvp01ProfilerEditorDiagnosticLatest.json` | 2026-08-31 09:28 | failed | Editor diagnostic p95 `19.1631 ms`; foot-contact and render audit fail. Not authoritative, not green. |
| `BuildReports/NativeWindows.json` | 2026-08-31 05:05 | build succeeded, 186 warnings | Fails the repository zero-warning acceptance rule. |

All timestamps above are file contents, not filesystem modification times. Historical rows below
the two `FoundationWorkingTree` artifacts predate remote base `e401a22` (created at
`2026-08-31T11:13:40Z`); they remain reproduction history, not validation of `8c2e624`.

## Now / Next / Later

### Now — restore one trustworthy M11 golden path

1. Preserve the shared dirty worktree; identify whether any active Unity/test process owns the
   latest reports before starting a new run.
2. Rebuild `EarthCoreSlice` through `M3EarthCoreSetup.Configure()` after any further source asset,
   importer, profile, or generator change. The current rig/import rebuild is complete.
3. Run the complete focused PlayMode chain from the tested implementation. Full EditMode and the two
   new foundation PlayMode regressions are fresh; the broader PlayMode scope is not.
4. Repair remaining failures one authority seam at a time in this order: physical input/targeting;
   support/landing; visible Humanoid/ragdoll reset; projectile contact; accepted evidence runner.
5. Run the accepted-evidence scenario, standalone 720-frame profiler/capture, and a warning-free
   Native Windows build. Record commit, machine, resolution, mode, sample count, and whether GPU
   timing was actually available.
6. Accept or keep proposed ADR 0033 based on its complete same-worktree verification gate, then
   reconcile M11's status/radius/performance prose with the evidence.

### Next — close the vertical-slice gate

- Run repository-wide EditMode and PlayMode after focused M11 is green; M11 explicitly says the
  full suites were not rerun in its earlier focused rebuild.
- Recheck the golden path manually from fresh launch through movement, physical input, matter
  extraction/launch, structure fracture/repair, rival impact, both KO paths, respawn, and replay.
- Capture representative 1920x1080 frames and normal-speed video from the shipping camera; compare
  silhouettes, contact, foot motion, shadows, arena seating, and both fighters.
- Measure current-head CPU, GPU, memory, GC, frame pacing, loading, and queue peaks on the reference
  PC. Add a minimum-target machine before claiming production performance.
- Protect the new `CharacterSupportAuthority` -> `PlanetMotor` boundary with focused support,
  landing, moving-platform, generation-switch, and seam/debris coverage.
- Update the current milestone and `.solo-studio` summaries to link these two canonical living
  docs instead of carrying divergent status claims.

### Later — explicitly outside the M11 rescue

- Full-game atomic save/backup/recovery and durable replay files.
- Production network transport, real multi-machine sessions, disconnect/reconnect, service failure,
  relay/NAT, security, and platform integration.
- Controller/accessibility matrix, onboarding with fresh players, localization, store/release,
  telemetry/privacy, support, and rollback operations.
- HP/score/rounds/victory UI, progression, NavMesh/behavior tree, or another enemy/element only
  after the Earth vertical slice is acceptance-green and externally playtested.

## Completed work with evidence

“Completed” here means the code/asset/decision exists at HEAD. It does not imply the current HEAD
passes the full acceptance gate unless the evidence column says so.

| Work | State at HEAD | Evidence / commits | Remaining caveat |
|---|---|---|---|
| MVP 0.1 rescue baseline | Implemented and previously focused-green | `08afcd3` “Complete MVP 0.1 earth core rescue”; `add1e3a` evidence update; ADRs 0029/0030 | Later integration changed the scene and current focused PlayMode is red. |
| Broken Crown import/fracture/runtime integration | Implemented | `c5ebeab`; ADR 0031; `BrokenCrownPlay.json` `1/1` | Full focused suite and fresh current-HEAD visual/interaction matrix still required. |
| Animation/contact/rendering rehabilitation | Implemented under proposed decision | `c5ebeab`; ADR 0033; targeted animation reports | ADR 0033 remains proposed; latest bot telemetry has `hardGatesPassed: false`. |
| Finite surf semantic graph | Implemented and targeted-green | ADR 0030 follow-up; `SurfFinitePlay.json` `3/3` | Protect in full focused suite; no broad acceptance implied. |
| Shared Earth matter mass policy | Pure policy + tests + arena/decor adapters present | `5054d09`, `930b8bd`, `b7d75d3`, `3a33a11`, `3d87414`, `6da1ae9`; `EarthMatterMassPolicyTests.cs` | Current-HEAD runtime/profile evidence is missing; integration is not universal by file search. |
| Gameplay-locked celestial clock/key | Policy, tests, and presentation wiring present | `5685799`, `e1f7e61`, `8a4dbc4`; `CelestialLightingAuthorityTests.cs`; `CelestialSystemBehaviour.cs` | Fresh rebuilt-scene visual/render audit required. |
| Classified character support policy | Integrated at `8c2e624` through `CharacterSupportRuntimeAdapter` and `PlanetMotor` | `CharacterSupportAuthorityTests.cs`; `FoundationWorkingTreePlay-20260831.xml` support regression | Complete focused landing/moving-platform PlayMode remains pending. |
| Linebreaker rig/secondary-motion rescue | Weighted source and runtime FBX integrated; generated scene rebuilt | `LinebreakerRigged_weighted.blend`; four weight/rig reports; `FoundationWorkingTreePlay-20260831.xml` | Full visual gait/ragdoll/KO gate and capture remain pending. |
| Canonical input boundary cleanup | Rumble lookdev/VFX shortcuts routed through `EarthInputAdapter` | `FoundationWorkingTreeEdit-20260831.xml`; full source-scan test passes | Physical mouse golden-path failures from the older aggregate still require full focused rerun. |
| Duel-space shadow stabilization | URP/project settings changed | `c606e2a`, `e401a22` | No report or capture after either commit. |
| EAMM production animation graph and catalog | Implemented in dirty working tree | `EarthAnimationGraph`, `EarthInertializationJob`, semantic catalog, 2D locomotion, front/back recovery, bounded magic reach | No PlayMode/capture/profile run by explicit request; final Unity import after catalog bootstrap and visual acceptance remain pending. |

## Latest focused PlayMode failures to reproduce

Source: `BuildReports/Mvp01FocusedPlay.json`, 2026-08-31 09:28 UTC. These are observations from
that run, not assumptions about current HEAD.

1. Broken Crown semantic placement reported floor/rock/rubble gaps and penetrations.
2. A physically held decor rock acquired but received zero control force.
3. Near/far physical mouse wall strokes committed no command and ended cancelled.
4. Stationary physical LMB never started terrain extraction.
5. A high fall onto the landing cushion did not satisfy the safe-landing expectation.
6. Player/bot visible ragdoll atomic reset expectation failed.
7. Rival did not share the player's Humanoid Avatar/controller in the loaded scene.
8. Stomp stone launch was `0.2919` where the test expected greater than `20`.
9. Accepted evidence aggregate wrote `success: false`.
10. A shallow near-surface projectile graze entered `Sleeping` instead of staying `Armed`.

The later `BrokenCrownPlay.json` pass may supersede item 1 only in its narrow scenario. Rerun the
complete focused suite before deleting, reclassifying, or fixing any item.

## Corner-case and regression matrix

| Area | Protected corner case | Primary seam / evidence |
|---|---|---|
| Input ownership | 80 ms dual-button decision preserves the original full primary path; quick drag is draw, stationary hold is acquire/extract, chord is not order-dependent | `EarthActionRouterTests.cs`, `EarthCoreVisualRuntimeTests.cs`, `EarthContextualInputRuntimeTests.cs` |
| Physical targeting | Dense arena dressing cannot hide the canonical planet; caster cannot target itself; viewport-normalized intent survives camera change | `EarthInputAdapter`, `EarthTargetQueryService`, physical-input focused PlayMode tests |
| Terrain transaction | Failed/cancelled extraction exposes neither hole nor fragment; commit exposes exactly one reserved fragment; buffered release survives commit latency | `TerrainExtractionTransactionTests.cs`, `EarthCoreReplayTests.cs`, `EarthPlayerGoldenPathRuntimeTests.cs` |
| Surface/support | Nearest valid constructed support wins; generation changes invalidate stale handles; seam/debris cannot steal stable character authority; exact-contact fallback prevents one-frame airborne state | `EarthSurfaceContractTests.cs`, `EarthSurfaceQueryRuntimeTests.cs`, `CharacterSupportAuthorityTests.cs`, `PlanetMotorPlayModeTests.cs` |
| Platform fracture | Cast creates no runtime objects/collider burst; early impact queues until ready; one cell prepares per frame; settled support velocity is zero | ADR 0029, `EarthPlatformPreparationBudgetTests.cs`, `EarthPlayerGoldenPathRuntimeTests.cs` |
| Arena fracture | Ordinary hits cannot destroy meteor floor; vector/pluck releases one cell; gravity disassembly is progressive; complete repair restores intact proxy | ADR 0031, `BrokenCrownArenaRuntimeTests.cs` |
| Matter identity/mass | Stable provenance survives representation change; return is atomic; authored/collider masses resolve to one gameplay policy without zero/NaN or scale drift | `EarthMatterKernelTests.cs`, `EarthReturnSessionTests.cs`, `EarthMatterMassPolicyTests.cs` |
| Projectile contact | Shallow tangent graze stays armed; direct wall/character hit spends once; sweep/callback cannot double-apply impact | `EarthProjectileSurfaceContactSolverTests.cs`, `EarthProjectileSurfaceContactRuntimeTests.cs` |
| Character outcome | One large stone is recoverable knockdown, not KO; three distinct clustered sources can KO; landing cushion suppresses fall KO; impact applies once | `CharacterOutcomeResolverTests.cs`, `EarthCharacterFeelTests.cs`, `EarthMvpEncounterRuntimeTests.cs` |
| Animator/ragdoll | Animator and PhysX never own the same bones; both fighters use visible 11-body rigs; KO/disable/cancel resets control, colliders, bones, IK, and materials atomically | ADR 0029/0030, `ActiveRagdollRuntimeTests.cs`, `EarthMvpEncounterRuntimeTests.cs` |
| Foot contact | No simultaneous locomotion locks; swing must re-arm; support generation change does not retain stale anchor; IK/pelvis/knee steps stay bounded at 30/60/120 | ADR 0033, animation contact/matrix/visual reports and telemetry |
| Surf | Time/coplanar transfer causes no loss; wall damage stays in lower 32%; one contact latches once; small body/character is not a board-killing wall | ADR 0030 finite-surf follow-up, `EarthSurfIntegritySolverTests.cs`, `SurfFinitePlay.json` |
| Camera/rendering | Both fighters remain in sharp envelope; unsafe motion follows accepted DOF policy; one sun/ambient owner; shadow/SSAO changes do not hide contact or affect gameplay | ADR 0032/0033, `EarthCinematicDepthOfFieldSolverTests.cs`, render audit/captures |
| Scene rebuild | Rebuild preserves 55.1 m world, approved arena root/rival spawn, exact floor collider, no missing scripts, stable imported child transforms | `M3EarthCoreSetup.cs`, `BrokenCrownArenaImporterTests.cs`, `BrokenCrownArenaRuntimeTests.cs` |
| Save/replay | Voxel v1 reads into v2; invalid version/flags/edit count reject; commands stay tick-ordered; replay reproduces ordered edits | `VoxelPlanetStateTests.cs`, `ReplayAuditTests.cs`, `EarthCoreReplayTests.cs` |
| Capability | NativeHigh/NativeLow/Web reduce visuals and budgets without changing canonical gameplay; unsupported features reject or use documented fallback | `CapabilityProfileTests.cs`, `CapabilityRuntimeTests.cs`, `Docs/performance-budgets.md` |

## Blockers, stoppers, and active risks

| ID | Severity | Evidence / leading indicator | Mitigation | Fallback | Decision date |
|---|---|---|---|---|---|
| B-001 Tested implementation is only partially validated | Stopper | Full EditMode `586/587` and targeted foundation PlayMode `2/2`; complete focused/broad PlayMode not rerun | Run focused chain and accepted scenario from `8c2e624` (plus docs-only changes) | Use old reports only to prioritize reproduction, never as current verdict | Next integration session |
| B-002 Focused PlayMode red | Stopper | `Mvp01FocusedPlay.json`: 10/18 failed | Reproduce, group by authority seam, fix/rerun one seam at a time | Cut non-core presentation only if it preserves input, terrain/matter, support, and duel truth | Before M11 acceptance |
| B-003 Warning-free build absent | Stopper | `NativeWindows.json`: succeeded with 186 warnings | Classify project vs external warnings and produce a fresh zero-warning build | No release/acceptance claim; warnings may be waived only by an explicit documented owner/removal milestone | Before M11 acceptance |
| B-004 ADR 0033 still proposed | High | Status says implementation under live verification; latest bot telemetry `hardGatesPassed: false` | Complete the exact same-worktree telemetry, seating, visual, and profiler gate | Restore locomotion contact weight to zero while retaining telemetry and authored motion, per ADR rollback | Before accepting ADR 0033 |
| R-001 Generated integration surface | High | 3.5k-line setup, 175k-line generated scene, frequent broad rebuilds | Keep source-of-truth changes in profiles/importers/setup; validate after every rebuild | Revert one generator concern through its ADR seam; never hand-maintain divergent scene edits | Ongoing |
| R-003 Evidence freshness/overwrite | High | `Latest`/`Accepted` files conflict; tracked baseline differs from dirty working result | Include commit/branch in reports or companion manifest; never infer pass from filename | Preserve timestamped raw artifacts and cite immutable ones | Next evidence-tool change |
| R-004 Input-camera-arena coupling | High | Three physical input failures after Broken Crown integration | Keep device boundary singular; test actual `<Mouse>` path at near/far/collider-dense views | Reduce non-authoritative ray blockers/camera composition without bypassing canonical surface query | Before M11 acceptance |
| R-005 Animation/physics handoff | High | Ragdoll reset/shared controller failure; bot telemetry hard gate false | Instrument owner/phase and reset; run interrupts, KO, respawn, arbitrary gait phase | Authored locomotion + foot IK zero; retain physical KO only if atomic reset passes | Before M11 acceptance |
| R-006 Performance claim gap | High | Standalone GPU unavailable/waived; editor diagnostic red; newer code unprofiled | Fresh standalone target-device capture with CPU/GPU/GC/frame pacing and render audit | Disable degradable atmosphere/DOF/motes before changing gameplay fidelity | Before vertical-slice acceptance |
| R-007 Package/maintenance surface | Medium | Git MiniBokeh, prerelease AI Assistant, AI Inference, VFX Graph present | Confirm usage, pinning, license, platform/build-size impact, and removal seam | Remove unused package; keep project-local/URP fallback and CPU gameplay truth | Before dependency freeze |
| R-008 Save/network/release gaps | Medium now, release blocker later | Codec/harness exist but no runtime disk save or production transport | Keep outside M11; create separate gated milestones with recovery/failure tests | Ship local sandbox only if product scope explicitly accepts it | Before Alpha/online commitment |

## Pending M11 acceptance gates

Landing follow-up (2026-09-03, dirty worktree on
`codex/environment-aware-motion-matching-spike`, HEAD `d2174eded114dd022e4a9c442abadda7a0e44555`):
the earlier user report was a collapsing short hop after style/input fixes. Ordinary
`Land` shares the full-strength hard-landing clip. The current patch adds height/impact-scaled
pose blending and excludes initial support acquisition, without changing jump physics or camera.
This is implementation evidence, not visual acceptance. Test suites and automatic Play/Stop
cycles are intentionally omitted at the user's request; short-hop and startup appearance remain
open until checked in the user's live scene.
Connected Unity MCP refresh completed with compilation/import idle and zero Console
errors/warnings; no Play mode cycle or test runner was started for this verification.
The user subsequently confirmed short jumping fixed, but reported startup falling and
unwanted low-height rolls. The follow-up seeds classified motor support before first render
and prevents initial/ordinary landing from forcing the physical knockdown/get-up path;
catastrophic fall KO and combat knockdowns remain. New startup/roll behavior is not yet
visually accepted.
The earlier roll revision admitted a fast forward/backward jump (`6.5 m/s`) or a strong
external airborne velocity change (`4 m/s`, gravity excluded), in addition to a drop `>2 m`.
That revision selected reverse roll playback for backward travel. Motor-only capsule rotation is protected during
animated control and released on motor disable; camera, jump impulse and gravity are unchanged.
Follow-up verification: new code compiles; generated backward clip is Humanoid, has `138`
curves and duration `0.7043334 s`, and is assigned to `Moving Land Back` at positive speed `1`.
The user stopped Play after a script-reload session produced motion-matching cache exceptions;
the agent baked the asset in Edit mode and did not start another Play run. Fresh runtime/visual
acceptance remains pending; old Console exceptions were not cleared or presented as a pass.

Latest correction (same date, supersedes the reverse-roll revision): the user's renewed report
was reproduced by a read-only, explicitly armed Editor startup recorder, not Test Runner.
`CharacterStartupProbe-Before.log` shows folded EAMM output at startup and after the bot's first
cast despite grounded motors, zero impacts and no ragdoll. Candidate hierarchy validation now
rejects that pose before graph output; authored locomotion remains the safe fallback.
The fresh `BuildReports/RuntimeRescue/CharacterStartupProbe.log` contains 87 samples over
5.064 seconds: hips-up projection stays 0.61–0.82, with neither original negative startup pose
nor bot's second collapse. The player receives two actual impacts at 4.438 seconds; that combat
recovery is deliberately retained, not mislabeled as startup instability. Both diagnostic Play
sessions were stopped; zero Console errors after the new run. No test suite was run.
The roll speed threshold is now 9 m/s (normal authored run speed is 7.2, above the old 6.5 gate),
and takeoff itself is excluded from external-impulse evidence. Backward landings temporarily use
ordinary landing/brace; the visually rejected synthetic reverse clip is no longer referenced by
`Moving Land Back`. The typed impact adapter also excludes classified static support contacts
from the combat impulse path. Jump/landing appearance still requires the user's in-game check;
EAMM retarget calibration itself remains unresolved, not accepted as working.
Existing source clips and download settings: `Docs/ANIMATION_INVENTORY.md`.

Runtime rescue update (2026-09-03): baked local-space EAMM, unified animation parameter routing,
camera wiring, dual-fighter DOF, and radial-gravity audits are live and visually captured in
`BuildReports/RuntimeRescue/GameView_RuntimeRescue_Final.png`. EditMode animation contracts pass
`43/43`; Earth Magic Expansion PlayMode is `16/17`, blocked only by the first-tick pillar launch
speed gate (`0.416 m/s`, required `>0.5 m/s`).

| Gate | Required proof at one commit/worktree | Current status |
|---|---|---|
| Compile/import | Zero compiler/shader warnings/errors, no missing scripts after rebuild | Editor refresh reports zero compile errors; warning-free build remains red/unknown because the last Windows report has 186 warnings |
| Focused EditMode | All selected M11 tests green | Fresh full EditMode `586/587`; only external build-evidence warning gate fails |
| Focused PlayMode | Physical input, terrain/matter, platform, support, duel, projectile, arena, surf all green | New foundation subset `2/2`; complete focused suite not rerun, older aggregate remains historical red evidence |
| Accepted scenario | `Mvp01RescueCurrent.json` success and scenario test green | **Fail:** latest aggregate `success: false` |
| ADR 0033 | Contact telemetry, terrain corpus, seating, 1920x1080 visual, representative profiler green together | **Pending/proposed** |
| Performance | 720 representative standalone frames, zero-GC gate, CPU/GPU budgets or explicit justified unavailable metric, queue peaks | Historical CPU pass; no current-HEAD proof; GPU waived |
| Visual | Fresh shipping-camera frames/video show both fighters, seating/contact, fracture, shadows, no missing main capture | **Unknown at HEAD**; working tree lacks `Mvp01RescueCurrent.png` |
| Native build | Reproducible Windows build with zero project warnings/errors and smoke pass | Build succeeds but warning gate fails |
| Broad regression | Full repository EditMode + PlayMode after focused acceptance | Full EditMode rerun (`586/587`); full PlayMode not rerun |
| Player evidence | Fresh player can understand and repeat the duel/earth loop without explanation | No external playtest evidence recorded |

## Handoff checklist for every agent/chat

- [ ] Read `PROJECT_TECHNICAL_STATE.md`, this tracker, `Docs/architecture.md`, current M11, and
      only the ADRs relevant to the change.
- [ ] Record `git status --short --branch`, branch, and full HEAD. Preserve all user-owned dirty,
      untracked, recovery, report, and Unity-generated files.
- [ ] Name the one authority seam and player-visible outcome being changed. Do not expand scope
      while a stopper above is red.
- [ ] If profiles, imports, setup, or generated scene contracts changed, rebuild through
      `M3EarthCoreSetup.Configure()`; do not hand-edit generated `EarthCoreSlice` as source.
- [ ] Run the narrow pure test, the corresponding runtime/physical-input test, then the focused
      suite. Run broad suites/build/profile when the acceptance gate requires them.
- [ ] Record exact totals, failure names, UTC, machine/mode/resolution, report paths, and commit.
      Label missing GPU/player/target-device evidence as unknown.
- [ ] Check `git diff --check`; verify only intended files changed; do not commit generated evidence
      that contradicts the claimed status.
- [ ] Update the affected rows in both living docs when a public contract, accepted result,
      blocker, fallback, or current milestone materially changes.

## Small append/update protocol

Keep this tracker short enough to reread at every handoff:

1. Replace the header snapshot only after inspecting the exact checkout.
2. Update `Current verdict`, `Evidence snapshot`, and affected gate/risk rows; do not rewrite
   history to make a decision look cleaner.
3. Add completed work only with a path, test/report, ADR, or commit hash. Say “implemented” when
   tests are stale; say “accepted” only when the required gate is green at the same commit.
4. Move resolved incidents out of active blockers after recording the closing evidence in the
   milestone/ADR or an immutable timestamped report. Keep one short completed row here.
5. Put deep rationale in an ADR, detailed evidence in `BuildReports`/milestone QA docs, and stable
   architecture in `Docs/architecture.md`; link it here rather than duplicating it.
6. Never use a `Latest`, `Current`, or `Accepted` filename as status by itself. Read its result,
   timestamp, capture conditions, and commit provenance.

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
