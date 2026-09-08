from pathlib import Path
root=Path(__file__).resolve().parents[2]
stage=Path(__file__).parent/'Modified'
def edit(path, mutate):
    src=root/path
    before=src.read_text(encoding='utf-8-sig')
    after=mutate(before)
    assert before!=after,path
    out=stage/path
    out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(after,encoding='utf-8')
    original=Path(__file__).parent/'Original'/path
    original.parent.mkdir(parents=True,exist_ok=True)
    original.write_text(before,encoding='utf-8')
def replace(s,a,b):
    assert a in s,a[:150]
    return s.replace(a,b,1)
def arena(s):
    s=replace(s,'        private int _repairStartReleased;','''        private int _repairStartReleased;
        private int _repairRequestedCount;
        private int _repairFlyingPiece = -1;
        private float _repairFlightTime, _repairFlightDuration;
        private Vector3 _repairFlightStart;
        private Quaternion _repairRotationStart;
        public int RepairFlyingPieceIndex => _repairFlyingPiece;
        public bool HasPendingMagicRepair => _repairFlyingPiece >= 0 ||
            (_repairRequestedCount > _repairStartReleased - _releasedCount && _repairStartReleased > 0);
        private static readonly ProfilerMarker RepairFlightMarker =
            new ProfilerMarker("Elemental.Earth.ArenaRepair.Flight");''')
    s=replace(s,'            _repairStartReleased = 0;\n            int desiredReleased','            CancelMagicRepair();\n            int desiredReleased')
    start=s.index('        public bool SetMagicRepairProgress(float phase01)')
    end=s.index('        public bool TriggerMeteorImpact',start)
    s=s[:start]+'''        public bool SetMagicRepairProgress(float phase01)
        {
            if (!repairable || !_fractured || _releasedCount <= 0) return false;
            if (_repairStartReleased <= 0) _repairStartReleased = _releasedCount;
            _repairRequestedCount = Mathf.Max(_repairRequestedCount, Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Clamp01(phase01) * _repairStartReleased), 0, _repairStartReleased));
            return true;
        }

        public void CancelMagicRepair()
        {
            if (_repairFlyingPiece >= 0)
            {
                int index = _repairFlyingPiece;
                Rigidbody body = _pieceBodies[index];
                if (body != null && _released[index] && !_shattered[index])
                {
                    body.isKinematic = false;
                    body.detectCollisions = true;
                    body.WakeUp();
                    if (_pieceGravity[index] != null) _pieceGravity[index].enabled = true;
                }
            }
            _repairFlyingPiece = -1;
            _repairStartReleased = _repairRequestedCount = 0;
        }

        private void FixedUpdate() => TickMagicRepair(Time.fixedDeltaTime);

        public void TickMagicRepair(float deltaTime)
        {
            if (!_fractured || !HasPendingMagicRepair || deltaTime <= 0f) return;
            using (RepairFlightMarker.Auto())
            {
                if (_repairFlyingPiece < 0)
                {
                    int next = FindReleasedPiece();
                    if (next < 0) { _repairRequestedCount = _repairStartReleased - _releasedCount; return; }
                    _repairFlyingPiece = next;
                    Rigidbody nextBody = _pieceBodies[next];
                    _repairFlightStart = nextBody.position;
                    _repairRotationStart = nextBody.rotation;
                    _repairFlightTime = 0f;
                    _repairFlightDuration = EarthRepairFlight.Duration(
                        Vector3.Distance(nextBody.position, RepairRestPosition(next)));
                    nextBody.linearVelocity = nextBody.angularVelocity = Vector3.zero;
                    nextBody.isKinematic = true;
                    nextBody.detectCollisions = false;
                    if (_pieceGravity[next] != null) _pieceGravity[next].enabled = false;
                }
                int index = _repairFlyingPiece;
                if (_shattered[index] || !_released[index]) { CancelMagicRepair(); return; }
                Rigidbody body = _pieceBodies[index];
                _repairFlightTime += deltaTime;
                float phase = EarthRepairFlight.Phase(_repairFlightTime, _repairFlightDuration);
                Vector3 target = RepairRestPosition(index);
                quaternion rotation = _pieceDefinitions[index].RestLocalRotation;
                Quaternion localRotation = new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
                Quaternion targetRotation = pieces[index].parent != null
                    ? pieces[index].parent.rotation * localRotation : localRotation;
                body.MovePosition(Vector3.LerpUnclamped(_repairFlightStart, target, phase));
                body.MoveRotation(Quaternion.SlerpUnclamped(_repairRotationStart, targetRotation, phase));
                if (_repairFlightTime < _repairFlightDuration) return;
                // Only this already-arrived cell may commit its original graph pose.
                ReattachPiece(index);
                _repairFlyingPiece = -1;
                materialFeedback?.Emit(EarthMaterialFeedbackKind.RepairSeat, target,
                    coordinateRoot.up, 0.7f, 0.4f, structureId, _generation);
                if (_releasedCount != 0) return;
                materialFeedback?.Emit(EarthMaterialFeedbackKind.RepairComplete,
                    intactRenderer.bounds.center, coordinateRoot.up, 1f,
                    Mathf.Min(3f, intactRenderer.bounds.extents.magnitude), structureId, _generation);
                ResetToIntact();
            }
        }

        private Vector3 RepairRestPosition(int index)
        {
            float3 value = _pieceDefinitions[index].RestLocalPosition;
            Vector3 local = new Vector3(value.x, value.y, value.z);
            return pieces[index].parent != null ? pieces[index].parent.TransformPoint(local) : local;
        }

'''+s[end:]
    s=replace(s,'            _fractured = false;\n            _releasedCount = 0;','            _repairFlyingPiece = -1;\n            _repairRequestedCount = 0;\n            _fractured = false;\n            _releasedCount = 0;')
    return s
