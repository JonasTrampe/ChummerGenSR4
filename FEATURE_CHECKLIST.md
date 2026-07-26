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
  Karma/Nuyen history entry now mutates the loaded XML and survives save/reload; character
  profile/general fields such as alias, notes, biography text, street-cred values, and Nuyen
  also write back to the loaded XML. Many other edits remain read-only or unwired.
- ✅ Multiple characters open in tabs at once
- ❌ Character creation flow (priority/point-buy system, `frmCreate` equivalent) — only three
  mockup dialogs (Settings Profile → Karma/GP → Metatype) chained together with no real logic
  behind any of them
- 🟡 Cloud save/share (RunnersPoint API) wired into the UI — Avalonia now has a Cloud Documents
  menu entry and dialog with OAuth/API-token login, folder tree, shared-documents toggle,
  push/download/archive/unarchive, metadata editing, revisions, and document-to-folder drag/drop.
  Remaining gaps are mostly parity/polish items around conflict/newer-revision flows and broader
  legacy-Chummer UX parity.
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
- ✅ Straßenausrüstung → Lebensstil (Auswahl, Hinzufügen/Löschen und Monatskosten), Panzerung
  (inkl. gespeicherter Mods und persistenter Sets, die angelegt, zugeordnet und aufgelöst werden
  können), Waffen (inkl. Zubehör/Mods und persistenter Standorte), Ausrüstung
- 🟡 Straßenausrüstung → Haustiere und Begleiter — saved `Pet` contact entries can be edited,
  added/removed, and linked to a companion `.chum` file, with their name, notes, and free status.
- 🟡 Fahrzeuge und Drohnen — saved handling/pilot/body/armor/sensor/device, availability/cost/slots,
  and the installed mods/onboard gear/weapons tree are displayed. A filterable vehicle picker can
  add/delete root vehicles and deduct their cost. Rules-data vehicle modifications can be selected
  (including rating), persisted, charged with Body-aware formulas, and removed. Root vehicles support
  persistent physical-damage tracking with add/repair controls. Direct onboard gear can be selected
  from the existing gear picker, persisted, charged, and removed. Editing onboard weapons, locations,
  drones, mod eligibility/slot validation, and rules-data-derived totals remain unported.
- ✅ Charakter-Information — text fields and profile counters load, edit, and save back into the
  character file
- ✅ Karma und Nuyen (expense history + real running-total charts)
- ✅ Kalender (saved weeks, persistent notes editing, adding weeks, and shifting the calendar start date)
- ✅ Notizen
- ✅ Verbessern / Improvements list (read-only list with type, target, value, source, and active
  status; editing remains blocked on the shared write path)

## Character sheet tabs — editing

- 🟡 **Add Quality, Spell, Gear, and Karma/Nuyen history entries** work end-to-end (UI → character XML →
  save/reload); selected Qualities, Spells, and root-level Gear entries can also be deleted. All other
  add/delete/edit operations remain unwired.

## Item picker dialogs (`frmSelectXxx` equivalents)

- 🟡 10 of ~41: selected-item flows exist for Quality, Spell, Gear, Cyberware/Bioware, Armor,
  Weapon, Vehicle, Vehicle Mod, Lifestyle, and exotic Skills. The implementations remain deliberately scoped
  (for example, no advanced vehicle-mod eligibility validation and no advanced lifestyle construction).
- ❌ The remaining pickers (Skill beyond exotic skills, MartialArt, Metamagic, CritterPower,
  ContactConnection, and others) don't exist yet.

## Derived stats / calculations

- ✅ Essence
- ✅ Condition monitor size (Physical/Stun), live damage-box display, and persisted heal/damage controls
- ✅ Armor encumbrance penalty
- ✅ Skill dice pools, including skill-rating augmentation display
- ✅ Composure, Judge Intentions, Lift and Carry, Memory
- ✅ Initiative, Initiative Passes
- ✅ Astral Initiative
- ✅ Matrix Initiative, Matrix Initiative Passes (human, Technomancer, A.I./technocritter/
  protosapient, and active-Commlink-Response paths all covered)
- ✅ Career Karma / career Nuyen totals
- ✅ Sprite Matrix Initiative from the persisted INI metatype minimum, including wound modifiers
- ✅ Walk/Swim/Fly movement rates, including MovementPercent/SwimPercent/FlyPercent and FlySpeed Improvements
- ✅ Worn armor rating (Ballistische/Stoßpanzerung sidebar rows) — uses the highest equipped
  armor value plus BallisticArmor/ImpactArmor Improvements, with a source tooltip
- ✅ Live wound modifier from physical/stun condition-monitor boxes, with ConditionMonitor Improvement adjustments
- ✅ Damage resistance dice pool sidebar row, including Improvement contributions and tooltip
- ✅ Edge tracking ("X von Y verbleibend"), including spend/regain controls persisted as legacy `EdgeUse` Improvements
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

- ✅ Options/settings UI: settings profiles, sourcebooks, build/karma/BP values, optional and
  house rules, global update/PDF/cloud options, and persistence through `SettingsStore`
- 🟡 Language selection UI exists in Options and reloads the language catalog; most Avalonia UI
  strings still remain hard-coded and are therefore not translated yet.

## Drag-and-drop / interaction niceties

- 🟡 Gear tree reordering/reparenting (done, MVVM-bound) — not extended to the Cyberware/
  Weapons/Armor trees, which don't support it at all

## Platform / packaging

- ❌ AppImage or other Linux distribution packaging (`docs/LINUX_PORT_PLAN.md` Phase 5)
- 🟡 Startup/runtime crashes have been fixed as found (data path layout, asset casing) but there's
  no smoke-test automation beyond manual `dotnet build` + kill-timeout runs done ad hoc in this
  session

## Test infrastructure

- ✅ `Chummer.Tests` has real coverage for `CharacterFileService`; targeted `dotnet test` runs
  successfully in this workspace. The project still emits a known `System.Net.Http` MSBuild
  conflict warning while building tests.
