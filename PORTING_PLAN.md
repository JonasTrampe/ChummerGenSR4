# Chummer Core parity plan

Status: active. Implement Core before Avalonia presentation; preserve legacy XML; add behavior and save/reload tests; update the checklist and audit in the same commit.

## Completed

- [x] Foci and Stacked Foci: projection, legacy compatibility, limits, stack/unstack, Karma, and equipped bonuses.

## Next

1. Complete creation spend/refund enforcement and house-rule limits.
2. Add missing bonus consumers and calculation edge cases.
3. Extract legacy special commands: metatype, critter, Free Sprite, reapply, BP availability.
4. Add nested containment and item-transfer operations.
5. Add clipboard and history data operations.

## Completion gate

The plan completes when every checklist row is `[x]`, full tests pass, and relevant legacy XML round-trips.
