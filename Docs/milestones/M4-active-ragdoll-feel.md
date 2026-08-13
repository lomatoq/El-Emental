# M4 Active Ragdoll + Feel

Status: complete

## Gate

Implement `AnimatedMotor`, `PhysicalAssist`, `Stagger`, `FullRagdoll`, and `Recovery`; configurable-joint puppet profiles; pose targets; local-gravity balance; impact-driven stagger debt and muscle weakening; deterministic-enough recovery; debug view; CharacterFeelLab; 200-impact and 100-recovery stress fixtures with zero steady-state GC allocations.

## Current evidence

- Pure physical state machine and typed state snapshot implemented.
- Recovery selection is relative to local gravity and tested on all six axial planet sides.
- Balance torque is finite, tangent, and clamped.
- 200-impact and 100-recovery-cycle EditMode stress fixtures pass.
- CharacterFeelLab contains a six-body configurable-joint puppet, falling rocks, repeated pushes, a recovery slope, and local-gravity motor coupling.
- Joint drive profiles expose spring, damping, maximum force, angular limits, mass distribution, and editable pose targets.
- Runtime impact/state events route to optional particles, audio, and camera feedback without presentation authority.
- Scene stress and minimal-puppet PlayMode fixtures pass with finite velocities and bounded joints.
- Steady-state physical controller test measures 0 B managed allocations across 1000 steps.
- EarthCore uses a valid CC0 KayKit Mage Humanoid as a replaceable visual layer. The Animator is root-motion-free, exposes locomotion/jump/casting/impact layers and uses built-in hand IK while the hidden configurable-joint proxy remains physical authority.
- A moving-platform grace window suppresses self-impact debt and temporarily filters rider/puppet collisions. The carry solver accelerates feet toward the rising top, preserves support velocity for locomotion/jump and restores ordinary collision after settling.
- Animation Rigging 1.4.0 is documented but rolled back on Unity 6000.5.7f1 because its package source does not compile against obsolete-as-error APIs; ADR-0013 records the built-in Humanoid IK fallback and upgrade path.
