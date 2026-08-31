using System;
using UnityEngine;

namespace Elemental.Authoring.Fracture
{
    public enum EarthArenaFractureCause : byte
    {
        Impact = 0,
        MeteorImpact = 1
    }

    [Serializable]
    public struct EarthArenaFractureEntry
    {
        public string structureId;
        public string intactObjectName;
        public string fractureProfile;
        public EarthArenaFractureCause activationCause;
        public bool ordinaryDamageEnabled;
        public bool repairable;
        public bool usesVirtualDormantSupport;
        public EarthFractureAsset fractureAsset;
    }

    [CreateAssetMenu(
        menuName = "Elemental/Arena Fracture Catalog",
        fileName = "EarthArenaFractureCatalog")]
    public sealed class EarthArenaFractureCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private GameObject importedModel;
        [SerializeField] private EarthArenaFractureEntry[] structures =
            Array.Empty<EarthArenaFractureEntry>();
        [SerializeField] private string[] looseRockObjectNames = Array.Empty<string>();
        [SerializeField] private string[] cosmeticRubbleObjectNames = Array.Empty<string>();

        public int SchemaVersion => schemaVersion;
        public GameObject ImportedModel => importedModel;
        public EarthArenaFractureEntry[] Structures => structures;
        public string[] LooseRockObjectNames => looseRockObjectNames;
        public string[] CosmeticRubbleObjectNames => cosmeticRubbleObjectNames;

        public void SetImportedData(
            GameObject model,
            EarthArenaFractureEntry[] configuredStructures,
            string[] looseRocks,
            string[] cosmeticRubble)
        {
            schemaVersion = CurrentSchemaVersion;
            importedModel = model;
            structures = configuredStructures ?? Array.Empty<EarthArenaFractureEntry>();
            looseRockObjectNames = looseRocks ?? Array.Empty<string>();
            cosmeticRubbleObjectNames = cosmeticRubble ?? Array.Empty<string>();
        }

        public bool TryGet(string structureId, out EarthArenaFractureEntry entry)
        {
            if (!string.IsNullOrEmpty(structureId))
            {
                for (int index = 0; index < structures.Length; index++)
                {
                    if (!string.Equals(
                            structures[index].structureId,
                            structureId,
                            StringComparison.Ordinal)) continue;
                    entry = structures[index];
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
