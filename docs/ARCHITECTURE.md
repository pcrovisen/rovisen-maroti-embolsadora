# Architecture

This document describes how `ModbusServer` (the coordinator service) and `TcpHMIClient` (the operator HMI) work internally. Read `README.md` first for the plant-level picture.

## Execution model: cooperative state machines

Everything in the service runs on **one worker thread** (`MainProcess.DoWork`) that calls `MainMachine.Step()` every 100 ms. There is no blocking inside `Step()`:

- Every component derives from `Machine<TState>` (`ModbusServer/StateMachine/Machine.cs`) as `class Foo : Machine<Foo.States>`, with a nested `public enum States`, a `Step()` that switches on `State`, and `NextState()` to transition. `StateTime` (a stopwatch restarted on each transition) implements timeouts and retry back-off.
- All I/O (SQL, Omron FINS, printers, QR readers, HMI sockets) is started as a `Task` and **polled** on subsequent steps, through `Machine.TryComplete(ref task, restart, what)`: it returns true once the task succeeded, re-launches it after a `RetryDelayMs` back-off if it faulted, and returns false while it is still running.
- `Step()` is a sealed wrapper on the base that runs a **liveness check** and then calls the machine's `OnStep()`. Machines declare `StateTimeouts` (state → ms) for the states that must make progress and override `OnStuck(state)` to add an operator message or an alarm; a stall is logged once per visit. Idle states, waits on an operator and waits on conveyor/car movement are deliberately left out — a false alarm is worse than none. Reporting only: nothing is forced, because guessing a transition with a pallet halfway through an applicator is not safe.
- Machine names and current states are published into `Status.Instance.StateMachine` so HMIs can display them: `Machines` is name → current state value, `MachinesStates` is name → value → state name, filled from `typeof(TState)` in the constructor.

Machine tree (instantiated in constructors, stepped top-down):

```
MainMachine
├── FatekPLCCommunication          — handshake/watchdog with the Fatek master PLC; gates everything below
│   ├── PalletEntry                — entry station: QR read → DB routing → send to Bocedi1 or car
│   │   ├── QrReaderConnection     — keeps entry ifm reader connected
│   │   └── QrReadMachine          — one read attempt with Config.QrRetries retries
│   ├── DeletePalletEmb x2         — HMI-requested removal of a pallet from FIFO queues (PC+PLC two-phase validation)
│   ├── PalletLabel x2             — exit of each Bocedi: weight → labels from DB → print/apply → notify out
│   │   ├── OmronConnection        — FINS/TCP connection to the Bocedi's Omron PLC
│   │   ├── NetworkPrinterConnection
│   │   └── PrinterMachine         — weight reading + the print/apply/label-lost retry protocol
│   ├── CarMachine                 — tracks transfer car position/pallet, notifies pallet-in to Bocedi2
│   └── ElevatorAccess             — elevator QR read + DB authorization
└── AcceptHMIs                     — TCP listener :8153, one HMIConnection per client (stepped last)
```

A legacy direct TCP link to the car (`CarConnection`/`CarCommunication`, port 51401) and the old `PrinterConnection` were removed; the car is driven through the master PLC since the "coordination at PLC level" change.

## Shared state: `Status`

`Status.Instance` (`ModbusServer/Status.cs`) is the single in-memory model: entry pallet, `Packager1/2` (FIFO queue + label-position pallet + exit pallet), `Car`, `Connections`, error messages (operator-facing, in Spanish), and the machine-state registry. `Updated` flags are reset at the top of every `MainMachine.Step()` and set by whoever mutates a section; `HMIConnection` uses them to send queue data only when it changed. FIFO re-reads from PLC memory are serialized with two `SemaphoreSlim`s (`UpdateFIFO1/2`).

## Fatek master PLC link (Modbus TCP)

The PC hosts a **Modbus TCP server** (EasyModbus); the Fatek master PLC is the client that polls/writes it. Wrapper: `Devices/FatekPLC.cs`.

- **Coils** = boolean signals (`FatekPLC.Signals`). Convention: **1–20 are written by the PC**, **21+ are written by the PLC**. `HMIConnection` sends both ranges to HMIs: coils 21+ as `Signals` (`ReadQR..WaitLabel2`) and coils 1–20 as `PcSignals` (`ReadingPallet..ElevatorFailedQr`); both lengths are computed from the enum, so appending new signals needs no count change.
- **Holding registers** = data (`FatekPLC.Memory`): entry QR/ID, the four FIFOs (FIFO1/2 = Bocedi1 queue QRs/IDs, FIFO3/4 = Bocedi2), label/exit slots, car pallet, and the deletion scratch area (`DEL*`).
- **Connectivity watchdog**: `IsConnected` = at least one Modbus client and some register/coil write within the last 5 s (any PLC write restarts a stopwatch).
- **Startup handshake** (`FatekPLCCommunication`): PLC raises `PLCStarting` → PC raises `Alive` → PLC raises `SendingFIFOs` → PC raises `ReceivingFIFOs`, PLC dumps FIFOs and raises `Ready` → PC re-reads queues into `Status` → `Working`. Losing `Ready` or the watchdog sends it back to `Init`.

