using Elemental.Simulation.Bending;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthArmorController : MonoBehaviour
    {
        private const int MaximumPieces = EarthArmorProfile.MaximumPieceCount;
        private const int MaximumCasterColliders = 32;
        private const int MaximumBodyRenderers = 24;
        private const float GoldenAngle = 2.39996323f;
        private readonly EarthArmorPiece[] _pieces = new EarthArmorPiece[MaximumPieces];
        private readonly Collider[] _casterColliders = new Collider[MaximumCasterColliders];
        private readonly Collider[] _bodyColliders = new Collider[MaximumCasterColliders];
        private readonly Renderer[] _bodyRenderers = new Renderer[MaximumBodyRenderers];
        private readonly Material[][] _originalBodyMaterials = new Material[MaximumBodyRenderers][];
        private readonly Material[][] _stoneBodyMaterials = new Material[MaximumBodyRenderers][];
        private readonly BodySurfaceAnchor[] _bodyAnchors = new BodySurfaceAnchor[MaximumPieces];
        private readonly int[] _anchorColliderIndices = new int[MaximumPieces];
        private readonly int[] _colliderAssignmentCounts = new int[MaximumCasterColliders];
        private readonly int[] _colliderAssignmentRanks = new int[MaximumCasterColliders];
        private EarthArmorProfile _profile;
        private Rigidbody _caster;
        private ActiveRagdollPuppet _casterPuppet;
        private Transform _planetCenter;
        private GravityWorldBehaviour _gravityWorld;
        private EarthArmorSession _session;
        private float _elapsed;
        private uint _generation;
        private int _pieceCount;
        private int _casterColliderCount;
        private int _bodyColliderCount;
        private int _bodyRendererCount;
        private Material _armorMaterial;
        private bool _stoneSkinApplied;
        private EarthMatterKernelBehaviour _matterKernel;
        private Vector3 _aimDirection;
        private float _casterMomentumGuardUntil;
        private Vector3 _guardedCasterLinearVelocity;
        private Vector3 _guardedCasterAngularVelocity;

        private struct BodySurfaceAnchor
        {
            public Collider Collider;
            public Renderer Renderer;
            public Transform Bone;
            public Transform AxisTarget;
            public HumanBodyBones BoneId;
            public EarthArmorShellRegion Region;
            public Vector3 LocalDirection;
            public Vector3 PlateScale;
            public float SurfaceRadius;
        }

        public bool IsActive => _session != null && _session.Active;
        public float Phase01 => _session != null ? _session.Phase01 : 0f;
        public int OverscrollSteps => _session != null ? _session.OverscrollSteps : 0;
        public int ActivePieceCount => _pieceCount;
        public EarthMatterId PrimaryMatterId
        {
            get
            {
                for (int index = 0; index < _pieceCount; index++)
                {
                    EarthArmorPiece piece = _pieces[index];
                    EarthMatterIdentity identity = piece != null && piece.gameObject.activeSelf
                        ? piece.MatterIdentity
                        : null;
                    if (identity != null && identity.MatterId.IsValid) return identity.MatterId;
                }
                return default;
            }
        }
        public int ControllablePieceCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _pieceCount; index++)
                {
                    EarthArmorPiece piece = _pieces[index];
                    if (piece != null && piece.gameObject.activeSelf && !piece.IsReleased) count++;
                }
                return count;
            }
        }

        public void SetAimDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.01f) _aimDirection = direction.normalized;
        }

        public void Configure(
            Rigidbody caster,
            Transform planetCenter,
            EarthFragmentPool fragmentPool,
            EarthArmorProfile profile)
        {
            _caster = caster;
            _casterPuppet = caster != null ? caster.GetComponent<ActiveRagdollPuppet>() : null;
            _planetCenter = planetCenter;
            _gravityWorld = fragmentPool != null ? fragmentPool.GravityWorld : null;
            _profile = profile;
            _matterKernel = EarthMatterKernelBehaviour.FindOrCreate(this);
            EarthArmorProfileData data = profile != null ? profile.Data : EarthArmorProfileData.Default;
            _session = new EarthArmorSession(in data);
            CacheCasterColliders();
            EnsurePool(fragmentPool != null ? fragmentPool.SharedMaterial : null);
        }

        public bool Begin()
        {
            if (_caster == null || IsActive) return false;
            EnsurePool(null);
            _generation = _generation == uint.MaxValue ? 1u : _generation + 1u;
            // The production profile uses a denser 96-tile skin. Profile-less tests
            // and generic fallback actors retain the original 64-piece budget.
            int requestedPieces = _profile != null
                ? _profile.PieceCount
                : EarthArmorShellDefinition.RequiredSegmentCount;
            _pieceCount = Mathf.Clamp(requestedPieces, 40, MaximumPieces);
            CacheCasterColliders();
            BuildBodySurfaceAnchors();
            // Armor is an external shell around the hero, not a replacement avatar.
            // Keep both the animated character and its authored materials alive under
            // the stones. Recolouring the body made compact armor read as a material
            // swap instead of a physical shell and destroyed the character silhouette.
            SetBodyRendererVisibility(true);
            SetStoneSkin(false);
            Vector3 up = LocalUp;
            Vector3 forward = Vector3.ProjectOnPlane(_caster.transform.forward, up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.Cross(up, Vector3.right).normalized;
            _aimDirection = forward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 ground = _caster.position - up * 0.9f;
            for (int index = 0; index < _pieceCount; index++)
            {
                float angle = index * Mathf.PI * 2f / _pieceCount;
                Vector3 radial = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                Vector3 source = ground + radial * Mathf.Lerp(0.55f, 1.1f, Hash01(index)) - up * 0.22f;
                _pieces[index].Activate(_generation, source, Quaternion.LookRotation(radial, up));
                BodySurfaceAnchor bodyAnchor = _bodyAnchors[index];
                Vector3 bodyScale = bodyAnchor.PlateScale;
                if (bodyScale.sqrMagnitude > 0.001f)
                {
                    float silhouetteScale = (_profile != null ? _profile.BodyPlateScaleMultiplier : 0.91f) *
                                            AnchorCoverageMultiplier(bodyAnchor, index);
                    float thicknessScale = Mathf.Lerp(1f, silhouetteScale, 0.42f);
                    _pieces[index].SetBaseScale(new Vector3(
                        bodyScale.x * silhouetteScale,
                        bodyScale.y * thicknessScale,
                        bodyScale.z * silhouetteScale));
                }
                Vector3 sourceLocal = _planetCenter != null
                    ? _planetCenter.InverseTransformPoint(source)
                    : source;
                _pieces[index].RegisterMatter(
                    _matterKernel,
                    sourceLocal,
                    _generation,
                    new EarthOwnerId(1u, 1));
            }
            for (int index = _pieceCount; index < _pieces.Length; index++)
                _pieces[index]?.ResetToPool();
            _elapsed = 0f;
            _session.Begin();
            return true;
        }

        public EarthArmorInputResult ApplyWheel(float rawWheelDelta, float now)
        {
            if (!IsActive || Mathf.Abs(rawWheelDelta) < 0.001f) return EarthArmorInputResult.None;
            // Input System reports one ordinary wheel notch as roughly +/-120.
            float steps = Mathf.Abs(rawWheelDelta) >= 2f ? rawWheelDelta / 120f : rawWheelDelta;
            EarthArmorInputResult result = _session.ApplyWheelSteps(steps, now);
            if (steps < 0f && result == EarthArmorInputResult.PhaseChanged)
                RecallSurvivingReleasedPieces();
            return result;
        }

        public int RecallSurvivingReleasedPieces()
        {
            if (!IsActive) return 0;
            int recalled = 0;
            for (int index = 0; index < _pieceCount; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece != null && piece.TryBeginRecall()) recalled++;
            }
            return recalled;
        }

        public void ReleaseAsDebris()
        {
            if (_session == null) return;
            _session.End();
            SetStoneSkin(false);
            SetBodyRendererVisibility(true);
            Vector3 up = LocalUp;
            for (int index = 0; index < _pieceCount; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf || piece.IsReleased) continue;
                Vector3 radial = Vector3.ProjectOnPlane(piece.transform.position - _caster.worldCenterOfMass, up).normalized;
                piece.Release(radial * 0.8f - up * 0.6f, DebrisRestSeconds, DebrisShrinkSeconds);
            }
        }

        public void ReleaseRadially()
        {
            if (_session != null) _session.End();
            SetStoneSkin(false);
            SetBodyRendererVisibility(true);
            _casterPuppet?.SuppressImpacts(0.45f);
            ArmCasterMomentumGuard(0.16f);
            Vector3 center = _caster != null ? _caster.worldCenterOfMass : transform.position;
            float minimum = _profile != null ? _profile.MinimumBurstSpeed : 16f;
            float maximum = _profile != null ? _profile.MaximumBurstSpeed : 24f;
            for (int index = 0; index < _pieceCount; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf || piece.IsReleased) continue;
                Vector3 direction = (piece.transform.position - center).normalized;
                if (direction.sqrMagnitude < 0.5f) direction = Quaternion.AngleAxis(index * 137.5f, LocalUp) * _caster.transform.forward;
                float speed = Mathf.Lerp(minimum, maximum, Hash01(index + 91));
                piece.Release(direction * speed, DebrisRestSeconds, DebrisShrinkSeconds);
            }
        }

        public bool FireNearest(Vector3 aimDirection)
        {
            if (!IsActive || Phase01 <= 0.30f || _caster == null) return false;
            Vector3 aim = SafeDirection(aimDirection, _caster.transform.forward);
            Vector3 origin = _caster.worldCenterOfMass;
            int best = -1;
            float bestScore = float.NegativeInfinity;
            for (int index = 0; index < _pieceCount; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf || piece.IsReleased) continue;
                Vector3 toPiece = piece.transform.position - origin;
                float score = Vector3.Dot(toPiece.normalized, aim) - toPiece.sqrMagnitude * 0.0015f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = index;
            }
            if (best < 0) return false;
            _casterPuppet?.SuppressImpacts(0.35f);
            ArmCasterMomentumGuard(0.10f);
            float speed = _profile != null ? _profile.AimedProjectileSpeed : 31f;
            _pieces[best].Release(aim * speed, DebrisRestSeconds, DebrisShrinkSeconds);
            EndIfEmpty();
            return true;
        }

        public int FireAll(Vector3 aimDirection)
        {
            if (!IsActive || Phase01 <= 0.30f || _caster == null) return 0;
            Vector3 aim = SafeDirection(aimDirection, _caster.transform.forward);
            Vector3 up = LocalUp;
            Vector3 right = Vector3.Cross(up, aim).normalized;
            if (right.sqrMagnitude < 0.1f) right = Vector3.Cross(up, _caster.transform.forward).normalized;
            float baseSpeed = _profile != null ? _profile.AimedProjectileSpeed : 31f;
            int launched = 0;
            _casterPuppet?.SuppressImpacts(0.55f);
            ArmCasterMomentumGuard(0.14f);
            for (int index = 0; index < _pieceCount; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf || piece.IsReleased) continue;
                float signed = Mathf.Repeat((index + 0.5f) * 0.6180339f, 1f) * 2f - 1f;
                Vector3 direction = (aim + right * signed * 0.18f + up * (0.03f + Mathf.Abs(signed) * 0.05f)).normalized;
                piece.Release(direction * Mathf.Max(20f, baseSpeed - launched * 0.22f),
                    DebrisRestSeconds, DebrisShrinkSeconds);
                launched++;
            }
            if (launched > 0)
            {
                _session.End();
                SetStoneSkin(false);
                SetBodyRendererVisibility(true);
            }
            return launched;
        }

        private void ArmCasterMomentumGuard(float duration)
        {
            if (_caster == null || _caster.isKinematic) return;
            _guardedCasterLinearVelocity = _caster.linearVelocity;
            _guardedCasterAngularVelocity = _caster.angularVelocity;
            _casterMomentumGuardUntil = Mathf.Max(
                _casterMomentumGuardUntil,
                Time.fixedTime + Mathf.Max(Time.fixedDeltaTime, duration));
        }

        private void FixedUpdate()
        {
            if (_caster != null && !_caster.isKinematic && Time.fixedTime <= _casterMomentumGuardUntil)
            {
                // Armor projectiles ignore the caster colliders, but a release can
                // still perturb a compound/ragdoll body during the physics handoff.
                // Preserve only the momentum that existed before the spell for a
                // handful of fixed ticks; external physics resumes immediately after.
                _caster.linearVelocity = _guardedCasterLinearVelocity;
                _caster.angularVelocity = _guardedCasterAngularVelocity;
            }
            if (!IsActive || _caster == null) return;
            _elapsed += Time.fixedDeltaTime;
            Vector3 up = LocalUp;
            Vector3 forward = Vector3.ProjectOnPlane(_caster.transform.forward, up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.Cross(up, Vector3.right).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 center = _caster.worldCenterOfMass + up * 0.05f;
            Vector3 formationAim = Vector3.ProjectOnPlane(_aimDirection, up).normalized;
            if (formationAim.sqrMagnitude < 0.5f) formationAim = forward;
            float phase = Phase01;
            // Compact armor never replaces the hero's materials. The visible plates
            // provide the stone read while the character remains identifiable through
            // seams and through the camera visibility corridor.
            SetStoneSkin(false);
            float assembly = _profile != null ? _profile.AssemblySeconds : 0.30f;
            float expandedScale = _profile != null ? _profile.ExpandedPlateScaleMultiplier : 1.22f;
            for (int index = 0; index < _pieceCount; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf || piece.IsReleased) continue;
                float slot01 = (index + 0.5f) / _pieceCount;
                float orbitAngle = _elapsed * Mathf.Lerp(0f, 0.85f, Mathf.InverseLerp(0.78f, 1f, phase));
                float angle = index * GoldenAngle + orbitAngle;
                Vector3 radial = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                Vector3 target;
                Vector3 surfaceNormal;
                if (phase <= 0.30f)
                {
                    target = EvaluateBodySurfaceTarget(index, center, up, forward, out surfaceNormal);
                    piece.SetFormationScale(1f);
                }
                else if (phase <= 0.78f)
                {
                    float domeT = (phase - 0.30f) / 0.48f;
                    float domeRadius = Mathf.Lerp(
                        _profile != null ? _profile.BodyRadius : 0.78f,
                        _profile != null ? _profile.DomeRadius : 2.5f,
                        domeT);
                    Vector3 bodyTarget = EvaluateBodySurfaceTarget(index, center, up, forward, out Vector3 bodyNormal);
                    EarthArmorFormationSample dome = EarthArmorFormationSolver.DirectedDome(
                        index,
                        _pieceCount,
                        ToFloat3(formationAim),
                        ToFloat3(up),
                        _generation);
                    Vector3 hemisphereDirection = ToVector3(dome.Direction);
                    Vector3 domeTarget = center - up * 0.18f +
                                         hemisphereDirection * (domeRadius * dome.RadiusMultiplier);
                    float smoothDome = domeT * domeT * (3f - 2f * domeT);
                    target = Vector3.Lerp(bodyTarget, domeTarget, smoothDome);
                    Vector3 shieldNormal = Vector3.Slerp(hemisphereDirection, formationAim, 0.72f).normalized;
                    surfaceNormal = Vector3.Slerp(bodyNormal, shieldNormal, smoothDome).normalized;
                    piece.SetFormationScale(Mathf.Lerp(
                        1f,
                        expandedScale * dome.ScaleMultiplier,
                        smoothDome));
                }
                else
                {
                    float orbitT = (phase - 0.78f) / 0.22f;
                    float radius = Mathf.Lerp(
                        _profile != null ? _profile.DomeRadius : 2.5f,
                        _profile != null ? _profile.OrbitRadius : 3.2f,
                        orbitT);
                    EarthArmorFormationSample orbit = EarthArmorFormationSolver.BrokenOrbit(
                        index,
                        _pieceCount,
                        _elapsed,
                        ToFloat3(formationAim),
                        ToFloat3(up),
                        _generation);
                    surfaceNormal = ToVector3(orbit.Direction);
                    target = center - up * 0.20f + surfaceNormal * (radius * orbit.RadiusMultiplier);
                    piece.SetFormationScale(expandedScale * orbit.ScaleMultiplier);
                }
                // Ninety-six production plates still need to read as one decisive
                // spell, not a half-second queue with late stones hovering off-body.
                float gather01 = Mathf.Clamp01((_elapsed - index * 0.0015f) / Mathf.Max(0.05f, assembly));
                float eased = 1f - Mathf.Pow(1f - gather01, 3f);
                Vector3 next = Vector3.Lerp(piece.SourcePosition, target, eased);
                Vector3 tangent = EvaluateBodySurfaceTangent(index, surfaceNormal, up, forward, right);
                Quaternion rotation = Quaternion.LookRotation(tangent, surfaceNormal) *
                                      Quaternion.AngleAxis(Mathf.Sin(_elapsed * 1.7f + index) * 3.5f, Vector3.up);
                piece.Move(next, rotation);
            }
        }

        public int CopyActivePiecesNonAlloc(EarthArmorPiece[] destination)
        {
            if (destination == null) return 0;
            int count = 0;
            for (int index = 0; index < _pieceCount && count < destination.Length; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece != null && piece.gameObject.activeSelf) destination[count++] = piece;
            }
            return count;
        }

        internal void ReapplyCasterCollisionIgnores(Collider pieceCollider)
        {
            if (pieceCollider == null) return;
            for (int index = 0; index < _casterColliderCount; index++)
            {
                Collider casterCollider = _casterColliders[index];
                if (casterCollider != null)
                    UnityEngine.Physics.IgnoreCollision(pieceCollider, casterCollider, true);
            }
        }

        private Vector3 EvaluateBodySurfaceTarget(
            int pieceIndex,
            Vector3 fallbackCenter,
            Vector3 up,
            Vector3 forward,
            out Vector3 normal)
        {
            BodySurfaceAnchor anchor = _bodyAnchors[pieceIndex];
            float offset = _profile != null ? _profile.BodySurfaceOffset : 0.055f;
            if (anchor.Bone != null)
            {
                Vector3 boneCenter = anchor.Bone.position;
                if (anchor.Region == EarthArmorShellRegion.Head)
                {
                    // Humanoid Head bones usually sit near the skull base. Derive the
                    // cranium offset from the actual imported rig instead of carrying
                    // KayKit's oversized fixed shift into Mixamo characters.
                    float parentSpan = anchor.Bone.parent != null
                        ? Vector3.Distance(anchor.Bone.position, anchor.Bone.parent.position)
                        : 0.14f;
                    boneCenter += up * Mathf.Clamp(parentSpan * 0.58f, 0.075f, 0.16f);
                }
                if (IsLongLimb(anchor.BoneId) && anchor.AxisTarget != null)
                {
                    Vector3 boneAxis = anchor.AxisTarget.position - anchor.Bone.position;
                    boneCenter += boneAxis * 0.5f;
                    Vector3 axis = boneAxis.normalized;
                    Vector3 radialForward = Vector3.ProjectOnPlane(forward, axis).normalized;
                    if (radialForward.sqrMagnitude < 0.25f)
                        radialForward = Vector3.ProjectOnPlane(up, axis).normalized;
                    if (radialForward.sqrMagnitude < 0.25f)
                        radialForward = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
                    Vector3 radialRight = Vector3.Cross(radialForward, axis).normalized;
                    normal = (radialRight * anchor.LocalDirection.x +
                              axis * anchor.LocalDirection.y +
                              radialForward * anchor.LocalDirection.z).normalized;
                }
                else
                {
                    Vector3 characterRight = Vector3.Cross(up, forward).normalized;
                    normal = (characterRight * anchor.LocalDirection.x +
                              up * anchor.LocalDirection.y +
                              forward * anchor.LocalDirection.z).normalized;
                }
                if (normal.sqrMagnitude < 0.25f) normal = forward;
                float shellRadius = anchor.SurfaceRadius > 0f
                    ? anchor.SurfaceRadius
                    : Mathf.Max(anchor.PlateScale.x, anchor.PlateScale.z) * 0.31f;
                return boneCenter + normal *
                    (shellRadius + offset + anchor.PlateScale.y * 0.5f);
            }
            Renderer bodyRenderer = anchor.Renderer;
            if (bodyRenderer != null && bodyRenderer.enabled && bodyRenderer.gameObject.activeInHierarchy &&
                TryEvaluateRendererSurface(
                    bodyRenderer,
                    anchor.LocalDirection,
                    up,
                    forward,
                    out Vector3 renderedSurface,
                    out normal))
            {
                return renderedSurface + normal * (offset + anchor.PlateScale.y * 0.5f);
            }
            Collider bodyCollider = anchor.Collider;
            if (bodyCollider != null && bodyCollider.enabled && bodyCollider.gameObject.activeInHierarchy)
            {
                Vector3 direction = bodyCollider.transform.TransformDirection(anchor.LocalDirection).normalized;
                Bounds bounds = bodyCollider.bounds;
                Vector3 probe = bounds.center + direction * (bounds.extents.magnitude + 1f);
                Vector3 surface = bodyCollider.ClosestPoint(probe);
                normal = (surface - bounds.center).normalized;
                if (normal.sqrMagnitude < 0.25f) normal = direction;
                return surface + normal * (offset + anchor.PlateScale.y * 0.5f);
            }

            // Tests and minimal fallback characters may only have a root body. Keep the
            // fallback recognisably humanoid instead of reverting to a cylindrical ring.
            float slot = pieceIndex / (float)Mathf.Max(1, _pieceCount);
            float side = (pieceIndex & 1) == 0 ? -1f : 1f;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            if (slot < 0.22f)
            {
                normal = (forward + right * side * 0.35f).normalized;
                return fallbackCenter + up * 0.35f + normal * 0.46f;
            }
            if (slot < 0.38f)
            {
                normal = (forward + right * side * 0.45f + up * 0.2f).normalized;
                return fallbackCenter + up * 1.03f + normal * 0.34f;
            }
            if (slot < 0.58f)
            {
                normal = (right * side + forward * 0.22f).normalized;
                return fallbackCenter + right * side * 0.55f + up * 0.35f + normal * 0.13f;
            }
            normal = (right * side * 0.35f + forward).normalized;
            float legHeight = pieceIndex % 4 < 2 ? -0.25f : -0.78f;
            return fallbackCenter + right * side * 0.23f + up * legHeight + normal * 0.25f;
        }

        private Vector3 HemisphereDirection(
            int index,
            Vector3 right,
            Vector3 forward,
            Vector3 up,
            float azimuthOffset)
        {
            // Fibonacci hemisphere: every stone gets a distinct latitude as well as
            // longitude, so the expanded armor cannot collapse into a cylinder.
            float y = Mathf.Clamp01((index + 0.62f) / Mathf.Max(1f, _pieceCount));
            float horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = index * GoldenAngle + azimuthOffset;
            return (right * (Mathf.Cos(angle) * horizontal) +
                    forward * (Mathf.Sin(angle) * horizontal) + up * y).normalized;
        }

        private Vector3 EvaluateBodySurfaceTangent(
            int pieceIndex,
            Vector3 surfaceNormal,
            Vector3 up,
            Vector3 forward,
            Vector3 right)
        {
            BodySurfaceAnchor anchor = _bodyAnchors[pieceIndex];
            Vector3 tangent = Vector3.zero;
            if (anchor.Bone != null && anchor.AxisTarget != null)
                tangent = anchor.AxisTarget.position - anchor.Bone.position;
            if (tangent.sqrMagnitude < 0.0025f) tangent = up;
            tangent = Vector3.ProjectOnPlane(tangent, surfaceNormal).normalized;
            if (tangent.sqrMagnitude < 0.25f)
                tangent = Vector3.ProjectOnPlane(forward, surfaceNormal).normalized;
            if (tangent.sqrMagnitude < 0.25f)
                tangent = Vector3.Cross(surfaceNormal, right).normalized;
            return tangent;
        }

        private void CacheCasterColliders()
        {
            System.Array.Clear(_casterColliders, 0, _casterColliders.Length);
            System.Array.Clear(_bodyColliders, 0, _bodyColliders.Length);
            SetStoneSkin(false);
            System.Array.Clear(_bodyRenderers, 0, _bodyRenderers.Length);
            System.Array.Clear(_originalBodyMaterials, 0, _originalBodyMaterials.Length);
            System.Array.Clear(_stoneBodyMaterials, 0, _stoneBodyMaterials.Length);
            _casterColliderCount = 0;
            _bodyColliderCount = 0;
            _bodyRendererCount = 0;
            if (_caster == null) return;

            _casterPuppet = _caster.GetComponent<ActiveRagdollPuppet>();
            if (_casterPuppet != null)
                _casterColliderCount = _casterPuppet.CopySelfCollidersNonAlloc(_casterColliders);
            if (_casterColliderCount <= 0)
            {
                Collider[] discovered = _caster.GetComponentsInChildren<Collider>(false);
                _casterColliderCount = Mathf.Min(discovered.Length, _casterColliders.Length);
                for (int index = 0; index < _casterColliderCount; index++)
                    _casterColliders[index] = discovered[index];
            }

            Collider rootCollider = _caster.GetComponent<Collider>();
            bool hasDetailedBody = _casterColliderCount > 1;
            for (int index = 0; index < _casterColliderCount; index++)
            {
                Collider candidate = _casterColliders[index];
                if (candidate == null || candidate.isTrigger || (hasDetailedBody && candidate == rootCollider))
                    continue;
                _bodyColliders[_bodyColliderCount++] = candidate;
            }
            if (_bodyColliderCount == 0 && rootCollider != null)
                _bodyColliders[_bodyColliderCount++] = rootCollider;

            // Prefer the actual animated presentation geometry. The active ragdoll
            // colliders are deliberately offset from the Humanoid render skeleton;
            // using them made armor orbit an invisible puppet instead of tracing the
            // character the player sees. Requiring an Animator ancestor filters out
            // particles, world props and the hidden primitive fallback.
            Renderer[] discoveredRenderers = _caster.GetComponentsInChildren<Renderer>(false);
            // KayKit exposes the visible body as six rigid mesh renderers parented to
            // animated Humanoid bones. Prefer those exact surfaces over eyes, weapons,
            // VFX and the hidden fallback puppet. This makes the compact shell fit the
            // character that is actually rendered instead of an approximate skeleton.
            for (int pass = 0; pass < 2 && _bodyRendererCount == 0; pass++)
            {
                bool semanticOnly = pass == 0;
                for (int index = 0; index < discoveredRenderers.Length &&
                                    _bodyRendererCount < _bodyRenderers.Length; index++)
                {
                    Renderer candidate = discoveredRenderers[index];
                    if (!IsUsableBodyRenderer(candidate)) continue;
                    bool semanticBody = SemanticBodyPieceCount(candidate) > 0;
                    if (semanticOnly != semanticBody) continue;
                    _bodyRenderers[_bodyRendererCount++] = candidate;
                }
            }

            for (int pieceIndex = 0; pieceIndex < _pieces.Length; pieceIndex++)
                if (_pieces[pieceIndex] != null)
                    ReapplyCasterCollisionIgnores(_pieces[pieceIndex].PieceCollider);
        }

        private void BuildBodySurfaceAnchors()
        {
            // Split rigid-body presentations can be packed from their semantic
            // renderers. A normal Mixamo character is one SkinnedMeshRenderer,
            // however, so its whole-body AABB is not an anatomical surface. In that
            // case the baked Humanoid shell is authoritative and follows every limb.
            System.Array.Clear(_colliderAssignmentCounts, 0, _colliderAssignmentCounts.Length);
            System.Array.Clear(_colliderAssignmentRanks, 0, _colliderAssignmentRanks.Length);
            int surfaceCount = _bodyRendererCount > 0 ? _bodyRendererCount : _bodyColliderCount;
            if (surfaceCount <= 0)
            {
                for (int index = 0; index < _pieceCount; index++) _bodyAnchors[index] = default;
                return;
            }

            bool hasCompleteSemanticBody = _bodyRendererCount > 0;
            int semanticTotal = 0;
            for (int index = 0; index < _bodyRendererCount; index++)
            {
                int semanticCount = SemanticBodyPieceCount(_bodyRenderers[index]);
                if (semanticCount <= 0) hasCompleteSemanticBody = false;
                semanticTotal += semanticCount;
            }
            hasCompleteSemanticBody &= semanticTotal == _pieceCount;

            if (!hasCompleteSemanticBody && TryBuildHumanoidShellAnchors()) return;

            if (hasCompleteSemanticBody)
            {
                int pieceIndex = 0;
                for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
                {
                    int count = SemanticBodyPieceCount(_bodyRenderers[surfaceIndex]);
                    _colliderAssignmentCounts[surfaceIndex] = count;
                    for (int rank = 0; rank < count; rank++)
                        _anchorColliderIndices[pieceIndex++] = surfaceIndex;
                }
            }
            else
            {
                float totalWeight = 0f;
                for (int index = 0; index < surfaceCount; index++)
                    totalWeight += BodySurfaceWeight(index);
                for (int pieceIndex = 0; pieceIndex < _pieceCount; pieceIndex++)
                {
                    float target = totalWeight * ((pieceIndex + 0.5f) / _pieceCount);
                    float accumulated = 0f;
                    int selected = surfaceCount - 1;
                    for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
                    {
                        accumulated += BodySurfaceWeight(surfaceIndex);
                        if (target <= accumulated)
                        {
                            selected = surfaceIndex;
                            break;
                        }
                    }
                    _anchorColliderIndices[pieceIndex] = selected;
                    _colliderAssignmentCounts[selected]++;
                }
            }

            for (int pieceIndex = 0; pieceIndex < _pieceCount; pieceIndex++)
            {
                int colliderIndex = _anchorColliderIndices[pieceIndex];
                Collider bodyCollider = _bodyRendererCount > 0 ? null : _bodyColliders[colliderIndex];
                Renderer bodyRenderer = _bodyRendererCount > 0 ? _bodyRenderers[colliderIndex] : null;
                int rank = _colliderAssignmentRanks[colliderIndex]++;
                int count = Mathf.Max(1, _colliderAssignmentCounts[colliderIndex]);
                Vector3 localDirection;
                // Reserve exact crown/sole extrema before filling the rest with a
                // Fibonacci distribution. A single full-body SkinnedMesh otherwise
                // misses its full height by half a sample. Width/depth extrema do
                // not need reserved tiles and keeping them in the distribution
                // avoids stacking plates along the character's narrow front axis.
                if (count >= 2 && rank < 2)
                {
                    localDirection = rank == 0 ? Vector3.up : Vector3.down;
                }
                else
                {
                    int distributedRank = count >= 2 ? rank - 2 : rank;
                    int distributedCount = Mathf.Max(1, count >= 2 ? count - 2 : count);
                    float y = 1f - 2f * ((distributedRank + 0.5f) / distributedCount);
                    float planar = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    float angle = distributedRank * GoldenAngle + colliderIndex * 0.73f;
                    localDirection = new Vector3(
                        Mathf.Cos(angle) * planar,
                        y,
                        Mathf.Sin(angle) * planar).normalized;
                }
                EarthArmorShellRegion region = RendererRegion(bodyRenderer);
                // Preserve deliberate face/chest apertures. Central front samples
                // move toward alternating side seams instead of stacking opaque
                // plates directly over the character's identity landmarks.
                if ((region == EarthArmorShellRegion.Head || region == EarthArmorShellRegion.Torso) &&
                    localDirection.z > 0.24f && Mathf.Abs(localDirection.x) < 0.48f)
                {
                    float side = (rank & 1) == 0 ? -1f : 1f;
                    localDirection.x = side * Mathf.Lerp(0.48f, 0.66f,
                        Hash01(pieceIndex + 1181));
                    localDirection.z *= 0.78f;
                    localDirection.Normalize();
                }
                // Size plates from the surface area they actually cover. Fixed tiny
                // shards passed the proximity test but left the character visually
                // naked; fixed large shards made limbs read as a cylinder. A local
                // area budget produces a dense head/torso/limb silhouette while
                // keeping every stone independently faceted.
                // xy + yz + zx is only half the box surface represented by the
                // visible renderer bounds. Semantic KayKit body parts also lose
                // silhouette area to the deliberately chipped octagonal footprint,
                // so budget the real wrap area instead of leaving white islands
                // between otherwise well-placed tile centres.
                float surfaceAreaFactor = bodyRenderer != null &&
                                          SemanticBodyPieceCount(bodyRenderer) > 0
                    ? RegionSurfaceAreaFactor(region)
                    : 1f;
                float span = Mathf.Sqrt(BodySurfaceWeight(colliderIndex) * surfaceAreaFactor / count);
                // Preserve roughly the same covered area while changing silhouette
                // aggressively. This creates broad wedges beside narrow splinters
                // without inflating the whole shell or forcing neighbouring stones
                // through one another.
                float plateAspect = Mathf.Lerp(0.76f, 1.38f, Hash01(pieceIndex + 271));
                float aspectRoot = Mathf.Sqrt(plateAspect);
                float maximumSpan = RegionMaximumPlateSpan(region);
                float width = Mathf.Clamp(
                    span * aspectRoot * Mathf.Lerp(0.90f, 1.10f, Hash01(pieceIndex + 311)),
                    0.12f,
                    maximumSpan);
                float height = Mathf.Clamp(
                    span / aspectRoot * Mathf.Lerp(0.90f, 1.10f, Hash01(pieceIndex + 557)),
                    0.13f,
                    maximumSpan);
                float thickness = Mathf.Lerp(0.050f, 0.078f, Hash01(pieceIndex + 809));
                _bodyAnchors[pieceIndex] = new BodySurfaceAnchor
                {
                    Collider = bodyCollider,
                    Renderer = bodyRenderer,
                    Bone = null,
                    Region = region,
                    LocalDirection = localDirection,
                    PlateScale = new Vector3(width, thickness, height)
                };
            }
        }

        private bool TryBuildHumanoidShellAnchors()
        {
            Animator animator = _caster != null ? _caster.GetComponentInChildren<Animator>(false) : null;
            if (animator == null || !animator.isHuman) return false;
            int output = 0;
            EarthArmorShellDefinition definition = _profile != null ? _profile.ShellDefinition : null;
            EarthArmorShellSegment[] segments = definition != null && definition.IsValid
                ? definition.Segments
                : EarthArmorShellDefinition.CreateDefaultSegments();
            for (int index = 0; index < _pieceCount; index++)
            {
                int sourceIndex = _pieceCount == segments.Length
                    ? index
                    : Mathf.Clamp(Mathf.RoundToInt(index * (segments.Length - 1f) /
                                                   Mathf.Max(1f, _pieceCount - 1f)), 0, segments.Length - 1);
                EarthArmorShellSegment segment = segments[sourceIndex];
                Transform bone = ResolveBone(animator, segment.Bone);
                AddShellAnchor(
                    ref output,
                    bone,
                    ResolveAxisTarget(animator, segment.Bone),
                    segment.Bone,
                    segment.Region,
                    segment.CharacterDirection,
                    segment.Scale,
                    ResolveSurfaceRadius(animator, segment.Bone, segment.Region));
            }
            return output == _pieceCount;
        }

        private void AddShellAnchor(
            ref int output,
            Transform bone,
            Transform axisTarget,
            HumanBodyBones boneId,
            EarthArmorShellRegion region,
            Vector3 direction,
            Vector3 scale,
            float surfaceRadius = 0f)
        {
            if (output >= _bodyAnchors.Length) return;
            _bodyAnchors[output++] = new BodySurfaceAnchor
            {
                Bone = bone,
                AxisTarget = axisTarget,
                BoneId = boneId,
                Region = region,
                LocalDirection = direction.normalized,
                PlateScale = scale,
                SurfaceRadius = surfaceRadius
            };
        }

        private static Transform ResolveBone(Animator animator, HumanBodyBones bone)
        {
            Transform resolved = animator.GetBoneTransform(bone);
            if (resolved != null) return resolved;
            if (bone == HumanBodyBones.UpperChest)
                resolved = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (resolved == null && (bone == HumanBodyBones.UpperChest || bone == HumanBodyBones.Chest))
                resolved = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (resolved == null && (bone == HumanBodyBones.LeftToes || bone == HumanBodyBones.RightToes))
                resolved = animator.GetBoneTransform(
                    bone == HumanBodyBones.LeftToes ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            return resolved;
        }

        private static Transform ResolveAxisTarget(Animator animator, HumanBodyBones bone)
        {
            HumanBodyBones target = bone switch
            {
                HumanBodyBones.LeftUpperArm => HumanBodyBones.LeftLowerArm,
                HumanBodyBones.RightUpperArm => HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftLowerArm => HumanBodyBones.LeftHand,
                HumanBodyBones.RightLowerArm => HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg => HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.RightUpperLeg => HumanBodyBones.RightLowerLeg,
                HumanBodyBones.LeftLowerLeg => HumanBodyBones.LeftFoot,
                HumanBodyBones.RightLowerLeg => HumanBodyBones.RightFoot,
                HumanBodyBones.LeftFoot => HumanBodyBones.LeftToes,
                HumanBodyBones.RightFoot => HumanBodyBones.RightToes,
                _ => HumanBodyBones.LastBone
            };
            return target == HumanBodyBones.LastBone ? null : ResolveBone(animator, target);
        }

        private static bool IsLongLimb(HumanBodyBones bone) =>
            bone == HumanBodyBones.LeftUpperArm || bone == HumanBodyBones.RightUpperArm ||
            bone == HumanBodyBones.LeftLowerArm || bone == HumanBodyBones.RightLowerArm ||
            bone == HumanBodyBones.LeftUpperLeg || bone == HumanBodyBones.RightUpperLeg ||
            bone == HumanBodyBones.LeftLowerLeg || bone == HumanBodyBones.RightLowerLeg;

        private static float ResolveSurfaceRadius(
            Animator animator,
            HumanBodyBones bone,
            EarthArmorShellRegion region)
        {
            float humanScale = animator != null ? Mathf.Max(0.75f, animator.humanScale) : 1f;
            Transform source = animator != null ? ResolveBone(animator, bone) : null;
            Transform target = animator != null ? ResolveAxisTarget(animator, bone) : null;
            float segmentLength = source != null && target != null
                ? Vector3.Distance(source.position, target.position)
                : 0f;
            float measured = region switch
            {
                EarthArmorShellRegion.Head => ResolveHeadRadius(animator, humanScale),
                EarthArmorShellRegion.Torso => ResolveShoulderRadius(animator, humanScale) * 0.66f,
                EarthArmorShellRegion.Pelvis => ResolveHipRadius(animator, humanScale),
                EarthArmorShellRegion.Arm when segmentLength > 0f => segmentLength * 0.28f,
                EarthArmorShellRegion.Leg when segmentLength > 0f => segmentLength * 0.24f,
                EarthArmorShellRegion.Arm => humanScale * 0.075f,
                EarthArmorShellRegion.Leg => humanScale * 0.095f,
                _ => humanScale * 0.12f
            };
            return Mathf.Clamp(measured, humanScale * 0.055f, humanScale * 0.22f);
        }

        private static float ResolveHeadRadius(Animator animator, float humanScale)
        {
            if (animator == null) return humanScale * 0.13f;
            Transform head = ResolveBone(animator, HumanBodyBones.Head);
            Transform neck = ResolveBone(animator, HumanBodyBones.Neck);
            if (head == null || neck == null) return humanScale * 0.13f;
            return Mathf.Max(humanScale * 0.11f, Vector3.Distance(head.position, neck.position) * 0.82f);
        }

        private static float ResolveShoulderRadius(Animator animator, float humanScale)
        {
            if (animator == null) return humanScale * 0.20f;
            Transform left = ResolveBone(animator, HumanBodyBones.LeftUpperArm);
            Transform right = ResolveBone(animator, HumanBodyBones.RightUpperArm);
            if (left == null || right == null) return humanScale * 0.20f;
            return Vector3.Distance(left.position, right.position) * 0.5f;
        }

        private static float ResolveHipRadius(Animator animator, float humanScale)
        {
            if (animator == null) return humanScale * 0.13f;
            Transform left = ResolveBone(animator, HumanBodyBones.LeftUpperLeg);
            Transform right = ResolveBone(animator, HumanBodyBones.RightUpperLeg);
            if (left == null || right == null) return humanScale * 0.13f;
            return Mathf.Max(humanScale * 0.10f, Vector3.Distance(left.position, right.position) * 0.72f);
        }

        private void SetBodyRendererVisibility(bool visible)
        {
            for (int index = 0; index < _bodyRendererCount; index++)
            {
                Renderer renderer = _bodyRenderers[index];
                if (renderer != null) renderer.enabled = visible;
            }
        }

        private void SetStoneSkin(bool enabled)
        {
            if (enabled == _stoneSkinApplied) return;
            if (enabled && _armorMaterial == null) return;
            bool applied = false;
            for (int index = 0; index < _bodyRendererCount; index++)
            {
                Renderer renderer = _bodyRenderers[index];
                if (renderer == null) continue;
                if (enabled)
                {
                    if (_originalBodyMaterials[index] == null)
                        _originalBodyMaterials[index] = renderer.sharedMaterials;
                    if (_stoneBodyMaterials[index] == null ||
                        _stoneBodyMaterials[index].Length != _originalBodyMaterials[index].Length)
                    {
                        int materialCount = Mathf.Max(1, _originalBodyMaterials[index].Length);
                        _stoneBodyMaterials[index] = new Material[materialCount];
                        for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                            _stoneBodyMaterials[index][materialIndex] = _armorMaterial;
                    }
                    renderer.sharedMaterials = _stoneBodyMaterials[index];
                    applied = true;
                }
                else if (_originalBodyMaterials[index] != null)
                {
                    renderer.sharedMaterials = _originalBodyMaterials[index];
                }
            }
            _stoneSkinApplied = enabled && applied;
        }

        private float BodySurfaceWeight(int index)
        {
            if (_bodyRendererCount > 0)
            {
                Renderer renderer = index >= 0 && index < _bodyRendererCount ? _bodyRenderers[index] : null;
                if (renderer == null) return 0f;
                Vector3 size = renderer.bounds.size;
                return Mathf.Max(0.05f, size.x * size.y + size.y * size.z + size.z * size.x);
            }
            return index >= 0 && index < _bodyColliderCount
                ? ColliderSurfaceWeight(_bodyColliders[index])
                : 0f;
        }

        private static bool TryEvaluateRendererSurface(
            Renderer renderer,
            Vector3 localDirection,
            Vector3 characterUp,
            Vector3 characterForward,
            out Vector3 surface,
            out Vector3 normal)
        {
            surface = default;
            normal = Vector3.up;
            if (renderer == null) return false;
            // Renderer.bounds is the authoritative visible pose. KayKit's rigid FBX
            // parts are reparented under Humanoid bones at runtime while preserving
            // their authored mesh transform; evaluating raw Mesh.bounds after that
            // reparenting put the head shell roughly a metre above the rendered head.
            Bounds worldBounds = renderer.bounds;
            Vector3 up = characterUp.sqrMagnitude > 0.5f ? characterUp.normalized : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(characterForward, up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 authoredDirection = localDirection.sqrMagnitude > 0.001f
                ? localDirection.normalized
                : Vector3.forward;
            Vector3 direction = (right * authoredDirection.x +
                                 up * authoredDirection.y +
                                 forward * authoredDirection.z).normalized;
            Vector3 extents = worldBounds.extents;
            float denominator = Mathf.Sqrt(
                (direction.x * direction.x) / Mathf.Max(0.0001f, extents.x * extents.x) +
                (direction.y * direction.y) / Mathf.Max(0.0001f, extents.y * extents.y) +
                (direction.z * direction.z) / Mathf.Max(0.0001f, extents.z * extents.z));
            if (!float.IsFinite(denominator) || denominator <= 0.0001f) return false;
            Vector3 worldOffset = direction / denominator;
            surface = worldBounds.center + worldOffset;
            normal = new Vector3(
                worldOffset.x / Mathf.Max(0.0001f, extents.x * extents.x),
                worldOffset.y / Mathf.Max(0.0001f, extents.y * extents.y),
                worldOffset.z / Mathf.Max(0.0001f, extents.z * extents.z)).normalized;
            return float.IsFinite(surface.x) && float.IsFinite(surface.y) && float.IsFinite(surface.z) &&
                   normal.sqrMagnitude > 0.5f;
        }

        private static bool IsUsableBodyRenderer(Renderer candidate)
        {
            return candidate != null && candidate.enabled &&
                   candidate is not ParticleSystemRenderer && candidate is not LineRenderer &&
                   candidate.GetComponentInParent<Animator>() != null;
        }

        private static int SemanticBodyPieceCount(Renderer renderer)
        {
            if (renderer == null) return 0;
            string name = renderer.gameObject.name;
            // The production shell is a dense 96-tile stone skin. The deliberately
            // oversized chibi cranium receives a quarter of the entire budget;
            // arms and legs keep enough independent facets to follow animation
            // without exposing shoulders, hands, knees or feet.
            if (name.EndsWith("_Head", System.StringComparison.OrdinalIgnoreCase)) return 24;
            if (name.EndsWith("_Body", System.StringComparison.OrdinalIgnoreCase)) return 24;
            if (name.EndsWith("_ArmLeft", System.StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_ArmRight", System.StringComparison.OrdinalIgnoreCase)) return 12;
            if (name.EndsWith("_LegLeft", System.StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_LegRight", System.StringComparison.OrdinalIgnoreCase)) return 12;
            return 0;
        }

        private static float AnchorCoverageMultiplier(in BodySurfaceAnchor anchor, int pieceIndex)
        {
            if (anchor.Renderer == null) return RegionCoverageMultiplier(pieceIndex);
            // Generic one-piece test/fallback characters otherwise receive 64 tiles
            // sized from the same whole-body AABB and the chipped convex shoulders
            // overlap. Production semantic parts already have their own local area
            // budgets and keep the full edge-to-edge silhouette scale.
            if (SemanticBodyPieceCount(anchor.Renderer) <= 0) return 0.82f;
            bool readabilityRegion = anchor.Region == EarthArmorShellRegion.Head ||
                                     anchor.Region == EarthArmorShellRegion.Torso;
            return EarthCameraArmorVisibilitySolver.ResolveBodyCoverageScale(
                readabilityRegion,
                new Unity.Mathematics.float3(
                    anchor.LocalDirection.x,
                    anchor.LocalDirection.y,
                    anchor.LocalDirection.z));
        }

        private static EarthArmorShellRegion RendererRegion(Renderer renderer)
        {
            if (renderer == null) return EarthArmorShellRegion.Torso;
            string name = renderer.gameObject.name;
            if (name.EndsWith("_Head", System.StringComparison.OrdinalIgnoreCase))
                return EarthArmorShellRegion.Head;
            if (name.EndsWith("_Body", System.StringComparison.OrdinalIgnoreCase))
                return EarthArmorShellRegion.Torso;
            if (name.IndexOf("Arm", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return EarthArmorShellRegion.Arm;
            if (name.IndexOf("Leg", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return EarthArmorShellRegion.Leg;
            return EarthArmorShellRegion.Pelvis;
        }

        private static float RegionSurfaceAreaFactor(EarthArmorShellRegion region)
        {
            // Region-aware budgets prevent the oversized chibi head from inflating
            // every plate into an intersecting spike while preserving a dense torso.
            return region switch
            {
                EarthArmorShellRegion.Head => 0.88f,
                EarthArmorShellRegion.Torso => 1.32f,
                EarthArmorShellRegion.Pelvis => 1.32f,
                EarthArmorShellRegion.Arm => 1.16f,
                EarthArmorShellRegion.Leg => 1.16f,
                _ => 1.2f
            };
        }

        private static float RegionMaximumPlateSpan(EarthArmorShellRegion region)
        {
            return region switch
            {
                EarthArmorShellRegion.Head => 0.34f,
                EarthArmorShellRegion.Torso => 0.44f,
                EarthArmorShellRegion.Pelvis => 0.42f,
                EarthArmorShellRegion.Arm => 0.34f,
                EarthArmorShellRegion.Leg => 0.36f,
                _ => 0.40f
            };
        }

        private static float ColliderSurfaceWeight(Collider collider)
        {
            if (collider == null) return 0f;
            Vector3 size = collider.bounds.size;
            return Mathf.Max(0.05f, size.x * size.y + size.y * size.z + size.z * size.x);
        }

        private static float RegionCoverageMultiplier(int pieceIndex)
        {
            // The baked definition is ordered by anatomy. Keep every region close to
            // its authored size: global inflation was the cause of crossed shards.
            if (pieceIndex < 12) return 1.00f;   // head
            if (pieceIndex < 24) return 1.00f;   // torso
            if (pieceIndex < 30) return 0.99f;   // pelvis
            if (pieceIndex < 46) return 0.98f;   // arms
            return 0.99f;                        // legs and feet
        }

        private void EnsurePool(Material material)
        {
            if (material != null) _armorMaterial = material;
            if (_pieces[0] != null) return;
            for (int index = 0; index < MaximumPieces; index++)
            {
                GameObject go = new GameObject($"Earth Armor Piece {index + 1:00}");
                go.transform.SetParent(null, false);
                Mesh mesh = EarthArmorPlateMeshFactory.Create(index);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                MeshCollider collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;
                Rigidbody body = go.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                GravityBody gravity = go.AddComponent<GravityBody>();
                gravity.Configure(_gravityWorld, body);
                EarthArmorPiece piece = go.AddComponent<EarthArmorPiece>();
                piece.Configure(this, index, body, collider, mesh);
                ReapplyCasterCollisionIgnores(collider);
                go.SetActive(false);
                _pieces[index] = piece;
            }
        }

        private void OnDestroy()
        {
            SetStoneSkin(false);
            SetBodyRendererVisibility(true);
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthArmorPiece piece = _pieces[index];
                if (piece == null) continue;
                Mesh mesh = piece.OwnedMesh;
                if (mesh != null) Destroy(mesh);
                Destroy(piece.gameObject);
            }
        }

        private void OnDisable()
        {
            SetStoneSkin(false);
            SetBodyRendererVisibility(true);
        }

        private Vector3 LocalUp
        {
            get
            {
                Vector3 center = _planetCenter != null ? _planetCenter.position : Vector3.zero;
                Vector3 up = (_caster != null ? _caster.worldCenterOfMass : transform.position) - center;
                return up.sqrMagnitude > 0.01f ? up.normalized : transform.up;
            }
        }

        private float DebrisRestSeconds => _profile != null ? _profile.DebrisRestSeconds : 1.2f;
        private float DebrisShrinkSeconds => _profile != null ? _profile.DebrisShrinkSeconds : 1.1f;

        private void EndIfEmpty()
        {
            if (ControllablePieceCount > 0) return;
            _session?.End();
            SetStoneSkin(false);
            SetBodyRendererVisibility(true);
        }

        private static Vector3 SafeDirection(Vector3 value, Vector3 fallback) =>
            value.sqrMagnitude > 0.0001f ? value.normalized : fallback.normalized;

        private static Unity.Mathematics.float3 ToFloat3(Vector3 value) =>
            new Unity.Mathematics.float3(value.x, value.y, value.z);

        private static Vector3 ToVector3(Unity.Mathematics.float3 value) =>
            new Vector3(value.x, value.y, value.z);

        private static float Hash01(int value)
        {
            uint hash = unchecked((uint)(value + 1) * 0x9E3779B9u);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

}
