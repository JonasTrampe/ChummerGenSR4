# Feature checklist — Avalonia port vs. the full legacy Chummer feature set

**Goal: full feature parity with legacy WinForms Chummer.** This port is no longer scoped as an
MVP — every 🟡 row below is a real, currently-accepted gap, not a permanent simplification. When
a row's note says something was "deliberately scoped," "out of scope for this pass," or "not
ported yet," treat that as backlog to close, not a decision to leave alone. The **Backlog toward
full parity** section right below is the actionable summary; the feature-by-feature sections
after it are the detailed record of what's done, what's partial, and exactly why.

There wasn't a granular checklist before this — `PORTING_PLAN.md` is a phased narrative plan and
`docs/LINUX_PORT_PLAN.md` is a high-level status doc from before most of the Phase 1/2 work in
this file existed. This is the actual feature-by-feature inventory, kept up to date as work
lands. Update the checkbox and status when a row changes; add a one-line note on any real
simplification so the gap is visible without re-reading commit history. When closing a backlog
item, remove or check off its line in both the Backlog section and its detail row.

Legend: ✅ done · 🟡 partial (real but scoped down or read-only) · ❌ not started

## Backlog toward full parity

Concrete, actionable gaps pulled from the detail sections below, so this list can be worked
through directly without re-reading prose. Grouped by area; see the linked section for full
context on each.

