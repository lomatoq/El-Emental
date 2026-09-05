using System.Linq;
using Elemental.Authoring;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor.MotionMatching
{
    /// <summary>
    /// Persistent entry point for the drop-and-build workflow. The asset still
    /// owns all serialized recipes; this window simply makes its custom editor
    /// discoverable without requiring the user to hunt for the asset first.
    /// </summary>
    [InitializeOnLoad]
    public sealed class MotionLibraryWindow : EditorWindow
    {
        private const string LibraryPath =
            "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset";
        private const string DatabasePath =
            "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibraryData.asset";
        private const string CharacterModelPath =
            "Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx";
        private const string MixamoRoot = "Assets/ThirdParty/Mixamo/";
        private const string KayKitRoot = "Assets/ThirdParty/KayKit/Animations/";

        private MotionLibraryAsset _library;
        private UnityEditor.Editor _libraryEditor;
        private Vector2 _scroll;

        static MotionLibraryWindow()
        {
            EditorApplication.delayCall += EnsureProductionCatalogAfterReload;
        }

        private static void EnsureProductionCatalogAfterReload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            MotionLibraryAsset library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(LibraryPath);
            if (library == null) return;
            bool legacyEntry = library.clips == null || library.clips.Any(recipe =>
                recipe == null || string.IsNullOrWhiteSpace(recipe.stableId));
            bool missingOverrides = library.transitionOverrides == null ||
                                    library.transitionOverrides.Count < 5;
            if (!legacyEntry && !missingOverrides) return;
            int added = PopulateCuratedProjectMotions(library);
            Debug.Log($"[EAMM] Production catalog auto-migrated after script reload: " +
                      $"{library.clips.Count} motions ({added} added).");
        }

        [MenuItem("Window/Elemental/EAMM Motion Library")]
        [MenuItem("Elemental Suite/Character/Open EAMM Motion Library")]
        public static void Open()
        {
            MotionLibraryWindow window = GetWindow<MotionLibraryWindow>();
            window.titleContent = new GUIContent("EAMM Motion Library");
            window.minSize = new Vector2(430f, 520f);
            window.Show();
            window.Focus();
        }

        [MenuItem("Elemental Suite/Character/Populate Production EAMM Catalog")]
        public static void PopulateProductionCatalog()
        {
            MotionLibraryAsset library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(LibraryPath) ??
                                         CreateDefaultLibrary();
            int added = PopulateCuratedProjectMotions(library);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Selection.activeObject = library;
            Debug.Log($"[EAMM] Production catalog synchronized: {library.clips.Count} motions ({added} added).");
        }

        [MenuItem("Elemental/Authoring/September/Repair EAMM Locomotion Catalog And Rebuild")]
        public static void RepairProductionLocomotionCatalogAndRebuild()
        {
            MotionLibraryAsset library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(LibraryPath);
            if (library == null)
                throw new System.InvalidOperationException($"Missing production motion library: {LibraryPath}");
            int changed = RepairProductionLocomotionCatalog(library);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            MotionLibraryBuilder.Bake(library);
            Debug.Log($"[EAMM] Production locomotion repaired ({changed} recipe changes) and rebuilt.");
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("EAMM Motion Library");
            FindLibrary();
        }

        private void OnDisable()
        {
            if (_libraryEditor != null) DestroyImmediate(_libraryEditor);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Environment-aware Motion Matching", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drop Humanoid clips or FBX files below, validate them, then build the 30 Hz JLPM/EAMM database. " +
                "PlanetMotor keeps gameplay-root authority and EarthFootContactController keeps IK authority.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            MotionLibraryAsset selected = (MotionLibraryAsset)EditorGUILayout.ObjectField(
                "Library", _library, typeof(MotionLibraryAsset), false);
            if (EditorGUI.EndChangeCheck()) SetLibrary(selected);

            if (_library == null)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Create Default Linebreaker Library", GUILayout.Height(34f)))
                    SetLibrary(CreateDefaultLibrary());
                if (GUILayout.Button("Find Existing Library")) FindLibrary();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping Asset")) EditorGUIUtility.PingObject(_library);
                if (GUILayout.Button("Select Asset")) Selection.activeObject = _library;
            }
            if (GUILayout.Button("Populate Curated Project Motions"))
            {
                int added = PopulateCuratedProjectMotions(_library);
                Debug.Log($"[EAMM] Added {added} curated project motions; library now contains {_library.clips.Count} clips.");
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            UnityEditor.Editor.CreateCachedEditor(
                _library,
                typeof(MotionLibraryAssetInspector),
                ref _libraryEditor);
            _libraryEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void FindLibrary()
        {
            MotionLibraryAsset found = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(LibraryPath);
            if (found == null)
            {
                string guid = AssetDatabase.FindAssets("t:MotionLibraryAsset").FirstOrDefault();
                if (!string.IsNullOrEmpty(guid))
                    found = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(AssetDatabase.GUIDToAssetPath(guid));
            }
            SetLibrary(found);
        }

        private void SetLibrary(MotionLibraryAsset library)
        {
            if (_library == library) return;
            if (_libraryEditor != null) DestroyImmediate(_libraryEditor);
            _libraryEditor = null;
            _library = library;
            Repaint();
        }

        private static MotionLibraryAsset CreateDefaultLibrary()
        {
            EnsureFolder("Assets/Elemental/Content/Characters/MotionMatching");
            MotionLibraryAsset library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(LibraryPath);
            if (library != null) return library;

            Object occupant = AssetDatabase.LoadMainAssetAtPath(LibraryPath);
            if (occupant != null)
            {
                if (occupant is global::MotionMatching.MotionMatchingData &&
                    AssetDatabase.LoadMainAssetAtPath(DatabasePath) == null)
                {
                    string error = AssetDatabase.MoveAsset(LibraryPath, DatabasePath);
                    if (!string.IsNullOrEmpty(error))
                        throw new System.InvalidOperationException($"Could not recover overwritten EAMM database: {error}");
                }
                else
                {
                    throw new System.InvalidOperationException(
                        $"Cannot create MotionLibraryAsset because {LibraryPath} is occupied by {occupant.GetType().Name}.");
                }
            }

            library = CreateInstance<MotionLibraryAsset>();
            library.name = "EarthMotionLibrary";
            library.sourceRig = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            library.databaseRate = 30f;
            PopulateCuratedProjectMotions(library);
            AssetDatabase.CreateAsset(library, LibraryPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = library;
            return library;
        }

        private static int PopulateCuratedProjectMotions(MotionLibraryAsset library)
        {
            int before = library.clips.Count;
            RepairProductionLocomotionCatalog(library);
            AddClip(library, "idle.neutral", MixamoRoot + "X Bot@Walking.fbx", "XBot Walk Neutral",
                MotionClipRole.Idle, MotionSemantic.NeutralIdle, 0f, 0f, true);
            AddClip(library, "idle.guard", MixamoRoot + "X Bot@Idle.fbx", null,
                MotionClipRole.Idle, MotionSemantic.GuardedIdle, 0f, 0f, true);
            AddClip(library, "walk.forward.mixamo", MixamoRoot + "X Bot@Walking.fbx", "Walking",
                MotionClipRole.Locomotion, MotionSemantic.WalkForward, 2.4f, 0f, true);
            AddClip(library, "walk.backward.mixamo", MixamoRoot + "X Bot@Walking Backwards.fbx", null,
                MotionClipRole.Locomotion, MotionSemantic.WalkBackward, 1.8f, 180f, true);
            AddClip(library, "pivot.left.mixamo", MixamoRoot + "X Bot@Left Turn.fbx", null,
                MotionClipRole.Pivot, MotionSemantic.PivotLeft, 0f, -90f, false);

            const string basic = KayKitRoot + "Rig_Medium_MovementBasic.fbx";
            AddClip(library, "run.forward.a", basic, "Running_A", MotionClipRole.Locomotion,
                MotionSemantic.RunForward, 5.0f, 0f, true);
            AddClip(library, "run.forward.b", basic, "Running_B", MotionClipRole.Locomotion,
                MotionSemantic.RunForward, 4.6f, 0f, true);
            AddClip(library, "jump.start", basic, "Jump_Start", MotionClipRole.Recovery,
                MotionSemantic.JumpStart, 0f, 0f, false);
            AddClip(library, "jump.loop", basic, "Jump_Idle", MotionClipRole.Recovery,
                MotionSemantic.JumpLoop, 0f, 0f, false);
            AddClip(library, "land.soft", basic, "Jump_Land", MotionClipRole.Recovery,
                MotionSemantic.SoftLand, 0f, 0f, false);

            const string advanced = KayKitRoot + "Rig_Medium_MovementAdvanced.fbx";
            AddClip(library, "run.left", advanced, "Running_Strafe_Left", MotionClipRole.Locomotion,
                MotionSemantic.RunLeft, 4.0f, -90f, true);
            AddClip(library, "run.right", advanced, "Running_Strafe_Right", MotionClipRole.Locomotion,
                MotionSemantic.RunRight, 4.0f, 90f, true);
            AddClip(library, "dodge.forward", advanced, "Dodge_Forward", MotionClipRole.Recovery,
                MotionSemantic.DodgeForward, 0f, 0f, false);
            AddClip(library, "dodge.backward", advanced, "Dodge_Backward", MotionClipRole.Recovery,
                MotionSemantic.DodgeBackward, 0f, 180f, false);
            AddClip(library, "dodge.left", advanced, "Dodge_Left", MotionClipRole.Recovery,
                MotionSemantic.DodgeLeft, 0f, -90f, false);
            AddClip(library, "dodge.right", advanced, "Dodge_Right", MotionClipRole.Recovery,
                MotionSemantic.DodgeRight, 0f, 90f, false);

            const string general = KayKitRoot + "Rig_Medium_General.fbx";
            AddClip(library, "recovery.back", general, "Spawn_Ground", MotionClipRole.Recovery,
                MotionSemantic.RecoverBack, 0f, 0f, false);
            AddClip(library, "impact.light.a", general, "Hit_A", MotionClipRole.Impact,
                MotionSemantic.LightImpact, 0f, 0f, false);
            AddClip(library, "impact.light.b", general, "Hit_B", MotionClipRole.Impact,
                MotionSemantic.MediumImpact, 0f, 0f, false);
            AddClip(library, "magic.gather", general, "PickUp", MotionClipRole.Magic,
                MotionSemantic.Gather, 0f, 0f, false);
            AddClip(library, "magic.release", general, "Throw", MotionClipRole.Magic,
                MotionSemantic.Release, 0f, 0f, false);

            const string ranged = KayKitRoot + "Rig_Medium_CombatRanged.fbx";
            AddClip(library, "magic.lift", ranged, "Ranged_Magic_Raise", MotionClipRole.Magic,
                MotionSemantic.Lift, 0f, 0f, false);
            AddClip(library, "magic.push", ranged, "Ranged_Magic_Shoot", MotionClipRole.Magic,
                MotionSemantic.Push, 0f, 0f, false);
            AddClip(library, "magic.sustain", ranged, "Ranged_Magic_Spellcasting_Long", MotionClipRole.Magic,
                MotionSemantic.Sustain, 0f, 0f, false);
            AddClip(library, "magic.slam", ranged, "Ranged_Magic_Summon", MotionClipRole.Magic,
                MotionSemantic.Slam, 0f, 0f, false);

            AddClip(library, "recovery.front", MixamoRoot + "X Bot@Falling To Roll.fbx", null,
                MotionClipRole.Recovery, MotionSemantic.RecoverFront, 0f, 0f, false);
            AddClip(library, "land.hard", MixamoRoot + "X Bot@Hard Landing.fbx", null,
                MotionClipRole.Recovery, MotionSemantic.HardLand, 0f, 0f, false);
            AddClip(library, "impact.side", MixamoRoot + "X Bot@Hit To Side Of Body.fbx", null,
                MotionClipRole.Impact, MotionSemantic.MediumImpact, 0f, 0f, false);
            AddClip(library, "impact.uppercut", MixamoRoot + "X Bot@Receiving An Uppercut.fbx", null,
                MotionClipRole.Impact, MotionSemantic.MediumImpact, 0f, 0f, false);
            AddClip(library, "magic.pull.mixamo", MixamoRoot + "X Bot@Standing 1H Cast Spell 01.fbx", null,
                MotionClipRole.Magic, MotionSemantic.Pull, 0f, 0f, false);
            AddClip(library, "magic.push.mixamo", MixamoRoot + "X Bot@Standing 1H Magic Attack 03.fbx", null,
                MotionClipRole.Magic, MotionSemantic.Push, 0f, 0f, false);
            AddClip(library, "magic.lift.mixamo", MixamoRoot + "X Bot@Standing 2H Cast Spell 01.fbx", null,
                MotionClipRole.Magic, MotionSemantic.Lift, 0f, 0f, false);
            AddClip(library, "magic.slam.mixamo", MixamoRoot + "X Bot@Standing 2H Magic Area Attack 02.fbx", null,
                MotionClipRole.Magic, MotionSemantic.Slam, 0f, 0f, false);
            AddClip(library, "magic.release.mixamo", MixamoRoot + "X Bot@Standing 2H Magic Attack 03.fbx", null,
                MotionClipRole.Magic, MotionSemantic.Release, 0f, 0f, false);
            AddClip(library, "magic.sustain.mixamo", MixamoRoot + "X Bot@Standing 2H Magic Attack 05.fbx", null,
                MotionClipRole.Magic, MotionSemantic.Sustain, 0f, 0f, false);

            EnsureDefaultOverrides(library);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library.clips.Count - before;
        }

        private static int RepairProductionLocomotionCatalog(MotionLibraryAsset library)
        {
            if (library.clips == null)
                library.clips = new System.Collections.Generic.List<MotionClipRecipe>();
            int before = library.clips.Count;
            library.clips.RemoveAll(recipe => recipe != null &&
                (recipe.stableId == "walk.forward.a" ||
                 recipe.stableId == "walk.forward.b" ||
                 recipe.stableId == "walk.forward.c" ||
                 recipe.stableId == "walk.backward.kaykit" ||
                 recipe.stableId == "walk.crouch" ||
                 recipe.stableId == "walk.sneak"));
            int afterRemoval = library.clips.Count;
            AddClip(library, "walk.forward.mixamo", MixamoRoot + "X Bot@Walking.fbx", "Walking",
                MotionClipRole.Locomotion, MotionSemantic.WalkForward, 2.4f, 0f, true);
            MotionClipRecipe forward = library.clips.FirstOrDefault(recipe =>
                recipe != null && recipe.stableId == "walk.forward.mixamo");
            int rebound = forward != null && forward.clip != null && forward.clip.name == "Walking" ? 1 : 0;
            return before - afterRemoval + rebound;
        }

        private static void AddClip(
            MotionLibraryAsset library,
            string stableId,
            string assetPath,
            string clipName,
            MotionClipRole role,
            MotionSemantic semantic,
            float speed,
            float direction,
            bool loop)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__preview__") &&
                    (string.IsNullOrEmpty(clipName) || candidate.name == clipName));
            if (clip == null) return;
            MotionClipRecipe existing = library.clips.FirstOrDefault(recipe =>
                recipe != null && (recipe.clip == clip || recipe.stableId == stableId));
            if (existing != null)
            {
                existing.stableId = stableId;
                existing.clip = clip;
                existing.role = role;
                existing.semantic = semantic;
                existing.nominalSpeed = speed;
                existing.nominalYaw = role == MotionClipRole.Pivot ? direction : 0f;
                existing.nominalDirection = direction;
                existing.loop = loop;
                return;
            }
            library.clips.Add(new MotionClipRecipe
            {
                stableId = stableId,
                clip = clip,
                role = role,
                semantic = semantic,
                nominalSpeed = speed,
                nominalYaw = role == MotionClipRole.Pivot ? direction : 0f,
                nominalDirection = direction,
                loop = loop
            });
        }

        private static void EnsureDefaultOverrides(MotionLibraryAsset library)
        {
            library.transitionOverrides ??= new System.Collections.Generic.List<MotionTransitionOverride>();
            AddOverride(library, MotionSemantic.RunForward, MotionSemantic.NeutralIdle, 0.085f, 0f, false);
            AddOverride(library, MotionSemantic.WalkBackward, MotionSemantic.WalkForward, 0.075f, 0f, true);
            AddOverride(library, MotionSemantic.PivotLeft, MotionSemantic.RunForward, 0.065f, 0.08f, false);
            AddOverride(library, MotionSemantic.RecoverFront, MotionSemantic.WalkForward, 0.11f, 0f, false);
            AddOverride(library, MotionSemantic.RecoverBack, MotionSemantic.WalkForward, 0.12f, 0f, false);
        }

        private static void AddOverride(
            MotionLibraryAsset library,
            MotionSemantic from,
            MotionSemantic to,
            float halfLife,
            float start01,
            bool preserveGait)
        {
            if (library.transitionOverrides.Any(value => value != null && value.from == from && value.to == to))
                return;
            library.transitionOverrides.Add(new MotionTransitionOverride
            {
                from = from,
                to = to,
                halfLifeSeconds = halfLife,
                destinationStart01 = start01,
                preserveGaitPhase = preserveGait
            });
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
