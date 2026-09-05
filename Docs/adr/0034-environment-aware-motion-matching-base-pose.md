# ADR 0034: Environment-aware motion matching as a base-pose source

Status: accepted for feature-flagged spike.

## Decision

Vendor pinned MIT code from JLPM MotionMatching and UPC-ViRVIG EAMM. Bake only project-owned Humanoid clips. Run search/playback on a hidden simulation skeleton and retarget the result through an `AnimationScriptPlayable`. Never copy the simulation root into gameplay. Disable the EAMM weight for authored actions and ragdoll, then let the existing foot-contact controller resolve final contacts.

## Consequences

This preserves deterministic gameplay and current action authority while allowing environment-aware base locomotion. It also keeps rollback to one profile flag. The cost is a dedicated bake step and explicit retarget setup. A production rollout is blocked until the quality matrix and zero-GC profile are green with the final motion catalog.

## Implementation note — 2026-09-02

The feature-flagged spike now composes Animator, optional EAMM and cached per-bone inertialization in one `EarthAnimationGraph`. `EarthInertializationJob` excludes gameplay root translation and planted foot/toe groups. The catalog has stable semantic IDs and pair-specific transition overrides; the searchable database excludes magic, recovery, impact, jump and dodge actions. This does not promote the ADR to production acceptance: the requested implementation pass intentionally omitted PlayMode and capture runs.
