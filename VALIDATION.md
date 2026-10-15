# v1.5.2 Performance Verification & Release Hardening — Preview, 2026-10-15

Actual Windows 11 build 26100, one 2560x1440 / 96-DPI display. User paused
automatic input; it was not resumed. Production D:\CURSORIUM remains v1.5.1.
The matrix has 153 completed raw trials, 118 accepted, 35 excluded by input
conditions, and 21 mode/version groups short of three accepted rounds.
Each observation uses 5 seconds warmup and 20 seconds stable. The strict complete
gate rejects the incomplete matrix. This is not a full performance pass.
Natural-input comparison, native CPU attribution, complete WPF/native rerun,
24/72h, Windows 10 and physical multi-monitor/mixed-DPI remain Pending.
No new production-renderer optimization or general CPU reduction is claimed.

Actual non-input checks: core 162 (without native roundtrip), online security 51,
curation/update 104, real public-network encrypted-filesystem download 14,
native cursor-file validation FROST/Obsidian 388 each. Curation initially failed
because its retained previous-catalog signature fixture had not been copied;
the original public fixture was restored and the check rerun successfully.
Seventeen synthetic protocol correctness tests pass; these are not benchmark data.
Release solution build: zero errors/warnings. PerformanceHost publishes for exact
8.0.24 / 10.0.13; explicit mixed-host exceptions remain WFO0003 and WFAC010.
All 201 permanent user files, critical JSON presence/hashes and native registry
values/types match the private baseline. No user data or raw trace is published.

See [Chinese delivery](docs/CURSORIUM-v1.5.2-DELIVERY.md),
[protocol](docs/CURSORIUM-v1.5.2-PROTOCOL.md), and
[sanitized actual evidence](docs/validation/v1.5.2).
The delivery's release addendum records isolated installer and final artifact gates
when actually executed. Historical results below do not count as this round's rerun.
v1.4.0 remains Latest Stable; this preview is not Steam Ready.

# v1.5.1 Download filesystem compatibility — prerelease, 2026-10-14

Reproduced Simple Dot or Star failure at DownloadManager.InstallCursor/Directory.Move
under an encrypted, isolated LocalAppData root. Controlled copies now validate before
atomic manifest commit. Real network/filesystem 14, core without native roundtrip 162,
online security 51, curation/update 104, actual WPF/network 107 checks passed.
WPF testing used encrypted isolated data and restored its original native snapshot.
Repeated live previews now release the previous owned resources.
See [patch delivery](docs/CURSORIUM-v1.5.1-DELIVERY.md) and
[actual installed-theme screenshot](docs/validation/v1.5.1/simple-dot-star-installed.png).
Historical v1.5 hardware/performance limitations below remain open.
# v1.5.0 Stability, Performance & Compatibility — prerelease, 2026-10-14

Target net10.0-windows; self-contained runtime 10.0.13. Release build: zero
errors/warnings. Two mixed WPF/WinForms hosts explicitly retain manifest PerMonitorV2
and exclude only WFO0003 (documented exception); no broad warning suppression added.
Retained checks: core 180, desktop 85, interface 98, cached restart 99, collection 28,
online security 51, curation/update 104, network/WPF 103, FROST/Obsidian 388 each.
New crash/concurrency/external-edit/data-root/log checks: 44. Isolated installer
lifecycle: 16, including installed self-removal helper, junction rejection and
failure-blocked deletion. Cross-volume upgrade clone: 104 protected files and
854 payload files; native values/types unchanged. Final assets may contain additional
documentation files; the final upgrade report records their actual payload count.

Additional deferred-tray/real IPC checks: 11, cached first show 12, no-panel actual
tray Quit 5. First-show draft preservation and source resource lookup passed.
Actual normal --tray before first UI: baseline 176.6 MiB/GDI 227/USER 88 versus
candidate 141.7 MiB/GDI 28/USER 23, single-core CPU 1.130 -> 0.617% over 15 seconds.
This is a short startup-tray comparison; opening the UI retains it for draft continuity.
ScrollChanged now explicitly updates ANI visibility and overlay exclusion; the
existing 28 collection assertions, including the original timing check, pass.

180.57 seconds / 156 real WPF show-hide/Profile/native-load/rebuild cycles passed;
this is not a 24/72-hour pass. Native crash tests actually terminate child processes
after four registry writes and then recover the pre-test snapshot. Original user
critical JSON existence/hashes and 102 permanent files match the first private
baseline; full native registry export also matches. No private backups are committed.

