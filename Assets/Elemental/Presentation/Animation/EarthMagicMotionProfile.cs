using System;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [Serializable]
    public sealed class EarthMagicMotionEntry
    {
        public EarthHumanoidPoseSlot slot;
        public EarthMagicClipTiming timing = EarthMagicClipTiming.Default;
        [Range(0f, 1f)] public float actionHandInfluence = 0.16f;
        [Range(0f, 1f)] public float sustainedHandInfluence = 0.48f;
    }

    [CreateAssetMenu(menuName = "Elemental/Animation/Earth Magic Motion Profile")]
    public sealed class EarthMagicMotionProfile : ScriptableObject
    {
        [Tooltip("Eleven semantic slots. Values are clip-normalized markers and visual interpolation seconds; gameplay timing is unchanged.")]
        public EarthMagicMotionEntry[] motions = CreateDefaults();

        public static EarthMagicMotionEntry[] CreateDefaults()
        {
            return new[]
            {
                Entry(EarthHumanoidPoseSlot.RaiseWall, Timing(.09f, .21f, .39f, .56f, .72f, .98f, .09f, .12f, .17f, .11f, .19f, .22f), .12f, .34f),
                Entry(EarthHumanoidPoseSlot.RaisePlatform, Timing(.11f, .25f, .43f, .59f, .75f, .98f, .10f, .14f, .18f, .11f, .18f, .23f), .10f, .38f),
                Entry(EarthHumanoidPoseSlot.PullStone, Timing(.08f, .20f, .36f, .50f, .73f, .98f, .08f, .12f, .15f, .09f, .22f, .22f), .18f, .62f),
                Entry(EarthHumanoidPoseSlot.HeavyThrow, Timing(.10f, .27f, .47f, .64f, .76f, .98f, .10f, .16f, .20f, .11f, .15f, .23f), .08f, .24f),
                Entry(EarthHumanoidPoseSlot.VectorPush, Timing(.07f, .17f, .31f, .46f, .61f, .96f, .07f, .10f, .13f, .08f, .14f, .18f), .08f, .28f),
                Entry(EarthHumanoidPoseSlot.GravityRepair, Timing(.12f, .28f, .44f, .54f, .78f, .99f, .11f, .15f, .16f, .09f, .24f, .21f), .20f, .66f),
                Entry(EarthHumanoidPoseSlot.WaveResonance, Timing(.10f, .24f, .42f, .58f, .77f, .99f, .10f, .14f, .18f, .10f, .20f, .22f), .10f, .42f),
                Entry(EarthHumanoidPoseSlot.Pillar, Timing(.08f, .19f, .35f, .49f, .63f, .96f, .08f, .11f, .15f, .09f, .14f, .19f), .06f, .20f),
                Entry(EarthHumanoidPoseSlot.ArmorAssemble, Timing(.13f, .30f, .49f, .62f, .82f, .99f, .12f, .17f, .19f, .10f, .22f, .22f), .22f, .58f),
                Entry(EarthHumanoidPoseSlot.ArmorBarrage, Timing(.06f, .16f, .30f, .48f, .66f, .96f, .06f, .10f, .13f, .08f, .16f, .18f), .06f, .18f),
                Entry(EarthHumanoidPoseSlot.GenericCast, Timing(.07f, .18f, .33f, .47f, .62f, .96f, .07f, .11f, .14f, .08f, .14f, .18f), .10f, .30f)
            };
        }

        private static EarthMagicMotionEntry Entry(
            EarthHumanoidPoseSlot slot,
            EarthMagicClipTiming timing,
            float actionHandInfluence,
            float sustainedHandInfluence) => new EarthMagicMotionEntry
            {
                slot = slot,
                timing = timing,
                actionHandInfluence = actionHandInfluence,
                sustainedHandInfluence = sustainedHandInfluence
            };

        private static EarthMagicClipTiming Timing(
            float acquireEnd, float rootEnd, float loadEnd, float contact, float sustain, float recoverEnd,
            float acquireSeconds, float rootSeconds, float loadSeconds, float strikeSeconds,
            float sustainSeconds, float recoverSeconds) => new EarthMagicClipTiming
            {
                AcquireEnd = acquireEnd,
                RootEnd = rootEnd,
                LoadEnd = loadEnd,
                Contact = contact,
                Sustain = sustain,
                RecoverEnd = recoverEnd,
                AcquireSeconds = acquireSeconds,
                RootSeconds = rootSeconds,
                LoadSeconds = loadSeconds,
                StrikeSeconds = strikeSeconds,
                SustainSeconds = sustainSeconds,
                RecoverSeconds = recoverSeconds
            };

        public EarthMagicMotionEntry Find(int slot)
        {
            if (motions == null) return null;
            for (int i = 0; i < motions.Length; i++)
                if (motions[i] != null && (int)motions[i].slot == slot) return motions[i];
            return null;
        }

        public bool Validate(out string error)
        {
            for (int slot = 1; slot <= 11; slot++)
            {
                EarthMagicMotionEntry entry = Find(slot);
                if (entry == null || !entry.timing.IsValid) { error = $"Magic slot {slot} needs ordered markers and positive durations."; return false; }
            }
            error = string.Empty; return true;
        }
    }
}
