# Directed-rise lifecycle v2

Apply after `directed-rise-fix`.

The initial lease counts down in `PlanetMotor.FixedUpdate`. A kinematic motor exits
that method before decrementing while `EarthPillarMobility` can still finish its
own rise. The remaining lease would then suppress locomotion after the body became
dynamic again.

This two-file follow-up gives the pillar explicit lease release at the completion
boundary and cancels a pending rise on component disable. It prevents stale launch
or locomotion suppression after disable/kinematic lifecycle transitions.

Files to copy over `Assets/Elemental`:

- `Runtime/Characters/PlanetMotor.cs`
- `Runtime/Characters/EarthPillarMobility.cs`
