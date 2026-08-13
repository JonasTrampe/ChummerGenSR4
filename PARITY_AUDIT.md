# Avalonia/Core parity audit — 2026-08-13 (full legacy surface scan)

This is the authoritative static comparison of the legacy WinForms application
(`Chummer/code/frm*.cs`, `clsCharacter.cs`, `clsEquipment.cs`, `clsImprovement.cs`) with
`Chummer.Core` and `Chummer.Avalonia`. It replaces the earlier marker-only scan. A dialog,
persisted option, or read-only tree counts only as evidence of partial coverage; `[x]` in the
feature checklist requires its visible legacy behavior and a Core test or smoke-test evidence.

## Method and scope

- Enumerated every legacy `frm*` workflow and both host forms' File/Edit/Special/context-menu
  handlers, then searched the Core mutation APIs and Avalonia commands for its equivalent.
- Compared every persisted behavior option with its legacy read site, not merely its Options UI
  binding.
- Reviewed all explicit Core port-boundary comments and export paths. This is a source-level
  audit: it cannot prove platform dialogs or cloud servers without runtime smoke tests.
- **Core re-scan (2026-08-13):** compared every top-level collection written by
  `clsCharacter.Save` with `CharacterDocument` projections and mutation APIs. Contacts, spells,
  powers, spirits, techprograms, martial arts, armor, weapons, cyberware, qualities, lifestyles,
  gear, vehicles, initiation grades, improvements, expenses, and calendar all have a Core path.
  `<foci>` and `<stackedfoci>` are the two persisted legacy collections with no
  `CharacterDocument` projection or mutation path. The scan also rechecked all explicit Core
  "not ported" boundaries; they are summarized below and belong to the Core backlog, not UI-only
  cleanup.

## Surface map

| Legacy area | Port status | Evidence / remaining work |
| --- | --- | --- |
| File, tabbed documents, save, recent files | partial | Open/save/Save As/recent/multi-tab, dirty prompts, and Window-menu navigation work. Character history remains absent. |
| Character creation / career transition | partial | Normal Karma/BP creation, budget categories, and backup-on-career work. Complete budget enforcement, creation clipboard, metatype change, critter conversions, Free Sprite conversion, and BP availability override remain absent. |
| Core character tabs | partial | All primary tabs render and common add/remove operations work. Persisted per-item notes now cover roots and installed equipment, including Spirits/Sprites. Nested add-as-plugin flows, Foci, and special conversion flows remain absent. |
| Item pickers | partial | Common rules-data pickers are present. Category pickers, several generic legacy selection modes, and complete PACKS expansion are not. |
| Improvements and rules calculations | partial | Common bonus types and displayed calculations work. The omitted bonus nodes and documented skill/weapon/vehicle edge cases still change legitimate legacy characters. |
| Vehicles / drones | partial | Root vehicle, mod, gear, weapon, location, damage and mount workflows work. Sensor/cyberware/nexus/plugin nesting, underbarrels, item notes/names, and vehicle-scoped Improvements do not. |
| Output / print / export | partial | XSLT preview, HTML/PDF export, multiple-character and Squad Manager export work. PrintToFileFirst is implemented. Native-print smoke testing and mounted-weapon sheet fields remain incomplete. |
| Cloud | partial | Documents/folders/revisions/share plus login, newer-revision and conflict choice flows are present. What remains is an end-to-end server round-trip verification pass. |
| Settings / localization | partial | Profiles and almost all controls persist. Several behavior options are only persisted; sourcebook filtering does not cover Suites/PACKS. |

## Newly surfaced functional gaps

These were absent or too narrowly described in the prior checklist and are now tracked there.

1. **File/document behavior:** separate Save As, unsaved-change confirmation, Window-menu document
   navigation, and character history (`frmHistory`) have no Avalonia equivalent.
2. **Creation special commands:** Change Metatype, Mutant Critter, Toxic Critter, Cyberzombie,
   Convert to Free Sprite, Reapply Improvements, BP availability override, and legacy copy/paste
   are all callable workflows in `frmCreate`/`frmCareer` with no port equivalent.
3. **Item editing and containment:** legacy context menus support renaming and per-item notes across
   weapons, armor, gear, cyberware, vehicle components, qualities, spells, powers, lifestyles,
   martial arts and improvements. The port only exposes character/contact/calendar notes. It also
   lacks several valid nested operations: add gear as a plugin to armor/cyberware/accessories,
   vehicle sensor/cyberware/Nexus/plugin flows, and weapon underbarrels.
