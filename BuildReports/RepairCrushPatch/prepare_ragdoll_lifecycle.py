exec((__import__('pathlib').Path(__file__).parent/'prepare_repair2.py').read_text())
edit(Path('Assets/Elemental/Runtime/Characters/HumanoidRagdollRig.cs'),lambda s:rep(s,
    '        public bool IsRagdollActive { get; private set; }',
    '''        public EarthCharacterImpactTarget ImpactReceiver => motorRootBody != null
            ? motorRootBody.GetComponent<EarthCharacterImpactTarget>() : null;
        public bool IsRagdollActive { get; private set; }'''))
edit(Path('Assets/Elemental/Runtime/Characters/EarthStoneCharacterContact.cs'),lambda s:rep(rep(s,
    'collision.collider.GetComponentInParent<EarthCharacterImpactTarget>()', 'ResolveTarget(collision.collider)'),
    '        public static bool Deliver(',
    '''        public static EarthCharacterImpactTarget ResolveTarget(Collider collider)
        {
            if (collider == null) return null;
            EarthCharacterImpactTarget target = collider.GetComponentInParent<EarthCharacterImpactTarget>();
            if (target != null) return target;
            // The physical visual rig is deliberately unparented at full handoff.
            // Keep its explicit motor-owner link for subsequent damage and death.
            HumanoidRagdollRig rig = collider.GetComponentInParent<HumanoidRagdollRig>();
            return rig != null ? rig.ImpactReceiver : null;
        }

        public static bool Deliver('''))
edit(Path('Assets/Elemental/Runtime/Characters/EarthTypedCombatProjectile.cs'),lambda s:rep(s,
    'hit.GetComponentInParent<EarthCharacterImpactTarget>()', 'EarthStoneCharacterContact.ResolveTarget(hit)'))
def fragment(s):
    s=rep(s,'collision.collider.GetComponentInParent<EarthCharacterImpactTarget>()',
        'EarthStoneCharacterContact.ResolveTarget(collision.collider)')
    return rep(s,'            _executor?.HandleFragmentImpact(this, collision, impulse);',
        '            if (!IsHeld) EarthStoneCharacterContact.Deliver(collision, targetBody, FragmentId);\n            _executor?.HandleFragmentImpact(this, collision, impulse);')
edit(Path('Assets/Elemental/Runtime/Physics/EarthFragment.cs'),fragment)
edit(Path('Assets/Elemental/Runtime/World/MagicExecutor.cs'),lambda s:s.replace(
    'collision.collider.GetComponentInParent<EarthCharacterImpactTarget>()', 'EarthStoneCharacterContact.ResolveTarget(collision.collider)').replace(
    'hitCollider.GetComponentInParent<EarthCharacterImpactTarget>()', 'EarthStoneCharacterContact.ResolveTarget(hitCollider)'))
