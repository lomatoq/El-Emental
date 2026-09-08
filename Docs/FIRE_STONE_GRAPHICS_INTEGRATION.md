# Fire, stone impacts and graphics integration — 2026-09-07

Working tree: shared `main`, HEAD `1235579`, uncommitted work. Existing neighboring wall, input, sonar, HUD, animation and online changes are preserved. No push is requested. The external kits are implementation references, not authority to replace the project or its gameplay contracts.

## Sources

Unchanged Fire reference: `Tools/FireIntegration/Reference/EL_Emental_Fire_v1`.
ZIP SHA-256 `5249D339CAB0C9DEFE4D43DF3D261838B40269DB67D4C5A221258EDA10D522CF`.

Unchanged graphics reference: `Tools/FireIntegration/Reference/StoneUI/EL_Emental_StoneUI`.
User supplied the local ZIP after the browser download was denied. SHA-256
`45C59D2DE36BA89C1657E92DE51CAD6709F4F61AB284030B353E471B59A4DF88`.
All 90 RGBA images equal their manifest rectangles in the five source atlases;
see `Tools/FireIntegration/Reports/StoneUiArchiveIntegrity.json`.
The SourceOnly image stays in reference materials. The runtime library contains
89 images without duplicate source atlases. Original demo controllers, scene
installers and reference UXML remain archived rather than replacing live UI.

## Implemented Fire contract

Pure bounded simulation owns groups, six field nodes and eight finite contacts.
Lifecycle is Active → Draining → Retired. Runtime sweeps publish canonical
surface identity/generation/revision, shared gravity, moving constraints and
geometry invalidation before presentation. Cosmetic particles never create
damage, heat authority or network state. Existing particles redirect at contact;
impact does not reinitialize the effect.

Actual `.vfx`, `.shadergraph`, profile, shaders and an isolated FireLab scene were
created through installed Unity/package APIs. Native URP VFX requires Linear;
the project's existing Gamma settings were preserved. An explicit Automatic /
GpuVfx / CpuMesh profile selects a bounded Burst/mesh cosmetic implementation
on this host. One mesh per group, persistent particle arrays, scaled clock and
shared swept contact math preserve particle age and identity. Explicit native
selection on unsupported settings reports its capability failure.

Fire is infrastructure and a lab, not a new playable element: the handoff
expressly excludes new moves, hotkeys and damage. No fire attack or network
authority path is invented by this integration.

## Evidence so far

- Reference Python checks: 15/15. These are not Unity verification.
- Actual Fire Play: 4/4 at 07:33:04 UTC, including existing-particle continuity,
  two corner constraints, moving contacts, invalidation, pause/slow clock,
  draining and zero managed allocation. Report `BuildReports/FireLabPresentationPlay.json`.
- Captures: `Logs/FireLab/Captures`, Bloom on/off.
- Standalone 1920×1080, D3D11/Gamma, Ryzen 7 5700G / RTX 4070, eight High groups,
  360 samples, 1538–1611 live particles: summed cosmetic CPU p95 **1.8037 ms**,
  whole-frame managed allocation max **0 B**. Report `BuildReports/FireLabStandalone1080.json`.
  The proposed 0.8 ms CPU target **failed**. An optimization candidate is staged;
  no improved runtime result is claimed yet. GPU timing was unavailable at 1080p,
  not zero. Metal and native Linear runtime are untested on this Windows host.
- Stone policy Edit: 33/33 at 08:04:12 UTC. Corrected physical-drop Play 1/1 at
  08:12:40, contact/compositing 2/2 at 08:16:32, current wall contracts 2/2 at
  08:17:22. Reports: StoneDustEdit, StonePhysicalDropPlay,
  StoneContactCompositingPlay, StoneCurrentWallContractsPlay in BuildReports.
- Runtime finite convex contacts / resolver / FireWorld Edit 19/19 at 08:14:03;
  actual Earth wall fracture and pool identity Play 1/1 at 08:16:01. Reports
  FireConvexFollowupEdit and FireConvexFollowupPlay.
- The earlier StoneDustRegressionPlay suite passed 12/14. Its two failures used
  obsolete assumptions: a velocity ratio above 4 despite the current compressed
  and capped shared mass policy, and three eagerly allocated wall roots despite
  current lazy single-root reuse. Tests now verify policy-derived masses, equal
  impulse/inverse-mass response, actual travel and shell reuse. Both pass; no
  wall physics source was changed to satisfy them.

## Stone presentation

Cosmetic chips use four different meshes and a distribution weighted toward
small pieces. Two asymmetric low dust lobes plus a smaller lifted layer replace
uniform puffs. Low drops use actual mass and closing speed, with bounded source /
generation cooldown; sleeping contacts do not emit repeatedly. Canonical geometry,
repairable piece dimensions, conserved mass and network physics are unchanged.