4. **Creation/career accounting:** `FinalizeCreation` intentionally has no validation gate; it
   neither applies the CreateBackupOnCareer setting nor covers all legacy creation/career costs.
5. **Output data:** the exporter omits or simplifies fields some shipped sheets consume, including
   vehicle-mounted weapon Damage/AP/RC and item-level notes; this makes “full print XML” inaccurate.

6. **Core persisted-collection gap:** legacy saves retain Focus and Stacked Focus state in
   `<foci>` and `<stackedfoci>`, including gear linkage, bond state, stacking composition and
   Karma/improvement implications. `Focus.cs` exists as an unused value object, but
   `CharacterDocument` neither projects nor mutates either collection. This is the first Core
   subsystem to port because it also unlocks `AllowHigherStackedFoci`.

7. **Core rule-boundary gap:** the explicit Core omissions are actionable parity work: vehicle-
   scoped bonus effects; `essencemax`, `nuyenamt`, free-quality and cyberware-essence-multiplier
   bonuses; ArmorMod B/I effects; loaded-ammo recoil; special-weapon range matching; Skillsoft/
   Activesoft overrides; Mystic-Adept/SwapSkillAttribute/Enhanced Articulation/MetaRatingModifier
   edge cases; and Cyborg Essence. Each needs a consumer plus a fixture-based regression test.

## Existing partial areas confirmed by the scan

- **Character creation budget enforcement** — display-only tracker; creation mutations do not all
  consume/reject against one shared budget.
- **Cloud conflict/newer revision handling** — Avalonia contains the login, newer-revision and
  conflict decision flows. The former “missing flow” finding was corrected during this audit;
  treat cloud as incomplete only until a server round-trip test proves those decisions persist.
- **Bonus application / Manual Improvements** — `BonusApplier` omits `enabletab`, `addattribute`,
  vehicle stat effects, `essencemax`, `nuyenamt`, free-quality grants and the cyberware-essence
  multiplier. Manual Improvements expose only 13 legacy types.
- **PACKS kits** — Vehicle, Martial Art, Spirit, Lifestyle, nested Armor Gear/Mods, Weapon
  Accessory/Mods and Exotic Skill expansion is intentionally skipped.
- **Calculations** — loaded-ammo recoil, special-weapon range disambiguation, Skillsoft/Activesoft
  overrides, Mystic-Adept split, SwapSkillAttribute, Enhanced Articulation, MetaRatingModifier,
  special metatype initiative cap, cyborg Essence and some armor-mod effects are still absent.
- **Quality swap** — remove/add only; no Karma delta/refund or metatype-origin guard.
- **Foci / Stacked Foci**, Cyberzombie's `frmDiceHits`, custom Cyberware Suite/PACKS authoring,
  and the Skill/Spell Category pickers are absent.

## Behavior-option audit

| Option | Actual current status |
| --- | --- |
| `SingleDiceRoller`, `DatesIncludeTime`, `StartupFullscreen` | implemented runtime behavior |
| `AutomaticUpdate`, `SuppressCloudUnreachableWarning` | implemented, subject to cloud/update smoke testing |
| `BookEnabled` | picker filtering works for ordinary rules-data pickers; Suites and PACKS do not filter |
| `ConfirmDelete` | main Avalonia item/list deletion actions honor it through one shared dialog; non-item destructive actions still need audit |
| `ConfirmKarmaExpense` | persisted only; legacy has confirmations at many costly commands |
| `CreateBackupOnCareer` | implemented: the creation shell writes an atomic pre-career snapshot beside the saved character and stops transition on write failure |
| `AutomaticCopyProtection`, `AutomaticRegistration` | implemented in root and nested gear acquisition; eligible Unwired Matrix/soft items gain the zero-cost legacy children |
| `LocalisedUpdatesOnly` | persisted only; GitHub-release updater does not fetch localized payloads |
| `OmaeAutoLogin` | obsolete legacy service setting is persisted only; requires an explicit replacement/retirement decision, not silent exclusion |
| `PrintToFileFirst` | native printing uses a temporary HTML file and cleans it up after the platform web dialog closes |

## Audit policy

A known skipped legacy branch remains `[~]` or `[ ]` in `FEATURE_CHECKLIST.md`, never `[x]`.
Every future parity change must update that checklist and this audit when it changes a listed
boundary. Runtime-only features require platform smoke evidence before moving to `[x]`.