Comparable eight-mode and separate fifteen-mode measurements, CSV resource samples,
same-runtime copy microbenchmarks and actual normal-tray idle observations are saved.
Idle sampling: 63.90 -> 10.48/s. Typed-copy allocation: 22.56 -> 2.56 MB per 20,000
operations. Fifteen-second normal tray idle: 1.128 -> 0.720 percent of one CPU core,
171.6 -> 172.4 MiB ending working set, GDI 227 and USER 88 unchanged. Some active
four-second driver-loop CPU measurements increased. Those results remain explicit,
not claimed as global CPU improvements; longer low-interference confirmation blocks
Stable/Steam Ready. State-probe startup timing is not first-painted-frame timing.

Only Windows 11 build 26100, one 2560x1440/96-DPI display was physically tested.
Windows 10 LTSC candidate, physical DPI/mixed DPI, dual monitors, hotplug, lock/sleep,
RDP/Explorer restart, exclusive games, long stress and complete historical lifecycle
matrix remain unverified. .NET 8 v1.4.0 Stable remains available. See
[Chinese delivery](docs/CURSORIUM-v1.5-DELIVERY.md),
[compatibility and Steam blockers](docs/CURSORIUM-v1.5-COMPATIBILITY.md),
[reproduction commands](docs/CURSORIUM-v1.5-DIAGNOSTICS.md), and
[actual reports](docs/validation/v1.5). This is a prerelease, not Steam Ready.

---

# v1.4.0 Online Library Expansion & Curation — 2026-10-14

Release source build: zero errors/warnings. Retained checks passed: core/native 180,
desktop/input 85, interface 98, cached WPF restart 99, collection 28, online security
51. Added curation/update/version-pin checks: 104; actual public-network WPF checks:
103; publisher boundary checks: 10. The actual installed 1.3.1 binary also passed
98 checks while fetching/verifying the expanded catalog. Evidence:
[summary](docs/validation/v1.4/summary.json), [delivery](docs/CURSORIUM-v1.4-DELIVERY.md),
[audit](docs/CURSORIUM-v1.4-SOURCES.md), [WPF captures](docs/CURSORIUM-v1.4-SCREENSHOTS.md).

29 actual resources, not the aspirational 30+: 9 newly approved full-role cursors,
10 new original trusted-renderer presets, 10 preserved packages. Public verification
refetched all 29 packages and 35 images; pinned RSA-PSS signatures, SHA256, sizes and
production ZIP/CUR/ANI/native loading passed. Catalog 1.1.1 changes tags/collections,
not resource package versions. 100/300/500-resource fixtures remain bounded to 24
cards; timings/handles and eight short engine measurements are reported separately.

Same-ID updates retain old immutable versions, explicit preview/Apply and correct
Cancel/motion-only behavior. Tests use isolated data and restore exact native state;
protected user JSON existence/hashes match. No user profiles, raw registry snapshots,
private signing key or rollback archives are committed. The first BitBlt outputs
captured an unrelated window and were excluded; public images rasterize the running
WPF visual tree at its real DPI. They are not an unobstructed desktop capture claim.

Observed hardware: Windows 11, one 2560x1440 monitor, 96 DPI. Physical 125/150%, dual
monitors, Windows 10, mixed DPI, long-duration stress and the full install/uninstall
matrix remain unverified. Logical layout simulations are explicitly not physical
DPI tests. Guarded isolated/current installation upgrade reports remain local;
release checksums and public-download verification accompany the final assets.

---

# v1.3.1 Visual Experience Refinement — 2026-10-14

Release build: zero errors/warnings. Current checks: core/native 180,
desktop/input 85, interface 98, collection 28, online security 51 and actual
public network/WPF 80, all passed. Original online 51 retained with 29 added
visual/state/lifecycle assertions. Additional actual cached WPF restart and full
interface checks: 99 passed. A production startup failure exposed card resource
lookup before the control joined the host tree; rendering now waits for Loaded
and immediately uses the valid cache before asynchronous refresh. The failed
startup is not counted as passing installation evidence. Another 99-check real
WPF run uses a private clone of current user data. Dropdown keyboard tests retain
their original assertions and real SendInput, with bounded condition waits rather
than fixed 60/100 ms guesses; Escape explicitly closes an open menu before Cancel.
The private clone and its screenshots are not published. Original FROST and Obsidian resource suites:
388 checks each. No native transaction, security protocol or renderer rewrite.

Same-size old/new captures cover Explore, Details, Cursors and Trails; actual
850x520 and 1680x980 windows included. Windows 11 build 26100, one 2560x1440 /
96 DPI display. 125%/150% and alternate resolutions are logical layout checks,
not physical DPI switches or dual-monitor evidence.

72 actual pre-existing data/resource files retain hashes through development and
isolated regression; five critical files separately checked by existence/hash.
Upgrade checks compare all files before launch, then distinguish normal signed
catalog-cache refresh from protected settings/profiles/backups/native files.
Private snapshots/installation backups remain untracked.

