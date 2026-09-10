using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
namespace Elemental.Tests.PlayMode
{
    internal static class SchoolHintCaptureDiagnostics
    {
        public static int Write(string path)
        {
            var text=new StringBuilder();int visible=0;
            foreach(var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                var root=doc.rootVisualElement;if(root==null)continue;
                text.AppendLine($"document={doc.name} id={doc.GetEntityId()} scene={doc.gameObject.scene.path} active={doc.isActiveAndEnabled} panel={root.panel!=null} duelRoots={root.Query(name: "duel-hud").ToList().Count}");
                foreach(var hint in root.Query<Label>(name:"element-action-hint").ToList())
                {
                    bool shown=doc.isActiveAndEnabled&&root.panel!=null;
                    for(VisualElement p=hint;p!=null;p=p.parent)shown&=p.resolvedStyle.display!=DisplayStyle.None&&p.resolvedStyle.visibility==Visibility.Visible&&p.resolvedStyle.opacity>0;
                    if(shown)visible++;
                    text.AppendLine($"hint visible={shown} text={hint.text} bounds={hint.worldBound} size={hint.resolvedStyle.fontSize} spacing={hint.resolvedStyle.letterSpacing} font={hint.resolvedStyle.unityFont?.name} inlineShadow={hint.style.textShadow.value} outline={hint.resolvedStyle.unityTextOutlineWidth}");
                }
            }
            text.AppendLine("visibleHintCount="+visible);File.WriteAllText(path,text.ToString());return visible;
        }
    }
}
