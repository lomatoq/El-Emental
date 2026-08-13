using System;
using System.IO;
using Elemental.Authoring.Assets;
using Elemental.Simulation.Capabilities;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Runtime.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ElementalMaterialDefinition = Elemental.Simulation.Materials.MaterialDefinition;

namespace Elemental.Authoring.Editor
{
    public sealed class ElementalSuiteWindow : EditorWindow
    {
        private const string StylePath = "Assets/Elemental/Content/UI/ElementalSuite.uss";
        private VisualElement _content;

        [MenuItem("Elemental/Tools/Open Elemental Suite")]
        public static void Open()
        {
            var window = GetWindow<ElementalSuiteWindow>();
            window.titleContent = new GUIContent("Elemental Suite");
            window.minSize = new Vector2(620f, 520f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null) rootVisualElement.styleSheets.Add(style);
            rootVisualElement.AddToClassList("suite-root");

            var header = new VisualElement();
            header.AddToClassList("suite-header");
            var title = new Label("ELEMENTAL // CREATOR SUITE");
            title.AddToClassList("suite-title");
            header.Add(title);
            header.Add(new Label("Abilities, materials, planet labs, budgets and diagnostics in one editable UI Toolkit panel."));
            rootVisualElement.Add(header);

            var toolbar = new Toolbar();
            toolbar.Add(Tab("Overview", ShowOverview));
            toolbar.Add(Tab("Ability Workbench", ShowAbilityWorkbench));
            toolbar.Add(Tab("Earth Magic", ShowEarthMagic));
            toolbar.Add(Tab("Material Lab", ShowMaterialLab));
            toolbar.Add(Tab("World & Space", ShowPlanetLab));
            toolbar.Add(Tab("Diagnostics", ShowDiagnostics));
            rootVisualElement.Add(toolbar);

            _content = new ScrollView();
            _content.AddToClassList("suite-content");
            rootVisualElement.Add(_content);
            ShowOverview();
        }

        private ToolbarButton Tab(string title, Action action)
        {
            var button = new ToolbarButton(action) { text = title };
            button.AddToClassList("suite-tab");
            return button;
        }

        private void BeginPage(string title, string subtitle)
        {
            _content.Clear();
            Label heading = new Label(title);
            heading.AddToClassList("page-title");
            _content.Add(heading);
            _content.Add(new Label(subtitle));
        }

        private void ShowOverview()
        {
            BeginPage("Project cockpit", "The authoring surface is data-driven: assets remain editable in Inspector and JSON, while runtime receives baked immutable data.");
            VisualElement cards = new VisualElement();
            cards.AddToClassList("card-grid");
            cards.Add(Card("10 milestone scenes", "Bootstrap through WebLab are independently playable and remain in Build Settings."));
            cards.Add(Card("3 capability profiles", "NativeHigh, NativeLow and WebLab expose centralized budgets and visible degradation."));
            cards.Add(Card("Typed simulation boundary", "Commands enter; events and snapshots leave. Presentation cannot become authority."));
            cards.Add(Card("One-click evidence", "Validation and bug bundles include version, builds, tests and logs."));
            _content.Add(cards);
            _content.Add(ActionButton("Run project validation", () =>
            {
                string report = ElementalProjectValidator.Validate(out bool valid);
                EditorUtility.DisplayDialog(valid ? "Validation passed" : "Validation failed", report, "OK");
            }));
        }

        private void ShowAbilityWorkbench()
        {
            BeginPage("Ability Workbench", "Select any AbilityRecipeAsset, validate its compiled form, or round-trip it through schema-versioned JSON.");
            var field = new ObjectField("Ability asset") { objectType = typeof(AbilityRecipeAsset), allowSceneObjects = false };
            var summary = new HelpBox("Choose an ability asset.", HelpBoxMessageType.Info);
            field.RegisterValueChangedCallback(_ => RefreshAbility(field, summary));
            _content.Add(field);
            _content.Add(summary);
            VisualElement row = Row();
            row.Add(ActionButton("Validate", () => RefreshAbility(field, summary)));
            row.Add(ActionButton("Export JSON", () => ExportAbility(field.value as AbilityRecipeAsset)));
            row.Add(ActionButton("Import JSON", () => ImportAbility(field.value as AbilityRecipeAsset, summary)));
            _content.Add(row);
        }