def lifecycle(s):
    s=rep(s,'            if (!_built) BuildProxies();','            if (_built && !ValidateRuntimeBindings()) _built = false;\n            if (!_built) BuildProxies();')
    s=rep(s,'            if (!_built || _suspended || !isActiveAndEnabled ||','            if (!_built || _suspended || !isActiveAndEnabled || !ValidateRuntimeBindings() ||')
    s=rep(s,'        private void FixedUpdate()\n        {\n            if (!_built || _suspended) return;',
        '        private void FixedUpdate()\n        {\n            if (!_built || _suspended || !ValidateRuntimeBindings()) return;')
    s=rep(s,'            if (!_built || _suspended || Time.deltaTime <= 0f) return;',
        '            if (!_built || _suspended || Time.deltaTime <= 0f || !ValidateRuntimeBindings()) return;')
    s=rep(s,'        public void ResetToAnimation()\n        {\n            if (!_built) return;',
        '        public void ResetToAnimation()\n        {\n            if (!_built || !ValidateRuntimeBindings()) return;')
    s=rep(s,'        private void OnDisable() { if (_built && !_suspended) ResetToAnimation(); }\n        private void OnDestroy()\n        {\n            if (_proxyRoot != null) Destroy(_proxyRoot);\n        }',
    '''        private bool ValidateRuntimeBindings()
        {
            if (!_built) return false;
            bool complete = _motionRoot != null && _proxyRoot != null;
            for (int i = 0; complete && i < Count; i++)
                complete = _bones[i] != null && _bodies[i] != null && _anchors[i] != null && _joints[i] != null;
            if (complete) return true;
            // Scene teardown can destroy the separate proxy/rig roots before this
            // component's final LateUpdate. Invalidate this lifetime atomically;
            // a subsequent explicit Configure rebuilds every proxy together.
            ReleaseRuntimeBindings();
            return false;
        }

        private void ReleaseRuntimeBindings()
        {
            _built = false;
            _suspended = true;
            _hasPose = false;
            for (int i = 0; i < Count; i++)
            {
                _active[i] = _wrote[i] = false;
                _bodies[i] = _anchors[i] = null;
                _joints[i] = null;
            }
            if (_proxyRoot != null)
            {
                if (Application.isPlaying) Destroy(_proxyRoot);
                else DestroyImmediate(_proxyRoot);
            }
            _proxyRoot = null;
        }

        private void OnDisable() { if (_built && !_suspended) ResetToAnimation(); }
        private void OnDestroy() => ReleaseRuntimeBindings();''')
    return s
edit(Path('Assets/Elemental/Runtime/Characters/HumanoidLocalizedPhysicsResponse.cs'),lifecycle)
def tests(s):
    s=rep(s,'            PrepareRegionalGround();\n            ResetRegionalActor();\n            yield return new WaitForSeconds(.8f);\n            IEarthPhysicalTarget source = null;',
    '''            Assert.That(_target.FighterId, Is.EqualTo(EarthDuelFighterId.Bot));
            EarthMvpDuelController duel = Find<EarthMvpDuelController>();
            float healthBefore = duel.Match.BotHealth;
            PrepareRegionalGround();
            ResetRegionalActor();
            yield return new WaitForSeconds(.8f);
            IEarthPhysicalTarget source = null;''')
    s=rep(s,'            Assert.That(_physics.HasActiveResponse || _target.IsRecoverablyKnockedDown, Is.True);',
    '''            Assert.That(_physics.HasActiveResponse || _target.IsRecoverablyKnockedDown, Is.True);
            Assert.That(duel.Match.BotHealth, Is.LessThan(healthBefore), "The actual large cell must lower bot health, not only publish a reaction event.");''')
    s=rep(s,'response={_accepted.Response}, events={_acceptedEvents - before}, direction={_accepted.Direction}");',
        'response={_accepted.Response}, health={healthBefore:F2}->{duel.Match.BotHealth:F2}, events={_acceptedEvents - before}, direction={_accepted.Direction}");')
    insert='''        [UnityTest]
        public IEnumerator RepeatedActualStonesDamageUnparentedRagdollAndKillBot()
        {
            Assert.That(_target.FighterId, Is.EqualTo(EarthDuelFighterId.Bot));
            EarthMvpDuelController duel = Find<EarthMvpDuelController>();
            HumanoidRagdollRig rig = _target.GetComponentInChildren<HumanoidRagdollRig>(true);
            PrepareRegionalGround();
            ResetRegionalActor();
            yield return new WaitForSeconds(.8f);
            var evidence = new StringBuilder("Actual repeated 600kg stones, 25m/s, production Bot and current physical bone positions.\\n");
            int hits = 0;
            for (; hits < 8 && duel.Match.BotHealth > 0f; hits++)
            {
                Vector3 aim = _physics.Bone(1).position;
                Vector3 start = aim + _target.transform.forward * 1.5f + _motor.LocalUp * .4f;
                if (hits > 0)
                {
                    Assert.That(rig.transform.IsChildOf(_target.transform), Is.False, "Repeat must exercise the genuinely detached physical rig.");
                    var chestCollider = _physics.Bone(1).GetComponent<Collider>();
                    Assert.That(EarthStoneCharacterContact.ResolveTarget(chestCollider), Is.SameAs(_target));
                }
                float health = duel.Match.BotHealth;
                int before = _acceptedEvents;
                _sourceId++;
                _stone.Initialize(_sourceId, null, start, .2f, 600f);
                _typed.Arm(_stone, EarthCharacterImpactSourceKind.LooseStone);
                _stone.LaunchProjectile((aim - start).normalized, 25f, null);
                UnityEngine.Physics.SyncTransforms();
                double deadline = Time.realtimeSinceStartupAsDouble + .7d;
                while (_acceptedEvents == before && Time.realtimeSinceStartupAsDouble < deadline) yield return _fixed;
                Assert.That(_acceptedEvents - before, Is.EqualTo(1), $"Physical repeat {hits} never reached the standing or detached bot.");
                Assert.That(duel.Match.BotHealth, Is.LessThan(health), $"Physical repeat {hits} must actually inflict damage.");
                evidence.AppendLine($"hit={hits + 1}, health={health:F2}->{duel.Match.BotHealth:F2}, response={_accepted.Response}, direction={_accepted.Direction}");
                yield return _fixed;
            }
            Assert.That(duel.Match.BotHealth, Is.Zero);
            Assert.That(duel.BotKnockoutCount, Is.EqualTo(1));
            Assert.That(duel.Match.PlayerScore, Is.EqualTo(1));
            Assert.That(_accepted.Response, Is.EqualTo(EarthCharacterImpactResponse.Knockout));
            Assert.That(rig.IsRagdollActive, Is.True);
            File.WriteAllText(Path.Combine(Folder, "RepeatedActualStoneKill.txt"), evidence.ToString());
        }

'''
    return rep(s,'        private void PrewarmStone()',insert+'        private void PrewarmStone()')
