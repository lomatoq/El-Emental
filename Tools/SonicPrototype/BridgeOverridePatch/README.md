# SONIC production-preview graph ownership fix

The failed production preview applied two Humanoid frames and then stopped receiving
`OnAnimatorIK`. `SonicPlannerPreviewAdapter` disabled `EAMMBasePoseBridge`, whose
`OnDisable` destroys the `EarthAnimationGraph` that owns the production Animator
output. The two applications were queued graph evaluations, not sustained playback.

The staged change keeps that graph alive and leases only EAMM's base-pose weight to
the experimental adapter. The authored controller continues to evaluate, SONIC writes
its pose in execution order 500, and `EarthFootContactController` keeps its later
execution order 1000 final terrain-contact pass. Stop, disable, destroy and failed
startup release only the lease they own; the bridge component's enabled state is
never changed.

Integration targets:

- `Assets/Elemental/Presentation/MotionMatching/EAMMBasePoseBridge.cs`
- `Assets/Experimental/SonicPrototype/SonicPlannerPreviewAdapter.cs`

Focused validation:

1. Compile with zero new warnings.
2. In Play Mode run `Elemental/Experimental/SONIC/5 Preview SONIC On Production Actor`.
3. Both walk and boxing captures must report increasing retarget counts, with
   `lastRetargetFrame` within three frames of each capture.
4. The report must retain `bridgeWasEnabled=true`, `bridgeRestored=true`,
   `cameraAndUiRestored=true`, and the final-foot-IK production test must remain green.
