namespace dwis.openlab.adcs.Base;

public class BasicController
{
    private IOPCUADWISClient _dwisClient;
    private ILogger<BasicController>? _logger;
    private object _lock = new object();
    public string Name { get; private set; }

    private double _setPoint;
    private double _rateOfChangeSetPoint;
    private double _actualValue;
    private double _command;

    private ushort _setPointNamespaceIndex;
    private string _setPointIdentifier;
    private TimeSpan _loopSpan = TimeSpan.FromMilliseconds(100);


    public double MinimumSetPoint { get; set; }
    public double MaximumSetPoint { get; set; }
    public double MaximumRateOfChangeSetPoint { get; set; }
    public double MinimumRateOfChangeSetPoint { get; set; }

    private MachineLimits _machineLimits;

    public BasicController(IOPCUADWISClient dwisClient, MachineLimits machineLimits, ILogger<BasicController>? logger, string name)
    {
        _dwisClient = dwisClient;
        _machineLimits = machineLimits;
        _logger = logger;
        Name = name;
    }

    internal void SetActualValue(double val)
    {
        lock (_lock) { _actualValue = val; }
    }

    public double GetActualValue()
    {
        double val = double.NaN;
        lock (_lock) { val = _actualValue; }
        return val;

    }
    public double GetSetPoint()
    {
        double val = double.NaN;
        lock (_lock) { val = _setPoint; }
        return val;
    }

    private double GetRateOfChangeSetPoint()
    {
        double val = double.NaN;
        lock (_lock) { val = _rateOfChangeSetPoint; }
        return val;
    }

    public double GetCommand()
    {
        double val = double.NaN;
        lock (_lock) { val = _command; }
        return val;
    }
    private void SetCommand(double val)
    {
        lock (_lock) { _command = val; }
    }
    public double SetSetPoint(double setPoint)
    {
        double previousSetPoint = setPoint;
        if (!double.IsNaN(setPoint))
        {
            if (!double.IsNaN(MinimumSetPoint))
            {
                setPoint = System.Math.Max(MinimumSetPoint, setPoint);
            }
            if (!double.IsNaN(MaximumSetPoint))
            {
                setPoint = System.Math.Min(MaximumSetPoint, setPoint);
            }
            lock (_lock)
            {
                _setPoint = System.Math.Max(_machineLimits.MachineMinimumSetPoint, System.Math.Min(_machineLimits.MachineMaximumSetPoint, setPoint));
            }
        }

        return _setPoint;
    }

    public double SetRateOfChangeSetPoint(double rateOfChangeSetPoint)
    {
        if (!double.IsNaN(rateOfChangeSetPoint))
        {
            if (!double.IsNaN(MinimumSetPoint))
            {
                rateOfChangeSetPoint = System.Math.Max(MinimumSetPoint, rateOfChangeSetPoint);
            }
            if (!double.IsNaN(MaximumSetPoint))
            {
                rateOfChangeSetPoint = System.Math.Min(MaximumSetPoint, rateOfChangeSetPoint);
            }
            lock (_lock)
            {
                _rateOfChangeSetPoint = System.Math.Max(_machineLimits.MachineMinimumRateOfChangeSetPoint, System.Math.Min(_machineLimits.MachineMaximumRateOfChangeSetPoint, rateOfChangeSetPoint));
            }
        }
        return _rateOfChangeSetPoint;
    }

    public async void Start(CancellationToken token)
    {
        PeriodicTimer periodicTimer = new PeriodicTimer(_loopSpan);
        try
        {
            while (await periodicTimer.WaitForNextTickAsync())
            {
                double currentValue = GetActualValue();
                double currentSetPoint = GetSetPoint();
                double rateOfChange = GetRateOfChangeSetPoint();

                if (!double.IsNaN(currentValue) && !double.IsNaN(currentSetPoint) && currentValue != currentSetPoint)
                {
                    if (double.IsNaN(rateOfChange))
                    {
                        if (currentValue > currentSetPoint)
                        {
                            rateOfChange = _machineLimits.MachineMinimumRateOfChangeSetPoint;
                        }
                        else
                        {
                            rateOfChange = _machineLimits.MachineMaximumRateOfChangeSetPoint;
                        }
                    }
                    else
                    {
                        rateOfChange = System.Math.Sign(currentSetPoint - currentValue) * System.Math.Abs(rateOfChange);
                    }


                    double command = currentValue + rateOfChange * _loopSpan.TotalSeconds;  //not sure this will provide the right acceleration...

                    SetCommand(command);

                    //_dwisClient.UpdateAnyVariables((_setPointNamespaceIndex, _setPointIdentifier, command, DateTime.Now));
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            //exit
        }
    }
}
