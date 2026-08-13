# Avalonia parity audit — 2026-08-13

This is an evidence-based scan of the current Avalonia/Core port against explicit legacy behavior
and the port's own source comments. It does **not** mark a feature complete merely because a dialog
or a Core method exists. Each item below has a concrete missing behavior or a conflicting checklist
claim and must remain visible in the main checklist until resolved.

## High-impact functional gaps

- **Character creation budget enforcement** — `CharacterSidebarViewModel` displays the creation
  budget, but creation mutations do not all consume/reject against that shared tracker. See
  `FEATURE_CHECKLIST.md` “Creation-mode BP/Karma budget tracker”.
- **Cloud conflict/newer-revision handling** — the checklist simultaneously calls the full cloud
  workflow done and says conflict/newer-revision handling is open. Treat the latter as authoritative
  until a round-trip conflict test proves it.
- **Bonus application / Manual Improvements** — `BonusApplier.cs:34-46` excludes vehicle-scoped
  bonuses, `enabletab`, `addattribute`, `essencemax`, `nuyenamt`, free-quality grants, and the
  cyberware-essence multiplier. Manual Improvements expose only a curated subset, not the legacy
  type set.
- **PACKS kits** — `CharacterFileService.cs:2375-2384` intentionally skips Vehicles, Martial
  Arts, Spirits, kit Armor Mods/nested Gear, Weapon Accessories/Mods, and Exotic Skills.
- **Combat calculations** — `CharacterFileService.cs:7731` explicitly excludes loaded-ammo recoil
  bonuses; `CharacterFileService.cs:8098-8102` excludes Skillsoft/Activesoft overrides, Mystic
  Adept MAG split, SwapSkillAttribute, Enhanced Articulation, and MetaRatingModifier.
- **Character output** — vehicle-mounted weapon Damage/AP/RC is missing from generated sheet XML;
  native OS printing still needs desktop smoke validation.

## Feature coverage that is deliberately partial

- **Quality swap** is remove/add only: no Karma delta, refund, or metatype-origin guard
  (`GeneralSectionTab.axaml.cs:159-163`).
- **Selectable category pickers** (`frmSelectSkillCategory`, `frmSelectSpellCategory`) are absent.
- **Custom Cyberware Suite/PACKS authoring** is absent because user-data storage has not been
  designed.
- **Foci / Stacked Foci** and the `AllowHigherStackedFoci` rule remain absent.
- **Cyberzombie conversion / frmDiceHits** remains absent.

## Audit policy

An item is `[x]` only if its legacy-visible behavior is present end to end and has a proportional
Core/UI test or smoke-test evidence. A feature with known skipped branches is `[~]`; its skipped
branches must remain listed in `FEATURE_CHECKLIST.md`, not only in source comments.
