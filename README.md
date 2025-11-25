# DWIS-demo-2024-openlab-adcs
ADCS-focused OpenLab demo stack: replicates DDHub signals into the DWIS blackboard and exposes an ADCS low-level interface for simulator/digital-twin scenarios (2024 demo).

## Repository layout
- `src/dwis.openlab.adcs.sln` — solution for all services.
- `src/DWIS.OpenLab.ADCS/dwis.openlab.adcs.app/` — entrypoint app (Dockerfile provided).
- `src/DWIS.OpenLab.ADCS/DWIS.OpenLab.ADCS.base/` — core ADCS base classes (controllers, machine limits, setpoints, low-level in/out signals, OpenLab ADCS orchestration).
- `src/DWIS.OpenLab.ADCS/DWIS.OpenLab.ADCS.LowLevelInterfaceClient/` — client for the ADCS low-level interface (includes sample injection results under `InjectionResults/`).
- `src/DWIS.OpenLab.ADCS/DWIS.OpenLab.ADCS.LowLevelSignalsReplication/` — service that mirrors low-level signals; Dockerfile included and `manifest/` for mappings.
- `src/DWIS.OpenLab.ADCS/DWIS.OpenLab.DDHubReplicator/` — replicates signals between DDHub and the DWIS blackboard (Dockerfile, config, mapping; see `Readme.md` for run commands and `home/config.json` sample).
- `src/DWIS.OpenLab.ADCS/home/` — sample runtime config and DDHub content snapshot.
- `.github/workflows/` — Docker build/push for the replicator image.

## Build
- `dotnet build src/DWIS.OpenLab.ADCS/dwis.openlab.adcs.sln`

## Run (DDHub replicator example)
1. Create network: `docker network create dwisnetwork`
2. Run replicator (Windows example): `docker run -d --name ddhubreplicator -v C:\Volumes\DWISOpenLabDDHubReplicator:/home --network dwisnetwork digiwells/dwisopenlabddhubreplicator:stable`
3. Configure `/home/config.json` with `Blackboard`, `DDHub`, `MappingIn`, and `MappingOut` entries (see `src/DWIS.OpenLab.ADCS/home/config.json`).

## Notes
- Default loop duration is 100 ms; adjust endpoints in configs for your DDHub/blackboard instances.
- Additional services provide low-level signal replication and ADCS base behaviors; use Dockerfiles for containerized deployment.