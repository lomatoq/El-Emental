from pathlib import Path
root=Path(__file__).resolve().parents[2]
stage=Path(__file__).parent/'Repair2'
def edit(path,change):
    s=(root/path).read_text(encoding='utf-8-sig');changed=change(s);assert changed!=s,path
    p=stage/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(changed,encoding='utf-8')
    p=Path(__file__).parent/'Repair2Original'/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(s,encoding='utf-8')
def rep(s,a,b):
    assert a in s,a[:150]
    return s.replace(a,b,1)
def arena(s):
    s=rep(s,'        private bool[] _released = Array.Empty<bool>();','''        private bool[] _released = Array.Empty<bool>();
        private bool[] _repairReserved = Array.Empty<bool>();
        public bool IsPieceReservedForRepair(int index) => index >= 0 && index < _repairReserved.Length && _repairReserved[index];''')
    s=rep(s,'            _released = new bool[pieceCount];','            _released = new bool[pieceCount];\n            _repairReserved = new bool[pieceCount];')
    s=rep(s,'            if (_repairStartReleased <= 0) _repairStartReleased = _releasedCount;','''            if (_repairStartReleased <= 0)
            {
                _repairStartReleased = _releasedCount;
                for (int index = 0; index < _released.Length; index++)
                {
                    if (!_released[index] || _shattered[index] || _pieceTargets[index].HasMagicOwner) continue;
                    _repairReserved[index] = true;
                    Rigidbody waiting = _pieceBodies[index];
                    if (!waiting.isKinematic) waiting.linearVelocity = waiting.angularVelocity = Vector3.zero;
                    waiting.isKinematic = true;
                    waiting.detectCollisions = false;
                    if (_pieceGravity[index] != null) _pieceGravity[index].enabled = false;
                }
            }''')
    a=s.index('            if (_repairFlyingPiece >= 0)',s.index('        public void CancelMagicRepair()'))
    b=s.index('            _repairFlyingPiece = -1;',a)
    s=s[:a]+'''            for (int index = 0; index < _repairReserved.Length; index++)
            {
                if (!_repairReserved[index]) continue;
                _repairReserved[index] = false;
                Rigidbody body = _pieceBodies[index];
                if (body != null && _released[index] && !_shattered[index])
                {
                    body.isKinematic = false;
                    body.detectCollisions = true;
                    body.WakeUp();
                    if (_pieceGravity[index] != null) _pieceGravity[index].enabled = true;
                }
            }
'''+s[b:]
    s=rep(s,'                    nextBody.linearVelocity = nextBody.angularVelocity = Vector3.zero;',
        '                    if (!nextBody.isKinematic) nextBody.linearVelocity = nextBody.angularVelocity = Vector3.zero;')
    s=rep(s,'                _released[index] = false;','                _released[index] = false;\n                _repairReserved[index] = false;')
    s=rep(s,'if (_released[index] && !_shattered[index] && CanSeatOnAttachedSupport(index)) return index;',
        'if (_released[index] && _repairReserved[index] && !_shattered[index] && CanSeatOnAttachedSupport(index)) return index;')
    s=rep(s,'            _released[index] = false;\n            _releasedCount--;','            _released[index] = false;\n            _repairReserved[index] = false;\n            _releasedCount--;')
    return s
edit(Path('Assets/Elemental/Runtime/Physics/EarthArenaStructure.cs'),arena)
edit(Path('Assets/Elemental/Runtime/Physics/EarthArenaPiece.cs'),lambda s:rep(rep(s,
    '        public Rigidbody Body => body;','        public Rigidbody Body => body;\n        public bool HasMagicOwner => _hasMagicOwner;'),
    'owner.RepairFlyingPieceIndex != pieceIndex','!owner.IsPieceReservedForRepair(pieceIndex)'))

