# Rovisen — Maroti Embolsadora (Wenco)

Industrial automation system that coordinates a pallet **bagging (embolsado) and labeling line** at the Wenco/Maroti plant. An industrial PC runs a Windows service that orchestrates PLCs, QR readers, label printers and the plant database, and serves live status to operator HMIs.

## What the line does

1. A pallet (coming from injection machines) arrives at the entry conveyor. An ifm QR reader reads the pallet's QR code.
2. The central SQL Server database (`[maroti]` schema, "Wenco DB") is asked which bagging machine (embolsadora/Bocedi 1 or 2) should receive the pallet, with which recipe, from which injector, and whether it must be labeled.
3. The pallet is routed by the Fatek master PLC either directly to **Bocedi 1**, or onto a **transfer car (carro)** that carries it to **Bocedi 2**.
4. Each Bocedi bags the pallet. Inside, per-machine FIFO queues (mirrored between the PC and PLC holding registers) track the pallets.
5. At the exit of each Bocedi, the pallet is weighed (read from the Bocedi's Omron PLC), the weight is sent to the DB, which returns two label commands (A/B). The labels are printed on a network printer and applied by the applicator; label-lost errors are retried up to 3 times.
6. The pallet leaves; the DB is notified. Errors and machine states are also reported to the DB as alarms/events.
7. A separate **elevator (ascensor)** entry point reads a pallet QR and asks the DB for authorization before letting the pallet up.

Operators watch and control the line from WinForms HMIs that connect to the service over TCP and can delete pallets from the queues.

## Repository layout

| Path | What it is |
|---|---|
| `ModbusServer/` | The main Windows service (.NET Framework 4.8, Topshelf). Despite the name, it is the whole line coordinator; the name comes from it hosting a Modbus TCP server that the Fatek master PLC polls. |
| `TcpHMIClient/` | WinForms HMI (.NET Framework 4.7.2) for operators. Connects to the service on TCP port 8153 and renders queues, car position, connection health and error messages. |
| `PLCs/` | Fatek WinProladder projects (`.pdw`): `MasterPLC`, `SlavePLC`, `CarroPLC` (transfer car), `Ascensor`/`Ascensor_New` (elevator), `master-rpd`. Opened/edited only with WinProladder on Windows. |
| `Dependencies/` | Prebuilt DLLs: `EasyModbus.dll` (Modbus TCP server) and `mc.Omron.v1.00.dll` (Omron FINS/TCP client). |
| `ModbusServer.sln` | Visual Studio solution with both C# projects. |

See `docs/ARCHITECTURE.md` for the full design: state machines, PLC signal/memory maps, protocols and known pitfalls. `CLAUDE.md` has the maintenance quick reference.

## Network / hardware map

| Device | Address | Protocol |
|---|---|---|
| Industrial PC (this service) | 192.168.6.237 | — |
| Fatek master PLC | polls the PC | Modbus TCP (PC is the *server*; PLC is the client). Slave PLC, car and elevator hang off the master. |
| Entry QR reader (ifm O2I) | 192.168.6.236:50010 | ifm protocol V3 (`App.config: ipQrReader`) |
| Elevator QR reader (ifm O2I) | 192.168.6.241:50010 | ifm protocol V3 (`App.config: ipQrElevator`) |
| Bocedi 1 Omron PLC | 192.168.6.124:9600 | FINS/TCP (`App.config: ipOmron1`) |
| Bocedi 2 Omron PLC | 192.168.250.1:9600 | FINS/TCP (`App.config: ipOmron2`) |
| Printer Bocedi 1 ("Worldjet 1") | 192.168.6.122:9100 | raw label commands (`App.config: ipPrinter1`) |
| Printer Bocedi 2 | 192.168.6.163:9100 | raw label commands (`App.config: ipPrinter2`) |
| Wenco DB (SQL Server) | 192.168.20.69, catalog `SISTEMAS` | `sqlConnectiongString` in `secrets.config` (gitignored; see `ModbusServer/secrets.config.example`) |
| HMIs | any, connect to PC :8153 | line-less JSON over TCP |

## Building & deploying

Both projects are classic .NET Framework — they build with **Visual Studio / MSBuild on Windows** (not `dotnet build`, and not on macOS/Linux). NuGet packages are `packages.config`-style; restore happens via Visual Studio or `nuget restore ModbusServer.sln`.

The service is managed with Topshelf. On the plant PC, from an elevated prompt in the output directory:

```
ModbusServer.exe install   # installs Windows service "IndustrialPCServer" (display: "Server Wenco Embolsado")
ModbusServer.exe start
ModbusServer.exe stop
ModbusServer.exe           # run in console mode for debugging
```

When deploying, place a `secrets.config` with the real SQL connection string next to `ModbusServer.exe.config` (copy `ModbusServer/secrets.config.example` and fill it in — the file is gitignored and, when present in the project directory, copied to the build output automatically).

Logs are written next to the exe under `Logs/` (`info-YYYY-MM-DD.log`, `error-YYYY-MM-DD.log`). Runtime tuning lives in the Windows registry under `HKCU\SOFTWARE\WencoSettings` (QR retries, default recipe, continue-without-QR/DB flags) — note the service runs as LocalSystem, so that is LocalSystem's HKCU hive, not the logged-in user's.
