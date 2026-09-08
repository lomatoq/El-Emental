# Live menu clock and centered pause pivot

September 9, 2026; same authorized main change set based on 4b592086.

FrontendFlowController previously held Time.timeScale at zero in Main, Settings, Host and Join as well as match boundaries. Menu pages now release that hold. Readiness, arena restoration, countdown/ending, completed rounds and explicit local pause keep their existing clock ownership; network rounds retain their authority rules. Closed duel controls and the match-ready gate still prevent combat and match-timer progression in the menu. The pure FrontendWorldClockPolicy defines hold ownership without UI or scene dependencies.

Pause-button pivot is centered in the default, exact and reference HUD layouts. Offsets are compensated by half the existing size, preserving the visible position and dimensions; parent anchoring remains at the screen's upper-right. Default construction and StoneHudExactProfile authoring use the same centered origin. The press animation therefore scales around the icon center.

Production verification extends the existing menu-camera path through main, settings, combat, pause, resume and restored main. It observes scaled time, day/night phase, animation-driver state, the closed match clock and actual pause-button world bounds during press.
Final evidence: FrontendFollowupEdit **35/35** at 2026-09-08T23:33:19Z (0.73 s); FrontendLiveClockPlay **1/1** at 2026-09-08T23:39:22Z (29.00 s). The actual scene passes moving day/night and character animation in Main/Settings/restored Main, stable pre-match timer, stopped local pause, centered press (less than 0.2 UI pixels of center drift), and portrait framing after resume/restore. Reframe CPU observations are recorded in BuildReports/MenuCamera/framing.txt; restored-main sample 2.3469 ms. Capture/assert timing waits for the newly visible HUD layout before reading its screen coordinates. Screenshots are in BuildReports/MenuCamera. Editor CPU measurements are not target-device frame-budget guarantees.

Console after verification: zero errors and no compiler warnings. The pre-existing Graphics Ring Buffer space warning recurred during editor scene restoration after the passing run. Owner: editor QA tooling; removal milestone: editor stress-run graphics-buffer configuration. It is not a new gameplay exception.
