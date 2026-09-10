using System;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
namespace Elemental.Runtime.Diagnostics
{
    // Explicit diagnostic owner only. Use a Development Player; do not combine its
    // expensive allocation callstacks with frame-time acceptance measurements.
    public sealed class HardPolishNativeGcCapture : IDisposable
    {
        public const string Prefix="HP.GC.";
        public const string CalibrationStart="HP.GC.Calibration.Start",CalibrationEnd="HP.GC.Calibration.End";
        private static readonly ProfilerMarker StartMarker=new ProfilerMarker(CalibrationStart);
        private static readonly ProfilerMarker EndMarker=new ProfilerMarker(CalibrationEnd);
        private static readonly ProfilerMarker[] Markers=CreateMarkers();
        private static bool active;
        private static volatile object escaped;
        private readonly bool previousEnabled,previousCalls,previousCpu,previousMemory;
        private readonly string previousFile;
        private bool disposed;
        public static bool Active=>active;
        public HardPolishNativeGcCapture(string absoluteBinaryPath)
        {
            if(!Debug.isDebugBuild)throw new InvalidOperationException("Native GC capture requires a Development build.");
            if(active||Profiler.enableBinaryLog)throw new InvalidOperationException("An existing binary profiler owner must finish first.");
            if(!Path.IsPathRooted(absoluteBinaryPath))throw new ArgumentException("Use an absolute .raw path.");
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteBinaryPath));
            if(File.Exists(absoluteBinaryPath))throw new IOException("Refusing to overwrite existing profiler evidence.");
            previousEnabled=Profiler.enabled;previousCalls=Profiler.enableAllocationCallstacks;
            previousCpu=Profiler.GetAreaEnabled(ProfilerArea.CPU);previousMemory=Profiler.GetAreaEnabled(ProfilerArea.Memory);
            previousFile=Profiler.logFile;
            Profiler.SetAreaEnabled(ProfilerArea.CPU,true);Profiler.SetAreaEnabled(ProfilerArea.Memory,true);
            Profiler.enableAllocationCallstacks=true;Profiler.logFile=absoluteBinaryPath;
            Profiler.enableBinaryLog=true;Profiler.enabled=true;active=true;
        }
        // Call after at least two rendered frames following Begin, and again before
        // Stop; keep two rendered frames after the final calibration so it is flushed.
        public static void Calibrate(bool final)
        {
            if(!active)throw new InvalidOperationException("Start native capture before calibrating.");
            var marker=final?EndMarker:StartMarker;
            marker.Begin();
            try{Allocate(128);Allocate(8192);}
            finally{marker.End();escaped=null;}
        }
        [MethodImpl(MethodImplOptions.NoInlining|MethodImplOptions.NoOptimization)]
        private static void Allocate(int size){var bytes=new byte[size];bytes[size-1]=17;escaped=bytes;}
        private static ProfilerMarker[] CreateMarkers()
        {
            int paths=(int)HardPolishAllocationCounters.Path.Count;
            var markers=new ProfilerMarker[HardPolishAllocationCounters.PhaseCount*paths];
            string[] phases={"steady","combat","heavy","transitions"};
            for(int phase=0;phase<phases.Length;phase++)for(int path=0;path<paths;path++)
                markers[phase*paths+path]=new ProfilerMarker(Prefix+phases[phase]+"."+((HardPolishAllocationCounters.Path)path));
            return markers;
        }
        internal static bool BeginPath(int index)
        {if(!active)return false;Markers[index].Begin();return true;}
        internal static void EndPath(int index)=>Markers[index].End();
        public void Dispose()
        {
            if(disposed)return;disposed=true;active=false;
            Profiler.enabled=false;Profiler.enableBinaryLog=false;Profiler.logFile=previousFile;
            Profiler.enableAllocationCallstacks=previousCalls;
            Profiler.SetAreaEnabled(ProfilerArea.CPU,previousCpu);Profiler.SetAreaEnabled(ProfilerArea.Memory,previousMemory);
            Profiler.enabled=previousEnabled;
        }
    }
}
