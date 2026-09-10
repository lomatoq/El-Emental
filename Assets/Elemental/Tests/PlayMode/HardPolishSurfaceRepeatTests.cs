using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest] public IEnumerator ActualSurfaceSixRatesRepeatThreeTimesWithIndependentEvidence()
        {
            const string folder="BuildReports/HardPolish/G03/Repeats";Directory.CreateDirectory(folder);
            var failures=new List<string>();
            for(int repeat=1;repeat<=3;repeat++)
            {
                foreach(var actor in _actors)actor.Input.Move=float2.zero;
                yield return new WaitForSeconds(.5f);
                _activeSurfaceReport=null;
                var stack=new Stack<IEnumerator>();
                stack.Push(FinalHumanoidFeetTraverseRealPitHumpAndSlopeAtControlledThirtySixtyOneTwentySteps());
                Exception failure=null;
                try
                {
                    // Drive yielded child enumerators explicitly. Unity's outer runner
                    // may abandon their finally on assertion; unwind every owned track
                    // and restored motor pose before starting the next independent attempt.
                    while(stack.Count>0)
                    {
                        var current=stack.Peek();bool moved=false;object yielded=null;
                        try{moved=current.MoveNext();if(moved)yielded=current.Current;}
                        catch(Exception error){failure=error;break;}
                        if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                        if(yielded is IEnumerator child)stack.Push(child);
                        else yield return yielded;
                    }
                }
                finally
                {
                    while(stack.Count>0)
                    {
                        try{(stack.Pop() as IDisposable)?.Dispose();}
                        catch(Exception error){if(failure==null)failure=error;}
                    }
                    string prefix=folder+"/repeat-"+repeat.ToString("D2");
                    if(_activeSurfaceReport!=null)File.WriteAllText(prefix+".json",JsonUtility.ToJson(_activeSurfaceReport,true));
                    File.WriteAllText(prefix+"-status.txt",failure==null?"All six existing actor/rate traversals completed.":failure.ToString());
                    PersistSurfaceFailureAndRestoreClock();
                }
                if(failure!=null)failures.Add("Repeat "+repeat+": "+failure.Message);
                // Track destruction is deferred on the successful child path; wait
                // one frame before another fixture can query the restored terrain.
                yield return null;
            }
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }
    }
}
