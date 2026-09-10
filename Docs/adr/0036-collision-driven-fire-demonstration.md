# ADR 0036: Collision-driven Fire demonstration

Status: implemented candidate outside Assets, 2026-09-09, base `b646f675`; native acceptance pending.

The user explicitly supersedes the fixed axial volume as the required Fire proof:
a powerful aimable stream must transport fire around real obstacles like flowing
hot gas. Depth-clipping a straight capsule or painting a stationary wall fan does
not satisfy that behavior.

Use a bounded Lagrangian transport model: 192 preallocated parcels, 240 births/s
at the actual hand nozzle, 13.5–15 m/s injection, inertia retained after aim
changes, coherent curl and buoyancy, finite 8 m traveled path, .68–.78 s life.
These are provisional demonstration settings, not user-approved numeric budgets.
An injected `IFireFlowCollision` owns swept contacts. Its Runtime adapter queries
current PhysX geometry, moving surface velocity and an explicit ignored emitter.
A prewarmed disabled sphere resolves overlap/growth; inward momentum becomes
lateral flow, with residual sweeps for up to three contacts in an internal step.
Queries saturating or finding an embedded center reject that gas; exhausted
frame budgets stop movement, never advance unqueried positions. Step budget is
768 collision queries, each consisting of a bounded overlap and sweep (and up to
24 overlap penetration checks), not merely 768 raw PhysX calls. Steps target90Hz,
with at most6 internal steps and an explicit dropped-time diagnostic above1/15s.

Presentation integrates absorption/emission through ten samples in each
transported ellipsoid. Bounds align to physical velocity, not camera. Real
contact planes clip swelling at a wall/corner, and camera depth clips opaque
occlusion. Parcel centers sort back-to-front in one preallocated mesh. Shader
in Resources is included for standalone; local duel binding selects it explicitly
and surfaces missing/unsupported shader errors. Decorative column fires remain
on their independent backend.

This is cosmetic physical transport, **not an incompressible/SPH/grid solver**:
no particle pressure solve, incompressibility constraint, fluid-grid framework,
GPU readback or new rigid bodies. It cannot move stones or apply damage.
`FireWorld` and `FireStreamSession` retain existing gameplay authority, cover,
range and 10Hz damage. Deflected visible gas does not silently gain damage around
corners. Gameplay-following-flow would need a separate sampled volume/contact
contract and tests before adoption.

Evidence so far: six pure solver NUnit tests executed under .NET10 pass6/6;
all changed full Unity assemblies compile offline using current Bee references.
Runtime assembly retains seven preexisting obsolete-API warnings, none in new
files. No Unity Play/PhysX/shader compile/visual/GPU result is claimed yet.

Required native gates: `FireFlowCollisionPlayTests` head-on/oblique/corner,
`CollisionDrivenFireActualLinebreakerHeadOnObliqueCornerOrbitAimAndMovingCapture`,
existing lifecycle/ownership regression, shader-error scan, actual frame review
and standalone GPU/CPU/allocation captures. The saved Linebreaker and his motor
must remain visible/active. Alpha sorting is per parcel (not a joint density
integral), transparent surfaces without depth are not opaque blockers, and a
camera along the stream may incur substantial overdraw. These require measurement.

Research context: [GPU Gems3, fluid transport and volume rendering](https://developer.nvidia.com/gpugems/gpugems3/part-v-physics-simulation/chapter-30-real-time-simulation-and-rendering-3d-fluids)
informs separation of motion and rendering; the chosen inexpensive transport is
not an implementation of its grid solver. [Fuller etal2007](https://web.cs.ucdavis.edu/~hamann/FullerKrishnanMahrousHamannJoyFirePaperFor_I3D2007AsSubmitted11012006.pdf)
informs stylized density/emission profiles, not proof of this backend's beauty.
