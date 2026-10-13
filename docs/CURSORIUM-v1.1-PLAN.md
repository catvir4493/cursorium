# CURSORIUM v1.1 interface audit and plan

Baseline: `68dc329`, clean main matching GitHub. Recovery branch:
`codex/rollback-cursorium-v1.0.0`; implementation branch: `codex/interface-v1.1`.

Read v1.0 delivery, architecture, validation, existing XAML/code-behind, preview,
application/tray lifetime, service models and desktop test entry points.

The stack remains .NET 8/WPF/Win32. All styling is currently window-local. The
three-column UI has generic white ComboBoxes/slider parts, a system light caption,
a fixed bottom library and an oversized passive cursor illustration. Draft isolation
already exists, but Apply is enabled even for unchanged settings. Profiles/native
transactions and the desktop engine remain intact.

## Implementation

A. Capture the old 1280x830 window on all five pages using an isolated draft. Create
   ResourceDictionary tokens, shared control templates, vector icon geometry and
   common focus/hover/pressed/disabled states. Build.
B. WindowChrome title bar with normal native drag/resize/maximize/minimize/close,
   explicit system dark-caption fallback, rebalanced responsive shell and status. Build.
C. Large interactive preview, role selector and precise draft-only message; wrap the
   catalog library directly below it. Build.
D. Summary + Cursor/Trail/Click tabs, primary controls, advanced expanders, custom
   toggles/sliders/color controls, recovery in the Cursor group and Settings. Build.
E. Small presentation state model, accurate Apply/Cancel, inline errors/success and
   saved-profile feedback; optional reduced-motion-aware UI microinteractions. Build.
F. Original functional checks, native window/keyboard/input tests, UI screenshot matrix,
   high-DPI layout emulation explicitly separated from physical DPI tests, new release
   build, data-preserving installed upgrade and GitHub v1.1.0 delivery.

No native theme manager or desktop renderer rewrite. No new large framework. Screenshots
show real WPF controls and sampled preview input; no decorative fake trails. User settings,
profiles and imported assets stay in their existing locations and formats.
