using System;
using System.Collections.Generic;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class EarthWall : MonoBehaviour, IEarthPhysicalTarget, IEarthReassemblableStructure, IEarthDamageableStructure, IEarthPluckableStructure
    {
        private static readonly ProfilerMarker EmergenceMarker =
            new ProfilerMarker("Elemental.Earth.Wall.Emergence");
        private static readonly ProfilerMarker ColliderValidationMarker =
            new ProfilerMarker("Elemental.Earth.Wall.ColliderValidation");
        private const int ColliderValidationCapacity = 32;
        private const float MaximumSafePenetration = 0.012f;

        private BoxCollider _collider;
        private MeshRenderer _renderer;
        private Transform _visualEmergenceRoot;
        private Rigidbody _body;
        private EarthWallProfile _profile;
        private EarthCohesiveStructure _cohesion;
        private EarthStructureRuntime _structureRuntime;
        private EarthStructureProxySwitcher _proxySwitcher;
        private EarthReassemblyController _reassembly;
        private Transform[] _pieces;
        private IEarthPhysicalTarget[] _pieceTargets;
        private Rigidbody[] _pieceBodies;
        private Vector3[] _pieceBasePositions;
        private Vector3[] _pieceFractureScales;
        private float[] _pieceVolumeFractions;
        private float[] _pieceDetachedAt;
        private bool[] _pieceAnchored;
        private bool[] _pieceShrinking;
        private int[] _connectivityQueue;
        private EarthWallBond[] _bonds;
        private float[] _bondStrength;
        private float[] _bondDamage;
        private bool[] _bondBroken;
        private Vector3 _surfacePosition;
        private Vector3 _buriedPosition;
        private Vector3 _embeddedStart;
        private Vector3 _embeddedEnd;
        private Vector3 _finalScale;
        private Vector3 _tangent;
        private Vector3 _up;
        private Vector3 _forward;
        private Quaternion _surfaceRotation;
        private float _emergenceDuration;
        private float _emergence;
        private float _stableElapsed;
        private float _fractureElapsed;
        private bool _fractured;
        private bool _manualFracturePaused;
        private float _gestureDisassemblyProgress;
        private Vector3 _fractureOrigin;
        private Vector3 _fractureBias;
        private bool _magicFieldActive;
        private float _planetRadius;
        private Vector3 _planetCenter;
        private float _surfaceRootRadius;
        private float _foundationEmbed;
        private float _colliderBottomInset;
        private bool _pendingColliderActivation;
        private Vector3 _emergenceRootPosition;
        private readonly Collider[] _colliderValidationHits = new Collider[ColliderValidationCapacity];
        private readonly List<Collider> _magicCasterColliders = new List<Collider>(12);
        private float _restoreCasterCollisionsAt = -1f;
        private uint _generation;
        private EarthMatterRecord[] _matterChildren;
        private EarthMatterId[] _matterChildIds;
        private EarthMatterId[] _matterMergeIds;
        private EarthMatterId _matterConsumedRoot;
        private ConstructionOrientationMode _orientationMode;

        public event Action<EarthWall> Collapsed;
        public event Action<IEarthFractureSource> TargetsActivated;

        public uint WallId { get; private set; }
        public uint Generation => _generation;
        public Vector3 Start { get; private set; }
        public Vector3 End { get; private set; }
        public float Height { get; private set; }
        public float Thickness { get; private set; }
        public bool IsCollapsing => _fractured;
        public bool IsCohesiveFractureActive => _fractured && RemainingBondCount > 0;
        public uint SourceTick { get; private set; }
        public float PeakEmergenceTremorMeters { get; private set; }
        public float PeakRootEmergenceDisplacementMeters { get; private set; }
        public bool IsEmergenceComplete => _emergence >= 1f && !_pendingColliderActivation;
        public Transform VisualEmergenceRoot => _visualEmergenceRoot;
        public int RemainingBondCount
        {
            get
            {
                if (_structureRuntime != null && _structureRuntime.IsConfigured)
                    return _structureRuntime.RemainingBondCount;
                if (_bondBroken == null) return 0;
                int count = 0;
                for (int index = 0; index < _bondBroken.Length; index++)
                    if (!_bondBroken[index]) count++;
                return count;
            }
        }
        public int ActiveFracturePieceCount
        {
            get
            {
                int count = 0;
                if (_pieces == null) return count;
                for (int index = 0; index < _pieces.Length; index++)
                    if (_pieces[index] != null && _pieces[index].gameObject.activeSelf) count++;
                return count;
            }
        }
        public Transform FirstFracturePiece => _pieces != null && _pieces.Length > 0 ? _pieces[0] : null;
        public float EstimatedMass => Mathf.Max(1f, _finalScale.x * _finalScale.y * _finalScale.z * 170f);
        public Rigidbody Body => _body;
        public Vector3 SurfaceUp => _up;
        public Collider SurfaceCollider => _collider;
        public bool IsSurfaceAvailable => gameObject.activeInHierarchy && !_fractured &&
                                          _collider != null && _collider.enabled;
        public uint StableEarthId => WallId;
        public EarthPhysicalTargetHandle TargetHandle => new EarthPhysicalTargetHandle(WallId, _generation);
        public float EarthMass => EstimatedMass;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.Wall;
        public bool IsEarthTargetValid => gameObject.activeSelf && !_fractured && _body != null;
        public uint StructureId => WallId;
        public bool UsesBakedFracture => _structureRuntime != null && _structureRuntime.IsConfigured;
        public EarthStructureRuntime StructureRuntime => _structureRuntime;
        public EarthReassemblyController Reassembly => _reassembly;
        public IEarthRepairController RepairController => _reassembly;
        public ConstructionOrientationMode OrientationMode => _orientationMode;
        bool IEarthFractureSource.IsFractured => _fractured;

        public int CopyActiveTargetsNonAlloc(IEarthPhysicalTarget[] destination)
        {
            if (destination == null || _pieces == null || !_fractured) return 0;
            int output = 0;
            for (int index = 0; index < _pieces.Length && output < destination.Length; index++)
            {
                Transform piece = _pieces[index];
                if (piece == null || !piece.gameObject.activeSelf) continue;
                IEarthPhysicalTarget target = _pieceTargets != null && index < _pieceTargets.Length
                    ? _pieceTargets[index]
                    : piece.GetComponent<EarthPieceRuntime>();
                if (target == null || !target.IsEarthTargetValid) continue;
                destination[output++] = target;
            }
            return output;
        }

        public bool IsPieceStructurallySupported(int pieceIndex) =>
            _pieceAnchored != null && pieceIndex >= 0 && pieceIndex < _pieceAnchored.Length &&
            _pieceAnchored[pieceIndex];

        public void ConfigureProfile(EarthWallProfile profile) => _profile = profile;

        public void ConfigureCollapsePieces(
            Transform[] pieces,
            float[] volumeFractions,
            EarthWallBond[] bonds)
        {
            _pieces = pieces;
            _bonds = bonds ?? Array.Empty<EarthWallBond>();
            int pieceCount = pieces?.Length ?? 0;
            _pieceBodies = new Rigidbody[pieceCount];
            _pieceTargets = new IEarthPhysicalTarget[pieceCount];
            _pieceBasePositions = new Vector3[pieceCount];
            _pieceFractureScales = new Vector3[pieceCount];
            _pieceVolumeFractions = new float[pieceCount];
            _pieceDetachedAt = new float[pieceCount];
            _pieceAnchored = new bool[pieceCount];
            _pieceShrinking = new bool[pieceCount];
            _connectivityQueue = new int[pieceCount];
            _bondStrength = new float[_bonds.Length];
            _bondDamage = new float[_bonds.Length];
            _bondBroken = new bool[_bonds.Length];
            if (_cohesion == null) _cohesion = GetComponent<EarthCohesiveStructure>();
            if (_cohesion == null) _cohesion = gameObject.AddComponent<EarthCohesiveStructure>();
            _cohesion.Configure(pieceCount);
            for (int index = 0; index < pieceCount; index++)
            {
                if (pieces[index] == null) continue;
                _pieceBasePositions[index] = pieces[index].localPosition;
                _pieceBodies[index] = pieces[index].GetComponent<Rigidbody>();
                _pieceVolumeFractions[index] = volumeFractions != null && index < volumeFractions.Length
                    ? Mathf.Max(0.0001f, volumeFractions[index])
                    : 1f / Mathf.Max(1, pieceCount);
                EarthWallPiece wallPiece = pieces[index].GetComponent<EarthWallPiece>();
                if (wallPiece == null) wallPiece = pieces[index].gameObject.AddComponent<EarthWallPiece>();
                wallPiece.Configure(this, index);
                _pieceTargets[index] = wallPiece;
            }
        }

        public bool ConfigureBakedRuntime(
            IEarthFractureAssetRuntimeData asset,
            EarthRepairProfile repairProfile = null)
        {
            if (asset == null || _pieces == null || _bonds == null) return false;
            ResolveReferences();
            if (_structureRuntime == null) _structureRuntime = GetComponent<EarthStructureRuntime>();
            if (_structureRuntime == null) _structureRuntime = gameObject.AddComponent<EarthStructureRuntime>();
            if (_proxySwitcher == null) _proxySwitcher = GetComponent<EarthStructureProxySwitcher>();
            if (_proxySwitcher == null) _proxySwitcher = gameObject.AddComponent<EarthStructureProxySwitcher>();
            if (!_structureRuntime.Configure(asset, this, _pieces, _bonds)) return false;
            _proxySwitcher.Configure(_renderer, _collider, _pieces);
            if (_reassembly == null) _reassembly = GetComponent<EarthReassemblyController>();
            if (_reassembly == null) _reassembly = gameObject.AddComponent<EarthReassemblyController>();
            _reassembly.Configure(_structureRuntime, this, repairProfile);
            for (int index = 0; index < _pieces.Length; index++)
                _pieceTargets[index] = _pieces[index].GetComponent<EarthPieceRuntime>();
            return true;
        }

        public void Initialize(
            uint id,
            Vector3 start,
            Vector3 end,
            Vector3 planetCenter,
            float height,
            float thickness,
            uint sourceTick = 0u,
            Vector3 supportNormal = default)
        {
            ResolveReferences();
            _reassembly?.ResetRepairCollisionPolicy();
            WallId = id;
            _generation = _generation == uint.MaxValue ? 1u : _generation + 1u;
            Start = start;
            End = end;
            Height = Mathf.Max(0.1f, height);
            Thickness = Mathf.Max(0.05f, thickness);
            SourceTick = sourceTick;
            PeakEmergenceTremorMeters = 0f;

            Vector3 midpoint = (start + end) * 0.5f;
            Vector3 chord = end - start;
            _planetCenter = planetCenter;
            _tangent = chord.normalized;
            if (_tangent.sqrMagnitude < 0.5f) _tangent = Vector3.right;
            Vector3 radial = midpoint - planetCenter;
            _planetRadius = Mathf.Max(1f,
                Mathf.Min(Vector3.Distance(start, planetCenter), Vector3.Distance(end, planetCenter)));
            bool attachedToPlanarSurface = supportNormal.sqrMagnitude > 0.5f;
            _orientationMode = attachedToPlanarSurface
                ? ConstructionOrientationMode.PreserveAuthoredFrame
                : ConstructionOrientationMode.FollowPlanetGravity;
            _up = attachedToPlanarSurface
                ? Vector3.ProjectOnPlane(supportNormal, _tangent).normalized
                : Vector3.ProjectOnPlane(radial, _tangent).normalized;
            if (_up.sqrMagnitude < 0.5f) _up = Vector3.up;
            _forward = Vector3.Cross(_tangent, _up).normalized;
            // The exact sphere solve prevents the chord ends from floating. The
            // additional visual margin covers the authored SDF noise and marching-
            // cubes interpolation, which can sit outside the collision proxy.
            float clearance = MinimumChordEmbedDepth + SurfaceTolerance + VisibleVoxelSafetyDepth;
            float embed = attachedToPlanarSurface
                ? Mathf.Max(MinimumChordEmbedDepth, Thickness * 0.32f)
                : RequiredCornerSafeEmbed(
                    midpoint,
                    planetCenter,
                    _tangent,
                    _up,
                    _forward,
                    chord.magnitude * 0.5f,
                    Thickness * 0.5f,
                    Mathf.Max(0.1f, _planetRadius - clearance),
                    clearance);
            _foundationEmbed = embed;
            Vector3 embeddedChordCenter = midpoint - (_up * embed);
            _embeddedStart = embeddedChordCenter - (_tangent * chord.magnitude * 0.5f);
            _embeddedEnd = embeddedChordCenter + (_tangent * chord.magnitude * 0.5f);
            _surfacePosition = embeddedChordCenter + (_up * Height * 0.5f);
            _surfaceRootRadius = Vector3.Distance(_surfacePosition, _planetCenter);
            _buriedPosition = _surfacePosition - (_up * Height * 0.92f);
            _finalScale = new Vector3(Mathf.Max(0.25f, Vector3.Distance(start, end)), Height, Thickness);
            _surfaceRotation = Quaternion.LookRotation(_forward, _up);
            _emergenceDuration = Mathf.Lerp(
                MinimumEmergenceSeconds,
                MaximumEmergenceSeconds,
                Mathf.InverseLerp(1.25f, 10.5f, Height));
            _emergence = 0f;
            _stableElapsed = 0f;
            _fractureElapsed = 0f;
            _fractured = false;
            _manualFracturePaused = false;
            _gestureDisassemblyProgress = 0f;
            _fractureOrigin = _surfacePosition;
            _fractureBias = _forward;
            SetMagicCasterCollisionIgnored(false);
            _magicFieldActive = false;
            _pendingColliderActivation = false;
            _cohesion?.ResetCohesion();
            _structureRuntime?.ResetExact(new EarthStructureId(id), _generation, sourceTick);

            _body.useGravity = false;
            _body.constraints = RigidbodyConstraints.FreezeRotation;
            _body.mass = EstimatedMass;
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            _body.isKinematic = true;
            transform.SetPositionAndRotation(_surfacePosition, _surfaceRotation);
            transform.localScale = _finalScale;
            _emergenceRootPosition = transform.position;
            PeakRootEmergenceDisplacementMeters = 0f;
            ConfigureColliderFoundationInset(_foundationEmbed + SurfaceTolerance);
            _collider.enabled = false;
            _renderer.enabled = true;
            ResetVisualEmergencePose();
            _proxySwitcher?.ShowIntact(false);
            HideFracturePieces();
            gameObject.SetActive(true);
        }

        public float ApplyMagicPush(Vector3 direction, float impulse)
        {
            if (impulse <= 0f) return 0f;
            Vector3 tangentDirection = Vector3.ProjectOnPlane(direction, _up).normalized;
            if (tangentDirection.sqrMagnitude < 0.5f) tangentDirection = _forward;
            _fractureBias = tangentDirection;
            // A structure is partly embedded and pays much more contact friction than
            // a free rock. Compensate that predictable grounding loss here so a tap is
            // visibly useful; the separate flick path still owns projectile speeds.
            float groundedImpulse = impulse * 2.15f;
            float velocityChange = groundedImpulse / EstimatedMass;
            if (!_fractured)
            {
                ActivateDynamicPhysics();
                if (_body == null || _body.isKinematic) return 0f;
                Vector3 before = _body.linearVelocity;
                Vector3 radial = Vector3.Project(before, _up);
                Vector3 tangent = Vector3.ProjectOnPlane(before, _up) + tangentDirection * velocityChange;
                _body.linearVelocity = Vector3.ClampMagnitude(tangent, MaximumSlideSpeed) + radial;
                _body.WakeUp();
                velocityChange = (_body.linearVelocity - before).magnitude;
            }
            else
            {
                ApplyDistributedImpulse(tangentDirection, groundedImpulse);
                DamageBonds(_fractureOrigin, tangentDirection, groundedImpulse * 0.38f);
            }
            return velocityChange;
        }

        public float ApplyMagicLaunchVelocity(Vector3 direction, float targetSpeed)
        {
            if (_fractured || targetSpeed <= 0f) return 0f;
            Vector3 tangentDirection = Vector3.ProjectOnPlane(direction, _up).normalized;
            if (tangentDirection.sqrMagnitude < 0.5f) tangentDirection = _forward;
            ActivateDynamicPhysics();
            if (_body == null || _body.isKinematic) return 0f;
            Vector3 previous = _body.linearVelocity;
            float radialSpeed = Mathf.Clamp(Vector3.Dot(previous, _up), -1.5f, 1.5f);
            _body.linearVelocity = tangentDirection * Mathf.Clamp(targetSpeed, 0f, 14f) +
                                   _up * radialSpeed;
            _body.angularVelocity = Vector3.zero;
            _body.WakeUp();
            return (_body.linearVelocity - previous).magnitude;
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            _magicFieldActive = grip == EarthMagicGripKind.VectorField;
            if (_magicFieldActive && !_fractured)
            {
                ActivateDynamicPhysics();
                _restoreCasterCollisionsAt = -1f;
                SetMagicCasterCollisionIgnored(true);
            }
            if (_body != null && !_body.isKinematic) _body.WakeUp();
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            if (grip != EarthMagicGripKind.VectorField) return;
            _magicFieldActive = false;
            // Keep the caster isolated for a short release window. Re-enabling a
            // wall collider on the same fixed step as the launch turns overlap
            // depenetration into a much larger impulse on the player than the spell.
            _restoreCasterCollisionsAt = Time.time + 0.35f;
        }

        private void SetMagicCasterCollisionIgnored(bool ignored)
        {
            if (!ignored)
            {
                _restoreCasterCollisionsAt = -1f;
                if (_collider != null)
                {
                    for (int index = 0; index < _magicCasterColliders.Count; index++)
                    {
                        Collider casterCollider = _magicCasterColliders[index];
                        if (casterCollider != null)
                            UnityEngine.Physics.IgnoreCollision(_collider, casterCollider, false);
                    }
                }
                _magicCasterColliders.Clear();
                return;
            }

            SetMagicCasterCollisionIgnored(false);
            GameObject caster = GameObject.FindGameObjectWithTag("Player");
            if (caster == null || _collider == null) return;
            caster.GetComponentsInChildren(true, _magicCasterColliders);
            for (int index = _magicCasterColliders.Count - 1; index >= 0; index--)
            {
                Collider casterCollider = _magicCasterColliders[index];
                if (casterCollider == null || casterCollider == _collider)
                {
                    _magicCasterColliders.RemoveAt(index);
                    continue;
                }
                UnityEngine.Physics.IgnoreCollision(_collider, casterCollider, true);
            }
        }

        public Vector3 GetBasePoint(float along01, float depth01)
        {
            float along = Mathf.Clamp01(along01);
            float depth = Mathf.Lerp(-Thickness * 0.5f, Thickness * 0.5f, Mathf.Clamp01(depth01));
            float side = Mathf.Lerp(-_finalScale.x * 0.5f, _finalScale.x * 0.5f, along);
            return transform.position - (transform.up * Height * 0.5f) +
                   (transform.right * side) + (transform.forward * depth);
        }

        internal bool AcquirePieceForMagic(int pieceIndex)
        {
            if (!_fractured || _cohesion == null || !_cohesion.AcquirePiece(pieceIndex)) return false;
            _structureRuntime?.BreakPieceBonds(pieceIndex, CurrentStructureTick);
            BreakRemainingPieceBonds(pieceIndex);
            _pieceShrinking[pieceIndex] = false;
            Transform piece = _pieces[pieceIndex];
            if (piece != null)
            {
                piece.localScale = _pieceFractureScales[pieceIndex];
                piece.gameObject.SetActive(true);
            }
            Rigidbody body = _pieceBodies[pieceIndex];
            if (body != null)
            {
                body.isKinematic = false;
                body.detectCollisions = true;
                body.WakeUp();
            }
            return true;
        }

        internal bool AcquirePieceForRepair(int pieceIndex)
        {
            if (!_fractured || _cohesion == null || !_cohesion.AcquirePiece(pieceIndex)) return false;
            if (pieceIndex < 0 || pieceIndex >= _pieces.Length) return false;
            // Snapshot/order selection happens before capture. Once a piece enters
            // the repair session, release its old damaged constraints so PhysX is
            // never asked to satisfy an old island and a new staging pose at once.
            _structureRuntime?.BreakPieceBonds(pieceIndex, CurrentStructureTick);
            BreakRemainingPieceBonds(pieceIndex);
            _pieceShrinking[pieceIndex] = false;
            Transform piece = _pieces[pieceIndex];
            if (piece != null)
            {
                piece.localScale = _pieceFractureScales[pieceIndex];
                piece.gameObject.SetActive(true);
            }
            Rigidbody body = _pieceBodies[pieceIndex];
            if (body != null)
            {
                body.isKinematic = false;
                body.detectCollisions = true;
                body.WakeUp();
            }
            return true;
        }

        internal void ReleasePieceFromRepair(int pieceIndex)
        {
            _cohesion?.ReleasePiece(pieceIndex);
            if (_structureRuntime != null &&
                _structureRuntime.GetPieceState(pieceIndex).Phase != EarthPiecePhase.Welded)
            {
                _structureRuntime.SetPiecePhase(pieceIndex, EarthPiecePhase.Dynamic, CurrentStructureTick);
            }
        }

        internal void RestoreBondForRepair(int bondIndex)
        {
            if (bondIndex < 0 || bondIndex >= _bonds.Length) return;
            EarthWallBond bond = _bonds[bondIndex];
            _bondBroken[bondIndex] = false;
            _bondDamage[bondIndex] = 0f;
            EarthBondRuntime runtime = _structureRuntime?.GetBondRuntime(bondIndex);
            Rigidbody connected = bond.Foundation ? _body : _pieceBodies[bond.PieceB];
            if (runtime != null) runtime.Activate(connected);
        }

        internal void SetRepairBondCollisionIgnored(int bondIndex, bool ignored)
        {
            if (bondIndex < 0 || bondIndex >= _bonds.Length) return;
            EarthWallBond bond = _bonds[bondIndex];
            if (bond.Foundation || bond.PieceA < 0 || bond.PieceB < 0 ||
                bond.PieceA >= _pieces.Length || bond.PieceB >= _pieces.Length) return;
            Collider first = _pieces[bond.PieceA].GetComponent<Collider>();
            Collider second = _pieces[bond.PieceB].GetComponent<Collider>();
            if (first != null && second != null)
                UnityEngine.Physics.IgnoreCollision(first, second, ignored);
        }

        internal void CompletePhysicalRepair(uint tick)
        {
            if (!_fractured) return;
            RestoreMatterAfterRepair();
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthPieceDefinition definition = _structureRuntime.GetPieceDefinition(index);
                Transform piece = _pieces[index];
                Rigidbody pieceBody = _pieceBodies[index];
                if (pieceBody != null)
                {
                    pieceBody.detectCollisions = false;
                    if (!pieceBody.isKinematic)
                    {
                        pieceBody.linearVelocity = Vector3.zero;
                        pieceBody.angularVelocity = Vector3.zero;
                    }
                    pieceBody.isKinematic = true;
                }
                piece.SetParent(transform, false);
                piece.localPosition = new Vector3(
                    definition.RestLocalPosition.x,
                    definition.RestLocalPosition.y,
                    definition.RestLocalPosition.z);
                quaternion rest = definition.RestLocalRotation;
                piece.localRotation = new Quaternion(rest.value.x, rest.value.y, rest.value.z, rest.value.w);
                piece.localScale = new Vector3(
                    definition.RestLocalScale.x,
                    definition.RestLocalScale.y,
                    definition.RestLocalScale.z);
                piece.gameObject.SetActive(false);
                _pieceAnchored[index] = false;
                _pieceDetachedAt[index] = -1f;
                _pieceShrinking[index] = false;
            }
            for (int index = 0; index < _bonds.Length; index++)
                _structureRuntime.GetBondRuntime(index)?.Release();
            _fractured = false;
            _manualFracturePaused = false;
            _gestureDisassemblyProgress = 0f;
            _cohesion?.ResetCohesion();
            _structureRuntime.CompleteRebuild(tick);
            _proxySwitcher?.ShowIntact(true);
            _renderer.enabled = true;
            _collider.enabled = true;
            _body.isKinematic = false;
            _body.mass = EstimatedMass;
            _body.WakeUp();
        }

        internal void ReleasePieceFromMagic(int pieceIndex)
        {
            _cohesion?.ReleasePiece(pieceIndex);
            _structureRuntime?.SetPieceReleased(pieceIndex, CurrentStructureTick);
            if (_pieceDetachedAt != null && pieceIndex >= 0 && pieceIndex < _pieceDetachedAt.Length)
                _pieceDetachedAt[pieceIndex] = _fractureElapsed;
        }

        public bool ApplyRockImpact(Vector3 point, Vector3 direction, float impulse)
        {
            if (impulse < MinimumRockImpactImpulse) return false;
            Vector3 tangentDirection = Vector3.ProjectOnPlane(direction, _up).normalized;
            if (tangentDirection.sqrMagnitude < 0.5f) tangentDirection = _forward;
            _fractureOrigin = point;
            _fractureBias = tangentDirection;
            if (!_fractured) BeginCohesiveFracture();
            DamageBonds(point, tangentDirection, impulse);
            return true;
        }

        public bool ApplyStructureImpact(Vector3 point, Vector3 direction, float impulse)
        {
            return ApplyRockImpact(point, direction, impulse);
        }

        public bool ApplyEarthImpact(in EarthStructureImpact impact) =>
            ApplyStructureImpact(impact.Point, impact.Direction, impact.Impulse);

        public bool TryPluckCell(Vector3 point, out IEarthPhysicalTarget target)
        {
            target = null;
            if (_pieces == null || _pieceBodies == null || _pieceTargets == null) return false;
            if (!_fractured) BeginCohesiveFracture();
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < _pieces.Length; index++)
            {
                if (_pieces[index] == null || !_pieces[index].gameObject.activeSelf) continue;
                float distance = Vector3.SqrMagnitude(_pieces[index].position - point);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }
            if (best < 0) return false;
            _fractureOrigin = point;
            _fractureBias = (_pieces[best].position - transform.position).normalized;
            BreakRemainingPieceBonds(best);
            Rigidbody body = _pieceBodies[best];
            if (body != null)
            {
                body.isKinematic = false;
                body.detectCollisions = true;
                body.WakeUp();
            }
            target = _pieceTargets[best];
            return target != null && target.IsEarthTargetValid;
        }

        public bool SetMagicDisassemblyProgress(
            float phase01,
            Vector3 focus,
            Vector3 direction)
        {
            float requested = Mathf.Clamp01(phase01);
            if (requested <= _gestureDisassemblyProgress || _bonds == null || _bonds.Length == 0)
                return _fractured;
            _fractureOrigin = focus;
            _fractureBias = Vector3.ProjectOnPlane(direction, _up).normalized;
            if (_fractureBias.sqrMagnitude < 0.5f) _fractureBias = _forward;
            if (!_fractured) BeginCohesiveFracture();
            _gestureDisassemblyProgress = requested;
            _manualFracturePaused = requested < 1f;

            int targetBroken = Mathf.Clamp(
                Mathf.CeilToInt(requested * _bonds.Length), 1, _bonds.Length);
            int broken = 0;
            for (int index = 0; index < _bondBroken.Length; index++)
                if (_bondBroken[index]) broken++;
            bool connectivityDirty = false;
            while (broken < targetBroken)
            {
                int candidate = FindNextGestureBond(focus);
                if (candidate < 0) break;
                ReleaseBond(candidate, 0f, focus, _fractureBias);
                broken++;
                connectivityDirty = true;
            }
            if (connectivityDirty) RecomputeConnectivity();
            return true;
        }

        private int FindNextGestureBond(Vector3 focus)
        {
            int best = -1;
            float bestScore = float.PositiveInfinity;
            for (int index = 0; index < _bonds.Length; index++)
            {
                if (_bondBroken[index]) continue;
                EarthWallBond bond = _bonds[index];
                Vector3 a = _pieces[bond.PieceA].position;
                Vector3 b = bond.Foundation ? _body.worldCenterOfMass : _pieces[bond.PieceB].position;
                float score = Vector3.SqrMagnitude(((a + b) * 0.5f) - focus) +
                              Hash01(WallId ^ 0xA11CEu, index) * 0.08f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = index;
            }
            return best;
        }

        internal void HandlePieceCollision(int pieceIndex, Collision collision)
        {
            if (!_fractured || collision.contactCount == 0 || collision.impulse.magnitude < MinimumRockImpactImpulse)
                return;
            if (_reassembly != null && _reassembly.IsRepairing)
            {
                EarthPieceRuntime otherPiece = collision.collider != null
                    ? collision.collider.GetComponentInParent<EarthPieceRuntime>()
                    : null;
                // Seating contacts and the intentional terrain insertion are
                // solver mechanics, not new gameplay impacts. External dynamic
                // bodies still pass through the normal damage path.
                if ((otherPiece != null && otherPiece.Owner == this) || collision.rigidbody == null)
                    return;
            }
            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = _pieceBodies[pieceIndex] != null &&
                                _pieceBodies[pieceIndex].linearVelocity.sqrMagnitude > 0.01f
                ? _pieceBodies[pieceIndex].linearVelocity.normalized
                : -contact.normal;
            DamageBonds(contact.point, direction, collision.impulse.magnitude);
        }

        private void Awake() => ResolveReferences();

        private void ResolveReferences()
        {
            if (_collider == null) _collider = GetComponent<BoxCollider>();
            if (_visualEmergenceRoot == null)
            {
                Transform existing = transform.Find("VisualEmergenceRoot");
                if (existing != null)
                {
                    _visualEmergenceRoot = existing;
                    _renderer = existing.GetComponent<MeshRenderer>();
                }
                else
                {
                    MeshFilter sourceFilter = GetComponent<MeshFilter>();
                    MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
                    if (sourceFilter != null && sourceRenderer != null)
                    {
                        var visual = new GameObject("VisualEmergenceRoot");
                        _visualEmergenceRoot = visual.transform;
                        _visualEmergenceRoot.SetParent(transform, false);
                        MeshFilter visualFilter = visual.AddComponent<MeshFilter>();
                        visualFilter.sharedMesh = sourceFilter.sharedMesh;
                        _renderer = visual.AddComponent<MeshRenderer>();
                        _renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                        sourceRenderer.enabled = false;
                    }
                }
            }
            if (_renderer == null && _visualEmergenceRoot != null)
                _renderer = _visualEmergenceRoot.GetComponent<MeshRenderer>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_cohesion == null) _cohesion = GetComponent<EarthCohesiveStructure>();
        }

        private void Update()
        {
            if (_restoreCasterCollisionsAt >= 0f && Time.time >= _restoreCasterCollisionsAt)
                SetMagicCasterCollisionIgnored(false);
            if (_fractured)
            {
                UpdateFracture();
                return;
            }
            if (_emergence < 1f)
            {
                UpdateEmergence();
                return;
            }

            _stableElapsed += Time.deltaTime;
            float automaticCrackDelay = AutomaticCrackDelaySeconds;
            if (automaticCrackDelay > 0f && _stableElapsed >= automaticCrackDelay)
            {
                _fractureOrigin = transform.position - (_up * Height * 0.34f);
                BeginCohesiveFracture();
            }
        }

        private void FixedUpdate()
        {
            if (_pendingColliderActivation)
            {
                TryFinalizeEmergencePhysics();
                if (_pendingColliderActivation) return;
            }
            if (_body == null || _body.isKinematic) return;
            StabilizeRootBody();
            if (!_fractured || _pieceBodies == null) return;
            for (int index = 0; index < _pieceBodies.Length; index++)
            {
                Rigidbody pieceBody = _pieceBodies[index];
                if (pieceBody == null || pieceBody.isKinematic || _pieces[index] == null ||
                    !_pieces[index].gameObject.activeSelf) continue;
                EarthPieceRuntime runtimePiece = _pieceTargets[index] as EarthPieceRuntime;
                if (runtimePiece != null && runtimePiece.HasMagicOwner &&
                    runtimePiece.MagicOwner == EarthMagicGripKind.Repair) continue;
                Vector3 inward = _planetCenter - pieceBody.worldCenterOfMass;
                if (inward.sqrMagnitude < 0.01f) inward = -_up;
                pieceBody.AddForce(
                    inward.normalized * PlanetaryDebrisAcceleration,
                    ForceMode.Acceleration);
            }
        }

        private void StabilizeRootBody()
        {
            if (_orientationMode == ConstructionOrientationMode.PreserveAuthoredFrame)
            {
                // A construction authored on another structure owns its frame.
                // Re-solving this body against the planet radial frame turns a
                // horizontal child vertical as soon as magic activates physics.
                // Translation remains physical; only uncommanded rotation is
                // rejected while the intact structure slides in its authored pose.
                _body.angularVelocity = Vector3.zero;
                _body.rotation = _surfaceRotation;
                Vector3 authoredVelocity = _body.linearVelocity;
                float authoredDrag = _magicFieldActive ? MagicFieldSlideDrag : WallSlideDrag;
                _body.AddForce(-authoredVelocity * authoredDrag, ForceMode.Acceleration);
                if (authoredVelocity.magnitude > MaximumSlideSpeed)
                    _body.linearVelocity = Vector3.ClampMagnitude(authoredVelocity, MaximumSlideSpeed);
                return;
            }
            Vector3 velocity = _body.linearVelocity;
            Vector3 radial = _body.position - _planetCenter;
            Vector3 predictedRadial = radial + (velocity * Time.fixedDeltaTime);
            Vector3 localUp = predictedRadial.sqrMagnitude > 0.01f ? predictedRadial.normalized : _up;
            float normalOffset = radial.magnitude - _surfaceRootRadius;
            float normalVelocity = Vector3.Dot(velocity, localUp);
            _body.AddForce(-localUp * ((normalOffset * 68f) + (normalVelocity * 14f)), ForceMode.Acceleration);
            Vector3 tangentVelocity = Vector3.ProjectOnPlane(velocity, localUp);
            float slideDrag = _magicFieldActive ? MagicFieldSlideDrag : WallSlideDrag;
            _body.AddForce(-tangentVelocity * slideDrag, ForceMode.Acceleration);
            if (tangentVelocity.magnitude > MaximumSlideSpeed)
                _body.linearVelocity = Vector3.ClampMagnitude(tangentVelocity, MaximumSlideSpeed) +
                                       (localUp * normalVelocity);

            Vector3 localTangent = Vector3.ProjectOnPlane(_tangent, localUp).normalized;
            if (localTangent.sqrMagnitude < 0.5f)
                localTangent = Vector3.Cross(localUp, _forward).normalized;
            Vector3 localForward = Vector3.Cross(localTangent, localUp).normalized;
            _up = localUp;
            _tangent = localTangent;
            _forward = localForward;
            _surfacePosition = _planetCenter + (localUp * _surfaceRootRadius);
            Vector3 baseCenter = _surfacePosition - (localUp * Height * 0.5f);
            _embeddedStart = baseCenter - (localTangent * _finalScale.x * 0.5f);
            _embeddedEnd = baseCenter + (localTangent * _finalScale.x * 0.5f);
            _surfaceRotation = Quaternion.LookRotation(localForward, localUp);
            // Rotation is gameplay-owned while the wall is intact. PhysX rotation
            // constraints intentionally reject collision torque, and they also reject
            // MoveRotation on some backends; assign the solved local-gravity frame so
            // a fast slide follows the planet instead of appearing to tilt.
            _body.rotation = _surfaceRotation;
        }

        private static float RequiredCornerSafeEmbed(
            Vector3 midpoint,
            Vector3 planetCenter,
            Vector3 tangent,
            Vector3 up,
            Vector3 forward,
            float halfLength,
            float halfThickness,
            float targetRadius,
            float minimumEmbed)
        {
            float low = Mathf.Max(0f, minimumEmbed);
            float high = low;
            while (MaximumBottomCornerRadius(
                       midpoint - (up * high), planetCenter, tangent, forward,
                       halfLength, halfThickness) > targetRadius && high < targetRadius)
                high = Mathf.Max(high + 0.1f, high * 1.6f);

            high = Mathf.Min(high, targetRadius);
            for (int iteration = 0; iteration < 20; iteration++)
            {
                float candidate = (low + high) * 0.5f;
                float radius = MaximumBottomCornerRadius(
                    midpoint - (up * candidate), planetCenter, tangent, forward,
                    halfLength, halfThickness);
                if (radius > targetRadius) low = candidate;
                else high = candidate;
            }
            return high;
        }

        private static float MaximumBottomCornerRadius(
            Vector3 baseCenter,
            Vector3 planetCenter,
            Vector3 tangent,
            Vector3 forward,
            float halfLength,
            float halfThickness)
        {
            float maximum = 0f;
            for (int side = -1; side <= 1; side += 2)
            for (int depth = -1; depth <= 1; depth += 2)
            {
                Vector3 corner = baseCenter + (tangent * halfLength * side) +
                                 (forward * halfThickness * depth);
                maximum = Mathf.Max(maximum, Vector3.Distance(corner, planetCenter));
            }
            return maximum;
        }

        private void UpdateEmergence()
        {
            using var marker = EmergenceMarker.Auto();
            _emergence = Mathf.Min(1f, _emergence + (Time.deltaTime / _emergenceDuration));
            float eased = _emergence * _emergence * (3f - 2f * _emergence);
            float time = Time.time;
            float envelope = Mathf.Lerp(1f, 0.26f, _emergence);
            float sideJolt = ((Mathf.Sin((time * 31f) + WallId) * 0.72f) +
                              (Mathf.Sin((time * 67f) + (WallId * 1.91f)) * 0.28f)) * 0.13f * envelope;
            float depthJolt = Mathf.Sin((time * 43f) + (WallId * 0.63f)) * 0.055f * envelope;
            float liftJolt = Mathf.Abs(Mathf.Sin((time * 24f) + WallId)) * 0.052f * envelope;
            float massPulse = Mathf.Sin(Mathf.Clamp01(_emergence / 0.34f) * Mathf.PI) * 0.105f;
            // Even very short authored rise times get one readable lateral weight
            // transfer instead of sampling the two noise waves near a zero crossing.
            sideJolt += 0.095f + massPulse;
            Vector3 tremor = (_tangent * sideJolt) + (_forward * depthJolt) + (_up * liftJolt);
            PeakEmergenceTremorMeters = Mathf.Max(PeakEmergenceTremorMeters, tremor.magnitude);

            if (_visualEmergenceRoot != null)
            {
                _visualEmergenceRoot.localPosition = new Vector3(
                    sideJolt / Mathf.Max(0.01f, _finalScale.x),
                    Mathf.Lerp(-0.92f, 0f, eased) + (liftJolt / Mathf.Max(0.01f, _finalScale.y)),
                    depthJolt / Mathf.Max(0.01f, _finalScale.z));
                _visualEmergenceRoot.localRotation = Quaternion.Euler(
                    depthJolt * 22f, sideJolt * 7f, -sideJolt * 24f);
                _visualEmergenceRoot.localScale = new Vector3(1f, Mathf.Lerp(0.18f, 1f, eased), 1f);
            }

            PeakRootEmergenceDisplacementMeters = Mathf.Max(
                PeakRootEmergenceDisplacementMeters,
                Vector3.Distance(transform.position, _emergenceRootPosition));
            if (_emergence < 1f) return;
            SetVisualFinalPose();
            _pendingColliderActivation = true;
        }

        private void ResetVisualEmergencePose()
        {
            if (_visualEmergenceRoot == null) return;
            _visualEmergenceRoot.localPosition = new Vector3(0f, -0.92f, 0f);
            _visualEmergenceRoot.localRotation = Quaternion.identity;
            _visualEmergenceRoot.localScale = new Vector3(1f, 0.18f, 1f);
        }

        private void SetVisualFinalPose()
        {
            if (_visualEmergenceRoot == null) return;
            _visualEmergenceRoot.localPosition = Vector3.zero;
            _visualEmergenceRoot.localRotation = Quaternion.identity;
            _visualEmergenceRoot.localScale = Vector3.one;
        }

        private void ConfigureColliderFoundationInset(float requestedInset)
        {
            _colliderBottomInset = Mathf.Clamp(requestedInset, 0f, Mathf.Max(0f, Height - 0.16f));
            float normalizedInset = _colliderBottomInset / Mathf.Max(0.1f, Height);
            _collider.size = new Vector3(1f, Mathf.Max(0.04f, 1f - normalizedInset), 1f);
            _collider.center = new Vector3(0f, normalizedInset * 0.5f, 0f);
        }

        private void TryFinalizeEmergencePhysics()
        {
            using var marker = ColliderValidationMarker.Auto();
            if (!IsColliderPoseSafe(out float penetration))
            {
                float nextInset = Mathf.Min(Height - 0.16f,
                    _colliderBottomInset + Mathf.Max(0.035f, penetration + 0.015f));
                if (nextInset > _colliderBottomInset + 0.0001f)
                {
                    ConfigureColliderFoundationInset(nextInset);
                    return;
                }
                return;
            }

            _collider.enabled = true;
            _body.mass = EstimatedMass;
            // The completed wall remains an anchored kinematic structure until
            // gameplay explicitly asks it to move. This prevents a contact pair
            // at the noisy SDF surface from generating a depenetration launch on
            // the first simulation tick, while still making magic activation
            // dynamic before any force is applied.
            _body.isKinematic = true;
            _pendingColliderActivation = false;
        }

        private void ActivateDynamicPhysics()
        {
            if (_body == null || !_body.isKinematic || _fractured || _emergence < 1f) return;
            _body.mass = EstimatedMass;
            _body.isKinematic = false;
            _body.WakeUp();
        }

        private bool IsColliderPoseSafe(out float maximumPenetration)
        {
            maximumPenetration = 0f;
            Vector3 lossy = transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(_collider.size * 0.5f,
                new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
            Vector3 center = transform.TransformPoint(_collider.center);
            int count = UnityEngine.Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _colliderValidationHits,
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);
            bool safe = true;
            for (int index = 0; index < count; index++)
            {
                Collider other = _colliderValidationHits[index];
                if (other == null || other == _collider || other.transform.IsChildOf(transform)) continue;
                if (!UnityEngine.Physics.ComputePenetration(
                        _collider, transform.position, transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out _, out float distance)) continue;
                maximumPenetration = Mathf.Max(maximumPenetration, distance);
                if (distance > MaximumSafePenetration) safe = false;
            }
            return safe;
        }

        private void BeginCohesiveFracture()
        {
            if (_fractured) return;
            SetMagicCasterCollisionIgnored(false);
            _fractured = true;
            _cohesion?.BeginFracture();
            _structureRuntime?.BeginFracture(CurrentStructureTick);
            _fractureElapsed = 0f;
            Vector3 displacement = transform.position - _surfacePosition;
            Start += displacement;
            End += displacement;
            _embeddedStart += displacement;
            _embeddedEnd += displacement;
            _surfacePosition += displacement;
            _buriedPosition += displacement;
            transform.SetPositionAndRotation(_surfacePosition, _surfaceRotation);
            transform.localScale = _finalScale;
            SetVisualFinalPose();
            _collider.enabled = false;
            _renderer.enabled = false;
            _proxySwitcher?.ShowFractured();
            if (_pieces == null)
            {
                TargetsActivated?.Invoke(this);
                Collapsed?.Invoke(this);
                return;
            }

            Vector3 inheritedVelocity = _body.linearVelocity;
            _body.mass = Mathf.Max(1f, EstimatedMass * 0.10f);
            for (int index = 0; index < _pieces.Length; index++)
            {
                Transform piece = _pieces[index];
                piece.SetParent(transform, false);
                piece.localPosition = _pieceBasePositions[index];
                piece.localRotation = Quaternion.identity;
                piece.localScale = Vector3.one;
                piece.gameObject.SetActive(true);
                piece.SetParent(transform.parent, true);
                _pieceFractureScales[index] = piece.localScale;
                _pieceDetachedAt[index] = -1f;
                _pieceAnchored[index] = false;
                _pieceShrinking[index] = false;
                Rigidbody pieceBody = _pieceBodies[index];
                if (pieceBody == null) continue;
                pieceBody.mass = Mathf.Max(0.35f, EstimatedMass * 0.90f * _pieceVolumeFractions[index]);
                pieceBody.isKinematic = false;
                pieceBody.detectCollisions = true;
                pieceBody.linearVelocity = inheritedVelocity;
                pieceBody.angularVelocity = Vector3.zero;
                pieceBody.WakeUp();
            }
            RegisterMatterFracture();
            ActivateBonds();
            RecomputeConnectivity();
            TargetsActivated?.Invoke(this);
            Collapsed?.Invoke(this);
        }

        private void RegisterMatterFracture()
        {
            EarthMatterIdentity root = GetComponent<EarthMatterIdentity>();
            if (root == null || !root.TryRead(out EarthMatterRecord parent) ||
                parent.Phase == EarthMatterPhase.Consumed || _pieces == null) return;
            EarthMatterKernelBehaviour kernel = root.Kernel;
            int count = _pieces.Length;
            if (_matterChildren == null || _matterChildren.Length != count)
            {
                _matterChildren = new EarthMatterRecord[count];
                _matterChildIds = new EarthMatterId[count];
                _matterMergeIds = new EarthMatterId[count];
            }
            float fractionTotal = 0f;
            for (int index = 0; index < count; index++)
                fractionTotal += Mathf.Max(0.0001f, _pieceVolumeFractions[index]);
            for (int index = 0; index < count; index++)
            {
                float fraction = Mathf.Max(0.0001f, _pieceVolumeFractions[index]) /
                                 Mathf.Max(0.0001f, fractionTotal);
                Transform piece = _pieces[index];
                Rigidbody body = _pieceBodies[index];
                Vector3 position = body != null ? body.worldCenterOfMass : piece.position;
                Quaternion rotation = body != null ? body.rotation : piece.rotation;
                var pose = new EarthMatterPose(ToFloat3(position),
                    new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
                _matterChildren[index] = new EarthMatterRecord
                {
                    Phase = EarthMatterPhase.FreeDynamic,
                    Representation = EarthRepresentationTier.SecondaryPhysical,
                    Material = parent.Material,
                    Volume = parent.Volume * fraction,
                    Mass = parent.Mass * fraction,
                    Integrity = Mathf.Clamp01(parent.Integrity),
                    Source = new EarthSourceProvenance(
                        EarthSourceKind.StructureCell,
                        parent.Id.StableId,
                        parent.Id.Generation,
                        index,
                        SourceTick,
                        parent.Source.SourceLocalPoint,
                        parent.Volume * fraction,
                        EarthProvenanceFlags.SourceStructureAlive |
                        EarthProvenanceFlags.VolumeReserved),
                    Owner = parent.Owner,
                    Shape = EarthShapeSemantic.WallCell,
                    RestPose = pose,
                    CurrentPose = pose,
                    LinearVelocity = body != null ? ToFloat3(body.linearVelocity) : float3.zero,
                    AngularVelocity = body != null ? ToFloat3(body.angularVelocity) : float3.zero
                };
            }
            if (!kernel.Registry.TrySplit(root.MatterId, _matterChildren, count, _matterChildIds))
            {
                Debug.LogError($"[EarthMatter] Wall {WallId} fracture split rejected: {kernel.Registry.LastFailure}", this);
                return;
            }
            _matterConsumedRoot = root.MatterId;
            for (int index = 0; index < count; index++)
                EarthMatterRuntimeBridge.BindExistingRecord(
                    _pieces[index], kernel, _matterChildIds[index], _pieceBodies[index]);
        }

        private void RestoreMatterAfterRepair()
        {
            if (!_matterConsumedRoot.IsValid || _matterMergeIds == null) return;
            EarthMatterIdentity root = GetComponent<EarthMatterIdentity>();
            if (root == null || root.Kernel == null ||
                !root.Kernel.Registry.TryGet(_matterConsumedRoot, out EarthMatterRecord consumed)) return;
            int count = 0;
            for (int index = 0; index < _pieces.Length; index++)
            {
                EarthMatterIdentity child = _pieces[index].GetComponent<EarthMatterIdentity>();
                if (child == null || !child.MatterId.IsValid) return;
                _matterMergeIds[count++] = child.MatterId;
            }
            Quaternion rotation = transform.rotation;
            var pose = new EarthMatterPose(ToFloat3(transform.position),
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
            consumed.Phase = EarthMatterPhase.FreeDynamic;
            consumed.Representation = EarthRepresentationTier.HeroPhysical;
            consumed.Integrity = 1f;
            consumed.CurrentPose = pose;
            consumed.RestPose = pose;
            consumed.LinearVelocity = _body != null ? ToFloat3(_body.linearVelocity) : float3.zero;
            consumed.AngularVelocity = float3.zero;
            if (!root.Kernel.Registry.TryMerge(
                    _matterConsumedRoot, _matterMergeIds, count, in consumed, out EarthMatterId restored))
            {
                Debug.LogError($"[EarthMatter] Wall {WallId} repair merge rejected: {root.Kernel.Registry.LastFailure}", this);
                return;
            }
            EarthMatterRuntimeBridge.BindExistingRecord(this, root.Kernel, restored, _body);
            _matterConsumedRoot = default;
        }

        private void ActivateBonds()
        {
            for (int index = 0; index < _bonds.Length; index++)
            {
                EarthWallBond bond = _bonds[index];
                ConfigurableJoint joint = bond.Joint;
                _bondDamage[index] = 0f;
                _bondBroken[index] = false;
                float contactWeight = Mathf.Sqrt(Mathf.Max(0.04f, bond.NormalizedContactArea * _pieces.Length));
                _bondStrength[index] = EstimatedMass * CohesionImpulsePerMass * contactWeight *
                                       (bond.Foundation ? FoundationStrengthMultiplier : 1f);
                _structureRuntime?.SetBondStrengths(
                    index,
                    _bondStrength[index],
                    _bondStrength[index] * 1.18f,
                    _bondStrength[index] * 3.4f);
                EarthBondRuntime runtimeBond = _structureRuntime?.GetBondRuntime(index);
                if (runtimeBond != null)
                {
                    runtimeBond.Activate(bond.Foundation ? _body : _pieceBodies[bond.PieceB]);
                }
                else
                {
                    joint.autoConfigureConnectedAnchor = true;
                    joint.connectedBody = bond.Foundation ? _body : _pieceBodies[bond.PieceB];
                    joint.xMotion = ConfigurableJointMotion.Locked;
                    joint.yMotion = ConfigurableJointMotion.Locked;
                    joint.zMotion = ConfigurableJointMotion.Locked;
                    joint.angularXMotion = ConfigurableJointMotion.Locked;
                    joint.angularYMotion = ConfigurableJointMotion.Locked;
                    joint.angularZMotion = ConfigurableJointMotion.Locked;
                    joint.enableCollision = false;
                    joint.enablePreprocessing = false;
                }
            }
        }

        private void UpdateFracture()
        {
            _fractureElapsed += Time.deltaTime;
            bool connectivityDirty = false;
            float decayStart = FractureWaveSeconds;
            if (_structureRuntime == null && !_manualFracturePaused && _fractureElapsed >= decayStart)
            {
                for (int index = 0; index < _bonds.Length; index++)
                {
                    if (_bondBroken[index]) continue;
                    float variation = Mathf.Lerp(0.72f, 1.34f, Hash01(WallId ^ 0xC011u, index));
                    _bondDamage[index] += _bondStrength[index] *
                                          (Time.deltaTime / CohesionDecaySeconds) * variation;
                    if (_bondDamage[index] < _bondStrength[index]) continue;
                    ReleaseBond(index, 0f, _fractureOrigin, _fractureBias);
                    connectivityDirty = true;
                }
            }
            if (connectivityDirty) RecomputeConnectivity();

            bool anyActive = false;
            if (_pieces == null)
            {
                gameObject.SetActive(false);
                return;
            }
            for (int index = 0; index < _pieces.Length; index++)
            {
                if (_pieces[index] == null || !_pieces[index].gameObject.activeSelf) continue;
                anyActive = true;
                UpdatePieceShrink(index);
            }
            if (anyActive) return;
            HideFracturePieces();
            gameObject.SetActive(false);
        }

        private void DamageBonds(Vector3 point, Vector3 direction, float impulse)
        {
            if (!_fractured || _bonds == null || impulse <= 0f) return;
            float impactRatio = Mathf.Max(1f, impulse / Mathf.Max(1f, MinimumRockImpactImpulse));
            float radius = Mathf.Lerp(
                Mathf.Max(0.65f, Thickness * 1.8f),
                Mathf.Max(_finalScale.x, Height) * 0.82f,
                1f - Mathf.Exp(-impactRatio * 0.22f));
            if (_structureRuntime != null && _structureRuntime.IsConfigured)
            {
                Vector3 localPointVector = transform.InverseTransformPoint(point);
                Vector3 localDirection = transform.InverseTransformDirection(direction).normalized;
                float localRadius = radius / Mathf.Max(0.25f, Mathf.Max(_finalScale.x, _finalScale.y));
                _structureRuntime.ApplyImpact(
                    new float3(localPointVector.x, localPointVector.y, localPointVector.z),
                    new float3(localDirection.x, localDirection.y, localDirection.z) *
                    (impulse * ImpactDamageMultiplier),
                    Mathf.Max(0.05f, localRadius),
                    1f,
                    CurrentStructureTick);
                bool releasedAny = false;
                for (int index = 0; index < _bonds.Length; index++)
                {
                    if (_bondBroken[index] || !_structureRuntime.IsBondBroken(index)) continue;
                    ReleaseBond(index, 0f, point, direction);
                    releasedAny = true;
                }
                if (releasedAny) RecomputeConnectivity();
                return;
            }
            bool connectivityDirty = false;
            for (int index = 0; index < _bonds.Length; index++)
            {
                if (_bondBroken[index]) continue;
                EarthWallBond bond = _bonds[index];
                Vector3 a = _pieces[bond.PieceA].position;
                Vector3 b = bond.Foundation ? _body.worldCenterOfMass : _pieces[bond.PieceB].position;
                Vector3 center = (a + b) * 0.5f;
                float falloff = Mathf.Clamp01(1f - (Vector3.Distance(center, point) / radius));
                if (falloff <= 0f) continue;
                _bondDamage[index] += impulse * ImpactDamageMultiplier * falloff * falloff;
                if (_bondDamage[index] < _bondStrength[index]) continue;
                float excess = _bondDamage[index] - _bondStrength[index];
                ReleaseBond(index, excess, point, direction);
                connectivityDirty = true;
            }
            if (connectivityDirty) RecomputeConnectivity();
        }

        private void ReleaseBond(int index, float excessImpulse, Vector3 origin, Vector3 direction)
        {
            if (_bondBroken[index]) return;
            _bondBroken[index] = true;
            _structureRuntime?.MarkBondBroken(index, CurrentStructureTick);
            EarthWallBond bond = _bonds[index];
            ConfigurableJoint joint = bond.Joint;
            Rigidbody a = _pieceBodies[bond.PieceA];
            Rigidbody b = bond.Foundation ? _body : _pieceBodies[bond.PieceB];
            EarthBondRuntime runtimeBond = _structureRuntime?.GetBondRuntime(index);
            SetRepairBondCollisionIgnored(index, false);
            if (runtimeBond != null)
            {
                runtimeBond.Release();
                if (a != null) a.isKinematic = false;
                if (!bond.Foundation && b != null) b.isKinematic = false;
            }
            else
            {
                joint.xMotion = ConfigurableJointMotion.Free;
                joint.yMotion = ConfigurableJointMotion.Free;
                joint.zMotion = ConfigurableJointMotion.Free;
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;
                joint.connectedBody = null;
                joint.enableCollision = true;
            }
            if (excessImpulse <= 0f) return;

            Vector3 releaseDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : _fractureBias;
            float releasedImpulse = Mathf.Min(excessImpulse * ExcessImpulseRelease, EstimatedMass * 1.8f);
            if (a != null && !a.isKinematic)
                a.AddForceAtPosition(releaseDirection * releasedImpulse, origin, ForceMode.Impulse);
            if (!bond.Foundation && b != null && !b.isKinematic)
                b.AddForceAtPosition(-releaseDirection * releasedImpulse * 0.45f, origin, ForceMode.Impulse);
        }

        private void RecomputeConnectivity()
        {
            if (_structureRuntime != null && _structureRuntime.IsConfigured)
            {
                for (int index = 0; index < _pieceAnchored.Length; index++)
                {
                    _pieceAnchored[index] = _structureRuntime.IsPieceSupported(index);
                    if (!_pieceAnchored[index] && _pieceDetachedAt[index] < 0f)
                        _pieceDetachedAt[index] = _fractureElapsed;
                }
                return;
            }
            Array.Clear(_pieceAnchored, 0, _pieceAnchored.Length);
            int head = 0;
            int tail = 0;
            for (int index = 0; index < _bonds.Length; index++)
            {
                if (_bondBroken[index] || !_bonds[index].Foundation) continue;
                int piece = _bonds[index].PieceA;
                if (_pieceAnchored[piece]) continue;
                _pieceAnchored[piece] = true;
                _connectivityQueue[tail++] = piece;
            }
            while (head < tail)
            {
                int current = _connectivityQueue[head++];
                for (int index = 0; index < _bonds.Length; index++)
                {
                    if (_bondBroken[index] || _bonds[index].Foundation) continue;
                    EarthWallBond bond = _bonds[index];
                    int next = bond.PieceA == current ? bond.PieceB : bond.PieceB == current ? bond.PieceA : -1;
                    if (next < 0 || _pieceAnchored[next]) continue;
                    _pieceAnchored[next] = true;
                    _connectivityQueue[tail++] = next;
                }
            }
            for (int index = 0; index < _pieceAnchored.Length; index++)
            {
                if (!_pieceAnchored[index] && _pieceDetachedAt[index] < 0f)
                    _pieceDetachedAt[index] = _fractureElapsed;
            }
        }

        private void UpdatePieceShrink(int index)
        {
            if (_cohesion != null && _cohesion.IsPieceHeld(index)) return;
            // Structural pieces retain provenance and stay targetable for repair.
            // Shrink-out is an explicit legacy cleanup policy, never the MVP default.
            if (!ShrinkDetachedStructuralPieces) return;
            if (_pieceAnchored[index] || _pieceDetachedAt[index] < 0f) return;
            Elemental.Simulation.Structures.DynamicDebrisLifecycleSample lifecycle =
                Elemental.Simulation.Structures.DynamicDebrisLifecycle.Evaluate(
                    _fractureElapsed - _pieceDetachedAt[index],
                    DebrisRestSeconds,
                    DebrisShrinkSeconds);
            if (!lifecycle.Shrinking) return;
            Rigidbody pieceBody = _pieceBodies[index];
            if (!_pieceShrinking[index])
            {
                _pieceShrinking[index] = true;
                BreakRemainingPieceBonds(index);
                if (pieceBody != null)
                {
                    pieceBody.isKinematic = false;
                    pieceBody.detectCollisions = true;
                    pieceBody.WakeUp();
                }
            }
            if (lifecycle.Complete)
            {
                if (pieceBody != null)
                {
                    pieceBody.detectCollisions = false;
                    pieceBody.isKinematic = true;
                }
                _pieces[index].gameObject.SetActive(false);
                return;
            }
            _pieces[index].localScale = _pieceFractureScales[index] *
                                        Mathf.Max(0.0125f, lifecycle.Scale01);
        }

        private void BreakRemainingPieceBonds(int pieceIndex)
        {
            bool changed = false;
            for (int index = 0; index < _bonds.Length; index++)
            {
                if (_bondBroken[index]) continue;
                EarthWallBond bond = _bonds[index];
                if (bond.PieceA != pieceIndex && bond.PieceB != pieceIndex) continue;
                ReleaseBond(index, 0f, _pieces[pieceIndex].position, _fractureBias);
                changed = true;
            }
            if (changed) RecomputeConnectivity();
        }

        private void ApplyDistributedImpulse(Vector3 direction, float impulse)
        {
            float rootShare = 0.10f;
            if (!_body.isKinematic) _body.AddForce(direction * impulse * rootShare, ForceMode.Impulse);
            for (int index = 0; index < _pieceBodies.Length; index++)
            {
                Rigidbody pieceBody = _pieceBodies[index];
                if (pieceBody == null || pieceBody.isKinematic || !_pieces[index].gameObject.activeSelf) continue;
                pieceBody.AddForce(direction * impulse * 0.90f * _pieceVolumeFractions[index], ForceMode.Impulse);
            }
        }

        private void HideFracturePieces()
        {
            if (_bonds != null)
            {
                for (int index = 0; index < _bonds.Length; index++)
                {
                    SetRepairBondCollisionIgnored(index, false);
                    ConfigurableJoint joint = _bonds[index].Joint;
                    joint.connectedBody = null;
                    joint.xMotion = ConfigurableJointMotion.Free;
                    joint.yMotion = ConfigurableJointMotion.Free;
                    joint.zMotion = ConfigurableJointMotion.Free;
                    joint.angularXMotion = ConfigurableJointMotion.Free;
                    joint.angularYMotion = ConfigurableJointMotion.Free;
                    joint.angularZMotion = ConfigurableJointMotion.Free;
                    joint.enableCollision = true;
                    _bondBroken[index] = true;
                    _bondDamage[index] = 0f;
                }
            }
            if (_pieces != null)
            {
                for (int index = 0; index < _pieces.Length; index++)
                {
                    Transform piece = _pieces[index];
                    Rigidbody pieceBody = _pieceBodies[index];
                    if (pieceBody != null)
                    {
                        pieceBody.detectCollisions = false;
                        if (!pieceBody.isKinematic)
                        {
                            pieceBody.linearVelocity = Vector3.zero;
                            pieceBody.angularVelocity = Vector3.zero;
                        }
                        pieceBody.isKinematic = true;
                    }
                    if (piece == null) continue;
                    piece.gameObject.SetActive(false);
                    piece.SetParent(transform, false);
                    piece.localPosition = _pieceBasePositions[index];
                    piece.localRotation = Quaternion.identity;
                    piece.localScale = Vector3.one;
                    _pieceDetachedAt[index] = -1f;
                    _pieceAnchored[index] = false;
                    _pieceShrinking[index] = false;
                }
            }
            if (_body != null)
            {
                if (!_body.isKinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }
                _body.isKinematic = true;
            }
            _cohesion?.ResetCohesion();
            _structureRuntime?.ResetExact(
                new EarthStructureId(WallId), _generation, CurrentStructureTick);
            _proxySwitcher?.ShowIntact(false);
        }

        internal void ReturnToPoolAsTransientProxy()
        {
            if (_pieces != null)
            {
                for (int index = 0; index < _pieces.Length; index++)
                    _pieces[index]?.GetComponent<EarthMatterIdentity>()?.RetireTransientRepresentation();
            }
            GetComponent<EarthMatterIdentity>()?.RetireTransientRepresentation();
            HideFracturePieces();
            gameObject.SetActive(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_fractured || collision.contactCount == 0) return;
            EarthWall otherWall = collision.collider != null
                ? collision.collider.GetComponentInParent<EarthWall>()
                : null;
            EarthWallPiece otherPiece = collision.collider != null
                ? collision.collider.GetComponent<EarthWallPiece>()
                : null;
            if (otherWall == null) otherWall = otherPiece?.Owner;
            if (otherWall == null || otherWall == this) return;
            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = _body.linearVelocity.sqrMagnitude > 0.01f
                ? _body.linearVelocity.normalized
                : -contact.normal;
            ApplyStructureImpact(contact.point, direction, collision.impulse.magnitude);
        }

        private float MinimumEmergenceSeconds => _profile != null ? _profile.MinimumEmergenceSeconds : 0.36f;
        private float MaximumEmergenceSeconds => _profile != null ? _profile.MaximumEmergenceSeconds : 0.92f;
        private float AutomaticCrackDelaySeconds => _profile != null ? _profile.AutomaticCrackDelaySeconds : 0f;
        private float FractureWaveSeconds => _profile != null ? _profile.FractureWaveSeconds : 0.26f;
        private float CohesionDecaySeconds => _profile != null ? _profile.CohesionDecaySeconds : 2.8f;
        private float DebrisRestSeconds => _profile != null ? _profile.DebrisRestSeconds : 1.35f;
        private float DebrisShrinkSeconds => _profile != null ? _profile.DebrisShrinkSeconds : 1.25f;
        private bool ShrinkDetachedStructuralPieces =>
            _profile != null && _profile.ShrinkDetachedStructuralPieces;
        private float MinimumRockImpactImpulse => _profile != null ? _profile.MinimumRockImpactImpulse : 55f;
        private float WallSlideDrag => _profile != null ? _profile.WallSlideDrag : 0.72f;
        private float MaximumSlideSpeed => _profile != null ? _profile.MaximumSlideSpeed : 14f;
        private float CohesionImpulsePerMass => _profile != null ? _profile.CohesionImpulsePerMass : 0.12f;
        private float ImpactDamageMultiplier => _profile != null ? _profile.ImpactDamageMultiplier : 0.92f;
        private float ExcessImpulseRelease => _profile != null ? _profile.ExcessImpulseRelease : 0.18f;
        private float FoundationStrengthMultiplier => _profile != null ? _profile.FoundationStrengthMultiplier : 1.45f;
        private float PlanetaryDebrisAcceleration => _profile != null ? _profile.PlanetaryDebrisAcceleration : 11.5f;
        private float MinimumChordEmbedDepth => _profile != null ? _profile.MinimumChordEmbedDepth : 0.42f;
        private float SurfaceTolerance => _profile != null ? _profile.SurfaceTolerance : 0.06f;
        private float VisibleVoxelSafetyDepth => _profile != null ? _profile.VisibleVoxelSafetyDepth : 0.55f;
        private float MagicFieldSlideDrag => _profile != null ? _profile.MagicFieldSlideDrag : 0.16f;
        private uint CurrentStructureTick => SourceTick +
                                             (uint)Mathf.Max(0, Mathf.RoundToInt(_fractureElapsed * 60f));

        private static float Hash01(uint seed, int index)
        {
            uint value = seed ^ ((uint)(index + 1) * 0x9E3779B9u);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }

    [DisallowMultipleComponent]
    public sealed class EarthWallPiece : EarthPieceRuntime
    {
    }
}
