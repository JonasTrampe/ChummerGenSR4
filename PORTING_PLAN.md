# Chummer cross-platform port plan

Status: active. This is the single source of truth for Linux/Avalonia migration and Core legacy parity. `[x]` verified; `[~]` partial; `[ ]` missing.

## Platform status

- [x] Legacy cleanup: WCF/Omae/update paths modernized; REST API available.
- [x] Settings migration: `SettingsStore` replaces direct Registry use.
- [x] Mono hardening: compatibility baseline complete.
- [~] Core extraction: UI-agnostic factories and shared services are extracted; parity work remains below.
- [~] Avalonia rewrite: incremental, driven by Core parity.
- [x] Linux packaging: `packaging/linux/build-appimage.sh` builds AppImage artifacts.

## Architecture

| Layer | Responsibility |
| --- | --- |
| `Chummer.Core` | UI-agnostic domain logic, persistence, cloud DTOs, authentication contracts. |
| WinForms | Legacy UI and adapter rendering during migration. |
| `Chummer.Avalonia` | Cross-platform UI driven by Core. |

## Verified Core parity

| Area | Status | Evidence |
| --- | --- | --- |
| Principal collections: contacts, spells, powers, spirits, techprograms, martial arts, armor, weapons, cyberware, qualities, lifestyles, gear, vehicles, grades, improvements, expenses, calendar | `[x]` | Core projection and mutation paths exist. |
| Foci | `[x]` | Projection, bond/unbond, MAG limits, Karma, equipped bonuses, and save/reload coverage. |
| Stacked Foci | `[x]` | Projection, save/reload, stack/unstack, Force rule, binding Karma, and equipped bonuses. |
| Save/load, main derived stats, Core data pickers, sourcebook options, protection/registration, creation backup, PrintToFileFirst | `[x]` | Regression-covered Core behavior. |

## Remaining parity work

| Priority | Area | Status | Required outcome |
| --- | --- | --- | --- |
| P0 | Creation budget | `[~]` | Attributes (incl. SpecialAttributeKarmaLimit for MAG/RES/EDG), skills (incl. BreakSkillGroupsInCreateMode), contacts, qualities (incl. positive/negative quality limits), spells, complex forms, Nuyen, martial arts, maneuvers (with capacity cap), and spirits/sprites debit/refund one enforced pool; continue auditing the remaining creation mutators and limits. |
| P1 | Bonus consumers | `[~]` | ArmorMod B/I, essence, nuyen (nuyenamt), free-quality (addqualities), and essence-multiplier (essencemax) are wired. Vehicle-context stat bonuses (flyspeed/speed/accel/handling/response) remain - need per-vehicle Improvement scoping this port's Vehicle model doesn't have yet. |
| P1 | Calculations | `[~]` | Recoil (incl. loaded-ammo RC), Cyborg Essence override, Special Weapons range-based skill lookup, and Mystic-Adept EssencePenalty-adjusted split are covered; "soft overrides" has no identifiable referent in the legacy codebase (no matching mechanism found; treat as resolved/moot unless a concrete target surfaces). |
| P1 | Special commands | `[~]` | Reapply known improvements, Free Sprite conversion, transactional metatype replacement, and BP/availability configuration are covered; critter creation remains. |
| P1 | Containment | `[x]` | Underbarrels plus armor, cyberware, weapon-accessory, and vehicle gear/plugin trees persist and mutate. |
| P2 | Clipboard/history | `[~]` | Immutable snapshots and typed XML copy/paste exist; per-item validation and host wiring remain. |
| Product | Cloud conflict/newer revision | `[~]` | Flows exist; server round-trip remains. |
| Product | Native print | `[~]` | Implementation exists; Linux, Windows, and macOS smoke tests remain. |
| Product | Sourcebook filtering | `[~]` | Normal pickers work; Suites and PACKS remain. |
| Product | ConfirmDelete/ConfirmKarmaExpense | `[~]` | Primary paths work; audit remaining costly/destructive paths. |
| Product | LocalisedUpdatesOnly/OmaeAutoLogin | `[ ]` | Runtime decisions remain. |

## Delivery sequence

1. Complete creation accounting, missing bonus consumers, and calculation edges.
2. Extract remaining special commands and nested containment operations.
3. Add clipboard/history data operations.
4. Complete Avalonia presentation and platform smoke tests.
5. Ship self-contained AppImages for `linux-x64` and `linux-arm64`.

## Migration and validation rules

- Keep model factories free of `TreeNode`, `TreeView`, and `ContextMenuStrip`.
- Keep legacy UI adapters in WinForms; put new behavior in Core before Avalonia presentation.
- Preserve legacy XML and stable IDs where present.
- Each Core increment needs focused regression, legacy-shape XML and save/reload coverage, full `dotnet test`, project build, and affected UI smoke test.
- Completion requires every parity row verified and functional—not pixel-perfect—cross-platform behavior.
