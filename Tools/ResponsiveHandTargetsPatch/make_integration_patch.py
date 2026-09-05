from pathlib import Path
import difflib


project = Path(__file__).resolve().parents[2]
relative = Path("Assets/Elemental/Presentation/Animation/HumanoidCharacterPresentation.cs")
source_path = project / relative
original = source_path.read_text(encoding="utf-8")
modified = original

replacements = (
    (
        "        private int _activeBaseStateHash;\n"
        "        private HandIkState _handIkState;",
        "        private int _activeBaseStateHash;\n"
        "        private EarthResponsiveHandTargetState _responsiveHandTargetState;\n"
        "        private HandIkState _handIkState;",
    ),
    (
        """        private void UpdateHandTargets()
        {
            if (_handIkState == HandIkState.Inactive || leftHandTarget == null || rightHandTarget == null) return;
            Vector3 focus;
            if (executor != null && executor.IsGravityWellActive) focus = executor.GravityWellFocus;
            else if (executor != null && executor.IsVectorFieldActive) focus = executor.VectorFieldPoint;
            else if (executor != null && executor.HeldBody != null) focus = executor.HeldBody.worldCenterOfMass;
            else focus = transform.position + transform.forward * 1.4f + motor.LocalUp * 0.8f;

            // Telekinesis points can be many metres away. They define gaze/aim,
            // not a literal wrist destination. Feeding the distant point directly
            // into TwoBoneIK fully stretched both arms and flipped the elbows.
            Transform chest = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Chest)
                : null;
            Vector3 shoulderCenter = chest != null
                ? chest.position
                : transform.position + motor.LocalUp * 0.66f;
            Vector3 aimDirection = focus - shoulderCenter;
            if (aimDirection.sqrMagnitude < 0.001f) aimDirection = transform.forward;
            EarthCastPhase castPhase = poseController != null
                ? poseController.CurrentRequest.Phase
                : EarthCastPhase.Sustain;
            float effort = poseController != null
                ? poseController.CurrentRequest.Effort01
                : _castWeight;
            Vector3 localAim = transform.InverseTransformDirection(aimDirection);
            EarthMagicReachSample reach = EarthMagicReachSolver.Resolve(
                new float3(localAim.x, localAim.y, localAim.z),
                castPhase,
                effort);
            aimDirection = transform.TransformDirection(new Vector3(
                reach.LocalAim.x,
                reach.LocalAim.y,
                reach.LocalAim.z)).normalized;
            float targetReach = reach.ReachMeters;
            Vector3 reachableFocus = shoulderCenter + aimDirection * targetReach;
            Vector3 across = Vector3.Cross(motor.LocalUp, aimDirection).normalized;
            if (across.sqrMagnitude < 0.1f) across = transform.right;
            leftHandTarget.position = reachableFocus - across * reach.HandSpreadMeters;
            rightHandTarget.position = reachableFocus + across * reach.HandSpreadMeters;
            Quaternion rotation = Quaternion.LookRotation(aimDirection, motor.LocalUp);
            leftHandTarget.rotation = rotation;
            rightHandTarget.rotation = rotation;
        }""",
        """        private void UpdateHandTargets()
        {
            if (_handIkState == HandIkState.Inactive || leftHandTarget == null || rightHandTarget == null)
            {
                EarthResponsiveHandTargetSolver.Reset(ref _responsiveHandTargetState);
                return;
            }

            bool hasLiveFocus = false;
            Vector3 focus = default;
            if (executor != null && executor.IsGravityWellActive)
            {
                focus = executor.GravityWellFocus;
                hasLiveFocus = true;
            }
            else if (executor != null && executor.IsVectorFieldActive)
            {
                focus = executor.VectorFieldPoint;
                hasLiveFocus = true;
            }
            else if (executor != null && executor.HeldBody != null)
            {
                focus = executor.HeldBody.worldCenterOfMass;
                hasLiveFocus = true;
            }
            // A one-shot retains the authored arm pose. During sustained release,
            // keep the last body-relative target while the rig weight fades.
            if (!hasLiveFocus && !_responsiveHandTargetState.IsInitialized) return;

            // Telekinesis points can be many metres away. They define gaze/aim,
            // not a literal wrist destination. Feeding the distant point directly
            // into TwoBoneIK fully stretched both arms and flipped the elbows.
            Transform chest = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Chest)
                : null;
            Vector3 shoulderCenter = chest != null
                ? chest.position
                : transform.position + motor.LocalUp * 0.66f;
            EarthResponsiveHandTargetSample target;
            if (hasLiveFocus)
            {
                Vector3 desiredAim = focus - shoulderCenter;
                if (desiredAim.sqrMagnitude < 0.001f) desiredAim = transform.forward;
                EarthCastPhase castPhase = poseController != null
                    ? poseController.CurrentRequest.Phase
                    : EarthCastPhase.Sustain;
                float effort = poseController != null
                    ? poseController.CurrentRequest.Effort01
                    : _castWeight;
                Vector3 localAim = transform.InverseTransformDirection(desiredAim);
                EarthMagicReachSample reach = EarthMagicReachSolver.Resolve(
                    new float3(localAim.x, localAim.y, localAim.z),
                    castPhase,
                    effort);
                target = EarthResponsiveHandTargetSolver.Step(
                    ref _responsiveHandTargetState,
                    reach.LocalAim,
                    reach.ReachMeters,
                    reach.HandSpreadMeters,
                    true,
                    Time.deltaTime);
            }
            else
            {
                target = EarthResponsiveHandTargetSolver.Step(
                    ref _responsiveHandTargetState, default, 0f, 0f, false, Time.deltaTime);
            }

            Vector3 aimDirection = transform.TransformDirection(new Vector3(
                target.LocalAim.x, target.LocalAim.y, target.LocalAim.z)).normalized;
            Vector3 reachableFocus = shoulderCenter + aimDirection * target.ReachMeters;
            Vector3 across = Vector3.Cross(motor.LocalUp, aimDirection).normalized;
            if (across.sqrMagnitude < 0.1f) across = transform.right;
            leftHandTarget.position = reachableFocus - across * target.HandSpreadMeters;
            rightHandTarget.position = reachableFocus + across * target.HandSpreadMeters;
            Quaternion rotation = Quaternion.LookRotation(aimDirection, motor.LocalUp);
            leftHandTarget.rotation = rotation;
            rightHandTarget.rotation = rotation;
        }""",
    ),
    (
        """        public float HandConstraintWeight
        {
            get
            {
                if (_wasMantling) return _mantleHandWeight;
                return _magicHandConstraintWeight;
            }
        }
""",
        """        public float HandConstraintWeight
        {
            get
            {
                if (_wasMantling) return _mantleHandWeight;
                return _magicHandConstraintWeight;
            }
        }

        public bool HasResponsiveSustainedAim =>
            _responsiveHandTargetState.IsInitialized && _magicHandConstraintWeight > 0.001f;
        public float3 ResponsiveSustainedLocalAim => _responsiveHandTargetState.LocalAim;
        public float ResponsiveSustainedAimWeight => _magicHandConstraintWeight;
""",
    ),
    (
        "            _magicHandConstraintWeight = 0f;\n"
        "            _handIkState = HandIkState.Inactive;",
        "            _magicHandConstraintWeight = 0f;\n"
        "            EarthResponsiveHandTargetSolver.Reset(ref _responsiveHandTargetState);\n"
        "            _handIkState = HandIkState.Inactive;",
    ),
)

for old, new in replacements:
    count = modified.count(old)
    if count != 1:
        raise RuntimeError(f"Expected one integration seam, found {count}: {old[:80]!r}")
    modified = modified.replace(old, new, 1)

from_name = f"a/{relative.as_posix()}"
to_name = f"b/{relative.as_posix()}"
lines = [f"diff --git {from_name} {to_name}\n"]
lines.extend(difflib.unified_diff(
    original.splitlines(keepends=True),
    modified.splitlines(keepends=True),
    fromfile=from_name,
    tofile=to_name,
    n=3,
))
(Path(__file__).with_name("HumanoidCharacterPresentation.integration.patch"))\
    .write_text("".join(lines), encoding="utf-8", newline="\n")
