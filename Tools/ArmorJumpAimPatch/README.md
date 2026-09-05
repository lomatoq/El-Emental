# Armor, jump and centered-aim animation patch

This patch is staged outside `Assets`; it has not been imported or run by Unity.

Concrete source faults addressed:

- `EarthCharacterPoseController.ResolveSustainedState` treated an equipped armor
  shell as a permanent `ArmorAssemble` cast. The bound source pose raises both
  hands and owns body/head through the upper-body mask, so the actor stayed bent
  for the full armor lifetime. The patch leaves persistent armor on ordinary
  locomotion and keeps finite armor action requests independent.
- Armor previously had no physical movement weight. A dedicated continuous
  encumbrance lane now reduces ordinary locomotion to about 83% speed for compact
  armor and 75% for fully expanded armor. It does not enter cast stance, disable
  automatic mantle, or replace ordinary locomotion ownership. Other live action
  brace multipliers still combine once through the existing motor path.
- A short Space press is initially routed as `EarthActionOwner.Pillar`, even though
  `EarthPillarMobility.IsCharging` remains false until the hold threshold. The old
  pose policy displayed `PillarJump` immediately. The patch gates that sustained
  pose on a real charge and clears a pre-existing cast once on a confirmed ordinary
  motor takeoff. Typed `PillarRaised` events and real charged pillar presentation
  remain intact.
- `EarthChoreographyVisualSolver` always chose a dominant side; exactly forward
  aim (`localDirection.x == 0`) therefore added right-biased chest/head yaw and
  roll. The patch attenuates only those late lateral body offsets near centered
  aim. Authored arm motion, pitch, off-center aiming, hand IK, magic A/B buffers,
  inertialization, foot IK and punch timing are unchanged.

Copy the contents of `after/Assets` over the project `Assets` folder after rebasing
against any newer edits to the six production files. Then run:

- `Elemental/QA/Armor Jump Aim Animation Edit Tests`
- `Elemental/QA/Armor Jump Aim Animation Play Test`

Expected reports:

- `BuildReports/ArmorJumpAimAnimationEdit.json`
- `BuildReports/ArmorJumpAimAnimationPlay.json`

The Play test uses the production scene and real synthetic Shift+MMB / short Space
input. It requires armor to remain active after the finite presentation window,
armor encumbrance to be active while cast brace stays clear, the magic layer and
armor semantic pose to be released,
hands to return below the head, and the short Space route to become an ordinary
jump with no PillarJump overlay.
