using System;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Authoring.Fracture
{
    [Serializable]
    public struct EarthFracturePieceRecord
    {
        public ushort id;
        public short parentPieceIndex;
        public byte hierarchyLevel;
        public EarthPieceFlags flags;
        public Vector3 restLocalPosition;
        public Quaternion restLocalRotation;
        public Vector3 restLocalScale;
        public float mass;
        public float volume;
        public Vector3 localCenterOfMass;
        public byte materialId;
        public Mesh renderMesh;
        public Mesh colliderMesh;
        public EarthPieceFaceFlags faceFlags;
        public byte exteriorSubmesh;
        public byte interiorSubmesh;
        public byte magicMaskChannel;

        public EarthPieceDefinition ToDefinition()
        {
            return new EarthPieceDefinition
            {
                Id = new EarthPieceId(id),
                ParentPieceIndex = parentPieceIndex,
                HierarchyLevel = hierarchyLevel,
                Flags = flags,
                RestLocalPosition = ToFloat3(restLocalPosition),
                RestLocalRotation = new quaternion(
                    restLocalRotation.x,
                    restLocalRotation.y,
                    restLocalRotation.z,
                    restLocalRotation.w),
                RestLocalScale = ToFloat3(restLocalScale),
                Mass = mass,
                Volume = volume,
                LocalCenterOfMass = ToFloat3(localCenterOfMass),
                MaterialId = materialId
            };
        }

        public EarthPieceFaceMetadata ToFaceMetadata()
        {
            return new EarthPieceFaceMetadata
            {
                Flags = faceFlags,
                ExteriorSubmesh = exteriorSubmesh,
                InteriorSubmesh = interiorSubmesh,
                MagicMaskChannel = magicMaskChannel
            };
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }

    [Serializable]
    public struct EarthFractureBondRecord
    {
        public ushort id;
        public short pieceA;
        public short pieceB;
        public EarthBondFlags flags;
        public Vector3 localCentroid;
        public Vector3 localNormalA;
        public float contactArea;
        public float tensileStrength;
        public float shearStrength;
        public float compressionStrength;

        public EarthBondDefinition ToDefinition()
        {
            return new EarthBondDefinition
            {
                Id = new EarthBondId(id),
                PieceA = pieceA,
                PieceB = pieceB,
                Flags = flags,
                LocalCentroid = ToFloat3(localCentroid),
                LocalNormalA = ToFloat3(localNormalA),
                ContactArea = contactArea,
                TensileStrength = tensileStrength,
                ShearStrength = shearStrength,
                CompressionStrength = compressionStrength
            };
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }

    [CreateAssetMenu(menuName = "Elemental/Earth Fracture Asset", fileName = "EarthFractureAsset")]
    public sealed class EarthFractureAsset : ScriptableObject, IEarthFractureAssetRuntimeData
    {
        public const int CurrentSchemaVersion = 2;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private Mesh intactRenderMesh;
        [SerializeField] private Mesh intactColliderMesh;
        [SerializeField] private EarthFracturePieceRecord[] pieces = Array.Empty<EarthFracturePieceRecord>();
        [SerializeField] private EarthFractureBondRecord[] bonds = Array.Empty<EarthFractureBondRecord>();

        public int SchemaVersion => schemaVersion;
        public Mesh IntactRenderMesh => intactRenderMesh;
        public Mesh IntactColliderMesh => intactColliderMesh;
        public int PieceCount => pieces?.Length ?? 0;
        public int BondCount => bonds?.Length ?? 0;
        public EarthFracturePieceRecord[] PieceRecords => pieces;
        public EarthFractureBondRecord[] BondRecords => bonds;

        public void SetBakedData(
            Mesh configuredIntactRenderMesh,
            Mesh configuredIntactColliderMesh,
            EarthFracturePieceRecord[] configuredPieces,
            EarthFractureBondRecord[] configuredBonds)
        {
            schemaVersion = CurrentSchemaVersion;
            intactRenderMesh = configuredIntactRenderMesh;
            intactColliderMesh = configuredIntactColliderMesh;
            pieces = configuredPieces ?? Array.Empty<EarthFracturePieceRecord>();
            bonds = configuredBonds ?? Array.Empty<EarthFractureBondRecord>();
        }

        public Mesh GetPieceRenderMesh(int index)
        {
            return index >= 0 && index < PieceCount ? pieces[index].renderMesh : null;
        }

        public Mesh GetPieceColliderMesh(int index)
        {
            return index >= 0 && index < PieceCount ? pieces[index].colliderMesh : null;
        }

        public EarthPieceFaceMetadata GetPieceFaceMetadata(int index)
        {
            return index >= 0 && index < PieceCount
                ? pieces[index].ToFaceMetadata()
                : default;
        }

        public bool CopyDefinitions(
            EarthPieceDefinition[] pieceDestination,
            EarthBondDefinition[] bondDestination)
        {
            if (pieceDestination == null || bondDestination == null ||
                pieceDestination.Length < PieceCount || bondDestination.Length < BondCount)
            {
                return false;
            }

            for (int index = 0; index < PieceCount; index++)
                pieceDestination[index] = pieces[index].ToDefinition();
            for (int index = 0; index < BondCount; index++)
                bondDestination[index] = bonds[index].ToDefinition();
            return true;
        }
    }
}