edit(Path('Assets/Elemental/Tests/PlayMode/LocalPhysicsProductionAcceptanceTests.cs'),tests)
def lifecycle_test(s):
    needle='        private static EarthWorldResponseEvent Hit('
    insert='''        [UnityTest]
        public IEnumerator DestroyedBoneLifetimeStopsPoseWritesAndExplicitConfigureRebuilds()
        {
            var owner = new GameObject("Local response lifecycle owner");
            var skeleton = new GameObject("Disposable visible skeleton");
            try
            {
                var bones = new Transform[EarthLocalizedPhysicsResponse.BoneCount];
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject($"Bone{i}").transform;
                    bones[i].SetParent(skeleton.transform, false);
                }
                var response = owner.AddComponent<HumanoidLocalizedPhysicsResponse>();
                response.Configure(bones, owner.transform, null);
                yield return null;
                Assert.That(response.IsReady, Is.True);
                Object.Destroy(skeleton);
                yield return null;
                yield return null;
                Assert.That(response.IsReady, Is.False, "Lost skeleton must invalidate the entire proxy lifetime.");
                skeleton = new GameObject("Replacement visible skeleton");
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject($"ReplacementBone{i}").transform;
                    bones[i].SetParent(skeleton.transform, false);
                }
                response.Configure(bones, owner.transform, null);
                yield return null;
                Assert.That(response.IsReady, Is.True);
                Assert.That(response.Bone(0), Is.SameAs(bones[0]));
                var hit = Hit(812u, bones[0].position, EarthCharacterImpactResponse.Flinch);
                Assert.That(response.ApplyHit(in hit, 1f), Is.True);
            }
            finally { Object.Destroy(owner); Object.Destroy(skeleton); }
        }

'''
    return rep(s,needle,insert+needle)
edit(Path('Assets/Elemental/Tests/PlayMode/EarthLocalizedPhysicsRuntimeTests.cs'),lifecycle_test)
print('Prepared rig contact ownership, actual health/death proof and local-physics lifetime rebind')
