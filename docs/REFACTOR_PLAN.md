# Refactor plan — state machine hardening

Branch `refactor/state-machine-hardening`, off `feature/web-hmi`.

Origin: code review of the state machines, the `Machine` framework, the HMI
protocol and the SQL layer. Items are ordered so that each one is a small,
self-contained commit that can be reviewed and, if needed, dropped without
touching the rest.

Verification available while doing this: `./build/typecheck.sh` compiles both
projects on macOS against the .NET Framework reference assemblies, and the
`windows` job in CI does the real MSBuild. Steps 1-4 and 10-12 were confirmed
green by the former. There are still no tests — behaviour is verified against
the plant hardware or a Modbus client simulating the PLC.

## Status

| # | Step | State |
|---|---|---|
| 1 | Harden the HMI `del` message parsing | done |
| 2 | Fix the `DeletePalletEmb1/2` start race (`needDel`) | done |
| 3 | `PrinterMachine.Print1` fall-through | done |
| 4 | Retry idiom helper + fix blind `.Result` reads | done |
| 5 | Merge `DeletePalletEmb1/2` into one parameterized machine | done |
| 6 | Merge `PalletLabel1/2` into one parameterized machine | done |
| 7 | `Machine<TState>` — typed state, drop the reflection rule | done |
| 8 | Collapse `SqlDatabase` duplication | done |
| 9 | Bit math instead of hex-string surgery in `FatekPLC` | done |
| 10 | `Config.Load` must not overwrite settings on read failure | done |
| 11 | Connection/resource cleanup (`IDisposable`, no finalizer `.Wait()`) | done |
| 12 | Dead code removal | done |
| 13 | Liveness watchdogs on waiting states *(added during the work)* | done |

**All thirteen steps are done.** Every commit is green on `./build/typecheck.sh`
and on the `windows` MSBuild job in CI. Nothing here has run against the plant
hardware — see "What still needs the plant" below.

Follow-ups deliberately left out of this branch:

- ~~Generate the WebHMI enum mirrors from the C# sources~~ — done,
  `WebHMI/devserver/generate_enums.mjs`.
- ~~A shared contract assembly to delete the hand-duplicated DTOs and rule 3~~ —
  done, `Contracts/` (`Wenco.Contracts`). Rule 3 now describes a shared
  assembly instead of a manual mirroring duty.
- `Status` still publishes `Queue` and then `LabelPallet`/`ExitPallet` in
  separate assignments, so an HMI snapshot can catch a new queue with a stale
  label pallet. The list-swap in `UpdateQueue` fixed the enumeration crash, not
  this.
- A `sistema_bloqueado` (or similar) alarm type in `sp_evento_alarma`, so step 13
  can report a stall to the database instead of only the log and the HMI.

---

## 1. Harden the HMI `del` message parsing

`HMIConnection.Step()` deserializes `del…` payloads with no `try`. A malformed
message throws out of `Step()`, is caught by the blanket handler in
`AcceptHMIs.Step()`, and that handler does `hmis.Clear()` + restarts the
listener — so one bad packet from any host on the plant subnet disconnects
**every** HMI. Same bug class already fixed on the Node dummy server (057e0e7).

- Wrap the deserialize; on failure answer `NOK` and stay connected.
- Validate `Packager` is 1 or 2 (today anything but 1 routes to Bocedi 2).
- Validate `Pallet` is non-null and `Position` is in range.

## 2. Fix the `DeletePalletEmb1/2` start race

`StartDelete` calls `NextState(States.Validating)` synchronously, then awaits
`SetQrAndId`, which awaits a SQL round trip *before* writing `DEL*a/b/ID`. The
step thread can therefore enter `Validating` and compare registers that are not
written yet. The `needDel` flag in `States.Waiting` is exactly the intended
handshake and is dead code — nothing sets it.

- `StartDelete` writes the registers first, sets `needDel` last, never calls
  `NextState`.
- Reject a start while a deletion is already in flight (the machine is a
  singleton shared by every `HMIConnection`).

## 3. `PrinterMachine.Print1` fall-through

