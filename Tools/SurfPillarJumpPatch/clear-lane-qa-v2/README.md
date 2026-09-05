# Clear-lane surf-pillar QA v2

Apply after `unobstructed-qa`.

With the physical rival removed, the default heading reaches a nearby arena column
before the 0.55–0.72 second charge ends. `EarthSurfController` correctly breaks the
board, then the router cancels the pending charge; this produced `TryRelease=false`
and `Charge01=0` before any launch.

This test-only patch samples 32 tangent headings with a board-width production
physics sphere cast, validates walkable support every 1.5m, and selects the
direction with the longest real clearance. It requires at
least 5.5m, keeps `surf.Continue(Vector2.up, direction)` active in the direct runtime
hold, and logs state immediately before release. The physical visual test still
uses Shift+W+Space through the shipping input map after setting the same clear aim.

`EarthSurfRuntimeTests` also gains a yielding `UnityTearDown` that unloads any
EarthCoreSlice instance after an assertion failure. This prevents a failed direct
test from making the following ordinary-pillar test observe the stale scene/player.
The physical hold writes `[SurfPillarInputDiag]` on every frame with router owner,
route, charge, surf/support state, motor command and ragdoll mode.

Files to copy over `Assets/Elemental/Tests/PlayMode`:

- `SurfPillarQaLane.cs`
- `EarthSurfRuntimeTests.cs`
- `SurfPillarJumpVisualQaTests.cs`
