using UnityEngine;
using UnityEngine.UIElements;

namespace ElEmental.StoneUI
{
    /// <summary>Optional bounded GPU accent rendered to a UI Toolkit background texture.</summary>
    public sealed class StoneUIFxSurface : MonoBehaviour
    {
        public StoneUIAssets assets;
        public UIDocument document;
        public string targetElement="result-halo";
        [Range(0,3)] public int effectMode=1;
        public Color tint=new Color(.73f,.86f,.45f,1);
        [Range(0,2)] public float intensity=.5f;
        [Range(8,60)] public int framesPerSecond=30;
        public bool reducedMotion;
        private RenderTexture _rt;
        private Material _material;
        private VisualElement _target;
        private float _next;
        private void OnEnable()
        {
            if(document==null)document=GetComponent<UIDocument>();
            if(assets==null)assets=Resources.Load<StoneUIAssets>("ElEmentalStoneUI/StoneUIAssets");
            if(assets==null||assets.uiFxShader==null)return;
            _material=new Material(assets.uiFxShader){hideFlags=HideFlags.HideAndDontSave};
            _material.SetTexture("_MainTex",assets.Texture("button_fx_packed"));
            _rt=new RenderTexture(512,256,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){name="EE_UIFX_512x256",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};_rt.Create();
        }
        private void Update()
        {
            if(document==null||_material==null||_rt==null)return;
            var root=document.rootVisualElement;var target=root?.Q<VisualElement>(targetElement);
            if(target==null||target.panel==null||target.resolvedStyle.display==DisplayStyle.None)return;
            if(_target!=target){_target=target;_target.style.backgroundImage=Background.FromRenderTexture(_rt);_next=0;}
            if(Time.unscaledTime<_next)return;
            _next=Time.unscaledTime+1f/Mathf.Max(8,framesPerSecond);
            var controller=GetComponent<StoneUIController>();bool freeze=reducedMotion||(controller!=null&&controller.Settings.reducedMotion);
            _material.SetFloat("_Clock",freeze?0:Time.unscaledTime);_material.SetFloat("_Mode",effectMode);_material.SetColor("_Tint",tint);_material.SetFloat("_Intensity",intensity);
            RenderTexture previous=RenderTexture.active;
            try{Graphics.Blit(null,_rt,_material,0);}finally{RenderTexture.active=previous;}
        }
        private void OnDisable()
        {
            // Restore the USS-provided static texture when removing the effect.
            if(_target!=null)_target.style.backgroundImage=StyleKeyword.Null;
            if(_rt!=null){_rt.Release();Destroy(_rt);_rt=null;}
            if(_material!=null){Destroy(_material);_material=null;}
            _target=null;
        }
    }
}