If `omronReadingDataTask.Result[1]` is neither 1 nor 2 the state neither
transitions nor stops, so the block re-runs every 100 ms — including
`plc.WriteCIOBit(161, 3, 1)`, pulsing the applicator at 10 Hz.

- Handle the unexpected value: log, alarm, and leave labeling for that pallet.
- Move the `WriteCIOBit` after the label selection so it only fires on a valid
  instruction.

## 4. Retry idiom helper + blind `.Result` reads

The `IsCompleted` / `IsFaulted` / back-off / relaunch idiom is copy-pasted ~20
times and several sites skip the `IsFaulted` check, so a faulted task throws
`AggregateException` out of `Step()`. Known sites: `PalletEntry.cs:127`,
`PalletLabel1.cs:179`, `PrinterMachine.cs:408`, `HMIConnection.cs:78,120,149`,
`AcceptHMIs.cs:80`.

- Add `Machine.TryComplete(...)` helpers covering the two shapes in use
  (retry-on-fault, and treat-fault-as-false).
- Convert the unchecked sites; leave the already-correct ones for the later
  mechanical pass so this commit stays readable.

## 5–6. Merge the per-packager machines

`PalletLabel1`/`PalletLabel2` differ by 124 diff lines, `DeletePalletEmb1`/`2`
by 75, and the difference is mechanically the digit 1 vs 2 in signal names,
memory bases, config keys and error fields. They have already drifted
(`WaitAck` sits at a different enum position; `static ILog` vs
`static readonly ILog`).

- Introduce a `PackagerBinding` describing one lane: signals, memory bases,
  `Status` packager accessor, error-message setter, printer/Omron app keys,
  printer-machine identifier (`Wolrdjet1`/`Wolrdjet2` — the typo is
  load-bearing, see ARCHITECTURE quirks).
- One `PalletLabel` and one `DeletePalletEmb`, instantiated twice.
- `Machine.Name` must stay `PalletLabel1` / `DeletePalletEmb1` etc. — the HMI
  and `transitions.json` key on those names.

## 7. `Machine<TState>`

`public object State` forces `(States)State` casts everywhere, boxes on every
transition, and makes enum *declaration order* a silent semantic contract
(`PrinterMachine.WeightOk` compares with `>=`). The reflection lookup of a
nested type literally named `States` is CLAUDE.md hard rule #2 — a rule that
exists only because the base class can't see the type.

- `abstract class Machine<TState> where TState : struct, Enum`, keeping a
  non-generic `Machine` base for the heterogeneous collections.
- Enum values read from `typeof(TState)`; the reflection rule goes away.
- Update `CLAUDE.md` and `docs/ARCHITECTURE.md`.
- Check `generate_transitions.mjs` still matches the class declarations.

## 8. Collapse `SqlDatabase`

Eight near-identical 25-line methods, each doubled by a `static Foo() =>
Instance._Foo()` wrapper. `ExecuteReader()` is the synchronous call inside
`async` methods.

- One `ExecuteAsync<T>(Func<SqlConnection, SqlCommand> build, Func<SqlDataReader, T> read, T onError)`.
- `ExecuteReaderAsync`, and check the `ReadAsync()` result before reading columns.
- Keep the stored-proc text as-is (it is a contract with the `[maroti]`
  schema); only the plumbing changes.

## 9. Bit math in `FatekPLC`

`GetPalletInfo` decodes the ID word with `ToString("X")` + `Substring` +
`Convert.ToInt16(…, 16)`; `SetQr`/`SetQrAndId` encode the same way. It throws
`FormatException` on a short hex string and is hard to read.

- `word & 0xF`, `(word >> 4) & 0xF`, `(word >> 8) & 0xFF` for decode; shifts
  for encode. Same wire values, no string round trip.
- `VisualID`: keep a reverse `ushort -> string` map instead of
  `Data.Keys.ElementAt(id - 1)` (O(n) and insertion-order dependent).

## 10. `Config.Load` must not clobber settings

`Load()` ends with an unconditional `Save()`, so a transient read failure
writes the defaults back over the operator's registry values — silent,
permanent config loss.

