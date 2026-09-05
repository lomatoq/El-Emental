# Armor / jump / centered-aim visual proof

Staged outside `Assets`; no Unity operation has been performed.

The focused PlayMode test loads the production `EarthCoreSlice`, waits for the
readiness gate before touching input, then uses the shipping PlayerInput routes:

- centered pointer baseline;
- physical Shift+MMB armor held beyond its finite activation gesture;
- short Space tap through the real jump-versus-pillar disambiguation. The test
  waits 0.8 seconds for released armor pieces to clear, observes every rendered
  frame through the first 0.22 seconds of flight, and captures at about 0.15
  seconds after actual support loss.

It renders the production main camera synchronously from fixed full-body front
and profile compositions, restoring its transform, lens and render target after
every image. The JSON records head height, torso/head tilt, torso yaw relative to
the motor facing, anatomical head-above-feet height, both hands below the head,
maximum early-jump hand asymmetry and magic-layer weight, semantic pose,
magic-layer weight, armor encumbrance and cast brace.

Run `Elemental/QA/Armor Jump Aim Visual Proof`.

Expected outputs:

- `BuildReports/ArmorJumpAimVisualProofPlay.json`
- `BuildReports/ArmorJumpAimVisualProof/Latest.json`
- timestamped folder containing four PNGs and `ArmorJumpAimVisualManifest.json`
