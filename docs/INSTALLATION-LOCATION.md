# Installation location and relocation

Application files and user data have separate lifecycles. The initial installation
defaults to `%LOCALAPPDATA%/Programs/FrostWhiteComet`. User settings, imported themes,
immutable applied cursor resources and original backups stay in
`%LOCALAPPDATA%/FrostWhiteComet` when application files move to another drive.

Setup now resolves the existing `InstallLocation` from its current-user uninstall
registration before upgrade/uninstall. It requires an absolute, available, non-linked
directory, a CURSORIUM installation marker and the actual CURSORIUM executable.
Drive roots, user-data directories, missing/unmarked paths and linked directories
are rejected. `--inspect-installation --silent` reports the resolved path without
installing, restoring cursors, writing data or changing registration.

An application relocation must back up installation/data, shortcuts, startup and
uninstall registration; copy to a new owned staging directory; verify every file;
quit through normal IPC; update shortcuts, startup and registration; replace the
old fixed-path uninstaller with the location-aware build; verify actual launch and
read-only uninstaller resolution; and remove the original owned application folder
only after those checks. No junction to the old C: path is necessary.

The application remains v1.2.1 for this local relocation; its executable/resources
are unchanged. Only the local recovery/uninstall executable is rebuilt. Existing
published v1.2.1 Release assets and tags are preserved; future releases include this
location fix. Historical installers still use their original default path.

Validation: Release solution builds with zero warnings/errors. Eight read-only
location checks cover the marked original and an isolated marked copy, relative
paths, a drive root, user-data path, missing/unmarked paths and a junction. Each
probe restores registration in finally; private snapshots stay in artifacts.
Actual relocation verifies application/data hashes, unchanged native cursor values,
desktop/Start-menu startup, tray residency and the D: uninstall target. Actual
uninstall is intentionally not performed by relocation.
