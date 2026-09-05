# Environment-aware motion matching integration

## Boundary

EAMM is a base-pose source, not a second character controller.

- `PlanetMotor` remains the sole owner of gameplay root position, rotation and velocity.
- `EarthFootContactController` remains the sole owner of visible foot IK, knee hints and pelvis correction.
- Authored jump, landing, dodge, hit, magic and ragdoll lanes bypass the EAMM base pose.
- The magic Animator layer remains upstream of the base-pose job; EAMM is disabled for `MagicCast`.
- The hidden JLPM simulation bone follows the motor only to form a query origin. Its root result is never copied back.

`PlanetEAMMCharacterController` converts root, velocity and intent into a gravity-relative tangent frame. It also supplies non-alloc nearby-obstacle circles and samples predicted support height/slope. `EAMMBasePoseBridge` retargets the selected JLPM pose into an `AnimationScriptPlayable`; `ProcessRootMotion` is intentionally empty.

`EarthAnimationGraph` is now the single pose-composition graph. It evaluates the authored Animator controller, blends the optional EAMM world-space base pose, then runs `EarthInertializationJob`. The job keeps persistent per-bone output and rotation-offset state, captures offsets only on explicit semantic/action transitions, and decays them by a bounded half-life. Gameplay root translation is absent from the job, while planted feet/toes bypass generic decay and remain owned by the contact controller.

`EarthAnimationDriver` is the single runtime parameter/state writer after the graph is attached. The bridge copies the legacy Animator values into the playable controller once during graph creation; it must not resynchronize them every frame, because the Animator no longer receives the live locomotion parameters. Pose and choreography components therefore write through the same driver.

For tank-steered actors the EAMM trajectory adapter uses `A/D` as predicted heading change and `W/S` as signed translation, matching `PlanetMotor`. Facing and travel are kept separate: an in-place pivot changes the direction feature without adding displacement, while reverse locomotion preserves the body's forward-facing direction. The pivot source is baked without synthetic translation so an in-place turn cannot produce a sideways leg query.

The first production transition set covers run-to-stop, reverse/pivot, cast-to-locomotion, locomotion-to-hit and front/back recovery-to-locomotion. `EarthTransitionDirector` publishes inertialization requests; authored action changes provide a second safe trigger for magic, impact and recovery lanes.

## Frame-rate behavior

Databases are baked at 30 Hz. Runtime playback advances continuous database time by `deltaTime / databaseFrameTime` and interpolates adjacent poses. Render FPS is not locked and `Application.targetFrameRate` is untouched. Player and bot search cadence is controlled independently by `EAMMRuntimeProfile`.

## Setup

1. Create `Elemental/Animation/Motion Library`.
2. Assign the project-owned Humanoid source rig and drag provenance-safe clips into `clips`.
3. Set role, nominal speed/yaw/direction and contact/cancel/recovery windows.
4. Run `Elemental Suite/Character/Bake Selected EAMM Motion Library`.
5. On a hidden child, add `MotionMatchingController` and `PlanetEAMMCharacterController`; assign the generated `MotionMatchingData` and an `EnvironmentMotionMatchingSearch` asset.
6. On the visible Humanoid add `EAMMBasePoseBridge`, assign the hidden controller and an `EAMMRuntimeProfile`.

Missing data is a safe legacy fallback: the bridge does not create a graph until a valid controller, database and Humanoid Animator are present.

The visible Animator base layer is a 2D tangent-space locomotion tree driven by `MoveX` and `MoveY`; it includes forward/backward walk, authored run and left/right strafe. Ragdoll recovery chooses front or back authored recovery from the recorded recovery side. Procedural slope/acceleration lean and landing compression remain bounded additive presentation, not a second locomotion or root owner.

## Rollback

Disable player/bot flags in `EAMMRuntimeProfile` or remove `EAMMBasePoseBridge`. No motor, input, authored-action, ragdoll or IK code has to be reverted.
