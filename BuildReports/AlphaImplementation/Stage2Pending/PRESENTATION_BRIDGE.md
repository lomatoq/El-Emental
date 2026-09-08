# Stage 2 local camera and HUD presentation (pending only)

Not imported into live Assets. No Unity or multiplayer acceptance is claimed.

## Explicit authoring integration

Call `OnlinePresentationAuthoring.Install(onlineOwner, binding, actorTwoGraph,
flow, menu, hud, authoredController, additionalCameraDrivers)` after both Actor
records and the dedicated online duel are bound. `actorTwoGraph` is the result
of `OnlineActorGraphAuthoring.CloneOwnerGraph` over the original player, magic
runtime, complete camera owner and required external owner presentation roots.
The clone remains inactive in the saved offline scene. Do not create a new
visible character or replace the user's original controller/profile/animation.

The helper reads the existing serialized references on the passed menu, HUD,
flow and camera controller. It requires exact clone-map counterparts for camera,
listener, brain, gameplay virtual camera, rig/controller/director, charge/DOF,
actor animation/motor, magic/executor/dual-mouse and optional pillar/wave inputs.
Menu virtual camera, actual HUD/flow, readiness, planet and arena are shared.
An actor-two reference still pointing into actor one is rejected. The helper
only adds/configures the bridge, does not save the scene or mutate user refs.
Scene composition owns saving and must provide every additional local camera
presentation driver explicitly (including owner-local lookdev/sonar if present).
Do not pass hardware input or gameplay mutators as additional camera drivers.

Camera, listener, brain, gameplay virtual camera, camera controller, legacy rig,
director, charge/DOF and additional camera drivers must be OUTSIDE
Actor.LocalOnlyControls and EarthMutationControls. The bridge owns this whole
presentation set; gameplay binding owns hardware input and gameplay authority.
This avoids BeginRound re-enabling charge lookdev during the menu blend.

Added pending asmdef references: Unity.Cinemachine for Online; Presentation,
Input and Unity.Cinemachine for Online.Editor. Apply the root HudPatch and
FrontendPatch together: frontend now additionally exposes
SetPresentationCameraDirector and PresentationTransitionSeconds. The latter
reads the existing authored theme duration (currently 0.85 seconds).

## Runtime lifecycle

Awake captures each explicitly authored camera driver's enabled state; no runtime
Find/Resources/global mutable lookup is introduced. Prepared(1/2), emitted after
the real actor/duel prepare, finishes prior menu ownership before switching.
That restores actor one's animation clock, previous DOF/charge settings and
foreground renderer state. It disables both camera graphs, enables the selected
local graph, then sets exactly the selected output camera/listener enabled.
The final ordering handles the existing camera controller's listener-enabling
OnEnable side effect. Only the local gameplay virtual camera remains eligible
alongside the shared menu camera.

HUD Configure uses the selected actor's actual spell/pillar/dual-mouse inputs,
matching online duel and local fighter health/respawn. Client restart is disabled.
Flow preferences target the selected existing director. Menu Configure/Enter
binds that actor's actual EarthAnimationDriver and applies its existing .45
presentation clock. EarthOnlineFrontend.RoundStarted still invokes the real
BeginOnlineMatch: existing Starting -> Combat, theme 0.85s blend and clock .45->1
remain authoritative. The bridge never forces Combat or bypasses readiness.

Ended fires after gameplay restores both actors and offline roots. The bridge
finishes the selected menu's ownership and restores actor-one camera/menu/HUD
references, offline duel and host-capable restart. Frontend's existing leave flow
then returns to Main. Actor-two graph stays in its restored inactive saved state.

## Required actual validation

1. Compile pending full Presentation + Online/Editor with actual imported SDKs.
2. Real host and client: Prepared selects actor1/2; precisely one enabled active
   output camera and listener throughout waiting, menu transition and combat.
3. Both processes capture menus and transition video: actor2 actual EAMM clock
   .45 before Begin; smoothly reaches 1 through the existing 0.85s transition;
   actor1 clock restored to 1 on client. No second eligible gameplay camera.
4. Client UI reads actor2 health/mana/respawn and cannot restart the match;
   navigation and sensitivity reference actor2's real camera/motor.
5. Cancel during waiting, disconnect in Starting, and leave Combat: restore
   original menu/HUD/camera refs; one offline camera/listener; actor2 inactive;
   original player input and bot round work again.
6. Inspect user references before/after installer and end. No live animation
   assets, rig/controller values or authored output/profile references replaced.

Existing parser/runtime-slice compile evidence does not prove these presentation
or two-process acceptance cases. They remain pending root integration/Unity QA.

## Pause and actual session exit (pending addition)

Frontend NetworkPauseRequested(bool) is wired by EarthOnlineFrontend to
binding.SetLocalGameplayInputSuppressed(bool). This changes only the selected
local actor: its motor emits zero input, semantic adapter switches to a persistent
empty override, and its three ability route components are temporarily disabled
with captured enabled states. Host-local surf/resonance are canceled; executor,
physics, remote host actor, replication and match clock continue. The semantic
override survives re-enabling a component during physical recovery. UI hardware
input is not disabled and Time.timeScale is never changed for online pause.

Client pause sends Cancel plus the previously held bits as releases once, clears
predicted tap jump, then sends neutral controls/movement to keep the host clear
of stale input. Resume restores input routing; EndRound clears suppression before
restoring original actor bindings. EarthSemanticInputSuppression has two prepared
Edit cases (OnlinePauseInputTests); they have not run in Unity.

EarthOnlineFrontend remembers an entered online round across Paused, pause
Settings and Ending, including after transport has already stopped. Leave stops
gameplay, awaits actual session.CancelAsync cleanup, then returns Main. Session
callbacks during cleanup cannot prematurely switch screens. Live/pending root
frontend files were not overwritten for pause.

After this addition, Compile-RuntimeSlice.ps1 compiled complete pending
Simulation/Runtime/Input successfully. Output contained seven existing CS0618
warnings in EarthPillarMobility, EarthRuntimeRescueSystems and EarthPlatformPool,
no new warning locations or compilation errors. Online SDK and UI semantic
compilation, pause/resume/disconnect two-process evidence remain pending.

New root-requested four-second countdown is NOT implemented in transport here.
Current BeginRound still starts clock/control before the frontend transition;
a synchronized authority start gate must be integrated separately. Camera final
1.5-second blend and countdown UI belong to root and are not replaced here.


September 6 integration correction: the earlier final paragraph saying countdown is not implemented is superseded. NgoGameplayTransport now sends a shared server-time deadline and begins the simulation only at that deadline; frontend reads SecondsUntilStart. The imported HUD also preserves editable layout and selects per-life win/loss from the local fighter perspective. SDK/two-peer acceptance remains pending.
