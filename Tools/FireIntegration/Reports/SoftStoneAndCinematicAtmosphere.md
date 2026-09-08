# Actual changes and verification — 2026-09-07

Root owns Unity. Native computer interaction remains stopped. Unity MCP disconnected during the second fracture-shading test run: Logs/relay.txt reports client disconnect at17:22:35Z and relay shutdown at17:25:35Z. No editor restart or alternate control path attempted. A reconnect was requested; no new build or final captures can be claimed while disconnected.

## Applied

- Both sandstone materials now use facet contrast .12, shadow floor .66, contact AO strength .68, roughness .96, bevel .13. Original orange palette and authored normals remain. An opt-in .18 twilight sky fill prevents entirely black rock faces near sunset. Fracture triplanar blend normals now follow their captured rest frame, including nonuniform/mirrored scale. Current untextured cut-face darkness was not diagnosed as a UV seam.
- Ordinary impact dust has correct scalar depth-fade properties. Surface wind dust uses opacity .75/brightness1.35, approximately2× the earlier .42/1.2 product, without adding particles. Actual ground Play test passed1/1 at17:12:06Z,24.665s;123particles,53clustered candidates. CPU marker mean.173ms/peak.647ms is one Editor capture, not standalone performance acceptance.
- Distant fog no longer applies the planet's lower-surface opacity band to distant columns. Only the local planet underside retains that forced seal. Physical height fog can reach full opacity continuously; falloff75m/density.012 replaces45m/.018. Day upper air tint .75/.865/.98 is slightly bluer; sunset/night palette retained.
- Far art fades in450–850m, still behind near/planet guards. Pale chromatic edge shift has a4px1080p cap, positive light differences only. Tonal dots are13.5px cells with at most.10 relative modulation, to survive existing far DOF. These settings require visual acceptance; they are not proof that the requested look is achieved.
- Existing sunlight air cue now uses40m span/20m height/strength.25. IMPORTANT: an initial root claim that V2 skipped this cue was corrected. ApplyValleyAtmosphere already calls ApplySunDust at its end. No duplicate invocation remains. This is a four-sample stylized air-light cue, not shadow-map volumetric god rays.
- ColumnDecorFlame now uses66% broad body parcels,30% tapered tongues,4% small embers, a wider/lower birth region and restrained gold core. Actual installer ran successfully; four night lamps and Bloom .3 remain. Needs fresh day/night motion review.

## Verified / pending

Before the latest fog/fire revision: CurrentVisualContracts15/15 at17:09:31Z; valley geometry20/20 at17:10:49Z. Current UI Play4/4 at16:38:57Z. Stone shader supported with0messages; revised flame supported. Atmosphere supported with a potential-uninitialized warning; explicit initialization of fallback cloud arrays was subsequently applied but needs Unity recompilation.

Initial fracture suite14/16 at17:13:27Z: failures were stale exact facet.16 and shadowOff expectations. Targeted test update expects user-authorized facet.12 and already-committed shadowOn/receiveTrue (HEAD1235579/postprocessor ContractVersion2/ADR0033); transform checks are unchanged. Repeat run lost connection; NO fresh16/16 result exists.

Three dissolve continuity tests are included in Current Visual Contracts; runtime execution pending. Same-camera cut-face Lit/Albedo/Normals diagnostic command is ready at Staged/StoneSurfaceSoftness/CaptureActualCutSurface.cs; it requires an actual fracture and restores state. Final ground/fog/fire captures, shader warning check, saved-arena build and sequential online validation remain pending connection recovery.

Preservation check after installer: all9983 original scene records retained,10661 total, no unexpected changes. Project settings, package manifest and original HUD layout remain unchanged.

Offline follow-up: all four C# assemblies compiled successfully. ColumnFireBoundsGuard applied: billboard extents constrained to99% of existing CPU padding;600000 sampled corners validated by agent. Final HLSL import/visual check still pending MCP reconnect.

Offline numerical dissolve check:701sampled heights monotonic,max opacity drop .00076241947 per metre; forced far-column band absent across201heights; physical lowerfog/planet seal1, protectedplayable0. Actual shader formula matches oracle. This is not a Unity runtime test. Evidence: Staged/SoftValleyDissolve/Reports/offline-dissolve-validation.json.

2026-09-07 fine halftone follow-up (user reference9f4686b0): actual ValleyAtmosphereV2.hlsl now uses a4px-at1080p staggered grid instead of13.5px random dots. Dot diameter varies continuously .56–2.32px with pre-fog lit-source luminance (smaller in light, larger in shade). Removed arbitrary screen sine and random positive/negative polarity. Low-contrast multiplicative tone retains scene hue; far-depth/near/UI guards unchanged. No new texture samples or render passes. Still before DOF; final visibility and shader compilation require restored Unity MCP and are not claimed verified.

2026-09-07 face-oriented halftone follow-up: rows now rotate with the camera projection of a face tangent derived from reconstructed world-position derivatives. Horizontal faces use a forward tangent fallback; derivatives evaluated before divergent masks. Fine4px grid retained. Bright lit areas gain pale tiny dots while shadows retain tonal dark ink. Far chromatic shift strengthened3.9→5.85px at1080p (cap6), positive-edge mix .72→.95; near/planet/depth guards retained. No extra texture reads or passes. Source applied; Unity shader/runtime verification still pending restored MCP. Grid phase remains screen-space; face orientation follows geometry, not UVs.
