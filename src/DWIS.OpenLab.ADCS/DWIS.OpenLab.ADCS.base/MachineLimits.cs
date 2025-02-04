namespace dwis.openlab.adcs.Base;

public struct MachineLimits
{
    public double MachineMinimumSetPoint { get; }
    public double MachineMaximumSetPoint { get; }

    public double MachineMaximumRateOfChangeSetPoint { get; }
    public double MachineMinimumRateOfChangeSetPoint { get; }
}
