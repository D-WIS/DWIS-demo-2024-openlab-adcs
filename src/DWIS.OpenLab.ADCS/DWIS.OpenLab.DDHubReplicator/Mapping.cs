using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.OpenLab.DDHubReplicator
{
    public class Mapping
    {
        public string? nsDDHub { get; set; } = null;
        public string? idDDHub { get; set; } = null;
        public string? nsBlackboard { get; set; } = null;
        public string? idBlackboard { get; set; } = null;
        public string? SparQL { get; set; } = null;
        public bool Display { get; set; } = false;
    }
}
