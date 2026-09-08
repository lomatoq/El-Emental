exec((__import__('pathlib').Path(__file__).parent/'prepare.py').read_text())

def platform(s):
    a=s.index('        private void UpdateRepair()')
    end=s.index('        private void CompletePhysicalRepair()',a)
    body=s[a:end]
    body=replace(body,'            int seated = 0;','            int seated = 0;\n            bool flyingPieceUpdated = false;')
    body=replace(body,'                body.isKinematic = true;\n                body.detectCollisions = false;\n','')
    body=replace(body,'                float speed = Mathf.Lerp(7f, 18f, _repairTarget01);','''                if (body.isKinematic && Vector3.Distance(body.position, targetPosition) <= .025f &&
                    Quaternion.Angle(body.rotation, targetRotation) <= .75f)
                { seated++; continue; }
                if (flyingPieceUpdated) continue;
                flyingPieceUpdated = true;
                body.isKinematic = true;
                body.detectCollisions = false;
                float speed = Mathf.Lerp(7f, 18f, _repairTarget01);''')
    return s[:a]+body+s[end:]
edit(Path('Assets/Elemental/Runtime/Physics/EarthPlatform.cs'),platform)

def tests(s):
    insert='''        [UnityTest]
        public IEnumerator HeavyFallingStoneCrushesPlayerIntoDynamicRagdoll() => HeavyFallingStone(EarthDuelFighterId.Player);

        [UnityTest]
        public IEnumerator HeavyFallingStoneCrushesBotIntoDynamicRagdoll() => HeavyFallingStone(EarthDuelFighterId.Bot);

        private IEnumerator HeavyFallingStone(EarthDuelFighterId fighter)
        {
            if (_target.FighterId != fighter)
            {
                _target.WorldResponseRequested -= OnResponse;
                foreach (GameObject root in _scene.GetRootGameObjects())
                foreach (EarthCharacterImpactTarget candidate in root.GetComponentsInChildren<EarthCharacterImpactTarget>())
                    if (candidate.FighterId == fighter) _target = candidate;
                Assert.That(_target.FighterId, Is.EqualTo(fighter));
                _target.WorldResponseRequested += OnResponse;
                _motor = _target.GetComponent<PlanetMotor>();
                _motor.ConfigureInputSource(_motor.gameObject.AddComponent<LocalPhysicsQaIdleInput>());
                _physics = _target.GetComponentInChildren<HumanoidRagdollRig>(true).LocalizedPhysics;
            }
            PrepareRegionalGround();
            ResetRegionalActor();
            yield return new WaitForSeconds(.8f);
            Assert.That(_motor.IsGrounded, Is.True);
            HumanoidRagdollRig rig = _target.GetComponentInChildren<HumanoidRagdollRig>(true);
            HumanoidRagdollBone[] bones = rig.GetComponentsInChildren<HumanoidRagdollBone>(true);
            Assert.That(bones.Length, Is.EqualTo(11));
            Vector3 up = _motor.LocalUp;
            Vector3 head = _physics.Bone(2).position;
            Vector3 initialCentre = Vector3.zero;
            foreach (var bone in bones) initialCentre += bone.Body.worldCenterOfMass / bones.Length;
            _sourceId++;
            _stone.Initialize(_sourceId, null, head + up * 1.55f, .65f, 600f);
            _typed.Arm(_stone, EarthCharacterImpactSourceKind.LooseStone);
            _stone.LaunchProjectile(-up, 6f, null);
            int before = _acceptedEvents;
            UnityEngine.Physics.SyncTransforms();
            double deadline = Time.realtimeSinceStartupAsDouble + 2d;
            while (_acceptedEvents == before && Time.realtimeSinceStartupAsDouble < deadline) yield return _fixed;
            Assert.That(_acceptedEvents - before, Is.EqualTo(1), "Actual falling rock must cause one canonical impact.");
            Assert.That(_target.LastResponse, Is.EqualTo(EarthCharacterImpactResponse.RecoverableKnockdown));
            Assert.That(Vector3.Dot((Vector3)_accepted.ImpulseDirection, up), Is.LessThan(-.65f), "Crush handoff must follow incoming downward travel.");
            Assert.That(rig.IsRagdollActive, Is.True);
            Assert.That(rig.DynamicBodyCount, Is.EqualTo(11));
            Assert.That(_target.GetComponent<CapsuleCollider>().enabled, Is.False, "Static motor capsule must release its blocking support.");
            foreach (var bone in bones)
            {
                Assert.That(bone.Body.isKinematic, Is.False);
                Assert.That(bone.Shape.enabled, Is.True);
                Assert.That(bone.Body.detectCollisions, Is.True);
            }
            for (int frame = 0; frame < 12; frame++) yield return _fixed;
            Vector3 finalCentre = Vector3.zero;
            foreach (var bone in bones) finalCentre += bone.Body.worldCenterOfMass / bones.Length;
            float downwardDisplacement = Vector3.Dot(initialCentre - finalCentre, up);
            Assert.That(downwardDisplacement, Is.GreaterThan(.06f), "Visible full rig must actually compress downward under falling rock.");
            File.WriteAllText(Path.Combine(Folder, $"HeavyCrush-{fighter}.txt"),
                $"Actual 600kg rock at6m/s; fighter={fighter}; response={_target.LastResponse}; dynamicBodies={rig.DynamicBodyCount}; downwardRigDisplacement={downwardDisplacement:F4}m; direction={(Vector3)_accepted.ImpulseDirection}; events={_acceptedEvents - before}");
        }

'''
    return replace(s,'        private void PrewarmStone()',insert+'        private void PrewarmStone()')
