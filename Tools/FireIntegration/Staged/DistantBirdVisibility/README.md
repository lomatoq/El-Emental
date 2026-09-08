# Distant bird visibility — same bounded flock

Actual user review approved the world but birds were not perceptible. This changes two existing source files only. No bird count, topology, update, physics or network expansion.

- Flocks0 and2 (four birds each, one +Z and one−Z) move from780–1050m to560–650m and height110–140m. Lower elevation keeps these nearer birds inside the known Combat camera's upper field of view rather than above it.
- Near wingspan2.25–2.65m, distant1.3–1.9m. With FoV38, broadside near wing span projects approximately3.9–5.4px at actual785px height, or5.4–7.4px at1080. Orbital distance, banking and flapping naturally vary the silhouette; this is not a guarantee of minimum pixel coverage while edge-on.
- Two other flocks remain at780–1050m to retain scale/depth variety. Existing deterministic seeds, formation offsets, smooth banked orbits, flap/glide cadence and Reduced Motion behavior unchanged.
- Installer updates the existing owned bird material to dark(.025,.032,.040), opaque two-sided unlit. Existing atmospheric depth remains applicable. It previously set color only when first creating the material.
- Still16 birds,128 vertices,96 triangles, one renderer and one bounded mesh upload. No per-frame allocations added.

Import2files, run Elemental/Environment/Install Or Update Distant Birds in actual EarthCoreSlice Edit mode. Only owned bird configuration/material changes; installer marks scene dirty and does not save it automatically. Parent owns reviewed scene save.

Validation: four real Roslyn assemblies PASS. Pure actual-source oracle8640 continuity/bounds samples PASS;100000 evaluations0 managed bytes, max1ms movement.005964m. Original Edit tests compile unchanged. Actual Main/Combat visual capture remains necessary to verify unobstructed sky coverage and contrast; do not claim visibility based on projected full wingspan alone.
