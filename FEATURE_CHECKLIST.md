# Chummer port checklist

Status: active. `[x]` verified; `[~]` partial; `[ ]` missing. Keep rows short; record evidence in `PARITY_AUDIT.md`.

## Core priority

- [x] Foci and Stacked Foci: projection, legacy XML, lifecycle, limits, Karma, and bonuses.
- [~] Creation budget: common tracker exists; remaining mutations and house-rule limits need enforcement.
- [~] Bonus engine: vehicle context, essence/nuyen/free-quality/essence-multiplier, and ArmorMod B/I remain.
- [~] Calculations: ammo RC, special weapons, soft overrides, Mystic-Adept, and cyborg edges remain.
- [ ] Special operations: metatype, Mutant/Toxic Critter, Free Sprite, reapply improvements, BP availability.
- [ ] Nested containment: underbarrels and armor/cyberware/accessory/vehicle plugins.
- [ ] Clipboard and history operations.

## Product parity

- [~] Cloud conflict/newer revision: flows exist; server round-trip remains.
- [~] Native print: implementation exists; Linux, Windows, and macOS smoke tests remain.
- [~] Character creation: normal flow works; complete accounting and special commands remain.
- [~] Sourcebook filtering: normal pickers work; Suites and PACKS remain.
- [~] ConfirmDelete and ConfirmKarmaExpense: primary paths work; audit remaining destructive/costly paths.
- [ ] LocalisedUpdatesOnly and OmaeAutoLogin runtime decisions.

## Foundations

- [x] Save/load, principal item mutations, expenses, contacts, spells, spirits, lifestyles, grades, improvements, and calendar.
- [x] Main derived stats, Core data pickers, sourcebook options, protection/registration, creation backup, and PrintToFileFirst.
- [x] Every increment requires focused regression, full tests, project build, and XML round-trip where applicable.
