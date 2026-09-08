from pathlib import Path
p=Path('Assets/Elemental/Presentation/UI/CinematicMenuCamera.cs');s=p.read_text(encoding='utf-8-sig').replace('            float t = _countdownDollyProgress;\n            float align = Mathf.SmoothStep(0f, 1f, alignmentProgress);','            float t = reducedMotion ? 1f : _countdownDollyProgress;\n            float align = reducedMotion ? 1f : Mathf.SmoothStep(0f, 1f, alignmentProgress);');p.write_text(s,encoding='utf-8')
p=Path('Assets/Elemental/Tests/PlayMode/AlphaFrontendPlayTests.cs');s=p.read_text(encoding='utf-8-sig').replace('            Assert.That(flow.BeginBot(), Is.False);','            Assert.That(flow.BeginBot(), Is.False);\n            if (!flow.Preferences.ReducedMotion) Assert.That(menu.CountdownFocalLength, Is.EqualTo(150f).Within(.01f));');s=s.replace('            float roundBeforeCountdown = duel.RoundRemainingSeconds;','            float roundBeforeCountdown = duel.RoundRemainingSeconds;\n            float previousDollyDistance = float.PositiveInfinity;');s=s.replace('                frame++;','''                if (Time.unscaledTime - started > .15f && flow.State == FrontendState.Starting)
                {
                    float distance = Vector3.Distance(_camera.transform.position, menu.SubjectCenter);
                    Assert.That(distance, Is.LessThanOrEqualTo(previousDollyDistance + .10f), "Countdown camera moved away from the fighters.");
                    previousDollyDistance = distance;
                }
                frame++;''');p.write_text(s,encoding='utf-8')