### QR and ID encoding in PLC registers

Registers are 16-bit, so:

- **QR strings ↔ integers**: the DB assigns each QR string an int id (`sp_get_id` / `sp_get_codigo`, wrapped by `VisualID.GetQrId/GetQrString`). The int is stored little-endian across two registers (`FatekPLC.ReadQrId`/`WriteQrId`, used by `SetQr`/`GetQr`). An id of 0 means "empty slot".
- **Pallet ID word** (one register): bits 15–8 injector visual id, bits 7–4 recipe, bits 3–0 label flag + queue id. `labelAndId > 8` means "will be labeled" and the queue id is `labelAndId - 8`; otherwise the queue id is `labelAndId` as-is. Queue ids cycle **1..7** (`(id+1) % 8`, 0 is skipped), so 0 and 8 never appear as ids. Packed by `FatekPLC.PackId`, unpacked in `GetPalletInfo`.
- **Injector visual IDs**: `Data/VisualID.cs` maps injector names (strings from the DB) to small ushorts, persisted in `visualIdData.json` next to the exe. Lookups go through an inverse map built on load, so the id is the real key and the file's key order no longer matters.

⚠ Naming trap: `SqlDatabase.PackagerPreference.Labeling` is filled from `@out_omitir_proceso_etiquetado` (**omit** labeling), while `Pallet.Labeling` means **do** label. That's why `PalletEntry.SetPackagerAndRecipe` computes `labelAndId = !result.Labeling ? 8 + id : id`. The double negation is correct — don't "fix" it.

## Pallet entry flow (`PalletEntry`)

`Waiting` → (`ReadQR` coil) → `ReadingQR` (ifm reader, `Config.QrRetries` retries; on failure `ReadingQrInError`, which raises `ErrorQr` and keeps retrying — `ContinueIfNoQr`/`DefaultBehavior` is a stub that logs and falls back to the same retry loop, since entry without a pallet code was never implementable) → write QR id to `Memory.QR1` → `WaitingAvailability` (either Bocedi free) → `AskingDB` (`sp_evento_lectura_codigo`, retried every 1 s while it answers packager 0 / NULL) → `SendingID` (sets `ToEmb1`/`ToEmb2` + ID word, raises `SendingQR`) → then either:

- **Bocedi 1**: `WaitForBocedi1` → `WaitEnterBocedi` (PLC raises `SendUpdate`) → increment queue id, `ConfirmUpdate`, re-read FIFO1 → `sp_evento_ingreso_embolsadora(1)` → `Waiting`.
- **Car → Bocedi 2**: `WaitForCar` → `WaitEnterCar` (`SendUpdate` = pallet on car; `CarEntryError` pauses with operator message) → `Status.SetCarPallet(true)` → `ConfirmUpdate` → `Waiting`. `CarMachine` later detects delivery into Bocedi 2 (`SendUpdate2`), notifies `sp_evento_ingreso_embolsadora(2)` and re-reads FIFO2.

A PLC-side `Pause` coil sends the machine to `Paused` (reported to DB as `sistema_detenido`). The PLC can also force-reset the whole entry cycle by raising `WaitingPallet` ("Skip to waiting"). `PalletEntry.NotifyBocediStates()` edge-detects `BCD1OK`/`BCD2OK` and reports embolsadora running/stopped events to the DB.

## Labeling flow (`PalletLabel` + `PrinterMachine`)

Per Bocedi, gated on Omron PLC + printer both connected:

- `Label1` coil = new pallet at label position → re-read FIFO → `printerMachine.Reset(code, shouldLabel)` → raise `Labeling1`.
- `PLCLabeling1` = resume labeling after service restart (weight already taken).
- `WaitCorrection1` = queue id mismatch between PLC and machine → operator must fix (message on HMI, `desorden_embolsadora` alarm).
- `LabelNull1` = pallet at the labeler but queues empty → runs `PrinterMachine` in skip mode, pallet leaves unlabeled.

