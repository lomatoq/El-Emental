# Linebreaker character source

`LinebreakerRigged.blend` is the non-destructive project copy of the user-provided
Tripo character source. The original `Bender.blend` in Downloads was not overwritten.

- Runtime export: `Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx`
- Albedo/UV texture: `LinebreakerTexture.png`
- Texture SHA-256: `9D1882DBDF4FF5EA0EB7C4A5C7FE6DAD7B9EF555F1C26D9FB1428EB7C2FA37C8`
- Added deform chains: three helmet-tail bones and two bones for each belt strip
- Helmet ownership: `Secondary_HelmetAnchor` is parented to `mixamorig:Head`;
  `Secondary_HairLock` and `Secondary_Tail_01` are children of that anchor.
- Weight ownership: the combined head/plume island is normalized across the
  hair-lock and three tail bones; both belt strips are independently normalized
  across their two-bone chains.
- Provenance: user-provided/generated asset; no third-party license is asserted by this repository note

The Unity Humanoid mapping uses the renamed `mixamorig:` body bones. Secondary
bones remain outside Mecanim's Humanoid map and are presentation-only.

The checked runtime FBX is exported from `LinebreakerRigged_weighted.blend`.
`Tools/Blender/repair_linebreaker_character_rig.py` validates/repairs hierarchy,
and `Tools/Blender/weight_linebreaker_secondary.py` performs the deterministic
weight pass. Both scripts support dry-run JSON reports; never apply them directly
to `LinebreakerRigged.blend`.
