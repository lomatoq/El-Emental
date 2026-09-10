using System;
using System.Runtime.CompilerServices;
using Elemental.Runtime.Diagnostics;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class HardPolishAllocationCounterTests
    {
        private static volatile object escapingPayload;
        [MethodImpl(MethodImplOptions.NoInlining|MethodImplOptions.NoOptimization)]
        private static void AllocatePayload(int bytes){var value=new byte[bytes];value[bytes-1]=7;escapingPayload=value;}
        private static void RequireCalibratedCounter()
        {
            if(!HardPolishAllocationCounters.IsSupported)
                Assert.Ignore("Scoped allocation evidence unavailable: "+HardPolishAllocationCounters.CounterStatus+
                    "; escaping73728-byte cold positive control measured "+HardPolishAllocationCounters.CalibrationBytes+" bytes. Zero values are not evidence.");
        }
        [Test] public void ReportNeverCertifiesZeroWhenPositiveControlCannotObserveAllocation()
        {
            HardPolishAllocationCounters.Reset();HardPolishAllocationCounters.SetWindow(true,0);
            using(HardPolishAllocationCounters.Measure(HardPolishAllocationCounters.Path.FireBegin)){}
            HardPolishAllocationCounters.SetWindow(false,0);
            var row=HardPolishAllocationCounters.Snapshot()[(int)HardPolishAllocationCounters.Path.FireBegin];
            Assert.That(row.calls,Is.EqualTo(1));
            if(HardPolishAllocationCounters.IsSupported)Assert.That(HardPolishAllocationCounters.CalibrationBytes,Is.GreaterThanOrEqualTo(73728));
            else{Assert.That(row.result,Is.EqualTo("UNSUPPORTED_RUNTIME_COUNTER"));Assert.That(row.inclusiveBytes,Is.EqualTo(-1));Assert.That(row.maxBytesPerCall,Is.EqualTo(-1));}
        }
        [TearDown] public void Stop(){HardPolishAllocationCounters.SetWindow(false,0);escapingPayload=null;}
        [Test] public void DisabledAndWarmedValueScopesDoNotAllocatePerMeasurement()
        {
            HardPolishAllocationCounters.Reset();RequireCalibratedCounter();
            using(HardPolishAllocationCounters.Measure(HardPolishAllocationCounters.Path.FireBindingUpdate)){}
            Assert.That(HardPolishAllocationCounters.Snapshot()[0].calls,Is.Zero);
            HardPolishAllocationCounters.SetWindow(true,0);
            // Warm the exact generic-free call/Dispose path before measuring the instrumentation itself.
            using(HardPolishAllocationCounters.Measure(HardPolishAllocationCounters.Path.FireBindingUpdate)){}
            long start=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<1000;i++)using(HardPolishAllocationCounters.Measure(HardPolishAllocationCounters.Path.FireBindingUpdate)){}
            long overhead=GC.GetAllocatedBytesForCurrentThread()-start;
            HardPolishAllocationCounters.SetWindow(false,0);
            var sample=HardPolishAllocationCounters.Snapshot()[0];
            Assert.That(overhead,Is.Zero);Assert.That(sample.calls,Is.EqualTo(1001));Assert.That(sample.inclusiveBytes,Is.Zero);
        }
        [Test] public void NestedScopesReportInclusiveBytesWithoutPretendingTheyAreAdditive()
        {
            HardPolishAllocationCounters.Reset();RequireCalibratedCounter();AllocatePayload(16);HardPolishAllocationCounters.SetWindow(true,1);
            using(HardPolishAllocationCounters.Measure(HardPolishAllocationCounters.Path.FireAuthorityFixed))
            using(HardPolishAllocationCounters.Measure(HardPolishAllocationCounters.Path.FireStop))
            {AllocatePayload(128);}
            HardPolishAllocationCounters.SetWindow(false,1);
            var values=HardPolishAllocationCounters.Snapshot();int offset=(int)HardPolishAllocationCounters.Path.Count;
            var outer=values[offset+(int)HardPolishAllocationCounters.Path.FireAuthorityFixed];
            var inner=values[offset+(int)HardPolishAllocationCounters.Path.FireStop];
            Assert.That(inner.inclusiveBytes,Is.GreaterThanOrEqualTo(128));Assert.That(outer.inclusiveBytes,Is.GreaterThanOrEqualTo(inner.inclusiveBytes));
            Assert.That(inner.nonzeroCalls,Is.EqualTo(1));Assert.That(outer.nonzeroCalls,Is.EqualTo(1));
            Assert.That(values[0].result,Is.EqualTo("UNEXERCISED"));
        }
    }
}
