# CURSORIUM / FROST: validation record

## CURSORIUM v1.0 — October 13, 2026

Fresh checks on Windows 11 build 26100, SDK 10.0.103, .NET 8 target;
**one 2560×1440 display at 96 DPI**. The two-display results below are historical.

- Release solution build and self-contained Windows x64 app/installer publish: zero warnings/errors.
- Unchanged FROST native resources: 388 checks; new Obsidian resources: 388 checks.
- Core/catalog/import/profile/native transaction suite: 66 checks, including actual theme switches,
  partial failure after four role writes, applied-state persistence failure, exact rollback, durable
  interrupted-journal recovery and restoration of the pre-test native configuration.
- Published self-contained app desktop suite: 84 checks. Actual pointer movement, left/right
  SendInput, keyboard focus/pass-through, fullscreen pause, draft/apply/cancel separation,
  four distinct effect renderings, UI/tray/profile actions, import, independent click-only mode,
  fresh-process configuration reload, repeated preview resource checks and overlay disposal passed.
- 960×640, 960×600 and 850×520 DIP window layouts captured/inspected. These are layout checks,
  **not** physical DPI-scaling verification.
- Existing user's cursor backup and legacy/new configuration files passed before/after hash and
  existence checks. Tests used separate data roots and restored their pre-test Windows cursor scheme.
- New installer binary built. Its install/uninstall/upgrade round trip was **not run** on the existing
  user installation; old installer evidence below does not establish new-version installer behavior.

Published app short engine-only measurements (approximately four seconds each; 28 logical processors):

| Scenario | WPF invalidations/s | Working set MiB | CPU, one core % | GDI before/after |
|---|---:|---:|---:|---|
| All effects disabled | 0.00 | 128.1 | 3.89 | 18 / 18 |
| Enabled, stationary | 0.00 | 124.9 | 0.39 | 18 / 18 |
| Comet, 60 target | 53.18 | 143.3 | 17.56 | 18 / 18 |
| Minimal, 60 target | 53.17 | 132.9 | 3.90 | 18 / 18 |
| Neon, 60 target | 53.46 | 134.5 | 3.90 | 18 / 18 |
| Stardust, 60 target | 54.47 | 135.8 | 6.25 | 18 / 18 |
| Comet, 120 target | 69.71 | 135.6 | 6.25 | 18 / 18 |
| Power saving, 30 target | 29.97 | 136.2 | 3.12 | 18 / 18 |

Disabled sampling was 4/s, with no rendering. Idle observed one distinct pointer position and no
moving-model observations. First disabled/first active measurements can include deferred WPF/runtime
initialization and are not long-session performance claims. Machine CPU equals one-core CPU / 28;
memory excludes the full studio panel. Invalidations are **not measured GPU presentation FPS**.

Raw local evidence: `artifacts/cursorium-verification/{core-report,desktop-report,restart-report,
performance-report,user-data-protection}.json`, screenshots and effect render PNGs.
Shareable sanitized evidence: `docs/verification/cursorium-2026-10-13.json`.
Current architecture and limitations: [delivery](docs/CURSORIUM-DELIVERY.md),
[architecture](docs/CURSORIUM-ARCHITECTURE.md).

Windows 10, physical mixed DPI/multiple-monitor configurations, negative display coordinates,
physical hotplug, sleep/resume, secure/protected desktops, exclusive fullscreen, genuine 120 Hz
presentation and long-term resource/leak behavior remain unverified for this version.

The following sections preserve original FROST history; their pass counts are not new CURSORIUM tests.

### Subsequent installed-version upgrade and launch verification

On October 13, after the user reported the original FROST interface reopening, the
existing marked installation was backed up and upgraded using the built CURSORIUM
installer (`--silent --no-launch`, exit 0). All **577 published files** matched the
installed copies by SHA256. A new CURSORIUM Start menu entry and desktop shortcut were
verified against the installed executable. Launching the Start menu entry produced the
CURSORIUM window; reopening from the desktop retained the same new process. Windows'
uninstall registration displays CURSORIUM. Native window DPI was 96.

The upgrade-operation comparison preserved all 20 current cursor values/types and the
original cursor backup hash. Full old installation and user-data rollback copies remain
local under `artifacts/installed-upgrade-rollback-*`. Subsequent user Apply/Restore
actions are outside that comparison. Fresh install, uninstall and rollback round trips
remain unverified. This supersedes the earlier lack of an actual **upgrade** test only.

Raw evidence: `artifacts/cursorium-verification/installed-upgrade-report.json` and
`installed-cursorium.png`. Sanitized evidence:
`docs/verification/cursorium-installed-upgrade-2026-10-13.json`.

