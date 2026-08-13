# ADR-0013: Replaceable Humanoid presentation and IK rollback

Status: accepted  
Date: 2026-08-13

## Context

The primitive Earth Shaper conveyed physical state but could not provide production locomotion or spellcasting silhouettes. Gameplay movement and ragdoll authority must remain independent of whichever art model is installed.

## Decision

- KayKit Adventurers 2.0 FREE Mage and the selected KayKit Character Animations 1.1 collections are imported as Humanoid. Both archives are CC0; source URLs, versions and SHA-256 values are recorded in `THIRD_PARTY_NOTICES.md`.
- `CharacterPresentationProfile` owns the replaceable prefab, Avatar, controller, placement and blend/IK timing. `PlanetMotor` remains authoritative; Animator root motion is disabled.
- The generated controller contains an Idle/Walk/Run blend tree, Jump/Fall/Land states, an upper-body casting layer with AvatarMask and a restrained additive impact layer. `HumanoidCharacterPresentation` drives parameters and Humanoid hand IK toward the current Earth focus.
- The existing active-ragdoll bodies remain the physical proxy. Their renderers are hidden when the Humanoid is valid. `HumanoidRagdollBridge` disables Animator in `FullRagdoll`, then rebinds and blends the visual root back during recovery. The primitive pose driver stays as an explicit fallback.
- `com.unity.animation.rigging@1.4.0` was evaluated because it is the documented Unity 6 line. On Unity 6000.5.7f1 the package source fails compilation on APIs made obsolete-as-error (`GetInstanceID`). It is therefore not shipped. Built-in Humanoid `OnAnimatorIK` is the rollback path and preserves a clean project. Re-evaluate Animation Rigging when Unity publishes a package revision compatible with 6000.5.

## Consequences

Character art can be swapped without changing movement or magic code, and the project stays buildable on the locked editor version. Built-in IK provides hand targeting but not the full constraint-authoring UI of Animation Rigging. Physical recovery currently blends the visual model over the existing proxy rather than creating a second set of simulated Humanoid bones.

