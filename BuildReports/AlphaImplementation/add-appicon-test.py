from pathlib import Path
p=Path('Assets/Elemental/Authoring/Editor/AlphaFrontendSetup.cs');s=p.read_text(encoding='utf-8-sig');n='        [MenuItem("Elemental/UI/Install Alpha Frontend")]';s=s.replace(n,'''        [MenuItem("Elemental/UI/Apply Elemental App Icon")]
        public static void ApplyAppIcon()
        {
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(RootPath + "ElementalLogo.png");
            if (logo == null) throw new InvalidOperationException("The supplied Elemental logo is missing.");
            foreach (var target in new[] { UnityEditor.Build.NamedBuildTarget.Unknown, UnityEditor.Build.NamedBuildTarget.Standalone })
            {
                int count = Mathf.Max(1, PlayerSettings.GetIconSizes(target, IconKind.Any).Length);
                var icons = new Texture2D[count];
                for (int i = 0; i < count; i++) icons[i] = logo;
                PlayerSettings.SetIcons(target, icons, IconKind.Any);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Elemental] Saved original Elemental logo as default and Windows application icons.");
        }

'''+n);p.write_text(s,encoding='utf-8')
p=Path('Assets/Elemental/Tests/PlayMode/AlphaFrontendPlayTests.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('if (digits.Add(view.CountdownText)) yield return Capture($"Countdown-{view.CountdownText}.png");','''if (digits.Add(view.CountdownText))
                {
                    string digit = view.CountdownText;
                    yield return new WaitForSecondsRealtime(.12f);
                    yield return Capture($"Countdown-{digit}.png");
                    if (digit == "3")
                    {
                        Vector3 centerViewport = _camera.WorldToViewportPoint(menu.SubjectCenter);
                        Assert.That(centerViewport.x, Is.EqualTo(.5f).Within(.025f));
                        Assert.That(centerViewport.y, Is.EqualTo(.5f).Within(.025f));
                    }
                }''')
s=s.replace('            flow.Back();\n            Assert.That(flow.State, Is.EqualTo(FrontendState.Paused));','''            var pauseButton = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(hudRoot, "pause-match");
            Assert.That(pauseButton, Is.Not.Null);
            Assert.That(pauseButton.resolvedStyle.backgroundColor.a, Is.InRange(.2f, .5f));
            using (var submit = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) pauseButton.SendEvent(submit);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Paused));''')
p.write_text(s,encoding='utf-8')