100/300/500 synthetic catalog fixtures each create 24 cards, measured final layout
25.94/25.87/26.65 ms; USER delta 0. Thirty detail/search/back cycles: ~443 ms,
GDI delta -4, USER delta 0. Thirty actual cute-theme switches: GDI 187 -> 173,
USER 101 -> 81. Short measurements do not establish long-term leak/performance
claims. Original eight-mode diagnostics remain available; isolated processes can
coexist with the production tray and are not exclusive background CPU baselines.

Ten offline regenerated real-resource PNG hashes match exactly. Official catalog
1.0.4 changes cover URLs while preserving all ten package identities/hashes and
licenses; fixed-key RSA-PSS verification and all safe download checks remain.
Windows 10, physical mixed DPI, current dual-display, long stress, exclusive
fullscreen/protected desktops and complete install/uninstall/failure matrices
remain unverified.

[Chinese delivery](docs/CURSORIUM-v1.3.1-DELIVERY.md),
[real before/after screenshots](docs/CURSORIUM-v1.3.1-SCREENSHOTS.md),
[passing reports](docs/validation/v1.3.1).

The following records are historical evidence for their named versions.

---

# v1.3.0 Discover & Install — 2026-10-13

Release solution: 0 warnings/errors. Current iteration checks: core/native 180,
desktop/input 85, interface 98, collection 28, online security 51 and real public
network/WPF 51, all passed. Existing safety/transaction checks retained; local
preview assertions moved to Cursors as Explore became discovery, and details
receive actual CUR/ANI/motion interaction and download/apply/cancel checks.

Actual display: one Windows 11 2560x1440, 96 DPI monitor. 125%/150% and alternate
work areas are logical layout simulation; no claim of physical DPI changes,
dual-screen this run, Windows 10, mixed DPI, long stress or full uninstall matrix.
Five protected user files retain existence and SHA256 throughout isolated tests.
100/300/500 explicit performance fixtures instantiate only 24 cards; measured
layout 26.45/24.71/25.48 ms with USER delta zero. Original 8-mode short benchmark
and 30 Cute-switch native handle measurements are included as short measurements.

[Chinese delivery](docs/CURSORIUM-v1.3-DELIVERY.md),
[actual screenshots](docs/CURSORIUM-v1.3-SCREENSHOTS.md),
[passing reports](docs/validation/v1.3).
Cross-volume isolated upgrade also passed with D: target and C: Windows TEMP: 61 protected files, 750 payload files, five Cute themes and unchanged native values. Installer staging now uses the target volume; the first failed production attempt restored the old version safely.
Signed official catalog and real ZIP downloads are published separately in
catvir4493/cursorium-library. RSA-PSS authenticity does not replace untrusted
ZIP/native parsing. Key compromise, first-install replay and lack of transparency
or expiry services remain explicit boundaries. No private publishing key or user
configuration is included in source/release assets.

# v1.2.1 ZIP directory safety patch

Core/native checks: 180 passed, retaining all v1.2.0 checks and adding two
forged-central-directory checks. A 4096-entry directory falsely declaring one
entry is rejected before ZipArchive allocation; measured thread allocations stay
below 512 KiB. All five original author ZIPs / 75 resources still pass.
Release solution: zero warnings/errors. No UI, native transaction or renderer
changes. Prior 85 desktop / 98 interface / 28 collection checks remain the
unchanged-code baseline; the patch does not claim newly repeated physical tests.
[Patch notes](docs/releases/1.2.1.md). v1.2.0 publication/history is preserved.

# CURSORIUM v1.2.0 validation update — 2026-10-13

Release solution and self-contained x64 app/setup: 0 warnings, 0 errors.
FROST and Obsidian: original 388 resource checks each; generated resources/manifests unchanged.
Core/native roundtrip: 178 passed (all original 66 retained, 112 added).
Desktop: 85 passed on the currently enumerated single 2560×1440 / 96-DPI display.
The desktop test source is unchanged: the second monitor's four instances behind
historical 89 are unavailable this run, not deleted or reported passed.
Original UI: 98 passed. Collection UI: 28 passed. Five original ZIPs / 75 CUR+ANI
pass production safe staging/native loading and original per-file SHA256 checks.
All five protected user files remain unchanged by existence/SHA256 comparison.

[Chinese delivery and limits](docs/CURSORIUM-v1.2-DELIVERY.md),
[public validation reports](docs/validation/v1.2/),
[actual WPF captures](docs/CURSORIUM-v1.2-SCREENSHOTS.md).

Actual Windows 11 build 26100 / 100% DPI input, native Apply/rollback/restore,
window management, compact layouts and animated resource lifecycle verified.
Resolution and 125%/150% matrix are equivalent logical layouts; no physical DPI
switch is claimed. Current dual-monitor, Windows 10, mixed DPI, protected desktop,
exclusive fullscreen, long stress, fresh install and uninstall are not established.

