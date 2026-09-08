# Authored living hold and magic continuity

Working tree on main `1235579`, September 8. Implementation is pending fresh
Unity runtime/visual acceptance; previous animation acceptance does not prove
this patch. Existing manually assigned locomotion and cast clips are preserved.

## Observed causes and changes

- `EarthMagicClipClock` restarted a smoothstep at each phase. Short phase
  handoffs repeatedly decelerated the clip; it now progresses at the bounded
  phase rate. Contact markers still constrain progression. This is not a claim
  of mathematically C1 clock velocity or removal of every gameplay phase hold.
- Cancellation presents slot zero. The old clock looked up speed for that slot
  and obtained zero, freezing recovery. Recovery now retains the accepted clip
  slot, and the presenter retains that clip's timing metadata during release.
- An A/B crossfade previously held its outgoing source on one frame. It now
  continues the outgoing recovery from the actual buffer time, including combo
  time overrides, independently of the incoming clip clock.
- Magic clocks now consume the presentation-clock multiplier, matching the
  controller during slowed menus. No simulation clock or event tick changes.
- A new additive `Earth Living Hold` layer adds authored torso motion only after
  a real held-body/gravity/vector session has rendered its contact. Cancellation
  fades it out; explicit reset/disable clears its weight atomically. It does not
  admit ordinary one-shot Sustain labels as held ownership.

## Content

`EarthLivingHoldAuthoring.Install()` derives `Earth Living Hold.anim` from the
existing looping Mixamo X Bot Idle, using its original frame-zero additive
reference. Only Spine/Chest/UpperChest muscle curves survive. Root, hips, arms,
hands, legs, IK and clip events are removed. The Humanoid mask adds another
torso-only boundary. The layer plays at original speed with maximum weight .35.
No arbitrary attack subsegment is looped and no random joint oscillation is used.

This adds breathing/torso movement to the held silhouette; it is not equivalent
to a bespoke full-body sustain gesture with animated fingers and weight transfer.
Existing KayKit Spellcasting/Long/Summon sources are imported as non-loop clips;
their names alone do not establish a production-quality cyclic hold.

The installer touches only its derived clip/mask and one additional controller
layer. It does not rebuild the scene, source imports, locomotion, or existing
magic assignments. The final feet/pelvis/hand owners remain unchanged.

## Verification

- `EarthLivingHoldTestLauncher.RunEdit`: cancellation with zero slot,
  30/60/120 Hz phase progression, externally timed combo recovery, admission,
  moving torso-only installed content, plus the existing rescue fixture.
- `EarthLivingHoldTestLauncher.RunPlay`: actual ten-second gravity hold and
  cancellation; existing held release/reacquisition; repeated punches and
  30/60/120 Hz punch continuity.
- `BuildReports/LivingHold/hold-trace.json` records rendered torso/hand travel,
  per-frame jumps, loop phase and layer weight. PNGs are captured every two
  seconds. Trace limits do not replace inspection of movement at normal speed.
- `Elemental.Character.LivingHold` measures admission/weight CPU work. Existing
  character presentation and Unity animation profiling must establish total cost;
  the admission marker does not include Animator graph evaluation.

## Sources and direction

Unity documents [additive animation layers](https://docs.unity3d.com/Manual/AnimationLayers.html)
and [explicit additive reference poses](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimationUtility.SetAdditiveReferencePose.html).
Adobe confirms [Mixamo commercial use without an additional purchase](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html).
Sources checked September 8, 2026.

The preferred next content investment is a small consistent entry/held-loop/
release/chain set. The existing per-bone angular-velocity inertializer is already
implemented, so the old neural-research note's proposal to add it is superseded.
SONIC remains an isolated prototype, with its own measured 79–82 ms p95 planning
and a 774 MB model; this patch does not adopt a neural gameplay dependency.

## Fresh implementation evidence — 2026-09-08

Unity recovered from its unrecoverable D3D11 device-reset dialog; recovery copies preserved, production scene reopened. Living Hold installer succeeded and saved the derivative torso clip/mask plus existing-controller layer. LivingHoldEdit **27/27** passed at00:16:29Z; LivingHoldPlay **6/6** passed at00:22:53Z in80.098s: actual10s hold/cancel, held aim/reacquisition, repeated punches and30/60/120Hz continuity. Hold trace maximum chest step .578782degrees, summed torso travel2.850973degrees, hand travel .095597/.165650m. These are path lengths, not displacement or extra-effect-only amplitude. Captures `BuildReports/LivingHold/hold-00…08.png` inspected at production view. Full-body authored hand-loop and whole-game animation/GPU acceptance are not implied.

Fog/wind pure Edit **33/33** passed00:16:08Z. Initial combined Play **4/5**: both new UI tests, existing held centered press, and production wind passed; strict fog pixels exposed a further defect. `AtmosphereFullscreen.shader` used raw depth >1e-5 to classify opaque geometry; far surfaces at4000m with .1m near clip were treated as sky. It now tests exact cleared depth (>0 reversed / <1 normal). This is separate from closure and post-fog detail attenuation. Strict black/white and depth-independence test rerun follows.
