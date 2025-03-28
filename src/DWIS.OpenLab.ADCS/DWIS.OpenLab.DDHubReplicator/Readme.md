# ADCS Bridge BTWN
This program replicates the signals available on the DDHub server located on the
HMI machine of BTWN

First a docker network must be created:
```bash
docker network create dwisnetwork
```
## local
Then the DDHubReplicator container must be started:
On windows:
```bash
docker run -d --name ddhubreplicator -v c:\Volumes\DWISOpenLabDDHubReplicator:/home --network dwisnetwork -d dwisopenlabddhubreplicator:latest
```
On linux
```bash
docker run -d --name ddhubreplicator -v /home/Volumes/DWISOpenLabDDHubReplicator:/home --network dwisnetwork -d dwisopenlabddhubreplicator:latest
```

## server
Then the DDHubReplicator container must be started:
On windows:
```bash
docker run -d --name ddhubreplicator -v c:\Volumes\DWISOpenLabDDHubReplicator:/home --network dwisnetwork -d digiwells/dwisopenlabddhubreplicator:stable
```
On linux
```bash
docker run -d --name ddhubreplicator -v /home/Volumes/DWISOpenLabDDHubReplicator:/home --network dwisnetwork -d digiwells/dwisopenlabddhubreplicator:stable
```


## Configuration
A configuration file is available in the directory/folder that is connected to the internal `/home` directory. The name of the configuration
file is `config.json` and is in Json format.

The configuration file has the following properties:
- `LoopDuration` (a TimeSpan, default 0.1s): this property defines the loop duration of the service, i.e., the time interval used to check if new signals are available.
- `Blackboard` (a string, default "opc.tcp://10.120.34.112:48031"): this property defines the `URL` used to connect to the `DWIS Blackboard`
- `DDHub` (a string, default "opc.tcp://10.120.34.103:4840"): this property defines the `URL` used to connect to the `DDHub`
- `MappingIn` a list of signals that shall be replicated from the DDHub to the Blackboard
- `MappingOut` a list of signals that shall be replicated from the Blackboard to DDHub

