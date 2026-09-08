# Menu foreground framing candidate

Stage only. No Assets changes or Unity execution. Replay `SetMenuOrbitCandidate.cs` through Unity_RunCommand in Edit mode after acquiring the Unity lease. It edits exactly one existing scene component property, `CinematicMenuCamera.presentationAzimuth`, from 35 to 0 degrees. It refuses changed baselines, ambiguous owners, other scenes, and Play mode. It marks dirty without saving. Undo restores 35. Root must review and save explicitly before a fixture that reloads the saved scene.

## Evidence and scope

`BuildReports/StoneSkin/Main.png` was captured by `StoneSkinPlayTests` after fresh EarthCoreSlice readiness and before BeginBot. The fixture does not create or reposition foreground stones at this point. V2 has not populated the valley; distant V2 preview is not a plausible source of these nearby boulders.

Saved scene `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity` contains dynamic canonical `Magic Push Boulders` (parent transform 119148209), authored by `M3EarthCoreSetup.cs:4908` onward. They have EarthDestructibleDecorRock, Rigidbody, convex collider and stable IDs. They are gameplay objects, not disposable UI dressing.

| Object | Stable ID | Saved position | World AABB min | World AABB max |
|---|---|---|---|---|
| Light Push Boulder | 3551526913 | (-3.8,56.29,3.7) | (-4.7341,55.4214,2.8303) | (-2.8659,57.1586,4.5697) |
| Heavy Push Boulder | 3551526914 | (4.2,56.63,4.1) | (2.6179,55.1858,2.7660) | (5.7821,58.0742,5.4340) |

AABBs calculated from the actual saved mesh local bounds, quaternion and scale. Actor saved position is (-0.25622007,56.818676,-0.11455194), identity rotation. Heavy occupies a conservative horizontal angle interval about 26.9 to 66.3 degrees from the actor; light about -57.1 to -28.7. Existing 35-degree menu orbit points into the heavy sector. Zero degrees points through the gap. Bounds establish a strong candidate, not exact pixel ownership: runtime gravity settling and other arena meshes still require live visual confirmation.

The existing menu owner computes this orbit independently at `CinematicMenuCamera.cs:132`; its countdown uses a separate `countdownAzimuth`. Preserve FoV 38, characterScreenHeight .55, Dutch 10, camera transition ownership, gameplay camera, canonical transforms/materials and collider/physics/network state. Do not expand renderer suppression to solve this.

## Acceptance

Capture actual Main at 1920x1080 after readiness with the proposed authored orbit. Compare full helmet-to-feet visibility and arena backdrop against baseline. Check Settings/Host/Join and reduced-motion Main too. Enter Combat and return to Main to verify ownership restores. No full suite needed merely to prove a scalar artistic candidate; use existing production UI fixture after visual approval. If blocked by a different stone, inspect live projected bounds before selecting another menu-only angle. Do not claim the image passes based solely on static bounds.
