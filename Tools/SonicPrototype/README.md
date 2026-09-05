## Accepted bounded production-actor preview — 2026-09-05 15:23 UTC

`BuildReports/SonicPrototype/ProductionActorPreview/20260905-152326-358/PreviewReport.json`
passes the real Humanoid walk/boxing preview. Root visually inspected `walk.png`
and `boxing-dynamic.png`. Boxing records 252 distinct rendered frames over 2.554 s,
four accepted rolling plans, bilateral chest-relative hand ranges 0.302/0.282 m,
head height >=1.379 m, zero root position/rotation drift and retained final foot
ownership. Camera, UI, EAMM bridge and rival state restore on completion.

The preview geometry fit now uses `BakeMesh(mesh, true)` before TransformPoint:
on this scaled Linebreaker renderer, the false overload double-counted scale.
This corrects evidence framing; it does not alter production camera or animation.
The gameplay camera has its own permanent lens/height fix and separate QA.

This completes the isolated CPU prototype acceptance, not a production-wide
replacement. Saved gameplay scenes still contain no SONIC component or weight
reference. Earlier pending visual notes below are historical and superseded by
this report; the CPU latency measurements remain the stated prototype limits.
# SONIC planner isolated feasibility prototype

Current status, September 5: the pinned model runs on Unity Inference Engine CPU.
Ten-sample walk p50/p95 is **79.41/82.34 ms**, boxing **75.88/79.29 ms**;
`BuildReports/SonicPrototype/UnityCpuBenchmark.json` records the full run. The
Unity-versus-ORT comparison checks all 864 valid coordinates for each mode, with
maximum absolute errors **2.18e-5 / 2.20e-5**. Skeleton, retarget and rolling
timeline math pass **11/11 EditMode at 13:51 UTC**. These measurements used the
shortest prediction horizon; they are not a benchmark of the later rolling mode.

Authoritative code is now `Assets/Experimental/SonicPrototype`. All `Tools/`
patch subfolders are historical integration snapshots and must not be copied
over the current Assets tree. Both isolated asmdefs remain `autoReferenced:false`;
no saved gameplay scene or production assembly depends on the prototype.

The Humanoid hips remain owned by the existing body/foot solver, fixing the
previous collapsed torso. Rolling replanning now retains moving old/new
trajectories, uses the reference future look-ahead context, consumes the prefix
elapsed during inference and allows the complete prediction horizon range.
The real multi-frame boxing quality gate remains open until a fresh preview
passes; a still image and finite inference do not establish motion quality.

## Pinned artifact

The local model is the official NVIDIA `nvidia/GEAR-SONIC` SONIC V2 planner at
repository revision `6733128a3d8a523b1418b06bca3cdf61c8b0987f`.

| Field | Value |
|---|---|
| File | `Models/planner_sonic_6733128.onnx` |
| Bytes | `773,952,989` |
| SHA-256 | `39b553e197f62f077975ba38512bc04781a3fc37c2af7c6756e04629f760edea` |
| Xet hash | `c0a1bd71c613c4f9f54dd58b476cf7409fb44656c42234d05df0dc34d5c73fcd` |
| ONNX IR / opset | IR 8 / `ai.onnx` 17 |
| Producer | PyTorch 2.7.0 |
| License snapshot | `Licenses/NVIDIA_GEAR_SONIC_LICENSE_6733128.txt` |
| License SHA-256 | `24ab66be50d1aca4fc5e029ef76ce4ceaac6557ea21665caf4b140695a76ffee` |