A contact-scale soft-depth override on the impact dust renderer fixes the saved
material's 0.12�1.5 m fade rejecting small ground particles. Shared material and
broad rise/extraction effects remain unchanged. Dust-only captures now show the
puff independently of chips. Actual 4 cm gravity drops: 145.28 / 304.60 kg at
about 0.790 m/s closing speed emitted exactly 31 / 40 dust and 8 / 10 chips.
Each produced one collision cue and no sleeping repeat; canonical and body mass
were unchanged. Callback brackets measured 0 B and peaks 0.0587 / 0.0723 ms;
these are not whole-frame or GPU measurements. Captures and detailed limits:
BuildReports/StonePhysicalDrop/evidence.json.

## Graphics integration boundaries

Staged UI maps the supplied artwork onto existing uGUI menu buttons/input and
UI Toolkit HUD elements through an optional skin on the existing theme. Manual
HUD transforms, fonts, procedural gauges/globe, callbacks and room authority
remain with their current owners. Import borders, pivots and mask color space
come from the actual manifest. Installation must be idempotent and must not
regenerate the scene or replace the demo backend.

Distant decorative rocks are being adapted to the actual 55.1 m planet radius,
world coordinates and existing sun/night/atmosphere owner. They must not add
physics, network objects, camera following or a second independent fog pass.

UI/environment Unity import, visual checks, final build and protocol 3 online
verification are pending at this checkpoint. Existing protocol 2 reports and a
protocol 3 launch that failed with D3D device removal do not satisfy a fresh
network check.

## Integrated graphics checkpoint (08:40 UTC)

Optional StoneSkin installed; 2 Edit contracts and 1 twice-run installer
idempotence test passed. Production skin/interaction and three-aspect manual
HUD layout passed. The combined Play report is 3/4: the older life-result
fixture exposed a real production canonical-arena restore failure, now owned
by the neighboring task for correction; this is not waived as a UI assertion.

Distant backdrop: 7/7 Edit and 1/1 actual production Play passed; 34 groups and
34 LOD groups, pause/preference binding and 0 B in a warmed 256-call motion loop
(mean 0.005884 ms). Captures are in BuildReports/DistantBackdrop. The first
visual review rejected bland striped blocks and floating ring bottoms despite
passing functional tests. User then specified the EXACT existing arena material,
clouds below, and forthcoming shape functions. Backdrop profile/renderers now
reference the existing RumbleArenaSandstone.mat GUID
91fc0cc0e76ed8348bf172edefa7fd44. No material clone/recolor. Shape refinement is
pending the user functions; independent ValleyArt modifications are archived
DO_NOT_APPLY. A bounded world cloud layer is in preparation.

GraphicsPreservation.json confirms all 9,983 pre-existing scene records unchanged
except the SceneRoots list addition, plus unchanged HUDLayout, existing theme
fields, ProjectSettings and Packages manifest.

Final Fire bounded optimization: FireLabFinal1080.json at08:25:11, same8High
groups/1538�1611live/360samples, cosmetic CPU p95 0.9711 ms, p50 0.8397 ms,
0 B GC. This improves the old1.8037ms but still misses0.8ms even before
world/adapter costs. GPU unavailable. Latest4/4Play08:23:10 passed. Warmer
cohesion improved; detached translucent tongues remain, so neither full
production art nor overall CPU/GPU acceptance is claimed.

## User visual references — 2026-09-07

Five explicit user target images are preserved in
Tools/FireIntegration/Reference/UserVisualTargets/target-1.png through target-5.png.
They establish the desired outcome, not evidence of the current game:
1. Warm sandstone arena against tall irregular floating stone pillars, layered
   atmospheric distance and extensive clouds; readable restrained gold HUD.
2. Dark faceted stone settings curtain with fine gold trim and live arena view.
3. Mode selection with distinctive role icons and layered floating islands.
4. Warm ember defeat treatment with real retry/menu actions.
5. Green/gold victory treatment with actual score and rematch/menu actions.

The backdrop must retain the exact arena material reference. Distant apparent
cooling should come from existing lighting/atmospheric depth, not a replacement
blue stone palette. Clouds occupy the void below the floating planet. Current
procedural shapes are not accepted final art; user intends to supply shape
functions. Do not apply the independent archived ValleyArt shape/palette patch.
Existing manually authored HUD layout and gameplay/network behavior remain
constraints. References do not authorize invented functioning water/air/fire
moves, castle gameplay, or fake multiplayer results. Match-result art may only
bind to real match outcome state.

Cloud sources have now been imported and compile without console errors;
installation and actual visual tests are in progress. ProductionRestore staged
seven-file patch passes the current-source SHA baseline preflight, but has not
been imported while the cloud agent owns Unity.

