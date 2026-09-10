# Stylized volume fire: research and experiment

Decision status: prototype, not visually accepted. Retrieved 2026-09-09.
User rejects the current ribbon stream and requests convincing stylized volume.
The original hard-polish document's three-lane suggestion is a starting point,
not a requirement to retain a failed representation.

## Observed failure

Native 1920×1080 captures in `BuildReports/HardPolish/G05/FireVisual` show
parallel bright rails side-on and broad transparent cards in the near-front view.
Changing opacity, adding sections or widening the same ribbons did not establish
depth. Existing functional tests prove admission, range, cover and lifecycle;
they cannot prove good fire art. The actual Linebreaker avatar is the reference.

## Sources and decision

NVIDIA's [GPU Gems 3, chapter 30](https://developer.nvidia.com/gpugems/gpugems3/part-v-physics-simulation/chapter-30-real-time-simulation-and-rendering-3d-fluids)
separates fluid simulation from rendering: a ray traverses a volume, accumulating
its contribution, and an artist-controlled color mapping turns a reaction field
into flame. The chapter also describes integration with scene depth. We adopt
that rendering principle for a bounded procedural field; we do not adopt the
full fluid solver or claim its physical behavior/performance.

[GPU Gems, chapter 39](https://developer.nvidia.com/gpugems/gpugems/part-vi-beyond-triangles/chapter-39-volume-rendering-techniques)
explains emission/absorption accumulation and the sampling cost of volume data.
This supports a genuine thickness cue and explicit sample budget. It does not
establish that any chosen sample count will look good on our hardware.

The developer's [Ignitement breakdown on Unity](https://unity.com/blog/real-time-fluid-simulation-fire-vfx-ignitement-breakdown)
uses a 2D fluid simulation and parallax-style rendering, with deliberate limits
on vertical behavior. It is a strong example of coherent flow and integrated
lighting, but is not evidence of a freely viewable 3D fire volume. Our camera and
hand stream need front/side coverage, so that representation is not the selected
prototype. Its GPU-to-CPU damage coupling also conflicts with our existing
authoritative CPU FireWorld boundary.

[Riot's VFX style guide](https://nexus.leagueoflegends.com/en-us/2017/10/dev-leagues-vfx-style-guide/)
and [artist portfolio guidance](https://www.riotgames.com/en/portfolio-and-reel-suggestions)
emphasize shape, value, color, timing and gameplay readability. Our artistic
interpretation: one connected orange mass, two or three large asymmetrical
tongues, a small gold-white source, controlled sharp/soft edges and sparse
secondary embers. Detailed photographic noise and extra bloom cannot substitute
for that silhouette. These are our art choices, not a claim that Riot uses our
volume technique.

The [Unity URP Cookbook source](https://github.com/NikLever/Unity-URP-Cookbook)
provides an independently inspectable engine-level volume example under MIT.
Its existence supports feasibility in URP, not compatibility or performance of
an untested fire implementation. No third-party code or assets are copied in
this experiment; attribution/license review is required if that changes.

## Options

| Representation | Relevant strength | Limitation here | Decision |
|---|---|---|---|
| Billboards / flipbooks / current ribbons | Cheap, familiar authoring | View-dependent planes remain visible | Reject as primary body |
| Closed animated flame meshes | Strong stylized silhouette, bounded cost | Surface can read as opaque solid; no internal thickness | Reserve simpler fallback |
| Full 3D fluid simulation | Rich flow and obstacle response | Extra simulation/authoring cost, duplicates established world state | Do not introduce |
| Local procedural volume | Actual view-dependent thickness, controllable large shapes | Raymarch fill rate, sampling artifacts, sorting | Selected bounded prototype |

## Prototype boundary

One local proxy volume for the capsule stream, existing group lifecycle and
material/profile selection. A maximum 32–48 samples per ray is an initial budget,
not an accepted setting. Clip entry/exit analytically, stop at opaque scene depth,
and terminate at low transmittance. Handle cameras inside the proxy explicitly.
No new fullscreen pass, framework, physics queries, damage logic or light owner.

Construct a direction-aligned 3D density field with broad tapered lobes. Advect
continuous noise in flow space; vary large lobes before adding fine detail.
Keep the narrow source connected to the hand, retain a readable orange body,
and cool/dissolve the tail. Existing finite contact patches and source range
constrain sample visibility. Their boundaries must not become infinite planes.
Existing sparse secondary particles remain subordinate to the volume.

## Acceptance experiment

Use the saved scene, real avatar/material, native 1080, production camera,
day/night and the original grading/bloom. Record front, side, three-quarter and
camera-inside views plus an orbit clip. Require stable volume/parallax, a clear
non-rail silhouette, no proxy-box edge, no granular smoke appearance and no
overexposed sheet across the character. Verify hand continuity, cover contact,
partial occlusion, movement, release drain, pause and group reuse.

Compare the same scene/camera/seed with body rendering off/on in one standalone
binary. Capture GPU p50/p95/p99 and bounded CPU/native allocation coverage. Test
two playable streams plus decorative column fires. Current GPU target is the
plan's provisional +1.5 ms p95 for added cosmetics; report measured failure
rather than reducing sample count until the picture is visibly broken.

Kill criteria: card/box artifacts, persistent banding or shimmer, loss of cover
occlusion, detached muzzle/tail reset, or unacceptable measured GPU cost after
bounded optimization. A volume shader is not automatically better art. Final
adoption requires inspected moving captures; no current completion claim.
