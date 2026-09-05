using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Characters;
using Elemental.Input.Actions;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Voxel;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Authoring.Editor
{
    public static class StartupCacheBaker
    {
        private const string Folder = "Assets/Elemental/Content/StartupCaches";
        [MenuItem("Elemental/World/Bake Startup Caches In Current Scene")]
        public static void BakeCurrentScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before baking startup caches.");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Elemental/Content", "StartupCaches");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var planets = new List<VoxelPlanetBehaviour>();
            var pools = new List<EarthRockDebrisPool>();
            var fragmentPools = new List<EarthFragmentPool>();
            var scatters = new List<EarthPlanetRockScatter>();
            var structures = new List<EarthArenaStructure>();
            var colliders = new List<Collider>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                planets.AddRange(root.GetComponentsInChildren<VoxelPlanetBehaviour>(true));
                pools.AddRange(root.GetComponentsInChildren<EarthRockDebrisPool>(true));
                fragmentPools.AddRange(root.GetComponentsInChildren<EarthFragmentPool>(true));
                scatters.AddRange(root.GetComponentsInChildren<EarthPlanetRockScatter>(true));
                structures.AddRange(root.GetComponentsInChildren<EarthArenaStructure>(true));
                colliders.AddRange(root.GetComponentsInChildren<Collider>(true));
            }
            if (planets.Count != 1 || pools.Count != 1)
                throw new InvalidOperationException($"Expected one planet and debris pool in current scene; got {planets.Count}/{pools.Count}.");
            // Publish component references only after both independent cache builds succeeded.
            PlanetBaseMeshCache planetCache = BakePlanet(planets[0], scene.name);
            EarthConvexFractureCacheAsset convexCache = BakeConvex(
                pools[0], fragmentPools, scatters, structures, colliders, scene.name);
            Undo.RecordObject(planets[0], "Use exact baked planet base");
            Undo.RecordObject(pools[0], "Use baked convex fracture cache");
            planets[0].ConfigureBaseMeshCache(planetCache);
            pools[0].ConfigureBakedFractureCache(convexCache);
            EditorUtility.SetDirty(planets[0]); EditorUtility.SetDirty(pools[0]);
            var controls = new List<Behaviour>();
            EarthSceneReadinessGate gate = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                controls.AddRange(root.GetComponentsInChildren<EarthInputAdapter>(true));
                controls.AddRange(root.GetComponentsInChildren<EarthMvpBotController>(true));
                gate ??= root.GetComponentInChildren<EarthSceneReadinessGate>(true);
            }
            if (gate == null)
            {
                var go = new GameObject("Scene Readiness");
                Undo.RegisterCreatedObjectUndo(go, "Create scene readiness boundary");
                gate = go.AddComponent<EarthSceneReadinessGate>();
            }
            Undo.RecordObject(gate, "Configure scene readiness");
            gate.Configure(planets[0], pools[0], controls.ToArray());
            EditorUtility.SetDirty(gate);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static PlanetBaseMeshCache BakePlanet(VoxelPlanetBehaviour planet, string sceneName)
        {
            var properties = new SerializedObject(planet);
            var state = new VoxelPlanetState(properties.FindProperty("radius").floatValue,
                properties.FindProperty("seed").uintValue, properties.FindProperty("chunkResolution").intValue,
                properties.FindProperty("cellSize").floatValue, properties.FindProperty("noiseAmplitude").floatValue);
            string identity = FormattableString.Invariant($"{state.Radius:R}|{state.Seed}|{state.ChunkResolution}|{state.CellSize:R}|{state.NoiseAmplitude:R}|{PlanetBaseMeshCache.CurrentRevision}");
            string key = Key(identity, "Assets/Elemental/Simulation/Voxel/AnalyticSphereField.cs", "Assets/Elemental/Simulation/Voxel/SmoothSdfSurfaceMesher.cs", "Assets/Elemental/Simulation/Voxel/PlanetChunkShellSolver.cs", "Assets/Elemental/Simulation/Voxel/VoxelPlanetState.cs");
            string path = $"{Folder}/{sceneName}PlanetBase_{key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PlanetBaseMeshCache>(path);
            if (existing != null && !existing.Matches(state)) throw new InvalidOperationException($"Generated planet cache has invalid metadata: {path}. Remove that generated revision and rebake.");
            if (existing != null && existing.Matches(state))
            {
                ValidatePlanet(existing, state);
                Debug.Log($"[StartupCache] Reusing validated exact planet cache {path}.");
                return existing;
            }
            var cache = ScriptableObject.CreateInstance<PlanetBaseMeshCache>();
            using var output = new StagedAsset(cache, path);
            var entries = new List<PlanetBaseMeshCache.Entry>();
            using var mesher = new SmoothSdfSurfaceMesher();
            using var buffers = new ChunkMeshBuffers();
            var settings = new VoxelMeshingSettings(state.ChunkResolution, state.CellSize);
            int minimum = Mathf.FloorToInt(-state.Radius / state.ChunkWorldSize);
            int maximum = Mathf.FloorToInt(state.Radius / state.ChunkWorldSize);
            double started = EditorApplication.timeSinceStartup;
            for (int z = minimum; z <= maximum; z++)
            for (int y = minimum; y <= maximum; y++)
            for (int x = minimum; x <= maximum; x++)
            {
                if (!PlanetChunkShellSolver.IntersectsSurfaceShell(new int3(x,y,z), state.ChunkWorldSize,
                    state.Radius, state.NoiseAmplitude + state.CellSize * 1.5f)) continue;
                var coord = new ChunkCoord(x,y,z);
                mesher.Build(state, coord, settings, buffers);
                var mesh = new Mesh { name = $"Base {coord}", indexFormat = buffers.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                var vertices = new Vector3[buffers.Vertices.Length];
                var normals = new Vector3[buffers.Normals.Length];
                var triangles = new int[buffers.Indices.Length];
                for (int i = 0; i < vertices.Length; i++) { vertices[i] = buffers.Vertices[i]; normals[i] = buffers.Normals[i]; }
                for (int i = 0; i < triangles.Length; i++) triangles[i] = buffers.Indices[i];
                mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = triangles;
                mesh.RecalculateBounds();
                if (vertices.Length > 0) UnityEngine.Physics.BakeMesh(mesh.GetEntityId(), false);
                AssetDatabase.AddObjectToAsset(mesh, cache);
                entries.Add(new PlanetBaseMeshCache.Entry { X=x, Y=y, Z=z, Mesh=mesh, ContentHash=state.ComputeChunkHash(coord) });
            }
            cache.Configure(state, entries.ToArray());
            EditorUtility.SetDirty(cache);
            ValidatePlanet(cache, state);
            Debug.Log($"[StartupCache] Baked exact SDF base: {entries.Count} chunks, {EditorApplication.timeSinceStartup-started:F3}s, {path}.");
            output.Commit();
            return cache;
        }

        private sealed class FractureSource
        {
            public Mesh Mesh;
            public Collider Collider;
            public float VolumeScale;
            public string Identity;
            public string Label;
        }

        private static EarthConvexFractureCacheAsset BakeConvex(EarthRockDebrisPool pool,
            List<EarthFragmentPool> fragmentPools, List<EarthPlanetRockScatter> scatters,
            List<EarthArenaStructure> structures,
            List<Collider> colliders, string sceneName)
        {
            var sources = new List<FractureSource>();
            CollectArenaSources(structures, sources);
            foreach (Collider collider in colliders)
                if (collider.GetComponent<EarthDestructibleDecorRock>() != null)
                    sources.Add(ColliderSource(collider));
            if (sources.Count == 0)
                throw new InvalidOperationException("No authoritative arena/decor fracture sources were found. Complete arena/column scene integration before baking.");
            var poolSources = new List<Mesh>();
            pool.AppendAuthoredFractureSources(poolSources);
            foreach (EarthFragmentPool fragmentPool in fragmentPools)
                fragmentPool.AppendAuthoredFractureSources(poolSources);
            foreach (EarthPlanetRockScatter scatter in scatters)
                scatter.AppendAuthoredFractureSources(poolSources);
            if (poolSources.Count == 0)
                throw new InvalidOperationException("No persistent debris/hero/scatter fracture meshes are configured. Assign the authored physics libraries before baking startup caches.");
            foreach (Mesh source in poolSources)
                if (!source.isReadable || !AssetDatabase.Contains(source))
                    throw new InvalidOperationException($"Persistent pool fracture source '{source.name}' must be a readable imported asset before baking startup caches.");
            // Persistent runtime sources must be included in Player data and expose
            // their CPU mesh arrays. Normalize only the exact authored sources used
            // by fracture/scatter; imported meshes keep their importer ownership.
            var runtimeReadable = new HashSet<Mesh>();
            foreach (FractureSource source in sources)
                PreserveRuntimeReadability(source.Mesh, runtimeReadable, source.Label);
            foreach (Mesh source in poolSources)
                PreserveRuntimeReadability(source, runtimeReadable, "pool:" + source.name);
            foreach (EarthPlanetRockScatter scatter in scatters)
                PreserveScatterVisualReadability(scatter, runtimeReadable);
            var sourceKeys = new List<string>();
            foreach (FractureSource source in sources) sourceKeys.Add(source.Identity);
            foreach (Mesh source in poolSources)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
                sourceKeys.Add("pool:" + guid + ":" + id + ":" + EarthConvexFractureCacheAsset.Signature(source) + ":all-child-counts");
            }
            sourceKeys.Sort(StringComparer.Ordinal);
            // Policy changes can alter which second-generation plans must be present.
            var policy = new SerializedObject(pool).FindProperty("profile").objectReferenceValue;
            string policyPath = policy != null ? AssetDatabase.GetAssetPath(policy) : "";
            string key = Key(string.Join("|", sourceKeys) + "|" + (policyPath.Length > 0 ? AssetDatabase.GetAssetDependencyHash(policyPath).ToString() : "default") + "|" + EarthConvexFractureCacheAsset.CurrentRevision,
                "Assets/Elemental/Simulation/Structures/EarthConvexPartitionSolver.cs",
                "Assets/Elemental/Simulation/Structures/EarthRockBreakPolicy.cs",
                "Assets/Elemental/Runtime/Geometry/EarthFractureBevelMeshBuilder.cs",
                "Assets/Elemental/Runtime/Geometry/EarthConvexFragmentCache.cs",
                "Assets/Elemental/Runtime/Physics/EarthRockDebrisPool.cs",
                "Assets/Elemental/Runtime/Physics/EarthFragmentPool.cs",
                "Assets/Elemental/Runtime/Physics/EarthPlanetRockScatter.cs",
                "Assets/Elemental/Authoring/Editor/StartupCacheBaker.cs");
            string path = $"{Folder}/{sceneName}ConvexFracture_{key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EarthConvexFractureCacheAsset>(path);
            if (existing != null && !existing.Current) throw new InvalidOperationException($"Generated convex cache has invalid metadata: {path}. Remove that generated revision and rebake.");
            if (existing != null && existing.Current)
            {
                using var validation = new EarthConvexFragmentCache();
                validation.LoadBaked(existing);
                if (validation.BakedRejectedPlanCount != 0 || validation.BakedPlanCount != existing.Plans.Length)
                    throw new InvalidOperationException($"Invalid persistent convex cache {path}. Remove this generated revision and rebake.");
                Debug.Log($"[StartupCache] Reusing validated convex cache {path}.");
                return existing;
            }
            var asset = ScriptableObject.CreateInstance<EarthConvexFractureCacheAsset>();
            using var output = new StagedAsset(asset, path);
            using var cache = new EarthConvexFragmentCache();
            int bakedSources = 0;
            double started = EditorApplication.timeSinceStartup;
            foreach (FractureSource entry in sources)
            {
                Mesh source = entry.Mesh != null ? entry.Mesh : cache.SourceMesh(entry.Collider);
                float volumeScale = entry.VolumeScale;
                for (int count = 3; count <= 4; count++)
                    foreach (var child in cache.Get(source, count))
                    {
                        float radius = Mathf.Pow(child.Volume * volumeScale * .2387324f, 1f / 3f);
                        int next = pool.ResolveBreak(radius, 1f, 100000f, false, 1).PhysicalPieces;
                        if (next > 0) cache.Get(child.ColliderMesh, next);
                    }
                bakedSources++;
            }
            // Pool shapes can be resized before a later collision. Bake both supported
            // child counts for every first-generation cell so an impact cannot select a
            // policy-valid plan that was absent from the loading cache.
            foreach (Mesh source in poolSources)
            {
                for (int count = 3; count <= 4; count++)
                    foreach (var child in cache.Get(source, count))
                    {
                        cache.Get(child.ColliderMesh, 3);
                        cache.Get(child.ColliderMesh, 4);
                    }
                ValidatePoolCoverage(cache, source);
            }
            var plans = cache.ExportPlans();
            foreach (Mesh mesh in cache.GetOwnedMeshesForBake())
            {
                if (mesh == null)
                    throw new InvalidOperationException("Convex cache exported a null owned mesh.");
                // Runtime-generated cache meshes are protected with DontSave while
                // owned by the disposable cache. This transaction explicitly makes
                // them persistent sub-assets, so clear the protection immediately
                // before AddObjectToAsset (and nowhere earlier).
                mesh.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(mesh, asset);
            }
            cache.TransferMeshOwnershipToAsset();
            asset.Configure(plans);
            EditorUtility.SetDirty(asset);
            // Cache generation performs native mesh/collider work. Normalize the
            // source flags once more at the transaction boundary so no later bake
            // operation can be the last writer before SaveAssets.
            runtimeReadable.Clear();
            foreach (FractureSource source in sources)
                PreserveRuntimeReadability(source.Mesh, runtimeReadable, source.Label);
            foreach (Mesh source in poolSources)
                PreserveRuntimeReadability(source, runtimeReadable, "pool:" + source.name);
            foreach (EarthPlanetRockScatter scatter in scatters)
                PreserveScatterVisualReadability(scatter, runtimeReadable);
            Debug.Log($"[StartupCache] Baked {plans.Length} recursive convex plans from {bakedSources} authoritative arena/decor sources and {poolSources.Count} persistent pool/scatter sources in {EditorApplication.timeSinceStartup-started:F3}s, {path}.");
            output.Commit();
            return asset;
        }

        private static void CollectArenaSources(List<EarthArenaStructure> structures, List<FractureSource> destination)
        {
            foreach (EarthArenaStructure structure in structures)
            {
                var serialized = new SerializedObject(structure);
                var assetObject = serialized.FindProperty("fractureAssetObject").objectReferenceValue as ScriptableObject;
                var runtimeAsset = assetObject as IEarthFractureAssetRuntimeData;
                var pieces = serialized.FindProperty("pieces");
                if (runtimeAsset == null || pieces == null || runtimeAsset.PieceCount != pieces.arraySize)
                    throw new InvalidOperationException($"Arena structure '{structure.name}' has inconsistent fracture asset/piece bindings.");
                for (int index = 0; index < runtimeAsset.PieceCount; index++)
                {
                    Mesh mesh = runtimeAsset.GetPieceColliderMesh(index);
                    Transform piece = pieces.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    if (mesh == null || piece == null || !mesh.isReadable || !AssetDatabase.Contains(mesh))
                        throw new InvalidOperationException($"Arena structure '{structure.name}' piece {index} needs a readable persistent collider mesh.");
                    destination.Add(MeshSource(mesh, piece.transform.localToWorldMatrix.determinant,
                        $"arena:{structure.name}:{index}"));
                }
            }
        }

        private static FractureSource ColliderSource(Collider collider)
        {
            if (collider is MeshCollider meshCollider)
            {
                if (!meshCollider.convex || meshCollider.sharedMesh == null ||
                    !meshCollider.sharedMesh.isReadable || !AssetDatabase.Contains(meshCollider.sharedMesh))
                    throw new InvalidOperationException($"Decor collider '{collider.name}' needs a readable persistent convex mesh.");
                return MeshSource(meshCollider.sharedMesh, collider.transform.localToWorldMatrix.determinant,
                    $"decor:{collider.name}");
            }
            if (collider is not BoxCollider && collider is not SphereCollider && collider is not CapsuleCollider)
                throw new InvalidOperationException($"Decor collider '{collider.name}' uses unsupported {collider.GetType().Name} fracture geometry.");
            return new FractureSource { Collider = collider,
                VolumeScale = Mathf.Abs(collider.transform.localToWorldMatrix.determinant),
                Identity = PrimitiveIdentity(collider) + ":" + collider.transform.localToWorldMatrix.determinant.ToString("R", CultureInfo.InvariantCulture),
                Label = $"decor:{collider.name}" };
        }

        private static FractureSource MeshSource(Mesh mesh, float determinant, string label)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id);
            return new FractureSource { Mesh = mesh, VolumeScale = Mathf.Abs(determinant), Label = label,
                Identity = label + ":" + guid + ":" + id + ":" + EarthConvexFractureCacheAsset.Signature(mesh) + ":" + determinant.ToString("R", CultureInfo.InvariantCulture) };
        }

        private static void PreserveScatterVisualReadability(
            EarthPlanetRockScatter scatter,
            HashSet<Mesh> visited)
        {
            if (scatter == null) return;
            var serialized = new SerializedObject(scatter);
            SerializedProperty meshes = serialized.FindProperty("visualMeshes");
            if (meshes == null || !meshes.isArray)
                throw new InvalidOperationException($"Scatter '{scatter.name}' has no serialized visual mesh array.");
            for (int index = 0; index < meshes.arraySize; index++)
            {
                Mesh mesh = meshes.GetArrayElementAtIndex(index).objectReferenceValue as Mesh;
                PreserveRuntimeReadability(mesh, visited, $"scatter:{scatter.name}:{index}");
            }
        }

        private static void PreserveRuntimeReadability(
            Mesh mesh,
            HashSet<Mesh> visited,
            string label)
        {
            if (mesh == null || !visited.Add(mesh)) return;
            if (!AssetDatabase.Contains(mesh) || !mesh.isReadable)
                throw new InvalidOperationException(
                    $"Runtime procedural mesh '{label}' must be a readable persistent asset.");
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (AssetImporter.GetAtPath(assetPath) is ModelImporter modelImporter)
            {
                // Imported FBX/Blender sub-meshes are governed by the importer;
                // changing their native serialized fields would not survive reimport.
                if (!modelImporter.isReadable)
                    throw new InvalidOperationException(
                        $"Runtime procedural mesh '{label}' needs Read/Write Enabled on '{assetPath}'.");
                return;
            }
            // Script-created meshes inherit DontSave flags from runtime factories.
            // Those flags remain serialized even after CreateAsset and cause the
            // standalone build pipeline to omit the referenced source entirely.
            // A persistent asset referenced by the baked cache must participate in
            // the Player build. m_IsReadable/isReadable is the supported read/write
            // contract; m_KeepVertices/m_KeepIndices are native implementation
            // details and are intentionally not edited.
            HideFlags persistentFlags = mesh.hideFlags &
                ~(HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor);
            if (persistentFlags != mesh.hideFlags)
            {
                mesh.hideFlags = persistentFlags;
                EditorUtility.SetDirty(mesh);
            }
        }

        private static string PrimitiveIdentity(Collider collider)
        {
            if (collider is BoxCollider box)
                return FormattableString.Invariant($"box:{box.center.x:R},{box.center.y:R},{box.center.z:R}:{box.size.x:R},{box.size.y:R},{box.size.z:R}");
            if (collider is SphereCollider sphere)
                return FormattableString.Invariant($"sphere:{sphere.center.x:R},{sphere.center.y:R},{sphere.center.z:R}:{sphere.radius:R}");
            var capsule = (CapsuleCollider)collider;
            return FormattableString.Invariant($"capsule{capsule.direction}:{capsule.center.x:R},{capsule.center.y:R},{capsule.center.z:R}:{capsule.radius:R},{capsule.height:R}");
        }

        private static void ValidatePoolCoverage(EarthConvexFragmentCache cache, Mesh source)
        {
            for (int count = 3; count <= 4; count++)
            {
                if (!cache.HasPlan(source, count))
                    throw new InvalidOperationException($"Missing top-level pool fracture plan for {source.name}/{count}.");
                foreach (var child in cache.Get(source, count))
                    for (int descendantCount = 3; descendantCount <= 4; descendantCount++)
                        if (!cache.HasPlan(child.ColliderMesh, descendantCount))
                            throw new InvalidOperationException($"Missing descendant pool fracture plan for {source.name}/{count}/{descendantCount}.");
            }
        }

        private sealed class StagedAsset : IDisposable
        {
            private readonly string _temporary, _destination;
            private readonly UnityEngine.Object _mainAsset;
            private bool _committed;
            public StagedAsset(UnityEngine.Object asset, string destination)
            {
                _mainAsset = asset;
                _destination = destination;
                _temporary = AssetDatabase.GenerateUniqueAssetPath(Folder + "/StartupBakeStaging.asset");
                AssetDatabase.CreateAsset(asset, _temporary);
            }
            public void Commit()
            {
                // The main object selects serialization for the entire mesh-containing file.
                AssetDatabase.SetMainObject(_mainAsset, _temporary);
                AssetDatabase.SaveAssets();
                string error = AssetDatabase.MoveAsset(_temporary, _destination);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                _committed = true;
            }
            public void Dispose() { if (!_committed) AssetDatabase.DeleteAsset(_temporary); }
        }

        private static string Key(string identity, params string[] dependencies)
        {
            var text = new StringBuilder(identity);
            foreach (string path in dependencies) text.Append('|').Append(AssetDatabase.GetAssetDependencyHash(path));
            return Hash128.Compute(text.ToString()).ToString();
        }

        public static void ValidatePlanet(PlanetBaseMeshCache cache, VoxelPlanetState state)
        {
            if (!cache.Matches(state)) throw new InvalidOperationException("Planet cache signature mismatch.");
            using var mesher = new SmoothSdfSurfaceMesher();
            using var buffers = new ChunkMeshBuffers();
            var settings = new VoxelMeshingSettings(state.ChunkResolution, state.CellSize);
            var coords = new HashSet<ChunkCoord>();
            foreach (var entry in cache.Entries)
            {
                if (!coords.Add(entry.Coord) || entry.Mesh == null) throw new InvalidOperationException("Missing/duplicate cached mesh.");
                mesher.Build(state, entry.Coord, settings, buffers);
                var vertices = entry.Mesh.vertices; var normals = entry.Mesh.normals; var triangles = entry.Mesh.triangles;
                if (vertices.Length != buffers.Vertices.Length || normals.Length != buffers.Normals.Length || triangles.Length != buffers.Indices.Length || entry.ContentHash != state.ComputeChunkHash(entry.Coord))
                    throw new InvalidOperationException($"Cache data mismatch in {entry.Coord}.");
                for (int i=0; i<vertices.Length; i++)
                    if (!math.all((float3)vertices[i] == buffers.Vertices[i]) || !math.all((float3)normals[i] == buffers.Normals[i]))
                        throw new InvalidOperationException($"Cache geometry mismatch in {entry.Coord} vertex {i}.");
                for (int i=0; i<triangles.Length; i++)
                    if (triangles[i] != buffers.Indices[i]) throw new InvalidOperationException($"Cache topology mismatch in {entry.Coord}.");
            }
            int min = Mathf.FloorToInt(-state.Radius/state.ChunkWorldSize), max = Mathf.FloorToInt(state.Radius/state.ChunkWorldSize), expected=0;
            for(int z=min;z<=max;z++) for(int y=min;y<=max;y++) for(int x=min;x<=max;x++)
                if(PlanetChunkShellSolver.IntersectsSurfaceShell(new int3(x,y,z),state.ChunkWorldSize,state.Radius,state.NoiseAmplitude+state.CellSize*1.5f))
                { expected++; if(!coords.Contains(new ChunkCoord(x,y,z))) throw new InvalidOperationException("Incomplete planet cache."); }
            if(expected!=coords.Count) throw new InvalidOperationException("Unexpected planet cache chunks.");
        }
    }
}