The 774 MB model is intentionally ignored by `Models/.gitignore`. Download it
from the [pinned official file](https://huggingface.co/nvidia/GEAR-SONIC/resolve/6733128a3d8a523b1418b06bca3cdf61c8b0987f/planner_sonic.onnx)
and verify the size and SHA-256 before any import. `MODEL_PROVENANCE.json` is the
machine-readable identity record.

NVIDIA licenses repository source under Apache 2.0 and the model weights under
the NVIDIA Open Model License. Redistribution of the model must include the
license snapshot and this exact notice:

> Licensed by NVIDIA Corporation under the NVIDIA Open Model License.

## Evidence already produced

`inspect_onnx.py` loaded the exact file with ONNX 1.17 and wrote
`Reports/OnnxGraphInspection.json`:

- 7,932 nodes, 418 initializers and 64 distinct `ai.onnx` operators;
- fixed 11-input batch-one contract;
- `mujoco_qpos` output `[1,64,36]` float;
- `num_pred_frames` output **`INT32 [1]`**.

The final item differs from the official contract page, which currently calls
the count a scalar `INT64`. The pinned graph is authoritative for this spike.
Unity Inference Engine maps ONNX integer tensors to `Tensor<int>`.

Every operator name in this graph exists in the Inference Engine 2.6.1 ONNX
converter map. The graph also uses the converter's supported variants: last-axis
LayerNormalization, nearest/floor/asymmetric Resize, ordinary Scatter reductions,
and `select_last_index=0`. This source check does not establish successful Unity
conversion or numerical agreement; the import menu records the actual result and
all importer warnings/errors.

The Unity 6.0.5 / Inference Engine 2.6.1 import completed on 2026-09-05 in
13,885.445 ms and `ModelLoader.Load` completed in 297.490 ms. The converted model
has the expected 11 inputs and two outputs. Its 48 importer warnings are
value-equivalent defaults: all 44 LayerNormalization nodes specify `axis=-1`,
which is Unity's reported fallback, and all four nearest-neighbour Resize nodes
specify `cubic_coeff_a=-0.75`, also Unity's fallback and unused by nearest mode.
This proves structural conversion only. A finite Unity execution and comparison
against a reference implementation remain separate gates.

`benchmark_ort_cpu.py` ran the exact artifact with ONNX Runtime 1.20.1 on this
Windows 11 machine (`AMD64 Family 25 Model 80`, 16 logical CPUs), using only the
CPUExecutionProvider, a plausible neutral Z-up G1 history and the shortest valid
24-frame output mask. Report: `Reports/OrtCpuBenchmark.json`.

| Measurement | Walk mode 2 | Random-punch mode 13 |
|---|---:|---:|
| Warm-up | 61.486 ms | 53.915 ms |
| p50, 10 samples | 52.867 ms | 52.896 ms |
| p95, 10 samples | 55.763 ms | 56.151 ms |
| Valid output | 24 finite frames | 24 finite frames |

Session creation was 1,889.561 ms. Process RSS rose from 48,771,072 bytes before
session creation to 1,544,839,168 bytes after it and peaked at 1,546,682,368 bytes.
These numbers include blocking output materialization. They establish local CPU
feasibility only; they are separate from Unity Editor, Player, gameplay and
retarget-quality measurements. A 10 Hz planner cadence leaves less than half of
the 100 ms interval after this p95 and carries a material memory cost.

## Staged Unity harness

The harness is already integrated under `Assets/Experimental/SonicPrototype`.
Do not recopy the original staging snapshot. Both isolated asmdefs use
`autoReferenced: false`; no production assembly references them.

Run menus 1–4 in order while the Editor is outside Play Mode. Run menu 5 only
after entering the production scene in Play Mode:

1. `Elemental/Experimental/SONIC/1 Import And Inspect Pinned Planner`
2. `Elemental/Experimental/SONIC/2 Benchmark CPU Walk And Boxing`
3. `Elemental/Experimental/SONIC/3 Bake Retarget Profile From Selected Humanoid`
4. `Elemental/Experimental/SONIC/4 Export Unity CPU Parity Vectors`
5. `Elemental/Experimental/SONIC/5 Preview SONIC On Production Actor` (Play Mode only)

The first menu verifies SHA-256 and byte count before copying the model, performs
a synchronous import, loads the converted model, validates all 11 inputs, lists
converted operators and captures importer warnings/errors. A new failed imported
copy is removed from `Assets/Experimental` after its diagnostic report is written.
The copy is hash-verified in `Library` before a same-volume move, and the menu
refuses to overwrite a different existing asset.
Output: `BuildReports/SonicPrototype/UnityImportReport.json`.

The second menu uses `BackendType.CPU`, measures model and worker creation,
working set and Unity/managed memory, then runs one warm-up and ten blocking
schedule/readback samples for walk and random punches. It validates finite
`[1,64,36]` poses and a 24–64-frame integer count. Output:
`BuildReports/SonicPrototype/UnityCpuBenchmark.json`.

The actual Unity CPU run passed on 2026-09-05. Walk mode produced 24 finite
frames at 79.413 ms p50 / 82.343 ms p95; random punches produced 24 finite frames
at 75.879 ms p50 / 79.289 ms p95. Model load took 178.412 ms and worker creation
34.862 ms in the already-running Editor. These values are slower than the local
ONNX Runtime baseline and do not establish frame-budget suitability.

Menu 4 writes every valid qpos value and the exact inputs to
`BuildReports/SonicPrototype/UnityParityVectors.json`. Then run:

```powershell
python Tools/SonicPrototype/compare_unity_ort.py
```

This writes `BuildReports/SonicPrototype/UnityVsOrtParity.json`, including frame
count agreement, max/mean/p99 absolute error, RMSE, relative error and per-frame
drift. Its default comparison is `atol=1e-4`, `rtol=1e-3`; the report keeps these
tolerances explicit. Finite Unity output is not numerical parity until this step
passes.

The full comparison subsequently passed for both cases: valid-frame counts match
and the largest qpos absolute difference is approximately `2.2e-5`, below the
recorded `atol=1e-4`, `rtol=1e-3` gate. This establishes CPU numerical agreement
for these two fixed requests; it does not establish retarget quality.

The Unity run is accepted as a feasibility result when:

- import and `ModelLoader.Load` finish without error or semantic-conversion warning;
- the imported input names, integer/float types and fixed shapes match the report;
- both CPU cases return finite values, correct output shapes and 24–64 valid frames;
- p50, p95, startup time and peak working set are present in the report;
- removing `Assets/Experimental/SonicPrototype` returns the project to its prior state.

Numerical equivalence still requires official reference tensors. The current
official model repository did not contain planner reference inputs/outputs at the
pinned revision, so finite output is recorded separately from equivalence.

## Isolated G1 to Humanoid preview

The staged runtime assembly contains `SonicPlannerPreviewAdapter`,
`SonicG1Skeleton` and `SonicHumanoidRetargetProfile`. The adapter is opt-in and
does nothing until `takeBasePoseOwnership` is enabled with both a model and a
profile assigned. It reconstructs the pinned 29-DoF G1 skeleton, samples the
30 Hz output, crossfades replans and writes calibrated Humanoid local rotations
in `OnAnimatorIK`. The Animator controller must invoke IK callbacks on layer 0.

To prepare a profile, select the Humanoid and run menu 3. The baker reads local
T-pose rotations from `Avatar.humanDescription.skeleton`; it does not capture the
currently animated Transform pose. The generated profile records the exact source joint
and source-parent mapping, the avatar identity, each target rest local rotation,
and independent locomotion/boxing weights. The baker initializes `deltaBasis`
from the mapped G1 parent-rest frame and each target parent's imported T-pose
world rotation, translating the actual source parent axes into the Avatar's local
hierarchy. It remains explicitly editable per bone for visual calibration.

The preview leaves gameplay root motion with `PlanetMotor` and runs at execution
order 500. It clears or stales generated trajectories during authored actions,
mantle, ragdoll, recovery, pause, disable and mode changes.
`EarthFootContactController` retains its later order-1000 feet/knees/pelvis pass.
While the adapter owns the base pose it disables `EAMMBasePoseBridge`, and it
restores only a bridge that it disabled itself.

This adapter is a runnable seam, not a completed character prototype. It still
requires the parity comparison, a neutral/walk/boxing visual calibration capture,
an IK-order check on the actual controller, and lifecycle tests in Play Mode.
The measured ~1.55 GB ONNX Runtime process footprint also means that a second
worker for a bot must not be assumed viable until Unity memory is measured.

Menu 5 provides a bounded reviewer path on the active production player. It is
enabled only in Play Mode, requires the imported model, locates the active
`MagicInputController` Humanoid, creates a `HideFlags.DontSave` profile from the
Avatar T-pose and attaches a `HideFlags.DontSave` adapter to that runtime instance.
It generates an in-place walk, switches to random punches after a fresh sequence,
waits through the eight-frame blend, captures `walk.png` and `boxing.png`, stops
ownership, verifies the prior EAMM enabled state and destroys both temporary
objects. The report is written under
`BuildReports/SonicPrototype/ProductionActorPreview/<timestamp>/PreviewReport.json`.
It never saves or edits a scene or prefab.

The menu temporarily hides UI, applies the established full-body production
camera pitch/height and limits review distance to 4.2 m, then restores all values.
It reports and requires actual Humanoid-retarget application count, a recent IK
frame, head/foot viewport positions, at least 0.32 viewport body height, and a
head-to-feet height within 0.80–1.25 of the actor's pre-preview baseline. A finite
planner sequence alone can no longer produce a `Passed` visual report.

The preview uses nonblocking `Worker.Schedule`, output readback requests and
polling; it does not call the benchmark's blocking `DownloadToArray` from the
animation callback. The measured ~80 ms CPU inference remains a material worker
budget and cadence risk even when the main thread does not synchronously wait for
the whole inference.

## Primary sources

- [NVIDIA SONIC ONNX contract](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/docs/source/references/planner_onnx.md)
- [Pinned NVIDIA GEAR-SONIC model](https://huggingface.co/nvidia/GEAR-SONIC/blob/6733128a3d8a523b1418b06bca3cdf61c8b0987f/planner_sonic.onnx)
- [NVIDIA dual license](https://huggingface.co/nvidia/GEAR-SONIC/blob/6733128a3d8a523b1418b06bca3cdf61c8b0987f/LICENSE)
- [NVIDIA G1 joint-order utility](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/gear_sonic/envs/env_utils/joint_utils.py)
- [NVIDIA reference deployment parameters](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/gear_sonic_deploy/src/g1/g1_deploy_onnx_ref/include/policy_parameters.hpp)
- [Pinned NVIDIA G1 MuJoCo skeleton](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/daf389964fa4a4545218e8405f24eb55f4912453/gear_sonic/assets/g1/g1_29dof_with_hand.xml)
- [Unity Inference Engine 2.6 supported models](https://docs.unity.cn/Packages/com.unity.ai.inference@2.6/manual/supported-models.html)
- [Unity Inference Engine worker/backends](https://docs.unity.cn/Packages/com.unity.ai.inference@2.6/manual/create-an-engine.html)