edit(Path('Assets/Elemental/Runtime/Physics/EarthArenaStructure.cs'),arena)
edit(Path('Assets/Elemental/Runtime/Physics/EarthArenaPiece.cs'),lambda s:replace(s,'public bool IsEarthTargetValid => owner != null && owner.IsPieceReleased(pieceIndex) &&','public bool IsEarthTargetValid => owner != null && owner.RepairFlyingPieceIndex != pieceIndex && owner.IsPieceReleased(pieceIndex) &&'))
edit(Path('Assets/Elemental/Runtime/World/MagicExecutor.cs'),lambda s:replace(s,'        public void CancelGravityWell()\n        {','        public void CancelGravityWell()\n        {\n            if (_gravityFractureSource is EarthArenaStructure arenaRepair) arenaRepair.CancelMagicRepair();'))
def reassembly(s):
    a=s.index('                if (orderIndex >= _targetPieceCount) continue;',s.index('            IsRepairing = true;'))
    b=s.index('            }\n            AdvanceOrderCursor();',a)
    s=s[:a]+s[b:]
    a=s.index('            int previousCount = _targetPieceCount;')
    b=s.index('            AdvanceOrderCursor();',a)
    s=s[:a]+'            _targetPieceCount = requestedCount;\n'+s[b:]
    a=s.index('                for (int index = 0; index < _available.Length; index++)',s.index('        public void TickRepair'))
    b=s.index('\n                if (_targetPieceCount',a)
    s=s[:a]+'''                if (activePiece >= 0)
                {
                    if (!TryAcquireOrderedPiece(_nextOrderSlot, _tick))
                    { Interrupt(EarthRepairInterruptReason.TargetInvalidated, _tick); return; }
                    EarthPieceRuntime piece = _structure.GetPieceRuntime(activePiece);
                    if (piece == null || !piece.gameObject.activeSelf ||
                        piece.Generation != _generation || piece.Body == null)
                    { Interrupt(EarthRepairInterruptReason.TargetInvalidated, _tick); return; }
                    UpdatePiece(activePiece, piece, true, dt);
                    if (!IsRepairing) return;
                }
'''+s[b:]
    return s
