using Elemental.Authoring.Fracture;
using Elemental.Simulation.Structures;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public sealed class EarthFracturePreviewWindow : EditorWindow
    {
        private EarthFractureAsset _asset;
        private Vector2 _scroll;

        [MenuItem("Elemental/Fracture/Preview Baked Earth Structure")]
        public static void Open()
        {
            EarthFracturePreviewWindow window = GetWindow<EarthFracturePreviewWindow>();
            window.titleContent = new GUIContent("Earth Fracture Preview");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _asset = AssetDatabase.LoadAssetAtPath<EarthFractureAsset>(
                EarthFractureBaker.ProductionWallAssetPath);
        }

        private void OnGUI()
        {
            _asset = (EarthFractureAsset)EditorGUILayout.ObjectField(
                "Fracture asset", _asset, typeof(EarthFractureAsset), false);
            if (_asset == null)
            {
                EditorGUILayout.HelpBox("Select or bake an Earth fracture asset.", MessageType.Info);
                if (GUILayout.Button("Bake production wall"))
                    EarthFractureBaker.BakeProductionWallFromMenu();
                return;
            }

            EarthFractureValidationResult validation = EarthFractureValidator.Validate(_asset);
            EditorGUILayout.HelpBox(
                validation.IsValid
                    ? $"Valid schema {_asset.SchemaVersion}: {_asset.PieceCount} pieces, {_asset.BondCount} bonds."
                    : $"Invalid: {validation.Error} at {validation.Index} (graph {validation.GraphError}).",
                validation.IsValid ? MessageType.Info : MessageType.Error);
            using (new EditorGUI.DisabledScope(!validation.IsValid))
            {
                if (GUILayout.Button("Open pure bond graph diagnostic"))
                    EarthFractureGraphDebugWindow.Open();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Pieces", EditorStyles.boldLabel);
            EarthFracturePieceRecord[] pieces = _asset.PieceRecords;
            for (int index = 0; index < pieces.Length; index++)
            {
                EarthFracturePieceRecord piece = pieces[index];
                EditorGUILayout.LabelField(
                    $"P{piece.id:000}  rest {piece.restLocalPosition}  mass {piece.mass:0.##}  " +
                    $"faces {piece.faceFlags}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bonds", EditorStyles.boldLabel);
            EarthFractureBondRecord[] bonds = _asset.BondRecords;
            for (int index = 0; index < bonds.Length; index++)
            {
                EarthFractureBondRecord bond = bonds[index];
                string endpoint = bond.pieceB == EarthBondGraph.WorldPieceIndex
                    ? "world"
                    : $"P{bond.pieceB + 1:000}";
                EditorGUILayout.LabelField(
                    $"B{bond.id:000}  P{bond.pieceA + 1:000} ↔ {endpoint}  " +
                    $"area {bond.contactArea:0.###}");
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
