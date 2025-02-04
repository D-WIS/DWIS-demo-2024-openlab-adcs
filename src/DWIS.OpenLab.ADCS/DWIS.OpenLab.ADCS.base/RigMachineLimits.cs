namespace dwis.openlab.adcs.Base;

public struct RigMachineLimits
{
    public MachineLimits HoistingMachineLimits { get; }
    public MachineLimits RotationMachineLimits { get; }
    public MachineLimits CirculationMachineLimits { get; }
    public RigMachineLimits(MachineLimits hoistingML, MachineLimits rotationML, MachineLimits circulationML)
    {
        HoistingMachineLimits = hoistingML;
        RotationMachineLimits = rotationML;
        CirculationMachineLimits = circulationML;
    }
}
