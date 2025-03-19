using dwis.openlab.adcs.Base;
using DWIS.OpenLab.ADCS.LowLevelInterfaceClient;
using DWIS.Client.ReferenceImplementation.OPCFoundation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using Opc.Ua;

string url = string.Empty;
string inSignalsNamespace = string.Empty;
string outSignalsNamespace = string.Empty;
if (args != null && args.Any())
{
    url = args[0];

    if (args.Length >= 3)
    {
        inSignalsNamespace = args[1];
        outSignalsNamespace = args[2];
    }
}
var hostBuilder = Host.CreateDefaultBuilder();
hostBuilder.ConfigureServices(services =>
services.AddLogging(loggingBuilder => loggingBuilder
        .ClearProviders()
        .SetMinimumLevel(LogLevel.Warning)
        .AddFilter(ll => ll >= LogLevel.Warning)
        .AddConsole()
        )
);

var host = hostBuilder.Build();
var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

LowLevelInterfaceInSignals lowLevelInterfaceInSignals = new LowLevelInterfaceInSignals();

var conf = new DWIS.Client.ReferenceImplementation.DefaultDWISClientConfiguration();
if (!string.IsNullOrEmpty(url))
{
    conf.ServerAddress = url;
}
else
{
    conf.ServerAddress = "opc.tcp://10.120.34.103:4840";
}

DWISClientOPCF dwisClient = new DWISClientOPCF(conf, loggerFactory.CreateLogger<DWISClientOPCF>());

bool useFileNamespaceIndexes = string.IsNullOrEmpty(inSignalsNamespace) || string.IsNullOrEmpty(outSignalsNamespace);

LowLevelInterfaceClient lowLevelInterfaceClient = new LowLevelInterfaceClient(dwisClient, logger: loggerFactory.CreateLogger<LowLevelInterfaceClient>(), useFileNamespaceIndex:useFileNamespaceIndexes, inSignalsNamespace: inSignalsNamespace, outSignalsNamespace:outSignalsNamespace);

var props = typeof(LowLevelInterfaceInSignals).GetProperties();

var rootCommand = new RootCommand("main entry point for test application");
var setCommand = new Command("set");
rootCommand.AddCommand(setCommand);
foreach (var prop in props)
{
    if (prop.PropertyType == typeof(double))
    {
        var dArgument = new Argument<double>("value");
        var command = new Command(prop.Name);
        setCommand.AddCommand(command);

        command.AddArgument(dArgument);
        command.SetHandler(d => prop.SetValue(lowLevelInterfaceInSignals, d), dArgument);

    }
    else if (prop.PropertyType == typeof(short))
    {
        var sArgument = new Argument<short>(prop.Name);
        var command = new Command(prop.Name);
        setCommand.AddCommand(command);

        command.AddArgument(sArgument);
        command.SetHandler(d => prop.SetValue(lowLevelInterfaceInSignals, d), sArgument);
    }
    else if (prop.PropertyType == typeof(bool))
    {
        var bArgument = new Argument<bool>(prop.Name);
        var command = new Command(prop.Name);
        setCommand.AddCommand(command);

        command.AddArgument(bArgument);
        command.SetHandler( d => prop.SetValue(lowLevelInterfaceInSignals, d), bArgument);
    }
}

var printCommand = new Command("print");
rootCommand.AddCommand(printCommand);
printCommand.SetHandler(Print);

var pushCommand = new Command("push");
rootCommand.AddCommand(pushCommand);
pushCommand.SetHandler(Push);

Console.Write("> ");

while (true)
{
    string? rd = Console.ReadLine();
    if (!string.IsNullOrEmpty(rd))
    {
        await rootCommand.InvokeAsync(rd.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
    Console.Write("> ");
}


void Print()
{
    foreach (var prop in props) 
    {
        Console.WriteLine($"{prop.Name}: {prop.GetValue(lowLevelInterfaceInSignals)}");
    }
}

void Push() 
{
    lowLevelInterfaceClient.WriteInSignals(lowLevelInterfaceInSignals, DateTime.Now);
}