User additionally authorizes GPT Image generation/redrawing of missing or unsuitable assets from these references, autonomously. Use built-in image_gen and preserve alpha; validate outputs in actual scene/UI. Existing suitable source art need not be regenerated. This is not authorization to use rejected browser download workarounds. At this checkpoint no new generated raster has been produced; UI refinements reuse exact original supplied artwork, cloud volume remains shader-generated.

## Recovery checkpoint — 2026-09-07 09:12 UTC

Latest VisualReferenceUI imported: exact wordmark, role icons, diamond settings
handles and true numeric values. StoneSkinPlay4/4 passed09:06:04; installer
idempotence1/1 passed09:01:49. Varose decorative small text can appear doubled;
read-only diagnosis found no source duplicate/active material underlay. Font
assets remain unchanged; see Reports/VaroseTextDiagnosis.md.

ProductionRestore9files and loading-scene followup1file imported. Edit6/6 passed.
Latest productionPlay1/2 fails BEFORE full reset: authored Light Push Boulder
IsRegistered=false after ordinary KO, unchangedID/resetcount. Possible lawful
fracture is not yet established; neighbor prepared test-only MatterTrace lane
for next run, NOT imported. Do not weaken assertions or call this proven reset
failure. Full final protocol3 build/online pair remains unrun.

Built-in GPT Image generated CloudArt/reference-bank-v1.png,1536x1024 with real
soft alpha. Exact prompt in Staged/CloudArt/prompt.txt, original preserved. Source
and ten fixed distant cards imported via CloudBankCardsLane; actual on/off
captures in Logs/CloudBankCards. Current composition/postprocess hides useful
art detail, so new cards were explicitly disabled under Valley Cumulus Art
Banks before the final successful scene save. Existing cloud volume remains
functional but visually unaccepted (flat white sheets/dark holes); GPU cost
unmeasured. Asset is ready for future composition alongside user's forthcoming
rock functions, not a claim of reference-matched scenery.

Neighbor relayed that user reports Unity crash and is restarting it. ALL Unity,
Play and player calls stopped; no automatic editor recovery or relaunch. Last
successful save was clean EarthCoreSlice outside Play. Offline preservation
check confirms9983old records intact except explicitSceneRoots and two verified
cloud child references under planet;10410records total, no removals or other
mutations. Wait for recovery before MatterTrace/production tests and fresh online
build. Do not run editor Play with paired players. No push performed.

## Retry authorized — 2026-09-07 09:19 UTC

User explicitly requested retry. Unity GetState succeeded, idle; neighboring
owner confirmed no parallel launches. MatterTrace imported/tested09:14:03:
lawful floor collision splits Light Push Boulder into4secondary records, with
673.7081kg/1.264439m3 exactly conserved, before ordinaryKO. Test-only family
assertion fix imported. Latest productionPlay09:18:58 passes life preservation
and reaches actual postEndMatch proof, which FAILS: original identity invalid,
zero live family,4consumedrecords. Neighbor diagnosing startup baseline timing;
no assertion suppression. Network remains unrun until this is resolved.

## Verified local restore and network checkpoint — 2026-09-07

ProductionArenaRestorePlay2/2 passed09:28:30 after startup gate now waits for
scatter completion and seals baseline before physics release. Two actual full
EndMatch/Main/BeginBot cycles, unchanged foreign100kgworld,4sourcefamily records
survive ordinaryKO; original LightPushBoulder673.7081299kg1.2644389m3 restored
asM1:4/M1:7 and usable through real MagicInputController.EarthExecutor. Test
lookup correction follows actual authored separate Magic Runtime object.
StoneSkinPlay4/4 again09:30:10. Originalprotectedfiles check still passes.

Fresh saved arena build09:33:01 succeeded0errors195warnings142.9568s. MCP response
timeout120s did not abort build; authoritative BuildReports JSON proves success.
BASICprotocol3 Run-20260907T093340 PASSED host/client: samebuildEF1CF40DBBA9F577,
world56B5A8196F2D0383;10scombat/movement/input/disconnect true;2acceptedpresses/
releases,0motor/controlrejections,Main/menuArenaRestored/offlineReset1both.
Ownedprocesses closed before nextpair.

Stone-combat Run-20260907T093755 PARTIAL:12clientattempts, aimvisible, noheldbody/
quickprimed/selectedsource onobservedshots, noattributedthrowhit/kill. Some
LooseStone/source15 impacts exist but notselectedForShot, notaccepted as proof.
BothMain/menuArenaRestoredtrue,0reseterrors; ownedprocessesclosed. Neighbor
read-onlydiagnosing probe/actualacquisition, nofakePhysics/source0acceptance.

User has now supplied ProceduralRockValley document and explicitly requested
full UI/HUD reference matching, old backing removal and all relevant supplied
UI animations. New work contract: Reports/ValleyUIV2Contract.md. Geometry,
atmosphere/cloud and UI agents stage independently; root ownsactualUnity.
Previous shape-wait is lifted. These newvisualchanges are NOT in09:33build.
