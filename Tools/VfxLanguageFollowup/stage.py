from pathlib import Path
root=Path.cwd(); out=root/'Tools/VfxLanguageFollowup/after'; before=root/'Tools/VfxLanguageFollowup/before'
paths=['Assets/Elemental/Presentation/VFX/EarthMaterialFeedbackPresenter.cs','Assets/Elemental/Presentation/VFX/EarthCosmeticChipLibrary.cs','Assets/Elemental/Presentation/Fire/Shaders/FireCpuFlame.hlsl','Assets/Elemental/Presentation/Fire/Shaders/ColumnDecorFlame.shader','Assets/Elemental/Content/VFX/Fire/Fire_ColumnDecor.asset','Assets/Elemental/Content/VFX/Fire/Fire_ColumnDecor.mat']
for p in paths:
 for dst in [before,out]:
  (dst/p).parent.mkdir(parents=True,exist_ok=True); (dst/p).write_bytes((root/p).read_bytes())
p=out/paths[0]; s=p.read_text()
s=s.replace('private Mesh[] cosmeticChipMeshes;', '''private Mesh[] cosmeticChipMeshes;
        [SerializeField, Range(0f, .6f)] private float chipRestitution = .28f;
        [SerializeField, Range(0f, 1f)] private float chipFriction = .58f;
        [SerializeField, Min(0f)] private float secondaryDustThreshold = .85f;
        [SerializeField] private LayerMask chipSurfaceMask = Physics.DefaultRaycastLayers;
        private int secondaryPuffsThisFrame;''')
s=s.replace('main.maxParticles = tuning.MaxParticles;', 'main.maxParticles = rubble ? Mathf.Min(128, tuning.MaxParticles) : tuning.MaxParticles;')
s=s.replace('renderer.renderMode = ParticleSystemRenderMode.Mesh;', '''renderer.renderMode = ParticleSystemRenderMode.Mesh;
                // Seeded per-particle angular velocity can settle after a contact.
                var spin = ps.rotationOverLifetime; spin.enabled = false;''')
s=s.replace('new GradientAlphaKey(1f,.08f), new GradientAlphaKey(.5f,.6f)', 'new GradientAlphaKey(1f,.045f), new GradientAlphaKey(.68f,.5f)')
s=s.replace('var color = ps.colorOverLifetime; color.enabled = true; color.color = gradient;', '''var color = ps.colorOverLifetime; color.enabled = true; color.color = gradient;
                var sizeOverLife = ps.sizeOverLifetime; sizeOverLife.enabled = true;
                sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f,.7f), new Keyframe(.2f,1f), new Keyframe(1f,1.65f)));''')
a=s.index('                bool impactDust ='); b=s.index('                var p =',a)
s=s[:a]+'''                // Keep event budgets, but reserve distinct contact, body and residual roles.
                // Low seed bits preserve the role through the allocation-free integration loop.
                bool layered = !rubble && (cue.Kind == EarthMaterialFeedbackKind.Impact ||
                    cue.Kind == EarthMaterialFeedbackKind.Land || UsesBroadDust(cue.Kind));
                int role = layered ? (i % 10 < 4 ? 0 : i % 10 < 8 ? 1 : 2) : 0;
                float speed = Mathf.Lerp(tuning.Speed.x,tuning.Speed.y,Next()) * energy;
                float lift = rubble ? Mathf.Lerp(.65f,1.3f,Next()) : Mathf.Lerp(.12f,.35f,Next());
                float lifetime = Mathf.Lerp(tuning.Lifetime.x,tuning.Lifetime.y,Next());
                float opacity = Mathf.Lerp(tuning.ColorA.a,tuning.ColorB.a,Next());
                if (layered)
                {
                    if (role == 0) { size *= .8f; speed *= 1.25f; lifetime *= .65f; }
                    else
                    {
                        size = Mathf.Lerp(profile.Fracture.Dust.Size.x, profile.Fracture.Dust.Size.y,
                            Mathf.Lerp(.3f,1f,sizeSample)) * cue.ParticleSizeScale * (role == 1 ? 1.1f : .95f);
                        speed *= role == 1 ? .42f : .19f;
                        lift = role == 1 ? .75f : 1.4f;
                        lifetime = Mathf.Lerp(profile.Fracture.Dust.Lifetime.x,profile.Fracture.Dust.Lifetime.y,Next())
                            * (role == 1 ? 1f : 1.6f);
                        opacity *= role == 1 ? .9f : .38f;
                    }
                }
                if (rubble) lifetime *= 1.8f;
''' +s[b:]
s=s.replace('randomSeed = seed == 0u ? 1u : seed,','randomSeed = ((seed & ~3u) | (uint)role) + 4u,')
s=s.replace('new Color(1f, 1f, 1f, Mathf.Lerp(tuning.ColorA.a,tuning.ColorB.a,Next()))','new Color(1f, 1f, 1f, opacity)')
s=s.replace('using (Marker.Auto()) { Integrate(dust, dustBuffer, 1.3f); Integrate(fractureDust, fractureBuffer, 1.3f); Integrate(chips, chipBuffer, 14f); }','''using (Marker.Auto())
            {
                secondaryPuffsThisFrame = 0;
                Integrate(dust, dustBuffer, .6f); Integrate(fractureDust, fractureBuffer, .6f); Integrate(chips, chipBuffer, 14f);
            }''')
