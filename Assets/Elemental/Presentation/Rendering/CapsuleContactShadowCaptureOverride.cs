using System;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    /// <summary>
    /// Transient single-owner evidence seam. Serialized shipping state remains off.
    /// </summary>
    public static class CapsuleContactShadowCaptureOverride
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
        private static CapsuleContactShadowRuntimeSettings s_Settings;

        public static bool IsActive => s_Active;

        public static bool TryBegin(
            in CapsuleContactShadowRuntimeSettings settings,
            out Token token,
            out string failure)
        {
            token = default;
            if (s_Active)
            {
                failure = "A capsule contact-shadow capture override already owns the renderer.";
                return false;
            }
            s_Revision++;
            if (s_Revision == 0u)
                s_Revision++;
            s_Settings = settings;
            s_Active = true;
            token = new Token(s_Revision);
            failure = string.Empty;
            return true;
        }

        internal static bool TryGet(out CapsuleContactShadowRuntimeSettings settings)
        {
            settings = s_Settings;
            return s_Active;
        }

        private static void End(uint revision)
        {
            if (!s_Active || revision == 0u || revision != s_Revision)
                return;
            s_Active = false;
            s_Settings = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_Active = false;
            s_Revision = 0u;
            s_Settings = default;
        }
    }
}
