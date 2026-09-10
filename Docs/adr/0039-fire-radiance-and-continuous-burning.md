# Fire radiance, continuous soot and moving flame detail

September10, uncommitted working tree on b646f675.

The six shared stream lamps now sample actual hot gas positions rather than projected floor points. A bounded twelve-light ability presenter samples up to eight active ability regions and four burning surfaces. Seven authored torches retain their own lights. Brightness uses a bounded, smooth two-frequency envelope; no per-frame light allocation or extra shadow maps. Bloom eligibility includes visible fire groups rather than only the column-fire flag.

Ring parcels use stratified angular births and swept circulating motion; ring integration remains45Hz. Bolt gas inherits55% of bolt velocity, leaving an actual transported flame/smoke tail. Collision guards remain authoritative for cosmetic parcels.

Attached burning uses three moving upright tongue cards per each of eight existing burn seats, based on user-provided EpicFires demo masks (see FIRE_EPIC_REFERENCE_ASSETS.md), with cooling dust-material smoke. It replaces spherical burning parcels. Fire scorch decals now span roughly0.55–1.6m, use a more irregular mask, and char radii are0.55–1.1m. Adjacent arena render caches share the same anchored world-space footprint/state via bounded32-collider overlap; this presentation propagation does not inflict damage or ignite neighbors.

Validation so far: four assemblies compile; Native06125/25 EditMode PASS. Native062 6/6 and Native063 3/3 production checks passed (lighting on/off, seam, smoke and projectile tail). Native visual acceptance and performance remain pending; no standalone GPU/GC claim.


## Follow-up: foot matching, brief impacts and ring authority

Foot volume material now retains the hand atlas/body/edge settings, with a hotter fresh white core; foot light energy is halved independently. Attached burning point lamps are reduced from2.8 to0.9 intensity. Brief contacts immediately deposit bounded char; cooling char retains weak ember radiance and still clears smoothly. Impact light fades by0.18s; impact hot emission is0.05s, followed by cooling smoke. Bolt births vary across the travel axis and retain swept collisions.

Ring radius now follows a monotonic decelerating curve shared by authority and presentation. Its old filled-sphere128-collider query could discard all hits in dense arena geometry. Authority now queries24 bounded swept-front capsules and deduplicates accepted contacts in512 preallocated seats. Explicit thermal-shock contact, after the ordinary authority/contact guards, admits immediate ignition for the powerful ring; ordinary stream heating retains its sustained-dose threshold. This does not change cosmetic collision into damage authority.

Native064 selected no tests (short names), not passing evidence. Native065 corrected fully qualified fixtures:36/36 EditMode PASS. Native0665/5 PlayMode PASS: actual opponent ring damage and ignition, lamp activity, flight ascent/fall, hot/cooling bolt transport, char/smoke/fade and held ring. Captures still showed overly smooth flame forms; these passes do not establish visual acceptance. Hovl flipbook accent integration pending; see external Fire3D-reference/shader-audit/AUDIT.md for source analysis. Full visual/performance acceptance remains open.


Transparent Fire has no opaque depth, so post-transparent cinematic DOF treated flame over sky as distant sky and erased detail. The custom DOF pass now runs BeforeRenderingTransparents; opaque scenery retains focus blur, while transparent fire/smoke and later bloom retain their shape. This also leaves other transparent effects sharp, an intentional render-order tradeoff. Native067 isolates the resulting projectile capture before sprite-sheet accents.


## Shared atlas accents

All FireFlowVolumeBackend owners (hands, feet, bolts, rings, ground lines/pillars and seven columns) now prewarm a four-card accent mesh. Three samples follow transported hot gas age bands and one follows cooling soot; two shared8x8 RGBA atlases retain true alpha. Cards use scene depth, two physical contact clip planes, and the existing heat-haze source copy with guarded maximum0.6px distortion. Shared material; fixed arrays; no extra gameplay, physics queries or per-frame managed collections. One extra color draw and one heat draw per active flow: GPU cost remains unmeasured. Column births are64/s with0.28s hot plus0.2s cooling inside the existing32-parcel cap. Native0671/1 PASS for pre-accent DOF ordering; Native0683/3 PASS, but its paused foot capture stopped emission and is not accepted as foot visual evidence. Native0692/2 PASS corrects the capture clock without pausing availability, verifies the actual atlas pixel A/B and captures live foot flames and a close developed bolt. Native0701/1 real projectile impact flash/decay PASS.


Viewed evidence: G05/FireAbilities/20260909-235732-fireball-atlas-close.png shows irregular orange transported lobes, small separated flame and cooling smoke; 20260909-235742-foot-atlas-detail.png shows live bright foot jets and retained ground ember/soot. 20260909-235455-ring-late-coverage.png shows a continuous visible ring around geometry. Its diagnostic sample:935 parcels,24 sectors,1211 cosmetic queries,0 budget stops,22 blocked contacts,8.5626ms CPU step/upload including accents. This is one Editor sample, not a standalone GPU/frame-rate claim or final user art approval.


After Native070 had completed and restored the scene, Unity showed an unrecoverable D3D11 swapchain device reset/removed dialog during final shader-warning validation. Both MCP requests timed out. This is a real Editor stability failure, not a passing performance result; the exact GPU workload cause is not established. The user-authorized recovery closes the failed Editor and reopens the same saved project once. Log preserved at Native070-ImpactFlash/Editor-before-device-reset.log. All native results/captures preceded this reset; no full-plan GPU/GC closure is claimed.

Recovery completed: saved EarthCoreSlice reopened idle, MCP reconnected. FireFlipbookAccent, FireFlowParcel and FireSurfaceTongues all report supported=True and zero shader messages after the mask warning fix. No further heavy PlayMode run after the device reset.

The initial recovery launch used a hidden window; MCP/shader validation worked but native panels remained blank. Closed it gracefully and relaunched normally for the user-facing Editor. No further gameplay tests were run during recovery.

Recovery limitation: the normally relaunched Editor still exposes white native panels, also confirmed through Unity ReadScreenPixel, although MCP, scene state and shader validation respond. Default layout/repaint did not resolve it. Do not claim usable visual Editor recovery or GPU stability. Saved code/results remain intact; no driver/system reset was attempted.
