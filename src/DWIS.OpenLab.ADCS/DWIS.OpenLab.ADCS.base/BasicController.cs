namespace dwis.openlab.adcs.Base;

public class BasicController
{
    private ILogger<BasicController>? _logger;
    private object _lock = new object();
    public string Name { get; private set; }

    private double _setPoint;
    private double _rateOfChangeSetPoint;
    private double _actualValue;
    private double _command;

    private ushort _setPointNamespaceIndex;
    private string _setPointIdentifier;
    private TimeSpan _loopSpan = TimeSpan.FromMilliseconds(150);


    public double MinimumSetPoint { get; set; }
    public double MaximumSetPoint { get; set; }
    public double MaximumRateOfChangeSetPoint { get; set; }
    public double MinimumRateOfChangeSetPoint { get; set; }

    private MachineLimits _machineLimits;

    public BasicController( MachineLimits machineLimits, ILogger<BasicController>? logger, string name)
    {
        _machineLimits = machineLimits;
        _logger = logger;
        Name = name;
    }

    public void SetActualValue(double val)
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
                rateOfChangeSetPoint = System.Math.Max(MinimumRateOfChangeSetPoint, rateOfChangeSetPoint);
            }
            if (!double.IsNaN(MaximumSetPoint))
            {
                rateOfChangeSetPoint = System.Math.Min(MaximumRateOfChangeSetPoint, rateOfChangeSetPoint);
            }
            lock (_lock)
            {
                _rateOfChangeSetPoint = System.Math.Max(_machineLimits.MachineMinimumRateOfChangeSetPoint, System.Math.Min(_machineLimits.MachineMaximumRateOfChangeSetPoint, rateOfChangeSetPoint));
            }
        }
        else { _rateOfChangeSetPoint = double.NaN; }
        return _rateOfChangeSetPoint;
    }

    public async void Start(CancellationToken token)
    {
        PeriodicTimer periodicTimer = new PeriodicTimer(_loopSpan);
        try
        {
            DateTime lastUpdate = DateTime.UtcNow;
            while (await periodicTimer.WaitForNextTickAsync())
            {
                DateTime now = DateTime.UtcNow;
                TimeSpan elapsed = now- lastUpdate;
                lastUpdate = now;
                double currentValue = GetActualValue();
                double currentSetPoint = GetSetPoint();
                double rateOfChange = GetRateOfChangeSetPoint();

                if (!double.IsNaN(currentValue) && !double.IsNaN(currentSetPoint) && currentValue != currentSetPoint)
                {
                    double absoluteRateOfChange = (currentSetPoint - currentValue) / _loopSpan.TotalSeconds;


                    if (double.IsNaN(rateOfChange) || rateOfChange == 0)
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

                    if (absoluteRateOfChange < 0 && rateOfChange < absoluteRateOfChange)
                    {
                        rateOfChange = absoluteRateOfChange;
                    }
                    else if (absoluteRateOfChange > 0 && rateOfChange > absoluteRateOfChange) 
                    {
                        rateOfChange = absoluteRateOfChange;
                    }

                    double command = currentValue + rateOfChange * elapsed.TotalSeconds;  //not sure this will provide the right acceleration...

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
