using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.OpenLab.DDHubReplicator
{
    public class Configuration
    {
        public TimeSpan LoopDuration { get; set; } = TimeSpan.FromSeconds(0.1);
        public string? Blackboard { get; set; } = "opc.tcp://10.120.34.112:48031";

        public string? DDHub { get; set; } = "opc.tcp://10.120.34.103:4840";

        public List<Mapping> MappingIn { get; set; } = new List<Mapping>() { new Mapping() { nsDDHub = "http://ddhub.no/openLAB/Variables/", idDDHub = "LowLevelInterfaceOutSignals.ActualCirculationSpeedMeasured" } };
        public List<Mapping> MappingOut { get; set; } = new List<Mapping>();

        public bool Browse { get; set; } = false;
    }
}
