using System;
using System.Runtime.InteropServices;
using Elemental.Simulation.Fire;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.Fire
{
    /// <summary>Bounded cosmetic streamlines; FireWorld and its finite contact patches remain authoritative.</summary>
    public sealed class FireCoherentBodyMeshBackend : IDisposable
    {
        private const int Nodes = 6, Lanes = 3, Sections = 49;
        private const int VertexCapacity = Nodes * Lanes * Sections * 2;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Fire.CoherentBody.Step");
        private static readonly int ClockId = Shader.PropertyToID("_FireTime");
        private static readonly int FadeId = Shader.PropertyToID("_BodyFade");
        private static readonly int CountId = Shader.PropertyToID("_ContactCount");
        private static readonly int PointId = Shader.PropertyToID("_ContactPointRadius");
        private static readonly int NormalId = Shader.PropertyToID("_ContactNormalSkin");
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector2 UV;
            public Vector2 Shape;
        }
        private readonly FireVisualProfile profile;
        private readonly FireContactPatch[] contacts = new FireContactPatch[8];
        private readonly Vector4[] contactPoints = new Vector4[8], contactNormals = new Vector4[8];
        private NativeArray<Vertex> vertices;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private readonly GameObject visual;
        private float fade;
        private bool disposed;
        public bool Visible => renderer != null && renderer.enabled;
        public int ActiveTriangles { get; private set; }
        public double LastStepMilliseconds { get; private set; }
        public int RedirectedSegments { get; private set; }
        public int ActiveRibbons => ActiveTriangles / ((Sections - 1) * 2);
        public int SectionCount => Sections;
        public bool TryGetCenterlinePoint(int ribbon, int section, out Vector3 position)
        {
            position = default;
            if (disposed || ribbon < 0 || ribbon >= ActiveRibbons || section < 0 || section >= Sections) return false;
            int i = (ribbon * Sections + section) * 2;
            position = (vertices[i].Position + vertices[i + 1].Position) * .5f;
            return true;
        }

        public FireCoherentBodyMeshBackend(Transform owner, FireVisualProfile settings)
        {
            profile = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            Shader shader = settings.CoherentBodyShader;
            if (shader == null || !shader.isSupported)
                throw new NotSupportedException("Fire coherent body shader is missing or unsupported. Import FireCoherentBody.shader before enabling CoherentBody.");
            material = new Material(shader) { name = "Fire connected body clock" };
            if (settings.CpuMaterial != null) material.CopyPropertiesFromMaterial(settings.CpuMaterial);
            material.renderQueue = 2998;
            vertices = new NativeArray<Vertex>(VertexCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mesh = new Mesh { name = "Fire connected flow ribbons" }; mesh.MarkDynamic();
            mesh.SetVertexBufferParams(VertexCapacity,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2));
            var indices = new ushort[Nodes * Lanes * (Sections - 1) * 6];
            for (int lane = 0; lane < Nodes * Lanes; lane++)
                for (int section = 0; section < Sections - 1; section++)
                {
                    int offset = (lane * (Sections - 1) + section) * 6;
                    int v = (lane * Sections + section) * 2;
                    indices[offset] = (ushort)v; indices[offset + 1] = (ushort)(v + 1); indices[offset + 2] = (ushort)(v + 2);
                    indices[offset + 3] = (ushort)(v + 1); indices[offset + 4] = (ushort)(v + 3); indices[offset + 5] = (ushort)(v + 2);
                }
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length);
            mesh.subMeshCount = 1; mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0));
            visual = new GameObject("Fire connected body"); visual.transform.SetParent(owner, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = visual.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
        }

        public void Step(FirePresentationSnapshot snapshot, float delta, UnityEngine.Camera camera)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FireCoherentBodyMeshBackend));
            if (snapshot == null || snapshot.NodeCount < 0 || snapshot.NodeCount > Nodes ||
                snapshot.ContactCount < 0 || snapshot.ContactCount > 8 || !math.isfinite(delta) || delta < 0 || !math.isfinite(snapshot.Time))
                throw new ArgumentException("Coherent fire requires finite time and at most 6 nodes / 8 contacts.");
            using (Marker.Auto())
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                fade = snapshot.Emits ? math.saturate(snapshot.Energy) : math.max(0, fade - delta / math.max(.05f, profile.MaxLifetime));
                if (snapshot.Lifecycle == FireLifecycle.Retired || fade <= 0 || snapshot.NodeCount == 0) { Clear(); return; }
                for (int i = 0; i < snapshot.ContactCount; i++)
                {
                    contacts[i] = snapshot.Contacts[i];
                    // Trace the instantaneous velocity field against geometry at this snapshot.
                    // Spatial integration must not predict a moving wall into the future.
                    contacts[i].SurfaceVelocity = float3.zero; contacts[i].AngularVelocity = float3.zero;
                    var c = contacts[i];
                    contactPoints[i] = new Vector4(c.Point.x, c.Point.y, c.Point.z, c.Active ? c.Radius : 0);
                    contactNormals[i] = new Vector4(c.Normal.x, c.Normal.y, c.Normal.z, c.Skin);
                }
                material.SetInt(CountId, snapshot.ContactCount);
                material.SetVectorArray(PointId, contactPoints); material.SetVectorArray(NormalId, contactNormals);
                material.SetFloat(ClockId, snapshot.Time); material.SetFloat(FadeId, fade);
                float3 eye = camera != null ? (float3)camera.transform.position : snapshot.Origin + new float3(0, 0, -10);
                float3 cameraRight = camera != null ? (float3)camera.transform.right : new float3(1, 0, 0);
                float3 min = new float3(float.PositiveInfinity), max = new float3(float.NegativeInfinity);
                int ribbons = 0; RedirectedSegments = 0;
                for (int n = 0; n < snapshot.NodeCount; n++)
                {
                    var node = snapshot.Nodes[n]; if (!node.Active || !node.IsValid || node.Density <= 0) continue;
                    float3 axis = FireContactMath.SafeNormal(node.B - node.A, node.Up);
                    float3 side = FireContactMath.Tangent(axis);
                    float duration = math.clamp(profile.MaxLifetime * .8f, .15f, .85f);
                    float length = math.length(node.B-node.A);
                    bool capsule = node.Shape == FireShape.Capsule;
                    // A clipped source has a clipped cosmetic trace too, including
                    // unsupported cover that cannot publish a surface patch.
                    if(capsule)duration=math.min(duration,length/math.max(1,math.length(node.Flow)));
                    for (int lane = 0; lane < Lanes; lane++)
                    {
                        float h = duration * (capsule ? (lane == 0 ? 1f : lane == 1 ? .81f : .67f) : 1f) / (Sections-1);
                        float offset = capsule ? 0 : lane == 0 ? 0 : lane == 1 ? -.32f : .32f;
                        float phase = node.Phase + lane * 2.0944f;
                        float3 position = node.A + side * (node.Radius * offset);
                        // Shell emission is located on its surface rather than inside the sphere.
                        if (node.Shape == FireShape.Shell) position = node.A + FireContactMath.SafeNormal(side * math.cos(phase) + node.Up * math.sin(phase), node.Up) * node.Radius;
                        FireCpuField.Sample(snapshot, position, snapshot.Time, out float3 velocity, out _);
                        float arc = 0;
                        for (int section = 0; section < Sections; section++)
                        {
                            float u = section / (float)(Sections - 1);
                            float3 tangent = FireContactMath.SafeNormal(velocity, axis);
                            float3 facing = FireContactMath.SafeNormal(eye - position, new float3(0, 0, -1));
                            float3 ribbonSide = FireContactMath.SafeNormal(math.cross(tangent, facing), cameraRight);
                            float width = node.Radius * (lane == 0 ? .96f : .66f) *
                                (1 - math.smoothstep(.58f, 1f, u)) * (.86f + .14f * math.sin(u * math.PI));
                            float3 displayPosition=position;
                            if(capsule)
                            {
                                // A connected muzzle opens into staggered, curling tongues.
                                // These bounded cosmetic offsets never feed back into field/damage.
                                float travel=arc*1.35f-snapshot.Time*4.1f+phase;
                                float open=math.smoothstep(0,.16f,u);
                                float tip=1-math.smoothstep(.60f,1,u);
                                float flutter=node.Radius*open*math.sin(u*math.PI)*
                                    (lane==0?.7f:1.05f);
                                float3 transverse=math.cross(axis,side);
                                displayPosition+=side*(flutter*math.sin(travel))+
                                    transverse*(flutter*.65f*math.sin(travel*.71f+phase));
                                // The two shorter tongues finish off-axis instead of converging
                                // into the same spear tip. Their roots remain exactly shared.
                                float3 liftAxis=FireContactMath.SafeNormal(node.Up-axis*math.dot(node.Up,axis),transverse);
                                float branch=lane==0?.18f:lane==1?2.7f:1.4f;
                                float terminal=math.saturate((u-.55f)/.45f);
                                float fork=terminal*terminal*math.saturate(length/8f);
                                displayPosition+=liftAxis*(node.Radius*branch*fork)+
                                    side*(node.Radius*(lane-1)*.9f*fork);
                                float laneLength=math.max(.01f,length*(lane==0?1f:lane==1?.81f:.67f));
                                float derivative=2*terminal*math.saturate(length/8f)/(.45f*laneLength);
                                float3 curvedTangent=FireContactMath.SafeNormal(tangent+
                                    liftAxis*(node.Radius*branch*derivative)+side*(node.Radius*(lane-1)*.9f*derivative),tangent);
                                ribbonSide=FireContactMath.SafeNormal(math.cross(curvedTangent,facing),cameraRight);
                                float lobe=.79f+.14f*math.sin(travel*1.17f)+.07f*math.sin(travel*2.31f+phase);
                                width=node.Radius*(lane==0?1.9f:1.25f)*
                                    open*tip*lobe;
                            }
                            else width*=1+.07f*math.sin(arc*2.1f-snapshot.Time*3+phase);
                            int vertex = (ribbons * Sections + section) * 2;
                            float3 left = displayPosition-ribbonSide*width, right = displayPosition+ribbonSide*width;
                            if(capsule)
                            {
                                left=LimitToSourceLength(left,node.A,axis,length);
                                right=LimitToSourceLength(right,node.A,axis,length);
                                // Correct visible offsets against finite real patches, not an
                                // invented infinite plane. Shader clipping remains the final guard.
                                float3 cosmeticVelocity=float3.zero;
                                for(int pass=0;pass<3;pass++)for(int c=0;c<snapshot.ContactCount;c++)
                                {
                                    FireContactMath.ResolveSwept(contacts[c],position,0,0,profile.ParticleRadius,phase,ref left,ref cosmeticVelocity);
                                    FireContactMath.ResolveSwept(contacts[c],position,0,0,profile.ParticleRadius,phase,ref right,ref cosmeticVelocity);
                                }
                                left=LimitToSourceLength(left,node.A,axis,length);
                                right=LimitToSourceLength(right,node.A,axis,length);
                            }
                            vertices[vertex] = new Vertex { Position = (Vector3)left, UV = new Vector2(-1, arc), Shape = new Vector2(u, phase) };
                            vertices[vertex + 1] = new Vertex { Position = (Vector3)right, UV = new Vector2(1, arc), Shape = new Vector2(u, phase) };
                            min = math.min(min, math.min(left, right)); max = math.max(max, math.max(left, right));
                            if (section == Sections - 1) continue;
                            float3 previous = position;
                            FireCpuField.Sample(snapshot, position, snapshot.Time, out float3 target, out float response);
                            velocity = response > 0 ? math.lerp(velocity, target, 1 - math.exp(-response * h)) :
                                (velocity + snapshot.FreeUp * math.max(0, profile.FreeLift) * h) * math.exp(-math.max(0, profile.FreeDrag) * h);
                            for (int c = 0; c < snapshot.ContactCount; c++) FireCpuField.Steer(contacts[c], position, 0, profile.ParticleRadius, phase, h, ref velocity);
                            velocity = FireContactMath.Limit(velocity, profile.MaximumSpeed);
                            position += velocity * h;
                            for (int pass = 0; pass < 3; pass++)
                                for (int c = 0; c < snapshot.ContactCount; c++)
                                    if (FireContactMath.ResolveSwept(contacts[c], previous, 0, h, profile.ParticleRadius, phase, ref position, ref velocity))
                                        RedirectedSegments++;
                            arc += math.distance(previous, position);
                        }
                        ribbons++;
                    }
                }
                ActiveTriangles = ribbons * (Sections - 1) * 2;
                renderer.enabled = ribbons > 0;
                if (ribbons > 0)
                {
                    mesh.SetVertexBufferData(vertices, 0, 0, ribbons * Sections * 2, 0, MeshUpdateFlags.DontRecalculateBounds);
                    mesh.SetSubMesh(0, new SubMeshDescriptor(0, ActiveTriangles * 3), MeshUpdateFlags.DontRecalculateBounds);
                    var bounds = new Bounds((Vector3)((min + max) * .5f), (Vector3)math.max(max - min, new float3(.01f)));
                    // Vertex positions are world coordinates, as in FireCpuMeshBackend.
                    mesh.bounds = bounds; renderer.bounds = bounds;
                }
                LastStepMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            }
        }
        private static float3 LimitToSourceLength(float3 position,float3 origin,float3 axis,float length)
        {
            float projected=math.dot(position-origin,axis);
            return position+axis*(math.clamp(projected,0,length)-projected);
        }
        public void Clear() { fade = 0; ActiveTriangles = 0; if (renderer != null) renderer.enabled = false; }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (vertices.IsCreated) vertices.Dispose();
            if (visual != null) UnityEngine.Object.Destroy(visual);
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}

