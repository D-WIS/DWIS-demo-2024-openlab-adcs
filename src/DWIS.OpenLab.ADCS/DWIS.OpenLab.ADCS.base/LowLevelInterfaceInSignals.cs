using System.Reflection;

namespace dwis.openlab.adcs.Base;

public class LowLevelInterfaceInSignals
{

    private static PropertyInfo[] _allProperties = typeof(LowLevelInterfaceInSignals).GetProperties();


    //hoisting 
    public bool? RequestControlHoisting { get; set; } = null;

    public double? RequestedHoistingSpeed { get; set; } = null;
    public double? RequestedHoistingAccelerationLimit { get; set; }
    public bool? RequestedZeroWOB { get; set; } = null;
    public short? HoistingHeartBeat { get; set; } = null;
    public double? MaxRefreshDelayHoistingHeartbeat { get; set; } = null;
    public double? HoistingSMMDelay { get; set; } = null;
    public double? HoistingSMMAccelerationLimit { get; set; } = null;
    public double? HoistingSMMVelocityLimit { get; set; } = null;
    public double? HoistingSMMDisplacementRequest { get; set; } = null;

    //Rotation
    public bool? RequestControlRotation { get; set; } = null;
    public double? RequestedRotationSpeed { get; set; } = null;
    public double? RequestedRotationAccelerationLimit { get;  set; } = null;
    public double? RequestedMaxTorqueLimit { get; set; } = null;
    public bool? RequestedZeroTorque { get; set;  } = null;
    public short? RotationHeartBeat { get; set; } = null;
    public double? MaxRefreshDelayRotationHeartbeat { get; set; } = null;
    public double? RotationSMMDelay { get; set; } = null;
    public double? RotationSMMAccelerationLimit { get; set; } = null;
    public double? RotationSMMSpeedRequest { get; set; } = null;

    //Circulation
    public bool? RequestControlCirculation { get; set; } = null;
    public double? RequestedCirculationFlowRate { get; set; } = null;
    public double? RequestedCirculationAccelerationLimit { get; set; } = null;
    public bool? RequestedOpenIBOP { get; set; } = null;
    public short? CirculationHeartBeat { get; set; } = null;
    public double? MaxRefreshDelayCirculationHeartbeat { get; set; } = null;
    public double? CirculationSMMDelay { get; set; } = null;
    public double? CirculationSMMAccelerationLimit { get; set; } = null;
    public double? CirculationSMMSpeedRequest { get; set; } = null;
}