Initial tests: **October 12, 2026, Europe/Budapest**, Windows 11 Pro build 26100, SDK 10.0.103, .NET 8 target. Implemented features are distinguished from hardware/long-session behavior that remains unverified.

## Initial native version

| Stage | Delivery | Observed verification | Remaining work |
|---|---|---|---|
| 1: Native cursors | 15 CUR files at 32/48/64/96 px, two ANI files, vector and PNG sources | Actual Win32 loading, bitmap dimensions/hotspots for 60 CUR entries; both ANI files loaded; install/restore and theme persistence after engine exit. 410 checks passed. | Full native animation playback was not captured frame-by-frame; all Windows pointer-size settings were not inspected. |
| 2: Desktop trail | Per-display overlays, physical history, Raw Input, fading filaments and particles | Actual injected click reached the underlying button; keyboard character reached the original focused textbox; overlays did not steal focus; both displays and fade expiration passed. | Secure/protected/elevated interfaces and exclusive fullscreen were not tested. |
| 3: Controls and tray | Live parameters/colors/presets/halos, settings, tray, optional startup | Actual UI/tray actions changed settings; panel close/reopen worked; startup entry enable/disable was tested and restored. 39 desktop checks passed. | Additional visual/keyboard-accessibility refinement remains possible. |
| 4: Environment/resources | PerMonitorV2, per-display windows, rebuilds, power dispatch, bounded model, disposal | Two 96 DPI displays: 2560x1440 and 1920x1080, second below with x offset 339. Three rebuilds released old windows; external borderless fullscreen paused; model/configuration boundary checks passed. | Mixed DPI, negative-coordinate layouts, physical hotplug, sleep/resume, long sessions, and 120 Hz presentation remain untested. Stable displayed 60 FPS is not established. |
| 5: Release/installer | Self-contained x64 ZIP, installer/uninstaller, cursor/source ZIPs, checksums | Packaged app passed 410 native and 39 desktop checks. All 22 installation checks passed. Uninstall with the tray running and theme installed exactly restored original registry values/types/absence and removed files/shortcut/registration. Post-uninstall verification passed 388 checks. Final package round trip matched 518 installed files by hash. | Windows 10 untested; unsigned executables; x64 only. |

Counts repeat resource checks in separate runs and are not additive unique checks. Raw local evidence is in `artifacts/verification/`; machine-specific registry snapshots, local paths, and raw reports are excluded from Git.

## English edition

The first public edition translates app/accessibility labels, tray menus, installer/uninstaller prompts, cursor labels, operation errors, documentation, and generated metadata. Legacy Chinese preset names migrate to English while retaining numeric settings.

Fresh English-edition checks on the same machine:

- Release solution build: **zero warnings, zero errors**.
- Read-only native resource/model/configuration verification: **388 checks passed**, including migration of legacy preset names while preserving numeric settings.
- English interactive UI/tray/input/display verification: **39 checks passed**, exit code 0. The panel screenshot was visually inspected and its height adjusted to fit the longer English text.
- Tests ran with `--isolated-test` because the installed original edition was already running. The existing application's process, cursor scheme, backup, and settings file were preserved. No fresh system-theme install/uninstall round trip was run for this translation while that user installation was active.

Initial implementation results above are historical evidence, not a fresh test of the translated installer. Raw English test outputs remain local under `artifacts/english-verification/`.

## Initial short benchmark

Engine only, without the full panel; approximately four seconds per scenario. Machine CPU was normalized across 28 logical processors. Working set includes .NET/WPF and two full-display alpha surfaces.

| Scenario | Drawing requests/second | Working set | Machine CPU |
|---|---:|---:|---:|
| Standard, 60 FPS target | 54.16 | 161.1 MiB | 0.209% |
| Standard, 120 FPS target | 69.63 | 159.8 MiB | 0.418% |
| Power saving, 30 FPS target | 30.00 | 160.6 MiB | 0.126% |

The final idle sample observed 100 distinct pointer positions and substantial movement, so external input contaminated it; it is not an idle-performance conclusion. GDI counts were 33-34 without observed continuous growth in these short measurements. This is not a long-term leak test.

These numbers count WPF invalidations, **not GPU presentation FPS**. The native system pointer remains independent of effects. Additional presentation measurement/optimization is needed, especially at 120 Hz on high-resolution multiple displays.

## Restoration

Initial release tests ended with the original scheme restored, active backup archived, no FROST process, and test installation/startup/shortcut/uninstall entries removed. Settings and archived backups were retained by design.

Read-only resource and English UI checks do not automatically install the system theme. Install/restore fixtures temporarily change system cursors. Always inspect local reports and the final state when rerunning system-changing verification.
