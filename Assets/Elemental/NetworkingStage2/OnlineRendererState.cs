using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>The Earth shaders' authored per-piece overrides, with explicit wire indices.</summary>
    public static class OnlineRendererState
    {
        private static readonly int[] Floats = {
            Shader.PropertyToID("_MagicAmount"), Shader.PropertyToID("_MacroFrequency"),
            Shader.PropertyToID("_MineralAmount"), Shader.PropertyToID("_StoneFamily"),
            Shader.PropertyToID("_StrataScale"), Shader.PropertyToID("_GrainScale"),
            Shader.PropertyToID("_FractureMappingEnabled") };
        private static readonly int ColorId = Shader.PropertyToID("_ExteriorColor");
        private static readonly int MatrixId = Shader.PropertyToID("_FractureLocalToStructure");
        public static OnlinePacket Capture(Renderer renderer, MaterialPropertyBlock scratch)
        {
            scratch.Clear(); renderer.GetPropertyBlock(scratch);
            var packet = new OnlinePacket { Kind = OnlineMessage.BodyVisual };
            for (int i = 0; i < Floats.Length; i++)
            {
                bool present = scratch.HasProperty(Floats[i]);
                if (present) packet.Flags |= 1u << i;
                packet.Path.Add(new float3(present ? scratch.GetFloat(Floats[i]) : 0, 0, 0));
            }
            if (scratch.HasProperty(ColorId))
            {
                packet.Flags |= 1u << 7; Color color = scratch.GetColor(ColorId);
                packet.A = new Vector3(color.r, color.g, color.b); packet.Value = color.a;
            }
            if (scratch.HasProperty(MatrixId))
            {
                packet.Flags |= 1u << 8; Matrix4x4 matrix = scratch.GetMatrix(MatrixId);
                for (int column = 0; column < 4; column++)
                {
                    Vector4 value = matrix.GetColumn(column);
                    packet.Path.Add(new float3(value.x, value.y, value.z));
                    packet.Path.Add(new float3(value.w, 0, 0));
                }
            }
            return packet;
        }
        public static bool Apply(Renderer renderer, in OnlinePacket packet, MaterialPropertyBlock scratch)
        {
            if (renderer == null || packet.Flags > 511 || packet.Path.Length != ((packet.Flags & 256) != 0 ? 15 : 7)) return false;
            scratch.Clear();
            for (int i = 0; i < Floats.Length; i++)
                if ((packet.Flags & (1u << i)) != 0) scratch.SetFloat(Floats[i], packet.Path[i].x);
            if ((packet.Flags & 128) != 0) scratch.SetColor(ColorId, new Color(packet.A.x, packet.A.y, packet.A.z, packet.Value));
            if ((packet.Flags & 256) != 0)
            {
                var matrix = new Matrix4x4();
                for (int column = 0; column < 4; column++)
                {
                    float3 xyz = packet.Path[7 + column * 2];
                    matrix.SetColumn(column, new Vector4(xyz.x, xyz.y, xyz.z, packet.Path[8 + column * 2].x));
                }
                scratch.SetMatrix(MatrixId, matrix);
            }
            renderer.SetPropertyBlock(scratch); return true;
        }
        public static int Signature(in OnlinePacket packet)
        {
            unchecked
            {
                int hash = (int)packet.Flags ^ packet.A.GetHashCode() ^ packet.Value.GetHashCode();
                for (int i = 0; i < packet.Path.Length; i++) hash = hash * 31 + packet.Path[i].GetHashCode();
                return hash;
            }
        }
    }
}
