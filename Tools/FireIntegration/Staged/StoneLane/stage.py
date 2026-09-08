from pathlib import Path
import difflib, hashlib, json
root=Path('El-Emental'); lane=root/'Tools/FireIntegration/Staged/StoneLane'; before=lane/'before'; after=lane/'after'
paths=['Assets/Elemental/Presentation/VFX/EarthMaterialFeedbackPresenter.cs','Assets/Elemental/Runtime/World/EarthMaterialFeedbackHub.cs','Assets/Elemental/Runtime/Physics/EarthRockDebrisPool.cs','Assets/Elemental/Runtime/Physics/EarthDestructibleDecorRock.cs','Assets/Elemental/Runtime/Physics/EarthFragmentPool.cs']
for p in paths:
    data=(root/p).read_bytes()
    for folder in [before,after]:
        dest=folder/p; dest.parent.mkdir(parents=True,exist_ok=True); dest.write_bytes(data)
def edit(p,old,new):
    f=after/p; s=f.read_text(encoding='utf-8-sig'); assert old in s, (p,old[:80]); f.write_text(s.replace(old,new,1),encoding='utf-8')
def add(p,s):
    f=after/p; f.parent.mkdir(parents=True,exist_ok=True); f.write_text(s,encoding='utf-8')
add('Assets/Elemental/Simulation/Bending/EarthStoneImpactDust.cs', '''using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    /// <summary>Cosmetic impact response only. Mass is supplied by the canonical body owner.</summary>
    public static class EarthStoneImpactDust
    {
        public const float MinimumClosingSpeed = .25f;
        public const float CooldownSeconds = .12f;
        public static float Strength(float mass, float closingSpeed)
        {
            if (!math.isfinite(mass) || !math.isfinite(closingSpeed) || mass <= 0f ||
                closingSpeed < MinimumClosingSpeed) return 0f;
            // Normal kinetic energy distinguishes a heavy short drop from a light tap.
            // Log compression bounds count/velocity growth without scaling every mote.
            float speed = math.min(closingSpeed, 100f);
            float energy = .5f * math.min(mass, 1000000f) * speed * speed;
            return math.clamp(.25f + .32f * math.log2(1f + energy / 12f), .25f, 2.4f);
        }
        public static float FineBiasedSize(float unitSample)
        {
            float u = math.saturate(unitSample);
            return u * u * u;
        }
        public static uint CueSeed(uint source, uint generation, EarthMaterialFeedbackKind kind, float3 point)
        {
            uint h = math.hash(new uint4(source, generation, (uint)kind,
                math.hash(math.asuint(math.round(point * 64f)))));
            return h == 0u ? 7919u : h;
        }
    }
}
''')
p=paths[1]
edit(p,'        private int count;','''        private int count;
        private readonly uint[] impactSources = new uint[32], impactGenerations = new uint[32];
        private readonly float[] impactTimes = new float[32];
        private int impactSlots, impactCursor;
        public void EmitStoneImpact(Vector3 point, Vector3 normal, float mass, float closingSpeed,
            float radius, uint sourceId, uint generation = 0)
        {
            float strength = EarthStoneImpactDust.Strength(mass, closingSpeed);
            if (!isActiveAndEnabled || strength <= 0f || !IsFinite(point) || !IsFinite(normal) ||
                !float.IsFinite(radius)) return;
            float now = Time.fixedTime;
            // Unknown sources must not suppress other independent contacts.
            if (sourceId != 0u)
            {
                int slot = -1;
                for (int i = 0; i < impactSlots; i++)
                    if (impactSources[i] == sourceId && impactGenerations[i] == generation) { slot = i; break; }
                if (slot >= 0 && now >= impactTimes[slot] && now - impactTimes[slot] < EarthStoneImpactDust.CooldownSeconds) return;
                if (slot < 0)
                {
                    slot = impactCursor; impactCursor = (impactCursor + 1) % impactSources.Length;
                    impactSlots = Mathf.Min(impactSlots + 1, impactSources.Length);
                }
                impactSources[slot] = sourceId; impactGenerations[slot] = generation; impactTimes[slot] = now;
            }
            Emit(EarthMaterialFeedbackKind.Impact, point, normal, strength, radius, sourceId, generation);
        }''')
edit(p,'private void OnDisable() { count = 0; surfaceCount = 0; }','private void OnDisable() { count = 0; surfaceCount = 0; impactSlots = 0; impactCursor = 0; }')
p=paths[3]
edit(p,'''            if (!_shattered && !breakAttempted && approach >= 0.75f)
                materialFeedback?.Emit(EarthMaterialFeedbackKind.Impact, contact.point, contact.normal,
                    Mathf.Clamp(approach / 8f, 0.4f, 1f), visualRadius, StableEarthId, _generation);''','''            if (!_shattered && !breakAttempted)
                materialFeedback?.EmitStoneImpact(contact.point, contact.normal,
                    EarthMass, approach, visualRadius, StableEarthId, _generation);''')
p=paths[4]
edit(p,'''            if (specificImpulse < 0.75f) return;
            materialFeedback?.Emit(EarthMaterialFeedbackKind.Impact, point, normal,
                Mathf.Clamp(specificImpulse / 8f, 0.4f, 1f), fragment.Radius, fragment.FragmentId);''','''            materialFeedback?.EmitStoneImpact(point, normal, fragment.Mass,
                specificImpulse, fragment.Radius, fragment.FragmentId);''')
