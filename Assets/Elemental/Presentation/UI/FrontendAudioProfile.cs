using UnityEngine;

namespace Elemental.Presentation.UI
{
    [CreateAssetMenu(menuName="Elemental/UI/Frontend Audio",fileName="FrontendAudio")]
    public sealed class FrontendAudioProfile : ScriptableObject
    {
        [Header("Музыка и движение панели")]
        public AudioClip mainMenu, game, panelMove;
        [Range(0,1)] public float menuVolume=.42f, gameVolume=.46f, panelVolume=.24f;
        [Header("Плавные переходы, секунды")]
        [Min(.05f)] public float musicFadeSeconds=1.5f;
        [Min(.05f)] public float loopCrossfadeSeconds=3f;
        [Min(.01f)] public float panelAttackSeconds=.025f, panelReleaseSeconds=.15f;
        [Tooltip("Начало звука движения: пропуск тишины исходного клипа, секунды")]
        [Min(0)] public float panelStartOffsetSeconds=.044f;
        [Range(0,1)] public float pausedMusicGain=.65f;
    }
}
