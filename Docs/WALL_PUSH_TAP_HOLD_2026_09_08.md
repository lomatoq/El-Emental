# Ctrl+RMB charge and release

Hold Ctrl and click RMB for an ordinary wall shove. Hold both for charge, then release RMB while Ctrl remains held for a stronger shove. The wall stays at its captured pose during charge. Releasing Ctrl first, pause/stun or cancellation discards charge without launching and restores the previous body state. The charge reaches its maximum after1s, with an initial0.15s click deadzone.

Begin and charge steps emit no impulse. Release emits exactly one mass-dependent impulse: min(12000,14*mass), multiplied by1..2.5 according to charge. Repeated release cannot fire again. Launched speed is bounded14..28m/s according to released charge. Coasting drag is30% of the original held-wall drag; mass, colliders and physical obstacle response remain intact. Aim direction is latched; PhysX owns released translation; existing ground support probes and bounded dust/chips remain active.

Input routing commits only on RMB release with Ctrl still held. Cancellation never calls the launch method. The existing semantic Ctrl bit and network frame shape remain unchanged. No gauge or new menu settings are introduced.

Final Edit99/99 passed at2026-09-08T15:40:19.5359569Z. These include one-shot release, no impulse during charge, cancellation, repeated begin, mass-dependent tap/charge strength, time-partition invariance and routed mouse-release versus Ctrl-release semantics. Runtime checks require a stationary charging pose, no launch on cancellation, real paired Ctrl+RMB release, inertial travel, preserved wall mass, and a comparison of0.06s tap versus1.5s hold on the same physical floor. Results follow after execution.

## Runtime evidence

WallPushPowerPlay3/3 passed at2026-09-08T15:41:47.5587137Z (42.8382s). Actual paired-input release: stationary speed0 before RMB release,13.2735m/s after release; no active charge or retained router target afterward. Grounded input fixture preserved895.0063kg, emitted231 dust/39 chips, traveled1.5958m into existing physical rubble and passed explicit barrier stop/fracture. Held-push marker peak0.0767ms.

The same physical floor and1668.881kg production wall:0.06s click traveled1.434688m;1.5s held charge followed by release traveled7.252494m (5.06 times tap). Peak velocities7.1936 versus17.9774m/s. Both settled below0.0001m/s on the floor. No movement before release; cancellation and repeated release checks passed. This controlled-floor result does not promise clearance through arena obstacles. No new online pair or GPU measurement.
Final editor state: EarthCoreSlice open, nonplaying and clean. Console has zero errors; the pre-existing native Graphics Ring Buffer warning recurred on scene restoration. No new script compiler warnings.
