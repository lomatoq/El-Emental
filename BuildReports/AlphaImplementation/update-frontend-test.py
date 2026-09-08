from pathlib import Path
p=Path('Assets/Elemental/Tests/PlayMode/AlphaFrontendPlayTests.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('            int frame = 0;','            int frame = 0;\n            var digits = new System.Collections.Generic.HashSet<string>();\n            float roundBeforeCountdown = duel.RoundRemainingSeconds;')
s=s.replace('                if (frame++ % 3 == 0) yield return Capture($"Transition-{frame:000}.png");','                Assert.That(duel.CombatAllowed, Is.False);\n                Assert.That(duel.RoundRemainingSeconds, Is.EqualTo(roundBeforeCountdown));\n                if (digits.Add(view.CountdownText)) yield return Capture($"Countdown-{view.CountdownText}.png");\n                frame++;')
s=s.replace('Is.InRange(.80f, 1.2f)','Is.InRange(3.95f, 4.5f)')
s=s.replace('            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat)); Assert.That(duel.CombatAllowed, Is.True);','            CollectionAssert.AreEquivalent(new[] { "4", "3", "2", "1" }, digits);\n            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat)); Assert.That(duel.CombatAllowed, Is.True);')
a='''            yield return Capture("Combat.png");
            flow.Back(); yield return new WaitForSecondsRealtime(1);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Main)); Assert.That(duel.CombatAllowed, Is.False);
            Assert.That(flow.BeginBot(), Is.True);
            yield return new WaitForSecondsRealtime(1);'''
b='''            var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<ElementalUITheme>("Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset");
            Assert.That(theme.hudFont.name, Does.Contain("Varose"));
            var duelRoot = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(hudRoot, className: "duel-hud");
            UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.TextElement>(duelRoot).ForEach(label =>
                Assert.That(label.style.unityFont.value, Is.SameAs(theme.hudFont), label.name));
            yield return Capture("Combat.png");
            flow.Back();
            Assert.That(flow.State, Is.EqualTo(FrontendState.Paused)); Assert.That(Time.timeScale, Is.EqualTo(0));
            float health = duel.PlayerHealth, remaining = duel.RoundRemainingSeconds;
            Vector3 pausedPosition = duel.PlayerTransform.position;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(duel.RoundRemainingSeconds, Is.EqualTo(remaining));
            Assert.That(duel.PlayerTransform.position, Is.EqualTo(pausedPosition));
            foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(1920,1200), new Vector2Int(2520,1080) })
            { BindCapture(size); yield return null; yield return Capture($"Pause-{size.x}x{size.y}.png"); }
            flow.OpenSettings(); Assert.That(flow.State, Is.EqualTo(FrontendState.Settings));
            flow.Back(); Assert.That(flow.State, Is.EqualTo(FrontendState.Paused));
            flow.Resume(); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(duel.PlayerHealth, Is.EqualTo(health)); Assert.That(duel.RoundRemainingSeconds, Is.EqualTo(remaining));
            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat));
            flow.Back(); flow.EndMatch(); yield return new WaitForSecondsRealtime(1);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Main)); Assert.That(duel.CombatAllowed, Is.False);
            Assert.That(flow.BeginBot(), Is.True);
            yield return new WaitForSecondsRealtime(4.2f);'''
assert a in s;s=s.replace(a,b)
p.write_text(s,encoding='utf-8')
