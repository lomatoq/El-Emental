from pathlib import Path
p=Path('Assets/Elemental/Presentation/UI/CinematicMenuCamera.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('        [SerializeField] private Transform actor;','        [SerializeField] private Transform actor;\n        [SerializeField] private Transform countdownOpponent;\n        private PlanetMotor _opponentMotor;')
s=s.replace('PlanetMotor actorMotor, Transform subject)','PlanetMotor actorMotor, Transform subject, Transform opponent = null)')
s=s.replace('motor = actorMotor; actor = subject; }','motor = actorMotor; actor = subject; countdownOpponent = opponent; _opponentMotor = opponent != null ? opponent.GetComponent<PlanetMotor>() : null; }')
s=s.replace('            Vector3 center = feet + up * height * .5f;\n            SubjectCenter = center;','''            Vector3 center = feet + up * height * .5f;
            Vector3 separation = Vector3.zero;
            if (_countdownFraming && countdownOpponent != null)
            {
                if (_opponentMotor == null) _opponentMotor = countdownOpponent.GetComponent<PlanetMotor>();
                Vector3 otherFeet = _opponentMotor != null ? _opponentMotor.SupportFeetPoint(up) : countdownOpponent.position;
                separation = otherFeet - feet;
                center += separation * .5f;
                Vector3 duelAxis = Vector3.ProjectOnPlane(separation, up);
                if (duelAxis.sqrMagnitude > .01f) facing = duelAxis.normalized;
            }
            SubjectCenter = center;''')
a='''            Vector3 position = center + facing * distance;
            if (_countdownFraming) position = center + (facing * Mathf.Cos(countdownElevation * Mathf.Deg2Rad) + up * Mathf.Sin(countdownElevation * Mathf.Deg2Rad)) * distance;
            Vector3 right = Vector3.Cross(up, -facing).normalized;
            float aspect = outputCamera != null ? outputCamera.aspect : 16f / 9f;'''
b='''            Vector3 right = Vector3.Cross(up, -facing).normalized;
            float aspect = outputCamera != null ? outputCamera.aspect : 16f / 9f;
            if (_countdownFraming)
            {
                float halfWidth = Mathf.Abs(Vector3.Dot(separation, right)) * .5f + height * .3f;
                float depthMargin = Mathf.Abs(Vector3.Dot(separation, facing)) * .5f;
                distance = Mathf.Max(distance, halfWidth / (Mathf.Tan(fieldOfView * Mathf.Deg2Rad * .5f) * aspect * .8f) + depthMargin);
            }
            Vector3 position = center + facing * distance;
            if (_countdownFraming) position = center + (facing * Mathf.Cos(countdownElevation * Mathf.Deg2Rad) + up * Mathf.Sin(countdownElevation * Mathf.Deg2Rad)) * distance;'''
assert a in s;s=s.replace(a,b)
s=s.replace('renderer.transform.IsChildOf(actor) ||','renderer.transform.IsChildOf(actor) ||\n                    (_countdownFraming && countdownOpponent != null && renderer.transform.IsChildOf(countdownOpponent)) ||')
p.write_text(s,encoding='utf-8')
p=Path('Assets/Elemental/Authoring/Editor/AlphaFrontendSetup.cs');s=p.read_text(encoding='utf-8-sig').replace('director.Player.GetComponent<PlanetMotor>(), director.Player);','director.Player.GetComponent<PlanetMotor>(), director.Player, duel.BotTransform);');p.write_text(s,encoding='utf-8')
p=Path('BuildReports/AlphaImplementation/Stage2Pending/Assets/Elemental/NetworkingStage2/EarthOnlinePresentationBridge.cs');s=p.read_text(encoding='utf-8-sig').replace('view.Animation, view.Motor, view.Subject);','view.Animation, view.Motor, view.Subject, _online ? (actor == 2 ? actorOne.Subject : actorTwo.Subject) : actorOne.OriginalDofSecondary);');p.write_text(s,encoding='utf-8')
