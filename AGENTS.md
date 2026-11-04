# Maintenance boundaries

This repository owns the standalone Windows quota orb, quota reading, UI, lifecycle,
component installer and versioned releases. Follow the user's shared instructions.

- The desktop kit owns its final shortcuts, proxy environment and component selection.
- The transport bridge repository owns model transport and its configuration.
- In kit mode, use `install.ps1 -NoShortcuts`; never rewrite shared launchers or bridge settings.
- Changes to `docs/component-contract.md` require coordination with the kit maintainer before implementation. Keep backward-compatible entry points or version the contract explicitly.
- Keep paths, ports, account data, installation receipts and logs out of Git and release assets.
- Run `build.ps1 -Test` and affected installer checks. Native fixtures do not replace a real user cold-restart acceptance; never terminate the active Codex host to test one.
- Release from an immutable tested commit, package the scripts and docs with binaries, and publish SHA-256 sums. Do not make consumers follow a moving branch.
- Public documentation is authoritative; personal Agent Skills should point to version-matched docs instead of carrying another source/installer copy.
