using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Elemental.Experimental.SonicPrototype
{
    /// <summary>
    /// An explicit Editor-only feasibility harness for the pinned official SONIC planner.
    /// It is deliberately isolated from production assemblies and never changes a scene.
    /// </summary>
    public static class SonicPlannerImportAndBenchmark
    {
        internal const string ModelFileName = "planner_sonic_6733128.onnx";
        internal const long ExpectedModelBytes = 773952989L;
        internal const string ExpectedModelSha256 =
            "39b553e197f62f077975ba38512bc04781a3fc37c2af7c6756e04629f760edea";

        internal const string ImportedModelAssetPath =
            "Assets/Experimental/SonicPrototype/Models/planner_sonic_6733128.onnx";

        private const string ImportMenu =
            "Elemental/Experimental/SONIC/1 Import And Inspect Pinned Planner";
        private const string BenchmarkMenu =
            "Elemental/Experimental/SONIC/2 Benchmark CPU Walk And Boxing";
        private const string ParityMenu =
            "Elemental/Experimental/SONIC/4 Export Unity CPU Parity Vectors";

        private static readonly InputContract[] ExpectedInputs =
        {
            new InputContract("context_mujoco_qpos", DataType.Float, 1, 4, 36),
            new InputContract("target_vel", DataType.Float, 1),
            new InputContract("mode", DataType.Int, 1),
            new InputContract("movement_direction", DataType.Float, 1, 3),
            new InputContract("facing_direction", DataType.Float, 1, 3),
            new InputContract("random_seed", DataType.Int, 1),
            new InputContract("has_specific_target", DataType.Int, 1, 1),
            new InputContract("specific_target_positions", DataType.Float, 1, 4, 3),
            new InputContract("specific_target_headings", DataType.Float, 1, 4),
            new InputContract("allowed_pred_num_tokens", DataType.Int, 1, 11),
            new InputContract("height", DataType.Float, 1),
        };

        [MenuItem(ImportMenu, priority = 2200)]
        public static void ImportAndInspectPinnedPlanner()
        {
            var report = NewImportReport();
            var capture = new LogCapture();
            var importedModelIsUsable = false;
            var destinationWasCreated = false;
            string stagingPath = null;

            try
            {
                string sourcePath = GetPinnedSourcePath();
                report.sourcePath = sourcePath;
                VerifyPinnedModel(sourcePath, report);

                EnsureAssetFolder("Assets/Experimental/SonicPrototype/Models");
                string destinationPath = GetAbsoluteAssetPath(ImportedModelAssetPath);
                if (File.Exists(destinationPath))
                {
                    long destinationBytes = new FileInfo(destinationPath).Length;
                    string destinationHash = ComputeSha256(destinationPath);
                    if (destinationBytes != ExpectedModelBytes ||
                        !string.Equals(destinationHash, ExpectedModelSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"Refusing to overwrite a different asset at {ImportedModelAssetPath}. " +
                            $"Found {destinationBytes} bytes / {destinationHash}.");
                    }
                }
                else
                {
                    string stagingDirectory = GetAbsoluteAssetPath("Library/SonicPrototypeStaging");
                    Directory.CreateDirectory(stagingDirectory);
                    stagingPath = Path.Combine(stagingDirectory, ModelFileName + ".staging");
                    if (File.Exists(stagingPath)) File.Delete(stagingPath);
                    File.Copy(sourcePath, stagingPath, false);
                    if (new FileInfo(stagingPath).Length != ExpectedModelBytes ||
                        !string.Equals(ComputeSha256(stagingPath), ExpectedModelSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The staged SONIC copy failed its identity check.");
                    File.Move(stagingPath, destinationPath);
                    stagingPath = null;
                    destinationWasCreated = true;
                }

                var importTimer = Stopwatch.StartNew();
                AssetDatabase.ImportAsset(
                    ImportedModelAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                importTimer.Stop();
                report.importMilliseconds = importTimer.Elapsed.TotalMilliseconds;

                var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(ImportedModelAssetPath);
                if (modelAsset == null)
                    throw new InvalidOperationException(
                        $"Unity did not create a {nameof(ModelAsset)} at {ImportedModelAssetPath}.");

                var loadTimer = Stopwatch.StartNew();
                Model model = ModelLoader.Load(modelAsset);
                loadTimer.Stop();
                report.modelLoadMilliseconds = loadTimer.Elapsed.TotalMilliseconds;
                PopulateModelInspection(model, report);

                report.contractIssues = ValidateContract(model).ToArray();
                if (report.contractIssues.Length != 0)
                    throw new InvalidOperationException(
                        "The imported model contract differs from the pinned SONIC contract: " +
                        string.Join(" | ", report.contractIssues));

                importedModelIsUsable = true;
                report.status = "Passed";
            }
            catch (Exception exception)
            {
                report.status = "Failed";
                report.error = exception.ToString();
            }
            finally
            {
                capture.Dispose();
                report.logs = capture.Entries.ToArray();

                if (!importedModelIsUsable && destinationWasCreated)
                {
                    report.failedAssetDeleted = AssetDatabase.DeleteAsset(ImportedModelAssetPath);
                    if (!report.failedAssetDeleted)
                    {
                        string failedPath = GetAbsoluteAssetPath(ImportedModelAssetPath);
                        if (File.Exists(failedPath))
                            File.Delete(failedPath);
                        if (File.Exists(failedPath + ".meta"))
                            File.Delete(failedPath + ".meta");
                        report.failedAssetDeleted = !File.Exists(failedPath);
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }
                }
                if (!string.IsNullOrEmpty(stagingPath) && File.Exists(stagingPath))
                    File.Delete(stagingPath);

                report.completedUtc = DateTime.UtcNow.ToString("O");
                WriteReport("UnityImportReport.json", report);
                LogResult("SONIC import", report.status, report.error);
            }
        }

        [MenuItem(BenchmarkMenu, priority = 2201)]
        public static void BenchmarkCpuWalkAndBoxing()
        {
            var report = NewBenchmarkReport();
            var capture = new LogCapture();

            try
            {
                string sourcePath = GetPinnedSourcePath();
                report.sourcePath = sourcePath;
                VerifyPinnedModel(sourcePath, report);

                var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(ImportedModelAssetPath);
                if (modelAsset == null)
                {
                    throw new InvalidOperationException(
                        $"No imported SONIC ModelAsset exists at {ImportedModelAssetPath}. " +
                        $"Run '{ImportMenu}' first.");
                }

                report.memoryBefore = CaptureMemory();
                var modelLoadTimer = Stopwatch.StartNew();
                Model model = ModelLoader.Load(modelAsset);
                modelLoadTimer.Stop();
                report.modelLoadMilliseconds = modelLoadTimer.Elapsed.TotalMilliseconds;
                report.contractIssues = ValidateContract(model).ToArray();
                if (report.contractIssues.Length != 0)
                    throw new InvalidOperationException(string.Join(" | ", report.contractIssues));

                var workerTimer = Stopwatch.StartNew();
                using (var worker = new Worker(model, BackendType.CPU))
                {
                    workerTimer.Stop();
                    report.workerCreateMilliseconds = workerTimer.Elapsed.TotalMilliseconds;
                    report.memoryAfterWorker = CaptureMemory();

                    report.cases = new[]
                    {
                        RunCase(worker, "walk", mode: 2, moving: true, sampleCount: 10),
                        RunCase(worker, "randomPunches", mode: 13, moving: false, sampleCount: 10),
                    };

                    report.memoryAfterRuns = CaptureMemory();
                }

                if (report.cases.Any(item => !item.finiteValidOutput || item.validFrames < 24 || item.validFrames > 64))
                    throw new InvalidOperationException("SONIC returned a non-finite or invalid-length output.");

                report.status = "Passed";
            }
            catch (Exception exception)
            {
                report.status = "Failed";
                report.error = exception.ToString();
            }
            finally
            {
                capture.Dispose();
                report.logs = capture.Entries.ToArray();
                report.completedUtc = DateTime.UtcNow.ToString("O");
                WriteReport("UnityCpuBenchmark.json", report);
                LogResult("SONIC CPU benchmark", report.status, report.error);
            }
        }

        [MenuItem(ParityMenu, priority = 2203)]
        public static void ExportUnityCpuParityVectors()
        {
            var report = new ParityReport
            {
                startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                packageVersion = "com.unity.ai.inference 2.6.1",
                expectedBytes = ExpectedModelBytes,
                expectedSha256 = ExpectedModelSha256,
                importedModelAssetPath = ImportedModelAssetPath,
                backend = BackendType.CPU.ToString(),
                status = "Starting",
            };
            var capture = new LogCapture();
            try
            {
                string sourcePath = GetPinnedSourcePath();
                report.sourcePath = sourcePath;
                VerifyPinnedModel(sourcePath, report);
                var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(ImportedModelAssetPath);
                if (modelAsset == null)
                    throw new InvalidOperationException($"Run '{ImportMenu}' first.");
                Model model = ModelLoader.Load(modelAsset);
                string[] issues = ValidateContract(model).ToArray();
                if (issues.Length != 0)
                    throw new InvalidOperationException(string.Join(" | ", issues));

                using (var worker = new Worker(model, BackendType.CPU))
                {
                    report.cases = new[]
                    {
                        RunParityCase(worker, "walk", CreateParityInput(mode: 2, moving: true)),
                        RunParityCase(worker, "randomPunches", CreateParityInput(mode: 13, moving: false)),
                    };
                }
                report.status = "Passed";
            }
            catch (Exception exception)
            {
                report.status = "Failed";
                report.error = exception.ToString();
            }
            finally
            {
                capture.Dispose();
                report.logs = capture.Entries.ToArray();
                report.completedUtc = DateTime.UtcNow.ToString("O");
                WriteReport("UnityParityVectors.json", report);
                LogResult("SONIC Unity parity export", report.status, report.error);
            }
        }

        [MenuItem(ImportMenu, validate = true)]
        private static bool CanImportAndInspectPinnedPlanner() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(BenchmarkMenu, validate = true)]
        private static bool CanBenchmarkCpuWalkAndBoxing() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(ParityMenu, validate = true)]
        private static bool CanExportUnityCpuParityVectors() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static ImportReport NewImportReport()
        {
            return new ImportReport
            {
                startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                packageVersion = "com.unity.ai.inference 2.6.1",
                destinationAssetPath = ImportedModelAssetPath,
                expectedBytes = ExpectedModelBytes,
                expectedSha256 = ExpectedModelSha256,
                status = "Starting",
            };
        }

        private static BenchmarkReport NewBenchmarkReport()
        {
            return new BenchmarkReport
            {
                startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                packageVersion = "com.unity.ai.inference 2.6.1",
                importedModelAssetPath = ImportedModelAssetPath,
                expectedBytes = ExpectedModelBytes,
                expectedSha256 = ExpectedModelSha256,
                backend = BackendType.CPU.ToString(),
                processorType = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                operatingSystem = SystemInfo.operatingSystem,
                status = "Starting",
            };
        }

        private static void VerifyPinnedModel(string path, IdentityReport report)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Pinned official SONIC model is missing.", path);

            var info = new FileInfo(path);
            report.actualBytes = info.Length;
            report.actualSha256 = ComputeSha256(path);
            if (report.actualBytes != ExpectedModelBytes ||
                !string.Equals(report.actualSha256, ExpectedModelSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Pinned SONIC identity mismatch. Expected {ExpectedModelBytes} bytes / " +
                    $"{ExpectedModelSha256}; got {report.actualBytes} bytes / {report.actualSha256}.");
            }
        }

        private static void PopulateModelInspection(Model model, ImportReport report)
        {
            report.producerName = model.ProducerName;
            report.layerCount = model.layers.Count;
            report.constantCount = model.constants.Count;
            report.inputs = model.inputs.Select(input => new TensorContract
            {
                name = input.name,
                dataType = input.dataType.ToString(),
                shape = input.shape.ToString(),
                staticShape = input.shape.IsStatic() ? input.shape.ToTensorShape().ToArray() : Array.Empty<int>(),
            }).ToArray();
            report.outputs = model.outputs.Select(output => output.name).ToArray();
            report.operators = model.layers
                .GroupBy(layer => layer.opName)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new NamedCount { name = group.Key, count = group.Count() })
                .ToArray();
        }

        private static IEnumerable<string> ValidateContract(Model model)
        {
            if (model.inputs.Count != ExpectedInputs.Length)
                yield return $"Expected {ExpectedInputs.Length} inputs, got {model.inputs.Count}.";

            foreach (InputContract expected in ExpectedInputs)
            {
                int index = model.inputs.FindIndex(input => input.name == expected.Name);
                if (index < 0)
                {
                    yield return $"Missing input '{expected.Name}'.";
                    continue;
                }

                Model.Input actual = model.inputs[index];
                if (actual.dataType != expected.DataType)
                    yield return $"Input '{expected.Name}' type is {actual.dataType}; expected {expected.DataType}.";
                if (!actual.shape.IsStatic())
                {
                    yield return $"Input '{expected.Name}' shape is dynamic: {actual.shape}.";
                    continue;
                }

                int[] actualShape = actual.shape.ToTensorShape().ToArray();
                if (!actualShape.SequenceEqual(expected.Shape))
                    yield return $"Input '{expected.Name}' shape is [{string.Join(",", actualShape)}]; expected [{string.Join(",", expected.Shape)}].";
            }

            string[] expectedOutputs = { "mujoco_qpos", "num_pred_frames" };
            string[] actualOutputs = model.outputs.Select(output => output.name).ToArray();
            if (!actualOutputs.SequenceEqual(expectedOutputs))
                yield return $"Outputs are [{string.Join(",", actualOutputs)}]; expected [{string.Join(",", expectedOutputs)}].";
        }

        private static BenchmarkCase RunCase(
            Worker worker,
            string name,
            int mode,
            bool moving,
            int sampleCount)
        {
            using (var inputs = new SonicInputs(CreateParityInput(mode, moving)))
            {
                inputs.Bind(worker);
                double warmupMilliseconds = ScheduleAndRead(worker, out _, out _, out _);
                var samples = new double[sampleCount];
                float[] finalQpos = null;
                int[] finalCount = null;
                TensorShape finalQposShape = default;
                TensorShape finalCountShape = default;
                long managedBefore = GC.GetTotalMemory(false);

                for (int index = 0; index < sampleCount; index++)
                {
                    samples[index] = ScheduleAndRead(
                        worker,
                        out finalQpos,
                        out finalCount,
                        out OutputShapes shapes);
                    finalQposShape = shapes.Qpos;
                    finalCountShape = shapes.Count;
                }

                long managedAfter = GC.GetTotalMemory(false);
                int validFrames = finalCount != null && finalCount.Length != 0 ? finalCount[0] : -1;
                int finiteFrames = Math.Max(0, Math.Min(validFrames, 64));
                bool finite = finalQpos != null && finalQpos.Length == 64 * 36;
                for (int index = 0; finite && index < finiteFrames * 36; index++)
                    finite &= !float.IsNaN(finalQpos[index]) && !float.IsInfinity(finalQpos[index]);

                double[] sorted = samples.OrderBy(value => value).ToArray();
                return new BenchmarkCase
                {
                    name = name,
                    mode = mode,
                    warmupMilliseconds = warmupMilliseconds,
                    samplesMilliseconds = samples,
                    p50Milliseconds = Percentile(sorted, 0.50),
                    p95Milliseconds = Percentile(sorted, 0.95),
                    meanMilliseconds = samples.Average(),
                    minimumMilliseconds = samples.Min(),
                    maximumMilliseconds = samples.Max(),
                    managedMemoryDeltaBytes = managedAfter - managedBefore,
                    validFrames = validFrames,
                    qposShape = finalQposShape.ToArray(),
                    numPredFramesShape = finalCountShape.ToArray(),
                    finiteValidOutput = finite,
                    rootStart = finalQpos?.Take(7).ToArray() ?? Array.Empty<float>(),
                };
            }
        }

        private static ParityCase RunParityCase(Worker worker, string name, ParityInput input)
        {
            using (var inputs = new SonicInputs(input))
            {
                inputs.Bind(worker);
                double milliseconds = ScheduleAndRead(worker, out float[] qpos, out int[] counts, out OutputShapes shapes);
                int count = counts != null && counts.Length > 0 ? counts[0] : -1;
                if (qpos == null || qpos.Length != 64 * 36 || count < 24 || count > 64)
                    throw new InvalidDataException($"Parity case '{name}' returned an invalid output contract.");
                int validValueCount = count * 36;
                for (int index = 0; index < validValueCount; index++)
                {
                    if (float.IsNaN(qpos[index]) || float.IsInfinity(qpos[index]))
                        throw new InvalidDataException($"Parity case '{name}' returned a non-finite value at {index}.");
                }
                var validQpos = new float[validValueCount];
                Array.Copy(qpos, validQpos, validValueCount);
                return new ParityCase
                {
                    name = name,
                    input = input,
                    inferenceMilliseconds = milliseconds,
                    validFrames = count,
                    qposShape = shapes.Qpos.ToArray(),
                    numPredFramesShape = shapes.Count.ToArray(),
                    validQpos = validQpos,
                };
            }
        }

        private static ParityInput CreateParityInput(int mode, bool moving)
        {
            var context = new float[4 * 36];
            for (int frame = 0; frame < 4; frame++)
            {
                context[frame * 36 + 2] = .78f;
                context[frame * 36 + 3] = 1f;
            }
            var allowed = new int[11];
            allowed[0] = 1;
            return new ParityInput
            {
                contextMujocoQpos = context,
                targetVelocity = -1f,
                mode = mode,
                movementDirection = moving ? new[] { 1f, 0f, 0f } : new[] { 0f, 0f, 0f },
                facingDirection = new[] { 1f, 0f, 0f },
                randomSeed = 20260905,
                hasSpecificTarget = 0,
                specificTargetPositions = new float[12],
                specificTargetHeadings = new float[4],
                allowedPredictionTokenCounts = allowed,
                height = -1f,
            };
        }

        private static double ScheduleAndRead(
            Worker worker,
            out float[] qpos,
            out int[] frameCount,
            out OutputShapes shapes)
        {
            var timer = Stopwatch.StartNew();
            worker.Schedule();
            var qposTensor = worker.PeekOutput("mujoco_qpos") as Tensor<float>;
            var countTensor = worker.PeekOutput("num_pred_frames") as Tensor<int>;
            if (qposTensor == null || countTensor == null)
                throw new InvalidOperationException("SONIC output tensor types do not match float qpos + integer count.");

            qpos = qposTensor.DownloadToArray();
            frameCount = countTensor.DownloadToArray();
            timer.Stop();
            shapes = new OutputShapes(qposTensor.shape, countTensor.shape);
            return timer.Elapsed.TotalMilliseconds;
        }

        private static double Percentile(double[] sorted, double percentile)
        {
            if (sorted == null || sorted.Length == 0)
                return double.NaN;
            double position = (sorted.Length - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper)
                return sorted[lower];
            return sorted[lower] * (upper - position) + sorted[upper] * (position - lower);
        }

        private static MemorySnapshot CaptureMemory()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return new MemorySnapshot
                {
                    managedBytes = GC.GetTotalMemory(false),
                    unityAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                    workingSetBytes = process.WorkingSet64,
                    peakWorkingSetBytes = process.PeakWorkingSet64,
                };
            }
        }

        private static string GetPinnedSourcePath()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Tools",
                "SonicPrototype",
                "Models",
                ModelFileName));
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static string ComputeSha256(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8 * 1024 * 1024))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string[] segments = assetFolder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private static void WriteReport(string fileName, object report)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BuildReports", "SonicPrototype"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), JsonUtility.ToJson(report, true));
        }

        private static void LogResult(string action, string status, string error)
        {
            string reportDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BuildReports", "SonicPrototype"));
            if (string.Equals(status, "Passed", StringComparison.Ordinal))
                UnityEngine.Debug.Log($"{action} passed. Report: {reportDirectory}");
            else
                UnityEngine.Debug.LogError($"{action} failed. Report: {reportDirectory}\n{error}");
        }

        private sealed class SonicInputs : IDisposable
        {
            private readonly Tensor<float> _context;
            private readonly Tensor<float> _targetVelocity;
            private readonly Tensor<int> _mode;
            private readonly Tensor<float> _movementDirection;
            private readonly Tensor<float> _facingDirection;
            private readonly Tensor<int> _randomSeed;
            private readonly Tensor<int> _hasSpecificTarget;
            private readonly Tensor<float> _specificTargetPositions;
            private readonly Tensor<float> _specificTargetHeadings;
            private readonly Tensor<int> _allowedPredictionTokenCounts;
            private readonly Tensor<float> _height;

            public SonicInputs(ParityInput input)
            {
                _context = new Tensor<float>(new TensorShape(1, 4, 36), input.contextMujocoQpos);
                _targetVelocity = new Tensor<float>(new TensorShape(1), new[] { input.targetVelocity });
                _mode = new Tensor<int>(new TensorShape(1), new[] { input.mode });
                _movementDirection = new Tensor<float>(
                    new TensorShape(1, 3),
                    input.movementDirection);
                _facingDirection = new Tensor<float>(new TensorShape(1, 3), input.facingDirection);
                _randomSeed = new Tensor<int>(new TensorShape(1), new[] { input.randomSeed });
                _hasSpecificTarget = new Tensor<int>(new TensorShape(1, 1), new[] { input.hasSpecificTarget });
                _specificTargetPositions = new Tensor<float>(new TensorShape(1, 4, 3), input.specificTargetPositions);
                _specificTargetHeadings = new Tensor<float>(new TensorShape(1, 4), input.specificTargetHeadings);
                _allowedPredictionTokenCounts = new Tensor<int>(
                    new TensorShape(1, 11), input.allowedPredictionTokenCounts);
                _height = new Tensor<float>(new TensorShape(1), new[] { input.height });
            }

            public void Bind(Worker worker)
            {
                worker.SetInput("context_mujoco_qpos", _context);
                worker.SetInput("target_vel", _targetVelocity);
                worker.SetInput("mode", _mode);
                worker.SetInput("movement_direction", _movementDirection);
                worker.SetInput("facing_direction", _facingDirection);
                worker.SetInput("random_seed", _randomSeed);
                worker.SetInput("has_specific_target", _hasSpecificTarget);
                worker.SetInput("specific_target_positions", _specificTargetPositions);
                worker.SetInput("specific_target_headings", _specificTargetHeadings);
                worker.SetInput("allowed_pred_num_tokens", _allowedPredictionTokenCounts);
                worker.SetInput("height", _height);
            }

            public void Dispose()
            {
                _context.Dispose();
                _targetVelocity.Dispose();
                _mode.Dispose();
                _movementDirection.Dispose();
                _facingDirection.Dispose();
                _randomSeed.Dispose();
                _hasSpecificTarget.Dispose();
                _specificTargetPositions.Dispose();
                _specificTargetHeadings.Dispose();
                _allowedPredictionTokenCounts.Dispose();
                _height.Dispose();
            }
        }

        private sealed class LogCapture : IDisposable
        {
            public readonly List<LogEntry> Entries = new List<LogEntry>();

            public LogCapture()
            {
                Application.logMessageReceived += OnLog;
            }

            public void Dispose()
            {
                Application.logMessageReceived -= OnLog;
            }

            private void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    Entries.Add(new LogEntry
                    {
                        type = type.ToString(),
                        message = condition,
                        stackTrace = stackTrace,
                    });
                }
            }
        }

        private readonly struct InputContract
        {
            public readonly string Name;
            public readonly DataType DataType;
            public readonly int[] Shape;

            public InputContract(string name, DataType dataType, params int[] shape)
            {
                Name = name;
                DataType = dataType;
                Shape = shape;
            }
        }

        private readonly struct OutputShapes
        {
            public readonly TensorShape Qpos;
            public readonly TensorShape Count;

            public OutputShapes(TensorShape qpos, TensorShape count)
            {
                Qpos = qpos;
                Count = count;
            }
        }

        [Serializable]
        private abstract class IdentityReport
        {
            public string startedUtc;
            public string completedUtc;
            public string unityVersion;
            public string packageVersion;
            public string sourcePath;
            public long expectedBytes;
            public long actualBytes;
            public string expectedSha256;
            public string actualSha256;
            public string status;
            public string error;
            public LogEntry[] logs;
        }

        [Serializable]
        private sealed class ImportReport : IdentityReport
        {
            public string destinationAssetPath;
            public double importMilliseconds;
            public double modelLoadMilliseconds;
            public string producerName;
            public int layerCount;
            public int constantCount;
            public TensorContract[] inputs;
            public string[] outputs;
            public NamedCount[] operators;
            public string[] contractIssues;
            public bool failedAssetDeleted;
        }

        [Serializable]
        private sealed class BenchmarkReport : IdentityReport
        {
            public string importedModelAssetPath;
            public string backend;
            public string processorType;
            public int processorCount;
            public string operatingSystem;
            public double modelLoadMilliseconds;
            public double workerCreateMilliseconds;
            public MemorySnapshot memoryBefore;
            public MemorySnapshot memoryAfterWorker;
            public MemorySnapshot memoryAfterRuns;
            public string[] contractIssues;
            public BenchmarkCase[] cases;
        }

        [Serializable]
        private sealed class ParityReport : IdentityReport
        {
            public string importedModelAssetPath;
            public string backend;
            public ParityCase[] cases;
        }

        [Serializable]
        private sealed class ParityCase
        {
            public string name;
            public ParityInput input;
            public double inferenceMilliseconds;
            public int validFrames;
            public int[] qposShape;
            public int[] numPredFramesShape;
            public float[] validQpos;
        }

        [Serializable]
        private sealed class ParityInput
        {
            public float[] contextMujocoQpos;
            public float targetVelocity;
            public int mode;
            public float[] movementDirection;
            public float[] facingDirection;
            public int randomSeed;
            public int hasSpecificTarget;
            public float[] specificTargetPositions;
            public float[] specificTargetHeadings;
            public int[] allowedPredictionTokenCounts;
            public float height;
        }

        [Serializable]
        private sealed class TensorContract
        {
            public string name;
            public string dataType;
            public string shape;
            public int[] staticShape;
        }

        [Serializable]
        private sealed class NamedCount
        {
            public string name;
            public int count;
        }

        [Serializable]
        private sealed class LogEntry
        {
            public string type;
            public string message;
            public string stackTrace;
        }

        [Serializable]
        private sealed class MemorySnapshot
        {
            public long managedBytes;
            public long unityAllocatedBytes;
            public long workingSetBytes;
            public long peakWorkingSetBytes;
        }

        [Serializable]
        private sealed class BenchmarkCase
        {
            public string name;
            public int mode;
            public double warmupMilliseconds;
            public double[] samplesMilliseconds;
            public double p50Milliseconds;
            public double p95Milliseconds;
            public double meanMilliseconds;
            public double minimumMilliseconds;
            public double maximumMilliseconds;
            public long managedMemoryDeltaBytes;
            public int validFrames;
            public int[] qposShape;
            public int[] numPredFramesShape;
            public bool finiteValidOutput;
            public float[] rootStart;
        }
    }
}
