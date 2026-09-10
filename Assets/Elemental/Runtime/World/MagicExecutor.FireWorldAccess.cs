using Elemental.Runtime.Physics;
namespace Elemental.Runtime.World
{
    public sealed partial class MagicExecutor
    {
        public EarthMaterialFeedbackHub FireFeedbackHub=>materialFeedback;
        public EarthRockDebrisPool FireDebrisPool=>fragmentPool!=null?fragmentPool.DebrisPool:null;
    }
}
