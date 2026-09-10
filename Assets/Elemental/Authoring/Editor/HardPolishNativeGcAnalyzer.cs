using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Runtime.Diagnostics;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
namespace Elemental.Authoring.Editor
{
    public static class HardPolishNativeGcAnalyzer
    {
        [Serializable] public sealed class ScopeResult
        {
            public string marker,result;
            public long calls,inclusiveGcEvents,inclusiveBytes;
        }
        [Serializable] public sealed class Allocation
        {
            public int frame,thread,sample;
            public string threadName;
            public long bytes;
            public string[] scopes,callstack;
        }
        [Serializable] public sealed class Report
        {
            public string binary,unityVersion,result,metadata;
            public int firstFrame,lastFrame,threadFrames,missingMainFrames,missingSizeMetadata,missingCallstacks;
            public long allGcEvents,allGcBytes;
            public bool calibrationPassed,allocationDetailsTruncated;
            public List<ScopeResult> scopes=new List<ScopeResult>();
            public List<Allocation> allocations=new List<Allocation>();
        }
        private struct ActiveScope { public int end;public ScopeResult result; }
        // Example: -executeMethod Elemental.Authoring.Editor.HardPolishNativeGcAnalyzer.Run
        // -hardPolishGcRaw C:/evidence/combat.raw -hardPolishGcJson C:/evidence/combat.json
        public static void Run()
        {
            string[] args=Environment.GetCommandLineArgs();
            string Argument(string flag){int i=Array.IndexOf(args,flag);if(i<0||i+1>=args.Length)throw new ArgumentException("Missing "+flag);return args[i+1];}
            Analyze(Argument("-hardPolishGcRaw"),Argument("-hardPolishGcJson"));
        }
        public static Report Analyze(string binary,string output)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Analyze after the player/capture has stopped.");
            if(!File.Exists(binary))throw new FileNotFoundException("Native profiler evidence is missing",binary);
            // This replaces only the Editor Profiler's current capture. Retain .raw originals.
            if(!ProfilerDriver.LoadProfile(binary,false))throw new IOException("Unity could not import native profiler evidence: "+binary);
            var report=new Report{binary=Path.GetFullPath(binary),unityVersion=Application.unityVersion,
                firstFrame=ProfilerDriver.firstFrameIndex,lastFrame=ProfilerDriver.lastFrameIndex};
            var results=new Dictionary<string,ScopeResult>();
            ScopeResult Scope(string name)
            {if(!results.TryGetValue(name,out var value)){value=new ScopeResult{marker=name};results.Add(name,value);report.scopes.Add(value);}return value;}
            string[] phases={"steady","combat","heavy","transitions"};
            foreach(string phase in phases)for(int p=0;p<(int)HardPolishAllocationCounters.Path.Count;p++)
                Scope(HardPolishNativeGcCapture.Prefix+phase+"."+(HardPolishAllocationCounters.Path)p);
            ScopeResult start=Scope(HardPolishNativeGcCapture.CalibrationStart),end=Scope(HardPolishNativeGcCapture.CalibrationEnd);
            var stack=new List<ActiveScope>();var addresses=new List<ulong>();
            if(report.firstFrame<0||report.lastFrame<report.firstFrame)report.missingMainFrames++;
            for(int frame=report.firstFrame;frame>=0&&frame<=report.lastFrame;frame++)
            {
                for(int thread=0;thread<512;thread++)
                {
                    using RawFrameDataView view=ProfilerDriver.GetRawFrameDataView(frame,thread);
                    if(!view.valid){if(thread==0)report.missingMainFrames++;break;}
                    report.threadFrames++;stack.Clear();
                    for(int sample=0;sample<view.sampleCount;sample++)
                    {
                        while(stack.Count>0&&sample>=stack[stack.Count-1].end)stack.RemoveAt(stack.Count-1);
                        string name=view.GetSampleName(sample);
                        if(name.StartsWith(HardPolishNativeGcCapture.Prefix,StringComparison.Ordinal))
                        {
                            ScopeResult scope=Scope(name);scope.calls++;
                            stack.Add(new ActiveScope{end=sample+view.GetSampleChildrenCountRecursive(sample)+1,result=scope});
                        }
                        if(name!="GC.Alloc")continue;
                        report.allGcEvents++;
                        long bytes=-1;
                        if(view.GetSampleMetadataCount(sample)>0)
                        {
                            bytes=view.GetSampleMetadataAsLong(sample,0);
                            if(report.metadata==null)
                            {
                                var info=view.GetMarkerMetadataInfo(view.GetSampleMarkerId(sample));
                                report.metadata=string.Join(";",info.Select(m=>m.name+":"+m.type+":"+m.unit));
                            }
                        }
                        if(bytes<=0){report.missingSizeMetadata++;bytes=0;}
                        report.allGcBytes+=bytes;
                        foreach(ActiveScope scope in stack){scope.result.inclusiveGcEvents++;scope.result.inclusiveBytes+=bytes;}
                        if(stack.Count==0)continue;
                        addresses.Clear();view.GetSampleCallstack(sample,addresses);
                        if(addresses.Count==0)report.missingCallstacks++;
                        if(report.allocations.Count>=4096){report.allocationDetailsTruncated=true;continue;}
                        var calls=new string[addresses.Count];
                        for(int i=0;i<addresses.Count;i++)
                        {
                            var method=view.ResolveMethodInfo(addresses[i]);
                            calls[i]="0x"+addresses[i].ToString("x")+" "+method.methodName+" "+method.sourceFileName+":"+method.sourceFileLine;
                        }
                        report.allocations.Add(new Allocation{frame=frame,thread=thread,sample=sample,threadName=view.threadName,
                            bytes=bytes,scopes=stack.Select(x=>x.result.marker).ToArray(),callstack=calls});
                    }
                }
            }
            report.calibrationPassed=start.calls>0&&end.calls>0&&start.inclusiveGcEvents>=2&&end.inclusiveGcEvents>=2&&
                start.inclusiveBytes>=8320&&end.inclusiveBytes>=8320&&report.missingMainFrames==0&&report.missingSizeMetadata==0;
            foreach(ScopeResult scope in report.scopes)
                scope.result=!report.calibrationPassed?"INCOMPLETE_OR_UNSUPPORTED_CAPTURE":scope.calls==0?"UNEXERCISED":
                    scope.inclusiveGcEvents==0?"ZERO_IN_CAPTURED_SYNCHRONOUS_CALLS":"ALLOCATIONS_OBSERVED";
            report.result=!report.calibrationPassed?"INCOMPLETE_OR_UNSUPPORTED_CAPTURE":report.missingCallstacks>0?
                "CALIBRATED_BYTES_SOME_CALLSTACKS_UNAVAILABLE":"CALIBRATED_NATIVE_GC_SAMPLES";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Debug.Log("Native GC analysis: "+report.result+"; "+output);
            return report;
        }
    }
}
