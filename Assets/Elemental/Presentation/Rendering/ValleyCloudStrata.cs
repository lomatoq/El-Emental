using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.Rendering
{
    // The transform is a planet child. No camera-follow, simulation or global render state.
    [DisallowMultipleComponent]
    public sealed class ValleyCloudStrata : MonoBehaviour
    {
        public const string OwnedName="Valley Cloud Strata";
        public static Vector3 VolumeScale(float radius) => new Vector3(7000,180,7000);
        public static Vector3 VolumeCentre(float radius) => new Vector3(0,-radius-15-90,0);
        public void Configure(Transform planet,float radius,Vector3 stagingUp,Mesh cube,Material material)
        {
            if(planet==null || cube==null || material==null || !float.IsFinite(radius) || radius<=0)
                throw new System.ArgumentException("Cloud strata needs a planet, positive radius, cube mesh and material.");
            transform.SetParent(planet,false);
            Vector3 localUp=planet.InverseTransformDirection(stagingUp).normalized;
            if(localUp.sqrMagnitude<0.5f)throw new System.ArgumentException("Cloud strata staging up must be nonzero.");
            transform.localPosition=localUp*VolumeCentre(radius).y;
            transform.localRotation=Quaternion.FromToRotation(Vector3.up,localUp);transform.localScale=VolumeScale(radius);
            var filter=GetComponent<MeshFilter>();if(filter==null)filter=gameObject.AddComponent<MeshFilter>();filter.sharedMesh=cube;
            var view=GetComponent<MeshRenderer>();if(view==null)view=gameObject.AddComponent<MeshRenderer>();view.sharedMaterial=material;
            view.shadowCastingMode=ShadowCastingMode.Off;view.receiveShadows=false;
            view.lightProbeUsage=LightProbeUsage.Off;view.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
    }
}
