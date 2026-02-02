using dwis.openlab.adcs.Base;
using DWIS.API.DTO;
using DWIS.Client.ReferenceImplementation;
using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.OpenLab.ADCS.LowLevelInterfaceClient;
using System.Collections.Generic;
using System.Reflection;

string openLabManifestPath = @"manifest/simulatorManifest.json";

DWISClientOPCF dwisClient = new DWISClientOPCF(new DefaultDWISClientConfiguration(), null);
LowLevelInterfaceClient lowLevelInterfaceClient = new LowLevelInterfaceClient(dwisClient);
TimeSpan replicationSpan = TimeSpan.FromMilliseconds(500);
(string manifestItem, string propertyName)[] baseMapping =
    {
        ("FlowRateInSetPoint", nameof(LowLevelInterfaceOutSignals.ActualCirculationSpeedSetPoint)),
        ("TopOfStringVelocitySetPoint", nameof(LowLevelInterfaceOutSignals.ActualHoistingSpeedSetPoint)),
        ("SurfaceRPMSetPoint", nameof(LowLevelInterfaceOutSignals.ActualRotationSpeedSetPoint)),
        ("HookLoad", nameof(LowLevelInterfaceOutSignals.MeasuredHookload)),
        ("SPP", nameof(LowLevelInterfaceOutSignals.MeasuredStandPipePressure)),
        ("SurfaceTorque", nameof(LowLevelInterfaceOutSignals.MeasuredRotationTorque)),
        ("FlowRateIn", nameof(LowLevelInterfaceOutSignals.ActualCirculationSpeedMeasured)),
        //("ActivePitVolume", nameof(LowLevelInterfaceOutSignals.ta)),
        //("BitDepth", nameof(LowLevelInterfaceOutSignals.dep)),
        //("DownholeECD", nameof(LowLevelInterfaceOutSignals.ActualCirculationSpeedMeasured)),
        //("DownholePressure", nameof(LowLevelInterfaceOutSignals.ActualCirculationSpeedMeasured)),
        //("FlowRateOut", nameof(LowLevelInterfaceOutSignals.meas)),
        ("SurfaceRPM", nameof(LowLevelInterfaceOutSignals.ActualRotationSpeedMeasured)),
        //("TD", nameof(LowLevelInterfaceOutSignals.ActualCirculationSpeedMeasured)),
        ("WOB", nameof(LowLevelInterfaceOutSignals.MeasuredSWOB)),
        //("InstantaneousROP", nameof(LowLevelInterfaceOutSignals.spee)),
        //("HookPosition", nameof(LowLevelInterfaceOutSignals.po)),
        ("HookVelocity", nameof(LowLevelInterfaceOutSignals.ActualHoistingSpeedMeasured)),
        //("", nameof(LowLevelInterfaceOutSignals.ActualCirculationSpeedMeasured)),
};




if (File.Exists(openLabManifestPath))
{
    var json = File.ReadAllText(openLabManifestPath);
    if (!string.IsNullOrEmpty(json))
    {
        ManifestFile openLabManifestFile = ManifestFile.FromJsonString(json);

        var injectionResults = dwisClient.Inject(openLabManifestFile);
        List<(InjectionMapping injectionMapping, PropertyInfo llOutProperty)> mapping = BuildMapping(injectionResults);

        if (mapping != null 
            && mapping.Any() 
            && mapping.First().injectionMapping != null
            && mapping.All(m =>(m.injectionMapping!= null 
                                    && m.injectionMapping.InjectedID.NameSpaceIndex == mapping.First().injectionMapping.InjectedID.NameSpaceIndex)) 
            && dwisClient.GetNameSpace(mapping.First().injectionMapping.InjectedID.NameSpaceIndex, out string ns) 
            && dwisClient.GetNameSpaceIndex(ns, out ushort nsIdx))
        {

            PeriodicTimer timer = new PeriodicTimer(replicationSpan);

            while (await timer.WaitForNextTickAsync())
            {
                var llSignalsOut = lowLevelInterfaceClient.GetOutSignals();
                if (llSignalsOut != null)
                {
                    DateTime now = DateTime.Now;
                    var toWrite = mapping.Select(m => (nsIdx, m.injectionMapping.InjectedID.ID, m.llOutProperty.GetValue(llSignalsOut)!, now)).ToArray();
                    if (toWrite != null && toWrite.Any())
                    {
                        dwisClient.UpdateAnyVariables(toWrite);
                    }
                }
            }
        }
    }
}

Console.ReadLine();

 List<(InjectionMapping injectionMapping, PropertyInfo llOutProperty)> BuildMapping(ManifestInjectionResult injectionResult) 
{
    List<(InjectionMapping injectionMapping, PropertyInfo llOutProperty)> mapping = new List<(InjectionMapping injectionMapping, PropertyInfo llOutProperty)>();

    var providedVariables = injectionResult.ProvidedVariables;

    foreach (var variableMapping in baseMapping)
    {
        var injectionMapping = providedVariables.FirstOrDefault(im => im.ManifestItemID == variableMapping.manifestItem);
        if (injectionMapping != null) 
        {
            var prop = typeof(LowLevelInterfaceOutSignals).GetProperty(variableMapping.propertyName);
            if (prop != null) 
            {
                mapping.Add((injectionMapping, prop));
            }
        }
    }
    return mapping;
}