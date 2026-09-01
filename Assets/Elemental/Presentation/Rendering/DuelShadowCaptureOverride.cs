using System;

namespace Elemental.Presentation.Rendering
{
    /// <summary>
    /// Single-owner transient evidence seam. It never changes the serialized
    /// profile and is reset at subsystem registration, so shipping remains off.
    /// </summary>
    public static class DuelShadowCaptureOverride
    {
        public readonly struct Token : IDisposable
        {
            private readonly uint _revision;

            internal Token(uint revision)
            {
                _revision = revision;
            }

            public bool IsValid => _revision != 0u;

            public void Dispose()
            {
                End(_revision);
            }
        }

        private static bool s_Active;
        private static uint s_Revision;
        private static DuelShadowRuntimeSettings s_Settings;

        public static bool IsActive => s_Active;

        public static bool TryBegin(
            in DuelShadowRuntimeSettings settings,
            out Token token,
            out string failure)
        {
            token = default;
            if (s_Active)
            {
                failure = "A duel-shadow capture override already owns the renderer.";
                return false;
            }

            s_Revision++;
            if (s_Revision == 0u) s_Revision++;
            s_Settings = settings;
            s_Active = true;
            token = new Token(s_Revision);
            failure = string.Empty;
            return true;
        }

        internal static bool TryGet(out DuelShadowRuntimeSettings settings)
        {
            settings = s_Settings;
            return s_Active;
        }

        private static void End(uint revision)
        {
            if (!s_Active || revision == 0u || revision != s_Revision) return;
            s_Active = false;
            s_Settings = default;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_Active = false;
            s_Revision = 0u;
            s_Settings = default;
        }
    }
}
