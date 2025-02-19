using dwis.openlab.adcs.Base;
using DWIS.API.DTO;
using DWIS.Client.ReferenceImplementation;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace DWIS.OpenLab.ADCS.LowLevelInterfaceClient
{
    public class LowLevelInterfaceClient
    {
        private IOPCUADWISClient _dwisClient;
        private ILogger<LowLevelInterfaceClient>? _logger;

        private Lock _lock = new Lock();

        private LowLevelInterfaceOutSignals _lowLevelInterfaceOutSignals = new LowLevelInterfaceOutSignals();

        private static PropertyInfo[] _allOutProps = typeof(LowLevelInterfaceOutSignals).GetProperties();

        private Dictionary<PropertyInfo, (string id, ushort nsIndex)> _inSignalsDictionary = new Dictionary<PropertyInfo, (string id, ushort nsIndex)>();

        public LowLevelInterfaceClient(IOPCUADWISClient dwisClient, ManifestInjectionResult? outSignalsInjectionResults = null, ManifestInjectionResult? inSignalsInjectionResults = null, ILogger<LowLevelInterfaceClient>? logger = null)
        {
            _dwisClient = dwisClient;
            _logger = logger;

            if (outSignalsInjectionResults == null) 
            {
                string json = System.IO.File.ReadAllText("signalsOutInjectionResults.json");
                outSignalsInjectionResults = ManifestInjectionResult.FromJsonString(json);
            }
            if (outSignalsInjectionResults != null)
            {
                InitOutSignals(outSignalsInjectionResults);
            }
            else 
            {
                _logger?.LogCritical("Could not initialize out signals");
                throw new Exception("Could not initialize out signals");
            }
            if (inSignalsInjectionResults == null)
            {
                string json = System.IO.File.ReadAllText("signalsInInjectionResults.json");
                inSignalsInjectionResults = ManifestInjectionResult.FromJsonString(json);
            }
            if (inSignalsInjectionResults != null)
            {
                InitInSignals(inSignalsInjectionResults);
            }
            else
            {
                _logger?.LogCritical("Could not initialize in signals");
                throw new Exception("Could not initialize in signals");
            }
        }

        public void WriteInSignals(LowLevelInterfaceInSignals lowLevelInterfaceInSignals, DateTime souceTime)
        {
            _dwisClient.UpdateAnyVariables(_inSignalsDictionary.Select(kvp => (kvp.Value.nsIndex, kvp.Value.id, kvp.Key.GetValue(lowLevelInterfaceInSignals), souceTime)).ToArray());
        }

        public LowLevelInterfaceOutSignals GetOutSignals()
        {
            LowLevelInterfaceOutSignals res = new LowLevelInterfaceOutSignals();

            _lock.Enter();
            foreach (var prop in _allOutProps) 
            {
                prop.SetValue(res, prop.GetValue(_lowLevelInterfaceOutSignals));
            }
            _lock.Exit();
            return res;
        }

        private void InitOutSignals(ManifestInjectionResult outSignalsInjectionResults)
        {
            Type type = typeof(LowLevelInterfaceOutSignals);
            var nodes = outSignalsInjectionResults.ProvidedVariables.Select(iv => (iv.InjectedID.NameSpaceIndex, iv.InjectedID.ID,(object) type.GetProperty(iv.ManifestItemID))).ToArray();
            _dwisClient.Subscribe(null, SubscriptionDataChanged, nodes);        
        }

        private void SubscriptionDataChanged(object subscriptionData, UADataChange[] changes)
        {
            if ((changes != null && changes.Any()))
            {
                _lock.Enter();
                foreach (var change in changes)
                {
                    if (change != null && change.Value != null && change.UserData is PropertyInfo prop ) 
                    {
                        prop.SetValue(_lowLevelInterfaceOutSignals, change.Value);
                    }
                }
                _lock.Exit();
            }
        }

        private void InitInSignals(ManifestInjectionResult inSignalsInjectionResults)
        {
            Type type = typeof(LowLevelInterfaceInSignals); 
            foreach (var iv in inSignalsInjectionResults.ProvidedVariables) 
            {
                if (iv != null)
                {
                    PropertyInfo? propInfo = type.GetProperty(iv.ManifestItemID);
                    if (propInfo != null)
                    {
                        _inSignalsDictionary.Add(propInfo, (iv.InjectedID.ID, iv.InjectedID.NameSpaceIndex));
                    }
                }
            }
        }
    }
}
