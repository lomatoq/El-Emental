# Neural animation research — 2026-09-05

Status: research only. No neural runtime, model asset, package dependency, training
job or product adoption is approved by this note.

## Identification of the Unity demonstration

The referenced Reddit post is titled “I implemented Nvidia MotionBricks in Unity
as a third person controller”. The author says in the discussion that the Unity
prototype uses NVIDIA's **SONIC Planner ONNX**, first on CPU and then re-exported
for Unity Sentis GPU execution. The listed Humanoid support and more than 25 styles
also match the 27 modes in the official SONIC V2 planner. The post is therefore not
evidence of a released Unity port of the complete MotionBricks stack.

The Unity asset mentioned by the author is not published as of this research
snapshot. Its exact ONNX revision, model redistribution terms, supported build
targets, memory use, CPU latency and retarget implementation cannot be verified.
The CPU claim is a prototype-author claim, not an NVIDIA benchmark.

## SONIC Planner and MotionBricks are different products

### SONIC Planner

The official V2 planner is one fixed-batch ONNX model using opset 17. It receives
four recent Unitree G1 MuJoCo poses, desired velocity, movement and facing
directions, a behavior mode and optional keyframe constraints. It returns 24–64
valid frames at 30 Hz as 36-value G1 poses: world root position, root quaternion
and 29 joint angles.

The 27 modes cover idle, slow walk, walk, run, height-controlled ground poses,
crawling, boxing, left/right jabs and hooks, random punches and several styled
walks. They do not contain the project's eleven Earth-specific cast semantics,
ledge mantle, impact recovery or spherical-surface contact policy.

NVIDIA's deployment stack runs the planner on a dedicated thread at 10 Hz, uses
TensorRT and CUDA graphs, resamples 30 Hz output to 50 Hz and crossfades replans
over eight frames. Boxing periodic replanning is one second; mode or direction
changes can request an earlier replan. The published `planner_sonic.onnx` is about
774 MB. NVIDIA does not publish a CPU latency result for it.

### MotionBricks

MotionBricks is the larger modular keyframe-to-motion system shown in NVIDIA's UE5
demo. Smart locomotion and smart-object primitives emit proxy keyframes; the root,
pose-token and decoder modules generate approach, contact and follow-through. The
paper's 15,000 generated frames per second and 2 ms latency were measured on an
RTX 5090 through an ONNX/TensorRT native UE5 plugin, not on CPU.

The current official repository exposes a G1-oriented preview with roughly 2.2 GB
of checkpoints and CUDA-oriented setup. Its README still describes a later full
release as forthcoming, so that roadmap statement is stale by the date of this
note and must not be treated as delivered functionality.

The independent `localai-org/motion-bricks.cpp` repository provides a C++23/GGML
CPU and Vulkan path with a stable C ABI. Its current F32 bundle is about 0.73 GB,
contains 183,148,382 parameters and outputs root translations plus local rotations
for a 34-joint G1 skeleton. It has no Unity package, no production Humanoid
retargeter and no current direct conversion from arbitrary character motion to its
style format. It is a young community port and needs its own correctness,
performance and platform review before product use.

## Fit with El-Emental

The project already includes Unity Inference Engine 2.6.1, Burst 1.8.30, Animation
Rigging 1.4.1 and the embedded JLPM/EAMM motion-matching packages. Unity documents
support for most ONNX models in opsets 7–25 and CPU/GPUCompute backends. Opset
compatibility alone is insufficient: the real SONIC file must import successfully
and every operator, tensor shape, memory allocation and readback must be profiled.
No current Elemental assembly references `Unity.InferenceEngine` and no production
code runs a neural model.

The viable integration seam is an optional base-pose source feeding the existing
`EarthAnimationGraph` before final contact correction:

- `PlanetMotor` remains the sole gameplay root, velocity, collision and local-up
  owner. Generated world-root translation is reference data and is never copied
  into gameplay.
- `EarthFootContactController` remains the final visible foot, knee and pelvis
  owner on pits, humps, slopes and the spherical planet.
- Authored magic, mantle, hit, recovery and ragdoll lanes retain their current
  semantic and physical authority.
- Planet tangent velocity/facing are converted into the model's temporary Z-up
  frame, and generated local rotations are converted back before presentation.
