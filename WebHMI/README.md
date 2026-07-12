# Web HMI

Browser-based replacement for `TcpHMIClient`. The operator opens
`http://<server-ip>:8080` from any PC/tablet on the plant subnet — no client
install, no DTO duplication, no aspect-ratio problems.

Status: **frontend + dummy server (this folder) are done and testable on any
machine with Node ≥ 18**. The server side (serving `wwwroot/` and these API
endpoints from ModbusServer via `HttpListener`) is the next step and must
follow the contract below.

## Try it locally

```sh
node WebHMI/devserver/dummy_server.mjs
# open http://localhost:8080   (supervisor PIN: 1234)
```

`devserver/dummy_server.mjs` (zero dependencies) serves `wwwroot/` and
simulates the line: pallets arrive with a QR read (sometimes failing), route to
Bocedi 1 or via the transfer car to Bocedi 2, advance through bagging →
labeling → exit, with occasional connection drops and weight-correction
warnings. Deleting a pallet from a queue works end-to-end, including PIN login.

## Wire contract (to be implemented in ModbusServer)

Transport is **Server-Sent Events + JSON over HTTP**, chosen over WebSockets
because `EventSource` reconnects automatically and SSE needs nothing beyond
`HttpListener` on .NET Framework 4.8 (WebSockets would require Win8+).

### `GET /api/events` (SSE)

Every 500 ms the server pushes one event:

```
event: status
data: <SystemStatus JSON>
```

`SystemStatus` is exactly what `HMIConnection.CreateMessage(force: true)`
produces today — same property names and casing (System.Text.Json defaults,
PascalCase), with one simplification: **every snapshot is complete**; the
`Updated`-flag delta mechanism is not used (`Packager1/2`, `Car`, `States` are
never null).

```jsonc
{
  "Config":        { "QrRetries": 1, "ContinueIfNoQr": false, "ContinueIfNoDB": false, "DefaultRecipe": 1 },
  "Signals":       [ /* 28 bools, FatekPLC.Signals.ReadQR .. BCD2EntryError */ ],
  "Connections":   { "QrReader": true, "MasterPLC": true, "SlavePLC": true, "WencoDB": true,
                     "Packager1": true, "Packager2": true, "Labeler1": true, "Labeler2": true },
  "EntryPallet":   { "Qr": "…", "Id": "…", "Recipe": "…", "Injector": "…", "Labeling": false }, // or null
  "ErrorMessages": { "BDC1Error": "", "BDC2Error": "", "EntryError": "", "CarError": "" },
  "Packager1":     { "Queue": [ /* Pallet */ ], "LabelPallet": null, "ExitPallet": null },
  "Packager2":     { "Queue": [], "LabelPallet": null, "ExitPallet": null },
  "Car":           { "CarPosition": 2, "HasPallet": false, "Pallet": null }, // enum as int
  "MachineState":  { "PalletEntry": 0, "CarMachine": 1, /* machine name -> state int */ },
  "States":        { "PalletEntry": { "0": "Waiting", "1": "ReadingQR" /* … */ } /* … */ }
}
```

### `POST /api/login`

Body `{"pin": "…"}`. Returns `200 {"token": "…"}` or `401`. Tokens expire
after 15 min of inactivity (sliding). The status view needs **no** login;
only destructive actions do.

### `POST /api/delete`  (requires `Authorization: Bearer <token>`)

Body is the existing `DeletePallet` DTO:

```json
{ "Pallet": { "Qr": "…", "Id": "…", "Recipe": "…", "Injector": "…", "Labeling": false },
  "Packager": 1,
  "Position": 0 }
```

Responds only after the deletion state machine finishes (like the current
`OK`/`NOK` over TCP): `200 {"result":"OK"}`, `200 {"result":"NOK","error":"…"}`
or `401` when the token is missing/expired.

### Static files

Everything in `wwwroot/` is served as-is; `/` → `index.html`. The page is a
single plain-JS app (no build step): `index.html`, `style.css`, `app.js`.

## Server-side integration plan (ModbusServer)

1. New `WebHmi` component started by `MainProcess`: `HttpListener` on `:8080`
   serving `wwwroot/` (deployed next to the exe) plus the three endpoints.
2. Status snapshots: serialize on the step thread (reusing the
   `CreateMessage` logic, always-full), hand the string to the SSE writer —
   never let HTTP threads read `Status.Instance` directly.
3. Deletions: HTTP thread enqueues into `DeletePalletEmb1/2` exactly like
   `HMIConnection` does today, then awaits completion.
4. Auth: PIN (hashed) in `secrets.config` to start; later per-user accounts in
   the `[maroti]` schema if an audit trail per operator is needed.
5. Run alongside the TCP protocol (port 8153) until both operator stations use
   the browser, then retire `TcpHMIClient` and `AcceptHMIs`/`HMIConnection`.

## Keeping the dummy honest

If a DTO, enum order, or `FatekPLC.Signals` coil changes in ModbusServer, the
mirrors at the top of `wwwroot/app.js` and `devserver/dummy_server.mjs` must
change with it (same rule as the old hand-duplicated HMI DTOs — see
`CLAUDE.md` rule 3).
