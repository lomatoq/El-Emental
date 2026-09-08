using System;
using UnityEngine;

namespace Elemental.Presentation.Fire
{
    // Each record is written explicitly as SIX float4 rows. GPU stride is 16,
    // not sizeof(record). The CPU and HLSL row definitions must stay identical.
    [Serializable]
    public struct FireGpuNode
    {
        public Vector4 A_Radius;
        public Vector4 B_ShellHalfThickness;
        public Vector4 Flow_Response;
        public Vector4 Up_Lift;
        public Vector4 Dynamics; // swirl, noise speed, frequency, shape enum
        public Vector4 State;    // density, active, stable phase, max target speed

        public void Write(Vector4[] rows, int offset)
        {
            rows[offset] = A_Radius;
            rows[offset + 1] = B_ShellHalfThickness;
            rows[offset + 2] = Flow_Response;
            rows[offset + 3] = Up_Lift;
            rows[offset + 4] = Dynamics;
            rows[offset + 5] = State;
        }
    }

    [Serializable]
    public struct FireGpuContact
    {
        public Vector4 Point_Radius;
        public Vector4 Normal_FrontDepth;
        public Vector4 Tangent_RecoveryDepth;
        public Vector4 Velocity_SpreadFraction;
        public Vector4 AngularVelocity_Response;
        public Vector4 Settings; // skin, active, reserved, reserved

        public void Write(Vector4[] rows, int offset)
        {
            rows[offset] = Point_Radius;
            rows[offset + 1] = Normal_FrontDepth;
            rows[offset + 2] = Tangent_RecoveryDepth;
            rows[offset + 3] = Velocity_SpreadFraction;
            rows[offset + 4] = AngularVelocity_Response;
            rows[offset + 5] = Settings;
        }
    }
}
