using System;
using System.Collections.Generic;
using Elemental.Presentation.UI;
using UnityEditor;
using UnityEngine;
namespace Elemental.Authoring.Editor
{
    public static class MenuLayoutSetup
    {
        public const string Folder="Assets/Elemental/Content/UI/MenuLayouts";
        public const string LibraryPath=Folder+"/MenuPresentation.asset";
        [MenuItem("Elemental/UI/Install editable menu layouts")]
        public static void Install()
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Elemental/Content/UI","MenuLayouts");
            var library=AssetDatabase.LoadAssetAtPath<MenuPresentationLibrary>(LibraryPath);
            if(library==null){library=ScriptableObject.CreateInstance<MenuPresentationLibrary>();AssetDatabase.CreateAsset(library,LibraryPath);}
            var screens=new List<MenuScreenLayout>();
            foreach(MenuScreenId id in Enum.GetValues(typeof(MenuScreenId)))
            {
                string path=Folder+"/"+id+".asset";var layout=AssetDatabase.LoadAssetAtPath<MenuScreenLayout>(path);
                if(layout==null)
                {
                    layout=ScriptableObject.CreateInstance<MenuScreenLayout>();layout.screen=id;
                    if(id==MenuScreenId.Countdown||id==MenuScreenId.Returning)layout.elements.Add(new MenuElementLayout{path=id.ToString()});
                    if(id==MenuScreenId.Victory||id==MenuScreenId.Defeat||id==MenuScreenId.Draw)
                    {
                        layout.elements.Add(new MenuElementLayout{path="reference-result-title",fontSize=112,size=new Vector2(1100,135),offset=new Vector2(0,-8)});
                        layout.elements.Add(new MenuElementLayout{path="reference-result-emblem",size=new Vector2(150,132),offset=new Vector2(17.5f,-16)});
                        layout.elements.Add(new MenuElementLayout{path="reference-result-divider",offset=new Vector2(0,-10)});
                        layout.elements.Add(new MenuElementLayout{path="reference-result-score",fontSize=48,offset=new Vector2(0,-8)});
                        layout.elements.Add(new MenuElementLayout{path="restart-round",size=new Vector2(590,88),padding=new Vector4(30,18,30,18)});
                        layout.elements.Add(new MenuElementLayout{path="reference-result-menu",size=new Vector2(590,88),offset=new Vector2(-20,-8),padding=new Vector4(30,18,30,18)});
                    }
                    AssetDatabase.CreateAsset(layout,path);
                }
                screens.Add(layout);
                if((id==MenuScreenId.Countdown||id==MenuScreenId.Returning)&&layout.elements.Count==0)
                {layout.elements.Add(new MenuElementLayout{path=id.ToString()});EditorUtility.SetDirty(layout);}
            }
            library.screens=screens.ToArray();EditorUtility.SetDirty(library);
            var theme=AssetDatabase.LoadAssetAtPath<ElementalUITheme>("Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset");
            if(theme==null)throw new InvalidOperationException("Missing production UI theme.");
            theme.menuPresentation=library;EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();
        }
        [MenuItem("Elemental/UI/Capture live menu element catalog")]
        public static void Capture()
        {
            var library=AssetDatabase.LoadAssetAtPath<MenuPresentationLibrary>(LibraryPath);
            if(library==null)throw new InvalidOperationException("Install editable menu layouts first.");
            var views=UnityEngine.Object.FindObjectsByType<FrontendMenuView>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var view in views)
            {
                Add(library.Get(MenuScreenId.Sidebar),view.SidebarBindings);
                for(int i=0;i<5;i++)Add(library.Get((MenuScreenId)((int)MenuScreenId.Main+i)),view.PageBindings(i));
            }
            foreach(var hud in UnityEngine.Object.FindObjectsByType<EarthDuelHud>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                foreach(var id in new[]{MenuScreenId.Victory,MenuScreenId.Defeat,MenuScreenId.Draw})Add(library.Get(id),hud.ResultLayoutBindings);
            AssetDatabase.SaveAssets();
        }
        private static void Add(MenuScreenLayout profile,IReadOnlyList<MenuLayoutBinding> bindings)
        {
            if(profile==null||bindings==null)return;
            foreach(var b in bindings)if(profile.Find(b.Path)==null)profile.elements.Add(new MenuElementLayout{path=b.Path});
            profile.Revision++;EditorUtility.SetDirty(profile);
        }
    }
}
