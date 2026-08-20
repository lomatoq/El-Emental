using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Matter
{
    public static class EarthMatterRuntimeBridge
    {
        public static EarthMatterIdentity BindExistingRecord(
            Component target,
            EarthMatterKernelBehaviour kernel,
            EarthMatterId id,
            Rigidbody body)
        {
            if (target == null || kernel == null || !id.IsValid || !kernel.TryGet(id, out _)) return null;
            EarthMatterIdentity identity = target.GetComponent<EarthMatterIdentity>();
            if (identity == null) identity = target.gameObject.AddComponent<EarthMatterIdentity>();
            identity.AcceptRegistration(kernel, id);
            identity.BindBody(body);
            return identity;
        }

        public static EarthMatterIdentity EnsureIdentity(
            Component target,
            EarthMatterKernelBehaviour kernel,
            Rigidbody body,
            EarthMatterPhase phase,
            EarthRepresentationTier representation,
            EarthMaterialKind material,
            EarthShapeSemantic shape,
            float volume,
            float mass,
            in EarthSourceProvenance source,
            EarthOwnerId owner = default)
        {
            if (target == null || kernel == null) return null;
            EarthMatterIdentity identity = target.GetComponent<EarthMatterIdentity>();
            if (identity == null) identity = target.gameObject.AddComponent<EarthMatterIdentity>();
            Vector3 position = body != null ? body.position : target.transform.position;
            Quaternion rotation = body != null ? body.rotation : target.transform.rotation;
            var pose = new EarthMatterPose(
                new float3(position.x, position.y, position.z),
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
            var record = new EarthMatterRecord
            {
                Phase = phase,
                Representation = representation,
                Material = material,
                Volume = math.max(0.000001f, volume),
                Mass = math.max(0.000001f, mass),
                Integrity = 1f,
                Source = source,
                Owner = owner,
                Shape = shape,
                RestPose = pose,
                CurrentPose = pose,
                LinearVelocity = body != null
                    ? new float3(body.linearVelocity.x, body.linearVelocity.y, body.linearVelocity.z)
                    : float3.zero,
                AngularVelocity = body != null
                    ? new float3(body.angularVelocity.x, body.angularVelocity.y, body.angularVelocity.z)
                    : float3.zero
            };
            if (!identity.Configure(kernel, record, body))
                Debug.LogError($"[EarthMatter] Failed to register {target.name}: {kernel.Registry.LastFailure}", target);
            return identity;
        }
    }
}
