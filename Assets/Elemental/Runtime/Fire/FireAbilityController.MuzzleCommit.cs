using Elemental.Simulation.Fire;
using Unity.Profiling;
using UnityEngine;
namespace Elemental.Runtime.Fire
{
    public sealed partial class FireAbilityController
    {
        private static readonly ProfilerMarker FinalMuzzleMarker=new("Elemental.Fire.FinalMuzzleCommit");
        private FireProjectilePoseCommitter poseCommitter;
        public int CommittedLaunches {get;private set;}public int BlockedMuzzleCommits {get;private set;}
        public Vector3 CurrentHandMuzzlePosition=>hand!=null?hand.position:Vector3.zero;
        public Vector3 LastCommittedHand {get;private set;}public Vector3 LastLaunchPosition {get;private set;}
        public int LastLaunchFrame {get;private set;}=-1;
        private void ConfigurePoseCommitter(){if(poseCommitter==null)poseCommitter=gameObject.AddComponent<FireProjectilePoseCommitter>();poseCommitter.Configure(this);}
        public void CommitFinalProjectilePoses()
        {
            using var marker=FinalMuzzleMarker.Auto();
            if(!IsAvailable){CancelAll();return;}
            for(int i=0;i<bolts.Length;i++)
            {
                var bolt=bolts[i];if(!bolt.Active||!bolt.PosePending)continue;
                Vector3 source=bolt.Kick?foot.position+LocalUp*.08f:hand.position;
                Vector3 direction=(bolt.Aim-source).normalized,origin=source;
                bool valid=direction.sqrMagnitude>.9f;
                if(valid&&bolt.ChargedMuzzle)origin=source+direction*(FireChargedBoltProfile.FromPower(bolt.Power).VisualRadius+.08f);
                if(valid&&bolt.OrbitMuzzle)
                {
                    Vector3 planar=Vector3.ProjectOnPlane(bolt.Aim-root.position,LocalUp).normalized;
                    if(planar.sqrMagnitude>.5f)origin=root.position+LocalUp+planar*1.8f;
                }
                valid=valid&&Finite(source)&&Finite(origin)&&ClearMuzzle(source,origin,.2f*bolt.Power);
                bolt.PosePending=false;
                if(!valid){bolt.Active=false;bolts[i]=bolt;BlockedMuzzleCommits++;continue;}
                bolt.Position=origin;bolt.Direction=(bolt.Aim-origin).normalized;bolt.Age=0;bolts[i]=bolt;
                LastCommittedHand=source;LastLaunchPosition=origin;LastLaunchFrame=Time.frameCount;CommittedLaunches++;
                Cue(bolt.Kick?FireAbilityEffectKind.FootBolt:FireAbilityEffectKind.HandBolt,origin,bolt.Direction,LocalUp,bolt.Power,FireAbilityTuning.ProjectileLife,bolt.Id);
            }
        }
    }
}
