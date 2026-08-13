# Feature checklist — Avalonia port vs. legacy WinForms Chummer

**Goal: full feature parity with legacy.** This is not an MVP — every open item below is real
backlog, not an accepted permanent gap. There wasn't a granular checklist before this file;
`PORTING_PLAN.md`/`docs/LINUX_PORT_PLAN.md` are older phase-level narratives.

**Audit:** [PARITY_AUDIT.md](PARITY_AUDIT.md) records the full 2026-08-13 legacy-surface scan.
A known skipped legacy branch is `[~]`, never `[x]`, even when the main flow works.

**Legend (one system, used everywhere in this file):**
- `[x]` done — real, working, not a mockup
- `[~]` partial — either investigated-and-deliberately-deferred (reason given), or working but
  scoped down in a way that matters
- `[ ]` not started

Keep entries to 1–3 lines. When a line needs real justification (why deferred, what formula, what
file), put it in a short trailing note, not a paragraph. Update this file in the same commit as
the change it describes.

---

## Open backlog

Everything not `[x]`, in one place, grouped by area.

### House rules — blocked on missing subsystems
- [~] **Creation-mode BP/Karma budget tracker**: status bar plus a live sidebar budget card show
  the remaining pool, categorized spend, and overdrawn state (with explicit reconciliation row).
  Metatype/metavariant selection, Contacts/Enemies, Active/Knowledge Skills and Skill Groups,
  Qualities, Spells, and Complex Forms now debit/refund their persisted creation pool; the
  remaining house-rule limits and creation mutations still need to consume it directly.
- [~] **Complex Form costs**: Core now calculates normal, Skillsoft, and
  AlternateComplexFormCost costs and charges both Career and creation purchases from their active
  pool (including a persisted creation refund); other creation acquisitions still need to consume
  the shared budget directly.
- [x] **Armor capacity, modification limits, and degradation**: `ArmorSuitCapacity` and
  `MaximumArmorModifications` calculate, display, persist, and reject overflow when adding armor
  mods; `ArmorDegradation` supplies AR-44 B/I damage and repair controls gated by its house rule.
- [x] Technomancer Complex-Form eligibility: picker is Technomancer-only, Autosofts honor
  `TechnomancerAllowAutosoft`, and `TechnomancerAllowCommlink` selects the active-Commlink
  Matrix Initiative branch.
- [x] `ExtendAnyDetectionSpell`: picker toggle, save-compatible `extended` flag, +2 Drain display,
  and duplicate rules-data variant suppression match the legacy flow.
- [x] `AllowEditPartOfBaseWeapon`: the weapon detail pane can toggle an Accessory/Mod's persisted
  `included` flag only when the rule is on; moving a mod out preserves the six-slot capacity gate.
- [x] `AllowCustomTransgenics`: enabled Bioware picker exposes “Add as Transgenic”, forces
  Standard grade and persists the `Genetech: Transgenics` category; the Cyberware tree retains a
  visible Transgenic indicator after reload.
- [x] `AllowBiowareSuites`: Bioware suite lookup/addition and its UI action are available only
  when the house rule is enabled; Cyberware suites remain available unconditionally.
- [ ] **Foci / Stacked Foci Core subsystem:** legacy `<foci>`/`<stackedfoci>` save collections
  have no `CharacterDocument` projection, bond/unbond, Karma/improvement, or stacking path;
  `AllowHigherStackedFoci` depends on this first-class Core feature.
- [x] `AllowObsolescentUpgrade`: deleting an eligible Obsolete/Obsolescent vehicle modification
  opens a percentage-based Retrofit flow that checks funds, replaces the modification, and records
  its Nuyen expense; Obsolescent remains gated by the house rule.
- [x] `AllowSkillDiceRolling`: enabled characters expose a dice action beside each nonzero active
  and knowledge-skill pool; it opens the standard Dice Roller prefilled with that exact pool.