- Only `Save()` when the key was absent (first run).

## 11. Connection/resource cleanup

- `HMIConnection` finalizer does blocking `.Wait()` on the finalizer thread.
- `Machine.Remove()` deregisters from `Status` but never cancels the CTS or
  closes the socket; `AcceptHMIs` replacing a same-IP client leaks the old
  `TcpDevice`.
- Make `HMIConnection`/`TcpDevice` `IDisposable`, dispose explicitly, drop the
  finalizer.

## 12. Dead code

Removed: `IsQrValid` and the commented-out branch in `SetQr` that was its only
caller; `QrReader.Disconnect` / `NetworkPrinter.Disconnect` (unreferenced, and
both dereference a `stream` that is null until the first send);
`HMIClient.Status`.

Deliberately kept:

- `SqlDatabase.GetConfiguration` / `Config.ContinueIfNoDB` — remote config is
  planned, and the method is the only record of the `sp_get_parametros`
  signature. `ContinueIfNoDB` is also part of the `Config` DTO on the wire, so
  dropping it would mean touching `HMIClient` and both WebHMI mirrors.
- `FatekPLC.PrintFIFOs` / `SetVerbose` — console-mode debugging aids.
- `needDel` — live again after step 2.

## 13. Liveness watchdogs on waiting states  *(added during the work)*

Steps 2 and 3 each turned out to be an instance of the same larger gap: a state
that waits on a PLC bit or a device with no ceiling on how long it may wait. An
exception restarts the service and the PLC re-syncs it on the next handshake —
that path is designed for. A *hang* has nothing to trip, so the machine sits
there until somebody notices the line has stopped.

`Machine<TState>.Step()` is now a sealed wrapper that runs a liveness check and
then calls the machine's `OnStep()`. Machines declare `StateTimeouts` — the
states that are supposed to make progress and how long they may take — and
override `OnStuck(state)` to add an operator message or an alarm. A stall is
reported **once per visit** to the state, not ten times a second.

Reporting only. Nothing is forced: when a pallet is halfway through an
applicator, the safe move is to tell the operator exactly which machine and
state are stuck, not to guess a transition.

Covered: `PrinterMachine` (the whole weigh/print/apply cycle), `PalletLabel`
(the Fatek and database handshakes), `PalletEntry` (QR write, DB routing, FIFO
updates), `FatekPLCCommunication` (the startup handshake).

Deliberately **not** covered, because a long wait there is normal and a false
alarm is worse than none:

- idle states — `PalletEntry.Waiting`, `PalletLabel.WaitingPallet`
- waits on an operator — `WaitingCorrection`, `Paused`, `ReadingQrInError`
- `PalletEntry.WaitingAvailability` — both Bocedis can legitimately be full
- `PalletEntry.WaitForBocedi1` / `WaitEnterBocedi` / `WaitForCar` /
  `WaitEnterCar`, and all of `CarMachine` — conveyor and car movement, whose
  duration depends on how the line is running
- the three connection machines — reconnecting forever is the intended
  behaviour, and the HMI connection chips already show it
- `FatekPLCCommunication.Init` — with the PLC off, sitting there is correct

The timeouts are deliberately generous (30 s – 5 min): they are "this cycle is
not moving at all" limits, not cycle-time targets. They are the numbers most
likely to need tuning against the real line.

---

## What still needs the plant

Nothing in this branch has run against hardware. In rough order of risk:

1. **`FatekPLC` word packing** (step 9). Verified exhaustively equivalent to the
   old algorithm by simulation, but it is the PLC wire contract.
2. **`SqlDatabase.Execute`** (step 8). Every stored-procedure call now goes
   through one path, and `ExecuteReaderAsync` replaced a synchronous read.
3. **The merged lanes** (steps 5-6). Bocedi 2 now runs code that was only ever
   exercised as Bocedi 1's copy. Diffing showed no behavioural difference, but
   only the line proves it.
4. **Step 13's timeouts.** Watch the logs for `has been in ... and is not
   progressing` on a healthy line; any that appear are too tight.
