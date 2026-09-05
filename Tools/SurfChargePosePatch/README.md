# Surf charged-jump presentation seam

Staged outside `Assets`; Unity has not imported or run it.

While the action router retains `Surf` ownership, a real
`EarthPillarMobility.IsCharging` state now takes presentation priority and uses the
existing `PillarJump` semantic pose. Its focus follows the motor's tangent-forward
axis and points below the rider, matching the forward-tilted launch construction.
The motor, pillar mobility and surf controller remain the sole owners of charge,
launch impulse, pillar angle and board breakup. On release, the existing
`PillarRaised` event supplies the finite committed launch presentation.

The previously verified ordinary short jump is unchanged: it still cannot select
`PillarJump` before a real mobility charge begins.