def wall(s):
    s=rep(s,'        private int _nextOrderSlot;','''        private int _nextOrderSlot;
        private int _flightIndex = -1;
        private Vector3 _flightStartPosition;
        private Quaternion _flightStartRotation;
        private float _flightDuration;''')
    s=rep(s,'            _nextOrderSlot = 0;','            _nextOrderSlot = 0;\n            _flightIndex = -1;')
    s=rep(s,'                    continue;\n                }\n            }\n            AdvanceOrderCursor();','''                    continue;
                }
                if (!TryAcquireOrderedPiece(orderIndex, tick))
                { Interrupt(EarthRepairInterruptReason.TargetInvalidated, tick); return false; }
            }
            AdvanceOrderCursor();''')
    s=rep(s,'''            PieceCaptured?.Invoke(new EarthPieceCapturedEvent(
                tick, _structure.State.Id, _pieceDefinitions[pieceIndex].Id, orderIndex));''','''            Rigidbody waiting = piece.Body;
            if (!waiting.isKinematic) waiting.linearVelocity = waiting.angularVelocity = Vector3.zero;
            waiting.isKinematic = true;
            waiting.detectCollisions = false;''')
    s=rep(s,'                piece?.ReleaseFromRepair();','''                if (piece != null && piece.Body != null)
                { piece.Body.isKinematic = false; piece.Body.detectCollisions = true; piece.Body.WakeUp(); }
                piece?.ReleaseFromRepair();''')
    needle='''            float3 offset = EarthRepairPoseSolver.StagingOffset('''
    insert='''            if (phase == EarthPiecePhase.Captured)
            {
                if (_flightIndex != index)
                {
                    _flightIndex = index;
                    _flightStartPosition = body.position;
                    _flightStartRotation = body.rotation;
                    _flightDuration = EarthRepairFlight.Duration(Vector3.Distance(body.position, restPosition));
                    _phaseElapsed[index] = 0f;
                    PieceCaptured?.Invoke(new EarthPieceCapturedEvent(
                        _tick, _structure.State.Id, _pieceDefinitions[index].Id, _nextOrderSlot));
                }
                _phaseElapsed[index] += dt;
                float travel = EarthRepairFlight.Phase(_phaseElapsed[index], _flightDuration);
                body.MovePosition(Vector3.LerpUnclamped(_flightStartPosition, restPosition, travel));
                body.MoveRotation(Quaternion.SlerpUnclamped(_flightStartRotation, restRotation, travel));
                CurrentPiecePositionError = Vector3.Distance(body.position, restPosition);
                CurrentPieceAngleErrorDegrees = Quaternion.Angle(body.rotation, restRotation);
                if (_phaseElapsed[index] < _flightDuration) return;
                body.position = restPosition;
                body.rotation = restRotation;
                _phaseElapsed[index] = 0f;
                _settle[index] = default;
                _progress[index] = default;
                _progress[index].BestError = float.MaxValue;
                _structure.SetPiecePhase(index, EarthPiecePhase.Aligning, _tick);
                BeginReformingBonds(index);
                PrepareTerrainSeating(index, piece, restPosition, restRotation);
                body.isKinematic = false;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                body.detectCollisions = true;
                return;
            }
            float3 offset = EarthRepairPoseSolver.StagingOffset('''
    s=rep(s,needle,insert)
    # The PD input is a centre of mass; the rest pose is a Rigidbody origin.
    s=rep(s,'                ToFloat3(targetPosition),','                ToFloat3(targetPosition + restRotation * Vector3.Scale(body.centerOfMass, body.transform.lossyScale)),')
    return s
edit(Path('Assets/Elemental/Runtime/Physics/EarthReassemblyController.cs'),wall)
edit(Path('Assets/Elemental/Tests/PlayMode/OuterStoneRingRuntimeTests.cs'),lambda s:rep(s,
    '                if (index != flying) Assert.That(pieces[index].GetComponent<Rigidbody>().isKinematic, Is.False);',
    '''                if (index != flying)
                {
                    Assert.That(pieces[index].GetComponent<Rigidbody>().isKinematic, Is.True, "Waiting cells must stay at their captured poses.");
                    Assert.That(structure.IsPieceReservedForRepair(index), Is.True);
                }'''))
print('Prepared reserved waiting cells and bounded wall flight with final PD settling')
