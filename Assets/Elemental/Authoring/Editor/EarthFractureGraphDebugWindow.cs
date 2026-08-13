using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Small deterministic diagnostic for inspecting directional bond damage and
    /// support-island splits before a Rigidbody adapter exists.
    /// </summary>
    public sealed class EarthFractureGraphDebugWindow : EditorWindow
    {
        private const int PieceCount = 6;
        private const int BondCount = 9;
        private readonly EarthPieceDefinition[] _pieces = new EarthPieceDefinition[PieceCount];
        private readonly EarthPieceState[] _pieceStates = new EarthPieceState[PieceCount];
        private readonly EarthBondDefinition[] _bonds = new EarthBondDefinition[BondCount];
        private readonly EarthBondState[] _bondStates = new EarthBondState[BondCount];
        private readonly EarthBondId[] _broken = new EarthBondId[BondCount];
        private readonly int[] _islandByPiece = new int[PieceCount];
        private readonly bool[] _islandSupported = new bool[PieceCount];
        private readonly int[] _islandPieceCounts = new int[PieceCount];
        private readonly int[] _queue = new int[PieceCount];

        private float _impactX;
        private float _impulseNormal = 7f;
        private float _impulseShear = 2f;
        private float _radius = 1.7f;
        private uint _tick;
        private EarthBondDamageResult _lastDamage;
        private EarthIslandSolveResult _lastIslands;

        [MenuItem("Elemental/Diagnostics/Earth Fracture Graph")]
        public static void Open()
        {
            EarthFractureGraphDebugWindow window = GetWindow<EarthFractureGraphDebugWindow>();
            window.titleContent = new GUIContent("Earth Fracture Graph");
            window.minSize = new Vector2(580f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            BuildSample();
            ResetGraph();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pure fracture graph diagnostic", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Apply a structure-local impact. Bond colour shows health; node colour shows the " +
                "deterministic island. A white ring marks an island still connected to foundation.",
                MessageType.Info);

            _impactX = EditorGUILayout.Slider("Impact X", _impactX, -1.6f, 1.6f);
            _impulseNormal = EditorGUILayout.Slider("Normal impulse", _impulseNormal, -20f, 20f);
            _impulseShear = EditorGUILayout.Slider("Shear impulse", _impulseShear, -20f, 20f);
            _radius = EditorGUILayout.Slider("Radius", _radius, 0.2f, 4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply deterministic impact"))
                    ApplyImpact();
                if (GUILayout.Button("Reset graph"))
                    ResetGraph();
            }

            EditorGUILayout.LabelField(
                $"broken {_lastDamage.NewlyBrokenBondCount}, islands {_lastIslands.IslandCount}, " +
                $"supported {_lastIslands.SupportedIslandCount}, dynamic {_lastIslands.DynamicIslandCount}");

            Rect graphRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(390f));
            EditorGUI.DrawRect(graphRect, new Color(0.075f, 0.08f, 0.09f, 1f));
            DrawGraph(graphRect);
        }

        private void ApplyImpact()
        {
            _tick++;
            var impact = new EarthBondImpact(
                new float3(_impactX, 0.58f, 0f),
                new float3(_impulseNormal, _impulseShear, 0f),
                _radius,
                1f,
                _tick);
            _lastDamage = EarthFractureBatchRunner.ApplyImpact(
                in impact, _bonds, _bondStates, BondCount, _broken);
            SolveIslands();
            Repaint();
        }

        private void ResetGraph()
        {
            _tick = 0;
            for (int index = 0; index < PieceCount; index++)
                _pieceStates[index] = EarthPieceState.Intact;
            for (int index = 0; index < BondCount; index++)
                _bondStates[index] = EarthBondState.Healthy;
            _lastDamage = default;
            SolveIslands();
            Repaint();
        }

        private void SolveIslands()
        {
            _lastIslands = EarthFractureBatchRunner.SolveIslands(
                _pieces,
                _pieceStates,
                PieceCount,
                _bonds,
                _bondStates,
                BondCount,
                _islandByPiece,
                _islandSupported,
                _islandPieceCounts,
                _queue);
        }

        private void DrawGraph(Rect rect)
        {
            Vector2[] nodes =
            {
                NodePosition(rect, -1.2f, 0f),
                NodePosition(rect, 0f, 0f),
                NodePosition(rect, 1.2f, 0f),
                NodePosition(rect, -1.2f, 1.15f),
                NodePosition(rect, 0f, 1.15f),
                NodePosition(rect, 1.2f, 1.15f)
            };

            Handles.BeginGUI();
            for (int bondIndex = 0; bondIndex < BondCount; bondIndex++)
            {
                EarthBondDefinition bond = _bonds[bondIndex];
                Vector2 a = nodes[bond.PieceA];
                Vector2 b = bond.PieceB == EarthBondGraph.WorldPieceIndex
                    ? new Vector2(a.x, rect.yMax - 24f)
                    : nodes[bond.PieceB];
                float damage = _bondStates[bondIndex].AccumulatedDamage;
                Handles.color = _bondStates[bondIndex].Phase == EarthBondPhase.Broken
                    ? new Color(0.95f, 0.22f, 0.14f, 0.45f)
                    : Color.Lerp(new Color(0.28f, 0.75f, 0.42f), new Color(1f, 0.55f, 0.08f), damage);
                Handles.DrawAAPolyLine(
                    _bondStates[bondIndex].Phase == EarthBondPhase.Broken ? 2f : 5f,
                    a,
                    b);
                Vector2 label = Vector2.Lerp(a, b, 0.5f);
                Handles.Label(label + new Vector2(4f, -8f), $"B{bond.Id.Value} {damage:0.00}");
            }

            for (int pieceIndex = 0; pieceIndex < PieceCount; pieceIndex++)
            {
                int island = _islandByPiece[pieceIndex];
                Color islandColor = IslandColor(island);
                Handles.color = islandColor;
                Handles.DrawSolidDisc(nodes[pieceIndex], Vector3.forward, 18f);
                if (island >= 0 && _islandSupported[island])
                {
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(nodes[pieceIndex], Vector3.forward, 22f, 2.5f);
                }
                Handles.color = Color.black;
                Handles.Label(nodes[pieceIndex] + new Vector2(-11f, -8f), $"P{pieceIndex + 1}");
            }

            Vector2 impactPosition = NodePosition(rect, _impactX, 0.58f);
            Handles.color = new Color(0.3f, 0.82f, 1f, 0.85f);
            Handles.DrawWireDisc(impactPosition, Vector3.forward, _radius * 72f, 2f);
            Handles.DrawLine(impactPosition - new Vector2(9f, 0f), impactPosition + new Vector2(9f, 0f));
            Handles.DrawLine(impactPosition - new Vector2(0f, 9f), impactPosition + new Vector2(0f, 9f));
            Handles.EndGUI();
        }

        private void BuildSample()
        {
            float3[] positions =
            {
                new float3(-1.2f, 0f, 0f),
                new float3(0f, 0f, 0f),
                new float3(1.2f, 0f, 0f),
                new float3(-1.2f, 1.15f, 0f),
                new float3(0f, 1.15f, 0f),
                new float3(1.2f, 1.15f, 0f)
            };
            for (int index = 0; index < PieceCount; index++)
            {
                _pieces[index] = new EarthPieceDefinition
                {
                    Id = new EarthPieceId((ushort)(index + 1)),
                    ParentPieceIndex = EarthBondGraph.WorldPieceIndex,
                    Flags = EarthPieceFlags.Structural | EarthPieceFlags.Repairable,
                    RestLocalPosition = positions[index],
                    RestLocalRotation = quaternion.identity,
                    RestLocalScale = new float3(1f),
                    Mass = 2f,
                    Volume = 1f,
                    MaterialId = 1
                };
            }

            SetBond(0, 1, 0, EarthBondGraph.WorldPieceIndex, new float3(-1.2f, -0.35f, 0f), new float3(0f, -1f, 0f), true);
            SetBond(1, 2, 1, EarthBondGraph.WorldPieceIndex, new float3(0f, -0.35f, 0f), new float3(0f, -1f, 0f), true);
            SetBond(2, 3, 2, EarthBondGraph.WorldPieceIndex, new float3(1.2f, -0.35f, 0f), new float3(0f, -1f, 0f), true);
            SetBond(3, 4, 0, 1, new float3(-0.6f, 0f, 0f), new float3(1f, 0f, 0f));
            SetBond(4, 5, 1, 2, new float3(0.6f, 0f, 0f), new float3(1f, 0f, 0f));
            SetBond(5, 6, 3, 4, new float3(-0.6f, 1.15f, 0f), new float3(1f, 0f, 0f));
            SetBond(6, 7, 4, 5, new float3(0.6f, 1.15f, 0f), new float3(1f, 0f, 0f));
            SetBond(7, 8, 0, 3, new float3(-1.2f, 0.575f, 0f), new float3(0f, 1f, 0f));
            SetBond(8, 9, 2, 5, new float3(1.2f, 0.575f, 0f), new float3(0f, 1f, 0f));
        }

        private void SetBond(
            int index,
            ushort id,
            short pieceA,
            short pieceB,
            float3 centroid,
            float3 normal,
            bool foundation = false)
        {
            _bonds[index] = new EarthBondDefinition
            {
                Id = new EarthBondId(id),
                PieceA = pieceA,
                PieceB = pieceB,
                Flags = foundation
                    ? EarthBondFlags.Foundation | EarthBondFlags.Repairable
                    : EarthBondFlags.Repairable,
                LocalCentroid = centroid,
                LocalNormalA = normal,
                ContactArea = foundation ? 1.8f : 0.85f,
                TensileStrength = foundation ? 18f : 7f,
                ShearStrength = foundation ? 24f : 9f,
                CompressionStrength = foundation ? 60f : 28f
            };
        }

        private static Vector2 NodePosition(Rect rect, float x, float y)
        {
            float centerX = rect.center.x;
            float bottom = rect.yMax - 92f;
            return new Vector2(centerX + (x * 120f), bottom - (y * 190f));
        }

        private static Color IslandColor(int island)
        {
            switch (island)
            {
                case 0: return new Color(0.27f, 0.73f, 0.96f);
                case 1: return new Color(0.96f, 0.58f, 0.20f);
                case 2: return new Color(0.57f, 0.86f, 0.36f);
                case 3: return new Color(0.82f, 0.44f, 0.92f);
                default: return Color.gray;
            }
        }
    }
}
