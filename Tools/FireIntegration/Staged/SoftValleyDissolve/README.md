# Soft distant-column dissolve

Actual cause: `lowerTerrain=1-smoothstep(-.25R,.60R,surface.y)` was max-composited onto every distant column. At radius55 this imposes the same47m high horizontal opacity band regardless of distance. Physical height fog was also capped at .78 while this forced band reached1, making its boundary more apparent. The analytic integral itself is continuous; replacing it with noise is unnecessary.

The explicit opaque underside seal now applies only within the planet radial protection envelope. Distant columns use continuous height-fog plus aerial transmittance; only aerial haze keeps its existing cap, while physical fog can continuously reach1. The infinite lower-sky sea remains fully opaque. Near/playable protection is unchanged.

Authored height falloff45→75m and density.018→.012 soften the vertical dissolution. Day upper fog changes only slightly toward blue (.78,.88,.98→.75,.865,.98); blue lower daytime palette unchanged. Sunset/night palette logic untouched. No changes to clouds, stipple, chromatic shift, rays, geometry, camera or gameplay.

IMPORTANT root is editing other sections of ValleyAtmosphereV2.hlsl concurrently. Apply only fog-only.diff hunk to that shader; do not copy staged whole shader over root's far-art changes. Other3files have normal before/after stage baselines.

Four C# assemblies compile0. New3EditMode tests: far columns do not inherit planet height band; physical opacity and underside seal can reach1; integrated column profile is monotonic/continuous and lower sky opaque. Root run these in Unity plus original ValleyAtmosphereV2Tests; shader compile and same-camera day/dusk/night lookdown/column captures remain required. No claims of visual approval before that.