- [x] `MoreLethalGameplay`: weapon lists and equipment trees apply the legacy +2 DV to numeric
  damage codes for personal and vehicle weapons, while special nonnumeric damage remains unchanged.
- [x] `StartupFullscreen`: the desktop lifetime creates the main Avalonia window in native
  fullscreen when the persisted option is enabled.
- [x] `AutomaticUpdate` / `SuppressCloudUnreachableWarning`: startup update check and local-open
  cloud failure flow already read the corresponding persisted global options.
- [x] `DatesIncludeTime`: expense entry/editing binds a date and, when enabled, a time picker;
  saved date values are normalized to a date-only value when the option is disabled.
- [x] `SingleDiceRoller`: the modeless dice roller focuses and reuses its sole instance when the
  global option is on; with it off, each menu/skill-pool action opens an independent roller.
- [x] `AutomaticCopyProtection` / `AutomaticRegistration`: acquiring an eligible Unwired
  Matrix Program/Skillsoft/Autosoft automatically adds the legacy zero-cost, `[0]`-capacity
  child plugins in both root and nested gear paths; covered by save/reload Core tests.
- [~] `ConfirmDelete`: shared Yes/No dialog honors the character profile for all main item/list
  delete actions (qualities, contacts, skills, spells/forms/powers, gear, armor, vehicles,
  cyberware, martial arts and custom Improvements). Remaining non-item removal actions need audit.
- [x] `CreateBackupOnCareer`: before a saved character switches to career mode, the current
  Create-mode XML is atomically saved as `<source-directory>/backup/<name> (Create Mode).chum`;
  failed writes stop finalization and report the error. Unnamed/unsaved characters have no source
  path to back up. Core snapshot/no-op tests cover this behavior.
- [x] `PrintToFileFirst`: native sheet printing writes rendered HTML to a temporary file and
  navigates the platform browser to it before opening the print UI; the file survives navigation
  and is deleted when that browser closes, matching the legacy Wine workaround safely.
- [~] `ConfirmKarmaExpense`: character-profile setting is exposed from Core and Career attribute/
  skill/group/specialization/initiation actions use a shared confirmation dialog; purchases remain.
- [ ] `LocalisedUpdatesOnly` and `OmaeAutoLogin`: now in scope; each needs a verified runtime
  integration beyond persistence.
- [x] `BookEnabled`: enabled-book filtering is applied by all rules-data pickers, including the
  selected-settings-aware metatype/metavariant dialog used before character creation.

### Everything else open
- [~] Cloud save/share: login, folder tree, push/download/archive/unarchive, metadata, revisions,
  drag/drop and sharing work; conflict/newer-revision round-trip handling still needs an
  end-to-end verification pass (see Character file I/O).
- [x] Weapons/Armor trees support persisted sibling drag-and-drop reordering. Weapon locations
  and armor sets remain explicit grouping operations rather than drag targets, matching their
  legacy XML representation.
- [~] Native cross-platform "Drucken" — implemented through Avalonia's NativeWebDialog platform
  print UI; awaiting a desktop smoke test on Linux/Windows/macOS before it is marked complete.
- [~] `frmSelectSkillCategory`/`frmSelectSpellCategory` — no real rules-data item needs either
  picker today; the only real caller would be the Manual Improvement Creator's excluded types.
  Nothing to build until that's expanded.
- [~] `frmCreateCyberwareSuite`/`frmCreatePACKSKit` (save-your-loadout-as-a-template authoring
  tools) — writes to shared app data files (`data/custom_*.xml`), not the character save, unlike
  every other Add/Create feature here. Needs a deliberate "user data directory" design before
  attempting.
- [~] `frmDiceHits`/Cyberzombie conversion — single ultra-niche caller; only worth building
  bundled with a hypothetical Cyberzombie-conversion feature.
- [ ] Legacy Special commands: Change Metatype, Mutant Critter, Toxic Critter, Convert to Free
  Sprite, Reapply Improvements, and BP-availability override; all are currently absent, not retired.
