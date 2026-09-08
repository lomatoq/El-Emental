using System;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Independent local authentication storage for two standalone processes.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class OnlineLaunchProfile : MonoBehaviour
    {
        [SerializeField] private MpsRelaySession session;
        public void Configure(MpsRelaySession value) => session = value;
        private void Awake()
        {
            Application.runInBackground = true;
            string profile = Parse(Environment.GetCommandLineArgs());
            if (profile != null) session.Configure(session.Network, profile);
        }
        public static string Parse(string[] arguments)
        {
            string result = null;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] != "--online-profile") continue;
                if (result != null || ++i == arguments.Length)
                    throw new ArgumentException("Pass --online-profile once, followed by an authentication profile.");
                result = arguments[i];
                if (result.Length == 0 || result.Length > 30)
                    throw new ArgumentException("Online profile must contain 1–30 ASCII letters, digits, underscores or hyphens.");
                foreach (char character in result)
                    if (!(character >= 'a' && character <= 'z' || character >= 'A' && character <= 'Z' ||
                          character >= '0' && character <= '9' || character == '_' || character == '-'))
                        throw new ArgumentException("Invalid online authentication profile.");
            }
            return result;
        }
    }
}
