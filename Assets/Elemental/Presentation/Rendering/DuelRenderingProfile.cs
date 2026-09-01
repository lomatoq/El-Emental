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
        [SerializeField] private bool useCapsuleContactShadows = false;
        [SerializeField] private CapsuleContactShadowSettings capsuleContactShadows =
            new CapsuleContactShadowSettings();

        public bool UseDuelShadowMap => useDuelShadowMap;
        public DuelShadowSettings DuelShadows => duelShadows;
        public bool UseCapsuleContactShadows => useCapsuleContactShadows;
        public CapsuleContactShadowSettings CapsuleContactShadows => capsuleContactShadows;
    }
}
