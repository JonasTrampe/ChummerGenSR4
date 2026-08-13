# Core parity audit

Status: 2026-08-13. Source comparison: legacy `clsCharacter`, `clsEquipment`, `clsUnique`,
`clsImprovement`, and host-form commands against `Chummer.Core`.

## Rules

- `[x]` verified Core behavior with regression coverage.
- `[~]` implemented subset or verified gap; never treat as complete.
- `[ ]` no Core equivalent.

## Persisted collections

| Legacy collection | Core status | Gap |
| --- | --- | --- |
| Contacts, spells, powers, spirits, techprograms, martial arts, armor, weapons, cyberware, qualities, lifestyles, gear, vehicles, grades, improvements, expenses, calendar | `[x]` | Core projection and mutation path exist. |
| `foci` | `[ ]` | No projection, bond lifecycle, limit/cost, or linked-Gear handling. |
| `stackedfoci` | `[ ]` | No projection, stack/unstack, bonus lifecycle, or save path. |

## Verified Core gaps

| Priority | Area | Status | Required outcome |
| --- | --- | --- | --- |
| P0 | Foci / Stacked Foci | `[ ]` | Read/write, bond/unbond, MAG limits, Karma, improvements, stacking, round-trip. |
| P0 | Creation budget | `[~]` | Every create mutation debits/refunds one enforced pool. |
| P1 | Bonus consumers | `[~]` | Vehicle context, essence/nuyen/free-quality/essence-multiplier, ArmorMod B/I. |
| P1 | Calculations | `[~]` | Ammo RC, special weapons, soft overrides, Mystic-Adept and cyborg edge cases. |
| P1 | Special commands | `[ ]` | Metatype/critter/Free Sprite/Reapply/BP-availability Core operations. |
| P1 | Containment | `[ ]` | Underbarrels and plugin paths for armor, cyberware, accessories, vehicles. |
| P2 | Clipboard/history | `[ ]` | Character-safe copy/paste and historical snapshots. |

## Validation

Every Core change needs an inline legacy-shape XML test, save/reload test, and complete `dotnet test` run.
