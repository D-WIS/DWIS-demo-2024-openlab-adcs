using DWIS.API.DTO;
using System.Reflection;

namespace dwis.openlab.adcs.Base;

public class openLabADCS : IHostedService
{
    private IOPCUADWISClient _dwisClient;
    private ILogger<openLabADCS>? _logger;

    public DCS DrillingControlSystem { get; set; }

    public LowLevelInterfaceInSignals LowLevelInterfaceInSignals { get; set; } = new LowLevelInterfaceInSignals();
    public LowLevelInterfaceOutSignals LowLevelInterfaceOutSignals { get; set; } = new LowLevelInterfaceOutSignals();   

    private AcquiredSignals? _acquiredDrillerSignals;
    private string _drillerTOSVelocitySPTag;
    private string _drillerRotationSPTag;
    private string _drillerCirculationSPTag;

    public openLabADCS(IOPCUADWISClient dwisClient,ILoggerFactory? loggerFactory, AcquiredSignals? acquiredDrillerSignals, string drillerTosvSPTag, string drillerRotSPTag, string drillerCircSPTag)
    {
        _dwisClient = dwisClient;
        _logger = loggerFactory != null ?  loggerFactory.CreateLogger<openLabADCS>() : null;
        _acquiredDrillerSignals = acquiredDrillerSignals;

        _drillerTOSVelocitySPTag = drillerTosvSPTag;
        _drillerRotationSPTag = drillerRotSPTag;
        _drillerCirculationSPTag = drillerCircSPTag;

        MachineLimits hoistingLimits = new MachineLimits() { MachineMaximumSetPoint = 5, MachineMinimumSetPoint = -5, MachineMaximumRateOfChangeSetPoint = .05, MachineMinimumRateOfChangeSetPoint = -0.5 };
        MachineLimits rotationLimits = new MachineLimits() {MachineMaximumSetPoint = 5, MachineMinimumSetPoint = -1, MachineMaximumRateOfChangeSetPoint = .5, MachineMinimumRateOfChangeSetPoint = -1 };
        MachineLimits circulationLimits = new MachineLimits() { MachineMaximumSetPoint = 5000.0 / 60000.0, MachineMinimumSetPoint = -0.0001, MachineMaximumRateOfChangeSetPoint = 50.0 / 60000.0, MachineMinimumRateOfChangeSetPoint = -50.0 / 60000.0 };
        //DrillingControlSystem = new DCS(dwisClient, new RigMachineLimits(), )
        RigMachineLimits limits = new RigMachineLimits(hoistingLimits, rotationLimits, circulationLimits);

        DrillingControlSystem = new DCS(limits, loggerFactory);

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

        _logger?.LogInformation("Start the DCS");
        _ = Task.Run(()=> DrillingControlSystem.Start(cancellationToken));

        var outProperties = typeof(LowLevelInterfaceOutSignals).GetProperties();

        PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));

        bool wobTaraCommand = false;
        bool tobTaraCommand = false;
        double taraHookload = double.NaN;
        double taraSurfaceTorque = double.NaN;

        while (await timer.WaitForNextTickAsync())
        {
            bool hoistingRequested, circulationRequested, rotationRequested;

            double requestedHoistingSpeed,requestedRotationSpeed, requestedCirculationSpeed,  requestedHoistingAccelerationLimit, requestedRotationAccelerationLimit, requestedCirculationAccelerationLimit;

            double drillerHoistingSpeed, drillerRotationSpeed, drillerCirculationSpeed;
            drillerHoistingSpeed = drillerCirculationSpeed = drillerRotationSpeed = 0;

            double actualHoistingSpeed, actualRotationSpeed, actualCirculationSpeed;
            double tj1;
            
            bool inComingWOBTaraCommand, inComingTOBTaraCommand;
            double hookload, sft;

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

                inComingWOBTaraCommand = LowLevelInterfaceInSignals.RequestedZeroWOB;
                inComingTOBTaraCommand = LowLevelInterfaceInSignals.RequestedZeroTorque;
            }

            lock (LowLevelInterfaceOutSignals) 
            {
                actualHoistingSpeed = LowLevelInterfaceOutSignals.ActualHoistingSpeedMeasured;
                actualRotationSpeed = LowLevelInterfaceOutSignals.ActualRotationSpeedMeasured;
                actualCirculationSpeed = LowLevelInterfaceOutSignals.ActualCirculationSpeedMeasured;
                hookload = LowLevelInterfaceOutSignals.MeasuredHookload;
                sft = LowLevelInterfaceOutSignals.MeasuredRotationTorque;
                tj1 = LowLevelInterfaceOutSignals.ToolJoint1Elevation;
            }

            if (inComingWOBTaraCommand && !wobTaraCommand)
            {
                taraHookload = hookload;
            }
            if (inComingTOBTaraCommand && !tobTaraCommand) 
            {
                taraSurfaceTorque = sft;
            }

            wobTaraCommand = inComingTOBTaraCommand;
            tobTaraCommand = inComingTOBTaraCommand;

            LowLevelInterfaceOutSignals.MeasuredSWOB = taraHookload - hookload;

            DrillingControlSystem.DrawworkController.SetActualValue(actualHoistingSpeed);
            DrillingControlSystem.TopdriveController.SetActualValue(actualRotationSpeed);
            DrillingControlSystem.MudpumpsController.SetActualValue(actualCirculationSpeed);

            LowLevelInterfaceOutSignals.HoistingControlGranted = hoistingRequested;//make more advanced, with validation.
            LowLevelInterfaceOutSignals.RotationControlGranted = rotationRequested;
            LowLevelInterfaceOutSignals.CirculationControlGranted = circulationRequested;


            if (_acquiredDrillerSignals[_drillerTOSVelocitySPTag].Any())
            {
                drillerHoistingSpeed = _acquiredDrillerSignals[_drillerTOSVelocitySPTag][0].GetValue<double>();
            }
            if (_acquiredDrillerSignals[_drillerRotationSPTag].Any())
            {
                drillerRotationSpeed = _acquiredDrillerSignals[_drillerRotationSPTag][0].GetValue<double>();
            }
            if (_acquiredDrillerSignals[_drillerCirculationSPTag].Any())
            {
                drillerCirculationSpeed = _acquiredDrillerSignals[_drillerCirculationSPTag][0].GetValue<double>();
            }
            //hoisting
            if (LowLevelInterfaceOutSignals.HoistingControlGranted)
            {
                DrillingControlSystem.DrawworkController.SetSetPoint(requestedHoistingSpeed);
                if (actualHoistingSpeed > requestedHoistingSpeed)
                {
                    DrillingControlSystem.DrawworkController.SetRateOfChangeSetPoint(-requestedHoistingAccelerationLimit);
                }
                else
                { 
                    DrillingControlSystem.DrawworkController.SetRateOfChangeSetPoint( requestedHoistingAccelerationLimit);
                }
            }
            else
            {
                DrillingControlSystem.DrawworkController.SetSetPoint(drillerHoistingSpeed);
                DrillingControlSystem.DrawworkController.SetRateOfChangeSetPoint(double.NaN);
            }
            //rotation
            if (LowLevelInterfaceOutSignals.RotationControlGranted)
            {
                DrillingControlSystem.TopdriveController.SetSetPoint(requestedRotationSpeed);
                if (actualRotationSpeed > requestedRotationSpeed)
                {
                    DrillingControlSystem.TopdriveController.SetRateOfChangeSetPoint(-requestedRotationAccelerationLimit);
                }
                else
                {
                    DrillingControlSystem.TopdriveController.SetRateOfChangeSetPoint(requestedRotationAccelerationLimit);
                }
            }
            else
            {
                DrillingControlSystem.TopdriveController.SetSetPoint(drillerRotationSpeed);
                DrillingControlSystem.TopdriveController.SetRateOfChangeSetPoint(double.NaN);
            }

            //circulation
            if (LowLevelInterfaceOutSignals.CirculationControlGranted)
            {
                DrillingControlSystem.MudpumpsController.SetSetPoint(requestedCirculationSpeed);
                if (actualCirculationSpeed > requestedCirculationSpeed)
                {
                    DrillingControlSystem.MudpumpsController.SetRateOfChangeSetPoint(-requestedCirculationAccelerationLimit);
                }
                else
                {
                    DrillingControlSystem.MudpumpsController.SetRateOfChangeSetPoint(requestedCirculationAccelerationLimit);
                }
            }
            else
            {
                DrillingControlSystem.MudpumpsController.SetSetPoint(drillerCirculationSpeed);
                DrillingControlSystem.MudpumpsController.SetRateOfChangeSetPoint(double.NaN);
            }

            LowLevelInterfaceOutSignals.ActualHoistingSpeedSetPoint = DrillingControlSystem.DrawworkController.GetSetPoint();
            LowLevelInterfaceOutSignals.ActualRotationSpeedSetPoint = DrillingControlSystem.TopdriveController.GetSetPoint();
            LowLevelInterfaceOutSignals.ActualCirculationSpeedSetPoint= DrillingControlSystem.MudpumpsController.GetSetPoint();
            tj1 -= .5;
            lock (LowLevelInterfaceOutSignals)
            {
                LowLevelInterfaceOutSignals.ToolJoint1Elevation = tj1;
            }

            LowLevelInterfaceOutSignals.ToolJoint2Elevation = tj1 - 10.0;
            LowLevelInterfaceOutSignals.ToolJoint3Elevation = tj1 - 20.0;
            LowLevelInterfaceOutSignals.ToolJoint4Elevation = tj1 - 29.0;

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

        string json = ManifestInjectionResult.ToJsonString(resIn);
        System.IO.File.WriteAllText("signalsInInjectionResults.json", json);
        json = ManifestInjectionResult.ToJsonString(resOut);
        System.IO.File.WriteAllText("signalsOutInjectionResults.json", json);

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