**Bonus-application engine** (see § Bonus-application engine)
- [ ] Expand `BonusApplier`/`ApplyBonus` beyond the current tier-1 node-type set (frequency-ranked
  against this port's own data) toward covering legacy's full ~109-branch `CreateImprovements`
  if-chain.
- [ ] Manual Improvement *add* (`frmCreateImprovement.cs` — a ~50-type catalog with per-type
  dynamic fields and sub-picker dialogs). Delete already works for `Custom`-sourced entries; add
  does not exist at all.
- [x] Give Critter Powers a real Rating input — done. `CritterPowerDialog` now shows a Rating
  spinner (`NumericUpDown`, min 1) when the selected power's rules-data sets
  `<rating>yes</rating>` (ported from frmSelectCritterPower.cs's `nudCritterPowerRating`), and
  `AddCritterPower` scales the power's `<bonus>` by that Rating instead of always using 1;
  `CharacterCritterPowerData.Rating` persists the choice. Verified against real data: Armor
  (Ballistic) at Rating 4 grants +4 Ballistic Armor; Fear (no `<rating>` flag) ignores a passed-in
  Rating and stays at "0".
- [ ] `selectskill`/`selectattribute`/`selecttext` edge cases not yet modeled: exotic-skill-with-
  specialization matching in `selectskill`, and `precedence` stacking rules beyond what
  `ImprovementManager` already handles.

**Character sheet tabs**
- [ ] Fahrzeuge: track which specific "Weapon Mount"/"Mechanical Arm" mod each vehicle weapon
  occupies (currently a simplified count check — one weapon per mount, but not which mount).
- [ ] Weapon dice pools: accessory/mod dice pool bonuses and loaded-ammo pool bonuses aren't
  factored in yet (no field for either in this port's saved data).
- [ ] A real spellcasting dice pool per spell (Spellcasting skill + MAG) — separate from the
  Drain/Fading resistance pool, which is already done.

**House rules**
- [ ] `AllowExceedAttributeBp` — needs a new persisted "starting BP total" field (Bp is currently
  only ever tracked as a shrinking remaining pool), not just gating existing logic.

**Item picker dialogs**
- [ ] Enumerate and port the remaining `frmSelectXxx` pickers beyond the 19 of ~41 already ported
  (Skill-beyond-exotic doesn't need one — see detail row). No concrete list exists yet; the first
  step is auditing legacy's `frmSelectXxx` files against what's ported here.

**Output / tooling**
- [ ] Real PDF export / native cross-platform "Drucken" (currently HTML export only — no PDF
  library or print backend referenced anywhere in this port yet).
- [x] Update checker — done. Ported from legacy's own already-Linux-adapted `frmUpdate.cs` (see
  commit "Remove dead WCF Omae feature and legacy update checker (Phase 0)"): `UpdateChecker`
  (`Chummer.Core`) checks GitHub's Releases API and compares the tag against the running version;
  `UpdateDialog` shows the result and opens the release page in the browser (no self-replacing
  exe, same as legacy's own adaptation). Wired into a new Hilfe → "Nach Updates suchen..." menu
  item (always shows a result) and a startup check gated by the existing `AutomaticUpdate` option
  (silent - only shows a dialog when an update is actually found).

**Settings / i18n**
- [ ] Translate the Avalonia UI — most strings are still hard-coded despite the language-selection
  UI and catalog reload already existing.

**Interaction niceties**
- [ ] Extend drag/drop reordering/reparenting to the Cyberware/Weapons/Armor trees (currently only
  the Gear tree supports it).

**Platform / packaging**
- [ ] AppImage or other Linux distribution packaging (`docs/LINUX_PORT_PLAN.md` Phase 5).
- [ ] Automated smoke-test coverage — currently manual `dotnet build` + kill-timeout runs only.

**Cloud save/share**
- [ ] Conflict/newer-revision handling and broader UX parity with legacy's cloud flows.

## Character file I/O

- ✅ Open/save `.chum` files
- 🟡 **Anything added or edited in the UI actually persisting** — this has grown far past its
  original scope; by now most editable-looking controls across every tab genuinely write back to
  the loaded XML. A follow-up pass over every `SelectedItem`/`SelectedIndex`/`SelectedValue`
  binding in the whole Avalonia project (not just TextBox/CheckBox/NumericUpDown, which an earlier
  pass already covered) found 5 more missing `Mode=TwoWay` - the same class of bug the
  Improvements tab's dead selection binding turned out to be. Three were silent no-ops (selecting
  a different Aktiver Kommlink never actually called `SetActiveCommlink`; selecting a different
  Kalenderwoche or Lebensstil left the previous one targeted by "Notizen bearbeiten"/"Löschen");
  one silently discarded edits (changing a Knowledge Skill's category dropdown never saved). The
  fifth was worse: `LifestyleDialog`'s picker `Selected` binding lacked it too, and since
  `SelectedLifestyle` (what `OnOk` checks before allowing the dialog to close) reads straight from
  that property, **the entire Lifestyle picker was unusable end to end** - clicking an option
  never registered, so "Hinzufügen" could never succeed. All five fixed defensively also added
  `Mode=TwoWay` to a few more `IsChecked` bindings found in the same sweep (Contacts' "Gratis",
  "Zeige nur Kommlinks", KarmaGpDialog's "Ignoriere Charaktererschaffungsregeln") even without
  concrete proof CheckBox.IsChecked has the same default-binding-mode gap ComboBox/ListBox
  SelectedItem turned out to have - the fix is free either way. Also found and closed a real
  missing feature while in this area: Ausrüstung had no named locations at all despite an
  unwired "Ort hinzufügen" button already sitting in the XAML (see the Straßenausrüstung row
  above). What's still genuinely unwired is narrower and tracked under its own rows now: manual
  Improvement *add* (Character sheet tabs → editing), Drones/weapon-mount subsystems (Fahrzeuge
  und Drohnen), and Language/i18n across the UI (Settings / options).
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
  Finalizing creation now prompts for the starting-Lifestyle-Nuyen dice roll, ported from
  `frmCreate.cs`'s `ConfirmSaveCreatedCharacter`/`frmLifestyleNuyen.cs`: `GetLifestyleNuyenRollInfo`
  auto-adds a Street Lifestyle if none exists, picks the highest-multiplier owned Lifestyle for its
  dice/multiplier, and computes the "+1 per 100 leftover Nuyen, capped at 3x dice" bonus; the new
  `LifestyleNuyenDialog` lets the player manually enter their roll (the app has never auto-rolled
  dice, matching legacy's `NumericUpDown`-based entry), and `FinalizeCreationWithLifestyleNuyenRoll`
  applies `(roll + bonus) * multiplier` as starting Nuyen before finalizing. Mystic Adept is now
  offered in the metatype dialog's
  magic-type picker (sets both `adept` and `magician`); the General tab's MAG-split field is
  editable and calls the new `SetMysticAdeptMagicianMagSplit`, which writes `magsplitmagician`/
  `magsplitadept` (clamped to `[0, MAG]`), ported from `frmCreate.cs`'s
  `nudMysticAdeptMAGMagician_ValueChanged`.
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
- ✅ Sprüche und Geister (spells by category, spirits). The detail pane was entirely static mockup
  content (fake "Indirekt, Elementar"/"Kampfzauber"/"9" Würfelpool text bound to nothing) despite
  `CharacterSpellData` already carrying every field it needed - now shows the real selected
  spell's Category/Type/Range/Damage/Duration/Entzugsformel/Quelle. Tradition/Stream selection and
  a real Drain-resistance/Fading-resistance pool are now tracked too: `CharacterDocument.Tradition`/
  `Stream` persist the player's choice (a `ComboBox` populated from traditions.xml/streams.xml,
  shown above the spell tree only for Magicians/Technomancers respectively), and
  `DrainResistance`/`FadingResistance` (ported from clsCharacter.cs) resolve the chosen
  Tradition/Stream's two-attribute `<drain>` formula (e.g. Hermetic's `WIL + LOG`) plus any
  DrainResistance/FadingResistance Improvements, reusing the same `SumAttributesWithImprovements`
  helper Composure/Judge Intentions/etc. already use. A real spellcasting dice pool (Spellcasting
  skill + MAG, shown per-spell) is still not computed - that's a different, not-yet-ported number
  from the Drain/Fading pool covered here.
- ✅ Komplexe Formen (Complex Forms) / Kritter-Kräfte (Critter Powers) — both are displayed in the
  Sprüche und Geister tab, below Geister, and both now support add (via a `programs.xml`/
  `critterpowers.xml` picker dialog) and delete (by saved guid), always shown.
- ✅ Initiation (grades list, and raising the Initiate/Submersion Grade now works too - ported
  from frmCareer.cs's cmdImproveInitiation_Click, including the Group/Ordeal -20% cost discounts
  and the MAG/RES attribute cap. Also now ports the MAG/RES-boosting Improvement (replaced
  wholesale each raise, matching legacy's RemoveImprovements+CreateImprovement pair) and the
  Metamagic Improvement refresh: any owned Metamagic whose rules-data `<bonus>` XML references
  "Rating" gets its Improvements rebuilt at the new Grade as the Rating - none of this port's
  shipped `metamagic.xml` entries actually use "Rating" today, so this is verified against the
  guard condition (an unaffected real Metamagic) plus the MAG-boosting Improvement itself, not an
  end-to-end Rating-scaled Metamagic example)
- ✅ Cyberware und Bioware
- ✅ Straßenausrüstung → Lebensstil (Auswahl, Hinzufügen/Löschen und Monatskosten), Panzerung
  (inkl. gespeicherter Mods und persistenter Sets, die angelegt, zugeordnet und aufgelöst werden
  können), Waffen (inkl. Zubehör/Mods, die jetzt über eigene Picker hinzugefügt/gelöscht werden
  können, und persistenter Standorte), Ausrüstung (now with its own named locations too, ported
  from frmCareer.cs's cmdAddLocation_Click - found the "Ort hinzufügen" button was already in the
  XAML but wired to nothing; `CharacterFileService.GearLocations`/`AddGearLocation`/
  `RemoveGearLocation`/`SetGearLocation` mirror the existing WeaponLocations pattern, root-level
  only, same as legacy)
- ✅ Straßenausrüstung → Haustiere und Begleiter — saved `Pet` contact entries can be edited,
  added/removed, and linked to a companion `.chum` file, with their name, notes, and free status.
  Linking peeks the companion file's Metatype/Metavariant and shows it (ported from
  PetControl.cs's lblMetatype, which does the same file-open-just-to-read-this trick). Checked
  PetControl.Designer.cs field-by-field against this: the remaining two context-menu actions
  (open the linked character in a new tab, clear the link without deleting the Pet) are now wired
  too - "Charakter öffnen" and "Verknüpfung entfernen" (the latter just calls the existing
  `UpdateContactFile` with empty paths, no new Core method needed). Nothing legacy exposes for a
  Pet is left unported.
- 🟡 Fahrzeuge und Drohnen — saved handling/pilot/body/armor/sensor/device, availability/cost/slots,
  and the installed mods/onboard gear/weapons tree are displayed. A filterable vehicle picker can
  add/delete root vehicles and deduct their cost. Rules-data vehicle modifications can be selected
  (including rating), persisted, charged with Body-aware formulas, and removed. Root vehicles support
  persistent physical-damage tracking with add/repair controls. Direct onboard gear can be selected
  from the existing gear picker, persisted, charged, and removed. Direct onboard weapons can likewise
  be selected, persisted, charged, and removed. Vehicle locations can be created and deleted, are
  displayed in the detail pane, and existing onboard gear can be assigned to (or cleared from) a
  location via a "Zuweisen" button (mirrors the existing Weapon-location assignment pattern).
  Mod slot capacity is enforced (`AddVehicleMod` rejects a mod whose evaluated Slots would exceed
  `SlotsRemaining`), the Sensor rating can show a calculated value from the onboard sensor suite
  instead of the saved one (`UseCalculatedVehicleSensorRatings`), and a computed total-cost figure
  (`CharacterVehicleData.TotalCost` - the vehicle's own cost plus installed non-included mods,
  included mods' own attached weapons/gear, and direct onboard gear/weapons, ported from
  clsEquipment.cs's Vehicle.TotalCost) is now shown alongside the base cost. "Drones" turn out not
  to be a separate gap either: checked clsEquipment.cs and there's no distinct Drone class anywhere
  in legacy - a Drone is just a Vehicle whose category happens to be "Drones: Micro"/"Drones:
  Small"/etc. in vehicles.xml, and this port's vehicle picker/add/edit path is already
  category-agnostic, so Drones already work end to end through the exact same code as cars.
  Weapon-mount eligibility is now enforced too - researched frmCareer.cs's
  tsVehicleAddWeaponWeapon_Click and found legacy actually requires selecting a specific installed
  "Weapon Mount"/"Mechanical Arm" VehicleMod before it'll let you add a vehicle weapon at all (and
  nests the new weapon under that specific mod node). `AddVehicleWeapon` now checks the vehicle has
  more installed mount-type mods than it already has direct weapons - simplified from legacy's
  per-mount nesting to a simple count check (still one weapon per mount, just not tracking which
  mount each occupies), since this port's existing "direct onboard weapon" UI already attaches at
  the vehicle root rather than under a specific mod. Mod
  "class eligibility" (a mod's `<limit>` field, e.g.
  "Groundcraft Only") turns out not to be a real gap: checked frmSelectVehicleMod.cs and legacy
  itself never validates it either, it's purely informational text next to the mod name - already
  shown that way here too (`VehicleModOptionViewModel.Limit`).
- ✅ Charakter-Information — text fields and profile counters load, edit, and save back into the
  character file
- ✅ Karma und Nuyen (expense history + real running-total charts)
- ✅ Kalender (saved weeks, persistent notes editing, adding weeks, and shifting the calendar start date)
- ✅ Notizen
- 🟡 Verbessern / Improvements list (type, target, value, source, active status). "Löschen" now
  works for `Custom`-sourced entries (matches legacy's own cmdDeleteImprovement_Click, which only
  ever lets the user delete manually-created Improvements - everything else is a side effect of
  some other owned item, e.g. a Quality or Cyberware, and gets removed by removing that item
  instead). Also fixed a real bug found while wiring this: the ListBox's SelectedItem binding
  wasn't Mode=TwoWay, so selecting a different row never actually updated the detail pane after
  the initial load. Manual Improvement *add* (`frmCreateImprovement.cs` - a ~50-type catalog with
  per-type dynamic fields and sub-picker dialogs, applied through a bonus-XML interpreter this port
  doesn't have) remains unported.

## Character sheet tabs — editing

- 🟡 **Add Quality, Spell, Gear, Spirit/Sprite, Martial Art, Martial Art Maneuver, Adept Power,
  Metamagic, Complex Form, Critter Power, Weapon Accessory, Weapon Mod, and Karma/Nuyen history
  entries** work end-to-end (UI → character XML → save/reload); selected Qualities, Spells,
  root-level Gear, Spirits/Sprites, Martial Arts/Maneuvers, Adept Powers, Metamagics, Complex
  Forms, Critter Powers, and Weapon Accessories/Mods can also be deleted, and Karma/Nuyen history
  entries can be edited in place (amount/reason/date). Character-Information's portrait (mugshot)
  also loads/changes/clears. Custom-sourced Improvements can now be deleted too (see the Verbessern
  row above). Allgemein's "Gabe austauschen" button (found already sitting in the XAML with no
  Click handler) now works too - ported from frmCareer.cs's cmdSwapQuality_Click as a plain
  remove+add, since this port's Quality model never tracked BP/cost at all (AddQuality doesn't
  charge Karma for a normal add either), so legacy's Karma-cost-delta charge/refund and its
  Metatype-origin-cannot-be-swapped guard aren't ported - every owned Quality is swappable here.
  Manual Improvement *add* and most other operations remain unwired.

## Item picker dialogs (`frmSelectXxx` equivalents)

- 🟡 19 of ~41: selected-item flows exist for Quality, Spell, Gear, Cyberware/Bioware, Armor,
  Weapon, Weapon Accessory, Weapon Mod, Vehicle, Vehicle Mod, Lifestyle, exotic Skills, Martial
  Art, Martial Art Maneuver, Adept Power, Metamagic, CritterPower, ComplexForm, and
  ContactConnection (the last of these isn't a standard "pick an item" flow - it's
  `ContactGroupDialog`'s Group Network rating calculator: Membership/Area of Influence/Magical/
  Matrix Resources + group name/colour/free flag, wired to `UpdateContactGroup`). The
  implementations remain deliberately scoped (for example, no advanced lifestyle construction).
  Quality/Adept Power/Metamagic/Cyberware/Bioware additions now apply their rules-data
  `<bonus>` Improvements (CritterPower/ComplexForm additions still don't - see the
  bonus-application engine note below). Weapon Accessory/Mod additions now validate mount-slot
  eligibility, ported from
  `frmCareer.cs`'s `tsWeaponAddAccessory_Click`/`tsWeaponAddModification_Click`: accessories are
  rejected unless the weapon's `weapons.xml` entry allows accessories and lists a matching
  `<accessorymounts><mount>`, and mods are rejected if the weapon disallows mods, or - under the
  EnforceCapacity house rule - if installed mods' slots plus the new one would exceed the fixed
  6-slot cap every weapon has (`clsEquipment.cs`'s `Weapon.SlotsRemaining` hardcodes this as a
  constant, not a per-weapon data field). Neither check enforces exclusivity between multiple
  accessories/mods sharing the same mount, matching legacy.
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
- ✅ Edge tracking ("X von Y verbleibend"), including spend/regain controls persisted as legacy
  `EdgeUse` Improvements, and "Burn Edge" (Allgemein's EDG row delete-icon button, found already
  sitting in the XAML with no Click handler) - ported from frmCareer.cs's cmdBurnEdge_Click,
  permanently lowering the EDG attribute's own base value by 1
- ✅ Adept power point cost — per-power point-per-level (with Way-of-the-Adept/Geas discounts)
  and a total-pool "used vs. available" figure (MAG or the Mystic Adept MAG-Adept split, plus
  AdeptPowerPoints Improvements), with a source-breakdown tooltip
- ✅ Attribute karma-cost curve (`ComputeAttributeKarmaCostToIncrease`, house-rule aware via
  `AlternateMetatypeAttributeKarma`)
- ✅ Cyberware/bioware essence cost — the item-selection dialog applies the chosen Grade's
  Essence multiplier live (`CyberwareDialogViewModel.FinalEssence`), plus an optional house-rule-
  gated Essence discount %; already-installed items read the `ess` value the write path saved at
  add-time
- ✅ Gear/weapon/armor/cyberware availability & cost calculations — `CharacterTreeItemData`
  evaluates `Rating`-formula `cost`/`avail` strings (as saved verbatim by the write path) and sums
  cost across children; now surfaced in the Gear/Armor/Waffen/Cyberware detail panes (previously
  computed in Core but not shown anywhere in the UI). Vehicle mod slot capacity and a computed
  vehicle+mods total cost are both handled now too - see the Fahrzeuge und Drohnen row above.
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
  from the settings profile. `EnforceCapacity` is now honored too - `AddChildGear` rejects nesting
  gear that would exceed the parent's remaining capacity (same simplified, no-brackets capacity
  model `CharacterTreeItemData.CapacityRemaining` already used for display) unless the house rule
  is off. `AllowSkillRegrouping` is now honored too - while auditing it, found the ported skill
  group model had no concept of a "broken" group at all: `RaiseSkillGroup`/`RaiseSkillGroupCreate`/
  `SetSkillGroupRating` would silently overwrite every member skill's rating (and any Karma/BP
  already spent reaching it, unrefunded) whenever the group was raised, even if member skills had
  already been raised individually and diverged from each other or from the group's own stale
  rating. `CanRaiseSkillGroupAsAWhole` now blocks the raise unless every member skill agrees with
  each other, and (only via `AllowSkillRegrouping`) lets the group's own rating catch up to that
  shared value before proceeding. `AllowCyberwareEssDiscounts` is now honored too - the
  Cyberware/Bioware picker gained an Essence-discount % spinner (only shown when the house rule is
  on), applied client-side into the already-resolved Essence value the same way the grade
  multiplier already is. `AllowExceedAttributeBp` was investigated but skipped: this port has no
  persisted "starting BP total" field to check the 50% cap against at all (Bp is only ever tracked
  as a shrinking remaining pool), so implementing it correctly means adding new persisted state,
  not just gating existing logic - a bigger change than the rest of this sweep.
  `UseCalculatedVehicleSensorRatings` is now honored too - `CharacterVehicleData.CalculatedSensor`
  faithfully ports clsEquipment.cs's Vehicle.CalculatedSensor (including its "only ever looks at
  the vehicle's first onboard Gear item" quirk: averages that item's "Sensor Functions" category
  children's Rating, rounded up, only if the first item is itself Category "Sensors" with a Signal
  value), and `SensorDisplay` picks between it and the saved value per the house rule for the
  Fahrzeuge tab. `FreeSpiritPowerPointsMag` is now honored too - the new `FreeSpiritPowerPoints`
  derived value (ported from clsMainController.cs's CalculateFreeSpiritPowerPoints, shown on the
  Zauber tab next to Critter Powers for PC Free Spirits) uses EDG by default and MAG under the
  house rule, minus owned Critter Powers' point costs. Essence-loss-driven MAG/RES reduction is now
  built too - the new `EssencePenalty` (ported from clsCharacter.cs's `EssencePenalty`: whole
  points lost from the character's Essence maximum, rounded up) feeds a new
  `ApplyEssencePenaltyToAttribute`, ported from frmCareer.cs's `MetatypeSelected()` (`lblMAG`/
  `lblRES.Text`): by default MAG/RES's displayed value drops point-for-point with `EssencePenalty`;
  under `EssLossReducesMaximumOnly` the raw value is only clamped down if it exceeds the attribute's
  metatype maximum. This is a display-only adjustment applied in `Attributes`/`ReadAttributes` -
  calculations that read MAG/RES directly (Adept Power Points, Awakened, etc.) intentionally keep
  using the raw total, matching legacy (whose own `MAG.TotalMaximum` is never actually reduced by
  Essence loss anywhere in its codebase either - a legacy quirk faithfully preserved here, not a
  simplification of this port's). `RestrictStickNShock` is now honored too, deliberately
  simplified: legacy blocks loading Stick-n-Shock ammo into one specific excluded-category weapon,
  but this port's Gear tree has no concept of which weapon an ammo item is loaded into, so `AddGear`
  instead blocks acquiring "Ammo: Stick-n-Shock" at all when the character owns no weapon outside
  the excluded categories to use it with.

## Bonus-application engine (`clsImprovement.CreateImprovements` equivalent)

- 🟡 `BonusApplier` (`Chummer.Core/src/BonusApplier.cs`) is a new, from-scratch port of the
  non-interactive subset of clsImprovement.cs's `CreateImprovements`: given a rules-data
  `<bonus>` XML node, it resolves the tier-1 node types actually used across this port's own
  qualities/powers/cyberware/bioware/metamagic data - `specificattribute` (incl. the ESS special
  case), `specificskill`, `skillcategory`/`skillgroup`/`skillattribute`, `conditionmonitor`,
  `armor`, `reach`, `unarmeddv`/`unarmedap`, `initiative`/`initiativepass`, `lifestylecost`, and
  `notoriety` - into `ImprovementSpec` records. `CharacterFileService.ApplyBonus` then persists
  those as `<improvement>` elements (reusing the already-ported rating/attribute expression
  evaluator, `RatingExpression`, for legacy's `ValueToInt`), and `RemoveBonusImprovements` cleans
  them up again when the granting item is removed. Wired into `AddQuality`/`RemoveQuality`,
  `AddAdeptPower`/`RemoveAdeptPower`, `AddCyberware`/`RemoveCyberware` (both Cyberware and
  Bioware), and `AddMetamagic`/`RemoveMetamagic`. Verified end-to-end against real data: Analytical
  Mind's Skill bonuses, Improved Reflexes 2's InitiativePass/REA bonuses (with its `precedence`
  attribute), and Wired Reflexes' Rating-scaled InitiativePass/REA bonuses (including cleanup on
  removal).
  CritterPower/ComplexForm additions now call `ApplyBonus` too (`AddCritterPower`/
  `AddComplexForm`, cleaned up by the matching `RemoveCritterPower`/`RemoveComplexForm`). Critter
  Powers now have a real Rating input too - `CritterPowerDialog` shows a Rating spinner when the
  power's rules-data sets `<rating>yes</rating>` (only 4 of 189 do - Armor (Ballistic)/(Impact),
  Hardened/Mystic Armor), and `AddCritterPower` scales the bonus by that Rating instead of a fixed
  1; every other Critter Power still applies at 1 since it has no Rating concept at all. Complex
  Forms still always apply at Rating 1, matching their saved Rating (always "1" - this port has no
  Complex Form Rating input yet either). Verified against real data: Armor (Ballistic) at Rating 4
  grants +4 Ballistic Armor, and Empathy Software's Rating-scaled `skillcategory` bonus.

  **Selectable Improvement flow:** all three interactive bonus node types are now covered, not
  just `selecttext`:
  - `selecttext` (free-text prompt, `TextSelectionDialog`, ported from frmSelectText.cs) - wired
    into Quality (add + swap), Metamagic, Critter Power, and Complex Form adds. Verified against
    real data: Codeslinger (Quality), Attunement (Animal) (Metamagic), Elemental Attack (Critter
    Power), Knowsoft (Complex Form).
  - `selectskill`/`selectattribute` (a plain list picker, `ListSelectionDialog`, ported from
    frmSelectSkill.cs/frmSelectAttribute.cs with no category/rating extras this port doesn't
    otherwise model) - `GetQualitySkillSelectionOptions`/`GetQualityAttributeSelectionOptions` and
    their Adept Power/Critter Power/Complex Form equivalents list the eligible options (skills:
    the character's own owned active skills, filtered by the bonus's optional skillgroup/
    skillcategory/excludecategory attribute; attributes: the 8 physical/mental attributes plus
    MAG/RES when applicable, filtered by the bonus's optional attribute/excludeattribute list),
    wired into the same add flows as `selecttext`. Verified against real data: Aptitude and
    Exceptional Attribute (Quality), Improved Ability (Combat/Non-Combat) (Adept Power, including
    its Rating-scaled bonus and skillcategory/excludecategory filtering).

  All three share one apply path, `CharacterFileService.ApplySelectedImprovement` - the player's
  choice becomes a Text/Skill/Attribute Improvement, reusing each source's existing `strExtra`/
  `strSelected` parameter (Quality/Adept Power/Critter Power/Complex Form already had one for
  display purposes, e.g. "Allergy (Silver)"; Metamagic gained one). No real qualities.xml/
  powers.xml/critterpowers.xml/programs.xml/metamagic.xml entry combines more than one of
  `selecttext`/`selectskill`/`selectattribute`, so a single value per add is unambiguous.

  The Metamagic Improvement refresh on Initiation Grade raise is now ported too - see the
  Initiation entry above.

  `ImprovedSenseFullRating`/`selectsenseware` (the original trigger for building this engine) is
  now done, as a dedicated flow on top of `BonusApplier` rather than a generalized "Selectable
  Improvement" system: `GetSenseImprovementOptions` (ported from clsImprovement.cs's
  `AddSensewareSource`/the selectsenseware setup) lists eligible Cyberware/Bioware/Gear items for
  a power's `<selectsenseware>` bonus, filtered by category and (when requested)
  `<senseimprovement>yes</senseimprovement>`; the new `SenseImprovementDialog` lets the player pick
  one; `AddImprovedSensePower` then applies the selected item's own `<bonus>` at Rating 1, or its
  full `<rating>` under the `ImprovedSenseFullRating` house rule. Wired into the Adept Power picker
  (`AdeptPowersSectionTab`): adding "Improved Sense" now prompts for the senseware choice
  automatically instead of silently doing nothing. Verified end-to-end against real data
  (Olfactory Booster's Perception (Smell) bonus, both with and without the house rule).

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
  them specifically. "Als HTML exportieren" (found sitting unwired in the XAML as "Als PDF
  exportieren") now saves the actual rendered XHTML to disk - no PDF library or cross-platform
  print backend is referenced anywhere in this port, so real PDF export/native "Drucken" remain
  out of reach without adding a new dependency; "Drucken" stays disabled with a tooltip explaining
  why instead of silently doing nothing. Fixed two real bugs found while building/verifying this:
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
  picker dialogs, Kampfkünste's detail pane, and (now) Fahrzeuge's detail pane (via a new
  `TreeNodeViewModel.SourcePage`). Allgemein/Verbessern/Initiation's own "Quelle:" labels turned
  out not to be real book+page references (Allgemein/Initiation's detail panes are still static
  mockup content unrelated to this gap; Verbessern's "Quelle" is the Improvement's *granting
  source* - Quality/Cyberware/etc. - not a sourcebook page) so there was nothing to wire there.
  Now wired into every remaining picker dialog too (Cyberware/Bioware, Gear, Quality, Armor,
  Vehicle Mod, Weapon - the rest already had it from when they were originally built).
- ✅ Dice roller — ported frmDiceRoller.cs's roll/hit/glitch logic (Standard/Large/Really Large
  methods, Rule of 6, Cinematic Gameplay, Rushed Job, Gremlins rating, Threshold) into a
  standalone, character-independent `Zubehör → Würfeln...` dialog
- ✅ Update checker — ported from legacy's own already-Linux-adapted `frmUpdate.cs`.
  `UpdateChecker.CheckForUpdateAsync` (`Chummer.Core`) hits GitHub's Releases API
  (`api.github.com/repos/JonasTrampe/ChummerGenSR4/releases/latest`) and compares the tag against
  `Assembly.GetExecutingAssembly().GetName().Version` (parsing/comparison logic - stripping a
  leading `v` and any pre-release/build-metadata suffix - is pure and unit-tested separately from
  the network call). `UpdateDialog` shows the current/latest version and release notes, and opens
  the release page in the browser rather than self-replacing the running executable, matching
  legacy's own reasoning ("doesn't translate to how Linux/AppImage packaging will actually deliver
  updates"). Wired into Hilfe → "Nach Updates suchen..." (always shows a result, including "no
  update found") and a fire-and-forget startup check in `MainWindow`'s constructor gated by the
  existing `AutomaticUpdate` option (silent - only opens `UpdateDialog` if a newer release exists,
  same as legacy's `frmUpdate.SilentMode`). `Chummer.Avalonia.csproj` now sets `<Version>0.1.496
  </Version>`, continuing legacy's own `AssemblyInfo.cs` numbering, so there's a real version to
  compare against.

## Settings / options

- ✅ Options/settings UI: settings profiles, sourcebooks, build/karma/BP values, optional and
  house rules, global update/PDF/cloud options, and persistence through `SettingsStore`
- 🟡 Language selection UI exists in Options and reloads the language catalog; most Avalonia UI
  strings still remain hard-coded and are therefore not translated yet.

## Drag-and-drop / interaction niceties

- 🟡 Gear tree reordering/reparenting (MVVM-bound) — now backed by a real `CharacterFileService
  .MoveGear` write path (found while touching this area: the original drag/drop only mutated the
  in-memory ViewModel tree, so a reorder looked like it worked but was silently lost on the next
  reload/save - it now moves the actual XML node and reloads from the document like every other
  edit in this app). Not extended to the Cyberware/Weapons/Armor trees, which don't support
  drag/drop at all.

## Platform / packaging

- ❌ AppImage or other Linux distribution packaging (`docs/LINUX_PORT_PLAN.md` Phase 5)
- 🟡 Startup/runtime crashes have been fixed as found (data path layout, asset casing) but there's
  no smoke-test automation beyond manual `dotnet build` + kill-timeout runs done ad hoc in this
  session

## Test infrastructure

- ✅ `Chummer.Tests` has real coverage for `CharacterFileService`; targeted `dotnet test` runs
  successfully in this workspace. The project still emits a known `System.Net.Http` MSBuild
  conflict warning while building tests.
