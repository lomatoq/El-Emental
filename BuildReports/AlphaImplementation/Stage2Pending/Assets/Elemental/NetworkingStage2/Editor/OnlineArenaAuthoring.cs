using System;
using System.Collections.Generic;
using System.Linq;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Presentation.Camera;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Elemental.Online.Editor
{
    /// <summary>Targeted EarthCoreSlice composition. Never invokes the M3 arena rebuild.</summary>
    public static class OnlineArenaAuthoring
    {
        public const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string OwnerName = "Earth Online Runtime";
        private static readonly string[] OwnerNames = {
            "Planet Character", "Earth Magic Runtime", "Gravity Toy Camera", "Earth Cinemachine System",
            "Earth Landing Cushion Preview", "Earth Ability Preview", "Earth Ground Footprint Preview", "Earth Shaper Puppet"
        };
        private static readonly HashSet<string> MutatorNames = new HashSet<string>(StringComparer.Ordinal) {
            "MagicExecutor", "MagicInputController", "EarthActionRouterBehaviour", "EarthDualMouseAbilityController",
            "EarthTelekinesisController", "EarthTrapController", "EarthSurfController", "EarthResonanceController",
            "EarthReassemblyController", "EarthArmorController", "EarthPillarMobility", "EarthPillarWaveAbility",
            "EarthLandingCushion", "EarthMatterReturnController", "EarthTechniqueComboRuntime"
        };

        [MenuItem("Elemental/Online/Install EarthCoreSlice Online Only")]
        public static void Install()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open EarthCoreSlice in Edit mode before this targeted installation.");
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject existing = roots.SingleOrDefault(root => root.name == OwnerName);
            if (existing != null)
            {
                var current = Required<EarthOnlineGameplayBinding>(existing);
                if (current.Capabilities != OnlineCapabilities.All || existing.GetComponent<EarthOnlinePresentationBridge>() == null)
                    throw new InvalidOperationException("Existing online installation is incomplete. Undo that installation before retrying; no duplicate graph was created.");
                OnlineGeometryCatalogAuthoring.Bake();
                OnlineArenaIdentityAuthoring.Stamp(current, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("Existing online graph validated and identity refreshed. Save the scene after reviewing it.");
                return;
            }
            if (roots.SelectMany(root => root.GetComponentsInChildren<NetworkManager>(true)).Any())
                throw new InvalidOperationException("An unexpected NetworkManager already exists. Resolve explicit network ownership first.");
            GameObject[] source = OwnerNames.Select(name => ExactRoot(roots, name)).ToArray();
            GameObject player = source[0], magic = source[1], cameraRoot = source[2];
            var flow = Required<FrontendFlowController>(ExactRoot(roots, "Alpha Frontend"));
            var menu = Read<CinematicMenuCamera>(flow, "menuCamera");
            var hud = Read<EarthDuelHud>(flow, "hud");
            var offlineDuel = Read<EarthMvpDuelController>(flow, "duel");
            GameObject bot = ExactRoot(roots, "Rumble Linebreaker Bot");
            Rigidbody botBody = Read<Rigidbody>(offlineDuel, "botBody");
            var planet = Required<VoxelPlanetBehaviour>(ExactRoot(roots, "Editable Voxel Planet"));
            var gate = Read<EarthSceneReadinessGate>(flow, "readiness");
            var kernel = Required<EarthMatterKernelBehaviour>(magic);
            var queries = Required<EarthSurfaceQueryService>(magic);
            var controller = Required<EarthCinemachineCameraController>(cameraRoot);
            var sourceBody = Required<Rigidbody>(player);
            // Capture authored world identity before adding the second graph or online components.
            ulong initialHash = OnlineArenaIdentityAuthoring.WorldHash(scene);
            OnlineGeometryCatalogAuthoring.Bake();
            var catalog = AssetDatabase.LoadAssetAtPath<OnlineGeometryCatalog>(OnlineGeometryCatalogAuthoring.CatalogPath);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Install targeted Earth online graph");
            try
            {
                var owner = new GameObject(OwnerName); Undo.RegisterCreatedObjectUndo(owner, "Create online runtime");
                var graph = OnlineActorGraphAuthoring.CloneOwnerGraph(source, owner.transform, new Component[] { kernel, queries });
                // Move the whole owner graph together, including external physical puppet bodies.
                Quaternion rotation = botBody.rotation * Quaternion.Inverse(sourceBody.rotation);
                graph.Container.transform.SetPositionAndRotation(botBody.position - rotation * sourceBody.position, rotation);
                ValidateExternalReferences(graph, new[] { planet.gameObject,
                    ExactRoot(roots, "Planet Collision Proxy"), ExactRoot(roots, "Gravity World"),
                    ExactRoot(roots, "Earth Material Feedback"), bot }, new Component[] { kernel, queries });
                foreach (Behaviour component in graph.Container.GetComponentsInChildren<Behaviour>(true))
                    if (component.GetType().Name == "VisualQaCaptureBehaviour" || component.GetType().Name.Contains("CameraRuntimeAudit")) component.enabled = false;

                var binding = Undo.AddComponent<EarthOnlineGameplayBinding>(owner);
                var onlineDuel = Undo.AddComponent<EarthMvpDuelController>(owner);
                // Copy authored gameplay settings, then clear every copied offline actor reference.
                EditorUtility.CopySerialized(offlineDuel, onlineDuel);
                ClearSceneReferences(onlineDuel); onlineDuel.enabled = false;
                var bodies = Undo.AddComponent<OnlineBodyReplicas>(owner);
                var world = Undo.AddComponent<OnlineWorldReplicaRegistry>(owner);
                var terrain = Undo.AddComponent<OnlineTerrainReplication>(owner);
                var replicaRoot = new GameObject("Online World Replicas"); replicaRoot.transform.SetParent(owner.transform, false);
                var one = MakeActor(player, player, magic, cameraRoot, Read<Component>(menu, "animationDriver"));
                var two = MakeActor(graph.Container, graph.CloneOf(player), graph.CloneOf(magic), graph.CloneOf(cameraRoot),
                    graph.CloneOf(Read<Component>(menu, "animationDriver")));
                WriteActor(binding, "actorOne", one); WriteActor(binding, "actorTwo", two);
                Set(binding, "onlineDuel", onlineDuel); Set(binding, "offlineDuel", offlineDuel);
                Set(binding, "offlineOnlyRoots", new Object[] { bot }); Set(binding, "readiness", gate);
                Set(binding, "planet", planet); Set(binding, "bodies", bodies); Set(binding, "world", world); Set(binding, "terrain", terrain);
                Set(binding, "initialWorldHash", initialHash);
                var sources = new List<Transform> { magic.transform, graph.CloneOf(magic).transform, source[4].transform, graph.CloneOf(source[4]).transform };
                foreach (GameObject root in roots)
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    if (component is EarthArenaStructure || component is EarthDestructibleDecorRock) AddRoot(sources, component.transform);
                if (sources.Count <= 4) throw new InvalidOperationException("No authored arena structures or push boulders were found.");
                Set(world, "sourceRoots", sources.Cast<Object>().ToArray()); Set(world, "replicaRoot", replicaRoot.transform); Set(world, "catalog", catalog);
                var scatter = Required<EarthPlanetRockScatter>(planet.gameObject);
                Set(world, "rockScatter", scatter);
                Set(world, "armorOwners", new Object[] { Required<EarthArmorController>(player), Required<EarthArmorController>(graph.CloneOf(player)) });
                Set(world, "surfOwners", new Object[] { Required<EarthSurfController>(player), Required<EarthSurfController>(graph.CloneOf(player)) });
                Set(world, "clientSuspendedMutators", one.EarthMutationControls.Concat(two.EarthMutationControls).Concat(new Behaviour[] { scatter }).Cast<Object>().ToArray());
                Set(terrain, "planet", planet);
                // NGO may retain its manager across scene loads. Keep it separate so
                // the gameplay graph/frontend always remain owned by EarthCoreSlice.
                var networkOwner = new GameObject("Earth Online Network"); Undo.RegisterCreatedObjectUndo(networkOwner, "Create online transport");
                var manager = Undo.AddComponent<NetworkManager>(networkOwner);
                var relayTransport = Undo.AddComponent<UnityTransport>(networkOwner);
                manager.NetworkConfig.NetworkTransport = relayTransport;
                manager.NetworkConfig.EnableSceneManagement = false;
                manager.NetworkConfig.PlayerPrefab = null;
                manager.NetworkConfig.TickRate = 50;
                var session = Undo.AddComponent<MpsRelaySession>(owner); session.Configure(manager, "default");
                var transport = Undo.AddComponent<NgoGameplayTransport>(owner); transport.Configure(session, binding);
                var launch = Undo.AddComponent<OnlineLaunchProfile>(owner); launch.Configure(session);
                var frontend = Undo.AddComponent<EarthOnlineFrontend>(owner); frontend.Configure(flow, session, transport, binding);
                var probe = Undo.AddComponent<OnlineDevelopmentProbe>(owner); probe.Configure(frontend, session, transport, binding);
                Behaviour[] optionalCameraDrivers = cameraRoot.GetComponents<Behaviour>().Where(component =>
                    component.GetType().Name.StartsWith("EarthMiniBokeh", StringComparison.Ordinal) || component.GetType().Name == "MiniBokehController").ToArray();
                OnlinePresentationAuthoring.Install(owner, binding, graph, flow, menu, hud, controller, optionalCameraDrivers);
                OnlineArenaIdentityAuthoring.Stamp(binding, scene, initialHash);
                if (binding.Capabilities != OnlineCapabilities.All) throw new InvalidOperationException("Online graph is missing required gameplay references.");
                EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
                Debug.Log("Installed two authored Earth actors, Relay/NGO, authority/world bindings and presentation bridge. Review and save EarthCoreSlice. No broad arena rebuild. Use distinct --online-profile peer-one / peer-two in two processes.");
                Selection.activeGameObject = owner;
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        private static EarthOnlineGameplayBinding.Actor MakeActor(GameObject root, GameObject player, GameObject magic, GameObject camera, Component animation)
        {
            var body = Required<Rigidbody>(player);
            var input = Required<EarthInputAdapter>(player);
            var bridge = Add<OnlineEarthInputBridge>(player);
            Set(bridge, "input", input); Set(bridge, "castCamera", Required<Camera>(camera)); Set(bridge, "actorBody", body);
            var mutation = player.GetComponentsInChildren<Behaviour>(true).Concat(magic.GetComponentsInChildren<Behaviour>(true))
                .Where(component => MutatorNames.Contains(component.GetType().Name)).Distinct().ToArray();
            return new EarthOnlineGameplayBinding.Actor {
                Root = root, Body = body, Motor = Required<PlanetMotor>(player), Impact = Required<EarthCharacterImpactTarget>(player),
                Puppet = Required<ActiveRagdollPuppet>(player), Rig = Required<HumanoidRagdollRig>(player),
                Animator = Required<Animator>(animation.gameObject), Collider = Read<Collider>(Required<PlanetMotor>(player), "capsule"),
                CameraFrame = camera.transform, AuthoredMotorInput = Required<PlanetInputReader>(player), SemanticInput = input,
                LocalInput = Add<OnlineLocalMotorInput>(player), RemoteInput = Add<OnlineRemoteMotorInput>(player), EarthInput = bridge,
                Executor = Required<MagicExecutor>(magic), MagicInput = Required<MagicInputController>(player),
                ActionRouter = Required<EarthActionRouterBehaviour>(player), DualMouse = Required<EarthDualMouseAbilityController>(player),
                DebrisPool = Required<EarthRockDebrisPool>(magic),
                LocalOnlyControls = new Behaviour[] { Required<PlayerInput>(player) }, EarthMutationControls = mutation
            };
        }
        private static T Add<T>(GameObject owner) where T : Component => owner.GetComponent<T>() ?? Undo.AddComponent<T>(owner);
        private static T Required<T>(GameObject owner) where T : Component
        {
            T[] direct = owner.GetComponents<T>();
            if (direct.Length == 1) return direct[0];
            T[] components = owner.GetComponentsInChildren<T>(true);
            if (components.Length != 1) throw new InvalidOperationException($"{owner.name} needs exactly one authored {typeof(T).Name}, found {components.Length}.");
            return components[0];
        }
        private static GameObject ExactRoot(GameObject[] roots, string name) => roots.SingleOrDefault(root => root.name == name)
            ?? throw new InvalidOperationException("Required authored scene root missing: " + name);
        private static T Read<T>(Object owner, string field) where T : Object => new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as T
            ?? throw new InvalidOperationException(owner.name + "." + field + " has no authored reference.");
        private static void WriteActor(Object owner, string field, EarthOnlineGameplayBinding.Actor actor)
        {
            foreach (var member in typeof(EarthOnlineGameplayBinding.Actor).GetFields())
                if (member.IsPublic) Set(owner, field + "." + member.Name, member.GetValue(actor));
        }
        internal static void Set(Object owner, string field, object value)
        {
            var serialized = new SerializedObject(owner); var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(owner.GetType().Name + "." + field + " authoring seam changed.");
            if (value is ulong hash) property.ulongValue = hash;
            else if (value is Object[] values)
            { property.arraySize = values.Length; for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
            else property.objectReferenceValue = (Object)value;
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(owner);
        }
        private static void ClearSceneReferences(Component owner)
        {
            var serialized = new SerializedObject(owner); var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null &&
                    !EditorUtility.IsPersistent(property.objectReferenceValue) && !property.propertyPath.StartsWith("m_", StringComparison.Ordinal)) property.objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void AddRoot(List<Transform> roots, Transform candidate)
        {
            if (roots.Any(root => candidate.IsChildOf(root))) return;
            roots.RemoveAll(root => root.IsChildOf(candidate)); roots.Add(candidate);
        }
        private static void ValidateExternalReferences(OnlineActorGraphAuthoring.Result graph, GameObject[] allowedRoots, Component[] shared)
        {
            foreach (Component component in graph.Container.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Actor graph contains a missing script.");
                var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object reference = property.objectReferenceValue;
                    if (reference == null || EditorUtility.IsPersistent(reference) || shared.Contains(reference)) continue;
                    Transform target = reference is GameObject go ? go.transform : reference is Component child ? child.transform : null;
                    if (target == null || target.IsChildOf(graph.Container.transform) || allowedRoots.Any(root => target.IsChildOf(root.transform))) continue;
                    throw new InvalidOperationException($"Unexpected external actor reference: {component.name}/{component.GetType().Name}.{property.propertyPath} -> {target.name}. Add its explicit owner root or classify a shared service; installation rolled back.");
                }
            }
        }
    }
}
