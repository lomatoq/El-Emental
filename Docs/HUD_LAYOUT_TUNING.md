# HUD layout tuning

Open **Elemental > UI > Edit HUD Layout**. The saved production theme references
`Assets/Elemental/Content/UI/Frontend/ElementalHudLayout.asset`.

| Inspector section | What it controls |
| --- | --- |
| Health / Energy > Group | Whole bar, icon and value, anchored to the screen |
| Health / Energy > Bar | Gauge geometry relative to Group |
| Health / Energy > Under Bar | Common parent for the icon and number below the gauge |
| Health / Energy > Icon / Value | Independent children relative to Under Bar |
| Navigation > Group | Entire navigation group, anchored to the screen |
| Navigation > Caption / Globe / Legend | Independent children of the navigation group |
| Pause > Button | Visible button and clickable area, anchored to the screen |
| Pause > Icon | Two pause strokes relative to the button |
| Scoreboard | Whole score/timer group, anchored to the screen |

Every element has Anchor, Pivot, Position, Size, Scale and Rotation:

- Anchor is a normalized point in its parent: (0,0) top-left, (.5,.5) center,
  (1,1) bottom-right. For top-level groups the parent is the HUD screen.
- Pivot is the point on the element placed on that anchor, and the origin of
  scale/rotation. Position offsets it in UI pixels: X right, Y down.
- Size sets the layout box. Group size determines its children's anchor space.
  Scale changes the visual size including text/icons and children.
- Rotation is clockwise in degrees, inherited by children.

Example: move both items below health using **Health > Under Bar > Position**;
then move just the number using **Health > Value > Position**. Set each child's
Anchor and Pivot to (.5,.5) to position/rotate it around the group's center.

Inspector edits are applied during Play Mode, including when paused, without
rebuilding the HUD or restarting the round. Edit the asset itself, then use
**Save HUD Layout / Сохранить** (asset edits persist after exiting Play Mode).
Font roles/sizes and colors remain in **Elemental > UI > Select In-Game HUD Theme**.
The new layout controls concern the in-game HUD; they do not reposition the
uGUI main menu. Existing scene and animation assignments are unchanged.

Implementation: plain serialized layout data, a UI Toolkit transform adapter,
revision-driven updates in EarthDuelHud, and separate entrance offsets so the
HUD entrance does not overwrite authored pivots or rotations. Gauge width now
scales its actual painted shape, not just its layout box. Profiler marker:
`Elemental.DuelHud.ApplyLayout`, only emitted when the layout changes or binds.

Validation: `Elemental > QA > HUD Layout Edit` checks independent nested values
survive serialization; `HUD Layout Play` uses the actual HUD, production stylesheet
and a clone of the saved theme at 16:9, 16:10 and 21:9. It checks actual child
anchor transforms, live edits at timeScale=0, globe sizing, pause hit testing
and pointer down/up callback. Reports and captured panels: `BuildReports/HudLayout`.
The clone is destroyed after the test; no acceptance offsets touch the saved layout.

Accepted 2026-09-06: Edit **1/1** at 20:53:22 UTC, Play **1/1** at 20:55:57 UTC.
Captured default and edited panels visually inspected. The recorded change-only
layout application peaked at **0.1149 ms** in Editor; this excludes the subsequent
UI renderer's layout/paint work. No zero-allocation or standalone performance
claim is inferred from that marker.

Windows local alpha rebuilt from the saved arena at **20:57:43 UTC**: Succeeded,
49.99 seconds, 0 errors, 185 existing package shader warnings. Report:
`BuildReports/LocalAlphaSavedArena.json`; output: `Builds/LocalAlpha/ElEmental.exe`.
The standalone executable was rebuilt but not interactively smoke-tested in this
change; the layout captures/pointer checks above ran in Unity Play Mode.

## Multiplayer status

At this change, the local alpha still has Host/Join explicitly disabled. The
MPS/Relay/NGO packages are absent from the active manifest. Prepared Stage 2 work
under `BuildReports/AlphaImplementation/Stage2Pending` is not an integrated online
build and cannot be used to test a real room yet.

After integration, the intended smoke test is two separate builds/profiles:
Host creates a real code, Join enters it, both ready, then verify damage, stones,
wall destruction, result and host disconnect. Two computers on different networks
are required to validate the internet route; two local windows alone are insufficient.