edit(Path('Assets/Elemental/Tests/PlayMode/LocalPhysicsProductionAcceptanceTests.cs'),tests)

def outer(s):
    # Existing exact restoration assertions now observe the asynchronous contract.
    s=s.replace('Assert.That(structure.SetMagicRepairProgress(1f), Is.True);',
        'Assert.That(structure.SetMagicRepairProgress(1f), Is.True);\n                yield return AwaitRepair(structure);')
    s=replace(s,'                    Assert.That(repaired, Is.True);','                    Assert.That(repaired, Is.True);\n                    yield return AwaitRepair(structure);')
    s=replace(s,'                    Assert.That(structure.SetMagicRepairProgress(step / 10f), Is.True);',
        '                    Assert.That(structure.SetMagicRepairProgress(step / 10f), Is.True);\n                    yield return AwaitRepair(structure);')
    s=replace(s,'        [UnityTearDown]','''        private static IEnumerator AwaitRepair(EarthArenaStructure structure)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30d;
            while (structure.HasPendingMagicRepair && Time.realtimeSinceStartupAsDouble < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(structure.HasPendingMagicRepair, Is.False, "Sequential material repair timed out.");
        }

        [UnityTest]
        public IEnumerator RepairFliesOneCellAtATimeAndReleasePreservesCurrentPhysicalPose()
        {
            EarthArenaStructure structure = _ring.GetComponentsInChildren<EarthArenaStructure>(true)[0];
            Assert.That(structure.SetMagicDisassemblyProgress(1f, structure.transform.position, Vector3.zero), Is.True);
            Transform[] pieces = Field<Transform[]>(structure, "pieces");
            for (int index = 0; index < pieces.Length; index++)
            {
                Rigidbody body = pieces[index].GetComponent<Rigidbody>();
                body.position += structure.transform.forward * 2f;
                body.linearVelocity = Vector3.zero;
            }
            UnityEngine.Physics.SyncTransforms();
            int released = structure.ReleasedPieceCount;
            Assert.That(structure.SetMagicRepairProgress(1f), Is.True);
            Assert.That(structure.ReleasedPieceCount, Is.EqualTo(released), "Request must not teleport/weld any cell.");
            yield return new WaitForFixedUpdate();
            int flying = structure.RepairFlyingPieceIndex;
            Assert.That(flying, Is.GreaterThanOrEqualTo(0));
            Vector3 flightStart = pieces[flying].position;
            for (int frame = 0; frame < 4; frame++) yield return new WaitForFixedUpdate();
            Assert.That(structure.RepairFlyingPieceIndex, Is.EqualTo(flying));
            Assert.That(Vector3.Distance(pieces[flying].position, flightStart), Is.GreaterThan(.02f));
            Assert.That(structure.ReleasedPieceCount, Is.EqualTo(released), "No second cell may seat ahead of the flying support cell.");
            for (int index = 0; index < pieces.Length; index++)
                if (index != flying) Assert.That(pieces[index].GetComponent<Rigidbody>().isKinematic, Is.False);
            Vector3 interruptedPose = pieces[flying].position;
            structure.CancelMagicRepair();
            Assert.That(structure.HasPendingMagicRepair, Is.False);
            Assert.That(pieces[flying].GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(pieces[flying].GetComponent<Rigidbody>().detectCollisions, Is.True);
            Assert.That(Vector3.Distance(pieces[flying].position, interruptedPose), Is.LessThan(.001f));
            Assert.That(structure.SetMagicRepairProgress(1f), Is.True);
            yield return AwaitRepair(structure);
            Assert.That(structure.IsFractured, Is.False);
        }

        [UnityTearDown]''')
    return s
edit(Path('Assets/Elemental/Tests/PlayMode/OuterStoneRingRuntimeTests.cs'),outer)

def walltest(s):
    s=replace(s,'            int rebuiltEvents = 0;','            int rebuiltEvents = 0;\n            int captured = 0;\n            int weldedAtCapture = -1;\n            repair.PieceCaptured += _ =>\n            {\n                if (captured > 0) Assert.That(repair.WeldedPieceCount, Is.GreaterThan(weldedAtCapture), "Next wall piece may only fly after previous weld.");\n                weldedAtCapture = repair.WeldedPieceCount;\n                captured++;\n            };')
    s=replace(s,'            Assert.That(rebuiltEvents, Is.EqualTo(1));','            Assert.That(rebuiltEvents, Is.EqualTo(1));\n            Assert.That(captured, Is.GreaterThan(1));')
    return s
edit(Path('Assets/Elemental/Tests/PlayMode/EarthReassemblyRuntimeTests.cs'),walltest)
print('Staged sequential platform repair and regression tests')
