using DWIS.Client.ReferenceImplementation;
using DWIS.Client.ReferenceImplementation.OPCFoundation;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System.Threading;
using DWIS.API.DTO;
using System.Net;
using Opc.Ua.Server;
using Newtonsoft.Json.Linq;
using System.Linq.Expressions;

namespace DWIS.OpenLab.DDHubReplicator
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker>? _logger;
        private readonly ILogger<DWISClientOPCF>? _loggerDWISClient;
        private Configuration? Configuration { get; set; } = new Configuration();
        protected TimeSpan _loopSpan;
        protected IOPCUADWISClient? _DWISClient = null;

        protected ApplicationInstance? _application = null;
        protected ApplicationConfiguration _config;
        private bool _autoAccept = true;
        protected Opc.Ua.Client.Session? _session = null;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public Worker(ILogger<Worker>? logger, ILogger<DWISClientOPCF>? loggerDWISClient)
        {
            _logger = logger;
            _loggerDWISClient = loggerDWISClient;
            Initialize();
        }

        private void Initialize()
        {
            string homeDirectory = ".." + Path.DirectorySeparatorChar + "home";
            if (!Directory.Exists(homeDirectory))
            {
                try
                {
                    Directory.CreateDirectory(homeDirectory);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Impossible to create home directory for local storage");
                }
            }
            if (Directory.Exists(homeDirectory))
            {
                string configName = homeDirectory + Path.DirectorySeparatorChar + "config.json";
                if (File.Exists(configName))
                {
                    string jsonContent = File.ReadAllText(configName);
                    if (!string.IsNullOrEmpty(jsonContent))
                    {
                        try
                        {
                            Configuration? config = System.Text.Json.JsonSerializer.Deserialize<Configuration>(jsonContent);
                            if (config != null)
                            {
                                Configuration = config;
                            }
                        }
                        catch (Exception e)
                        {
                            if (_logger != null)
                            {
                                _logger.LogError(e.ToString());
                            }
                        }
                    }
                }
                else
                {
                    string defaultConfigJson = System.Text.Json.JsonSerializer.Serialize(Configuration);
                    using (StreamWriter writer = new StreamWriter(configName))
                    {
                        writer.WriteLine(defaultConfigJson);
                    }
                }
            }
            if (_logger != null && Configuration != null)
            {
                _logger.LogInformation("Configuration Loop Duration: " + Configuration.LoopDuration.ToString());
                _logger.LogInformation("Configuration Blackboard: " + Configuration.Blackboard);
                _logger.LogInformation("Configuration DDHub: " + Configuration.DDHub);
            }
            string hostName = System.Net.Dns.GetHostName();
            if (!string.IsNullOrEmpty(hostName))
            {
                var ip = System.Net.Dns.GetHostEntry(hostName);
                if (ip != null && ip.AddressList != null && ip.AddressList.Length > 0 && _logger != null)
                {
                    _logger.LogInformation("My IP Address: " + ip.AddressList[0].ToString());
                }
            }
        }

        protected virtual void ConnectToBlackboard()
        {
            try
            {
                if (Configuration != null)
                {
                    _loopSpan = Configuration.LoopDuration;
                }
                if (Configuration != null && !string.IsNullOrEmpty(Configuration.Blackboard))
                {
                    DefaultDWISClientConfiguration generalDWISClientConfiguration = new DefaultDWISClientConfiguration();
                    generalDWISClientConfiguration.UseWebAPI = false;
                    generalDWISClientConfiguration.ServerAddress = Configuration.Blackboard;
                    _DWISClient = new DWISClientOPCF(generalDWISClientConfiguration, _loggerDWISClient);
                }
            }
            catch (Exception e)
            {
                if (_loggerDWISClient != null)
                {
                    _loggerDWISClient.LogError(e.ToString());
                }
            }
        }

        protected async Task ConnectToDDHub()
        {
            if (Configuration != null && !string.IsNullOrEmpty(Configuration.DDHub))
            {
                try
                {
                    _application = new ApplicationInstance
                    {
                        ApplicationName = "DWIS client",
                        ApplicationType = ApplicationType.Client,
                        ConfigSectionName = "./config/Quickstarts.ReferenceClient"
                    };

                    // Load configuration and create the session
                    _config = await _application.LoadApplicationConfiguration(false);
                    _config.CertificateValidator.CertificateValidation += CertificateValidation;
                    //await _application.CheckApplicationInstanceCertificate(false, 2048);
                    EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(_config, Configuration.DDHub, false);
                    EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(_config);
                    ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                    _session = await Opc.Ua.Client.Session.Create(
                        _config,
                        endpoint,
                        false,
                        false,
                        _config.ApplicationName,
                        30 * 60 * 1000,
                        new UserIdentity(),
                        null);
                }
                catch (Exception e)
                {
                    if (_loggerDWISClient != null)
                    {
                        _loggerDWISClient.LogError(e.ToString());
                    }
                }
            }
        }

        protected void BrowseNodes(StreamWriter writer, NodeId nodeId, int depth)
        {
            if (_session != null)
            {
                if (depth > 10) return; // Limit recursion depth to prevent infinite loops

                // Browse the node
                ReferenceDescriptionCollection references;
                Byte[] continuationPoint;
                _session.Browse(
                    null,
                    null,
                    nodeId,
                    0u,
                    BrowseDirection.Forward,
                    ReferenceTypeIds.HierarchicalReferences,
                    true,
                    (uint)NodeClass.Object | (uint)NodeClass.Variable,
                    out continuationPoint,
                    out references);

                // Process references
                foreach (ReferenceDescription rd in references)
                {
                    // Indentation for hierarchy
                    writer.WriteLine($"{new string(' ', depth * 2)}- {rd.DisplayName} (NodeId: {rd.NodeId})");

                    // If it's a variable, read its value
                    if (rd.NodeClass == NodeClass.Variable)
                    {
                        if (rd.NodeId.IdType == IdType.String)
                        {
                            string name = "ns=" + rd.NodeId.NamespaceIndex + ";s=" + (string)rd.NodeId.Identifier;
                            DataValue value = _session.ReadValue(new NodeId(name));
                            writer.WriteLine($"{new string(' ', depth * 2 + 2)}  Value: {value.Value}");
                        }
                        else
                        {
                            writer.WriteLine($"{new string(' ', depth * 2 + 2)}  Variable but Identifier is not string");
                        }
                    }

                    // Recursively browse children
                    if (rd.NodeClass == NodeClass.Object)
                    {
                        BrowseNodes(writer, (NodeId)rd.NodeId, depth + 1);
                    }
                }
            }
        }
        protected void BrowseDDHub()
        {
            if (_session != null)
            {
                string homeDirectory = ".." + Path.DirectorySeparatorChar + "home";
                if (!Directory.Exists(homeDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(homeDirectory);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Impossible to create home directory for local storage");
                    }
                }
                if (Directory.Exists(homeDirectory))
                {
                    string browseName = homeDirectory + Path.DirectorySeparatorChar + "DDHubContent.txt";
                    try
                    {
                        using (StreamWriter writer = new StreamWriter(browseName))
                        {
                            writer.WriteLine("Namespaces:");
                            string[] namespaces = _session.NamespaceUris.ToArray();
                            for (int i = 0; i < namespaces.Length; i++)
                            {
                                writer.WriteLine($"[{i}] {namespaces[i]}");
                            }
                            writer.WriteLine("\nBrowsing OPC UA Server Nodes...\n");
                            BrowseNodes(writer, ObjectIds.ObjectsFolder, 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex.ToString());
                    }
                }
            }
        }

        private void CertificateValidation(Opc.Ua.CertificateValidator sender, Opc.Ua.CertificateValidationEventArgs e)
        {
            bool certificateAccepted = false;

            // ****
            // Implement a custom logic to decide if the certificate should be
            // accepted or not and set certificateAccepted flag accordingly.
            // The certificate can be retrieved from the e.Certificate field
            // ***

            ServiceResult error = e.Error;
            _logger?.LogError(error.ToString());
            if (error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted && _autoAccept)
            {
                certificateAccepted = true;
            }

            if (certificateAccepted)
            {
                _logger?.LogInformation("Untrusted Certificate accepted. Subject = {0}", e.Certificate.Subject);
                e.Accept = true;
            }
            else
            {
                _logger?.LogWarning("Untrusted Certificate rejected. Subject = {0}", e.Certificate.Subject);
            }
        }
        protected List<NodeId?> _nodeIDsInDDHub = new List<NodeId?>();
        protected List<NodeId?> _nodeIDsOutDDHub = new List<NodeId?>();
        protected List<NodeId?> _nodeIDsInBlackboard = new List<NodeId?>();
        protected List<NodeId?> _nodeIDsOutBlackboard = new List<NodeId?>();
        protected List<bool> _displayIn = new List<bool>();
        protected List<bool> _displayOut = new List<bool>();
        protected List<QueryResult?> _placeHoldersInBlackboard = new List<QueryResult?>();
        protected List<QueryResult?> _placeHoldersOutBlackboard = new List<QueryResult?>();
        protected Dictionary<string, int> _nameSpacesDDHub = new Dictionary<string, int>();
        protected Dictionary<int, string> _nameSpacesBlackboard = new Dictionary<int, string>();
        protected Dictionary<string, int> _nameSpacesBlackboardInvert = new Dictionary<string, int>();

        protected void PrepareExchange()
        {
            if (_session != null)
            {
                try
                {
                    NamespaceTable nameSpaceTable = _session.NamespaceUris;
                    if (Configuration != null && Configuration.MappingIn != null && _DWISClient != null && _DWISClient.Connected && _session != null)
                    {
                        foreach (var mapping in Configuration.MappingIn)
                        {
                            if (mapping != null)
                            {
                                _displayIn.Add(mapping.Display);
                                int nsi = -1;
                                if (!string.IsNullOrEmpty(mapping.nsDDHub))
                                {
                                    if (nameSpaceTable != null)
                                    {
                                        int index = nameSpaceTable.GetIndex(mapping.nsDDHub);
                                        if (index >= 0)
                                        {
                                            nsi = index;
                                            if (!_nameSpacesDDHub.ContainsKey(mapping.nsDDHub))
                                            {
                                                _nameSpacesDDHub.Add(mapping.nsDDHub, nsi);
                                            }
                                        }
                                    }
                                }
                                if (nsi >= 0 && !string.IsNullOrEmpty(mapping.idDDHub))
                                {
                                    string varname = "ns=" + nsi + ";s=" + mapping.idDDHub;
                                    NodeId nodeID = new NodeId(varname);
                                    _nodeIDsInDDHub.Add(nodeID);
                                }
                                else
                                {
                                    _nodeIDsInDDHub.Add(null);
                                }
                                nsi = -1;
                                if (!string.IsNullOrEmpty(mapping.nsBlackboard))
                                {
                                    if (_DWISClient.GetNameSpaceIndex(mapping.nsBlackboard, out ushort index))
                                    {
                                        nsi = index;
                                        if (!_nameSpacesBlackboard.ContainsKey(nsi))
                                        {
                                            _nameSpacesBlackboard.Add(nsi, mapping.nsBlackboard);
                                            _nameSpacesBlackboardInvert.Add(mapping.nsBlackboard, nsi);
                                        }
                                    }
                                }
                                if (nsi >= 0 && !string.IsNullOrEmpty(mapping.idBlackboard))
                                {
                                    string varname = "ns=" + nsi + ";s=" + mapping.idBlackboard;
                                    NodeId nodeID = new NodeId(varname);
                                    _nodeIDsInBlackboard.Add(nodeID);
                                }
                                else
                                {
                                    _nodeIDsInBlackboard.Add(null);
                                }
                                if (!string.IsNullOrEmpty(mapping.SparQL))
                                {
                                    QueryResult result = _DWISClient.GetQueryResult(mapping.SparQL);
                                    if (result != null && result.Count > 0 && result[0].Count > 0)
                                    {
                                        _placeHoldersInBlackboard.Add(result);
                                    }
                                    else
                                    {
                                        _placeHoldersInBlackboard.Add(null);
                                    }
                                }
                                else
                                {
                                    _placeHoldersInBlackboard.Add(null);
                                }
                            }
                        }
                    }
                    if (Configuration != null && Configuration.MappingOut != null && _DWISClient != null && _DWISClient.Connected)
                    {
                        foreach (var mapping in Configuration.MappingOut)
                        {
                            if (mapping != null)
                            {
                                _displayOut.Add(mapping.Display);
                                int nsi = -1;
                                if (!string.IsNullOrEmpty(mapping.nsDDHub))
                                {
                                    if (nameSpaceTable != null)
                                    {
                                        int index = nameSpaceTable.GetIndex(mapping.nsDDHub);
                                        if (index >= 0)
                                        {
                                            nsi = index;
                                            if (!_nameSpacesDDHub.ContainsKey(mapping.nsDDHub))
                                            {
                                                _nameSpacesDDHub.Add(mapping.nsDDHub, nsi);
                                            }
                                        }
                                    }
                                }
                                if (nsi >= 0 && !string.IsNullOrEmpty(mapping.idDDHub))
                                {
                                    string varname = "ns=" + nsi + ";s=" + mapping.idDDHub;
                                    NodeId nodeID = new NodeId(varname);
                                    _nodeIDsOutDDHub.Add(nodeID);
                                }
                                else
                                {
                                    _nodeIDsOutDDHub.Add(null);
                                }
                                nsi = -1;
                                if (!string.IsNullOrEmpty(mapping.nsBlackboard))
                                {
                                    if (_DWISClient.GetNameSpaceIndex(mapping.nsBlackboard, out ushort index))
                                    {
                                        nsi = index;
                                        if (!_nameSpacesBlackboard.ContainsKey(nsi))
                                        {
                                            _nameSpacesBlackboard.Add(nsi, mapping.nsBlackboard);
                                            _nameSpacesBlackboardInvert.Add(mapping.nsBlackboard, nsi);
                                        }
                                    }
                                }
                                if (nsi >= 0 && !string.IsNullOrEmpty(mapping.idBlackboard))
                                {
                                    string varname = "ns=" + nsi + ";s=" + mapping.idBlackboard;
                                    NodeId nodeID = new NodeId(varname);
                                    _nodeIDsOutBlackboard.Add(nodeID);
                                }
                                else
                                {
                                    _nodeIDsOutBlackboard.Add(null);
                                }
                                if (!string.IsNullOrEmpty(mapping.SparQL))
                                {
                                    QueryResult result = _DWISClient.GetQueryResult(mapping.SparQL);
                                    if (result != null && result.Count > 0 && result[0].Count > 0)
                                    {
                                        if (result[0][0] != null && !string.IsNullOrEmpty(result[0][0].NameSpace))
                                        {
                                            if (_DWISClient.GetNameSpaceIndex(result[0][0].NameSpace, out ushort index))
                                            {
                                                if (!_nameSpacesBlackboard.ContainsKey(index))
                                                {
                                                    _nameSpacesBlackboard.Add(index, result[0][0].NameSpace);
                                                    _nameSpacesBlackboardInvert.Add(result[0][0].NameSpace, index);
                                                }
                                            }
                                        }
                                        _placeHoldersOutBlackboard.Add(result);
                                    }
                                    else
                                    {
                                        _placeHoldersOutBlackboard.Add(null);
                                    }
                                }
                                else
                                {
                                    _placeHoldersOutBlackboard.Add(null);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger?.LogError(e.ToString());
                }
            }
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string sparql = string.Empty;
            sparql += @"PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>" + Environment.NewLine;
            sparql += @"PREFIX ddhub: <http://ddhub.no/>" + Environment.NewLine;
            sparql += @"SELECT ?signal " + Environment.NewLine;
            sparql += @"WHERE {" + Environment.NewLine;
            sparql += @"			?dataPoint ddhub:HasDynamicValue ?signal ." + Environment.NewLine;
            sparql += @"			?dataPoint rdf:type ddhub:FlowRateIn ." + Environment.NewLine;
            sparql += @"}" + Environment.NewLine;

            if (Configuration != null)
            {
                ConnectToBlackboard();
                await ConnectToDDHub();
                if (Configuration.Browse)
                {
                    BrowseDDHub();
                }
                PrepareExchange();
                if (_session != null &&
                    _nodeIDsInDDHub != null && 
                    _nodeIDsOutDDHub != null && 
                    _placeHoldersInBlackboard != null &&
                    _placeHoldersOutBlackboard != null && 
                    _nodeIDsInDDHub.Count == _placeHoldersInBlackboard.Count && 
                    _nodeIDsOutDDHub.Count == _placeHoldersOutBlackboard.Count &&
                    _displayIn.Count == _nodeIDsInBlackboard.Count &&
                    _displayOut.Count == _nodeIDsOutBlackboard.Count
                    )
                {
                    PeriodicTimer timer = new PeriodicTimer(_loopSpan);
                    while (await timer.WaitForNextTickAsync(stoppingToken))
                    {
                        if (Configuration.MappingIn != null)
                        {
                            for (int i = 0; i < _nodeIDsInDDHub!.Count; i++)
                            {
                                if (_nodeIDsInDDHub[i] != null && (_nodeIDsInBlackboard != null || _placeHoldersInBlackboard != null))
                                {
                                    DataValue val;
                                    try
                                    {
                                        val = _session.ReadValue(_nodeIDsInDDHub[i]);
                                        if (_nodeIDsInBlackboard![i] != null)
                                        {
                                            if (_nodeIDsInBlackboard![i]!.NamespaceIndex >= 0 && _nodeIDsInBlackboard![i]!.IdType == IdType.String && !string.IsNullOrEmpty((string)_nodeIDsInBlackboard![i]!.Identifier))
                                            {
                                                if (_nameSpacesBlackboard.ContainsKey(_nodeIDsInBlackboard![i]!.NamespaceIndex))
                                                {
                                                    // OPC-UA code to set the value at the node id = ID
                                                    (string nameSpace, string id, object value, DateTime sourceTimestamp)[] outputs = new (string nameSpace, string id, object value, DateTime sourceTimestamp)[1];
                                                    outputs[0].nameSpace = _nameSpacesBlackboard[_nodeIDsInBlackboard![i]!.NamespaceIndex];
                                                    outputs[0].id = (string)_nodeIDsInBlackboard![i]!.Identifier;
                                                    outputs[0].value = val.Value;
                                                    outputs[0].sourceTimestamp = DateTime.UtcNow;
                                                    _DWISClient!.UpdateAnyVariables(outputs);
                                                }
                                            }
                                        }
                                        if (_placeHoldersInBlackboard![i] != null && _placeHoldersInBlackboard[i]!.Count > 0 && _placeHoldersInBlackboard[i]![0].Count > 0)
                                        {
                                            NodeIdentifier? node = _placeHoldersInBlackboard[i]![0][0];
                                            if (node != null && !string.IsNullOrEmpty(node.NameSpace) && !string.IsNullOrEmpty(node.ID))
                                            {
                                                // OPC-UA code to set the value at the node id = ID
                                                (string nameSpace, string id, object value, DateTime sourceTimestamp)[] outputs = new (string nameSpace, string id, object value, DateTime sourceTimestamp)[1];
                                                outputs[0].nameSpace = node.NameSpace;
                                                outputs[0].id = node.ID;
                                                outputs[0].value = val.Value;
                                                outputs[0].sourceTimestamp = DateTime.UtcNow;
                                                _DWISClient!.UpdateAnyVariables(outputs);
                                            }
                                        }
                                        if (_displayIn[i])
                                        {
                                            _logger?.LogInformation("In value of " + _nodeIDsInDDHub[i]!.Identifier + ": " + val.Value);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger?.LogError(ex.ToString());
                                    }
                                }
                            }
                        }
                        if (Configuration.MappingOut != null)
                        {
                            for(int i = 0; i < _nodeIDsOutDDHub.Count; i++)
                            {
                                if (_nodeIDsOutDDHub[i] != null && (_nodeIDsOutBlackboard != null || _placeHoldersOutBlackboard != null))
                                {
                                    try
                                    {
                                        if (_nodeIDsOutBlackboard![i] != null)
                                        {
                                            if (_nodeIDsOutBlackboard![i]!.NamespaceIndex >= 0 && _nodeIDsOutBlackboard![i]!.IdType == IdType.String && !string.IsNullOrEmpty((string)_nodeIDsOutBlackboard![i]!.Identifier))
                                            {
                                                DataValue? val = ((DWISClientOPCF)_DWISClient!).ReadValue(_nodeIDsOutBlackboard![i]!);
                                                if (val != null && _nodeIDsOutDDHub[i] != null)
                                                {
                                                    WriteValue writeValue = new WriteValue
                                                    {
                                                        NodeId = _nodeIDsOutDDHub[i],
                                                        AttributeId = Attributes.Value,
                                                        Value = val
                                                    };

                                                    WriteValueCollection writeCollection = new WriteValueCollection { writeValue };
                                                    StatusCodeCollection statusCodes;
                                                    DiagnosticInfoCollection diagnosticInfos;

                                                    _session.Write(null, writeCollection, out statusCodes, out diagnosticInfos);
                                                    if (_displayOut[i])
                                                    {
                                                        _logger?.LogInformation("Out value of " + _nodeIDsOutDDHub[i]!.Identifier + ": " + val.Value);
                                                    }
                                                }
                                            }
                                        }
                                        else if (_placeHoldersOutBlackboard![i] != null && _placeHoldersOutBlackboard[i]!.Count > 0 && _placeHoldersOutBlackboard[i]![0].Count > 0)
                                        {
                                            NodeIdentifier? node = _placeHoldersOutBlackboard[i]![0][0];
                                            if (node != null && !string.IsNullOrEmpty(node.NameSpace) && _nameSpacesBlackboardInvert.ContainsKey(node.NameSpace))
                                            {
                                                string varname = "ns=" + _nameSpacesBlackboardInvert[node.NameSpace] + ";s=" + node.ID;
                                                NodeId n = new NodeId(varname);
                                                DataValue? val = ((DWISClientOPCF)_DWISClient!).ReadValue(n);
                                                if (val != null && _nodeIDsOutDDHub[i] != null)
                                                {
                                                    WriteValue writeValue = new WriteValue
                                                    {
                                                        NodeId = _nodeIDsOutDDHub[i],
                                                        AttributeId = Attributes.Value,
                                                        Value = val
                                                    };

                                                    WriteValueCollection writeCollection = new WriteValueCollection { writeValue };
                                                    StatusCodeCollection statusCodes;
                                                    DiagnosticInfoCollection diagnosticInfos;

                                                    _session.Write(null, writeCollection, out statusCodes, out diagnosticInfos);
                                                    if (_displayOut[i])
                                                    {
                                                        _logger?.LogInformation("Out value of " + _nodeIDsOutDDHub[i]!.Identifier + ": " + val.Value);
                                                    }
                                                }
                                            }
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        _logger?.LogError(ex.ToString());
                                    }
                                }
                            }
                        }
                    }
                    if (_session is not null)
                    {
                        _session.Close();
                    }
                }
            }
        }
    }
}
