using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [CreateAssetMenu(
        fileName = "DuelRenderingProfile",
        menuName = "Elemental/Rendering/Duel Rendering Profile")]
    public sealed class DuelRenderingProfile : ScriptableObject
    {
        [SerializeField] private bool useDuelShadowMap = false;
        [SerializeField] private DuelShadowSettings duelShadows = new DuelShadowSettings();

        public bool UseDuelShadowMap => useDuelShadowMap;
        public DuelShadowSettings DuelShadows => duelShadows;
    }
}
