# Chummer Linux port plan

Status: active. Target: Avalonia on modern .NET for Linux, Windows, and macOS. Functional parity takes precedence over pixel-perfect WinForms matching.

## Status

- [x] Legacy cleanup: WCF/Omae/update paths modernized; REST API available.
- [x] Settings migration: `SettingsStore` replaces direct Registry use.
- [x] Mono hardening: compatibility baseline complete.
- [~] Core extraction: UI-agnostic factories and shared services are extracted; remaining parity work is tracked in `../PORTING_PLAN.md`.
- [~] Avalonia rewrite: main UI is incremental and depends on Core parity.
- [x] Linux packaging: `packaging/linux/build-appimage.sh` builds AppImage artifacts.

## Architecture

| Layer | Responsibility |
| --- | --- |
| `Chummer.Core` | UI-agnostic domain logic, persistence, cloud DTOs, authentication contracts. |
| WinForms | Legacy UI and adapter rendering during migration. |
| `Chummer.Avalonia` | Cross-platform UI driven by Core. |

## Migration rules

- Keep model factories free of `TreeNode`, `TreeView`, and `ContextMenuStrip`.
- Keep legacy UI adapters in WinForms; put new behavior in Core.
- Validate each increment with the legacy build, Core tests, and affected UI smoke test.
- Use `PORTING_PLAN.md`, `FEATURE_CHECKLIST.md`, and `PARITY_AUDIT.md` for feature status.

## Delivery

Ship self-contained AppImage artifacts for `linux-x64` and `linux-arm64`; bundle the runtime and Avalonia dependencies.
