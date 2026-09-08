from pathlib import Path
lane=Path('El-Emental/Tools/FireIntegration/Staged/StoneLane');a=lane/'after'
f=a/'Assets/Elemental/Presentation/VFX/EarthCosmeticChipLibrary.cs';s=f.read_text().replace('public static Mesh[] Build()','public static Mesh[] Build(Mesh authoredChip = null)').replace('''                Mesh mesh = RumbleRockMeshFactory.Build(
                    RumbleRockMeshFactory.CreateDefaultRecipe(1847 + i * 997, family), "Cosmetic Chip " + i);''','''                Mesh mesh = i == 0 && authoredChip != null ? Object.Instantiate(authoredChip) : RumbleRockMeshFactory.Build(
                    RumbleRockMeshFactory.CreateDefaultRecipe(1847 + i * 997, family), "Cosmetic Chip " + i);'''); f.write_text(s)
f=a/'Assets/Elemental/Presentation/VFX/EarthMaterialFeedbackPresenter.cs';f.write_text(f.read_text().replace('EarthCosmeticChipLibrary.Build();','EarthCosmeticChipLibrary.Build(chipMesh);'))
f=a/'Assets/Elemental/Tests/EditMode/EarthStoneImpactDustTests.cs';f.write_text(f.read_text().replace('.Invoke(null,null);','.Invoke(null,new object[] { null });'))
