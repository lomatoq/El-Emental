namespace Elemental.Simulation.Characters
{
    public interface IPlanetMotorInputSource
    {
        PlanetMotorCommand SampleCommand(uint tick);
    }
}
