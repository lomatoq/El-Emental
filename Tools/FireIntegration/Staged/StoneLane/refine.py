from pathlib import Path
lane=Path('El-Emental/Tools/FireIntegration/Staged/StoneLane'); a=lane/'after'
f=a/'Assets/Elemental/Presentation/VFX/EarthCosmeticChipLibrary.cs'; s=f.read_text().replace('Six cold-created','Four cold-created').replace('new Mesh[6]','new Mesh[4]').replace('i < 2 ? RumbleRockFamily.Slab :\n                    i < 4 ? RumbleRockFamily.Wedge','i == 0 ? RumbleRockFamily.Slab :\n                    i < 3 ? RumbleRockFamily.Wedge'); f.write_text(s)
f=a/'Assets/Elemental/Tests/EditMode/EarthStoneImpactDustTests.cs'; f.write_text(f.read_text().replace('meshes.Length,Is.EqualTo(6)','meshes.Length,Is.EqualTo(4)'))
f=a/'Assets/Elemental/Runtime/Physics/EarthFragmentPool.cs'; f.write_text(f.read_text().replace('specificImpulse, fragment.Radius, fragment.FragmentId);','specificImpulse, fragment.Radius, fragment.FragmentId, fragment.TargetHandle.Generation);'))
f=a/'Assets/Elemental/Runtime/Physics/EarthRockDebrisPool.cs'; f.write_text(f.read_text().replace(')), radius, seed);',')), radius, piece.StableEarthId, piece.TargetHandle.Generation);').replace(')), radius, seed);',')), radius, piece.StableEarthId, piece.TargetHandle.Generation);'))
f=a/'Assets/Elemental/Presentation/VFX/EarthMaterialFeedbackPresenter.cs'; s=f.read_text(); s=s.replace('''            for (int i = 0; i < count; i++)
            {
                float angle = Next() * Mathf.PI * 2f;''','''            float lobeAxis = Next() * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = Next() * Mathf.PI * 2f;
                if (cue.Kind == EarthMaterialFeedbackKind.Impact && Next() < .55f)
                    angle = lobeAxis + (Next() < .68f ? 0f : Mathf.PI) + (Next() - .5f) * 1.5f;'''); f.write_text(s)
print('Updated conservative four-mesh library, deterministic impact lobes, typed generations.')
