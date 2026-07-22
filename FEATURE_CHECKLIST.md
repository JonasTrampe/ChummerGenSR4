# Feature checklist — Avalonia port vs. the full legacy Chummer feature set

There wasn't a granular checklist before this — `PORTING_PLAN.md` is a phased narrative plan and
`docs/LINUX_PORT_PLAN.md` is a high-level status doc from before most of the Phase 1/2 work in
this file existed. This is the actual feature-by-feature inventory, kept up to date as work
lands. Update the checkbox and status when a row changes; add a one-line note on any real
simplification so the gap is visible without re-reading commit history.

Legend: ✅ done · 🟡 partial (real but scoped down or read-only) · ❌ not started

## Character file I/O

- ✅ Open/save `.chum` files
- 🟡 **Anything added or edited in the UI actually persisting** — adding a Quality or a
  Karma/Nuyen history entry now mutates the loaded XML and survives save/reload; all other
  edits remain read-only or unwired.
- ✅ Multiple characters open in tabs at once
- ❌ Character creation flow (priority/point-buy system, `frmCreate` equivalent) — only three
  mockup dialogs (Settings Profile → Karma/GP → Metatype) chained together with no real logic
  behind any of them
- ❌ Cloud save/share (RunnersPoint API) wired into the UI — the API client, auth, and DTOs are
  ported to `Chummer.Core`, but nothing in Avalonia calls them (no share/cloud-open menu item)
- ✅ MRU / recently-opened files list — Dateimenü shows sticky + normal recent characters via
  MVVM-bound menu items backed by `GlobalOptions`; local paths reopen directly and stale entries
  are removed when open fails

## Character sheet tabs — display

- ✅ Allgemein (attributes, qualities tree, contacts, enemies)
- ✅ Fertigkeiten (skill groups, active skills with real dice pools, knowledge skills)
- ✅ Kampfkünste (martial arts + advantages + maneuvers)
- ✅ Adeptenkräfte
- ✅ Sprüche und Geister (spells by category, spirits)
- ✅ Initiation (grades list)
- ✅ Cyberware und Bioware
- ✅ Straßenausrüstung → Lebensstil, Panzerung (inkl. Mods/Sets), Waffen (inkl. Zubehör/Mods), Ausrüstung
- ❌ Straßenausrüstung → Haustiere und Begleiter (still a static mockup, no data model)
- 🟡 Fahrzeuge und Drohnen — vehicle list shows name+category only; no handling/pilot/armor/
  sensor/mod/weapon stats (`Chummer.Core` doesn't model Vehicle beyond that)
- 🟡 Charakter-Information — all fields load and are editable in the TextBoxes, but edits don't
  save (see the I/O gap above)
- ✅ Karma und Nuyen (expense history + real running-total charts)
- ✅ Kalender (read-only saved calendar weeks and notes; editing remains blocked on the shared
  write path)
- ✅ Notizen (read-only character-level notes; editing remains blocked on the shared write path)
- ✅ Verbessern / Improvements list (read-only list with type, target, value, source, and active
  status; editing remains blocked on the shared write path)

## Character sheet tabs — editing

- 🟡 **Add Quality, Spell, Gear, and Karma/Nuyen history entries** work end-to-end (UI → character XML →
  save/reload); selected Qualities, Spells, and root-level Gear entries can also be deleted. All other
  add/delete/edit operations remain unwired.

## Item picker dialogs (`frmSelectXxx` equivalents)

- 🟡 3 of ~41: `QualityDialog` and `SpellDialog` support their full selected-item flows.
  `GearDialog` now reads `gear.xml`, shows its key rules data, and adds/removes root-level gear
  items at their default rating; quantity and containers are not yet supported.
- ❌ The other ~38 (Cyberware, Armor, Weapon, Vehicle, Skill, MartialArt, Metamagic, Lifestyle,
  CritterPower, ContactConnection, ...) don't exist yet

## Derived stats / calculations

- ✅ Essence
- ✅ Condition monitor size (Physical/Stun)
- ✅ Armor encumbrance penalty
- ✅ Skill dice pools, including skill-rating augmentation display
- ✅ Composure, Judge Intentions, Lift and Carry, Memory
- ✅ Initiative, Initiative Passes
- ✅ Astral Initiative
- ✅ Matrix Initiative, Matrix Initiative Passes (human, Technomancer, A.I./technocritter/
  protosapient, and active-Commlink-Response paths all covered)
- ✅ Career Karma / career Nuyen totals
- ❌ Sprite Matrix Initiative (needs metatype-minimum data from `metatypes.xml`, which is
  bundled in `Chummer.Core/data` but not loaded/parsed by Core yet)
- ❌ Swim/Fly movement rates
- ✅ Worn armor rating (Ballistische/Stoßpanzerung sidebar rows) — uses the highest equipped
  armor value plus BallisticArmor/ImpactArmor Improvements, with a source tooltip
- ❌ Live wound modifier / current condition-monitor-filled-boxes penalty (`WoundModifiers` today
  only reflects Improvements, not how much damage is actually marked — see the derived-stats
  commit history for why)
- ❌ Damage resistance dice pool, "Schadenswiderstandswürfelpool" sidebar row
- ❌ Edge tracking ("X von Y verbleibend")
- ❌ Costs: adept power point cost, attribute karma-cost curves, cyberware/bioware essence cost,
  gear/weapon/vehicle availability & cost calculations
- ❌ House-rule (`CharacterOptions`) awareness in any calculation — everything above computes the
  vanilla-rules result regardless of what the character's settings profile says (e.g.
  `IgnoreArmorEncumbrance`, `EnforceMaximumSkillRatingModifier`, `CapSkillRating` are all ignored)

## Output / tooling

- ❌ Print / character sheet rendering (XSLT transform) — `SheetPreviewDialog` is a fully static
  HTML mockup; nothing in Core produces the character export XML the real sheets consume
- ❌ PDF sourcebook page linking
- ❌ Dice roller
- ❌ Update checker (arguably a non-goal for a Linux/AppImage distribution model rather than a
  gap — worth an explicit decision rather than silent omission)

## Settings / options

- 🟡 `GlobalOptions`/`CharacterOptions` ported to `Chummer.Core` and used internally (house
  rules aren't consumed by calculations yet, per above) — but there's no options/settings **UI**
  at all; `SettingsProfileDialog` is a static mockup
- ❌ Language selection UI (the `LanguageManager` engine works and is used for exactly one
  string today — `Title_CareerMode` — nothing else in the UI is localized through it)

## Drag-and-drop / interaction niceties

- 🟡 Gear tree reordering/reparenting (done, MVVM-bound) — not extended to the Cyberware/
  Weapons/Armor trees, which don't support it at all

## Platform / packaging

- ❌ AppImage or other Linux distribution packaging (`docs/LINUX_PORT_PLAN.md` Phase 5)
- 🟡 Startup/runtime crashes have been fixed as found (data path layout, asset casing) but there's
  no smoke-test automation beyond manual `dotnet build` + kill-timeout runs done ad hoc in this
  session

## Test infrastructure

- 🟡 `Chummer.Tests` has real coverage for everything `CharacterFileService` computes, growing
  with each derived-stat port, but `dotnet test`/`vstest` itself doesn't run in this sandbox (an
  `Avalonia.Base.dll` resolution failure unrelated to the test code) — every verification in this
  session used a throwaway console harness instead. Needs someone on a clean machine to confirm
  `dotnet test` actually works and wire it into CI.
