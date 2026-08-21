using System;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Runtime.Characters
{
    /// <summary>
    /// Installs the high-priority contact/impact/armor rescue components on any
    /// character created by the authoring scenes. The installer is deliberately
    /// idempotent and does not mutate prefabs or scene assets.
    /// </summary>
    internal static class EarthRuntimeRescueBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            EarthRuntimeRescueInstaller.Ensure();
        }
    }

    [DefaultExecutionOrder(1500)]
    internal sealed class EarthRuntimeRescueInstaller : MonoBehaviour
    {
        private static EarthRuntimeRescueInstaller _instance;
        private float _nextScanAt;

        public static void Ensure()
        {
            if (_instance != null) return;
            var host = new GameObject("Earth Runtime Rescue Installer")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<EarthRuntimeRescueInstaller>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Scan();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanAt) return;
            _nextScanAt = Time.unscaledTime + 1f;
            Scan();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Scan();

        private static void Scan()
        {
            PlanetMotor[] motors = FindObjectsByType<PlanetMotor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < motors.Length; index++)
            {
                PlanetMotor motor = motors[index];
                if (motor != null && motor.GetComponent<EarthCentralGroundingGuard>() == null)
                    motor.gameObject.AddComponent<EarthCentralGroundingGuard>();
            }

            ActiveRagdollPuppet[] puppets = FindObjectsByType<ActiveRagdollPuppet>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < puppets.Length; index++)
            {
                ActiveRagdollPuppet puppet = puppets[index];
                if (puppet != null && puppet.GetComponent<EarthHardLandingRagdollBridge>() == null)
                    puppet.gameObject.AddComponent<EarthHardLandingRagdollBridge>();
            }

            EarthArmorController[] armors = FindObjectsByType<EarthArmorController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < armors.Length; index++)
            {
                EarthArmorController armor = armors[index];
                if (armor != null && armor.GetComponent<EarthArmorBodyFollowRescue>() == null)
                    armor.gameObject.AddComponent<EarthArmorBodyFollowRescue>();
            }
        }
    }

    public static class EarthCentralSupportMath
    {
        public static float ProbeRadius(float capsuleRadius) =>
            math.clamp(math.max(0f, capsuleRadius) * 0.30f, 0.045f, 0.18f);

        public static float CenterToProbeBottom(float halfHeight, float probeRadius) =>
            math.max(0f, halfHeight - math.max(0.001f, probeRadius));

        public static bool IsWalkable(float3 normal, float3 up, float maximumSlopeDegrees)
        {
            float3 safeUp = math.normalizesafe(up, new float3(0f, 1f, 0f));
            float3 safeNormal = math.normalizesafe(normal, safeUp);
            float minimumDot = math.cos(math.radians(math.clamp(maximumSlopeDegrees, 1f, 89f)));
            return math.dot(safeNormal, safeUp) >= minimumDot;
        }
    }

    /// <summary>
    /// The motor's broad sphere cast is excellent at surviving seams, but a broad
    /// sphere can also bridge the rim of a real hole. This second, narrow probe is
    /// a support-integrity veto: when no material exists below the character core,
    /// the motor is released into a real fall instead of walking over empty space.
    /// </summary>
    [DefaultExecutionOrder(850)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlanetMotor), typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class EarthCentralGroundingGuard : MonoBehaviour
    {
        private const int HitCapacity = 12;
        private readonly RaycastHit[] _hits = new RaycastHit[HitCapacity];
        private PlanetMotor _motor;
        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private ActiveRagdollPuppet _puppet;
        private int _startupStableTicks;
        private bool _startupSettled;

        public bool HasCentralSupport { get; private set; }
        public float LastSupportDistance { get; private set; }

        private void Awake()
        {
            _motor = GetComponent<PlanetMotor>();
            _body = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _puppet = GetComponent<ActiveRagdollPuppet>();
        }

        private void OnEnable()
        {
            _startupStableTicks = 0;
            _startupSettled = false;
            HasCentralSupport = false;
            LastSupportDistance = float.PositiveInfinity;
        }

        private void FixedUpdate()
        {
            if (_motor == null || _body == null || _capsule == null || !_motor.enabled) return;

            // A registered moving support owns its support contract. The surf and
            // moving-platform solvers already supply contact-point velocity and a
            // generation-safe frame, so a world probe must not veto them.
            if (_motor.CurrentSupportFrame.IsValid)
            {
                HasCentralSupport = true;
                StabilizeStartup();
                return;
            }

            HasCentralSupport = ProbeCentralSupport(out float distance);
            LastSupportDistance = distance;
            if (_motor.IsGrounded && !HasCentralSupport)
            {
                _startupStableTicks = 0;
                _motor.BeginExternalLaunch(2);
                return;
            }

            if (HasCentralSupport && _motor.HasStableSupport) StabilizeStartup();
            else _startupStableTicks = 0;
        }

        private void StabilizeStartup()
        {
            if (_startupSettled) return;
            float2 move = _motor.LastCommand.Move;
            if (math.lengthsq(move) > 0.0025f)
            {
                _startupSettled = true;
                return;
            }

            _motor.SettleTangentialMotion();
            _startupStableTicks++;
            if (_startupStableTicks >= 10) _startupSettled = true;
        }

        private bool ProbeCentralSupport(out float supportDistance)
        {
            supportDistance = float.PositiveInfinity;
            Vector3 up = _motor.LocalUp.sqrMagnitude > 0.5f
                ? _motor.LocalUp.normalized
                : transform.up;
            Vector3 scale = transform.lossyScale;
            float capsuleRadius = _capsule.radius *
                               Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(
                capsuleRadius,
                _capsule.height * 0.5f * Mathf.Abs(scale.y));
            float probeRadius = EarthCentralSupportMath.ProbeRadius(capsuleRadius);
            float centerToBottom = EarthCentralSupportMath.CenterToProbeBottom(
                halfHeight, probeRadius);
            float probeAllowance = Mathf.Max(0.04f, _motor.GroundProbeDistance) + 0.055f;
            Vector3 origin = transform.TransformPoint(_capsule.center);
            int count = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                probeRadius,
                -up,
                _hits,
                centerToBottom + probeAllowance,
                _motor.GroundMask,
                QueryTriggerInteraction.Ignore);

            float best = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = _hits[index];
                if (hit.collider == null || hit.collider == _capsule ||
                    hit.rigidbody == _body || (_puppet != null && _puppet.OwnsCollider(hit.collider)))
                    continue;
                if (!EarthCentralSupportMath.IsWalkable(
                        new float3(hit.normal.x, hit.normal.y, hit.normal.z),
                        new float3(up.x, up.y, up.z),
                        _motor.MaximumSlopeAngle))
                    continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
            }

            if (!float.IsFinite(best)) return false;
            supportDistance = Mathf.Max(0f, best - centerToBottom);
            return supportDistance <= probeAllowance;
        }
    }

    public static class EarthHardLandingMath
    {
        public static float ImpactSeverity(float landingSpeed)
        {
            float speed = math.max(0f, landingSpeed);
            if (speed < 6.25f) return 0f;
            if (speed < 10.5f)
                return math.lerp(2.15f, 4.65f,
                    math.saturate((speed - 6.25f) / (10.5f - 6.25f)));
            return math.lerp(5.35f, 7.4f, math.saturate((speed - 10.5f) / 8f));
        }
    }

    /// <summary>
    /// Support contacts are intentionally filtered by ActiveRagdollPuppet so normal
    /// walking does not build impact debt. This bridge restores the missing semantic
    /// distinction between an ordinary step and a high-energy landing.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActiveRagdollPuppet), typeof(PlanetMotor), typeof(Rigidbody))]
    public sealed class EarthHardLandingRagdollBridge : MonoBehaviour
    {
        private ActiveRagdollPuppet _puppet;
        private PlanetMotor _motor;
        private Rigidbody _body;
        private bool _wasSupported;
        private float _minimumVerticalSpeed;
        private float _graceUntil;

        public float LastLandingSpeed { get; private set; }
        public float LastInjectedSeverity { get; private set; }

        private void Awake()
        {
            _puppet = GetComponent<ActiveRagdollPuppet>();
            _motor = GetComponent<PlanetMotor>();
            _body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            _wasSupported = _motor != null && _motor.HasStableSupport;
            _minimumVerticalSpeed = 0f;
            _graceUntil = Time.time + 0.9f;
        }

        private void FixedUpdate()
        {
            if (_puppet == null || _motor == null || _body == null) return;
            Vector3 up = _motor.LocalUp.sqrMagnitude > 0.5f
                ? _motor.LocalUp.normalized
                : transform.up;
            float supportUpSpeed = 0f;
            if (_motor.CurrentSupportFrame.IsValid)
            {
                float3 supportVelocity = _motor.CurrentSupportFrame.ContactPointVelocity;
                supportUpSpeed = Vector3.Dot(
                    new Vector3(supportVelocity.x, supportVelocity.y, supportVelocity.Z)", up);
            }
            float verticalSpeed = Vector3.Dot(_body.linearVelocity, up) - supportUpSpeed;
            bool supported = _motor.HasStableSupport;

            if (!supported)
            {
                _minimumVerticalSpeed = Mathf.Min(_minimumVerticalSpeed, verticalSpeed);
                _wasSupported = false;
                return;
            }

            if (!_wasSupported)
            {
                LastLandingSpeed = Mathf.Max(0f, -_minimumVerticalSpeed);
                LastInjectedSeverity = EarthHardLandingMath.ImpactSeverity(LastLandingSpeed);
                bool emergingSupport = _motor.CurrentSupportFrame.IsValid &&
                                       _motor.CurrentSupportFrame.Emerging;
                if (Time.time >= _graceUntil && !emergingSupport && LastInjectedSeverity > 0f)
                {
                    _puppet.InjectImpact(Mathf.Max(0.01f, _body.mass) * LastInjectedSeverity);
                }
            }

            _minimumVerticalSpeed = 0f;
            _wasSupported = true;
        }
    }

    /// <summary>
    /// Compact armor is a worn shell, not a flock. The normal armor controller uses
    /// compliant flight for assembly, dome and orbit phases. Once a plate reaches the
    /// body, this component captures a bone-local anchor and evaluates it on both the
    /// physics and render clocks, eliminating the visible one-tick tail behind the hero.
    /// </summary>
    [DefaultExecutionOrder(950)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EarthArmorController))]
    public sealed class EarthArmorBodyFollowRescue : MonoBehaviour
    {
        private const int MaximumPieces = EarthArmorProfile.MaximumPieceCount;
        private static readonly HumanBodyBones[] CandidateBones =
        {
            HumanBodyBones.Head,
            HumanBodyBones.Neck,
            HumanBodyBones.Chest,
            HumanBodyBones.Spine,
            HumanBodyBones.Hips,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot
        };

        private readonly EarthArmorPiece[] _pieces = new EarthArmorPiece[MaximumPieces];
        private readonly BoneAnchor[] _anchors = new BoneAnchor[MaximumPieces];
        private EarthArmorController _armor;
        private Animator _animator;
        private bool _wasActive;
        private float _compactElapsed;

        private struct BoneAnchor
        {
            public EarthArmorPiece Piece;
            public Transform Bone;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public bool Valid;
        }

        private void Awake()
        {
            _armor = GetComponent<EarthArmorController>();
            _animator = GetComponentInChildren<Animator>(true);
        }

        private void OnDisable() => ReleaseAnchors();

        private void FixedUpdate()
        {
            RefreshState();
            if (ShouldFollowCompactArmor()) ApplyAnchors();
        }

        private void LateUpdate()
        {
            RefreshState();
            if (!ShouldFollowCompactArmor()) return;
            CaptureReadyPieces();
            ApplyAnchors();
        }

        private void RefreshState()
        {
            if (_armor == null) return;
            bool active = _armor.IsActive;
            if (!active)
            {
                if (_wasActive) ReleaseAnchors();
                _wasActive = false;
                _compactElapsed = 0f;
                return;
            }

            if (!_wasActive)
            {
                _wasActive = true;
                _compactElapsed = 0f;
                ReleaseAnchors();
            }

            if (_armor.Phase01 <= 0.305f) _compactElapsed += Time.deltaTime;
            else
            {
                _compactElapsed = 0f;
                ReleaseAnchors();
            }
        }

        private bool ShouldFollowCompactArmor() =>
            _armor != null && _armor.IsActive && _armor.Phase01 <= 0.305f &&
            _compactElapsed >= 0.18f && _animator != null && _animator.isHuman;

        private void CaptureReadyPieces()
        {
            int count = _armor.CopyActivePiecesNonAlloc(_pieces);
            for (int index = 0; index < count; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece == null || piece.IsReleased || !piece.gameObject.activeInHierarchy) continue;
                int slot = FindAnchorSlot(piece);
                if (slot < 0 || _anchors[slot].Valid) continue;
                Transform bone = FindNearestBone(piece.transform.position);
                if (bone == null) continue;
                Vector3 boneOffset = piece.transform.position - bone.position;
                // Do not freeze a plate while it is still visibly flying toward the
                // body. Once it enters the attachment envelope, clamp any remaining
                // spring debt so compact armor sits on the body instead of behind it.
                if (boneOffset.sqrMagnitude > 0.62f * 0.62f) continue;
                Vector3 localPosition = bone.InverseTransformPoint(piece.transform.position);
                localPosition = Vector3.ClampMagnitude(localPosition, 0.42f);
                _anchors[slot] = new BoneAnchor
                {
                    Piece = piece,
                    Bone = bone,
                    LocalPosition = localPosition,
                    LocalRotation = Quaternion.Inverse(bone.rotation) * piece.transform.rotation,
                    Valid = true
                };
                if (piece.Body != null) piece.Body.interpolation = RigidbodyInterpolation.None;
            }
        }

        private int FindAnchorSlot(EarthArmorPiece piece)
        {
            for (int index = 0; index < _anchors.Length; index++)
            {
                if (_anchors[index].Valid && _anchors[index].Piece == piece) return index;
            }
            for (int index = 0; index < _anchors.Length; index++)
            {
                if (!_anchors[index].Valid) return index;
            }
            return -1;
        }

        private Transform FindNearestBone(Vector3 position)
        {
            Transform best = null;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < CandidateBones.Length; index++)
            {
                Transform bone = _animator.GetBoneTransform(CandidateBones[index]);
                if (bone == null) continue;
                float distance = (bone.position - position).sqrMagnitud;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = bone;
            }
            return best;
        }

        private void ApplyAnchors()
        {
            for (int index = 0; index < _anchors.Length; index++)
            {
                BoneAnchor anchor = _anchors[index];
                EarthArmorPiece piece = anchor.Piece;
                if (!anchor.Valid || piece == null || anchor.Bone == null || piece.IsReleased ||
                    !piece.gameObject.activeInHierarchy)
                    continue;
                Rigidbody body = piece.Body;
                if (body == null) continue;
                Vector3 targetPosition = anchor.Bone.TransformPoint(anchor.LocalPosition);
                Quaternion targetRotation = anchor.Bone.rotation * anchor.LocalRotation;
                body.position = targetPosition;
                body.rotation = targetRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void ReleaseAnchors()
        {
            for (int index = 0; index < _anchors.Length; index++)
            {
                EarthArmorPiece piece = _anchors[index].Piece;
                if (piece != null && piece.Body != null && !piece.IsReleased)
                    piece.Body.interpolation = RigidbodyInterpolation.Interpolate;
                _anchors[index] = default;
                _pieces[index] = null;
            }
        }
    }
}
