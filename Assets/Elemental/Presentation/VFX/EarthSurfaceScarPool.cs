using Elemental.Runtime.World;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Magic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class EarthSurfaceScarPool : MonoBehaviour
    {
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private bool fireOnly;
        public void ConfigureFireOnly()=>fireOnly=true;
        [SerializeField] private EarthFeedbackProfile profile;
        [SerializeField] private Material decalMaterial;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private DecalProjector[] projectors;

        private float[] _expiresAt;
        private int _cursor;
        private readonly FireWorldImpact[] fireOwners=new FireWorldImpact[2];
        private Transform[] anchors; private Vector3[] anchorPoints,anchorNormals;
        private bool[] fireMarks; private Material scorchMaterial; private Texture2D scorchTexture;
        private Elemental.Runtime.Physics.IEarthPhysicalTarget[] anchorTargets;
        private Elemental.Runtime.Physics.EarthPhysicalTargetHandle[] anchorHandles;
        // One existing bounded pool can consume both explicit fighters.
        public void ConfigureFire(FireWorldImpact impact)
        {
            if(impact==null)throw new System.ArgumentNullException(nameof(impact));
            EnsurePool();EnsureScorchMaterial();
            for(int i=0;i<fireOwners.Length;i++)if(fireOwners[i]==impact)return;
            for(int i=0;i<fireOwners.Length;i++)if(fireOwners[i]==null)
            {fireOwners[i]=impact;if(isActiveAndEnabled)impact.ContactAccepted+=OnFireContact;return;}
            throw new System.InvalidOperationException("Scar pool has two configured fire owners already.");
        }
        private void EnsureScorchMaterial()
        {
            if(scorchMaterial!=null)return;
            if(decalMaterial==null)throw new System.InvalidOperationException("Configure the production Earth scar material/profile before binding Fire scorch.");
            scorchMaterial=new Material(decalMaterial){name="Fire fading soot"};
            var soot=new Color(.07f,.048f,.03f,.38f);
            if(scorchMaterial.HasProperty("_BaseColor"))scorchMaterial.SetColor("_BaseColor",soot);
            if(scorchMaterial.HasProperty("_Color"))scorchMaterial.SetColor("_Color",soot);
            if(scorchMaterial.HasProperty("Base_Color"))scorchMaterial.SetColor("Base_Color",soot);
            if(scorchMaterial.HasProperty("_Smoothness"))scorchMaterial.SetFloat("_Smoothness",0);
            if(scorchMaterial.HasProperty("Smoothness"))scorchMaterial.SetFloat("Smoothness",0);
            if(scorchMaterial.HasProperty("Normal_Blend"))scorchMaterial.SetFloat("Normal_Blend",0);
            const int size=64;var pixels=new Color[size*size];
            // Cold, deterministic alpha asset for the existing URP decal template.
            // Zero coverage at every border prevents a black square projector footprint.
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size*2-1,v=(y+.5f)/size*2-1;
                float angle=Mathf.Atan2(v,u),radius=Mathf.Sqrt(u*u+v*v);
                float rim=.65f+.15f*Mathf.Sin(angle*3+.7f)+.10f*Mathf.Sin(angle*7-1.3f);
                float coverage=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(rim-.28f,rim,radius));
                float grain=Mathf.PerlinNoise(x*.13f+7.2f,y*.13f+3.8f);
                float alpha=coverage*Mathf.Lerp(.48f,1,grain);
                // URP's actual template graph uses Base_Map RGB directly; it has
                // no tint property. Coverage and soot color must both live here.
                pixels[y*size+x]=new Color(soot.r,soot.g,soot.b,alpha*soot.a);
            }
            scorchTexture=new Texture2D(size,size,TextureFormat.RGBA32,false,true)
                {name="Fire pooled soot soft mask",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            scorchTexture.SetPixels(pixels);scorchTexture.Apply(false,true);
            if(scorchMaterial.HasProperty("Base_Map"))scorchMaterial.SetTexture("Base_Map",scorchTexture);
            if(scorchMaterial.HasProperty("_BaseMap"))scorchMaterial.SetTexture("_BaseMap",scorchTexture);
        }
        private void OnFireContact(FireSurfaceContact contact)
        {
            if(contact.Surface==null||decalMaterial==null||projectors==null||projectors.Length==0)return;
            Transform anchor=contact.Surface.transform;int index=-1;
            for(int i=0;i<projectors.Length;i++)
                if(fireMarks[i]&&anchors[i]==anchor&&projectors[i].enabled&&Vector3.Dot(anchor.TransformDirection(anchorNormals[i]),contact.Normal)>.7f&&
                    (anchor.TransformPoint(anchorPoints[i])-contact.Point).sqrMagnitude<.64f){index=i;break;}
            bool fresh=index<0;
            if(index<0){index=_cursor;_cursor=(_cursor+1)%projectors.Length;}
            anchorTargets[index]=contact.Surface.GetComponentInParent<Elemental.Runtime.Physics.IEarthPhysicalTarget>();anchorHandles[index]=contact.Target;
            // A repeated hit reinforces the existing footprint. Its center and
            // orientation belong to the receiver, never to the moving flame tip.
            if(fresh)
            {
                anchors[index]=anchor;anchorPoints[index]=anchor.InverseTransformPoint(contact.Point);
                anchorNormals[index]=anchor.InverseTransformDirection(contact.Normal);
            }
            fireMarks[index]=true;
            var projector=projectors[index];projector.material=scorchMaterial;
            float variation=Hash01((uint)(index+1)*731u);
            float diameter=Mathf.Clamp(contact.Radius*2.5f,.55f,1.3f);
            Vector3 desired=new Vector3(diameter*Mathf.Lerp(.8f,1.25f,variation),diameter*Mathf.Lerp(1.2f,.8f,variation),.22f);
            projector.size=fresh?desired:Vector3.Min(new Vector3(1.6f,1.6f,.22f),Vector3.Max(projector.size,desired)+new Vector3(.002f,.002f,0));
            // Default pivot.z=.5 is independent of projector.size.z. Leaving it
            // there puts this thin volume behind the surface instead of across it.
            projector.pivot=Vector3.zero;
            projector.fadeFactor=.8f;projector.enabled=true;_expiresAt[index]=Time.time+3.5f;
            PositionFireMark(index);
        }
        private void PositionFireMark(int index)
        {
            Transform anchor=anchors[index];var target=anchorTargets[index];
            bool reused=target!=null&&(target.TargetHandle.StableId!=anchorHandles[index].StableId||target.TargetHandle.Generation!=anchorHandles[index].Generation);
            if(reused||anchor==null||!anchor.gameObject.activeInHierarchy)
            {projectors[index].enabled=false;return;}
            Vector3 normal=anchor.TransformDirection(anchorNormals[index]).normalized;
            Vector3 tangent=Vector3.Cross(normal,Mathf.Abs(normal.y)<.8f?Vector3.up:Vector3.right).normalized;
            projectors[index].transform.SetPositionAndRotation(anchor.TransformPoint(anchorPoints[index])+normal*.02f,Quaternion.LookRotation(-normal,tangent)*Quaternion.AngleAxis(Hash01((uint)(index+1))*360,Vector3.forward));
        }

        public MagicExecutor ConfiguredExecutor=>executor;
        public int Capacity => projectors?.Length ?? 0;
        public int ActiveCount { get; private set; }

        public void Configure(
            MagicExecutor configuredExecutor,
            EarthFeedbackProfile configuredProfile,
            Material configuredMaterial,
            Transform configuredPlanetCenter)
        {
            if (isActiveAndEnabled && executor != null) executor.Events.EarthImpactOccurred -= OnEarthImpact;
            executor = configuredExecutor;
            profile = configuredProfile;
            decalMaterial = configuredMaterial;
            planetCenter = configuredPlanetCenter;
            EnsurePool();
            if (isActiveAndEnabled && executor != null) executor.Events.EarthImpactOccurred += OnEarthImpact;
        }

        public void RebuildPool()
        {
            int count = profile != null ? profile.DecalCapacity : 24;
            projectors = new DecalProjector[count];
            _expiresAt = new float[count];
            anchors=new Transform[count];anchorPoints=new Vector3[count];anchorNormals=new Vector3[count];fireMarks=new bool[count];anchorTargets=new Elemental.Runtime.Physics.IEarthPhysicalTarget[count];anchorHandles=new Elemental.Runtime.Physics.EarthPhysicalTargetHandle[count];
            _cursor = 0;
            ActiveCount = 0;
            for (int index = 0; index < count; index++)
            {
                GameObject decalObject = new GameObject($"Earth Surface Scar {index + 1:00}");
                decalObject.transform.SetParent(transform, false);
                DecalProjector projector = decalObject.AddComponent<DecalProjector>();
                projector.material = decalMaterial;
                projector.drawDistance = profile != null ? profile.DecalDrawDistance : 42f;
                projector.fadeFactor = 0f;
                projector.enabled = false;
                projectors[index] = projector;
            }
        }

        private void Awake() => EnsurePool();

        private void OnEnable()
        {
            EnsurePool();
            if (executor != null) executor.Events.EarthImpactOccurred += OnEarthImpact;
            foreach(var fire in fireOwners)if(fire!=null)fire.ContactAccepted+=OnFireContact;
        }

        private void OnDisable()
        {
            if (executor != null) executor.Events.EarthImpactOccurred -= OnEarthImpact;
            foreach(var fire in fireOwners)if(fire!=null)fire.ContactAccepted-=OnFireContact;
        }

        private void Update()
        {
            if (projectors == null || _expiresAt == null) return;
            float now = Time.time;
            float fadeDuration = profile != null ? profile.ScarFadeSeconds : 4f;
            int active = 0;
            for (int index = 0; index < projectors.Length; index++)
            {
                DecalProjector projector = projectors[index];
                if (projector == null || !projector.enabled) continue;
                if(fireMarks[index]){PositionFireMark(index);if(!projector.enabled)continue;}
                float remaining = _expiresAt[index] - now;
                if (remaining <= 0f)
                {
                    projector.enabled = false;
                    projector.fadeFactor = 0f;
                    continue;
                }
                float fade = Mathf.Clamp01(remaining / Mathf.Max(0.01f, fireMarks[index]?3.5f:fadeDuration));
                projector.fadeFactor = fireMarks[index] ? .8f * fade * fade * (3f-2f*fade) : fade;
                active++;
            }
            ActiveCount = active;
        }

        private void OnEarthImpact(EarthImpactEvent impact)
        {
            if(fireOnly)return;
            if (profile == null || impact.Impulse < profile.MinimumScarImpulse ||
                projectors == null || projectors.Length == 0)
                return;
            EarthFeedbackSample sample = profile.Evaluate(in impact);
            int index = _cursor;
            _cursor = (_cursor + 1) % projectors.Length;
            DecalProjector projector = projectors[index];
            fireMarks[index]=false;anchors[index]=null;
            if (projector == null) return;

            Vector3 point = new Vector3(impact.Point.x, impact.Point.y, impact.Point.z);
            Vector3 normal = new Vector3(impact.Normal.x, impact.Normal.y, impact.Normal.z).normalized;
            if (normal.sqrMagnitude < 0.5f)
            {
                Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
                normal = (point - center).normalized;
            }
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.8f ? Vector3.up : Vector3.right).normalized;
            float roll = Hash01(impact.SourceId) * 360f;
            projector.transform.SetPositionAndRotation(
                point + normal * 0.035f,
                Quaternion.AngleAxis(roll, normal) * Quaternion.LookRotation(-normal, tangent));
            float diameter = sample.ScarRadius * 2f;
            projector.size = new Vector3(diameter, diameter, Mathf.Max(0.18f, sample.ScarRadius * 0.42f));
            projector.material = decalMaterial;
            projector.drawDistance = profile.DecalDrawDistance;
            projector.fadeFactor = 1f;
            projector.enabled = true;
            _expiresAt[index] = profile.PersistentSurfaceScars
                ? float.PositiveInfinity
                : Time.time + sample.Lifetime;
        }

        private void EnsurePool()
        {
            int count = profile != null ? profile.DecalCapacity : 24;
            if (projectors == null || projectors.Length != count)
            {
                RebuildPool();
                return;
            }
            if (_expiresAt == null || _expiresAt.Length != count) _expiresAt = new float[count];
            if(anchors==null||anchors.Length!=count){anchors=new Transform[count];anchorPoints=new Vector3[count];anchorNormals=new Vector3[count];fireMarks=new bool[count];anchorTargets=new Elemental.Runtime.Physics.IEarthPhysicalTarget[count];anchorHandles=new Elemental.Runtime.Physics.EarthPhysicalTargetHandle[count];}
        }

        private void OnDestroy(){if(scorchMaterial!=null)Destroy(scorchMaterial);if(scorchTexture!=null)Destroy(scorchTexture);}

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
