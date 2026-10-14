# CURSORIUM project instructions

## User-authorized publishing policy

The user explicitly requested on 2026-10-13: publish CURSORIUM to a separate GitHub
repository, and automatically upload every completed iteration without waiting for
another request. This authorization persists for this project.

- Repository: `https://github.com/catvir4493/cursorium`; default branch: `main`.
- After finishing an iteration and its relevant checks, create a new commit and push
  the completed work to `origin`. Do not stop at a local commit or ask again for routine
  upload approval. Respect a later explicit instruction to keep work local or use a PR.
- Verify the pushed commit against the live remote before reporting completion.
- Preserve published history: no force pushes, amended published commits, rebases of
  published history, or moving published release tags. Roll back with a new revert.
- The original FROST repository is the `frost-legacy` remote. Do not push CURSORIUM updates
  there. The local `frost-legacy-main` and `codex/rollback-frost-v1` retain the original baseline.
- Versioned application deliveries also get a GitHub Release with installer, portable ZIP,
  theme ZIP, source ZIP and SHA256 checksums. Use new `cursorium-v<version>` tags: the existing
  local `v1.0.0` belongs to FROST and must not be repointed or bulk-pushed to this repository.
- `scripts/publish-github.ps1` pushes/verifies main. Add `-Release -Version <version>
  -NotesPath docs/releases/<version>.md` for a prepared application release.

## Verification and data protection

- Keep the .NET 10/WPF/Win32 target adopted by the v1.5 preview and the .NET 8 v1.4 rollback baseline, plus FROST resources. Preserve cursor backups,
  per-operation rollback, physical coordinates, input pass-through and non-activation.
- Build code changes with `dotnet build Frost.sln -c Release`. Run checks appropriate to
  the changed behavior; native resources use `tests/Frost.Verify`. UI/engine/native
  transactions use the isolated verification scripts when relevant.
- Avoid rerunning broad desktop/input tests for documentation-only publishing changes.
- Do not claim unperformed hardware/OS tests passed. Keep limitations in `VALIDATION.md`.
- Native/system-changing verification must use isolated data and restore its original
  system cursor snapshot. Never replace the user's active backup with a test backup.
- Do not commit `artifacts/`, credentials, LocalAppData, raw machine-specific registry
  snapshots, installation rollback archives or the user's profiles/settings.
- Release assets belong in GitHub Releases, not the source tree. Keep source and release
  contents aligned. Publish only after local checks and checksum verification.
- Unresolved hardware, long-duration or active-performance gates require an explicitly
  labeled prerelease (`publish-github.ps1 -Release -Prerelease`); never claim Steam Ready
  from short or simulated checks. Publishing refetches public asset bytes and verifies hashes.
- Read credentials through Git Credential Manager in process memory. Never print or save
  authentication tokens. Credential unavailability is a real blocker to report, not a
  reason to invent a successful upload.
