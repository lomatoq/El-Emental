from pathlib import Path
root=Path(__file__).resolve().parents[2]
stage=Path(__file__).parent/'ContactFix'
def edit(path,change):
    p=root/path;s=p.read_text(encoding='utf-8-sig');changed=change(s);assert changed!=s,path
    dest=stage/path;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(changed,encoding='utf-8')
    original=Path(__file__).parent/'ContactOriginal'/path;original.parent.mkdir(parents=True,exist_ok=True);original.write_text(s,encoding='utf-8')
edit(Path('Assets/Elemental/Runtime/Matter/EarthMatterIdentity.cs'),lambda s:s.replace(
    'if (_kernel != null && _kernel != kernel) MatterId = default;',
    'if (!ReferenceEquals(_kernel, kernel)) MatterId = default;'))
edit(Path('Assets/Elemental/Runtime/Physics/EarthDualMouseAbilityController.cs'),lambda s:s.replace(
    '                _punchStone.CompleteReintegration();',
    '                // This crest never submitted a subtractive terrain transaction.\n                _punchStone.MatterIdentity?.RetireTransientRepresentation();\n                _punchStone.CompleteReintegration();'))
edit(Path('Assets/Elemental/Runtime/Physics/EarthFragmentPool.cs'),lambda s:s.replace(
    '                if (!_fragments[index].gameObject.activeSelf)',
    '                if (!_fragments[index].gameObject.activeSelf && _fragments[index].CanReuseInactiveRepresentation)'))
edit(Path('Assets/Elemental/Runtime/Physics/EarthFragment.cs'),lambda s:s.replace(
    '        public Vector3 IncomingPhysicsVelocity => _prePhysicsVelocity;',
    '''        public Vector3 IncomingPhysicsVelocity => _prePhysicsVelocity;
        public bool CanReuseInactiveRepresentation
        {
            get
            {
                EarthMatterIdentity identity = _matterIdentity != null ? _matterIdentity : GetComponent<EarthMatterIdentity>();
                return !gameObject.activeSelf && (identity == null || !identity.TryRead(out EarthMatterRecord record) ||
                    record.Phase == EarthMatterPhase.Consumed);
            }
        }'''))
edit(Path('Assets/Elemental/Runtime/World/MagicExecutor.cs'),lambda s:s.replace(
    '''            Vector3 direction = fragment.Body.linearVelocity.sqrMagnitude > 0.0001f
                ? fragment.Body.linearVelocity.normalized
                : -contact.normal;''',
    '''            Vector3 incoming = characterTarget != null
                ? characterTarget.OrientIncomingStoneVelocity(collision.relativeVelocity, fragment.IncomingPhysicsVelocity)
                : fragment.IncomingPhysicsVelocity;
            Vector3 direction = incoming.sqrMagnitude > 0.0001f ? incoming.normalized : -contact.normal;'''))
for file,needle,source in [
    ('EarthArenaPiece.cs','            if (owner != null && IsEarthTargetValid)','body, StableEarthId'),
    ('EarthPieceRuntime.cs','            if (Owner == null || Time.frameCount - _lastImpactFrame < 2) return;','Body, StableEarthId')]:
    edit(Path('Assets/Elemental/Runtime/Physics')/file,lambda s,n=needle,src=source:s.replace(n,
        '            Elemental.Runtime.Characters.EarthStoneCharacterContact.Deliver(collision, '+src+');\n'+n))
edit(Path('Assets/Elemental/Runtime/Physics/EarthPlatformPiece.cs'),lambda s:s.replace(
    '''        private void OnCollisionEnter(Collision collision) =>
            Owner?.ReportPieceImpact(PieceIndex, collision);''',
    '''        private void OnCollisionEnter(Collision collision)
        {
            Elemental.Runtime.Characters.EarthStoneCharacterContact.Deliver(collision, Body, StableEarthId);
            Owner?.ReportPieceImpact(PieceIndex, collision);
        }'''))
edit(Path('Assets/Elemental/Runtime/Physics/EarthDestructibleDecorRock.cs'),lambda s:s.replace(
    '            float impulse = Mathf.Max(collision.impulse.magnitude, EarthMass * approach);',
    '            Elemental.Runtime.Characters.EarthStoneCharacterContact.Deliver(collision, body, StableEarthId);\n            float impulse = Mathf.Max(collision.impulse.magnitude, EarthMass * approach);'))
edit(Path('Assets/Elemental/Runtime/Physics/EarthRockDebrisPool.cs'),lambda s:s.replace(
    '        private void OnCollisionEnter(Collision collision)\n        {',
    '        private void OnCollisionEnter(Collision collision)\n        {\n            if (!_accreting && _gripCount == 0)\n                Elemental.Runtime.Characters.EarthStoneCharacterContact.Deliver(collision, _body, StableEarthId);'))
print('Prepared actual structure contact and pooled identity fixes')
