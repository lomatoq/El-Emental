using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    [CreateAssetMenu(
        menuName = "Elemental/Earth/Shape Grammar Profile",
        fileName = "EarthShapeGrammarProfile")]
    public sealed class EarthShapeGrammarProfile : ScriptableObject
    {
        [SerializeField] private uint librarySeed = 0xE17F0411u;
        [SerializeField, Range(4, 32)] private int localHistoryLength = 16;
        [SerializeField, Range(1, 32)] private int candidateAttempts = 12;
        [SerializeField, Range(0.5f, 1.5f)] private float secondaryDetailScale = 1f;
        [SerializeField, Min(8)] private int minimumReviewSamplesPerFamily = 20;

        public uint LibrarySeed => librarySeed == 0u ? 0xE17F0411u : librarySeed;
        public int LocalHistoryLength => Mathf.Clamp(localHistoryLength, 4, 32);
        public int CandidateAttempts => Mathf.Clamp(candidateAttempts, 1, 32);
        public float SecondaryDetailScale => Mathf.Clamp(secondaryDetailScale, 0.5f, 1.5f);
        public int MinimumReviewSamplesPerFamily => Mathf.Max(8, minimumReviewSamplesPerFamily);
    }
}