- Planner context comes from the uncorrected generated base pose. Feeding the
  final IK result back into the network would form the same delayed-feedback loop
  the existing animation rescue removes.

The main engineering risk is retargeting. SONIC returns G1 joint angles rather than
Unity Humanoid muscles or production-skeleton quaternions. A spike must reconstruct
the exact G1 hierarchy and then apply calibrated G1-to-Humanoid offsets for both X
Bot and Linebreaker. Different proportions, shoulder axes, head mapping and leg
lengths can recreate the compressed head, straight knee and foot-skate faults. The
generated stride also has to agree with the motor-owned root without time-dependent
sliding.

## Recommended order

1. Finish the current all-eleven magic visual and real-input acceptance. A neural
   locomotion generator cannot repair missing gameplay events, stale presentation
   requests, wrong cast contact markers, unstable hand poles or unsuitable source
   clips.
2. Upgrade the existing final-pose inertialization from pose-offset-only continuity
   to velocity-preserving per-bone continuity. The embedded JLPM runtime already
   contains angular-velocity transition and implicit-spring update math, so this is
   smaller and lower-risk than adding another animation framework.
3. If neural motion is still desired, run an isolated SONIC feasibility spike:
   import the exact ONNX, validate reference tensors, measure CPU and GPUCompute
   startup/memory/p50/p95, reconstruct a hidden G1 pose, then retarget only idle,
   walk, run, turn and boxing into the base-pose lane behind a feature flag.
4. Compare the neural source against the current EAMM source on both characters at
   measured 30/60/120 Hz, including walk-stop, rapid direction changes, repeated
   left/right punches, head orientation, one-frame bone angular velocity, foot
   gap/drift and spherical hump/pit/slope traversal. Preserve the EAMM fallback.
5. Reconsider full MotionBricks only after the preview exposes the required
   production interfaces or the community native path passes platform, license,
   memory and retarget gates. Smart-object motion can propose a mantle pose, but
   motor collision, ledge admission and contact events remain authoritative.

No custom training is needed for a pretrained feasibility spike. It is not a quick
fallback if the supplied modes are inadequate: the official SONIC guide recommends
64 or more GPUs for training, and the SONIC paper reports about 9,000 GPU-hours.

## Licensing

NVIDIA's source code is Apache 2.0. SONIC and MotionBricks pretrained weights use
the NVIDIA Open Model License, which allows commercial use and redistribution but
requires the license copy and the attribution “Licensed by NVIDIA Corporation
under the NVIDIA Open Model License.” Use is also subject to NVIDIA's Trustworthy
AI and trade-compliance terms.

BONES-SEED has a separate dataset license. The published terms permit qualifying
academic users and qualifying startups below USD 1 million annual gross revenue;
other commercial users must obtain a separate license. Using NVIDIA's pretrained
weights does not by itself require downloading BONES-SEED. Training or fine-tuning
on that dataset does.

## Primary sources

- [Referenced Unity Reddit demonstration](https://www.reddit.com/r/Unity3D/comments/1w6xetg/i_implemented_nvidia_motionbricks_in_unity_as_a/)
- [NVIDIA SONIC planner ONNX contract](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/docs/source/references/planner_onnx.md)
- [NVIDIA GEAR-SONIC model files](https://huggingface.co/nvidia/GEAR-SONIC/tree/main)
- [NVIDIA GR00T Whole-Body Control repository and training guidance](https://github.com/NVlabs/GR00T-WholeBodyControl)
- [NVIDIA MotionBricks project](https://nvlabs.github.io/motionbricks/)
- [MotionBricks paper](https://research.nvidia.com/labs/gear/motionbricks/pdfs/motionbricks_siggraph_2026.pdf)
- [Official MotionBricks preview README](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/motionbricks/README.md)
- [Community MotionBricks CPU/Vulkan port](https://github.com/localai-org/motion-bricks.cpp)
- [Unity Inference Engine supported models](https://docs.unity.cn/Packages/com.unity.ai.inference@2.6/manual/supported-models.html)
- [Unity Inference Engine CPU/GPU backends](https://docs.unity.cn/Packages/com.unity.ai.inference@2.6/manual/create-an-engine.html)
- [NVIDIA source and model-weight license](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/LICENSE)
- [BONES-SEED dataset license](https://bones.studio/info/seed-license)