- [ ] Legacy item clipboard (copy/paste) and `frmHistory` character-history workflow.
- [~] Per-item rename and notes: Gear (including nested Gear), root Weapons, Armor, Quality,
  Spell, Spirit/Sprite, Adept Power, Martial Art, Maneuver, Metamagic, Complex Form, Critter Power, and Lifestyle
  notes have safe persisted paths; Armor sets are separate from legacy armor labels. Qualities,
  Spells, Adept Powers, Martial Arts, Maneuvers, Metamagics, Complex Forms, Critter Powers, and
  Lifestyles have no legacy custom-name field. Weapon accessories/modifications, attached gear
  and stored ammo; armor modifications and embedded armor gear; nested Cyberware/Bioware; and
  Vehicles with their installed items now have notes; the remaining item families still need
  theirs.
- [ ] Nested legacy containment flows: weapon underbarrels; gear-as-plugin under armor/cyberware/
  weapon accessories; vehicle sensor/cyberware/Nexus/plugin flows.

---

## Character file I/O

- [x] Open/save `.chum`, separate Save As, multiple characters in tabs, MRU/recent-files,
  unsaved-save/discard/cancel prompts and Window-menu document navigation all work.
- [x] Editing across tabs persists to the XML and survives save/reload (audited via a full
  `SelectedItem`/`SelectedIndex`/`Selected`/`IsChecked` binding sweep; fixed 5 missing
  `Mode=TwoWay` bindings, one of which made `LifestyleDialog` completely unusable)
- [~] Character creation flow (Karma/BP point-buy, not a priority-table system): Settings Profile
  → Karma/GP → Metatype (incl. Magician/Adept/Mystic Adept/Technomancer/none) → per-attribute/
  skill/skill-group Create-mode point spending → "Charakter fertigstellen" finalizes (prompts for
  the starting-Lifestyle-Nuyen roll first, `LifestyleNuyenDialog`) and switches to career-mode
  Karma costs. The special creation commands, creation clipboard, complete budget enforcement and
  backup-on-career remain open.
- [~] Cloud save/share (RunnersPoint API): login, folder tree, push/download/archive/unarchive,
  metadata, revisions, drag/drop into folders. Conflict/newer-revision handling still open (see
  backlog).

## Character sheet tabs

- [~] Allgemein, Fertigkeiten, Kampfkünste, Adeptenkräfte, Cyberware/Bioware, Charakter-Info,
  Karma/Nuyen, Kalender, Notizen — display + common add/edit/delete work; see open per-item
  rename/notes and nested-containment flows above.
- [x] Sprüche und Geister — real detail pane (was static mockup), Tradition/Stream selection,
  Drain/Fading resistance pool, per-spell casting dice pool (separate from Drain pool)
- [x] Komplexe Formen / Kritter-Kräfte — add/delete via picker
- [x] Initiation — grade list + raising a grade (Group/Ordeal discount, MAG/RES cap, MAG/RES
  Improvement replacement, Metamagic Rating refresh)
- [x] Straßenausrüstung — Lebensstil (incl. Advanced Lifestyle builder), Panzerung (mods + sets),
  Waffen (accessories/mods + locations + reload), Ausrüstung (incl. named locations)
- [x] Haustiere und Begleiter — edit/add/remove, link to a companion `.chum`, open linked
  character, clear link
- [x] Verbessern (Improvements list) — display, delete (Custom-sourced only, matches legacy), add
  (curated 13-type subset — see § Bonus-application engine)
- [~] Fahrzeuge und Drohnen — stats, mods (rating, cost, slot-capacity enforcement), onboard
  gear/weapons, damage tracking, locations, computed total cost, weapon-mount eligibility
  (tracks which mount each weapon occupies) all work. "Drones" aren't a separate feature in legacy
  either — just a Vehicle category, already handled. Nothing outstanding here beyond drag-drop
  (see backlog).
