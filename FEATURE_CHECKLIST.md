# Chummer parity checklist

Status legend: `[x]` verified; `[~]` partial; `[ ]` missing. Keep each row short; details belong in `PARITY_AUDIT.md`.

## Core priority

- [~] Foci and Stacked Foci: normal Focus is complete; Stacked Focus projection and round-trip work, but stack/unstack and lifecycle remain.
- [~] Creation budget: common tracker exists; remaining create mutations and house-rule limits must enforce it.
- [~] Bonus engine: common types work; vehicle context, essence/nuyen/free-quality/essence-multiplier and ArmorMod B/I remain.
- [~] Calculations: main pools work; ammo RC, special weapons, soft overrides, Mystic-Adept and cyborg edges remain.
- [ ] Special Core operations: Change Metatype, Mutant/Toxic Critter, Free Sprite, Reapply Improvements, BP availability.
- [ ] Nested containment: underbarrels; armor/cyberware/accessory/vehicle plugin flows.
- [ ] Clipboard and history Core operations.

## Verified partial product gaps

- [~] Cloud conflict/newer-revision: flows exist; server round-trip verification remains.
- [~] Native print: implementation exists; Linux/Windows/macOS smoke tests remain.
- [~] Character creation: normal flow works; complete creation accounting and special commands remain.
- [~] Sourcebook filtering: normal pickers work; Suites and PACKS remain.
- [~] ConfirmDelete and ConfirmKarmaExpense: main actions covered; remaining destructive/costly paths need audit.
- [ ] LocalisedUpdatesOnly and OmaeAutoLogin runtime decisions.

## Done foundations

- [x] Core save/load, gear/weapon/armor/cyberware/vehicle mutation paths, expenses, contacts, spells, spirits, lifestyles, grades, improvements, and calendar.
- [x] Main derived stats, Core rules-data pickers, sourcebook options, automatic program protection/registration, creation backup, and PrintToFileFirst.
- [x] Full regression suite and Avalonia build are required for every port increment.

## Validation

Every change: focused regression, full `dotnet test`, project build, XML round-trip where data changes.
