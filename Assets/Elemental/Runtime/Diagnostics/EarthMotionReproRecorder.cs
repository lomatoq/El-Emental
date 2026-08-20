using System;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Runtime.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class EarthMotionReproRecorder : MonoBehaviour
    {
        private const int FrameCapacity = 720;
        private const int FaultCapacity = 96;
        private readonly PlanetMotionFrame[] _frames = new PlanetMotionFrame[FrameCapacity];
        private readonly MotionFaultEvent[] _faults = new MotionFaultEvent[FaultCapacity];
        private int _frameWrite;
        private int _frameCount;
        private int _faultWrite;
        private int _faultCount;

        public event Action<MotionFaultEvent> FaultRecorded;

        public int RecordedFrameCount => _frameCount;
        public int RecordedFaultCount => _faultCount;
        public uint Seed { get; private set; }
        public uint ProfileHash { get; private set; }

        public void Configure(uint seed, uint profileHash)
        {
            Seed = seed;
            ProfileHash = profileHash;
        }

        public void Record(in PlanetMotionFrame frame, MotionFaultKind faults)
        {
            _frames[_frameWrite] = frame;
            _frameWrite = (_frameWrite + 1) % FrameCapacity;
            _frameCount = Mathf.Min(FrameCapacity, _frameCount + 1);
            if (faults == MotionFaultKind.None) return;

            var fault = new MotionFaultEvent(frame, faults, ProfileHash, Seed);
            _faults[_faultWrite] = fault;
            _faultWrite = (_faultWrite + 1) % FaultCapacity;
            _faultCount = Mathf.Min(FaultCapacity, _faultCount + 1);
            FaultRecorded?.Invoke(fault);
        }

        public int CopyRecentFramesNonAlloc(PlanetMotionFrame[] destination)
        {
            if (destination == null || destination.Length == 0) return 0;
            int count = Mathf.Min(destination.Length, _frameCount);
            int start = (_frameWrite - count + FrameCapacity) % FrameCapacity;
            for (int index = 0; index < count; index++)
                destination[index] = _frames[(start + index) % FrameCapacity];
            return count;
        }

        public int CopyFaultsNonAlloc(MotionFaultEvent[] destination)
        {
            if (destination == null || destination.Length == 0) return 0;
            int count = Mathf.Min(destination.Length, _faultCount);
            int start = (_faultWrite - count + FaultCapacity) % FaultCapacity;
            for (int index = 0; index < count; index++)
                destination[index] = _faults[(start + index) % FaultCapacity];
            return count;
        }
    }
}