`PrinterMachine` (per Bocedi, identifier `"Wolrdjet1"`/`"Wolrdjet2"` — **the typo is load-bearing**, it's a registry key and selects the packager number): reads weight from Omron DMs 40/44 (BCD-ish ASCII, `GetWeight`), caches it in `HKLM\SOFTWARE\WencoInfo\<identifier>\CurrentWeight` (survives restarts), writes DM27=1 (weight OK), asks the DB for label commands (`sp_evento_peso_embolsadora_y_datos_etiquetado`), waits for pallet at applicator (DM25==1), then loops on DM10/11: DM10==1 → apply (CIO 161.3 pulse) and print label A or B (DM11 selects), clear DM10/11 to confirm. Label-lost (CIO 178.0) → wait applicator ready (CIO 160.6) and retry, 3 strikes → give up on labeling, alarm `timeout_etiquetado`, operator prints manually with FEED.

When the Fatek raises `Leave1`, the FIFO is re-read, `sp_evento_etiquetado` (pallet out) is sent, `PalletLeave1` acknowledges, and the cycle restarts.

## Queue deletion (HMI-initiated)

HMI sends `del{json DeletePallet}` → `HMIConnection` calls `DeletePalletEmb.Instance(n).StartDelete`: writes the pallet QR/ID and positions into the `DEL*` scratch registers, then validates PC-side (scratch matches FIFO contents at that position) and PLC-side (`DelEmb1` coil → PLC answers `Del1Valid`/`Del1Error`). If valid, the FIFO registers are compacted in place, lengths decremented, FIFO re-read, and the HMI gets `OK`/`NOK`.

## HMI protocol (TCP :8153)

`AcceptHMIs` accepts clients (one per source IP; a reconnect replaces the old one). Request/response, UTF-8, every message prefixed with its byte length as a 4-byte little-endian int (`TcpDevice` on the server, `HMIClient.ReadMessage`/`Request` on the client — both sides must change together):

- `init` → full JSON snapshot; `update` (or anything else) → snapshot where `Packager1/2`, `Car`, `States` are `null` unless changed since that client's last message; `del…` → deletion (answer `OK`/`NOK`); `terminate` → close.
- Snapshot keys: `Config`, `Signals` (coils 21+ as bool[37], indexed by the client's `SignalsNames` enum which starts at `ReadQR`), `PcSignals` (coils 1–20 as bool[20], indexed by `PcSignalsNames`), `Connections`, `EntryPallet`, `ErrorMessages`, `Packager1`, `Packager2`, `Car`, `MachineState` (name → state ordinal), `States` (name → ordinal → state name).
- A client silent for 10 s is dropped. The client (`TcpHMIClient/HMIClient.cs`) polls every 100 ms from a `BackgroundWorker` and mirrors the server's DTOs/enums by hand — **if you touch `Status` DTOs, `Signals` order, or any `States` enum used by the HMI, update `HMIClient.cs` to match** (`PalletEntryStates`, `SignalsNames`, DTO classes).

## Database (SQL Server, schema `[maroti]`)

All calls are stored procedures built by string concatenation in `Devices/SqlDatabase.cs` (fire-and-forget for notifications, awaited for decisions):

| Proc | Used for |
|---|---|
| `sp_evento_lectura_codigo` | entry routing: returns packager preference, recipe, injector, omit-labeling |
| `sp_evento_peso_embolsadora_y_datos_etiquetado` | weight in grams → label A/B printer commands |
| `sp_evento_ingreso_embolsadora` | pallet entered Bocedi n |
| `sp_evento_etiquetado` | pallet labeled/left |
| `sp_evento_alarma` | alarms/events (`SqlDatabase.SystemErrors` enum names are the alarm type ids) |
| `sp_solicitar_ingreso_por_elevador` | elevator authorization |
| `sp_get_id` / `sp_get_codigo` | QR string ↔ int id mapping |
| `sp_get_parametros` | remote config (wired in `GetConfiguration` but not currently called) |

All inputs are passed as `SqlParameter`s (`sp_evento_alarma` maps packager 0 / empty code to `DBNull`). Keep it that way — never interpolate scanned QR content into SQL text.

## Configuration & persistence surfaces

| Where | What |
|---|---|
| `ModbusServer/App.config` | all device IPs (QR readers, printers, Omron PLCs) |
| `secrets.config` (gitignored, next to the exe; see `secrets.config.example`) | SQL connection string — overrides the empty `App.config` placeholder via the `appSettings file=` attribute |
| `HKCU\SOFTWARE\WencoSettings` | `QrRetries`, `ContinueIfNotQr`, `ContinueIfNoDB`, `DefaultRecipe` (read at startup by `Config`) |
| `HKLM\SOFTWARE\WencoInfo\Wolrdjet{1,2}` | last weight per Bocedi (crash recovery) |
| `visualIdData.json` (next to exe) | injector name → visual id map (the ids are what matter; key order is not) |
| `TcpHMIClient/App.config` | `serverIp` of the service |

## Known quirks / things not to "fix" blindly

- `Wolrdjet` (sic) identifiers — registry paths and packager selection depend on the exact string.
- `PackagerPreference.Labeling` means *omit* labeling (see encoding section).
- `Config.ContinueIfNoQr` is stored under registry name `ContinueIfNotQr`.
- Queue ids deliberately skip 0 and 8; 8 is the labeling flag bit.
- `PalletEntry` `currentIdEmb1/2` are re-derived from the FIFO **tail** ids on `Reset()` so restarts don't reuse ids (assumes the PLC appends new pallets at the end of the FIFO area, as the deletion compaction also does).
- The service self-kills (`Environment.Exit(-1)`) on any unhandled exception in the main loop; Topshelf/Windows service recovery restarts it (twice immediately, then after 1 min).
