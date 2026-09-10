using System;
using System.Runtime.CompilerServices;

namespace Elemental.Runtime.Diagnostics
{
    // Explicit QA activation only. Main-thread synchronous managed allocations; never an allocation profiler replacement.
    public static class HardPolishAllocationCounters
    {
        public enum Path { FireBindingUpdate, FireBindingLate, FireAuthorityFixed, FireBegin, FireStop,
            GoldLate, StageLate, ResponsePoll, ResponseCue, ResponseAudioUpdate, ResponseAudioCue, DustLate, SchoolHud, Count }
        public const int PhaseCount=4;
        private static readonly long[] Calls=new long[PhaseCount*(int)Path.Count];
        private static readonly long[] Bytes=new long[Calls.Length];
        private static readonly long[] Nonzero=new long[Calls.Length];
        private static readonly long[] Maximum=new long[Calls.Length];
        private static bool enabled;
        private static volatile object escapedCalibration;
        public static bool IsSupported { get; private set; }
        public static long CalibrationBytes { get; private set; }
        public static long SmallCalibrationBytes { get; private set; }
        public static string CounterStatus { get; private set; }="NOT_CALIBRATED";
        [MethodImpl(MethodImplOptions.NoInlining|MethodImplOptions.NoOptimization)]
        private static void AllocateEscaping(int length)
        {var bytes=new byte[length];bytes[length-1]=23;escapedCalibration=bytes;}
        private static void Calibrate()
        {
            IsSupported=false;CalibrationBytes=SmallCalibrationBytes=-1;
            try
            {
                AllocateEscaping(16); // Warm the actual non-inlined allocation function.
                long smallBefore=GC.GetAllocatedBytesForCurrentThread();AllocateEscaping(128);
                SmallCalibrationBytes=GC.GetAllocatedBytesForCurrentThread()-smallBefore;
                long before=GC.GetAllocatedBytesForCurrentThread();
                AllocateEscaping(8192);AllocateEscaping(65536);
                long after=GC.GetAllocatedBytesForCurrentThread();CalibrationBytes=after-before;
                IsSupported=SmallCalibrationBytes>=128&&CalibrationBytes>=8192+65536;
                CounterStatus=IsSupported?"CALIBRATED_ESCAPING_ALLOCATION":"UNSUPPORTED_RUNTIME_COUNTER";
            }
            catch(Exception error){CounterStatus="UNSUPPORTED_"+error.GetType().Name;}
            finally{escapedCalibration=null;}
        }
        private static int currentPhase;
        public static void Reset()
        {
            enabled=false;Array.Clear(Calls,0,Calls.Length);Array.Clear(Bytes,0,Bytes.Length);
            Array.Clear(Nonzero,0,Nonzero.Length);Array.Clear(Maximum,0,Maximum.Length);
            // Cold explicit QA calibration: zero-reading APIs cannot certify zero allocations.
            Calibrate();
        }
        public static void SetWindow(bool active,int phase)
        { enabled=active&&phase>=0&&phase<PhaseCount;currentPhase=phase; }
        public static Scope Measure(Path path)=>enabled?new Scope(currentPhase*(int)Path.Count+(int)path):default;
        public readonly struct Scope : IDisposable
        {
            private readonly int indexPlusOne;
            private readonly long start;
            private readonly bool native;
            internal Scope(int index){indexPlusOne=index+1;native=HardPolishNativeGcCapture.BeginPath(index);start=IsSupported?GC.GetAllocatedBytesForCurrentThread():-1;}
            public void Dispose()
            {
                if(indexPlusOne==0)return;
                int index=indexPlusOne-1;
                try
                {
                    if(start<0){Calls[index]++;return;}
                    long bytes=Math.Max(0,GC.GetAllocatedBytesForCurrentThread()-start);
                    Calls[index]++;Bytes[index]+=bytes;if(bytes>0)Nonzero[index]++;
                    if(bytes>Maximum[index])Maximum[index]=bytes;
                }
                finally{if(native)HardPolishNativeGcCapture.EndPath(index);}
            }
        }
        [Serializable] public sealed class Sample
        {
            public string phase,path;
            public long calls,inclusiveBytes,nonzeroCalls,maxBytesPerCall;
            public string result;
        }
        // Snapshot allocates only after capture stops. Inclusive nested callbacks are deliberately not summed.
        public static Sample[] Snapshot()
        {
            var result=new Sample[Calls.Length];string[] phases={"steady","combat","heavy","transitions"};
            for(int phase=0;phase<PhaseCount;phase++)for(int path=0;path<(int)Path.Count;path++)
            {
                int i=phase*(int)Path.Count+path;
                result[i]=new Sample{phase=phases[phase],path=((Path)path).ToString(),calls=Calls[i],inclusiveBytes=IsSupported?Bytes[i]:-1,
                    nonzeroCalls=IsSupported?Nonzero[i]:-1,maxBytesPerCall=IsSupported?Maximum[i]:-1,result=!IsSupported?"UNSUPPORTED_RUNTIME_COUNTER":Calls[i]==0?"UNEXERCISED":Bytes[i]==0?"ZERO_IN_COVERED_CALLS":"ALLOCATIONS_OBSERVED"};
            }
            return result;
        }
    }
}
