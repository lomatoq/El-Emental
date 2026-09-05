# Player cache source readability repair

Fresh Windows Player evidence rejected 50 plans and produced 26 cold misses while
the same cache accepted 1939 plans in Editor. Inspection found the 12 persistent
`Rock*Collider.asset` sources serialized with `m_ObjectHideFlags: 20`, specifically
`DontSaveInBuild | DontSaveInEditor`, inherited from the runtime mesh factory. Their
scene references therefore do not establish a valid Player source. The shared error
message from scatter covers either a missing collider mesh or an unreadable visual,
so it did not prove that `m_KeepVertices: 0` was a readability failure.

The staged baker clears only `DontSaveInBuild | DontSaveInEditor` on direct persistent
mesh assets discovered from the real fracture/scatter bindings. Imported FBX meshes
remain governed by `ModelImporter.isReadable`. It checks the supported public
`Mesh.isReadable` contract and does not edit Unity's native `m_Keep*` implementation
fields. Geometry, signatures, materials, counts, cache lookup and physics remain
unchanged; runtime validation is not weakened.
`EarthMaterialPassSetup` also normalizes already-existing `Rock*Collider.asset`
objects, so rerunning the owning authoring pass cannot leave the old factory flags
behind.
The same bounded source set is normalized again at the cache transaction boundary,
after native fracture/collider baking and before `SaveAssets`, so those operations
cannot overwrite the final persisted flags.

Generated child meshes also leave runtime cache ownership with `HideFlags.None`
immediately before `AddObjectToAsset`. This is required because Unity rejects a
`DontSaveInEditor` object as a persistent sub-asset; the flag remains protected for
the whole generation phase and is cleared only at the explicit commit boundary.

Integration and validation:

1. Replace `Assets/Elemental/Authoring/Editor/StartupCacheBaker.cs` with the staged
   `after` file; copy the staged `EarthMaterialPassSetup.cs` and
   `RuntimeMeshSerializationProbe.cs` alongside it.
2. Run `Elemental/World/Bake Startup Caches In Current Scene` and save assets/scene.
   Existing valid cache geometry can be reused; the source assets still need saving.
3. Check all `Rock*Collider.asset` sources now serialize `m_ObjectHideFlags: 0`;
   verify `Mesh.isReadable` for V5 physics and scatter visuals with the isolated
   AssetBundle serialization probe before a full Player build.
4. Rebuild with `Elemental/Build/Build Windows Development From Saved Scenes`, then
   run the fresh Player script.
5. Require `Rejected ... plans` and the scatter readability error to be absent;
   readiness must report 1939 baked plans, zero plan misses and 7368/7368 cooked
   meshes. Exercise one scatter rock and a recursive split in Player before accepting.