Existing v1.1 installation/data were copied into bounded TEMP isolation and upgraded
before touching production. Actual production upgrade then backed up installation,
data and native values; all 59 protected data/resources preserved, native registry
types/values unchanged, payload file hashes matched, registered version 1.2.0.
The absent desktop link was recreated from the correct Start-menu link; actual
startup and reopening use the installed process. Private backups stay untracked.

The records below remain historical evidence for their named versions.

---

# CURSORIUM / FROST: validation record

## CURSORIUM v1.1.0 — October 13, 2026

Current-version evidence: Windows 11 build 26100; SDK 10.0.103; .NET 8/WPF/Win32;
**two real displays (2560×1440 and 1920×1080), both 96 DPI**.

- Release solution and self-contained app/installer build: zero warnings/errors.
- FROST and Obsidian unchanged native resources: 388 checks each.
- Core/import/profile/native transactions: 66 passed.
- Original desktop suite on the two displays: 89 passed (88 original checks plus hidden-preview exclusion regression). Additional original count comes from
  existing per-display checks; the original functional paths were retained.
- Interface suite: 98 passed. Real slider arrow/Space toggle/F4 dropdown input after
  actual foreground activation; Escape popup isolation; draft/invalid/apply/cancel state;
  all 15 cursor preview roles; Saved Profile feedback; advanced views; persisted animation
  switch and OS gate; native/system-caption fallback; caption/resize hit tests;
  minimize/maximize/restore/close-to-tray/reopen; maximized controls inside work bounds;
  9 equivalent logical work-area cases across 5 pages, compact 850×520 and long names.
- Existing settings.json, studio.json, profiles.json, original-theme.json and interface.json
  preserved by existence/SHA256 comparison. Isolated native tests restored exact prior scheme.
- Same-size 1280×830 old/new and major pages captured from real WPF windows. Preview trails
  use actual local mouse samples. [Screenshot comparison](docs/CURSORIUM-v1.1-SCREENSHOTS.md).
- No changes to native services, desktop engine, overlay, painter, LivePreview or cursor assets.

Eight approximately four-second engine-only scenarios on 28 logical processors:

| Scenario | WPF invalidations/s | Working set MiB | CPU, one core % | GDI before/after |
|---|---:|---:|---:|---|
| Disabled | 0.0 | 151.4 | 9.36 | 33 / 33 |
| Stationary | 0.0 | 149.9 | 3.50 | 33 / 33 |
| Comet, 60 target | 53.7 | 167.4 | 13.65 | 33 / 33 |
| Minimal, 60 target | 52.9 | 157.2 | 6.24 | 33 / 33 |
| Neon, 60 target | 53.7 | 158.4 | 6.25 | 33 / 33 |
| Stardust, 60 target | 55.2 | 159.0 | 5.86 | 33 / 33 |
| Comet, 120 target | 68.9 | 160.6 | 8.58 | 33 / 33 |
| Power saving, 30 target | 29.5 | 160.7 | 3.90 | 33 / 33 |

Machine CPU is one-core CPU / 28. UI was hidden for benchmark. This includes runtime
initialization and per-monitor layered surface overhead, not GPU presentation FPS or
long-term leak/whole-studio memory evidence. USER remained 34–35; repeated native cursor
preview resource counts stayed bounded in the desktop suite.

The 1366×768/1920×1080/2560×1440 × 100/125/150 matrix adjusts available DIPs; monitor DPI
stays 96. **Physical 125%/150%/mixed DPI is unverified.** Windows 10, negative monitor
coordinates, physical hotplug, sleep/resume, secure/protected desktops, exclusive fullscreen,
genuine high-refresh presentation and long sessions remain unverified. Fresh install,
uninstall and upgrade-failure rollback were not run against the user's existing installation.

Raw successful evidence: `artifacts/interface-v1.1/verified/` (core, desktop, performance,
restart, data protection) and `artifacts/interface-v1.1/final-validated/interface/capture-report.json`.
The final regression rerun is under `artifacts/interface-v1.1/final-validated/`.
Sanitized evidence: [cursorium-v1.1-2026-10-13.json](docs/verification/cursorium-v1.1-2026-10-13.json).
First attempts with missing keyboard foreground and clipped maximized caption controls
failed, were corrected and rerun; failed attempts remain local and are not counted as passes.
[Delivery and limitations](docs/CURSORIUM-v1.1-DELIVERY.md).

Existing marked installation was actually upgraded with v1.1.0 (exit 0). First upgrade
matched 611 published files by SHA256 and preserved 58 existing data/resource files and
native cursor values/types. Start menu launch showed the new installed window; desktop
reopen retained its process. Registry version is 1.1.0. Full installation/data rollback
archives stay local. Documentation of this result was added after that run; final packaging
adds documentation, with no software changes. [Sanitized upgrade report](docs/verification/cursorium-v1.1-installed-upgrade-2026-10-13.json).


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
