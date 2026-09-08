# Procedural Valley V2: first Unity inspection candidate

KEEP EXPERIMENTAL until twelve-pillar preview is inspected in Unity under actual arena material/light. Root owns import and the Unity lease. This stage has no actual Assets writes, no generated assets and no scene edits.

## Integration

Apply after/Assets against before/Assets hashes in mapping.json (ten C# files, five replacements plus five additions). Existing DistantBackdrop/Profile/ProceduralRockMesh are extended; no competing runtime environment is introduced. New data helpers depend on Mathematics only, no GameObject/state authority. Authoring-only allocations are bounded by 2-4 parts per pillar, max14 bevel attempts/part,2-5 coarse chips/part,3-7 pillars/group,12ground+6floating groups,24 placement attempts/group. Meshes generated only by explicit authoring tools. Existing baked shared meshes persist; no mesh regeneration in Update.

1. Run Elemental/QA/Procedural Valley Geometry Edit.
2. Run Elemental/Environment/Procedural Valley/1 Preview Twelve Pillars. This creates a4x3 grid outside gameplay bounds under the existing backdrop owner, no new light/camera/material. Existing arena shader/material is used directly. Select/frame this preview in Scene View or a QA capture camera; production camera is untouched. Inspect geometry without atmosphere before proceeding. Report JSON goes to BuildReports/ProceduralValleyV2.
3. Only after visual gate: 2 Bake Group Meshes. Existing Massif_0..5 and Island_6..9 LOD mesh asset identities survive CopySerialized; Island_10/11 are additive. Profile arrays update to6ground+6floating families; exact RumbleArenaSandstone shared asset is assigned, no material mutation or cloning.
4. Inspect one ground and one floating bank mesh before 3 Regenerate Same Seed. Rebuild fills two asymmetrical side bands, not a ring. It preserves the current authored center/up/view-direction frame. Clear Generated removes only owned generated/preview transforms; baked shared assets remain. New Placement Seed does not change geometry or motion seed.

## Geometry contract

Each convex part starts as an asymmetrical5-8-plane prism, tilted top/bottom, mild taper and lean. True halfspace clipping closes a cap with outward normal; deduplication and collinear cleanup use scale-relative epsilon. Every intermediate candidate verifies each polygon plane, convex halfspaces, one oppositely directed partner for each edge, nonzero outward triangle area and positive signed volume. Optional invalid/deep cuts are skipped and counted, keeping prior solid. Bevel planes derive from actual adjacent faces; chips derive from the incident vertex normal cone. Compound parts overlap; this is not claimed to be a Boolean union surface. Internal overlaps are deliberate hidden seams.

Each face emits independent vertices with one explicit inverse-transpose transformed normal. All triangles of that face use the same color. Palette variation is3% per part encoded in vertices; exact existing material decides how it consumes attributes. No shader changes. High/low LOD share every part transform and coarse planes/chips. Low omits bevels; group fitting uses coarse descriptor bounds for both LODs.

## Actual user intent adaptation

The user explicitly wants the small playable planet floating inside a separate distant valley. Therefore kilometer-scale external bands use planetCenter+stagingUp+tangent frame and bases below the cloud floor. The document's optional sphere exponential-map placement is intentionally not applied to the55.1m combat planet: that would wrap bands onto gameplay terrain. No surface authority, collider or gravity source is invented. Sphere/box exclusion checks protect arena and authored camera volume; floating bounds include rotation/drift padding and reject overlap. Ground groups may overlap deliberately at their common lower mass.

Motion is absolute, deterministic and whole-group,0.08-0.4m vertical with20-40% lateral motion,10-22s vertical periods and longer independent horizontal periods. Regenerate opts into unscaled environmental time; explicit environmentPaused/environmentSpeed remain separate. ReducedMotion preferences still restore baselines. The staged production fixture now asserts V2 motion continues with timeScale zero, then asserts explicit environmentPaused stops it. Legacy mode retains its old scaled-time assertion. Pause is cleared before the warmed allocation loop; no-op profiling is not accepted.

## Evidence and remaining gates

compile/report.json: all4actualUnityRoslyn assemblies pass. Pure .NET oracle executes the exact Mathematics geometry code without Unity or rendering. oracle/report.json records multi-seed closure/volume/LOD validation; not a Unity performance or visual claim. Published mesh normals and deterministic arrays also have Edit tests. Preliminary48pillar-seed run: high152-368triangles; low48-148, with9 rejected optional detail cuts. Low160 cap is explicit versus proposed120 starting budget, to preserve all2-4 compound silhouette volumes. Group counts multiply per-pillar budget; emitted totals are recorded during bake.

Remaining: actual Edit output; twelve-mesh no-fog/light inspection; one-ground/one-floating inspection; menu/game cameras; baked scene reload; full exclusion/rebuild counts; draw calls/CPU/GPU measured in Unity. No claim of production visual acceptance is made from compilation or pure geometry checks.

Expanded exact-code oracle PASS: 672 cases (48 pillar seeds x2 LODs,48 group seeds x6 families x2 LODs),42.771s authoring test run, all closure/positive volume/LOD part-transform/containment gates passed. This is test harness elapsed time, not runtime frame performance.
