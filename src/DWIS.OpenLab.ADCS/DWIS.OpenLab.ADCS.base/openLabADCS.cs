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
        MachineLimits circulationLimits = new MachineLimits() { MachineMaximumSetPoint = 5000.0 / 60000.0, MachineMinimumSetPoint = -0.0001, MachineMaximumRateOfChangeSetPoint = 500.0 / 60000.0, MachineMinimumRateOfChangeSetPoint = -500.0 / 60000.0 };
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
            if (prop.PropertyType == typeof(double) || prop.PropertyType == typeof(double?))
            {
                manifestFile.ProvidedVariables.Add(new ProvidedVariable() { DataType = "double", VariableID = name });
            }
            else if (prop.PropertyType == typeof(short) || prop.PropertyType == typeof(short?))
            {
                manifestFile.ProvidedVariables.Add(new ProvidedVariable() { DataType = "short", VariableID = name });
            }
            else if (prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?)) 
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
        double iBOPOpeningSpeed = 0.1;

        short circulationHeartBeatLastUpdate = 0;
        short rotationHeartBeatLastUpdate = 0;
        short hoistingHeartBeatLastUpdate = 0;
        short slipsHeartBeatLastUpdate = 0;
        short messageHeartBeatLastUpdate = 0;
        DateTime circulationLastUpdate = DateTime.UtcNow;
        DateTime rotationLastUpdate = DateTime.UtcNow;
        DateTime hoistingLastUpdate = DateTime.UtcNow;
        DateTime slipsLastUpdate = DateTime.UtcNow;
        DateTime messageLastUpdate = DateTime.UtcNow;
        DateTime lastTimeStamp = DateTime.UtcNow;
        TimeSpan circulationMaxRefreshInterval = TimeSpan.FromSeconds(500);
        TimeSpan rotationMaxRefreshInterval = TimeSpan.FromSeconds(500);
        TimeSpan hoistingMaxRefreshInterval = TimeSpan.FromSeconds(500);
        TimeSpan slipsMaxRefreshInterval = TimeSpan.FromSeconds(500);
        TimeSpan messageMaxRefreshInterval = TimeSpan.FromSeconds(500);
        TimeSpan lostCommunicationMessageMaxRefreshInterval = TimeSpan.FromSeconds(500);
        DateTime circulationLostCommunicationLastUpdate = DateTime.MinValue;
        DateTime rotationLostCommunicationLastUpdate = DateTime.MinValue;
        DateTime hoistingLostCommunicationLastUpdate = DateTime.MinValue;
        DateTime slipsLostCommunicationLastUpdate = DateTime.MinValue;
        DateTime messageLostCommunicationLastUpdate = DateTime.MinValue;
       

        while (await timer.WaitForNextTickAsync())
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan deltat = now - lastTimeStamp;
            lastTimeStamp = now;
            bool? hoistingRequested, circulationRequested, rotationRequested;

            double? requestedHoistingSpeed,requestedRotationSpeed, requestedCirculationSpeed,  requestedHoistingAccelerationLimit, requestedRotationAccelerationLimit, requestedCirculationAccelerationLimit;

            double drillerHoistingSpeed, drillerRotationSpeed, drillerCirculationSpeed;
            drillerHoistingSpeed = drillerCirculationSpeed = drillerRotationSpeed = 0;

            double actualHoistingSpeed, actualRotationSpeed, actualCirculationSpeed;
            double tj1;
            double openiBOPStatus;
            
            bool? inComingWOBTaraCommand, inComingTOBTaraCommand;
            bool requestediBOPOpen = false;
            double hookload, sft;
            short circulationHeartBeat, rotationHeartBeat, hoistHeartBeat, slipsHeartBeat, messageHeartBeat;
            lock (LowLevelInterfaceOutSignals)
            {
                circulationHeartBeat = LowLevelInterfaceOutSignals.CirculationHeartBeat;
                rotationHeartBeat = LowLevelInterfaceOutSignals.RotationHeartBeat;
                hoistHeartBeat = LowLevelInterfaceOutSignals.HoistingHeartBeat;
                slipsHeartBeat = LowLevelInterfaceOutSignals.SlipsHeartBeat;
                messageHeartBeat = LowLevelInterfaceOutSignals.MessageHeartBeat;
                openiBOPStatus = LowLevelInterfaceOutSignals.OpeniBOPStatus;
            }

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
                if (LowLevelInterfaceInSignals.RequestedOpenIBOP != null)
                {
                    requestediBOPOpen = LowLevelInterfaceInSignals.RequestedOpenIBOP.Value;
                }
            }

            bool circulationLostCommunication = false;
            bool rotationLostCommunication = false;
            bool hoistingLostCommunication = false;
            bool slipsLostCommunication = false;
            bool messageLostCommunication = false;
            lock (LowLevelInterfaceInSignals)
            {
                if (LowLevelInterfaceInSignals.CirculationHeartBeat != null && LowLevelInterfaceInSignals.CirculationHeartBeat != circulationHeartBeatLastUpdate)
                {
                    circulationHeartBeatLastUpdate = LowLevelInterfaceInSignals.CirculationHeartBeat.Value;
                    circulationLastUpdate = now;
                }

                if (LowLevelInterfaceInSignals.RotationHeartBeat != null && LowLevelInterfaceInSignals.RotationHeartBeat != rotationHeartBeatLastUpdate)
                {
                    rotationHeartBeatLastUpdate = LowLevelInterfaceInSignals.RotationHeartBeat.Value;
                    rotationLastUpdate = now;
                }
                if (LowLevelInterfaceInSignals.HoistingHeartBeat != null && LowLevelInterfaceInSignals.HoistingHeartBeat != hoistingHeartBeatLastUpdate)
                {
                    hoistingHeartBeatLastUpdate = LowLevelInterfaceInSignals.HoistingHeartBeat.Value;
                    hoistingLastUpdate = now;
                }
                //if (LowLevelInterfaceInSignals.SlipsHeartBeat != slipsHeartBeatLastUpdate)
                //{
                //    slipsHeartBeatLastUpdate = LowLevelInterfaceInSignals.SlipsHeartBeat;
                //    slipsLastUpdate = now;
                //}
                //if (LowLevelInterfaceInSignals.MessageHeartBeat != messageHeartBeatLastUpdate)
                //{
                //    messageHeartBeatLastUpdate = LowLevelInterfaceInSignals.MessageHeartBeat;
                //    messageLastUpdate = now;
                //}
            }
            TimeSpan elapsed = now - circulationLastUpdate;
            circulationLostCommunication = elapsed > circulationMaxRefreshInterval;
            elapsed = now - rotationLastUpdate;
            rotationLostCommunication = elapsed > rotationMaxRefreshInterval;
            elapsed = now - hoistingLastUpdate;
            hoistingLostCommunication = elapsed > hoistingMaxRefreshInterval;
            // elapsed = now - slipsLastUpdate;
            // slipsLostCommunication = elapsed > slipsMaxRefreshInterval;
            // elapsed = now - messageLastUpdate;
            // messageLostCommunication = elapsed > messageMaxRefreshInterval;

            if (circulationLostCommunication)
            {
                if (now - circulationLostCommunicationLastUpdate > lostCommunicationMessageMaxRefreshInterval)
                {
                    _logger?.LogWarning("Lost communication with circulation system for more than " + circulationMaxRefreshInterval.TotalSeconds + " s.");
                    circulationLostCommunicationLastUpdate = now;
                }
                lock (LowLevelInterfaceOutSignals) 
                {
                    if (LowLevelInterfaceOutSignals.CirculationControlGranted)
                    {
                        _logger?.LogWarning("Apply circulation SMM.");
                        LowLevelInterfaceOutSignals.CirculationControlGranted = false;
                        // ApplyCirculationSMM();
                    }
                }
            }
            else
            {
                circulationLostCommunicationLastUpdate = DateTime.MinValue;
            }
            if (rotationLostCommunication)
            {
                if (now - rotationLostCommunicationLastUpdate > lostCommunicationMessageMaxRefreshInterval)
                {
                    _logger?.LogWarning("Lost communication with rotation system for more than " + rotationMaxRefreshInterval.TotalSeconds + " s.");
                    rotationLostCommunicationLastUpdate = now;
                }
                lock (LowLevelInterfaceOutSignals)
                {
                    if (LowLevelInterfaceOutSignals.RotationControlGranted)
                    {
                        _logger?.LogWarning("Apply rotation SMM.");
                        LowLevelInterfaceOutSignals.RotationControlGranted = false;
                        // ApplyRotationSMM();
                    }
                }
            }
            else
            {
                rotationLostCommunicationLastUpdate = DateTime.MinValue;
            }
            if (hoistingLostCommunication)
            {
                if (now - hoistingLostCommunicationLastUpdate > lostCommunicationMessageMaxRefreshInterval)
                {
                    _logger?.LogWarning("Lost communication with hoisting system for more than " + hoistingMaxRefreshInterval.TotalSeconds + " s.");
                    hoistingLostCommunicationLastUpdate = now;
                }
                lock (LowLevelInterfaceOutSignals)
                {
                    if (LowLevelInterfaceOutSignals.HoistingControlGranted)
                    {
                        _logger?.LogWarning("Apply hoisting SMM.");
                        LowLevelInterfaceOutSignals.HoistingControlGranted = false;
                        // ApplyHoistingSMM();
                    }
                }
            }
            else
            {
                hoistingLostCommunicationLastUpdate = DateTime.MinValue;
            }
            if (slipsLostCommunication)
            {
                if (now - slipsLostCommunicationLastUpdate > lostCommunicationMessageMaxRefreshInterval)
                {
                    _logger?.LogWarning("Lost communication with slips system for more than " + slipsMaxRefreshInterval.TotalSeconds + " s.");
                    slipsLostCommunicationLastUpdate = now;
                }
            }
            else
            {
                slipsLostCommunicationLastUpdate = DateTime.MinValue;
            }
            if (messageLostCommunication)
            {
                if (now - messageLostCommunicationLastUpdate > lostCommunicationMessageMaxRefreshInterval)
                {
                    _logger?.LogWarning("Lost communication with message system for more than " + messageMaxRefreshInterval.TotalSeconds + " s.");
                    messageLostCommunicationLastUpdate = now;
                }
            }
            else
            {
                messageLostCommunicationLastUpdate = DateTime.MinValue;
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

            if (requestediBOPOpen)
            {
                openiBOPStatus += iBOPOpeningSpeed * deltat.TotalSeconds;
                if (openiBOPStatus > 1)
                {
                    openiBOPStatus = 1;
                }
            }
            else
            {
                openiBOPStatus += iBOPOpeningSpeed * deltat.TotalSeconds;
                if (openiBOPStatus < 0)
                {
                    openiBOPStatus = 0;
                }
            }
            lock (LowLevelInterfaceOutSignals)
            {
                LowLevelInterfaceOutSignals.OpeniBOPStatus = openiBOPStatus;
            }
            if (inComingWOBTaraCommand != null && inComingWOBTaraCommand.Value && !wobTaraCommand)
            {
                taraHookload = hookload;
            }
            if (inComingTOBTaraCommand != null && inComingTOBTaraCommand.Value && !tobTaraCommand) 
            {
                taraSurfaceTorque = sft;
            }

            if (inComingTOBTaraCommand != null)
            {
                wobTaraCommand = inComingTOBTaraCommand.Value;
            }
            if (inComingTOBTaraCommand != null)
            {
                tobTaraCommand = inComingTOBTaraCommand.Value;
            }

            LowLevelInterfaceOutSignals.MeasuredSWOB = taraHookload - hookload;

            DrillingControlSystem.DrawworkController.SetActualValue(actualHoistingSpeed);
            DrillingControlSystem.TopdriveController.SetActualValue(actualRotationSpeed);
            DrillingControlSystem.MudpumpsController.SetActualValue(actualCirculationSpeed);

            LowLevelInterfaceOutSignals.HoistingControlGranted = hoistingRequested != null &&  hoistingRequested.Value && !hoistingLostCommunication;//make more advanced, with validation.
            LowLevelInterfaceOutSignals.RotationControlGranted = rotationRequested != null && rotationRequested.Value && !rotationLostCommunication;
            LowLevelInterfaceOutSignals.CirculationControlGranted = circulationRequested != null && circulationRequested.Value && !circulationLostCommunication;


            if (_acquiredDrillerSignals != null && _acquiredDrillerSignals[_drillerTOSVelocitySPTag].Any())
            {
                drillerHoistingSpeed = _acquiredDrillerSignals[_drillerTOSVelocitySPTag][0].GetValue<double>();
            }
            if (_acquiredDrillerSignals != null && _acquiredDrillerSignals[_drillerRotationSPTag].Any())
            {
                drillerRotationSpeed = _acquiredDrillerSignals[_drillerRotationSPTag][0].GetValue<double>();
            }
            if (_acquiredDrillerSignals != null && _acquiredDrillerSignals[_drillerCirculationSPTag].Any())
            {
                drillerCirculationSpeed = _acquiredDrillerSignals[_drillerCirculationSPTag][0].GetValue<double>();
            }
            //hoisting
            if (LowLevelInterfaceOutSignals.HoistingControlGranted && requestedHoistingSpeed != null && requestedHoistingAccelerationLimit != null)
            {
                DrillingControlSystem.DrawworkController.SetSetPoint(requestedHoistingSpeed.Value);
                if (actualHoistingSpeed > requestedHoistingSpeed)
                {
                    DrillingControlSystem.DrawworkController.SetRateOfChangeSetPoint(-requestedHoistingAccelerationLimit.Value);
                }
                else
                { 
                    DrillingControlSystem.DrawworkController.SetRateOfChangeSetPoint( requestedHoistingAccelerationLimit.Value);
                }
            }
            else
            {
                DrillingControlSystem.DrawworkController.SetSetPoint(drillerHoistingSpeed);
                DrillingControlSystem.DrawworkController.SetRateOfChangeSetPoint(double.NaN);
            }
            //rotation
            if (LowLevelInterfaceOutSignals.RotationControlGranted && requestedRotationSpeed != null && requestedRotationAccelerationLimit != null)
            {
                DrillingControlSystem.TopdriveController.SetSetPoint(requestedRotationSpeed.Value);
                if (actualRotationSpeed > requestedRotationSpeed)
                {
                    DrillingControlSystem.TopdriveController.SetRateOfChangeSetPoint(-requestedRotationAccelerationLimit.Value);
                }
                else
                {
                    DrillingControlSystem.TopdriveController.SetRateOfChangeSetPoint(requestedRotationAccelerationLimit.Value);
                }
            }
            else
            {
                DrillingControlSystem.TopdriveController.SetSetPoint(drillerRotationSpeed);
                DrillingControlSystem.TopdriveController.SetRateOfChangeSetPoint(double.NaN);
            }

            //circulation
            if (LowLevelInterfaceOutSignals.CirculationControlGranted && requestedCirculationSpeed != null && requestedCirculationAccelerationLimit != null)
            {
                DrillingControlSystem.MudpumpsController.SetSetPoint(requestedCirculationSpeed.Value);
                if (actualCirculationSpeed > requestedCirculationSpeed)
                {
                    DrillingControlSystem.MudpumpsController.SetRateOfChangeSetPoint(-requestedCirculationAccelerationLimit.Value);
                }
                else
                {
                    DrillingControlSystem.MudpumpsController.SetRateOfChangeSetPoint(requestedCirculationAccelerationLimit.Value);
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

            lock (LowLevelInterfaceOutSignals)
            {
                LowLevelInterfaceOutSignals.CirculationHeartBeat = (short)((circulationHeartBeat + 1) % 255);
                LowLevelInterfaceOutSignals.RotationHeartBeat = (short)((rotationHeartBeat + 1) % 255);
                LowLevelInterfaceOutSignals.HoistingHeartBeat = (short)((hoistHeartBeat + 1) % 255);
                LowLevelInterfaceOutSignals.SlipsHeartBeat = (short)((slipsHeartBeat + 1) % 255);
                LowLevelInterfaceOutSignals.MessageHeartBeat = (short)((messageHeartBeat + 1) % 255);
            }

            now = DateTime.Now;
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
