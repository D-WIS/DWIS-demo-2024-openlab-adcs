using System.Reflection;

namespace dwis.openlab.adcs.Base;

public class LowLevelInterfaceInSignals
{

    private static PropertyInfo[] _allProperties = typeof(LowLevelInterfaceInSignals).GetProperties();


    //hoisting 
    public bool RequestControlHoisting { get; set; }

    public double RequestedHoistingSpeed { get; set; }
    public double RequestedHoistingAccelerationLimit { get; set; }
    public bool RequestedZeroWOB { get; set; }
    public bool HoistingHearbeat { get; set; }
    public double MaxRefreshDelayHoistingHeartbeat { get; set; }
    public double HoistingSMMDelay { get; set; }
    public double HoistingSMMAccelerationLimit { get; set; }
    public double HoistingSMMVelocityLimit { get; set; }
    public double HoistingSMMDisplacementRequest { get; set; }

    //Rotation
    public bool RequestControlRotation { get; set; }
    public double RequestedRotationSpeed { get; set; }
    public double RequestedRotationAccelerationLimit { get;  set; }
    public double RequestedMaxTorqueLimit { get; set; }
    public bool RequestedZeroTorque { get; set;  }
    public bool RotationHeartBeat { get; set; }
    public double MaxRefreshDelayRotationHeartbeat { get; set; }
    public double RotationSMMDelay { get; set; }
    public double RotationSMMAccelerationLimit { get; set; }
    public double RotationSMMSpeedRequest { get; set; }

    //Circulation
    public bool RequestControlCirculation { get; set; }
    public double RequestedCirculationFlowRate { get; set; }
    public double RequestedCirculationAccelerationLimit { get; set; }
    public bool RequestedOpenIBOP { get; set; }
    public bool CirculationHeartBeat { get; set; }
    public double MaxRefreshDelayCirculationHeartbeat { get; set; }
    public double CirculationSMMDelay { get; set; }
    public double CirculationSMMAccelerationLimit { get; set; }
    public double CirculationSMMSpeedRequest { get; set; }
}


