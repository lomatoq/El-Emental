using Elemental.Runtime.Physics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Runtime.Characters
{
    internal static class EarthArmorAttachmentV2Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EarthArmorAttachmentV2Installer.Ensure();
    }

    [DefaultExecutionOrder(1520)]
    internal sealed class EarthArmorAttachmentV2Installer : MonoBehaviour
    {
        private static EarthArmorAttachmentV2Installer _instance;
        private float _nextScanAt;

        public static void Ensure()
        {
            if (_instance != null) return;
            var host = new GameObject("Earth Armor Attachment V2 Installer")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<EarthArmorAttachmentV2Installer>();
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
            EarthArmorController[] controllers = FindObjectsByType<EarthArmorController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < controllers.Length; index++)
            {
                EarthArmorController controller = controllers[index];
                if (controller == null) continue;

                // The first rescue proved the bone-local approach, but it could
                // capture a plate while it was still metres away from its body slot.
                // Keep the component for rollback/evidence while giving V2 sole
                // ownership of compact body attachment.
                EarthArmorBodyFollowRescue legacy =
                    controller.GetComponent<EarthArmorBodyFollowRescue>();
                if (legacy != null) legacy.enabled = false;

                if (controller.GetComponent<EarthArmorAttachmentV2>() == null)
                    controller.gameObject.AddComponent<EarthArmorAttachmentV2>();
            }
        }
    }

    public static class EarthArmorAttachmentMath
    {
        public static float AttachmentRadius(HumanBodyBones bone) => bone switch
        {
            HumanBodyBones.Head => 0.27f,
            HumanBodyBones.Neck => 0.23f,
            HumanBodyBones.Chest => 0.34f,
            HumanBodyBones.Spine => 0.32f,
            HumanBodyBones.Hips => 0.33f,
            HumanBodyBones.LeftShoulder or HumanBodyBones.RightShoulder => 0.28f,
            HumanBodyBones.LeftUpperArm or HumanBodyBones.RightUpperArm => 0.23f,
            HumanBodyBones.LeftLowerArm or HumanBodyBones.RightLowerArm => 0.20f,
            HumanBodyBones.LeftHand or HumanBodyBones.RightHand => 0.16f,
            HumanBodyBones.LeftUpperLeg or HumanBodyBones.RightUpperLeg => 0.25f,
            HumanBodyBones.LeftLowerLeg or HumanBodyBones.RightLowerLeg => 0.21f,
            HumanBodyBones.LeftFoot or HumanBodyBones.RightFoot => 0.18f,
            _ => 0.28f
        };

        public static float CaptureEnvelope(HumanBodyBones bone) =>
            AttachmentRadius(bone) + 0.24f;

        public static Vector3 ClampLocalOffset(
            Vector3 localOffset,
            Vector3 fallbackDirection,
            float maximumRadius,
            float minimumRadius = 0.055f)
        {
            float maximum = Mathf.Max(minimumRadius, maximumRadius);
            float magnitude = localOffset.magnitude;
            Vector3 direction = magnitude > 0.0001f
                ? localOffset / magnitude
                : math.normalizesafe(
                    new float3(fallbackDirection.x, fallbackDirection.y, fallbackDirection.z),
                    new float3(0f, 0f, 1f));
            float radius = Mathf.Clamp(magnitude, minimumRadius, maximum);
            return new Vector3(direction.x, direction.y, direction.z) * radius;
        }
    }

    /// <summary>
    /// Exact compact-shell attachment. Assembly remains physical/compliant until a
    /// plate enters a tight bone envelope; from that moment the plate is evaluated
    /// directly from a bone-local anchor on both physics and render clocks. Dome,
    /// orbit and projectile phases immediately release the anchor back to the armor
    /// controller, so this never turns the whole armor system into fake parenting.
    /// </summary>
    [DefaultExecutionOrder(980)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EarthArmorController))]
    public sealed class EarthArmorAttachmentV2 : MonoBehaviour
    {
        private const int MaximumPieces = EarthArmorProfile.MaximumPieceCount;
        private const float CompactPhaseMaximum = 0.305f;
        private const float MinimumAssemblySeconds = 0.12f;
        private const float InitialAttachBlendSeconds = 0.065f;

        private static readonly BoneCandidate[] CandidateBones =
        {
            new BoneCandidate(HumanBodyBones.Head),
            new BoneCandidate(HumanBodyBones.Neck),
            new BoneCandidate(HumanBodyBones.Chest),
            new BoneCandidate(HumanBodyBones.Spine),
            new BoneCandidate(HumanBodyBones.Hips),
            new BoneCandidate(HumanBodyBones.LeftShoulder),
            new BoneCandidate(HumanBodyBones.RightShoulder),
            new BoneCandidate(HumanBodyBones.LeftUpperArm),
            new BoneCandidate(HumanBodyBones.RightUpperArm),
            new BoneCandidate(HumanBodyBones.LeftLowerArm),
            new BoneCandidate(HumanBodyBones.RightLowerArm),
            new BoneCandidate(HumanBodyBones.LeftHand),
            new BoneCandidate(HumanBodyBones.RightHand),
            new BoneCandidate(HumanBodyBones.LeftUpperLeg),
            new BoneCandidate(HumanBodyBones.RightUpperLeg),
            new BoneCandidate(HumanBodyBones.LeftLowerLeg),
            new BoneCandidate(HumanBodyBones.RightLowerLeg),
            new BoneCandidate(HumanBodyBones.LeftFoot),
            new BoneCandidate(HumanBodyBones.RightFoot)
        };

        private readonly EarthArmorPiece[] _pieces =
            new EarthArmorPiece[MaximumPieces];
        private readonly Attachment[] _attachments =
            new Attachment[MaximumPieces];

        private EarthArmorController _armor;
        private Animator _animator;
        private bool _wasCompact;
        private float _compactElapsed;

        private readonly struct BoneCandidate
        {
            public BoneCandidate(HumanBodyBones bone) => Bone = bone;
            public HumanBodyBones Bone { get; }
        }

        private struct Attachment
        {
            public EarthArmorPiece Piece;
            public Transform Bone;
            public HumanBodyBones BoneId;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 StartPosition;
            public Quaternion StartRotation;
            public float StartedAt;
            public bool Valid;
        }

        public int AttachedCount { get; private set; }

        private void Awake()
        {
            _armor = GetComponent<EarthArmorController>();
            _animator = GetComponentInChildren<Animator>(true);
            EarthArmorBodyFollowRescue legacy =
                GetComponent<EarthArmorBodyFollowRescue>();
            if (legacy != null) legacy.enabled = false;
        }

        private void OnEnable()
        {
            _compactElapsed = 0f;
            _wasCompact = false;
            AttachedCount = 0;
        }

        private void OnDisable() => ReleaseAll();

        private void FixedUpdate()
        {
            RefreshPhase(Time.fixedDeltaTime);
            if (!CanAttachCompact()) return;
            CaptureReadyPieces();
            ApplyAttachments(Time.fixedUnscaledTime);
        }

        private void LateUpdate()
        {
            RefreshPhase(Time.deltaTime);
            if (!CanAttachCompact()) return;
            CaptureReadyPieces();
            ApplyAttachments(Time.unscaledTime);
        }

        private void RefreshPhase(float deltaTime)
        {
            bool compact = _armor != null && _armor.IsActive &&
                           _armor.Phase01 <= CompactPhaseMaximum;
            if (!compact)
            {
                if (_wasCompact || AttachedCount > 0) ReleaseAll();
                _wasCompact = false;
                _compactElapsed = 0f;
                return;
            }

            if (!_wasCompact)
            {
                ReleaseAll();
                _compactElapsed = 0f;
                _wasCompact = true;
            }
            _compactElapsed += Mathf.Max(0f, deltaTime);
        }

        private bool CanAttachCompact() =>
            _wasCompact && _compactElapsed >= MinimumAssemblySeconds &&
            _animator != null && _animator.isHuman;

        private void CaptureReadyPieces()
        {
            int count = _armor.CopyActivePiecesNonAlloc(_pieces);
            for (int index = 0; index < count; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece == null || piece.IsReleased ||
                    !piece.gameObject.activeInHierarchy || piece.Body == null)
                    continue;

                int slot = FindAttachmentSlot(piece);
                if (slot < 0 || _attachments[slot].Valid) continue;
                if (!TryFindNearestBone(
                        piece.transform.position,
                        out Transform bone,
                        out HumanBodyBones boneId,
                        out float distance))
                    continue;
                if (distance > EarthArmorAttachmentMath.CaptureEnvelope(boneId))
                    continue;

                Vector3 outwardWorld = piece.transform.position - bone.position;
                Vector3 fallbackWorld = outwardWorld.sqrMagnitude > 0.0001f
                    ? outwardWorld.normalized
                    : (_animator.transform.position - bone.position).normalized;
                if (fallbackWorld.sqrMagnitude < 0.1f)
                    fallbackWorld = bone.forward;
                Vector3 local = bone.InverseTransformPoint(piece.transform.position);
                Vector3 fallbackLocal = bone.InverseTransformDirection(fallbackWorld);
                local = EarthArmorAttachmentMath.ClampLocalOffset(
                    local,
                    fallbackLocal,
                    EarthArmorAttachmentMath.AttachmentRadius(boneId));

                _attachments[slot] = new Attachment
                {
                    Piece = piece,
                    Bone = bone,
                    BoneId = boneId,
                    LocalPosition = local,
                    LocalRotation = Quaternion.Inverse(bone.rotation) *
                                    piece.transform.rotation,
                    StartPosition = piece.transform.position,
                    StartRotation = piece.transform.rotation,
                    StartedAt = Time.unscaledTime,
                    Valid = true
                };
                piece.Body.interpolation = RigidbodyInterpolation.None;
                AttachedCount++;
            }
        }

        private int FindAttachmentSlot(EarthArmorPiece piece)
        {
            for (int index = 0; index < _attachments.Length; index++)
            {
                if (_attachments[index].Valid &&
                    _attachments[index].Piece == piece)
                    return index;
            }
            for (int index = 0; index < _attachments.Length; index++)
            {
                if (!_attachments[index].Valid) return index;
            }
            return -1;
        }

        private bool TryFindNearestBone(
            Vector3 position,
            out Transform bone,
            out HumanBodyBones boneId,
            out float distance)
        {
            bone = null;
            boneId = HumanBodyBones.LastBone;
            float bestDistanceSq = float.PositiveInfinity;
            for (int index = 0; index < CandidateBones.Length; index++)
            {
                HumanBodyBones candidateId = CandidateBones[index].Bone;
                Transform candidate = _animator.GetBoneTransform(candidateId);
                if (candidate == null) continue;
                float candidateDistanceSq =
                    (candidate.position - position).sqrMagnitude;
                if (candidateDistanceSq >= bestDistanceSq) continue;
                bestDistanceSq = candidateDistanceSq;
                bone = candidate;
                boneId = candidateId;
            }
            distance = Mathf.Sqrt(bestDistanceSq);
            return bone != null && float.IsFinite(distance);
        }

        private void ApplyAttachments(float now)
        {
            for (int index = 0; index < _attachments.Length; index++)
            {
                Attachment attachment = _attachments[index];
                EarthArmorPiece piece = attachment.Piece;
                if (!attachment.Valid || piece == null ||
                    attachment.Bone == null || piece.Body == null ||
                    piece.IsReleased || !piece.gameObject.activeInHierarchy)
                {
                    if (attachment.Valid) ReleaseAt(index);
                    continue;
                }

                Vector3 targetPosition =
                    attachment.Bone.TransformPoint(attachment.LocalPosition);
                Quaternion targetRotation = attachment.Bone.rotation *
                                            attachment.LocalRotation;
                float blend01 = InitialAttachBlendSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01((now - attachment.StartedAt) /
                                    InitialAttachBlendSeconds);
                blend01 = 1f - (1f - blend01) * (1f - blend01);
                Vector3 position = Vector3.LerpUnclamped(
                    attachment.StartPosition, targetPosition, blend01);
                Quaternion rotation = Quaternion.SlerpUnclamped(
                    attachment.StartRotation, targetRotation, blend01);

                Rigidbody body = piece.Body;
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                piece.transform.SetPositionAndRotation(position, rotation);
            }
        }

        private void ReleaseAt(int index)
        {
            Attachment attachment = _attachments[index];
            EarthArmorPiece piece = attachment.Piece;
            if (piece != null && piece.Body != null && !piece.IsReleased)
                piece.Body.interpolation = RigidbodyInterpolation.Interpolate;
            if (attachment.Valid) AttachedCount = Mathf.Max(0, AttachedCount - 1);
            _attachments[index] = default;
            _pieces[index] = null;
        }

        private void ReleaseAll()
        {
            for (int index = 0; index < _attachments.Length; index++)
                ReleaseAt(index);
            AttachedCount = 0;
        }
    }
}
