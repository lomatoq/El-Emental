using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Elemental.Presentation.UI
{
    /// <summary>Local alpha/brightness blur for overlay canvases, which render after URP bloom.</summary>
    public sealed class UiContourBloom : MaskableGraphic
    {
        private Image _source;
        private Sprite _sprite;
        private Material _ownedMaterial;
        private Vector4 _uv;
        private Vector2 _lastSize;
        private float _lastRadius;
        public float Radius = 9f;
        public override Texture mainTexture => _sprite != null ? _sprite.texture : Texture2D.whiteTexture;

        public static UiContourBloom Create(Image source, Color tint, float radius = 9f)
        {
            var shader=Resources.Load<Shader>("ElementalUiContourBloom");
            if(shader==null)throw new System.InvalidOperationException("Import ElementalUiContourBloom.shader into Presentation/UI/Resources before building the menu.");
            var go=new GameObject("Contour bloom: "+source.name,typeof(RectTransform),typeof(CanvasRenderer),typeof(UiContourBloom));
            go.transform.SetParent(source.transform.parent,false);go.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
            var bloom=go.GetComponent<UiContourBloom>();bloom._source=source;bloom.Radius=radius;bloom.color=tint;bloom.raycastTarget=false;
            bloom._ownedMaterial=new Material(shader){name="UI contour bloom instance",hideFlags=HideFlags.HideAndDontSave};
            bloom._ownedMaterial.SetColor("_Color",Color.white);bloom.material=bloom._ownedMaterial;bloom.Sync();return bloom;
        }

        public void SetStrength(float strength) => canvasRenderer.SetAlpha(Mathf.Clamp01(strength));

        private void LateUpdate() { if(_source!=null)Sync(); }

        private void Sync()
        {
            var src=_source.rectTransform;var dst=rectTransform;
            dst.anchorMin=src.anchorMin;dst.anchorMax=src.anchorMax;dst.pivot=src.pivot;
            dst.anchoredPosition=src.anchoredPosition;dst.sizeDelta=src.sizeDelta;dst.localScale=src.localScale;dst.localRotation=src.localRotation;
            var sprite=_source.overrideSprite!=null?_source.overrideSprite:_source.sprite;
            if(sprite!=_sprite){_sprite=sprite;_uv=sprite!=null?DataUtility.GetOuterUV(sprite):new Vector4(0,0,1,1);_ownedMaterial.SetVector("_SpriteRect",_uv);SetAllDirty();}
            var size=CoreRect().size;
            if(size!=_lastSize||Radius!=_lastRadius){_lastSize=size;_lastRadius=Radius;SetVerticesDirty();}
            _ownedMaterial.SetVector("_BlurUV",new Vector4((_uv.z-_uv.x)*Radius/Mathf.Max(1,size.x),(_uv.w-_uv.y)*Radius/Mathf.Max(1,size.y),0,0));
        }

        private Rect CoreRect()
        {
            var r=GetPixelAdjustedRect();
            if(_sprite!=null&&_source!=null&&_source.preserveAspect)
            {
                float ratio=_sprite.rect.width/_sprite.rect.height;
                if(r.width/r.height>ratio){float w=r.height*ratio;r.x+=(r.width-w)*rectTransform.pivot.x;r.width=w;}
                else {float h=r.width/ratio;r.y+=(r.height-h)*rectTransform.pivot.y;r.height=h;}
            }
            return r;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(_sprite==null)return;
            var core=CoreRect();float pad=Radius+2;
            var r=new Rect(core.x-pad,core.y-pad,core.width+2*pad,core.height+2*pad);
            float du=(_uv.z-_uv.x)*pad/Mathf.Max(1,core.width),dv=(_uv.w-_uv.y)*pad/Mathf.Max(1,core.height);
            vh.AddVert(new Vector3(r.xMin,r.yMin),color,new Vector2(_uv.x-du,_uv.y-dv));
            vh.AddVert(new Vector3(r.xMin,r.yMax),color,new Vector2(_uv.x-du,_uv.w+dv));
            vh.AddVert(new Vector3(r.xMax,r.yMax),color,new Vector2(_uv.z+du,_uv.w+dv));
            vh.AddVert(new Vector3(r.xMax,r.yMin),color,new Vector2(_uv.z+du,_uv.y-dv));
            vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
        }

        protected override void OnDestroy()
        {
            if(_ownedMaterial!=null){if(Application.isPlaying)Destroy(_ownedMaterial);else DestroyImmediate(_ownedMaterial);}
            base.OnDestroy();
        }
    }
}
