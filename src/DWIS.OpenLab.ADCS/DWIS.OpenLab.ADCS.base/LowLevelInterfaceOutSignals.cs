
using DWIS.API.DTO;
using System.Reflection;

namespace dwis.openlab.adcs.Base;

public class LowLevelInterfaceOutSignals
{
    //hoisting
    public bool HoistingControlGranted { get; set; }
    public double ActualHoistingSpeedSetPoint { get; set; }
    public double ActualHoistingSpeedMeasured { get; set; }
    public double MeasuredHookload { get; set; }
    public double MeasuredSWOB { get; set; }
    public bool SWOBTaring { get; set; }
    public double ToolJoint1Elevation { get; set; }
    public double ToolJoint2Elevation { get; set; }
    public double ToolJoint3Elevation { get; set; }
    public double ToolJoint4Elevation { get; set; }
    public double ToolJoint1AtMaxDrillElevation { get; set; }
    public bool TooLowElevationAlarm { get; set; }
    public bool TooHighElevationAlarm { get; set; }
    public short HoistingHeartBeat { get; set; }
    public double MaxHoistingRefreshDelay { get; set; }

    //rotation
    public bool RotationControlGranted { get; set; }
    public bool RotationSystemConnected { get; set; }
    public double ActualRotationSpeedSetPoint { get; set; }
    public double ActualRotationSpeedMeasured { get; set; }
    public double MeasuredRotationTorque { get; set; }
    public double ActualMaxRotationTorqueLimit { get; set; }
    public bool ZeroTorqueInProgress { get; set; }
    public short RotationHeartBeat { get; set; }
    public double MaxRotationRefreshDelay { get; set; }

    //circulation
    public bool CirculationControlGranted { get; set; }
    public double ActualCirculationSpeedSetPoint { get; set; }
    public double ActualCirculationSpeedMeasured { get; set; }
    public double MeasuredStandPipePressure { get; set; }
    public bool OpeniBOPCommand { get; set; }
    public double OpeniBOPStatus { get; set; }
    public short CirculationHeartBeat { get; set; }
    public double MaxCirculationRefreshDelay { get; set; }

    //slips
    public bool SlipsControlGranted { get; set; }
    public bool ActualSlipsState { get; set; }
    public short SlipsHeartBeat { get; set; }
    public double MaxSlipsRefreshDelay { get; set; }

    //booster pumping

    //messages

    public bool RequestedMessageAccepted { get; set; }
    public uint MaxMessageLength { get; set; }
    public bool FixedMessage { get; set; }
    public char TextFieldSeparator { get; set; }
    public short MessageHeartBeat { get; set; }
    public double MaxMessageRefreshDelay { get; set; }


    private static PropertyInfo[] _allProperties = typeof(LowLevelInterfaceOutSignals).GetProperties();


    public static bool UpdateVariables(LowLevelInterfaceOutSignals signals, IOPCUADWISClient client)
    {
        DateTime now = DateTime.Now;
        var input = _allProperties.Select(prop => (prop.Name, prop.GetValue(signals)!, now)).ToList();
        if (input != null)
        {
            return client.UpdateProvidedVariables(input);
        }
        else return false;
    }

    public static ManifestFile GetManifest(string manifestName, string providerName)
    {
        ManifestFile manifest = new ManifestFile() { InjectedNodes = new List<InjectedNode>(), InjectedReferences = new List<InjectedReference>(), InjectedVariables = new List<InjectedVariable>(), ProvidedVariables = new List<ProvidedVariable>(), Provider = new InjectionProvider() { Name = providerName }, InjectionInformation = new InjectionInformation(), ManifestName = manifestName };

        foreach (var property in _allProperties)
        {
            manifest.AddProvidedVariable(property.Name, GetVariableType(property), 0, null);
            manifest.AddNode(property.Name, Nouns.DrillingDataPoint);
            manifest.AddReference(manifest.InjectionInformation.InjectedNodesNamespaceAlias, property.Name,"http://ddhub.no/"+ Verbs.HasDynamicValue, manifest.InjectionInformation.ProvidedVariablesNamespaceAlias, property.Name);           
        }

        AddClasses(manifest, nameof(LowLevelInterfaceOutSignals.ActualHoistingSpeedSetPoint), Nouns.HookVelocity, Nouns.SetPoint);
        AddClasses(manifest, nameof(LowLevelInterfaceOutSignals.ActualHoistingSpeedMeasured), Nouns.HookVelocity, Nouns.Measurement);
        AddClasses(manifest, nameof(LowLevelInterfaceOutSignals.MeasuredHookload), Nouns.HookLoad, Nouns.Measurement);
        AddClasses(manifest, nameof(LowLevelInterfaceOutSignals.MeasuredSWOB), Nouns.WOB, Nouns.Measurement);
        AddClasses(manifest, nameof(LowLevelInterfaceOutSignals.ActualRotationSpeedSetPoint), Nouns.SurfaceRPM, Nouns.SetPoint);
        AddClasses(manifest, nameof(LowLevelInterfaceOutSignals.ActualRotationSpeedMeasured), Nouns.SurfaceRPM, Nouns.Measurement);
        AddClasses(manifest, nameof(LowLevelInterfaceOutSignals.MeasuredRotationTorque), Nouns.SurfaceTorque, Nouns.Measurement);
        AddClasses(manifest, nameof(LowLevelInterfaceOutSignals.ActualCirculationSpeedSetPoint), Nouns.FlowRateIn, Nouns.SetPoint);
        AddClasses(manifest, nameof(LowLevelInterfaceOutSignals.ActualCirculationSpeedMeasured), Nouns.FlowRateIn, Nouns.Measurement);
        return manifest;
    }

    private static void AddClasses(ManifestFile manifest, string propertyName, params string[] classes)
    {
        foreach (var className in classes) 
        {
            manifest.AddReference(manifest.InjectionInformation.InjectedNodesNamespaceAlias, propertyName, "http://ddhub.no/" + Verbs.BelongsToClass, "http://ddhub.no/", "http://ddhub.no/" + className);
        }
    }


    private static string GetVariableType(PropertyInfo property)
    {
        if (property.PropertyType == typeof(double))
        { return "double"; }
        if (property.PropertyType == typeof(float))
        { return "float"; }
        if (property.PropertyType == typeof(string))
        { return "string"; }
        if (property.PropertyType == typeof(bool))
        { return "boolean"; }
        if (property.PropertyType == typeof(int))
        { return "int"; }
        if (property.PropertyType == typeof(long))
        { return "long"; }
        if (property.PropertyType == typeof(uint))
        { return "uint"; }
        if (property.PropertyType == typeof(ulong))
        { return "ulong"; }
        if (property.PropertyType == typeof(short))
        { return "short"; }
        if (property.PropertyType == typeof(ushort))
        { return "ushort"; }
        if (property.PropertyType == typeof(char))
        { return "char"; }
        return string.Empty;
    }










}


