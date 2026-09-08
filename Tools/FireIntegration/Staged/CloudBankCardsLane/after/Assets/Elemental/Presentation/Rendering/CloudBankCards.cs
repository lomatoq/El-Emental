using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.Rendering
{
    // Fixed planet-relative artwork cards: no camera following or steady-state callbacks.
    [DisallowMultipleComponent]
    public sealed class CloudBankCards : MonoBehaviour
    {
        public const string OwnedName="Valley Cumulus Art Banks";
        public const int CardCount=10;
        public void Configure(Transform planet,Vector3 stagingUp,float radius,Mesh quad,Material material)
        {
            if(planet==null || quad==null || material==null || !float.IsFinite(radius) || radius<=0 || stagingUp.sqrMagnitude<0.01f)
                throw new System.ArgumentException("Cloud art requires planet, authored up, positive radius, quad and material.");
            transform.SetParent(planet,false);transform.localPosition=Vector3.zero;
            transform.localRotation=Quaternion.FromToRotation(Vector3.up,planet.InverseTransformDirection(stagingUp).normalized);transform.localScale=Vector3.one;
            for(int i=0;i<CardCount;i++)
            {
                string name="Cloud bank "+i.ToString("00");var old=transform.Find(name);
                var go=old!=null?old.gameObject:new GameObject(name);go.transform.SetParent(transform,false);
                float angle=(i*36+Mathf.Sin(i*7.13f)*9)*Mathf.Deg2Rad;
                float distance=1100+(i%3)*360;
                float width=780+Mathf.Sin(i*3.17f)*170;
                float height=width*(2f/3);
                // Every visible lobe stays below the planet bottom, with an additional gap.
                go.transform.localPosition=new Vector3(Mathf.Sin(angle)*distance,-radius-30-height*0.5f-(i%3)*35,Mathf.Cos(angle)*distance);
                go.transform.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg+180+Mathf.Sin(i*5.7f)*12,0);
                go.transform.localScale=new Vector3((i%2==0?1:-1)*width,height,1);
                var filter=go.GetComponent<MeshFilter>();if(filter==null)filter=go.AddComponent<MeshFilter>();filter.sharedMesh=quad;
                var view=go.GetComponent<MeshRenderer>();if(view==null)view=go.AddComponent<MeshRenderer>();view.sharedMaterial=material;
                view.shadowCastingMode=ShadowCastingMode.Off;view.receiveShadows=false;
                view.lightProbeUsage=LightProbeUsage.Off;view.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
        }
    }
}
