# Rest-calibrated head pitch stabilization (staged)

The fresh completed All11 matrix at `20260905T105754735Z` rules out paused-frame
accumulation. The production probe measured `46.177°` for RaisePlatform contact and
`61.566°` for Armor contact; the images show the face aimed upward and visually
compressed into the torso. Choreography can contribute only `3°`, so these excursions
come primarily from the authored upper-body clips. Other useful poses range from
`-11.69°` to `29.78°`, with Resonance briefly reaching `32.23°`.

Disabling the AvatarMask Head channel would remove every authored neck/head gesture,
including valid ones and impact response. This candidate instead extends the existing
final `HumanoidProceduralBodyResponse` head owner. It uses the exact avatar-rest
forward calibration already used by `EarthAnimationPoseProbe`, leaves pitch inside
`-25..+28°` untouched, and rotates only the excess back to the nearest bound. The
correction is applied to Neck (Head fallback) so the authored Head-to-Neck local
relationship and bone length remain intact. Mantle and ragdoll already return before
this path; paused GameTime also remains untouched. The envelope is applied only while
`CurrentAuthoredAction == MagicCast`, matching the reproduced scope and leaving
locomotion, surf, idle and other authored actions unchanged.

The pure solver tests preserve all in-range values, reproduce corrections for the
actual `46.177°`, `61.566°` and `32.23°` observations, clamp downward excess, and
reject non-finite input without adding rotation. The AllMagic diff records neck length
and rejects any captured pose outside the rest-calibrated envelope. After integration:

1. run the focused EditMode solver fixture;
2. rerun All11 and require all 36 images/captures, no head contract rejection, and
   inspect RaisePlatform/Armor contact and recovery side by side with the cited run;
3. run the paused-pose 2/2 and mantle 2/2 gates because this component owns both the
   pause guard and the final body/head adaptation;
4. accept only if the head still shows authored motion inside the envelope and the
   neck silhouette is not compressed.

This folder is Tools-only and has not been compiled or run in Unity.

Focused invocation: `Elemental.Tests.EditMode.HeadPitchStabilizerTestLauncher.Run()`;
report paths: `BuildReports/HeadPitchStabilizerEdit.json/xml`.
