using DWIS.API.DTO;
using System.Reflection;

namespace dwis.openlab.adcs.Base;

public class openLabADCS : IHostedService
{
    private IOPCUADWISClient _dwisClient;
    private ILogger<openLabADCS>? _logger;

    public DCS DrillingControlSystem { get; set; }

    public LowLevelInterfaceInSignals LowLevelInterfaceInSignals { get; set; }
    public LowLevelInterfaceOutSignals LowLevelInterfaceOutSignals { get; set; }

    private AcquiredSignals? _acquiredDrillerSignals;


    public openLabADCS(IOPCUADWISClient dwisClient, ILogger<openLabADCS>? logger, AcquiredSignals? acquiredDrillerSignals)
    {
        _dwisClient = dwisClient;
        _logger = logger;
        _acquiredDrillerSignals = acquiredDrillerSignals;
        Initialize();
    }

    private ManifestFile BuildManifest(Type type, string appName)
    {
        ManifestFile manifestFile = new ManifestFile();
        manifestFile.InjectedNodes = new List<InjectedNode>();
        manifestFile.InjectedReferences = new List<InjectedReference>();
        manifestFile.InjectedVariables = new List<InjectedVariable>();
        manifestFile.ProvidedVariables = new List<ProvidedVariable>();
        manifestFile.InjectionInformation = new InjectionInformation();
        manifestFile.ManifestName = appName;
        manifestFile.Provider = new InjectionProvider() { Company = "NORCE", Name = appName };

        var props = type.GetProperties();
        foreach (var prop in props) 
        {
            string name = prop.Name;
            if (prop.PropertyType == typeof(double))
            {
                manifestFile.ProvidedVariables.Add(new ProvidedVariable() { DataType = "double", VariableID = name });
            }
            else if (prop.PropertyType == typeof(bool)) 
            {
                manifestFile.ProvidedVariables.Add(new ProvidedVariable() { DataType = "bool", VariableID = name });
            }
        }
        return manifestFile;
    }


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.Register(OnCancellationRequested);


        var outProperties = typeof(LowLevelInterfaceOutSignals).GetProperties();

        PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMicroseconds(50));
        string TopOfStringVelocitySetPoint = "";
        string FlowRateInSetPoint = "";
        string SurfaceRPMSetPoint = "";
        while (await timer.WaitForNextTickAsync())
        {
            bool hoistingRequested, circulationRequested, rotationRequested;

            double requestedHoistingSpeed,requestedRotationSpeed, requestedCirculationSpeed,  requestedHoistingAccelerationLimit, requestedRotationAccelerationLimit, requestedCirculationAccelerationLimit;

            double drillerHoistingSpeed, drillerRotationSpeed, drillerCirculationSpeed;
            drillerHoistingSpeed = drillerCirculationSpeed = drillerRotationSpeed = 0;

            double actualHoistingSpeed, actualRotationSpeed, actualCirculationSpeed;

            
            lock (LowLevelInterfaceInSignals) 
            {
                hoistingRequested = LowLevelInterfaceInSignals.RequestControlHoisting;
                rotationRequested = LowLevelInterfaceInSignals.RequestControlRotation;
                circulationRequested = LowLevelInterfaceInSignals.RequestControlCirculation;

                requestedHoistingSpeed = LowLevelInterfaceInSignals.RequestedHoistingSpeed;
                requestedRotationSpeed = LowLevelInterfaceInSignals.RequestedRotationSpeed;
                requestedCirculationSpeed = LowLevelInterfaceInSignals.RequestedCirculationFlowRate;

                requestedHoistingAccelerationLimit = LowLevelInterfaceInSignals.RequestedHoistingAccelerationLimit;
                requestedRotationAccelerationLimit = LowLevelInterfaceInSignals.RequestedRotationAccelerationLimit;
                requestedCirculationAccelerationLimit = LowLevelInterfaceInSignals.RequestedCirculationAccelerationLimit;
            }

            lock (LowLevelInterfaceOutSignals) 
            {
                actualHoistingSpeed = LowLevelInterfaceOutSignals.ActualHoistingSpeedMeasured;
                actualRotationSpeed = LowLevelInterfaceOutSignals.ActualRotationSpeedMeasured;
                actualCirculationSpeed = LowLevelInterfaceOutSignals.ActualCirculationSpeedMeasured;
            }


            LowLevelInterfaceOutSignals.HoistingControlGranted = hoistingRequested;//make more advanced, with validation.
            LowLevelInterfaceOutSignals.RotationControlGranted = rotationRequested;
            LowLevelInterfaceOutSignals.CirculationControlGranted = circulationRequested;


            if (_acquiredDrillerSignals[TopOfStringVelocitySetPoint].Any())
            {
                drillerHoistingSpeed = _acquiredDrillerSignals[TopOfStringVelocitySetPoint][0].GetValue<double>();
            }
            if (_acquiredDrillerSignals[SurfaceRPMSetPoint].Any())
            {
                drillerRotationSpeed = _acquiredDrillerSignals[SurfaceRPMSetPoint][0].GetValue<double>();
            }
            if (_acquiredDrillerSignals[FlowRateInSetPoint].Any())
            {
                drillerCirculationSpeed = _acquiredDrillerSignals[FlowRateInSetPoint][0].GetValue<double>();
            }

            if (LowLevelInterfaceOutSignals.HoistingControlGranted)
            {
                DrillingControlSystem.DrawworkController.SetSetPoint(requestedHoistingSpeed);
                if (actualHoistingSpeed > requestedHoistingSpeed)
                {
                    DrillingControlSystem.DrawworkController.SetRateOfChangeSetPoint(requestedHoistingAccelerationLimit);
                }
                else
                { 
                    DrillingControlSystem.DrawworkController.SetRateOfChangeSetPoint(- requestedHoistingAccelerationLimit);
                }
            }
            else
            {
                DrillingControlSystem.DrawworkController.SetSetPoint(drillerHoistingSpeed);
                DrillingControlSystem.DrawworkController.SetRateOfChangeSetPoint(double.NaN);
            }

            LowLevelInterfaceOutSignals.ActualHoistingSpeedSetPoint = DrillingControlSystem.DrawworkController.GetSetPoint();
            LowLevelInterfaceOutSignals.ActualRotationSpeedSetPoint = DrillingControlSystem.TopdriveController.GetSetPoint();
            LowLevelInterfaceOutSignals.ActualCirculationSpeedSetPoint= DrillingControlSystem.MudpumpsController.GetSetPoint();
            

            DateTime now = DateTime.Now;
            List<(string, object, DateTime)> writeData;
            lock (LowLevelInterfaceOutSignals)
            {
                writeData = outProperties.Select(p => (p.Name, p.GetValue(LowLevelInterfaceOutSignals)!, now)).ToList();
            }
            _dwisClient.UpdateProvidedVariables(writeData);

        }
        throw new NotImplementedException();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {        throw new NotImplementedException();
    }

    private void OnCancellationRequested() { }

    private void Initialize()
    {
        var manifestIn = BuildManifest(typeof(LowLevelInterfaceInSignals), "LowLevelInterfaceInSignals");
        var manifestOut = BuildManifest(typeof(LowLevelInterfaceOutSignals), "LowLevelInterfaceOutSignals");

        var resIn = _dwisClient.Inject(manifestIn);
        var resOut = _dwisClient.Inject(manifestOut);

        var subData = resIn.ProvidedVariables.Select(pv => (pv.InjectedID.NameSpaceIndex, pv.InjectedID.ID,(object) typeof(LowLevelInterfaceInSignals).GetProperty(pv.ManifestItemID)!)).ToArray();

        _dwisClient.Subscribe(null, SubscriptionDataChanged, subData);

    }

    private void SubscriptionDataChanged(object subscriptionData, UADataChange[] changes)
    {
        if (changes != null && changes.Any()) 
        {
            foreach (var change in changes)
            {
                if (change != null && change.UserData != null && change.UserData is PropertyInfo prop) 
                {
                    lock (LowLevelInterfaceInSignals) 
                    {
                        prop.SetValue(LowLevelInterfaceInSignals, change.Value);
                    }
                }
            }
        }
    }

}
