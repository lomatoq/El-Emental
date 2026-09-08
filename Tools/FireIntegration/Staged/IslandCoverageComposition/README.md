# Visible island composition (candidate pending camera check)

Only ProceduralValleyAuthoring.cs is staged. No actual Assets/Unity writes.

The existing 20 airborne slots are redistributed without adding geometry or changing approved mesh shape/world-up yaw motion. Twelve ground slots and existing valley groups remain untouched. Main uses measured 38-degree camera. Combat candidate assumes authored gameplay 60-degree lens; root must confirm actual live lens before import.

Existing upper islands were around 1500-1900m away with only ~90m mesh height, around five percent of a 60-degree view. Candidate two Combat hero islands are at 420/480m and ~21-24.5% nominal screen height; the third is 14% at720m. Side islands are ~10-11%; upper accents6-7.5%; nearby satellites3.8-4.5%. Main equivalent distribution clears left35% UI, except intentional small side island starts around36%.

The analytical report includes full-yaw swept AABBs with motion padding. All20 candidate island envelopes are mutually disjoint. It does NOT prove ground occlusion/acceptance or visual match; root must regenerate and inspect actual Main/Combat.

Import menu: Elemental/Environment/Procedural Valley/8 Frame Visible Islands And Satellites. It is idempotent, updates existing positions/scales by name, preserves variant/meshes and all32slots, regenerates only owned backdrop and saves profile assets. No scene save or mesh bake is performed.

Offline Elemental.Authoring.Editor compilation: succeeded0errors, existing obsolete API warnings only. InspectActualProjectedBounds.cs is tools-only camera/renderer QA for root-owned Unity_RunCommand. It writes BuildReports/IslandCoverageComposition/{state}-projected-bounds.json including camera right/up and actual renderer bounds.