- [~] Add flow coverage: Quality, Spell, Gear, Spirit/Sprite, Martial Art (+Maneuver), Adept
  Power, Metamagic, Complex Form, Critter Power, Weapon Accessory/Mod, Karma/Nuyen history all
  work end-to-end with delete/edit. "Gabe austauschen" (swap Quality) works (simplified: no
  Karma-cost-delta charge since this port's Quality model has no BP/cost tracking at all — every
  owned Quality is swappable, no metatype-origin guard).

## Item picker dialogs (`frmSelectXxx` equivalents)

- [~] Of ~39 distinct pickers: Quality, Spell, Gear, Cyberware/Bioware, Armor, Weapon, Weapon
  Accessory, Weapon Mod, Vehicle, Vehicle Mod, Lifestyle (+ Advanced), exotic Skill, Martial Art
  (+Maneuver), Adept Power, Metamagic, CritterPower, ComplexForm, ContactConnection, ArmorMod,
  CyberwareSuite, MentorSpirit, Nexus, PACKSKit, ProgramOption, Side, SkillGroup — all ported.
  `Number`/`Quantity`/`Attribute`/`Skill`/`Text`/`Item` don't need dedicated dialogs (generic
  `ListSelectionDialog`/`TextSelectionDialog`, or inline `NumericUpDown`).
  - Weapon Accessory/Mod validate mount-slot eligibility (real `<accessorymounts>` + the
    `EnforceCapacity` 6-slot cap); doesn't enforce exclusivity between accessories/mods sharing a
    mount, matching legacy.
  - PACKSKit: majority-coverage pass — Vehicles, Martial Arts, Spirits, Lifestyles not ported
    (all rare in real `packs.xml`); within ported sections, Armor Mods/nested Armor Gear, Weapon
    Accessories/Mods, Exotic Skills are base-item-only.
- [~] `SkillCategory`/`SpellCategory` — see backlog.

## Derived stats / calculations

- [~] Essence, condition monitor (+ live damage boxes), armor encumbrance, skill dice pools
  (incl. defaulting at Rating 0), weapon dice pools (category mapping, Smartgun, specialization,
  installed Accessory/Mod bonuses, loaded-ammo `<weaponbonus><pool>`), Composure/Judge
  Intentions/Lift and Carry/Memory, Initiative (+Passes), Astral Initiative, Matrix Initiative
  (+Passes, all character types), career Karma/Nuyen totals, Sprite Matrix Initiative, movement
  rates, worn armor rating, wound modifier, damage resistance pool, Edge tracking + Burn Edge,
  Adept power points (incl. Mystic Adept MAG-split), attribute karma-cost curve, cyberware/
  bioware essence cost, gear/weapon/armor/cyberware avail & cost, Max Spirit/Sprite Force (Mystic
  Adept split vs. `SpiritForceBasedOnTotalMag`, Technomancer RES), weapon recoil compensation
  (`ComputeWeaponTotalRc`, ported from `clsEquipment.cs`'s `Weapon.TotalRC`: base + installed
  Accessory/Mod `<rc>` contributions, `RestrictRecoil`'s per-RC-Group cap - only the group's
  highest item counts instead of every item stacking, with the Foregrip+Sling combo guaranteeing
  at least 2 in Group 1 - and `StrengthAffectsRecoil`'s tiered STR bonus; shown in the Waffen tab's
  detail pane and the print sheet). Not ported: loaded-ammo `<weaponbonus><rc>` (no real gear.xml
  entry uses it, unlike the dice-pool equivalent). Commlink Response now factors in the
  `CalculateCommlinkResponse` house rule too (`ApplyCommlinkResponsePenalties`, ported from
  `clsEquipment.cs`'s `Commlink.TotalResponse`): Response drops by floor(running programs /
  System) for currently-Equipped child Gear items in a real "program" category, applied
  recursively so a Commlink nested under Armor/Cyberware is covered too. Also wires the
  `ErgonomicProgramLimit` house rule as a side effect (an "Ergonomic" child plugin exempts a
  program from the count only when this house rule is explicitly on - legacy's own
  inverted-sounding but exact logic, preserved as-is). System/Firewall/Signal themselves were
  already computed (the highest value found across an item's own tree, covering Commlink/OS
  Upgrade children) - only the Response-reduction house rule itself was the actual gap.
