# CLAUDE.md

Coordinator for a pallet bagging/labeling line (Wenco/Maroti plant). `ModbusServer/` is a Windows service that talks to a Fatek master PLC (Modbus TCP, PC is the server), two Bocedi bagging machines (Omron FINS PLCs + network label printers), ifm QR readers, and a SQL Server DB; `TcpHMIClient/` is the operator WinForms HMI. `PLCs/` holds Fatek WinProladder projects (binary, not editable here).

Read `README.md` for the plant/network overview and `docs/ARCHITECTURE.md` for state machines, PLC signal/memory maps, protocols and quirks. Keep both updated when behavior changes.

## Build / run

- .NET **Framework** (server 4.8, HMI 4.7.2), old-style csproj + `packages.config`. The deployable exe is produced only by Visual Studio/MSBuild on Windows (`nuget restore ModbusServer.sln` first), on the plant/dev PC or by the `windows` job in `.github/workflows/build.yml`.
- **`./build/typecheck.sh` type-checks both projects on macOS/Linux** with only the .NET SDK installed — same sources, compiled against the .NET Framework reference assemblies (`build/typecheck/*.csproj`). Use it before claiming a change compiles. It does *not* cover resources, manifests or packages.config resolution, so a green typecheck is not a green build.
- There are no automated tests. Real verification requires the plant hardware or a Modbus client simulating the PLC.
- After changing any state machine, regenerate `WebHMI/wwwroot/transitions.json` (`node WebHMI/devserver/generate_transitions.mjs`); CI fails if it is stale.
- Service (Topshelf): `ModbusServer.exe install|start|stop`, or run the exe directly for console mode. Logs: `Logs/info-*.log`, `Logs/error-*.log` next to the exe.

## Hard rules

1. **Single-threaded step model.** All logic runs in `MainProcess` → `MainMachine.Step()` every 100 ms. Never block inside a `Step()`; start a `Task` and poll `IsCompleted`/`IsFaulted` on later steps (see any machine for the retry idiom with `StateTime`).
2. Every `Machine` subclass must contain a nested enum literally named `States` — it's read by reflection at construction; renaming it crashes startup.
3. **Server and HMI DTOs are duplicated by hand.** Any change to `Status.cs` DTOs, `FatekPLC.Signals` order (coils 21+), or state enums shown on the HMI must be mirrored in `TcpHMIClient/HMIClient.cs` (`SystemStatus`, `SignalsNames`, `PalletEntryStates`). The HMI protocol is length-prefixed (4-byte LE int) on both sides — a change to the framing means deploying server and HMIs together.
4. **Coil/register maps are contracts with the PLC programs** in `PLCs/*.pdw`. Don't renumber `FatekPLC.Signals`/`Memory` values unless the PLC program changes too. Convention: coils 1–20 written by the PC, 21+ by the PLC.
5. Deliberate-looking oddities usually are deliberate — check "Known quirks" in `docs/ARCHITECTURE.md` before fixing: the `Wolrdjet` typo (registry key + packager id), `PackagerPreference.Labeling` meaning *omit* labeling, queue ids cycling 1–7 with 8 as the label flag, registry name `ContinueIfNotQr`.
6. Operator-facing strings (`Status.ErrorMessages`, HMI labels) are **Spanish**; logs are English.

## Key files

- `ModbusServer/StateMachine/FatekPLCCommunication.cs` — PLC handshake; gates all line machines
- `ModbusServer/StateMachine/PalletEntry.cs` — entry: QR → DB routing → Bocedi1/car
- `ModbusServer/StateMachine/PalletLabel{1,2}.cs` + `PrinterMachine.cs` — weighing/labeling per Bocedi
- `ModbusServer/Devices/FatekPLC.cs` — Modbus maps + QR/ID register encoding
- `ModbusServer/Devices/SqlDatabase.cs` — all stored-proc calls (`[maroti]` schema)
- `ModbusServer/StateMachine/AcceptHMIs.cs` / `HMIConnection.cs` — HMI TCP protocol (:8153)
- `ModbusServer/secrets.config` (gitignored, see `secrets.config.example`) — real SQL connection string, deployed next to the exe
