namespace dwis.openlab.adcs.Base;

public class DCS
{
    private ILogger<DCS>? _logger;

    public BasicController DrawworkController;
    public BasicController TopdriveController;
    public BasicController MudpumpsController;

    public DCS( RigMachineLimits rigMachineLimits, ILoggerFactory? factory)
    {
       
        _logger = factory?.CreateLogger<DCS>();

        DrawworkController = new BasicController( rigMachineLimits.HoistingMachineLimits, factory?.CreateLogger<BasicController>(), "Drawworks controller");
        TopdriveController = new BasicController( rigMachineLimits.RotationMachineLimits, factory?.CreateLogger<BasicController>(), "Topdrive controller");
        MudpumpsController = new BasicController( rigMachineLimits.CirculationMachineLimits, factory?.CreateLogger<BasicController>(), "Mudpumps controller");

        DrawworkController.MaximumSetPoint = rigMachineLimits.HoistingMachineLimits.MachineMaximumSetPoint;
        DrawworkController.MinimumSetPoint = rigMachineLimits.HoistingMachineLimits.MachineMinimumSetPoint;
        DrawworkController.MaximumRateOfChangeSetPoint = rigMachineLimits.HoistingMachineLimits.MachineMaximumRateOfChangeSetPoint;
        DrawworkController.MinimumRateOfChangeSetPoint = rigMachineLimits.HoistingMachineLimits.MachineMinimumRateOfChangeSetPoint;

        TopdriveController.MaximumSetPoint = rigMachineLimits.RotationMachineLimits.MachineMaximumSetPoint;
        TopdriveController.MinimumSetPoint = rigMachineLimits.RotationMachineLimits.MachineMinimumSetPoint;
        TopdriveController.MaximumRateOfChangeSetPoint = rigMachineLimits.RotationMachineLimits.MachineMaximumRateOfChangeSetPoint;
        TopdriveController.MinimumRateOfChangeSetPoint = rigMachineLimits.RotationMachineLimits.MachineMinimumRateOfChangeSetPoint;

        MudpumpsController.MaximumSetPoint = rigMachineLimits.CirculationMachineLimits.MachineMaximumSetPoint;
        MudpumpsController.MinimumSetPoint = rigMachineLimits.CirculationMachineLimits.MachineMinimumSetPoint;
        MudpumpsController.MaximumRateOfChangeSetPoint = rigMachineLimits.CirculationMachineLimits.MachineMaximumRateOfChangeSetPoint;
        MudpumpsController.MinimumRateOfChangeSetPoint = rigMachineLimits.CirculationMachineLimits.MachineMinimumRateOfChangeSetPoint;
    }

    public void Initialize()
    {

    }

    public void Start(CancellationToken token)
    {
        DrawworkController.Start(token);
        TopdriveController.Start(token);
        MudpumpsController.Start(token);
    }
}