- [~] House-rule awareness — most Karma/BP costs and the house rules listed below are wired; the
  remainder is tracked in the backlog above. Wired: `IgnoreArmorEncumbrance`/
  `AlternateArmorEncumbrance`/`NoSingleArmorEncumbrance`, `AlternateMetatypeAttributeKarma`,
  `EnforceMaximumSkillRatingModifier`/`CapSkillRating`/`SkillDefaultingIncludesModifiers`,
  `EnforceCapacity`, `AllowSkillRegrouping` (also fixed: raising a skill group with diverged
  member ratings used to silently overwrite them, unrefunded — now blocked unless members agree,
  and only lets the group catch up under this house rule), `AllowCyberwareEssDiscounts`,
  `AllowExceedAttributeBp`, `UseCalculatedVehicleSensorRatings`, `FreeSpiritPowerPointsMag`,
  essence-loss-driven MAG/RES reduction (`EssLossReducesMaximumOnly`), `RestrictStickNShock`
  (simplified: blocks acquiring the ammo at all if no eligible weapon is owned, since this port
  doesn't track which weapon ammo is loaded into at purchase time — reload-time tracking does
  exist, see `frmReload` below).

## Bonus-application engine (`clsImprovement.CreateImprovements` equivalent)

- [~] `BonusApplier` resolves the real-data-driven node types: `specificattribute` (+ESS special
  case), `specificskill`, `skillcategory`/`skillgroup`/`skillattribute`, `conditionmonitor`,
  `armor`, `reach`, `unarmeddv`/`unarmedap`, `initiative`/`initiativepass`, `lifestylecost`,
  `notoriety`, `matrixinitiative`/`matrixinitiativepass`, `damageresistance`, `movementpercent`,
  `smartlink`, `softweave`, `concealability`, `skillsoftaccess`, `blackmarketdiscount`,
  `livingpersona` (all 5 sub-stats), `spellcategory`, `weaponcategorydv`. Wired into every
  Add/Remove path (Quality, Adept Power, Cyberware/Bioware, Metamagic, Critter Power, Complex
  Form).
  - Deliberately not ported: `enabletab`/`addattribute` (this port derives Magician/Adept/
    Technomancer tabs at creation, not via Improvements); vehicle-context stat bonuses (needs
    per-vehicle Improvement scoping this port's Vehicle model lacks); `essencemax`/`nuyenamt`/
    `freepositivequalities`/`freenegativequalities`/`cyberwareessmultiplier` (no consumer yet —
    would be unverifiable inert data).

### Core-parity scan priorities (2026-08-13)

- [ ] Persisted Focus and Stacked Focus read/write, bond limits/costs, improvement lifecycle,
  stacking/unstacking, and save/reload coverage. This is the only top-level legacy collection
  written by `clsCharacter.Save` without a `CharacterDocument` equivalent.
- [ ] Core-only rule branches: vehicle-scoped bonuses; `essencemax`, `nuyenamt`, free-quality and
  cyberware-essence-multiplier bonuses; ArmorMod B/I; loaded-ammo recoil; special-weapon ranges;
  Skillsoft/Activesoft, Mystic-Adept, SwapSkillAttribute, Enhanced Articulation,
  MetaRatingModifier, and Cyborg Essence cases.
- [x] Selectable Improvement flow — `selecttext`, `selectskill`/`selectattribute` all wired into
  Quality/Metamagic/Critter Power/Complex Form/Adept Power add flows via one shared apply path
  (`ApplySelectedImprovement`).
- [x] `selectsenseware`/`ImprovedSenseFullRating` — dedicated flow (`GetSenseImprovementOptions`,
  `SenseImprovementDialog`, `AddImprovedSensePower`), wired into the Adept Power picker.
- [~] Manual Improvement *add* — curated 13-type subset (Attribute, Skill, Condition Monitor
  Physical/Stun/Threshold/Threshold Offset, Initiative, Movement %, Concealability, Unarmed
  DV/AP, Reach, Lifestyle Cost) reusing the same `<bonus>`-XML pipeline every other bonus-granting
  item uses. Remaining ~37 legacy types excluded: no consumer, or need a picker this port doesn't
  have (skillcategory/skillgroup/skillattribute).
  - Also fixed: `ImprovementManager.ValueOf`/`AugmentedValueOf`/`DescribeValueOf`/
    `DescribeAugmentedValueOf` used to unconditionally skip `Improvement.Custom` entries —
    Manual Improvements would have silently done nothing even once built.
- [x] `precedence0`/`precedence1` UniqueName stacking rules ported into `ImprovementManager.ValueOf`.

## Output / tooling

- [~] Print / character sheet rendering — `CharacterSheetExporter` builds the full print-XML
  (every section the shipped sheets read) and transforms via real `.xsl` files
  (`XslCompiledTransform`); `SheetPreviewDialog` renders the actual open character with a
  template picker. "Als HTML exportieren" and "Als PDF exportieren" (system headless
  Chromium/Chrome `--print-to-pdf`, no bundled PDF library) both work. Vehicle-mounted Weapons
  don't carry damage/AP/RC in this port's saved data, so those three sheet fields render blank
  for them. Native OS "Drucken" dialog not implemented (see backlog).
- [x] PDF sourcebook page linking (`PdfLinkService`) — per-book reader path/page-offset editable
  in Options; clickable `SourceLink` control wired into every picker dialog and detail pane that
  has a real book+page reference.
- [x] Dice roller — standalone `Zubehör → Würfeln...` dialog (Standard/Large/Really Large,
  Rule of 6, Cinematic Gameplay, Rushed Job, Gremlins rating, Threshold)
- [x] Update checker — `UpdateChecker` (GitHub Releases API) + `UpdateDialog`, wired into
  Hilfe menu and a silent startup check gated by `AutomaticUpdate`
- [x] `frmSellItem` — Gear/Weapon/Armor/Cyberware/Bioware/Vehicle (root items only, not their
  mods/accessories individually; Lifestyles aren't sellable in legacy either)
- [x] `frmCreateSpell` — full DV formula + per-category modifier catalog (all 5 categories).
  Descriptors/Limited/Restriction-text fields not modeled (informational only, no spell in this
  port carries them). Mutual-exclusion checkbox UI guidance not ported (doesn't change any DV
  outcome).
- [x] `frmNaturalWeapon` — DV/AP/skill-link assembly; found and fixed a real gap this surfaced:
  `AddWeapon` had no way to link a weapon to a specific Active Skill at all (`<useskill>`).
- [x] `frmPrintMultiple` — multi-character combined export (`Game Master Summary.xsl`)
- [x] `data/export/Squad Manager.xsl` (`frmExport.cs`) — export-template picker + transform
- [x] `frmReload` — ammo compatibility by category, reload with unspent-round return, loaded-ammo
  dice-pool bonus, extended to vehicle-mounted weapons. Ammo compatibility is name-based (this
  port has no purchase-time "restrict to category" gear mode legacy's Gear.Extra field relies on).

## Settings / i18n

- [x] Options/settings UI — profiles, sourcebooks, build/karma/BP values, house/optional rules,
  global update/PDF/cloud options, persisted via `SettingsStore`
- [~] Sourcebook filtering (`Options.BookXPath()`) is wired into ordinary rules-data pickers.
  PACKS Kits/Suites remain unfiltered; MentorSpirit has no legacy filtering requirement.
- [x] Cyberlimb attribute averaging (AGI/BOD/STR), ported from `clsUnique.cs`'s
  `Attribute.TotalValue`
- [x] UI translation — done. Every AXAML string and every real UI-facing `.cs` string literal now
  resolves through the `Chummer.Core/data/lang/*.xml` catalog via `{loc:Loc Key}`
  (`LocExtension.cs`) or `App.LanguageCatalog.GetString(...)`. 265 new keys minted (no legacy
  equivalent existed) and translated into all 4 shipped languages (en-us/de/fr/jp). `App.axaml.cs`
  defaults to `"de"` (matching this fork's audience) unless a language was explicitly persisted.
  Remaining literals were audited individually and left as-is deliberately: `"Chummer"` (app
  name), `"Adobe/Foxit"`/`"SumatraPDF"` (technical option values, not UI text), `"Street"`
  (a `skills.xml` category code, not display text).
  Also fixed 2 real data-integrity bugs the earlier bulk pass introduced: `SpiritDialog`'s Type
  picker and `AddExoticSkillDialog`'s Category/Attribute pickers were reading back their
  now-translated `ComboBoxItem.Content` and persisting *that* as the saved value (e.g. a
  Japanese-language install would have saved a Japanese string into `<type>`/`<category>`,
  breaking `RemoveSpirit`'s literal `"Spirit"` match and any code expecting the canonical
  English/skills.xml-matching value) - fixed by adding a stable `Tag`/index-based internal value
  separate from the translated display text. Several ViewModel-only sentinel strings ("Kein Ort"/
  "Kein Set"/"Alle", the Dice Roller's method names) had the same class of risk (population vs.
  comparison drifting apart across languages) and were fixed the same way - a single
  catalog-backed constant used for both. Rules-data item name translation (`de_data.xml`/etc.) is
  separate and out of scope here.

## Interaction niceties

- [x] Cyberware/Gear tree drag-and-drop reordering/reparenting, backed by a real
  `MoveGear`/`MoveCyberware` write path (fixed: reordering used to only mutate the in-memory
  ViewModel, silently lost on reload)
- [x] Weapons/Armor trees — persisted sibling drag-and-drop reordering; locations/sets remain
  explicit assignment operations.

## Platform / packaging

- [x] Linux AppImage packaging (`packaging/linux/build-appimage.sh`) — self-contained
  `dotnet publish`, AppDir assembly, `appimagetool` invocation (auto-downloaded if missing).
  `dotnet publish` output and `AppRun` launch verified locally; `appimagetool` itself wasn't run
  in the original sandboxed session (no outbound network at the time) — worth a real run to
  confirm the final `.AppImage` binary once network access is available.
- [x] CI coverage for the whole Avalonia stack — `build-avalonia` job in
  `.github/workflows/autobuild.yml` (build Core+Avalonia, run `Chummer.Tests`, headless smoke
  test). Previously the workflow only covered the legacy WinForms app.

## Test infrastructure

- [x] `Chummer.Tests` has real coverage for `CharacterFileService`; `dotnet test` passes locally
  and reliably (389 tests, 5 consecutive clean runs). Known pre-existing noise: a
  `System.Net.Http` MSBuild conflict warning.
  Fixed a real thread-safety bug in `LanguageStringCatalog`/`LanguageManager`: every
  `new CharacterOptions()` (plus `GlobalOptions`'/`XmlManager`'s static constructors) lazily
  re-triggers `LanguageManager.Instance.Load(...)`, and under this port's parallelized test run
  (dozens of `CharacterOptions` instances constructed concurrently) two overlapping reloads could
  interleave and leave the catalog briefly, observably empty - a concurrent `GetString` on another
  thread would throw a bare `KeyNotFoundException`. `LanguageStringCatalog.Load` now builds the
  whole new dictionary off to the side and swaps it in with one atomic, locked assignment instead
  of the old three-call `Reset()`/`LoadBase()`/`ApplyLanguage()` sequence (each separately mutating
  the shared field); `LanguageManager.Load` also now serializes concurrent reload attempts against
  each other. Added `Chummer.Tests/src/TestSetup.cs` (a `[ModuleInitializer]`) so the catalog is
  guaranteed loaded before any test runs, matching what `App.axaml.cs` does for the real app.
