namespace dwis.openlab.adcs.Base;

public class DCS
{
    private IOPCUADWISClient _dwisClient;
    private ILogger<DCS>? _logger;

    public BasicController DrawworkController;
    public BasicController TopdriveController;
    public BasicController MudpumpsController;

    public DCS(IOPCUADWISClient dwisClient, RigMachineLimits rigMachineLimits, ILoggerFactory? factory)
    {
        _dwisClient = dwisClient;
        _logger = factory?.CreateLogger<DCS>();

        DrawworkController = new BasicController(dwisClient, rigMachineLimits.HoistingMachineLimits, factory?.CreateLogger<BasicController>(), "Drawworks controller");
        TopdriveController = new BasicController(dwisClient, rigMachineLimits.RotationMachineLimits, factory?.CreateLogger<BasicController>(), "Topdrive controller");
        MudpumpsController = new BasicController(dwisClient, rigMachineLimits.CirculationMachineLimits, factory?.CreateLogger<BasicController>(), "Mudpumps controller");

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