s=s.replace('                buffer[i].velocity += (center - buffer[i].position).normalized * (gravity * dt);', '''                ref ParticleSystem.Particle particle = ref buffer[i];
                Vector3 down = (center - particle.position).normalized;
                if (ps == chips)
                {
                    int bounces = (int)(particle.randomSeed & 3u);
                    if (bounces == 3) { particle.velocity = Vector3.zero; continue; }
                    Vector3 velocity = particle.velocity;
                    float speed = velocity.magnitude;
                    float radius = Mathf.Max(.012f, particle.startSize * .3f);
                    // Sweep the step already advanced by ParticleSystem to avoid tunnelling.
                    Vector3 start = particle.position - velocity * dt;
                    if (speed > .01f && Physics.Raycast(start, velocity / speed, out RaycastHit hit,
                        speed * dt + radius, chipSurfaceMask, QueryTriggerInteraction.Ignore) &&
                        Vector3.Dot(velocity, hit.normal) < 0f)
                    {
                        float impact = -Vector3.Dot(velocity, hit.normal);
                        particle.position = hit.point + hit.normal * (radius + .005f);
                        bounces++;
                        Vector3 tangent = Vector3.ProjectOnPlane(velocity,hit.normal) * (1f-chipFriction);
                        particle.velocity = tangent + hit.normal * (impact * chipRestitution);
                        if (impact > secondaryDustThreshold && secondaryPuffsThisFrame < 8)
                            EmitSecondaryPuff(hit.point, hit.normal, particle.startSize, particle.randomSeed);
                        if (bounces >= 2 || particle.velocity.sqrMagnitude < .12f)
                        { bounces = 3; particle.velocity = Vector3.zero; }
                        particle.randomSeed = (particle.randomSeed & ~3u) | (uint)bounces;
                    }
                    else particle.velocity += down * (gravity * dt);
                    float spin = Mathf.Lerp(profile.Impact.Rubble.AngularSpeed.x,profile.Impact.Rubble.AngularSpeed.y,
                        ((particle.randomSeed >> 2) & 255u) / 255f) / (1f+bounces*2f);
                    if (bounces < 3) particle.rotation3D += new Vector3(spin,spin*.73f,-spin*.41f)*dt;
                    float age = 1f-particle.remainingLifetime/Mathf.Max(.01f,particle.startLifetime);
                    Color32 tint = particle.startColor; tint.a = (byte)(255f*(1f-Mathf.SmoothStep(.65f,1f,age)));
                    particle.startColor = tint;
                }
                else
                {
                    int role = (int)(particle.randomSeed & 3u);
                    float drag = role == 0 ? 3.8f : role == 1 ? 1.7f : 1.25f;
                    float settling = role == 0 ? gravity : role == 1 ? .16f : .035f;
                    particle.velocity = particle.velocity * Mathf.Exp(-drag * dt) + down * (settling * dt);
                }''')
pos=s.index('        private static bool IsFinite')
s=s[:pos]+'''        private void EmitSecondaryPuff(Vector3 point, Vector3 normal, float chipSize, uint particleSeed)
        {
            if (dust == null || profile == null || !profile.Impact.Dust.Enabled) return;
            secondaryPuffsThisFrame++;
            float size = Mathf.Clamp(chipSize*2.8f,profile.Impact.Dust.Size.x,profile.Impact.Dust.Size.y);
            var puff = new ParticleSystem.EmitParams
            {
                position = point + normal * (size*.5f), velocity = normal*.25f,
                startSize = size, startLifetime = profile.Impact.Dust.Lifetime.y*.7f,
                startColor = new Color(1f,1f,1f,.42f), randomSeed = particleSeed & ~3u,
                rotation3D = Vector3.forward*(particleSeed%360u)
            };
            dust.Emit(puff,1);
        }
''' +s[pos:]
s=s.replace('Mathf.SmoothStep(.65f,1f,age)','Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.65f,1f,age))')
p.write_text(s)
p=out/paths[1]; s=p.read_text().replace('Four cold-created','Eight cold-created').replace('new Mesh[4]','new Mesh[8]').replace('i < 3 ?','i < 5 ?'); p.write_text(s)
