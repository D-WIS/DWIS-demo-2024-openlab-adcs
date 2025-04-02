using Opc.Ua;
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
        /// <summary>
        /// multiplicative factor
        /// </summary>
        public double ConversionFactor { get; set; } = 1.0;
        /// <summary>
        /// the pre offset is applied before the multiplicative factor
        /// </summary>
        public double ConversionPreOffset { get; set; } = 0.0; 
        /// <summary>
        /// the post offset is applied after the multiplicative factor
        /// </summary>
        public double ConversionPostOffset { get; set; } = 0.0;
    }
}
