# Raw magic source continuity audit

This utility is staged outside Unity `Assets`. It does not modify a controller,
clip, importer, scene or prefab.

Copy `Assets/Elemental/Authoring/Editor/EarthMagicSourceClipContinuityAudit.cs`
into the matching project folder, let Unity compile, then invoke:

`Elemental/QA/Audit Raw Magic Source Clip Continuity`

The command samples all eleven clips from the saved `Earth Curated Casts` direct
BlendTree on both production Humanoid avatars:

- `Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx`
- `Assets/ThirdParty/Mixamo/X Bot.fbx`

Every clip is sampled directly over normalized `.15-.35`; no AnimatorController
evaluation, EAMM/C1 job, Animation Rigging, native IK or gameplay presentation is
active. The report includes two sample spacings at 30 and 60 Hz:

- `SourceSeconds`: native clip-time steps of 1/30 and 1/60 second.
- `RuntimeClock`: the normalized steps currently requested by
  `EarthMagicClipClock.MaximumSpeedForSlot(slot)` at those render rates.

This distinction establishes whether a large arm step exists in the imported
Humanoid source itself and how much the production normalized clock amplifies it.
The report is written to:

`BuildReports/SeptemberAnimation/MagicSourceClipContinuity.json`
