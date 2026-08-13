using System;
using System.IO;
using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public static class VoxelSaveCodec
    {
        private const uint Magic = 0x31565045u;
        private const uint SupportedFlags = 0u;
        public const ushort CurrentVersion = 2;

        public static void Write(Stream output, VoxelPlanetState state)
        {
            if (output == null || !output.CanWrite)
            {
                throw new ArgumentException("Output stream must be writable.", nameof(output));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            using BinaryWriter writer = new BinaryWriter(output, System.Text.Encoding.UTF8, true);
            writer.Write(Magic);
            writer.Write(CurrentVersion);
            writer.Write(SupportedFlags);
            WritePayload(writer, state);
        }

        private static void WritePayload(BinaryWriter writer, VoxelPlanetState state)
        {
            writer.Write(state.Radius);
            writer.Write(state.Seed);
            writer.Write(state.ChunkResolution);
            writer.Write(state.CellSize);
            writer.Write(state.NoiseAmplitude);
            writer.Write(state.EditCount);

            for (int index = 0; index < state.EditCount; index++)
            {
                SdfEdit edit = state.GetEdit(index);
                writer.Write(edit.Sequence);
                writer.Write((byte)edit.Kind);
                WriteFloat3(writer, edit.PointA);
                WriteFloat3(writer, edit.PointB);
                writer.Write(edit.Radius);
                writer.Write(edit.Material.Value);
            }
        }

        public static VoxelPlanetState Read(Stream input)
        {
            return ReadWithReport(input).State;
        }

        public static VoxelSaveLoadResult ReadWithReport(Stream input)
        {
            if (input == null || !input.CanRead)
            {
                throw new ArgumentException("Input stream must be readable.", nameof(input));
            }

            using BinaryReader reader = new BinaryReader(input, System.Text.Encoding.UTF8, true);
            if (reader.ReadUInt32() != Magic)
            {
                throw new InvalidDataException("Not an Elemental voxel save.");
            }

            ushort version = reader.ReadUInt16();
            if (version < 1 || version > CurrentVersion)
            {
                throw new InvalidDataException($"Unsupported voxel save version {version}.");
            }

            if (version >= 2)
            {
                uint flags = reader.ReadUInt32();
                if ((flags & ~SupportedFlags) != 0u)
                    throw new InvalidDataException($"Unsupported voxel save flags 0x{flags:X8}.");
            }

            VoxelPlanetState state = new VoxelPlanetState(
                reader.ReadSingle(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadSingle());
            int editCount = reader.ReadInt32();
            if (editCount < 0 || editCount > 1_000_000)
            {
                throw new InvalidDataException($"Invalid edit count {editCount}.");
            }

            SdfEdit[] edits = new SdfEdit[editCount];
            for (int index = 0; index < editCount; index++)
            {
                edits[index] = new SdfEdit(
                    reader.ReadUInt32(),
                    (SdfEditKind)reader.ReadByte(),
                    ReadFloat3(reader),
                    ReadFloat3(reader),
                    reader.ReadSingle(),
                    new VoxelMaterialId(reader.ReadByte()));
            }

            if (edits.Length > 0)
            {
                state.Apply(new EditBatch(edits));
            }

            return new VoxelSaveLoadResult(state, version, CurrentVersion);
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static float3 ReadFloat3(BinaryReader reader)
        {
            return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    public readonly struct VoxelSaveLoadResult
    {
        public VoxelSaveLoadResult(VoxelPlanetState state, ushort sourceVersion, ushort targetVersion)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            SourceVersion = sourceVersion;
            TargetVersion = targetVersion;
        }

        public VoxelPlanetState State { get; }
        public ushort SourceVersion { get; }
        public ushort TargetVersion { get; }
        public bool WasMigrated => SourceVersion != TargetVersion;
    }
}
