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
| P0 | Creation budget | `[x]` | Attributes (incl. SpecialAttributeKarmaLimit for MAG/RES/EDG), skills (incl. BreakSkillGroupsInCreateMode), contacts, qualities (incl. positive/negative quality limits), spells, complex forms, Nuyen, martial arts, maneuvers (with capacity cap), and spirits/sprites debit/refund one enforced pool. Closed the one remaining gap found by a full audit: Spirit/Sprite additions now enforce the CHA-based bound-count cap at creation (`frmCreate.cs`'s `cmdAddSpirit_Click` - career mode has no such cap, matching legacy). Priority/SumToTen build method remains entirely unimplemented in Core, but that's a separate, much larger architectural item (an alternate `CharacterBuildMethod`), not a creation-budget-mutator gap. |
| P1 | Bonus consumers | `[x]` | ArmorMod B/I, essence, nuyen (nuyenamt), free-quality (addqualities), essence-multiplier (essencemax), and Vehicle Mod speed/accel/body/handling/armor bonuses are all wired. |
| P1 | Calculations | `[~]` | Recoil (incl. loaded-ammo RC), Cyborg Essence override, Special Weapons range-based skill lookup, and Mystic-Adept EssencePenalty-adjusted split are covered; "soft overrides" has no identifiable referent in the legacy codebase (no matching mechanism found; treat as resolved/moot unless a concrete target surfaces). |
| P1 | Special commands | `[x]` | Reapply known improvements, Free Sprite conversion, transactional metatype replacement, BP/availability configuration, and critter creation (Core CreateCritterCharacter plus the Avalonia "New Critter" menu item/picker, including the RW-sourcebook warning and Force input for Force-based critters) are all covered. |
| P1 | Containment | `[x]` | Underbarrels plus armor, cyberware, weapon-accessory, and vehicle gear/plugin trees persist and mutate. |
| P2 | Clipboard/history | `[x]` | Immutable snapshots, root-element-validated typed XML copy/paste, and Avalonia host wiring (Copy/Paste buttons across the Gear/Armor/Weapon/Lifestyle, Cyberware/Bioware, and Vehicle tabs, backed by a single process-wide CharacterClipboard.Instance) are all covered. |
| Product | Cloud conflict/newer revision | `[~]` | Flows exist; server round-trip remains. |
| Product | Native print | `[~]` | Linux smoke test passed manually (real printer + Virtual_PDF_Printer via CUPS): character-sheet preview now renders via Ultralight (an off-screen HTML/CSS engine, not embedded in a native OS window - Avalonia's own WebView control failed to render at all when embedded as a child control on NVIDIA/GBM, and CefGlue.Avalonia has no release compatible with this project's Avalonia 12.1.0), driven by real Avalonia toolbar buttons; printing itself reuses a transient Avalonia.Controls.WebView `NativeWebDialog` purely to trigger the OS's native print dialog, which was independently proven to work end-to-end. Needs `AppCoreMethods.SetPlatformFontLoader()` + `Platform.SetDefaultFileSystem = true` + a single process-wide `Renderer` (creating more than one after the first crashes the process natively - not a catchable exception) - see `Chummer.Avalonia/src/Program.cs`'s `InitializeUltralight`. Also carries a `WEBKIT_DISABLE_DMABUF_RENDERER=1` re-exec workaround for the print dialog's own WebKitGTK engine (`Failed to create GBM buffer` on this Mesa/DRM driver combo otherwise). Windows and macOS smoke tests remain. |
| Product | Sourcebook filtering | `[x]` | Normal pickers work. PACKS kits and Cyberware/Bioware Suites now also skip any item whose resolved rules-data node has a disabled `<source>`, reusing the same `IsBookEnabled` check a normal picker runs - legacy itself never filtered these two (frmSelectPACKSKit.cs/frmSelectCyberwareSuite.cs have no such check), so this is a product improvement over legacy behavior, applied for consistency. Kit/Suite *names* themselves aren't hidden (packs.xml entries carry no `<source>` at the container level to filter on), only their resolved item contents are. |
| Product | ConfirmDelete/ConfirmKarmaExpense | `[x]` | Delete confirmations cover every collection (fixed the one gap found: Burn Edge now shows its own unconditional confirm, matching legacy). ConfirmKarmaExpense now covers Attributes/Skills/Initiation, career-mode Quality purchase/buy-off, career-mode Spell learning (flat KarmaSpell), Complex Form learning (category/house-rule-dependent cost, plus the expense-log/undo entry it was previously missing), and Martial Art/Maneuver purchase (5*KarmaQuality / KarmaManeuver - legacy shows no confirm dialog for these two, but this port confirms them anyway for UI consistency with the other three). Focus/Stacked Focus binding already charged Karma correctly in Core; Stacked Focus gained a `GetStackedFocusBindingKarmaCost` preview method for parity with the regular-Focus preview. The Avalonia Gear tab now has Bind/Unbind Focus, Bind/Unbind Stacked Focus, and Create Stacked Focus buttons wired to these Core methods (Karma confirm dialog on binding) - the last one via a new checkbox-list `MultiSelectionDialog` since the single-selection Gear tree can't drive `CreateStackedFocus`'s 2+-item requirement directly. Every previously-flagged gap in this row is now closed. |
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
