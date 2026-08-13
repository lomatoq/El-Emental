using Elemental.Simulation.Structures;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>Thin PhysX adapter for one canonical bond slot.</summary>
    public sealed class EarthBondRuntime
    {
        public EarthBondRuntime(int index, EarthBondId id, ConfigurableJoint joint)
        {
            Index = index;
            Id = id;
            Joint = joint;
        }

        public int Index { get; }
        public EarthBondId Id { get; }
        public ConfigurableJoint Joint { get; }
        public bool IsReleased { get; private set; } = true;

        public void Activate(Rigidbody connectedBody)
        {
            if (Joint == null) return;
            Joint.autoConfigureConnectedAnchor = true;
            Joint.connectedBody = connectedBody;
            Joint.xMotion = ConfigurableJointMotion.Locked;
            Joint.yMotion = ConfigurableJointMotion.Locked;
            Joint.zMotion = ConfigurableJointMotion.Locked;
            Joint.angularXMotion = ConfigurableJointMotion.Locked;
            Joint.angularYMotion = ConfigurableJointMotion.Locked;
            Joint.angularZMotion = ConfigurableJointMotion.Locked;
            Joint.enableCollision = false;
            Joint.enablePreprocessing = false;
            IsReleased = false;
        }

        public void Release()
        {
            if (Joint == null) return;
            Joint.xMotion = ConfigurableJointMotion.Free;
            Joint.yMotion = ConfigurableJointMotion.Free;
            Joint.zMotion = ConfigurableJointMotion.Free;
            Joint.angularXMotion = ConfigurableJointMotion.Free;
            Joint.angularYMotion = ConfigurableJointMotion.Free;
            Joint.angularZMotion = ConfigurableJointMotion.Free;
            Joint.connectedBody = null;
            Joint.enableCollision = true;
            IsReleased = true;
        }

        public void ResetForPool()
        {
            Release();
            if (Joint != null)
            {
                Joint.breakForce = Mathf.Infinity;
                Joint.breakTorque = Mathf.Infinity;
            }
        }
    }
}