        private static void RefreshAbility(ObjectField field, HelpBox summary)
        {
            if (field.value is not AbilityRecipeAsset asset)
            {
                summary.text = "Choose an ability asset.";
                summary.messageType = HelpBoxMessageType.Info;
                return;
            }
            try
            {
                CompiledAbilityRecipe recipe = new AbilityCompiler().Compile(asset.Bake());
                summary.text = $"VALID // ID {recipe.Id.Value} // {recipe.Selector} → {recipe.Geometry} // {recipe.Operators.Length} operators // radius {recipe.Radius:0.##} // strength {recipe.Strength:0.##}";
                summary.messageType = HelpBoxMessageType.Info;
            }
            catch (Exception exception)
            {
                summary.text = "INVALID // " + exception.Message;
                summary.messageType = HelpBoxMessageType.Error;
            }
        }

        private static void ExportAbility(AbilityRecipeAsset asset)
        {
            if (asset == null) return;
            string path = EditorUtility.SaveFilePanel("Export ability JSON", Application.dataPath, asset.name + ".json", "json");
            if (!string.IsNullOrWhiteSpace(path)) File.WriteAllText(path, AbilityRecipeJsonCodec.Export(asset));
        }

        private static void ImportAbility(AbilityRecipeAsset asset, HelpBox summary)
        {
            if (asset == null) return;
            string path = EditorUtility.OpenFilePanel("Import ability JSON", Application.dataPath, "json");
            if (string.IsNullOrWhiteSpace(path)) return;
            Undo.RecordObject(asset, "Import Elemental ability JSON");
            if (!AbilityRecipeJsonCodec.TryImport(File.ReadAllText(path), asset, out string error))
            {
                summary.text = "IMPORT REJECTED // " + error;
                summary.messageType = HelpBoxMessageType.Error;
                return;
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            summary.text = "JSON imported and compiled successfully.";
            summary.messageType = HelpBoxMessageType.Info;
        }

        private void ShowMaterialLab()
        {
            BeginPage("Material Lab", "Create editable definitions from safe presets, then tune density, thermal thresholds, latent heat, fuel and tags in the Inspector.");
            var selected = new ObjectField("Material definition") { objectType = typeof(MaterialDefinitionAsset), allowSceneObjects = false };
            var status = new HelpBox("Select an asset or create a preset.", HelpBoxMessageType.Info);
            selected.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is not MaterialDefinitionAsset asset) return;
                try
                {
                    ElementalMaterialDefinition data = asset.Bake();
                    status.text = $"VALID // ID {data.Id.Value} // density {data.Density:0.##} kg/m³ // melt {data.MeltTemperature:0.##} °C // boil {data.BoilTemperature:0.##} °C // {data.Tags}";
                    status.messageType = HelpBoxMessageType.Info;
                }
                catch (Exception exception) { status.text = exception.Message; status.messageType = HelpBoxMessageType.Error; }
            });
            _content.Add(selected);
            _content.Add(status);
            VisualElement row = Row();
            row.Add(ActionButton("Create Water", () => CreateMaterialPreset("Water", ElementalMaterialDefinition.Water)));
            row.Add(ActionButton("Create Brittle Rock", () => CreateMaterialPreset("BrittleRock", ElementalMaterialDefinition.BrittleRock)));
            row.Add(ActionButton("Create Fuel", () => CreateMaterialPreset("Fuel", ElementalMaterialDefinition.Fuel)));
            _content.Add(row);
        }

        private void ShowEarthMagic()
        {
            BeginPage(
                "Earth Magic tuning",
                "Live profiles for structures, vector-field strength, wave geometry and landing cushioning. Changes stay editable as project assets.");
            AddProfileEditor<EarthVectorFieldProfile>(
                "RMB vector field + speed limits",
                "Assets/Elemental/Content/Profiles/EarthVectorFieldProfile.asset");
            AddProfileEditor<EarthWallProfile>(
                "Wall fracture + cohesion",
                "Assets/Elemental/Content/Profiles/EarthWallProfile.asset");
            AddProfileEditor<EarthRockProfile>(
                "Rock growth + shatter",
                "Assets/Elemental/Content/Profiles/EarthRockProfile.asset");
            AddProfileEditor<EarthPillarWaveProfile>(
                "Pillar wave sector + power",
                "Assets/Elemental/Content/Profiles/EarthPillarWaveProfile.asset");
            AddProfileEditor<EarthPlatformProfile>(
                "Gesture platform area + height",
                "Assets/Elemental/Content/Profiles/EarthPlatformProfile.asset");
            AddProfileEditor<EarthLandingCushionProfile>(
                "Landing prediction + cushioning",
                "Assets/Elemental/Content/Profiles/EarthLandingCushionProfile.asset");
            AddProfileEditor<EarthHoverProfile>(
                "Stable stone hover",
                "Assets/Elemental/Content/Profiles/EarthHoverProfile.asset");
            AddProfileEditor<EarthGravityWellProfile>(
                "MMB gravity grip + fracture",
                "Assets/Elemental/Content/Profiles/EarthGravityWellProfile.asset");
            _content.Add(ActionButton("Open playable Earth Core", () => OpenScene(M3EarthCoreSetup.EarthCoreScenePath)));
        }

        private void AddProfileEditor<T>(string title, string path) where T : ScriptableObject
        {
            Label heading = new Label(title);
            heading.AddToClassList("card-title");
            _content.Add(heading);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                _content.Add(new HelpBox($"Missing profile: {path}", HelpBoxMessageType.Error));
                return;
            }

            var objectField = new ObjectField("Profile asset")
            {
                objectType = typeof(T),
                allowSceneObjects = false,
                value = asset
            };
            objectField.SetEnabled(false);
            _content.Add(objectField);
            _content.Add(new InspectorElement(new SerializedObject(asset)));
            _content.Add(ActionButton("Ping asset", () =>
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }));
        }

        private static void CreateMaterialPreset(string name, ElementalMaterialDefinition definition)
        {
            const string folder = "Assets/Elemental/Content/Materials/Definitions";
            if (!AssetDatabase.IsValidFolder("Assets/Elemental/Content/Materials"))
                AssetDatabase.CreateFolder("Assets/Elemental/Content", "Materials");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Elemental/Content/Materials", "Definitions");
            var asset = CreateInstance<MaterialDefinitionAsset>();
            asset.Configure(in definition);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void ShowPlanetLab()
        {
            BeginPage("World & Space", "Planet generation, scaled-space celestial motion, atmosphere, meteors, presentation and performance budgets.");
            AddProfileEditor<PlanetWorldProfile>(
                "Planet // radius, gravity, SDF and mesh budgets",
                M2VoxelPlanetSetup.WorldProfilePath);
            _content.Add(ActionButton("Apply / Rebuild World", () =>
            {
                if (EditorApplication.isPlaying)
                {
                    EditorUtility.DisplayDialog(
                        "World rebuild blocked",
                        "Planet radius is immutable during Play Mode. Stop playback, then apply the world profile.",
                        "OK");
                    return;
                }
                M3EarthCoreSetup.Configure();
            }));
            AddProfileEditor<CelestialSystemProfile>(
                "Space & Atmosphere // day, year, moon and scaled-space",
                "Assets/Elemental/Content/Profiles/CelestialSystemProfile.asset");
            AddProfileEditor<AtmosphereProfile>(
                "Atmosphere // Rayleigh, Mie and limb",
                "Assets/Elemental/Content/Profiles/AtmosphereProfile.asset");
            AddProfileEditor<MeteorShowerProfile>(
                "Meteors // distant streaks, physical impacts and crater budget",
                "Assets/Elemental/Content/Profiles/MeteorShowerProfile.asset");
            AddProfileEditor<CharacterPresentationProfile>(
                "Character & Animation // replaceable Humanoid presentation",
                "Assets/Elemental/Content/Profiles/CharacterPresentationProfile.asset");
            AddProfileEditor<EarthPhysicsFeelProfile>(
                "Physics Feel // friction, impact energies and CCD",
                "Assets/Elemental/Content/Profiles/EarthPhysicsFeelProfile.asset");
            VisualElement scenes = Row();
            scenes.Add(ActionButton("Gravity Toy", () => OpenScene(M1GravityToySetup.GravityToyScenePath)));
            scenes.Add(ActionButton("Voxel Planet", () => OpenScene(M2VoxelPlanetSetup.VoxelLabScenePath)));
            scenes.Add(ActionButton("Earth Core", () => OpenScene(M3EarthCoreSetup.EarthCoreScenePath)));
            scenes.Add(ActionButton("Element Lab", () => OpenScene(M6ElementLabSetup.ElementLabScenePath)));
            scenes.Add(ActionButton("WebLab", () => OpenScene(M9WebLabSetup.ScenePath)));
            _content.Add(scenes);

            var profile = new EnumField("Capability profile", CapabilityProfileKind.WebLab);
            var chunks = new IntegerField("Authored active chunks") { value = 64 };
            var fields = new IntegerField("Authored field regions") { value = 16 };
            var fluids = new IntegerField("Authored fluid proxies") { value = 12 };
            var ragdolls = new IntegerField("Authored ragdoll bodies") { value = 4 };
            var result = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _content.Add(profile); _content.Add(chunks); _content.Add(fields); _content.Add(fluids); _content.Add(ragdolls); _content.Add(result);
            void Refresh()
            {
                CapabilityProfileData data = Profile((CapabilityProfileKind)profile.value);
                bool fits = chunks.value <= data.Budgets.ActiveChunks && fields.value <= data.Budgets.FieldRegions &&
                    fluids.value <= data.Budgets.FluidProxies && ragdolls.value <= data.Budgets.RagdollBodies;
                result.text = $"{(fits ? "PASS" : "OVER BUDGET")} // chunks {chunks.value}/{data.Budgets.ActiveChunks} // fields {fields.value}/{data.Budgets.FieldRegions} // fluids {fluids.value}/{data.Budgets.FluidProxies} // ragdolls {ragdolls.value}/{data.Budgets.RagdollBodies}";
                result.messageType = fits ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            }
            profile.RegisterValueChangedCallback(_ => Refresh()); chunks.RegisterValueChangedCallback(_ => Refresh());
            fields.RegisterValueChangedCallback(_ => Refresh()); fluids.RegisterValueChangedCallback(_ => Refresh()); ragdolls.RegisterValueChangedCallback(_ => Refresh());
            Refresh();
        }

        private void ShowDiagnostics()
        {
            BeginPage("Diagnostics + builds", "Run the same validation used by batch evidence, create a portable bug bundle, or trigger a profile build.");
            _content.Add(ActionButton("Validate project", ElementalProjectValidator.ValidateFromMenu));
            _content.Add(ActionButton("Create bug bundle", ElementalBugBundle.CreateFromMenu));
            VisualElement row = Row();
            row.Add(ActionButton("Build Windows", ElementalBuildPipeline.BuildWindows));
            row.Add(ActionButton("Build macOS", ElementalBuildPipeline.BuildMacOS));
            row.Add(ActionButton("Build WebLab", ElementalBuildPipeline.BuildWebLab));
            _content.Add(row);
        }

        private static CapabilityProfileData Profile(CapabilityProfileKind kind) => kind switch
        {
            CapabilityProfileKind.NativeHigh => CapabilityProfileData.NativeHigh,
            CapabilityProfileKind.NativeLow => CapabilityProfileData.NativeLow,
            _ => CapabilityProfileData.WebLab
        };

        private static void OpenScene(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }

        private static VisualElement Card(string title, string body)
        {
            var card = new VisualElement(); card.AddToClassList("suite-card");
            Label heading = new Label(title); heading.AddToClassList("card-title");
            card.Add(heading); card.Add(new Label(body)); return card;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement(); row.AddToClassList("suite-row"); return row;
        }

        private static Button ActionButton(string title, Action action)
        {
            var button = new Button(action) { text = title }; button.AddToClassList("suite-action"); return button;
        }
    }
}
