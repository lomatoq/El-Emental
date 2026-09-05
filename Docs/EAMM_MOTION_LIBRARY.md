# EAMM motion library

The baker accepts Unity Humanoid `AnimationClip` references, so animation provenance stays in the project import pipeline. It samples poses at the configured 30 Hz, creates a JLPM skeleton, pose database, contact flags and normalized feature database under `StreamingAssets/MMDatabases`.

Minimum production catalog:

- idle and guarded idle;
- forward/back/left/right starts and stops;
- walk/run forward, backward and strafes;
- 90° and 180° pivots in both directions;
- front/back recovery;
- gather, pull, push, lift, slam, sustain and release magic families;
- directional medium stagger clips.

Locomotion clips feed the searchable base database. Jump, fall, landing, dodge, magic, hit and recovery recipes retain their metadata but remain authored action lanes until a dedicated database/tag policy is approved. This prevents a nearest-pose search from cutting through contact-critical moves.

The validator rejects missing source rigs, empty recipes, non-Humanoid motion and reversed contact windows. No upstream demo mocap, characters, BVH files or scenes are imported.

`MotionClipRecipe` now carries a stable semantic ID, nominal speed/yaw/direction, contact/cancel/recovery windows and a semantic family. Pair-specific `MotionTransitionOverride` entries carry inertialization half-life, destination phase and gait-phase preservation. On script reload, legacy project libraries are migrated once to the production catalog; the manual equivalent is `Elemental Suite/Character/Populate Production EAMM Catalog`.

The synchronized project catalog uses the existing Mixamo and KayKit imports for neutral/guard idle, forward/back locomotion, two runs, strafes, crouch/sneak, jump/land, four dodges, front/back recovery, light/medium impact and gather/pull/push/lift/slam/sustain/release magic. Only idle/start/locomotion/stop/pivot roles enter nearest-pose search; all contact-critical action clips stay in explicit authored lanes.