p=paths[2]
edit(p,'''            if (collision.relativeVelocity.sqrMagnitude < .5625f) return false;
            ContactPoint contact = collision.GetContact(0);''','''            if (collision.relativeVelocity.sqrMagnitude < .5625f)
            {
                ContactPoint quietContact = collision.GetContact(0);
                materialFeedback?.EmitStoneImpact(quietContact.point, quietContact.normal, piece.EarthMass,
                    Mathf.Max(0f, Vector3.Dot(collision.relativeVelocity, quietContact.normal)), radius, seed);
                return false;
            }
            ContactPoint contact = collision.GetContact(0);''')
edit(p,'''                if (approach >= 0.75f) materialFeedback?.Emit(EarthMaterialFeedbackKind.Impact,
                    contact.point, contact.normal, 0.4f, radius, seed);''','''                // Same closing convention as decor; presentation does not change the damage policy.
                materialFeedback?.EmitStoneImpact(contact.point, contact.normal, body.mass,
                    Mathf.Max(0f, Vector3.Dot(collision.relativeVelocity, contact.normal)), radius, seed);''')
add('Assets/Elemental/Presentation/VFX/EarthCosmeticChipLibrary.cs','''using Elemental.Presentation.Rendering;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    /// <summary>Six cold-created cosmetic silhouettes. Never used for repairable matter or colliders.</summary>
    internal static class EarthCosmeticChipLibrary
    {
        public static Mesh[] Build()
        {
            var meshes = new Mesh[6];
            for (int i = 0; i < meshes.Length; i++)
            {
                RumbleRockFamily family = i < 2 ? RumbleRockFamily.Slab :
                    i < 4 ? RumbleRockFamily.Wedge : RumbleRockFamily.Pebble;
                Mesh mesh = RumbleRockMeshFactory.Build(
                    RumbleRockMeshFactory.CreateDefaultRecipe(1847 + i * 997, family), "Cosmetic Chip " + i);
                // Center the particle pivot and normalize only the longest axis; preserve aspect ratios.
                Vector3 center = mesh.bounds.center, size = mesh.bounds.size;
                float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                Vector3[] vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++) vertices[v] = (vertices[v] - center) / longest;
                mesh.vertices = vertices; mesh.RecalculateBounds(); meshes[i] = mesh;
            }
            return meshes;
        }
    }
}
''')
p=paths[0]
edit(p,'        private void OnDestroy() => cosmeticMaterials.Dispose();','''        private Mesh[] cosmeticChipMeshes;
        private void OnDestroy()
        {
            cosmeticMaterials.Dispose();
            if (cosmeticChipMeshes == null) return;
            foreach (Mesh owned in cosmeticChipMeshes)
                if (owned != null) { if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned); }
        }''')
edit(p,'            if (rubble) { renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = chipMesh; }','''            if (rubble)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                cosmeticChipMeshes ??= EarthCosmeticChipLibrary.Build();
                renderer.SetMeshes(cosmeticChipMeshes, cosmeticChipMeshes.Length);
            }''')
edit(p,'                seed ^= cue.SourceId ^ cue.Generation;','                seed = EarthStoneImpactDust.CueSeed(cue.SourceId, cue.Generation, cue.Kind, cue.Point);')
edit(p,'''                float size = Mathf.Lerp(tuning.Size.x,tuning.Size.y,Next()) * cue.ParticleSizeScale;''','''                float sizeSample = EarthStoneImpactDust.FineBiasedSize(Next());
                float size = Mathf.Lerp(tuning.Size.x,tuning.Size.y,sizeSample) * cue.ParticleSizeScale;
                bool impactDust = !rubble && cue.Kind == EarthMaterialFeedbackKind.Impact;
                // Majority stays near the contact; fewer slow lofted puffs supply volume.
                bool lofted = impactDust && Next() < .24f;
                if (impactDust) size *= lofted ? 1.35f : .7f;''')
edit(p,'''                var p = new ParticleSystem.EmitParams
                {''','''                float speed = Mathf.Lerp(tuning.Speed.x,tuning.Speed.y,Next()) * energy;
                float lift = impactDust ? (lofted ? Mathf.Lerp(.55f,1.05f,Next()) : Mathf.Lerp(.08f,.28f,Next())) : Mathf.Lerp(.4f,1.2f,Next());
                float lifetime = Mathf.Lerp(tuning.Lifetime.x,tuning.Lifetime.y,Next());
                if (impactDust) { speed *= lofted ? .45f : .9f; lifetime *= lofted ? 1.2f : .7f; }
                var p = new ParticleSystem.EmitParams
                {''')
edit(p,'''                    velocity = (radial + up * Mathf.Lerp(.4f,1.2f,Next())).normalized * (Mathf.Lerp(tuning.Speed.x,tuning.Speed.y,Next()) * energy),
                    startLifetime = Mathf.Lerp(tuning.Lifetime.x,tuning.Lifetime.y,Next()),''','''                    velocity = (radial + up * lift).normalized * speed,
                    startLifetime = lifetime,
                    randomSeed = seed == 0u ? 1u : seed,''')
# Generate review patch and freshness manifest without touching git.
manifest={}
patch=[]
for dest in sorted(after.rglob('*.cs')):
    rel=dest.relative_to(after).as_posix(); src=before/rel
    old=src.read_text(encoding='utf-8-sig') if src.exists() else ''
    new=dest.read_text(encoding='utf-8')
    patch.extend(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='a/'+rel if src.exists() else '/dev/null',tofile='b/'+rel))
    manifest[rel]={'before_sha256':hashlib.sha256(src.read_bytes()).hexdigest() if src.exists() else None,'after_sha256':hashlib.sha256(dest.read_bytes()).hexdigest()}
(lane/'stone-lane.patch').write_text(''.join(patch),encoding='utf-8')
(lane/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('Staged',len(manifest),'files. No Assets changed.')
