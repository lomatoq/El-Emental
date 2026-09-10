using Elemental.Simulation.Combat;
using Unity.Mathematics;

namespace Elemental.Simulation.Time
{
    public enum MatchStageOutcome:byte { Draw,Victory,Defeat }
    public static class MatchStageTimeline
    {
        private static float Reveal(float age,float start,float end,bool reduced)
        {
            if(reduced){start=0;end=.10f;}
            float t=math.saturate((age-start)/(end-start));return t*t*(3-2*t);
        }
        public static float Root(float age,bool reduced)=>Reveal(age,.15f,.35f,reduced);
        public static float Camera(float age,bool reduced)=>Reveal(age,.15f,.65f,reduced);
        public static float Title(float age,bool reduced)=>Reveal(age,.25f,.55f,reduced);
        public static float Score(float age,bool reduced)=>Reveal(age,.40f,.70f,reduced);
        public static float Buttons(float age,bool reduced)=>Reveal(age,.65f,.90f,reduced);
        public static MatchStageOutcome Outcome(EarthDuelFighterId local,int playerScore,int botScore)
        {
            if(playerScore==botScore)return MatchStageOutcome.Draw;
            bool playerWon=playerScore>botScore;
            return playerWon==(local==EarthDuelFighterId.Player)?MatchStageOutcome.Victory:MatchStageOutcome.Defeat;
        }
    }
}
