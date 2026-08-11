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
- 🟡 Character creation flow (Karma/BP point-buy system, `frmCreate` equivalent) — this was more
  complete than previously documented here: Settings Profile → Karma/GP → Metatype (now with a
  Magie/Resonanz choice: Magician/Adept/Technomancer/none) produces a real seeded character via
  `NewCharacterFactory`, and every attribute/skill/skill-group/starting-Nuyen row already has
  working Create-mode point-spending (`RaiseXCreate`/`LowerXCreate`, toggled by `IsCreateMode`
  throughout the UI) instead of the career-mode Karma cost. What was actually missing end-to-end -
  a way to leave creation mode at all - is now wired: a "Charakter fertigstellen" button
  (`CharacterSidebar`, create-mode only) calls the new `FinalizeCreation()`, which flips `Created`
  and switches every already-built `IsCreateMode` binding over to normal career-mode Karma
  spending. Verified end-to-end with a scratch harness: create Adept → raise BOD/Pistolen via
  Create-mode Karma costs → finalize → raise AGI via the career-mode Karma formula, all against
  the same character. Not a true priority-table system (Karma/BP only, no A-E priority letters),
  no starting-Lifestyle-Nuyen dice roll, and Mystic Adept (both Adept+Magician) isn't offered.
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
- ✅ Komplexe Formen (Complex Forms) / Kritter-Kräfte (Critter Powers) — both are displayed in the
  Sprüche und Geister tab, below Geister, and both now support add (via a `programs.xml`/
  `critterpowers.xml` picker dialog) and delete (by saved guid), always shown.
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
  from the existing gear picker, persisted, charged, and removed. Direct onboard weapons can likewise
  be selected, persisted, charged, and removed. Vehicle locations can be created and deleted and
  are displayed in the detail pane; assigning existing onboard gear to locations remains open.
  Drones, weapon-mount and mod eligibility/slot validation, and rules-data-derived totals remain unported.
- ✅ Charakter-Information — text fields and profile counters load, edit, and save back into the
  character file
- ✅ Karma und Nuyen (expense history + real running-total charts)
- ✅ Kalender (saved weeks, persistent notes editing, adding weeks, and shifting the calendar start date)
- ✅ Notizen
- ✅ Verbessern / Improvements list (read-only list with type, target, value, source, and active
  status; editing remains blocked on the shared write path)

## Character sheet tabs — editing

- 🟡 **Add Quality, Spell, Gear, Spirit/Sprite, Martial Art, Martial Art Maneuver, Adept Power,
  Metamagic, Complex Form, Critter Power, and Karma/Nuyen history entries** work end-to-end
  (UI → character XML → save/reload); selected Qualities, Spells, root-level Gear, Spirits/Sprites,
  Martial Arts/Maneuvers, Adept Powers, Metamagics, Complex Forms, and Critter Powers can also be
  deleted, and Karma/Nuyen history entries can be edited in place (amount/reason/date).
  Character-Information's portrait (mugshot) also loads/changes/clears. Manual Improvement
  add/edit/delete and most other operations remain unwired.

## Item picker dialogs (`frmSelectXxx` equivalents)

- 🟡 17 of ~41: selected-item flows exist for Quality, Spell, Gear, Cyberware/Bioware, Armor,
  Weapon, Vehicle, Vehicle Mod, Lifestyle, exotic Skills, Martial Art, Martial Art Maneuver, Adept
  Power, Metamagic, CritterPower, ComplexForm, and ContactConnection. The implementations remain
  deliberately scoped (for example, no advanced vehicle-mod eligibility validation, no advanced
  lifestyle construction, and Metamagic/Adept Power/CritterPower/ComplexForm additions don't apply
  their rules-data Improvement bonuses).
- ✅ ContactConnection (Group Network rating: Membership/Area of Influence/Magical/Matrix Resources
  + group name/colour/free flag) — `ContactGroupDialog` wired to `UpdateContactGroup`.
- ❌ The remaining pickers (Skill beyond exotic skills — active/knowledge skills come from a fixed
  list plus freeform knowledge-skill entries, so no picker is actually needed there — and others)
  don't exist yet.

## Derived stats / calculations

