# Core parity plan

Status: active. This plan is ordered by Core dependency and user-visible correctness.

## Working rules

- Implement Core before Avalonia-only presentation.
- Preserve legacy XML shapes; avoid name-based identity where legacy saves provide IDs.
- Land a Core test and save/reload test with every behavior.
- Update `FEATURE_CHECKLIST.md` and `PARITY_AUDIT.md` in the same commit.

## Phase 0 — Foci / Stacked Foci `[x]`

Normal and stacked focus lifecycle is complete: projections, legacy XML compatibility, limits, stack/unstack, Karma, and equipped bonuses.

## Phase 1 — Creation and bonus correctness

1. Enforce remaining creation spends/refunds through the common budget.
2. Add missing bonus consumers: vehicle context, essence/nuyen/free-quality/essence multiplier, ArmorMod B/I.
3. Port remaining calculation edges: ammo RC, special weapons, soft overrides, Mystic-Adept and cyborg cases.

## Phase 2 — Legacy Core operations

1. Extract Special commands: metatype/critter/Free Sprite/Reapply/BP availability.
2. Add nested containment and item transfer operations.
3. Add clipboard/history Core data operations.

## Completion gate

No phase is complete until all its checklist rows are `[x]`, full tests pass, and relevant legacy XML round-trips.
