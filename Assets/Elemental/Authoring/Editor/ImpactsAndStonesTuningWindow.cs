using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Structures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public sealed class ImpactsAndStonesTuningWindow : EditorWindow
    {
        [MenuItem("Elemental/Setup/Apply 52.5mm Wall Bevels")]
        public static void ApplyWiderWallBevels()
        {
            const string path = "Assets/Elemental/Content/Profiles/EarthStoneBevelProfile.asset";
            EarthStoneBevelProfile profile = AssetDatabase.LoadAssetAtPath<EarthStoneBevelProfile>(path);
            if (profile == null) throw new System.InvalidOperationException("The existing stone bevel profile is required: " + path);
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("wallWidthMeters").floatValue = EarthStoneBevelProfile.DefaultWallWidthMeters;
            serialized.FindProperty("wallMaxLocalEdgeFraction").floatValue = EarthStoneBevelProfile.DefaultWallMaxLocalEdgeFraction;
            serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
        }
        private const string ImpactPath = "Assets/Elemental/Content/Profiles/CharacterImpactResponseProfile.asset";
        private const string WallPath = "Assets/Elemental/Content/Profiles/EarthWallProfile.asset";
        private const string RockPath = "Assets/Elemental/Content/Profiles/EarthRockProfile.asset";
        private CharacterImpactResponseProfile _impact;
        private EarthWallProfile _wall;
        private EarthRockProfile _rock;
        private EarthMatterMassPolicyAsset _mass;
        private Vector2 _scroll;
        private float _volume = .1f, _targetMass = 80f, _speed = 10f;
        private bool _showWall, _showRock;
        private UnityEditor.Editor _wallEditor, _rockEditor, _massEditor;
        private static readonly string[] PhysicalFields =
        {
            "localizedHitReaction", "physicalWeakDriveSeconds", "physicalRecoverySeconds",
            "physicalParentTransfer", "mediumStunSeconds", "physicalDriveSpring", "physicalDriveDamping",
            "physicalWeakDriveScale", "physicalMaximumDisplacement", "physicalMaximumAngle",
            "physicalHeadLimits", "physicalTorsoLimits", "physicalArmLimits", "physicalLegLimits",
            "maximumRagdollRise", "maximumRagdollTangentSpeed"
        };

        [MenuItem("Elemental/Tuning/Impacts & Stones")]
        public static void Open() => GetWindow<ImpactsAndStonesTuningWindow>("Impacts & Stones");

        private void OnEnable()
        {
            _impact = AssetDatabase.LoadAssetAtPath<CharacterImpactResponseProfile>(ImpactPath);
            _wall = AssetDatabase.LoadAssetAtPath<EarthWallProfile>(WallPath);
            _rock = AssetDatabase.LoadAssetAtPath<EarthRockProfile>(RockPath);
            _mass = AssetDatabase.LoadAssetAtPath<EarthMatterMassPolicyAsset>(EarthMassPolicyInstaller.AssetPath);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Physical character response", EditorStyles.boldLabel);
            _impact = (CharacterImpactResponseProfile)EditorGUILayout.ObjectField("Shared response profile", _impact, typeof(CharacterImpactResponseProfile), false);
            if (_impact != null)
            {
                var serialized = new SerializedObject(_impact);
                serialized.Update();
                foreach (string field in PhysicalFields)
                {
                    SerializedProperty property = serialized.FindProperty(field);
                    if (property != null) EditorGUILayout.PropertyField(property, true);
                }
                if (serialized.ApplyModifiedProperties()) EditorUtility.SetDirty(_impact);
                EditorGUILayout.HelpBox("Weak hits keep controls. Medium hits interrupt actions for the stun duration. Heavy hits hand the visible pose and velocities to full ragdoll. Units: seconds, metres, degrees.", MessageType.Info);
                if (GUILayout.Button("Apply profile to loaded fighters / save")) InstallLoadedScene();
            }
            DrawMassCalculator();
            EditorGUILayout.Space();
            _showWall = EditorGUILayout.Foldout(_showWall, "Wall bonds and impact transfer", true);
            if (_showWall)
            {
                _wall = (EarthWallProfile)EditorGUILayout.ObjectField("Wall profile", _wall, typeof(EarthWallProfile), false);
                UnityEditor.Editor.CreateCachedEditor(_wall, null, ref _wallEditor);
                if (_wallEditor != null) _wallEditor.OnInspectorGUI();
            }
            _showRock = EditorGUILayout.Foldout(_showRock, "Stone fragmentation", true);
            if (_showRock)
            {
                _rock = (EarthRockProfile)EditorGUILayout.ObjectField("Rock profile", _rock, typeof(EarthRockProfile), false);
                EditorGUILayout.HelpBox("This asset controls splitting and debris. Shared collider-to-mass conversion is the pure policy shown above.", MessageType.None);
                UnityEditor.Editor.CreateCachedEditor(_rock, null, ref _rockEditor);
                if (_rockEditor != null) _rockEditor.OnInspectorGUI();
            }
            if (GUILayout.Button("Save profile assets")) AssetDatabase.SaveAssets();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMassCalculator()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shared stone mass policy", EditorStyles.boldLabel);
            _mass = (EarthMatterMassPolicyAsset)EditorGUILayout.ObjectField("World mass policy", _mass, typeof(EarthMatterMassPolicyAsset), false);
            if (_mass == null)
            {
                EditorGUILayout.HelpBox("Install the shared mass policy to edit production masses.", MessageType.Info);
                if (GUILayout.Button("Install shared stone mass policy")) { EarthMassPolicyInstaller.Install(); OnEnable(); }
                return;
            }
            UnityEditor.Editor.CreateCachedEditor(_mass, null, ref _massEditor);
            if (_massEditor != null) _massEditor.OnInspectorGUI();
            EarthMatterMassProfile policy = _mass.Snapshot;
            EditorGUILayout.LabelField("Physical density", $"{policy.DensityKilogramsPerCubicMetre:F0} kg/m³");
            EditorGUILayout.LabelField("Reference conversion", $"{policy.ReferencePhysicalMassKilograms:F0} physical kg → {policy.ReferenceGameplayMassKilograms:F0} gameplay kg");
            EditorGUILayout.LabelField("Exponent / mass limits", $"{policy.CompressionExponent:F2} / {policy.MinimumGameplayMassKilograms:F0}–{policy.MaximumGameplayMassKilograms:F0} kg");
            _volume = Mathf.Max(.00001f, EditorGUILayout.FloatField("Stone solid volume (m³)", _volume));
            _targetMass = Mathf.Max(.01f, EditorGUILayout.FloatField("Character body mass (kg)", _targetMass));
            _speed = Mathf.Max(0f, EditorGUILayout.FloatField("Closing speed (m/s)", _speed));
            float mass = EarthMatterMassPolicy.ResolveGameplayMass(_volume, in policy);
            float impulse = EarthCharacterImpactSolver.StoneImpulse(mass, _targetMass, _speed);
            float reaction = impulse / _targetMass;
            var outcomeInput = new CharacterOutcomeInput(EarthCharacterImpactSourceKind.LooseStone, 0f, 0f, reaction);
            EditorGUILayout.LabelField("Stone gameplay mass", $"{mass:F2} kg");
            EditorGUILayout.LabelField("Normalized transferred impulse", $"{impulse:F2} N·s");
            EditorGUILayout.LabelField("Character reaction Δv", $"{reaction:F3} m/s — {CharacterOutcomeResolver.Resolve(in outcomeInput)}");
            EditorGUILayout.HelpBox("Policy fields above change new stone masses on the next Play session. Existing matter keeps its mass; children inherit parent shares. Volume is stored independently in m³. The three calculator inputs below the policy are previews only.", MessageType.None);
        }

        [MenuItem("Elemental/Setup/Install Local Physical Hit Response")]
        public static void InstallLoadedScene()
        {
            CharacterImpactResponseProfile profile = AssetDatabase.LoadAssetAtPath<CharacterImpactResponseProfile>(ImpactPath);
            if (profile == null) throw new System.InvalidOperationException("Shared impact profile missing at " + ImpactPath);
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new System.InvalidOperationException("Open the production scene first.");
            int fighters = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (EarthCharacterImpactTarget target in root.GetComponentsInChildren<EarthCharacterImpactTarget>(true))
            {
                HumanoidRagdollRig rig = target.GetComponentInChildren<HumanoidRagdollRig>(true);
                if (rig == null) continue;
                Undo.RecordObject(rig, "Configure local physical hits");
                rig.ConfigureLocalizedReactionProfile(profile);
                var serialized = new SerializedObject(target);
                serialized.FindProperty("responseProfile").objectReferenceValue = profile;
                serialized.ApplyModifiedProperties();
                if (rig.GetComponent<HumanoidLocalizedPhysicsResponse>() == null)
                    Undo.AddComponent<HumanoidLocalizedPhysicsResponse>(rig.gameObject);
                if (Application.isPlaying) rig.LocalizedPhysics?.ConfigureProfile(profile);
                EditorUtility.SetDirty(rig);
                fighters++;
            }
            if (fighters == 0) throw new System.InvalidOperationException("The active scene has no Humanoid impact targets; open EarthCoreSlice.");
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[Local physical hits] Configured {fighters} Humanoid fighters. Eleven proxies per fighter are prewarmed on entering Play.");
        }

        private void OnDisable()
        {
            if (_wallEditor != null) DestroyImmediate(_wallEditor);
            if (_rockEditor != null) DestroyImmediate(_rockEditor);
            if (_massEditor != null) DestroyImmediate(_massEditor);
        }
    }
}
