using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Authoring.Fracture;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Presentation.VFX;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Authoring.Editor
{
    /// <summary>Independent authored columns; reuses the arena's validated fracture runtime.</summary>
    public static class OuterStoneRingImporter
    {
        public const string Folder = "Assets/Elemental/Content/Arena/OuterStoneRing";
        public const string ModelPath = Folder + "/OuterStoneRing.fbx";
        public const string SidecarPath = Folder + "/OuterStoneRing.fracture.json";
        public const string CatalogPath = Folder + "/Generated/OuterStoneRingCatalog.asset";
        public const string SceneRootName = "Outer Stone Ring";
        public const string ExteriorPath = "Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat";
        public const string InteriorPath = "Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstoneFractureInterior.mat";

        [MenuItem("Elemental/Arena/Import Outer Stone Ring")]
        public static void Import()
        {
            var catalog = BrokenCrownArenaImporter.RebuildPackage(
                ModelPath, SidecarPath, Folder + "/Generated", CatalogPath, 7);
            Selection.activeObject = catalog;
        }

        [MenuItem("Elemental/Arena/Update Baked Intact Columns")]
        public static void UpdateIntactMeshes()
        {
            if (Application.isPlaying) throw new BuildFailedException("Stop Play Mode before updating baked columns.");
            var root = GameObject.Find(SceneRootName);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (root == null || model == null) throw new BuildFailedException("Existing outer ring and imported model required.");
            var sources = new Dictionary<string,Mesh>(StringComparer.Ordinal);
            foreach(var source in model.GetComponentsInChildren<MeshFilter>(true))
                if(source.name.EndsWith("_INTACT",StringComparison.Ordinal)) sources.Add(source.name,source.sharedMesh);
            var structures = root.GetComponentsInChildren<EarthArenaStructure>(true);
            if(structures.Length!=7 || sources.Count!=7) throw new BuildFailedException("Expected seven baked columns.");
            foreach(var structure in structures)
                if(!sources.ContainsKey(structure.name) || structure.GetComponent<MeshCollider>()==null)
                    throw new BuildFailedException("Missing intact binding: "+structure.name);
            foreach(var structure in structures)
            {
                var filter=structure.GetComponent<MeshFilter>();
                var collider=structure.GetComponent<MeshCollider>();
                Undo.RecordObject(filter,"Update baked intact column");
                Undo.RecordObject(collider,"Update baked intact column collision");
                filter.sharedMesh=sources[structure.name]; collider.sharedMesh=filter.sharedMesh;
                collider.convex=false;
            }
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Debug.Log("[Elemental] Updated seven seamless intact column proxies; placement and fracture cells retained.");
        }

        [MenuItem("Elemental/Arena/Place Outer Stone Ring In Current Scene")]
        public static void Place()
        {
            if (Application.isPlaying) throw new BuildFailedException("Stop Play Mode before placing columns.");
            var catalog = AssetDatabase.LoadAssetAtPath<EarthArenaFractureCatalog>(CatalogPath);
            var arena = GameObject.Find("Broken Crown Arena");
            var planet = UnityEngine.Object.FindAnyObjectByType<VoxelPlanetBehaviour>();
            var gravity = UnityEngine.Object.FindAnyObjectByType<GravityWorldBehaviour>();
            var debris = UnityEngine.Object.FindAnyObjectByType<EarthRockDebrisPool>();
            var queries = UnityEngine.Object.FindAnyObjectByType<EarthSurfaceQueryService>();
            var feedback = UnityEngine.Object.FindAnyObjectByType<EarthMaterialFeedbackHub>();
            var exterior = AssetDatabase.LoadAssetAtPath<Material>(ExteriorPath);
            var interior = AssetDatabase.LoadAssetAtPath<Material>(InteriorPath);
            if (catalog == null || arena == null || planet == null || gravity == null || debris == null ||
                queries == null || feedback == null || exterior == null || interior == null)
                throw new BuildFailedException("Column placement requires the imported catalog and existing arena, planet, gravity, debris, surface queries, feedback and materials.");
            if (GameObject.Find(SceneRootName) != null)
                throw new BuildFailedException("Outer Stone Ring already exists. Preserve authored scene changes before replacing it.");
            string backup = "BuildReports/OuterStoneRing/EarthCoreSlice.before-columns.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(EditorSceneManager.GetActiveScene().path, backup);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(catalog.ImportedModel);
            Undo.RegisterCreatedObjectUndo(root, "Place outer stone columns");
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = SceneRootName;
            // This FBX exports native Y-up model space. The older arena has an
            // additional Z-up legacy correction; copying it would lay columns sideways.
            Vector3 arenaUp=(arena.transform.position-planet.transform.position).normalized;
            root.transform.SetPositionAndRotation(arena.transform.position,
                Quaternion.FromToRotation(Vector3.up,arenaUp));
            root.transform.localScale = Vector3.one;
            var all = root.GetComponentsInChildren<Transform>(true);
            var named = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var t in all) { named.Add(t.name,t); t.gameObject.layer=29; }
            foreach (var entry in catalog.Structures)
            {
                Transform frame = named["FRAME_" + entry.structureId];
                Transform intact = named[entry.intactObjectName];
                Transform fracture = named["FR_" + entry.structureId + "_ROOT"];
                var filter = intact.GetComponent<MeshFilter>();
                var renderer = intact.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { exterior, interior };
                var collider = intact.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false; // Preserve the open hook instead of a solid bounding box.
                SeatColumn(frame, intact, planet.transform.position, planet.Radius,
                    arenaUp);
                var pieces = new Transform[entry.fractureAsset.PieceCount];
                for (int i=0;i<pieces.Length;i++) pieces[i]=named[$"FR_{entry.structureId}_P{i+1:000}"];
                var runtime = intact.gameObject.AddComponent<EarthArenaStructure>();
                if (!runtime.Configure(entry.fractureAsset,frame,fracture,renderer,collider,pieces,
                    gravity,exterior,interior,StableId(entry.structureId),true,true))
                    throw new BuildFailedException(entry.structureId + ": runtime configuration failed.");
                runtime.ConfigureRockBreakup(debris);
                runtime.ConfigureMaterialFeedback(feedback);
                var provider = intact.gameObject.AddComponent<EarthArenaSurfaceProvider>();
                provider.Configure(runtime,collider,queries,(frame.position-planet.transform.position).normalized,true);
            }
            for (int i=0;i<catalog.LooseRockObjectNames.Length;i++)
            {
                var item=named[catalog.LooseRockObjectNames[i]];
                var filter=item.GetComponent<MeshFilter>();
                item.GetComponent<MeshRenderer>().sharedMaterials=new[]{exterior,interior};
                SeatLoose(item,planet.transform.position,planet.Radius);
                var collider=item.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh=filter.sharedMesh;collider.convex=true;
                var body=item.gameObject.AddComponent<Rigidbody>();
                body.mass=Mathf.Clamp(filter.sharedMesh.bounds.size.magnitude*90f,45f,1500f);
                body.useGravity=false;body.isKinematic=true;body.constraints=RigidbodyConstraints.FreezeAll;
                body.interpolation=RigidbodyInterpolation.Interpolate;
                var gb=item.gameObject.AddComponent<GravityBody>();gb.Configure(gravity,body);gb.enabled=false;
                var rock=item.gameObject.AddComponent<EarthDestructibleDecorRock>();
                rock.Configure(StableId(item.name),body,collider,gb,debris,
                    filter.sharedMesh.bounds.size.magnitude*.28f,Mathf.Clamp(body.mass*5.5f,420f,2400f));
                rock.ConfigureMaterialFeedback(feedback);
            }
            foreach (var t in all)
                if (t.name.StartsWith("COL_",StringComparison.Ordinal) || t.name.StartsWith("BOND_",StringComparison.Ordinal))
                    t.gameObject.SetActive(false);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode=ShadowCastingMode.On;
                renderer.receiveShadows=true;
                renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
            }
            var dust=new GameObject("Outer Column Fracture Dust");
            dust.transform.SetParent(root.transform,false);dust.AddComponent<ParticleSystem>();
            dust.AddComponent<EarthArenaFractureDustPresenter>().Configure(M3EarthCoreSetup.CreateOrLoadEffectsProfile());
            FitClearance();
            UnityEngine.Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject=root;
            Debug.Log("[Elemental] Seven independent outer columns placed with curved-ground foundations and arena fracture runtime.");
        }

        private static void SeatColumn(Transform frame, Transform intact, Vector3 center, float radius, Vector3 originalUp)
        {
            Vector3 radial=(frame.position-center).normalized;
            frame.rotation=Quaternion.FromToRotation(originalUp,radial)*frame.rotation;
            frame.position=center+radial*radius;
            MeshFilter filter=intact.GetComponent<MeshFilter>();
            Vector3[] vertices=filter.sharedMesh.vertices;
            // FBX retains Blender's local Z axis within each column's authored frame.
            float minimum=float.PositiveInfinity;
            foreach(var v in vertices) minimum=Mathf.Min(minimum,frame.InverseTransformPoint(intact.TransformPoint(v)).z);
            var cap=new List<Vector3>();
            foreach(var v in vertices)
            {
                Vector3 world=intact.TransformPoint(v);
                if(frame.InverseTransformPoint(world).z<=minimum+0.07f) cap.Add(world-frame.position);
            }
            if(cap.Count<3) throw new BuildFailedException(frame.name+": cannot identify foundation cap.");
            // Solve against all cap vertices; the far corner must also be underground.
            float lo=radius-4f,hi=radius+1f;
            for(int k=0;k<30;k++)
            {
                float distance=(lo+hi)*.5f;
                float farthest=0;
                foreach(var v in cap) farthest=Mathf.Max(farthest,(radial*distance+v).magnitude);
                if(farthest>radius-.08f) hi=distance; else lo=distance;
            }
            frame.position=center+radial*((lo+hi)*.5f);
        }

        private static void SeatLoose(Transform item,Vector3 center,float radius)
        {
            Vector3 radial=(item.position-center).normalized;
            float lowest=float.PositiveInfinity;
            foreach(var v in item.GetComponent<MeshFilter>().sharedMesh.vertices)
                lowest=Mathf.Min(lowest,(item.TransformPoint(v)-center).magnitude);
            item.position+=radial*(radius-lowest+.015f);
        }

        /// <summary>Fit the final curved-ground silhouettes, rather than the flat Blender projection.</summary>
        public static void FitClearance()
        {
            var root=GameObject.Find(SceneRootName);
            var arena=GameObject.Find("Broken Crown Arena");
            var floor=GameObject.Find("Arena_FloorBase_INTACT");
            var planet=UnityEngine.Object.FindAnyObjectByType<VoxelPlanetBehaviour>();
            var catalog=AssetDatabase.LoadAssetAtPath<EarthArenaFractureCatalog>(CatalogPath);
            if(root==null || arena==null || floor==null || planet==null || catalog==null)
                throw new BuildFailedException("Cannot fit column clearance without the ring, arena floor, planet and catalog.");
            Vector3 center=planet.transform.position;
            Vector3 up=(arena.transform.position-center).normalized;
            var authored=new Dictionary<string,Transform>();
            foreach(var t in catalog.ImportedModel.GetComponentsInChildren<Transform>(true)) authored[t.name]=t;
            foreach(var structure in root.GetComponentsInChildren<EarthArenaStructure>(true))
            {
                Transform frame=structure.transform.parent;
                Vector3 offset=frame.position-center;
                Vector3 horizontal=offset-up*Vector3.Dot(offset,up);
                Vector3 direction=horizontal.normalized;
                float distance=horizontal.magnitude;
                float gap=0;
                for(int k=0;k<12;k++)
                {
                    frame.rotation=root.transform.rotation*authored[frame.name].rotation;
                    frame.position=center+direction*distance+up*Mathf.Sqrt(planet.Radius*planet.Radius-distance*distance);
                    SeatColumn(frame,structure.transform,center,planet.Radius,up);
                    gap=PlanarClearance(structure.transform,floor.transform,up);
                    if(Mathf.Abs(gap-2.5f)<.002f)break;
                    distance=Mathf.Clamp(distance+(2.5f-gap),5f,planet.Radius*.6f);
                }
                if(Mathf.Abs(gap-2.5f)>.02f)
                    throw new BuildFailedException(structure.name+": could not fit 2.5 metre arena clearance; measured "+gap);
            }
            foreach(var rock in root.GetComponentsInChildren<EarthDestructibleDecorRock>(true))
                SeatLoose(rock.transform,center,planet.Radius);
            ResolveLooseContacts(root,center,planet.Radius);
            UnityEngine.Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        private static void ResolveLooseContacts(GameObject root,Vector3 center,float radius)
        {
            var rocks=root.GetComponentsInChildren<EarthDestructibleDecorRock>(true);
            var obstacles=new List<Collider>();
            foreach(var s in root.GetComponentsInChildren<EarthArenaStructure>(true)) obstacles.Add(s.GetComponent<Collider>());
            foreach(var rock in rocks)obstacles.Add(rock.GetComponent<Collider>());
            for(int iteration=0;iteration<24;iteration++)
            {
                bool changed=false;
                UnityEngine.Physics.SyncTransforms();
                foreach(var rock in rocks)
                {
                    Collider shape=rock.GetComponent<Collider>();
                    foreach(var other in obstacles)
                    {
                        if(shape==other || !other.enabled)continue;
                        if(!UnityEngine.Physics.ComputePenetration(shape,rock.transform.position,rock.transform.rotation,
                            other,other.transform.position,other.transform.rotation,out Vector3 push,out float depth) || depth<.001f)continue;
                        Vector3 up=(rock.transform.position-center).normalized;
                        push=Vector3.ProjectOnPlane(push,up);
                        if(push.sqrMagnitude<.01f)push=Vector3.ProjectOnPlane(shape.bounds.center-other.bounds.center,up);
                        if(push.sqrMagnitude<.0001f)push=Vector3.Cross(up,Vector3.forward);
                        rock.transform.position+=push.normalized*(depth+.025f);
                        SeatLoose(rock.transform,center,radius);
                        changed=true;
                    }
                }
                if(!changed)return;
            }
            throw new BuildFailedException("Loose column fragments still overlap after seating; inspect their collision geometry.");
        }

        public static float PlanarClearance(Transform a,Transform b,Vector3 up)
        {
            Vector3 x=Vector3.Cross(up,Vector3.forward).normalized;
            if(x.sqrMagnitude<.5f)x=Vector3.Cross(up,Vector3.right).normalized;
            Vector3 y=Vector3.Cross(up,x).normalized;
            var first=ProjectedHull(a,x,y);var second=ProjectedHull(b,x,y);
            float minimum=float.PositiveInfinity;
            for(int pass=0;pass<2;pass++)
            {
                var points=pass==0?first:second;var polygon=pass==0?second:first;
                foreach(var p in points) for(int i=0;i<polygon.Count;i++)
                {
                    Vector2 start=polygon[i],edge=polygon[(i+1)%polygon.Count]-start;
                    float t=Mathf.Clamp01(Vector2.Dot(p-start,edge)/Mathf.Max(1e-10f,edge.sqrMagnitude));
                    minimum=Mathf.Min(minimum,Vector2.Distance(p,start+t*edge));
                }
            }
            return minimum;
        }

        private static List<Vector2> ProjectedHull(Transform item,Vector3 x,Vector3 y)
        {
            var points=new List<Vector2>();
            foreach(var vertex in item.GetComponent<MeshFilter>().sharedMesh.vertices)
            {Vector3 p=item.TransformPoint(vertex);points.Add(new Vector2(Vector3.Dot(p,x),Vector3.Dot(p,y)));}
            points.Sort((a,b)=>a.x!=b.x?a.x.CompareTo(b.x):a.y.CompareTo(b.y));
            var hull=new List<Vector2>();
            for(int pass=0;pass<2;pass++)
            {
                int start=hull.Count;
                for(int j=0;j<points.Count;j++)
                {
                    Vector2 p=points[pass==0?j:points.Count-1-j];
                    while(hull.Count>=start+2)
                    {
                        Vector2 a=hull[hull.Count-1]-hull[hull.Count-2],b=p-hull[hull.Count-1];
                        if(a.x*b.y-a.y*b.x>0)break;
                        hull.RemoveAt(hull.Count-1);
                    }
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count-1);
            }
            return hull;
        }

        public static uint StableId(string value)
        {
            uint hash=2166136261u;
            foreach(char c in value) hash=unchecked((hash^c)*16777619u);
            return hash==0?1:hash;
        }
    }
}
