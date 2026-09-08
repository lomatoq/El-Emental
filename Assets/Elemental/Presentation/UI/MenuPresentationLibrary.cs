using UnityEngine;
namespace Elemental.Presentation.UI
{
    [CreateAssetMenu(menuName="Elemental/UI/Menu Presentation Library")]
    public sealed class MenuPresentationLibrary : ScriptableObject
    {
        public MenuScreenLayout[] screens = System.Array.Empty<MenuScreenLayout>();
        public MenuScreenLayout Get(MenuScreenId id)
        {foreach(var s in screens)if(s!=null&&s.screen==id)return s;return null;}
    }
}