edit(Path('Assets/Elemental/Runtime/Physics/EarthReassemblyController.cs'),reassembly)
def solver(s):
    return replace(s,'        public const uint DefaultDuplicateWindowTicks = 3u;','''        // Crushing is a directional load case, not extra damage or a bigger
        // ordinary stone impulse. A massive rock landing above the centre of mass
        // transfers support to the full dynamic body even below the throw KO gate.
        public static bool IsHeavyCrush(float sourceMass, float targetMass, float closingSpeed,
            float3 incomingDirection, float3 up, float contactHeight)
        {
            if (!float.IsFinite(sourceMass) || !float.IsFinite(targetMass) ||
                !float.IsFinite(closingSpeed) || !float.IsFinite(contactHeight) ||
                targetMass <= 0f || sourceMass < math.max(100f, targetMass * 2f) || contactHeight < 0f)
                return false;
            float downward = -math.dot(math.normalizesafe(incomingDirection), math.normalizesafe(up));
            return downward >= .65f && closingSpeed * downward >= 2.5f;
        }

        public static float3 OrientIncomingContactVelocity(float3 reported, float3 receiverContactNormal) =>
            math.dot(reported, receiverContactNormal) < 0f ? -reported : reported;

        public const uint DefaultDuplicateWindowTicks = 3u;''')
edit(Path('Assets/Elemental/Simulation/Combat/EarthCharacterImpact.cs'),solver)
def target(s):
    s=replace(s,'            return ApplyImpact(point, direction, impulse, sourceKind, sourceStableId,\n                closingSpeed, damageOverride: damageOverride, calibratedStone: true);','''            Vector3 up = _motor != null ? _motor.LocalUp : transform.up;
            bool crush = EarthCharacterImpactSolver.IsHeavyCrush(sourceMass, targetBody.mass, closingSpeed,
                ToFloat3(direction), ToFloat3(up), Vector3.Dot(point - targetBody.worldCenterOfMass, up));
            return ApplyImpact(point, direction, impulse, sourceKind, sourceStableId,
                closingSpeed, damageOverride: damageOverride, calibratedStone: true, heavyCrush: crush);''')
    s=replace(s,'            bool calibratedStone = false)','            bool calibratedStone = false,\n            bool heavyCrush = false)')
    s=replace(s,'                Remember(sourceStableId, tick, impactTime);','''                if (heavyCrush && stoneImpact)
                    response = EarthCharacterImpactResponse.RecoverableKnockdown;
                Remember(sourceStableId, tick, impactTime);''')
    s=replace(s,'                Vector3 up = transform.position.sqrMagnitude > 0.1f\n                    ? transform.position.normalized\n                    : transform.up;','                Vector3 up = _motor != null ? _motor.LocalUp : transform.up;')
    s=replace(s,'                        : collision.relativeVelocity;\n                    if (incoming.sqrMagnitude < .0001f) incoming = -contact.normal;','''                        : (Vector3)EarthCharacterImpactSolver.OrientIncomingContactVelocity(
                            ToFloat3(collision.relativeVelocity), ToFloat3(contact.normal));
                    if (incoming.sqrMagnitude < .0001f) incoming = contact.normal;''')
    s=replace(s,'            EarthMatterIdentity matter = collider.GetComponentInParent<EarthMatterIdentity>();','''            IEarthPhysicalTarget structural = collider.GetComponentInParent<EarthArenaPiece>();
            structural ??= collider.GetComponentInParent<EarthPieceRuntime>();
            structural ??= collider.GetComponentInParent<EarthPlatformPiece>();
            if (structural != null && structural.Body != null && !structural.Body.isKinematic)
            {
                sourceKind = EarthCharacterImpactSourceKind.LooseStone;
                sourceStableId = structural.StableEarthId;
                return;
            }
            EarthMatterIdentity matter = collider.GetComponentInParent<EarthMatterIdentity>();''')
    return s
edit(Path('Assets/Elemental/Runtime/Characters/EarthCharacterImpactTarget.cs'),target)
print('Prepared repair/crush runtime files outside Assets')
