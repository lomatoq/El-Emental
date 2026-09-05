"""Apply only when Unity Play/test runs have stopped. Does not call Unity APIs."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
edit = root / 'Assets/Elemental/Tests/EditMode/DayNightSkyTests.cs'
play = root / 'Assets/Elemental/Tests/PlayMode/DayNightSkyRuntimeTests.cs'

old = '''        [Test]
        public void LegacyFullCycleIsMigratedOnceAndNewDefaultsStayFiveMinutesEach()
        {
            var profile = ScriptableObject.CreateInstance<CelestialSystemProfile>();
            try
            {
                Assert.That(profile.DaylightSeconds, Is.EqualTo(300));
                Assert.That(profile.NightSeconds, Is.EqualTo(300));
                EditorJsonUtility.FromJsonOverwrite("{\\"cycleSchema\\":0,\\"legacyCycleSeconds\\":480}", profile);
                profile.OnAfterDeserialize();
                Assert.That(profile.DaylightSeconds, Is.EqualTo(240));
                Assert.That(profile.NightSeconds, Is.EqualTo(240));
                EditorJsonUtility.FromJsonOverwrite("{\\"daylightSeconds\\":300,\\"nightSeconds\\":120}", profile);
                profile.OnAfterDeserialize();
                Assert.That(profile.DaySeconds, Is.EqualTo(420), "Old legacy value cannot overwrite edited settings twice.");
            }
            finally { Object.DestroyImmediate(profile); }
        }
'''
new = r'''        [Test]
        public void LegacyFullCycleIsMigratedOnceAndNewDefaultsStayFiveMinutesEach()
        {
            var defaults = ScriptableObject.CreateInstance<CelestialSystemProfile>();
            string path = "Assets/Elemental/Tests/LegacyCelestialMigration_" + System.Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                Assert.That(defaults.DaylightSeconds, Is.EqualTo(300));
                Assert.That(defaults.NightSeconds, Is.EqualTo(300));
                string guid = AssetDatabase.AssetPathToGUID("Assets/Elemental/Runtime/World/CelestialSystemProfile.cs");
                Assert.That(guid, Is.Not.Empty);
                // Exercise actual asset migration, including FormerlySerializedAs,
                // rather than assuming EditorJsonUtility's native object JSON layout.
                string yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                    "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" +
                    "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                    "  m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}\n  m_Name: LegacyCelestialMigration\n" +
                    "  m_EditorClassIdentifier: Elemental.Runtime::Elemental.Runtime.World.CelestialSystemProfile\n  daySeconds: 480\n";
                System.IO.File.WriteAllText(path, yaml.Replace("\\n", "\n"));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var legacy = AssetDatabase.LoadAssetAtPath<CelestialSystemProfile>(path);
                Assert.That(legacy, Is.Not.Null);
                Assert.That(legacy.DaylightSeconds, Is.EqualTo(240));
                Assert.That(legacy.NightSeconds, Is.EqualTo(240));
                var serialized = new SerializedObject(legacy);
                serialized.FindProperty("daylightSeconds").floatValue = 300;
                serialized.FindProperty("nightSeconds").floatValue = 120;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(legacy);
                AssetDatabase.SaveAssetIfDirty(legacy);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<CelestialSystemProfile>(path);
                Assert.That(reloaded.DaySeconds, Is.EqualTo(420), "Migration must not overwrite subsequent authored durations.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                Object.DestroyImmediate(defaults);
            }
        }
'''
before = edit.read_text(encoding='utf-8')
if old not in before:
    raise SystemExit('EditMode test changed; inspect before applying this staged fix.')
after = before.replace(old, new, 1)

play_before = play.read_text(encoding='utf-8')
needle = '''            Assert.That(_system, Is.Not.Null);
            yield return null;
            _oldPhase = _system.Snapshot.TimeOfDay01;'''
replacement = '''            Assert.That(_system, Is.Not.Null);
            Elemental.Runtime.World.EarthSceneReadinessGate gate = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                var foundGate = root.GetComponentInChildren<Elemental.Runtime.World.EarthSceneReadinessGate>(true);
                if (foundGate != null) gate = foundGate;
            }
            Assert.That(gate, Is.Not.Null, "The production scene must expose its readiness boundary.");
            double deadline = Time.realtimeSinceStartupAsDouble + 130d;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(gate.Failed, Is.False, gate.Status);
            Assert.That(gate.IsReady, Is.True, "Waited 130 unscaled seconds for production physics readiness: " + gate.Status);
            // The gate restores timeScale; allow the celestial clock one normal frame.
            yield return null;
            _oldPhase = _system.Snapshot.TimeOfDay01;'''
if needle not in play_before:
    raise SystemExit('PlayMode setup changed; inspect before applying this staged fix.')
play_after = play_before.replace(needle, replacement, 1)
edit.write_text(after, encoding='utf-8')
play.write_text(play_after, encoding='utf-8')
print('Updated DayNightSky EditMode migration fixture and PlayMode readiness wait. Refresh Unity only after Play runs have stopped.')
