using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Authoring.Fracture;
using Elemental.Runtime.Geometry;
using Elemental.Simulation.Structures;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public sealed class BrokenCrownArenaModelPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!string.Equals(
                    assetPath,
                    BrokenCrownArenaImporter.ModelPath,
                    StringComparison.Ordinal) &&
                !string.Equals(assetPath, OuterStoneRingImporter.ModelPath, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importConstraints = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.weldVertices = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.Import;
            // These authored columns use the arena's world-space procedural shader;
            // their closed collider proxies intentionally have no UV/tangent stream.
            if (string.Equals(assetPath, OuterStoneRingImporter.ModelPath, StringComparison.Ordinal))
                importer.importTangents = ModelImporterTangents.None;
            importer.addCollider = false;
            importer.useFileScale = true;
            importer.globalScale = 1f;
        }

    }

    public static class BrokenCrownArenaImporter
    {
        public const string ModelPath =
            "Assets/Elemental/Content/Arena/BrokenCrown/BrokenCrownArena.fbx";
        public const string SidecarPath =
            "Assets/Elemental/Content/Arena/BrokenCrown/BrokenCrownArena.fracture.json";
        public const string GeneratedFolder =
            "Assets/Elemental/Content/Arena/BrokenCrown/Generated";
        public const string CatalogPath = GeneratedFolder + "/BrokenCrownArenaCatalog.asset";

        private const float StoneDensityKilogramsPerCubicMetre = 2300f;

        [MenuItem("Elemental/Arena/Rebuild Broken Crown Import")]
        public static void RebuildFromMenu()
        {
            EarthArenaFractureCatalog catalog = Rebuild();
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }

        public static void RebuildFromBatch()
        {
            Rebuild();
        }

        public static EarthArenaFractureCatalog Rebuild()
        {
            EarthArenaFractureCatalog catalog = RebuildPackage(
                ModelPath, SidecarPath, GeneratedFolder, CatalogPath, 8);
            BrokenCrownArenaSceneIntegrator.RepairLoadedArenaRendererSettings();
            return catalog;
        }

        public static EarthArenaFractureCatalog RebuildPackage(
            string modelPath, string sidecarPath, string generatedFolder,
            string catalogPath, int expectedStructures)
        {
            if (!File.Exists(modelPath) || !File.Exists(sidecarPath))
            {
                throw new BuildFailedException(
                    "Broken Crown import requires both BrokenCrownArena.fbx and " +
                    "BrokenCrownArena.fracture.json. Run the Blender arena baker first.");
            }

            Sidecar sidecar = JsonUtility.FromJson<Sidecar>(File.ReadAllText(sidecarPath));
            ValidateSidecar(sidecar, expectedStructures);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport |
                                                  ImportAssetOptions.ForceUpdate);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                throw new BuildFailedException($"Unity could not import {ModelPath}.");

            EnsureFolder(generatedFolder);
            Dictionary<string, Transform> transforms = IndexTransforms(model.transform);
            var entries = new EarthArenaFractureEntry[sidecar.structures.Length];
            for (int index = 0; index < sidecar.structures.Length; index++)
                entries[index] = BuildStructure(sidecar.structures[index], model, transforms, generatedFolder);

            EarthArenaFractureCatalog catalog =
                AssetDatabase.LoadAssetAtPath<EarthArenaFractureCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EarthArenaFractureCatalog>();
                catalog.name = Path.GetFileNameWithoutExtension(catalogPath);
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            catalog.SetImportedData(
                model,
                entries,
                sidecar.looseRocks ?? Array.Empty<string>(),
                sidecar.cosmeticRubble ?? Array.Empty<string>());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[Elemental] Broken Crown import ready: {entries.Length} structures, " +
                $"{CountPieces(entries)} fracture pieces, catalog {catalogPath}.");
            return catalog;
        }

        private static EarthArenaFractureEntry BuildStructure(
            SidecarStructure source,
            GameObject model,
            IReadOnlyDictionary<string, Transform> transforms,
            string generatedFolder)
        {
            Transform frame = string.IsNullOrEmpty(source.frame_object)
                ? model.transform : RequireTransform(transforms, source.frame_object);
            Transform intact = RequireTransform(transforms, source.intact_object);
            Mesh intactMesh = RequireMesh(intact, source.intact_object);
            if (source.pieces == null || source.pieces.Length == 0)
                throw new BuildFailedException($"{source.structure_id} contains no pieces.");

            float averageVolume = 0f;
            for (int index = 0; index < source.pieces.Length; index++)
                averageVolume += Mathf.Max(0.0001f, source.pieces[index].volume_cubic_metres);
            averageVolume /= source.pieces.Length;

            var records = new EarthFracturePieceRecord[source.pieces.Length];
            Matrix4x4 intactToModel = frame.worldToLocalMatrix *
                                       intact.localToWorldMatrix;
            for (int index = 0; index < source.pieces.Length; index++)
            {
                SidecarPiece piece = source.pieces[index];
                if (piece.id != index + 1)
                    throw new BuildFailedException(
                        $"{source.structure_id}: piece IDs must be stable and one-based.");
                Transform renderTransform = RequireTransform(transforms, piece.name);
                Transform colliderTransform = RequireTransform(transforms, piece.collider);
                Mesh renderMesh = RequireMesh(renderTransform, piece.name);
                Mesh colliderMesh = RequireMesh(colliderTransform, piece.collider);
                Matrix4x4 rest = frame.worldToLocalMatrix *
                                 renderTransform.localToWorldMatrix;
                Matrix4x4 pieceToIntact = intactToModel.inverse * rest;
                renderMesh = BakeFractureRenderMesh(
                    source.structure_id,
                    index,
                    renderMesh,
                    intactMesh,
                    pieceToIntact,
                    generatedFolder);
                float volume = Mathf.Max(0.0001f, piece.volume_cubic_metres);
                EarthPieceFlags flags = EarthPieceFlags.Structural;
                if (piece.repairable) flags |= EarthPieceFlags.Repairable;
                if (volume >= averageVolume * 1.35f) flags |= EarthPieceFlags.HeroPiece;
                records[index] = new EarthFracturePieceRecord
                {
                    id = (ushort)piece.id,
                    parentPieceIndex = EarthBondGraph.WorldPieceIndex,
                    hierarchyLevel = 0,
                    flags = flags,
                    restLocalPosition = rest.GetColumn(3),
                    restLocalRotation = rest.rotation,
                    restLocalScale = rest.lossyScale,
                    mass = Mathf.Max(0.1f, volume * StoneDensityKilogramsPerCubicMetre),
                    volume = volume,
                    localCenterOfMass = Vector3.zero,
                    materialId = 1,
                    renderMesh = renderMesh,
                    colliderMesh = colliderMesh,
                    faceFlags = EarthPieceFaceFlags.HasExterior |
                                EarthPieceFaceFlags.HasInterior |
                                EarthPieceFaceFlags.HasMagicMask,
                    exteriorSubmesh = 0,
                    interiorSubmesh = 1,
                    magicMaskChannel = 2
                };
            }

            bool meteorOnly = string.Equals(
                source.damage_mode, "meteor_only", StringComparison.Ordinal);
            EarthFractureBondRecord[] bonds = BuildBonds(
                source,
                frame,
                transforms,
                records,
                meteorOnly);
            string assetPath = generatedFolder + "/" + ToAssetName(source.structure_id) + ".asset";
            EarthFractureAsset asset = AssetDatabase.LoadAssetAtPath<EarthFractureAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EarthFractureAsset>();
                asset.name = source.structure_id + " Fracture";
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.SetBakedData(intactMesh, intactMesh, records, bonds);
            EditorUtility.SetDirty(asset);
            EarthFractureValidationResult validation = EarthFractureValidator.Validate(asset);
            if (!validation.IsValid)
            {
                throw new BuildFailedException(
                    $"{source.structure_id} import failed validation: {validation.Error} " +
                    $"at {validation.Index} (graph {validation.GraphError}).");
            }

            return new EarthArenaFractureEntry
            {
                structureId = source.structure_id,
                intactObjectName = source.intact_object,
                fractureProfile = source.fracture_profile,
                activationCause = meteorOnly
                    ? EarthArenaFractureCause.MeteorImpact
                    : EarthArenaFractureCause.Impact,
                ordinaryDamageEnabled = !meteorOnly,
                repairable = source.repairable,
                usesVirtualDormantSupport = meteorOnly,
                fractureAsset = asset
            };
        }

        private static Mesh BakeFractureRenderMesh(
            string structureId,
            int pieceIndex,
            Mesh importedPiece,
            Mesh intactMesh,
            Matrix4x4 pieceToIntact,
            string generatedFolder)
        {
            string assetName = ToAssetName(structureId) +
                               $"Piece{pieceIndex + 1:000}Render";
            string assetPath = generatedFolder + "/" + assetName + ".asset";
            Mesh generated = EarthHardSurfaceMeshUtility.CreateFractureShadedCopy(
                importedPiece,
                intactMesh,
                pieceToIntact,
                0,
                1,
                assetName);
            if (generated == null)
                throw new BuildFailedException(
                    $"{structureId}: could not preserve normals for piece {pieceIndex + 1}.");

            Mesh persistent = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (persistent == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                return generated;
            }

            EditorUtility.CopySerialized(generated, persistent);
            persistent.name = assetName;
            EditorUtility.SetDirty(persistent);
            UnityEngine.Object.DestroyImmediate(generated);
            return persistent;
        }

        private static EarthFractureBondRecord[] BuildBonds(
            SidecarStructure source,
            Transform frame,
            IReadOnlyDictionary<string, Transform> transforms,
            EarthFracturePieceRecord[] pieces,
            bool meteorOnly)
        {
            int sourceCount = source.bonds?.Length ?? 0;
            int virtualCount = meteorOnly ? pieces.Length : 0;
            var bonds = new EarthFractureBondRecord[sourceCount + virtualCount];
            int output = 0;
            for (int index = 0; index < sourceCount; index++)
            {
                SidecarBond bond = source.bonds[index];
                if (bond.pieceA < 0 || bond.pieceA >= pieces.Length ||
                    bond.pieceB >= pieces.Length)
                {
                    throw new BuildFailedException(
                        $"{source.structure_id}: bond {bond.id} has invalid endpoints.");
                }
                Transform marker = RequireTransform(transforms, bond.marker);
                Vector3 centroid = frame.InverseTransformPoint(marker.position);
                if (bond.pieceB >= 0)
                {
                    centroid = StabilizeBondCentroid(
                        centroid,
                        pieces[bond.pieceA],
                        pieces[bond.pieceB]);
                }
                else
                {
                    centroid = ClampToRestBounds(pieces[bond.pieceA], centroid);
                }
                Vector3 normal = bond.pieceB >= 0
                    ? (pieces[bond.pieceB].restLocalPosition -
                       pieces[bond.pieceA].restLocalPosition).normalized
                    : string.IsNullOrEmpty(source.frame_object) ? Vector3.down : Vector3.back;
                int stableBondId = output + 1;
                bonds[output] = CreateBond(
                    stableBondId,
                    bond.pieceA,
                    bond.pieceB,
                    centroid,
                    normal,
                    bond.contactArea,
                    bond.foundation,
                    source.repairable);
                output++;
            }

            // Meteor floor pieces are dormant under the intact collision proxy. The
            // virtual supports make the immutable graph valid at rest; a MeteorImpact
            // activation adapter breaks all of them atomically with the proxy swap.
            for (int pieceIndex = 0; pieceIndex < virtualCount; pieceIndex++)
            {
                int stableBondId = output + 1;
                bonds[output] = CreateBond(
                    stableBondId,
                    pieceIndex,
                    EarthBondGraph.WorldPieceIndex,
                    pieces[pieceIndex].restLocalPosition,
                    Vector3.down,
                    Mathf.Max(0.015f, Mathf.Pow(pieces[pieceIndex].volume, 2f / 3f) * 0.25f),
                    true,
                    false);
                output++;
            }
            return bonds;
        }

        private static Vector3 StabilizeBondCentroid(
            Vector3 authoredCentroid,
            in EarthFracturePieceRecord pieceA,
            in EarthFracturePieceRecord pieceB)
        {
            // A recursive cut can leave one broad face opposite several smaller
            // faces. The Blender marker remains useful authoring data, but its
            // face-centroid can sit just outside one imported mesh bound. Alternating
            // projection finds the nearest shared rest-space anchor without changing
            // either piece or the contact area.
            Vector3 centroid = authoredCentroid;
            for (int iteration = 0; iteration < 4; iteration++)
            {
                centroid = ClampToRestBounds(pieceA, centroid);
                centroid = ClampToRestBounds(pieceB, centroid);
            }
            return centroid;
        }

        private static Vector3 ClampToRestBounds(
            in EarthFracturePieceRecord piece,
            Vector3 arenaPoint)
        {
            Matrix4x4 rest = Matrix4x4.TRS(
                piece.restLocalPosition,
                piece.restLocalRotation,
                piece.restLocalScale);
            Vector3 localPoint = rest.inverse.MultiplyPoint3x4(arenaPoint);
            Vector3 clamped = piece.renderMesh.bounds.ClosestPoint(localPoint);
            return rest.MultiplyPoint3x4(clamped);
        }

        private static EarthFractureBondRecord CreateBond(
            int id,
            int pieceA,
            int pieceB,
            Vector3 centroid,
            Vector3 normal,
            float area,
            bool foundation,
            bool repairable)
        {
            float contactArea = Mathf.Max(0.0001f, area);
            float areaRoot = Mathf.Sqrt(Mathf.Max(0.04f, contactArea));
            float foundationMultiplier = foundation ? 1.45f : 1f;
            EarthBondFlags flags = foundation ? EarthBondFlags.Foundation : EarthBondFlags.None;
            if (repairable) flags |= EarthBondFlags.Repairable;
            return new EarthFractureBondRecord
            {
                id = (ushort)id,
                pieceA = (short)pieceA,
                pieceB = (short)pieceB,
                flags = flags,
                localCentroid = centroid,
                localNormalA = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.right,
                contactArea = contactArea,
                tensileStrength = areaRoot * 10f * foundationMultiplier,
                shearStrength = areaRoot * 12.5f * foundationMultiplier,
                compressionStrength = areaRoot * 35f * foundationMultiplier
            };
        }

        private static void ValidateSidecar(Sidecar sidecar, int expectedStructures)
        {
            if (sidecar == null || sidecar.schemaVersion != 1 ||
                sidecar.structures == null || sidecar.structures.Length != expectedStructures)
            {
                throw new BuildFailedException(
                    $"Fracture sidecar must use schema 1 and contain {expectedStructures} structures.");
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < sidecar.structures.Length; index++)
            {
                SidecarStructure structure = sidecar.structures[index];
                if (string.IsNullOrEmpty(structure.structure_id) ||
                    !ids.Add(structure.structure_id))
                {
                    throw new BuildFailedException("Broken Crown structure IDs must be non-empty and unique.");
                }
                if (structure.piece_count != (structure.pieces?.Length ?? 0))
                    throw new BuildFailedException(
                        $"{structure.structure_id}: declared and actual piece counts differ.");
            }
        }

        private static Dictionary<string, Transform> IndexTransforms(Transform root)
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Transform current = stack.Pop();
                if (!result.TryAdd(current.name, current))
                    throw new BuildFailedException($"Duplicate FBX object name: {current.name}.");
                for (int index = 0; index < current.childCount; index++)
                    stack.Push(current.GetChild(index));
            }
            return result;
        }

        private static Transform RequireTransform(
            IReadOnlyDictionary<string, Transform> transforms,
            string name)
        {
            if (!string.IsNullOrEmpty(name) && transforms.TryGetValue(name, out Transform value))
                return value;
            throw new BuildFailedException($"Broken Crown FBX is missing object '{name}'.");
        }

        private static Mesh RequireMesh(Transform transform, string name)
        {
            MeshFilter filter = transform.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) return filter.sharedMesh;
            throw new BuildFailedException($"Broken Crown object '{name}' has no imported mesh.");
        }

        private static int CountPieces(EarthArenaFractureEntry[] entries)
        {
            int count = 0;
            for (int index = 0; index < entries.Length; index++)
                count += entries[index].fractureAsset != null
                    ? entries[index].fractureAsset.PieceCount
                    : 0;
            return count;
        }

        private static string ToAssetName(string structureId)
        {
            string[] words = structureId.Split('_');
            for (int index = 0; index < words.Length; index++)
            {
                if (words[index].Length == 0) continue;
                words[index] = char.ToUpperInvariant(words[index][0]) + words[index].Substring(1);
            }
            return string.Concat(words) + "Fracture";
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        [Serializable]
        private sealed class Sidecar
        {
            public int schemaVersion;
            public SidecarStructure[] structures;
            public string[] looseRocks;
            public string[] cosmeticRubble;
        }

        [Serializable]
        private sealed class SidecarStructure
        {
            public string structure_id;
            public string frame_object;
            public string intact_object;
            public string damage_mode;
            public string trigger;
            public string fracture_profile;
            public bool repairable;
            public int piece_count;
            public SidecarPiece[] pieces;
            public SidecarBond[] bonds;
        }

        [Serializable]
        private struct SidecarPiece
        {
            public int id;
            public string name;
            public string collider;
            public bool foundation;
            public bool repairable;
            public float volume_cubic_metres;
        }

        [Serializable]
        private struct SidecarBond
        {
            public int id;
            public int pieceA;
            public int pieceB;
            public string marker;
            public float contactArea;
            public bool foundation;
        }
    }
}
