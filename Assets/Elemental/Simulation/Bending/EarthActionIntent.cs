namespace Elemental.Simulation.Bending
{
    public enum EarthActionIntentKind : byte
    {
        None = 0,
        Cancel = 1,
        LandingWave = 2,
        Surf = 3,
        SelfRadialWave = 4,
        ManipulateTarget = 5,
        QuickFire = 6,
        QuickPrime = 7,
        FullBend = 8,
        GravityField = 9,
        Repair = 10,
        PillarJump = 11,
        ArmorHold = 12,
        ArmorSpread = 13,
        ArmorRadialRelease = 14,
        WaveCharge = 15,
        ResonanceCharge = 16,
        ResonanceVolley = 17,
        VectorFieldPush = 18,
        PillarCharge = 19,
        StompStone = 20,
        PillarCrest = 21
    }

    public readonly struct EarthActionIntent
    {
        public EarthActionIntent(
            EarthActionIntentKind kind,
            EarthInputConsumption consumption,
            float charge01 = 0f)
        {
            Kind = kind;
            Consumption = consumption;
            Charge01 = charge01 < 0f ? 0f : charge01 > 1f ? 1f : charge01;
        }

        public EarthActionIntentKind Kind { get; }
        public EarthInputConsumption Consumption { get; }
        public float Charge01 { get; }
        public bool Accepted => Kind != EarthActionIntentKind.None;

        public bool Consumes(EarthInputConsumption input) =>
            (Consumption & input) == input;
    }
}
