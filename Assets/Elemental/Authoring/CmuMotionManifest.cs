using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Authoring
{
    [Serializable]
    public sealed class CmuMotionManifestEntry
    {
        public string id;
        public string sourceUrl;
        public string licenseUrl;
        public string sha256;
        public string localFileName;
    }

    [CreateAssetMenu(fileName = "CMUMotionManifest", menuName = "Elemental/Animation/CMU Motion Manifest")]
    public sealed class CmuMotionManifest : ScriptableObject
    {
        public string provenanceNote = "Only import files whose source, licence and SHA-256 are recorded.";
        public List<CmuMotionManifestEntry> entries = new();
    }
}
