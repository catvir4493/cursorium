# CURSORIUM v1.0 implementation audit and plan

Audit: 2026-10-13. Baseline: `6efcd1b`; clean worktree. Recovery branch:
`codex/rollback-frost-v1`. Implementation branch: `codex/cursorium-v1`.
Baseline Release build: zero warnings/errors on Windows 11 26100, SDK 10.0.103.

## Existing system

- .NET 8, WPF, small WinForms tray adapter, Win32 interop. No new UI framework needed.
- Core: original vector artwork and CUR/ANI generator, 15 native roles, bounded physical-coordinate trail history, settings, atomic JSON writer, registry backup/install/restore.
- App: one shared mouse sampler/render loop with a transparent, non-activating overlay per display; Raw Input does not disable legacy input. Fullscreen and input-desktop checks already exist.
- UI currently changes live desktop settings immediately and assumes one theme. Refactor these responsibilities into a draft and an application service.
- Existing installation, startup, mutex/IPC identities and LocalAppData path remain compatible. Preserve all original assets and the old backup format.
- An older installed app and an active original-theme backup exist on this machine. Automated verification uses a separate data root. Any native round trip must restore its exact pre-test snapshot and must not archive the user's original backup.

## Phases

1. Versioned models, file catalogs, validated paths, atomic durable state, profile repository.
2. Generalize the existing registry/SPI transaction; immutable installed resources, per-operation rollback and restart recovery; FROST plus a distinct authored Obsidian palette; safe role-based CUR/ANI import.
3. Shared Comet/Minimal/Neon/Stardust rendering, independent Off/Halo/Ring clicks with left/right identity, low-frequency sampling when paused/disabled.
4. Draft/applied/saved separation, apply/cancel/restore coordinator and profile CRUD. A profile selection updates the draft until Apply.
5. Three-column WPF shell, catalog-driven cards/selectors, local live preview using the same renderer, scrolling and compact widths for DPI/small work areas; tray/startup integration.
6. Native resource and transaction tests, real WPF controls and pass-through tests, restart/persistence tests, measured performance, self-contained Release packaging and documented gaps.

Each phase gets a successful build before the next phase. No cloud account, marketplace, injection or external scripts.

## Delivery status

Phases 1–5 implemented and built successfully. Phase 6 produced self-contained x64
portable/installer/theme/source packages, 66 core/native transaction checks and 84
published-app desktop checks on Windows 11. Each native theme also passed 388 resource
checks. Extended hardware/OS and fresh installer round trips remain explicitly unverified
in `CURSORIUM-DELIVERY.md` and `VALIDATION.md`.
