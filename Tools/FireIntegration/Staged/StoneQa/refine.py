from pathlib import Path
p=Path('El-Emental/Tools/FireIntegration/Staged/StoneQa/Assets/Elemental/Tests/PlayMode/EarthStonePhysicalDropProductionTests.cs')
s=p.read_text(encoding='utf-8-sig').replace('public int collisions, impactEvents, dustAdmitted, chipsAdmitted,','public int collisions, impactEvents, firstImpactDust, firstImpactChips, dustAdmitted, chipsAdmitted,')
s=s.replace('''                active.impactEvents++; active.dustAdmitted+=cue.DustCount; active.chipsAdmitted+=cue.ChipCount;''','''                if(active.impactEvents==0) { active.firstImpactDust=cue.DustCount; active.firstImpactChips=cue.ChipCount; }
                active.impactEvents++; active.dustAdmitted+=cue.DustCount; active.chipsAdmitted+=cue.ChipCount;''')
s=s.replace('''            Vector3 contact=motor.transform.position+right*9f+up*4f;
            var floor''','''            Vector3 contact=motor.transform.position+right*9f+up*4f;
            up=(contact-planet.position).normalized;
            right=Vector3.Cross(up,Mathf.Abs(up.y)<.9f?Vector3.up:Vector3.forward).normalized;
            forward=Vector3.Cross(right,up);
            var floor''')
s=s.replace('_report.cases[1].dustAdmitted,Is.GreaterThan(_report.cases[0].dustAdmitted)','_report.cases[1].firstImpactDust,Is.GreaterThan(_report.cases[0].firstImpactDust)')
p.write_text(s,encoding='utf-8')
