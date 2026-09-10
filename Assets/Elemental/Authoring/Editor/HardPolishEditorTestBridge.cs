using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    /// <summary>Explicit current-editor test invocation. Initialization only restores callbacks; never starts a run.</summary>
    [InitializeOnLoad]
    public static class HardPolishEditorTestBridge
    {
        private const string Key="Elemental.HardPolish.EditorTestBridge.State";
        [Serializable] public sealed class SceneRecord { public string path;public bool loaded,active; }
        [Serializable] public sealed class Status
        {
            public string status,mode,runId,startedUtc,updatedUtc,outputDirectory,xmlPath,currentTest,message;
            public string[] selectors;public SceneRecord[] scenes;
            public bool running,restorePending,xmlWritten;
            public int passed,failed,skipped,inconclusive;
            public double durationSeconds;
        }
        private static Status state;
        private static Callbacks callbacks;
        private static TestRunnerApi api;
        static HardPolishEditorTestBridge()
        {
            string saved=SessionState.GetString(Key,"");
            if(string.IsNullOrEmpty(saved))return;
            state=JsonUtility.FromJson<Status>(saved);
            if(state.running)Register();
            if(state.restorePending)ScheduleRestore();
        }
        public static string ReadStatus() => state==null ? "{\"status\":\"IDLE\"}" : JsonUtility.ToJson(state,true);
        public static void RecoverStoppedRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AnyRunnerActive())
                throw new InvalidOperationException("The test runner must finish stopping before recovery.");
            if (state == null || !state.running) return;
            FinishFailure("The runner stopped without a completion callback; this run is incomplete and is not passing evidence.");
        }
        // Semicolon-delimited exact fixture/method prefixes, not unrestricted regex.
        public static string Run(string mode,string selectors,string outputDirectory)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait until the editor is idle in Edit Mode.");
            if(state!=null&&(state.running||state.restorePending)||AnyRunnerActive())
                throw new InvalidOperationException("A test run or scene restoration is already active.");
            if(!Enum.TryParse(mode,false,out TestMode testMode)||(testMode!=TestMode.EditMode&&testMode!=TestMode.PlayMode))
                throw new ArgumentException("Mode must be EditMode or PlayMode.");
            string[] names=(selectors??"").Split(';').Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct().ToArray();
            if(names.Length==0)throw new ArgumentException("At least one exact fixture or test prefix is required.");
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                Scene scene=SceneManager.GetSceneAt(i);
                if(scene.isDirty||string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException("Save the existing editor scene before testing; the bridge never saves or discards user changes.");
            }
            if(string.IsNullOrWhiteSpace(outputDirectory))throw new ArgumentException("An explicit output directory is required.");
            string folder=Path.GetFullPath(outputDirectory);
            if(File.Exists(Path.Combine(folder,"status.json"))||File.Exists(Path.Combine(folder,"results.xml")))
                throw new IOException("Choose a fresh report directory; existing test evidence is preserved.");
            Directory.CreateDirectory(folder);
            state=new Status {status="QUEUED",mode=mode,startedUtc=DateTime.UtcNow.ToString("O"),running=true,
                selectors=names,outputDirectory=folder,xmlPath=Path.Combine(folder,"results.xml"),
                scenes=EditorSceneManager.GetSceneManagerSetup().Select(s=>new SceneRecord {path=s.path,loaded=s.isLoaded,active=s.isActive}).ToArray()};
            Persist();Register();
            try
            {
                // These production fixtures load the saved scene additively themselves.
                if(testMode==TestMode.PlayMode)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var filter=new Filter {testMode=testMode,groupNames=names.Select(n=>"^"+Regex.Escape(n)+"(?:$|[.(])").ToArray()};
                api=ScriptableObject.CreateInstance<TestRunnerApi>();
                string id=api.Execute(new ExecutionSettings(filter){runSynchronously=false});
                state.runId=id;Persist();return id;
            }
            catch(Exception error)
            {
                FinishFailure(error.ToString());throw;
            }
        }
        private static bool AnyRunnerActive()
        {
            // The pinned installed TestRunner API exposes this guard internally only.
            MethodInfo method=typeof(TestRunnerApi).GetMethod("IsRunActive",BindingFlags.NonPublic|BindingFlags.Static);
            if(method==null)throw new NotSupportedException("Installed TestRunnerApi lacks the inspected IsRunActive guard; update the bridge before running.");
            return (bool)method.Invoke(null,null);
        }
        private static void Register()
        {
            if(callbacks!=null)TestRunnerApi.UnregisterTestCallback(callbacks);
            callbacks=new Callbacks();TestRunnerApi.RegisterTestCallback(callbacks,100);
        }
        private static void Persist()
        {
            state.updatedUtc=DateTime.UtcNow.ToString("O");
            string json=JsonUtility.ToJson(state,true);SessionState.SetString(Key,json);
            File.WriteAllText(Path.Combine(state.outputDirectory,"status.json"),json);
        }
        private static void FinishFailure(string message)
        {
            state.running=false;state.status="RUN_ERROR";state.message=message;
            state.restorePending=state.mode=="PlayMode";Persist();
            if(state.restorePending)ScheduleRestore();
        }
        public static void RetrySceneRestore()
        {
            if(state==null||!state.restorePending||state.running)throw new InvalidOperationException("No completed run is waiting for scene restoration.");
            ScheduleRestore();
        }
        private static void ScheduleRestore()
        {
            EditorApplication.update-=RestoreAfterRunner;
            EditorApplication.update+=RestoreAfterRunner;
        }
        private static void RestoreAfterRunner()
        {
            if(state==null||!state.restorePending){EditorApplication.update-=RestoreAfterRunner;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||AnyRunnerActive())return;
            EditorApplication.update-=RestoreAfterRunner;
            try
            {
                for(int i=0;i<SceneManager.sceneCount;i++)
                    if(SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("A scene became dirty after the run; review it before calling RetrySceneRestore. No changes were discarded.");
                // Standard editor scene setup restores every originally loaded scene,
                // its active selection, and unloaded scene entries after the runner exits.
                EditorSceneManager.RestoreSceneManagerSetup(state.scenes.Select(s=>new SceneSetup {path=s.path,isLoaded=s.loaded,isActive=s.active}).ToArray());
                state.restorePending=false;
                if(state.status=="RESULTS_READY")state.status="COMPLETE";
                Persist();
            }
            catch(Exception error)
            {
                state.status="SCENE_RESTORE_ERROR";state.message=error.ToString();Persist();
            }
        }
        private sealed class Callbacks:IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor tests)
            {
                if(state==null||!state.running)return;state.status="RUNNING";Persist();
            }
            public void TestStarted(ITestAdaptor test)
            {
                if(state==null||!state.running)return;state.currentTest=test.FullName;Persist();
            }
            public void TestFinished(ITestResultAdaptor result)
            {
                if(state==null||!state.running||result.HasChildren)return;
                if(result.ResultState.StartsWith("Failed",StringComparison.Ordinal))
                    File.AppendAllText(Path.Combine(state.outputDirectory,"failures.txt"),result.FullName+"\n"+result.Message+"\n"+result.StackTrace+"\n\n");
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                if(state==null||!state.running)return;
                try
                {
                    TestRunnerApi.SaveResultToFile(result,state.xmlPath);
                    state.xmlWritten=File.Exists(state.xmlPath);
                    state.passed=result.PassCount;state.failed=result.FailCount;state.skipped=result.SkipCount;
                    state.inconclusive=result.InconclusiveCount;state.durationSeconds=result.Duration;
                    state.message=result.Message;state.running=false;state.currentTest="";
                    state.restorePending=state.mode=="PlayMode";
                    state.status=state.restorePending?"RESULTS_READY":"COMPLETE";
                    if(result.PassCount+result.FailCount+result.SkipCount+result.InconclusiveCount==0)
                    {state.status="NO_TESTS_SELECTED";state.message="The selectors matched no executable tests; this is not passing evidence.";}
                    Persist();if(state.restorePending)ScheduleRestore();
                }
                catch(Exception error){FinishFailure(error.ToString());}
            }
            public void OnError(string message){if(state!=null&&state.running)FinishFailure(message);}
        }
    }
}
