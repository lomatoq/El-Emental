# Armor neck / shoulder / torso coverage patch

## Diagnosis

The production Humanoid takes the `TryBuildHumanoidShellAnchors` path. Its authored
96-piece definition has 18 head plates, 18 torso plates, 8 pelvis plates and the
remaining limb plates. Runtime then appends 16 measured **head-only** fillers.
There are no dedicated neck or shoulder-junction anchors. The three six-stone torso
rings are centered independently on UpperChest, Chest and Spine, so their seams open
when the arms lift or the chest twists.

The existing `20260905T131643563Z/03-armor-profile.png` shows the same failure: open
air below the helmet, exposed shoulder/upper-arm junctions and broad torso holes.
The pose telemetry in that run is healthy, so changing animation or forcing the
assembly clip would address the wrong owner.

## Bounded change

`EarthArmorCoverageShell` adds 28 small physical stones:

- 8 downward-biased collar stones following `Neck` (fallback `UpperChest`);
- 3 outward caps plus 1 inward keybone bridge per shoulder, following live shoulder bones;
- 6 staggered UpperChest and 6 Chest seam stones.

The existing 96 authored stones and their saved `EarthArmorShellDefinition.asset`
remain unchanged. The profile still requests 96 authored stones; the controller
appends fitted head and junction stones after measuring the live Humanoid. All added
stones use the existing pool, material, formation flight, collision, damage and
release path. Their body-relative directions are captured in each owning bone's local
frame, so chest twist and shoulder lift rotate the fitted surface instead of leaving
stones behind in the root frame. They read final bones at execution order 2300 and never write Animator,
pose, root body, cast brace or encumbrance.

Collar directions point slightly down and its radius is capped to 9.5% of Humanoid
scale. Shoulder plates point away from the neck. This fills the silhouette without
putting a slab through the jaw or face.

## Integration

From the repository root, while Unity is idle:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/ArmorCoverageFix/Apply-StagedPatch.ps1
```

The script hashes all replaced files first and refuses to overwrite newer work.
Then refresh Unity once. No scene, prefab, profile asset or shell-definition asset
needs to be regenerated or saved.

## Verification

The staged assemblies compile with the current Unity Bee references:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/ArmorCoverageFix/Validate-StagedPatch.ps1
```

Result at staging time: Simulation, Runtime, EditMode and PlayMode compiled; only
pre-existing Unity obsolete-API warnings were emitted. Unity tests were not run by
this staging task.

Run these menus serially after integration:

1. `Elemental > QA > Armor Coverage > Edit Contract`
   - requires 8/4/4/6/6 zone distribution;
   - collar azimuth gap at most 45 degrees and every collar stone below the jaw;
   - shoulder caps mirrored and directed away from the neck.
2. `Elemental > QA > Armor Coverage > Animated Production Shell`
   - assembles the real production shell;
   - measures physical collider coverage at idle, walk, left turn, right turn and
     settled idle;
   - requires neck gap <= 0.24 m, shoulder gap <= 0.25 m, torso gap <= 0.30 m and
     multiple distinct stones per junction;
   - verifies the existing armor encumbrance remains active.
3. `Elemental > QA > Armor Jump Aim Visual Proof`
   - now captures armored idle front/profile, armored walk front, armored turn
     profile/back and the existing ordinary jump proof;
   - inspect all armored PNGs for continuous collar, both shoulder caps and torso
     seams. The manifest must retain `magicLayerWeight < 0.12` and `castBrace < 0.01`
     in the compact normal pose.

Expected report roots:

- `BuildReports/ArmorCoverageEdit.json`
- `BuildReports/ArmorCoveragePlay.json`
- `BuildReports/ArmorJumpAimVisualProof/<UTC>/ArmorJumpAimVisualManifest.json`

Do not accept collider counts alone. The front/profile/back PNGs must show the added
stones without face penetration, giant floating slabs, or gaps reopening during the
walk and turn poses.

## Runtime collar diagnostic (density v2)

The production run after the 8-stone collar was integrated still reported only three
distinct nearest pieces at one animated neck pose. That number alone cannot tell
whether the collar has a hole: one of the original/head filler stones may win the
nearest query at several samples while all eight collar stones remain close behind.

Apply only the diagnostic test while Unity is idle:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/ArmorCoverageFix/Apply-CoverageDiagnostics.ps1
```

The animated coverage test now records every probe before any coverage assertion. For
each neck, shoulder and torso sample the JSON contains the bone and sample world
positions plus both:

- the nearest plate from the complete armor shell;
- the nearest plate from the expected fitted zone, including its piece ID, closest
  surface point, transform position, lossy scale and collider bounds.

Reports are written to
`BuildReports/EnvironmentAnimationRescue/ArmorCoverageDiagnostics/<UTC>/CoverageDiagnostics.json`
and mirrored to `BuildReports/ArmorCoverageDiagnosticsLatest.json`. Existing coverage
thresholds are intentionally unchanged. Compare `MinimumDistinctNeckPlates` with
`MinimumDistinctExpectedNeckPlates` and the corresponding maximum gap fields before
changing geometry or the diversity gate. A high expected-zone gap is a real collar
placement failure; a small expected-zone gap with higher expected diversity means the
nearest-any diversity gate is being occluded by other valid armor stones and should be
replaced only after the new profile/front/back captures confirm continuous coverage.

### Findings from the 2026-09-05 15:30:58 production run

The failure is geometric rather than a count shortage. Idle and turning poses put five
or six distinct collar plates next to the six neck probes. At the last sampled walk
pose, the complete eight-piece collar contributed only pieces 112 and 119 and its
maximum expected-zone surface distance rose to 0.13859 m. The all-shell query selected
three original/body pieces and remained under 0.028 m, which hid where the dedicated
collar had swung. The matching profile/turn images show the white throat and thin
diagonal collar edges.

All collar pieces used `Neck` as both their positional anchor and orientation frame.
The walk animation therefore pitched the whole ring with the neck. The final repair
keeps the ring centre on the live `Neck` bone but stores its normal/tangent in the
`UpperChest` frame. The collar now follows torso twist while leaving head/neck motion
free. It reuses the same eight stones and does not alter the authored 96-piece shell,
plate density, encumbrance, materials or release behavior.

The visual proof also deactivates the rival bot GameObject only inside the additive QA
scene and restores it in `finally`. Disabling only its controller left its full mesh
and collider between the front camera and the armored player, invalidating the front
captures.

Apply these two bounded source changes while Unity is idle:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/ArmorCoverageFix/Apply-FinalCollarRepair.ps1
```

Re-run Animated Production Shell and Armor Jump Aim Visual Proof. Acceptance still
requires the unchanged physical gap/diversity gates, and the player must be fully
visible in front/profile/walk/turn/back images with no exposed white throat.