- ✅ Essence
- ✅ Condition monitor size (Physical/Stun), live damage-box display, and persisted heal/damage controls
- ✅ Armor encumbrance penalty
- ✅ Skill dice pools, including skill-rating augmentation display
- ✅ Weapon dice pools (category→Active Skill mapping, Smartgun System bonus, specialization
  match), shown in the Waffen tab's detail pane and included in the print sheet. Not ported:
  accessory/mod dice pool bonuses (no field for it in this port's saved data yet) and loaded-ammo
  pool bonuses.
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
- ✅ Adept power point cost — per-power point-per-level (with Way-of-the-Adept/Geas discounts)
  and a total-pool "used vs. available" figure (MAG or the Mystic Adept MAG-Adept split, plus
  AdeptPowerPoints Improvements), with a source-breakdown tooltip
- ✅ Attribute karma-cost curve (`ComputeAttributeKarmaCostToIncrease`, house-rule aware via
  `AlternateMetatypeAttributeKarma`)
- ✅ Cyberware/bioware essence cost — the item-selection dialog applies the chosen Grade's
  Essence multiplier live (`CyberwareDialogViewModel.FinalEssence`); already-installed items read
  the `ess` value the write path saved at add-time
- ✅ Gear/weapon/armor/cyberware availability & cost calculations — `CharacterTreeItemData`
  evaluates `Rating`-formula `cost`/`avail` strings (as saved verbatim by the write path) and sums
  cost across children; now surfaced in the Gear/Armor/Waffen/Cyberware detail panes (previously
  computed in Core but not shown anywhere in the UI). Vehicle mod/vehicle-level cost/avail
  totals and eligibility/slot validation remain unported.
- ✅ Skill defaulting at Rating 0 — a Skill that allows defaulting (per skills.xml, cross-referenced
  by name; Knowledge/Language Skills always allow it) rolls Attribute - 1 instead of a flat 0 pool,
  respecting the `SkillDefaultingIncludesModifiers` house rule. Verified against a real save: every
  magic/technical Rating-0 skill (Spellcasting, Hacking, Medicine, ...) correctly stays at pool 0,
  every defaultable physical/social one (Etiquette, Escape Artist, Diving, ...) gets a real pool.
- 🟡 House-rule (`CharacterOptions`) awareness in calculations — armor encumbrance
  (`IgnoreArmorEncumbrance`/`AlternateArmorEncumbrance`/`NoSingleArmorEncumbrance`), attribute
  karma cost (`AlternateMetatypeAttributeKarma`), and skill dice pools
  (`EnforceMaximumSkillRatingModifier`/`CapSkillRating`/`SkillDefaultingIncludesModifiers`) are now
  house-rule aware; most Karma/BP costs (contacts, skills, skill groups) already read their rates
  from the settings profile. Most other Improvement-driven house rules remain unported.

## Output / tooling

- 🟡 Print / character sheet rendering (XSLT transform) — `CharacterSheetExporter` builds the
  print-XML (Info, Attributes, derived stats, Skills, Contacts, Qualities, Spells, Adept Powers,
  Complex Forms, Critter Powers, Martial Arts, Lifestyles, Cyberware/Bioware, Gear/Commlinks,
  Armor, Weapons (incl. dice pool), Vehicles (mods/gear/weapons), Karma/Nuyen expenses) and
  transforms it through a real `.xsl` file via `XslCompiledTransform`. `SheetPreviewDialog` renders
  the actual open character (with a template picker covering every non-"Base" sheet under
  `data/sheets`, defaulting to the Options-configured `DefaultCharacterSheet`) instead of static
  mockup content. Every section the shipped sheets read is now covered; vehicle-mounted Weapons
  don't carry damage/AP/RC in this port's saved tree data, so those three fields render blank for
  them specifically. Fixed two real bugs found while building/verifying this:
  - `CharacterTreeItemData.HasCommlinkStats` treated every saved Gear item as a Commlink, since
    legacy always writes `<response>0</response>` etc. on non-Commlink items and the check only
    tested for non-empty rather than positive.
  - Sheets that `xsl:include` a shared base stylesheet (`Shadowrun 4.xsl` and both "Grouped
    Skills" variants, not just `Text-Only.xsl`) failed to load at all - .NET's default
    `XmlReader.Create` resolver refuses to follow `xsl:include` as an "external URI". Fixed by
    loading via `XslCompiledTransform.Load(path, XsltSettings.TrustedXslt, new XmlUrlResolver())`.
  - The dialog's HTML-to-plain-text conversion (Avalonia has no built-in HTML renderer) left every
    sheet's embedded `&lt;style&gt;`/`&lt;script&gt;` block contents visible as raw CSS/JS text -
    never caught earlier because verification only inspected the raw HTML output, not what the
    dialog's TextBox actually displays.
- 🟡 PDF sourcebook page linking — ported clsCommon.cs's OpenPDF as `PdfLinkService`; the PDF
  reader path and argument style were already wired in Options (General tab) but per-book
  paths/page-offsets weren't editable anywhere, so added those to the Sourcebooks list there. A
  new `SourceLink` control (plain text when unconfigured, a clickable link once a reader + that
  book's path are set) replaces the raw "Quelle:" text in the Spell/Power/MartialArt/Metamagic
  picker dialogs. Not yet wired into the character-tab detail panes' own "Quelle:" labels (Allgemein,
  Kampfkünste, Verbessern, Initiation, and every other tab still show plain text there) or the
  ~10 other picker dialogs built earlier this session.
- ✅ Dice roller — ported frmDiceRoller.cs's roll/hit/glitch logic (Standard/Large/Really Large
  methods, Rule of 6, Cinematic Gameplay, Rushed Job, Gremlins rating, Threshold) into a
  standalone, character-independent `Zubehör → Würfeln...` dialog
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
