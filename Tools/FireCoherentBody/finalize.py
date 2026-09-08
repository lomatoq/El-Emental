from pathlib import Path
p=Path('Tools/FireCoherentBody/after/Assets/Elemental/Presentation/Fire/FireCoherentBodyMeshBackend.cs');s=p.read_text()
s=s.replace('        public double LastStepMilliseconds { get; private set; }','''        public double LastStepMilliseconds { get; private set; }
        public int RedirectedSegments { get; private set; }
        public int ActiveRibbons => ActiveTriangles / ((Sections - 1) * 2);
        public int SectionCount => Sections;
        public bool TryGetCenterlinePoint(int ribbon, int section, out Vector3 position)
        {
            position = default;
            if (disposed || ribbon < 0 || ribbon >= ActiveRibbons || section < 0 || section >= Sections) return false;
            int i = (ribbon * Sections + section) * 2;
            position = (vertices[i].Position + vertices[i + 1].Position) * .5f;
            return true;
        }''')
s=s.replace('                int ribbons = 0;', '                int ribbons = 0; RedirectedSegments = 0;')
s=s.replace('                                    FireContactMath.ResolveSwept(contacts[c], previous, 0, h, profile.ParticleRadius, phase, ref position, ref velocity);','''                                    if (FireContactMath.ResolveSwept(contacts[c], previous, 0, h, profile.ParticleRadius, phase, ref position, ref velocity))
                                        RedirectedSegments++;''')
p.write_text(s)
