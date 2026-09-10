using UnityEngine;
namespace Elemental.Runtime.Fire
{
    // Animation/IK/secondary pose completes by2900; visual Fire submits after this commit.
    [DefaultExecutionOrder(3000),DisallowMultipleComponent]
    public sealed class FireProjectilePoseCommitter:MonoBehaviour
    {
        private FireAbilityController owner;
        public void Configure(FireAbilityController source){owner=source;}
        private void LateUpdate(){if(owner!=null&&owner.isActiveAndEnabled)owner.CommitFinalProjectilePoses();}
    }
}
