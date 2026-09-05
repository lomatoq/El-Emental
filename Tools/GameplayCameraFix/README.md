# Production gameplay camera framing fix

## Confirmed causes

1. `EarthCameraStateProfile.Height` is documented and authored as camera height
   above the motor root. `CinemachineThirdPersonFollow` raises the camera by
   `CameraDistance * sin(downwardPitch)`, but the controller omitted that term
   when solving `VerticalArmLength`. In Explore the omitted lift is about 1.46 m.
   The saved 0.42 m minimum arm adds another 0.41 m of vertical lift.
2. The Main Camera is saved as a physical camera with Horizontal gate fit while
   the Brain lens override is disabled. The virtual camera supplies vertical FOV
   values, but the physical horizontal gate crops the vertical frame at a 16:9
   aspect. This is the same class of lens mismatch found in the separate SONIC QA,
   but here it is fixed in the permanent gameplay owner.

Together these place the production camera substantially above the profile and
remove lower-body margin even though the virtual camera reports a 60-degree FOV.

## Runtime change

- The Brain explicitly owns a Perspective lens and the output Camera leaves
  physical mode in `PrepareRig`.
- A pure solver subtracts tracking height, shoulder height and pitch-induced
  distance lift before choosing the vertical arm.
- State distance, height, pitch, shoulder offsets, damping and FOV remain sourced
  from the existing camera profile, so Aim/Draw/Hold/Airborne composition and the
  return to Explore keep their authored deltas.

## Saved-scene parity

After copying the staged sources, update only these existing serialized values in
`EarthCoreSlice.unity` through Unity serialization (do not regenerate the scene):

- `Gravity Toy Camera/Camera`: `usePhysicalProperties = false`.
- `Gravity Toy Camera/CinemachineBrain/LensModeOverride`: Enabled, default
  Perspective.
- `Gravity Toy Camera/EarthCinemachineCameraController.minimumArmLength = 0`.

The runtime owner enforces the same values, while saving them removes misleading
Inspector state and first-frame/editor-preview disagreement.

## Focused validation

- `Elemental/QA/Gameplay Camera Framing Edit Tests` writes
  `BuildReports/GameplayCameraFramingEdit` reports.
- `Elemental/QA/Gameplay Camera Full Body Visual Proof` writes
  `BuildReports/GameplayCameraFramingPlay` reports and three current-camera PNGs
  plus `BuildReports/GameplayCameraFraming/Latest.json`.

The visual proof uses the real production Cinemachine camera without changing its
transform, FOV or lens during capture. It checks head and both final humanoid feet
in neutral, a production bend camera state, and the return to Explore, with a 7%
lower-frame foot margin and a 6% upper-frame head margin.
