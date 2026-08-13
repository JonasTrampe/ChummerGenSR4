# Chummer Core parity audit

Status: active, last reviewed 2026-08-13. `[x]` verified; `[~]` partial; `[ ]` missing. Compare legacy `clsCharacter`, `clsEquipment`, `clsUnique`, `clsImprovement`, and host commands with `Chummer.Core`.

## Persisted collections

| Area | Status | Evidence or gap |
| --- | --- | --- |
| Contacts, spells, powers, spirits, techprograms, martial arts, armor, weapons, cyberware, qualities, lifestyles, gear, vehicles, grades, improvements, expenses, calendar | `[x]` | Core projection and mutation paths exist. |
| Foci | `[x]` | Projection, bond/unbond, MAG limits, Karma, equipped bonuses, and save/reload coverage. |
| Stacked Foci | `[x]` | Projection, save/reload, stack/unstack, Force rule, binding Karma, and equipped bonuses. |

## Remaining Core work

| Priority | Area | Status | Required outcome |
| --- | --- | --- | --- |
| P0 | Creation budget | `[~]` | Every creation mutation debits/refunds one enforced pool. |
| P1 | Bonus consumers | `[~]` | Vehicle context, essence/nuyen/free-quality/essence-multiplier, ArmorMod B/I. |
| P1 | Calculations | `[~]` | Ammo RC, special weapons, soft overrides, Mystic-Adept, cyborg edges. |
| P1 | Special commands | `[ ]` | Metatype, critter, Free Sprite, reapply, BP availability operations. |
| P1 | Containment | `[ ]` | Underbarrels and armor, cyberware, accessory, and vehicle plugins. |
| P2 | Clipboard/history | `[ ]` | Character-safe copy/paste and historical snapshots. |

## Validation

Each Core change needs a legacy-shape XML test, save/reload test, and full `dotnet test` run.
