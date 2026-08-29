using Elemental.Simulation.Combat;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [CreateAssetMenu(menuName = "Elemental/Characters/Impact Response Profile")]
    public sealed class CharacterImpactResponseProfile : ScriptableObject
    {
        [SerializeField] private ImpactResponseMode responseMode = ImpactResponseMode.Legacy;
        [SerializeField, Min(0.1f)] private float singleStoneRootVelocity = 0.8f;
        [SerializeField, Min(0.1f)] private float maximumRagdollRise = 2f;
        [SerializeField, Min(0.1f)] private float maximumRagdollTangentSpeed = 4f;

        public ImpactResponseMode ResponseMode => responseMode;
        public float SingleStoneRootVelocity => Mathf.Max(0.1f, singleStoneRootVelocity);
        public float MaximumRagdollRise => Mathf.Max(0.1f, maximumRagdollRise);
        public float MaximumRagdollTangentSpeed => Mathf.Max(0.1f, maximumRagdollTangentSpeed);
        public EarthCharacterImpactTuning Tuning => EarthCharacterImpactTuning.Default;
    }
}
