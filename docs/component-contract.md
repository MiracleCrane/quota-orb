# Component contract v1

Windows 10/11, .NET Framework 4.8, and a signed-in official Codex Desktop/CLI are required.
The orb can be installed independently; a proxy launcher and transport bridge are optional.

## Release and installation

Release `v0.1.10`: `quota-orb-0.1.10-windows.zip` and `SHA256SUMS`.
Extract the ZIP, then run:

```powershell
./install.ps1 -Destination $componentDirectory -NoShortcuts
```

This mode creates no desktop/Start-menu shortcuts and never edits Startup, Codex
shortcuts or bridge configuration. It rejects `-CodexShortcutPath` and an existing
`linked-shortcuts` directory; the kit must detach its old bindings before migration.
The receipt `quota-orb-install.json` records the physical destination and installed version.
Default installation continues to support the standalone manual toggle and optional bindings.

## Process interface

- `QuotaOrb.exe --ensure-running` (or no arguments): start or notify the existing instance to wait for a new Codex window. Never toggles it off.
- `QuotaOrb.exe --toggle`: standalone user switch only; do not use from kit startup.
- `QuotaOrb.exe --exit`: cooperative shutdown, no Codex shutdown.
- `Get-QuotaOrb.ps1 -Destination $componentDirectory`: JSON schema 1, installed/version/running/processIds/statusFresh/status. Stale or PID-mismatched diagnostic state is null, not evidence of a running ball.
- `uninstall.ps1 -Destination $componentDirectory`: requires a matching component receipt; removes only known installed files, retains settings, unknown files and the directory. Refuses shortcut-bound installations.

Launch the persistent GUI from the user's desktop context, not an Agent's kill-on-close
job. Do not wait for the primary GUI process to exit during startup. The component is
single-instance per Windows session, including across installation paths; stop an old
installation before intentionally switching to another. Successful dispatch alone does
not prove visible UI or current quota: verify the live PID and fresh diagnostic state.

No permanent watcher or login startup is installed. The existing 30-second startup wait,
45-second window replacement grace and short takeover wait are bounded. Restart signals
renew the wait. Missing 5h quota remains hidden; account state is read through the local
official app-server. The component must not receive or persist credentials from the kit.

## Ownership and failures

The kit owns one final launch entry and calls components independently. A missing or
failing orb must not prevent Codex startup. The orb never owns proxy or bridge fallback.
Kit-managed upgrade/uninstall must not use the standalone shortcut-binding option.
Store-packaged Agent processes can see a redirected AppData view; installer paths are
resolved from file handles, and status must be checked in the actual GUI user's context.
