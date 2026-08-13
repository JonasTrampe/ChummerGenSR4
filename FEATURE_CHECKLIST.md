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
- [x] Expand `BonusApplier`/`ApplyBonus` beyond the original tier-1 node-type set - done (a second,
  frequency-ranked pass). Ranked every `<bonus>` child tag across this port's own data files by
  real occurrence count, then added parsing for every type that either already had a consuming
  calculation elsewhere in this port or was a trivial one-line port with no ambiguity:
  `matrixinitiative`/`matrixinitiativepass`, `damageresistance`, `movementpercent`, `smartlink`,
  `softweave` (closes the "YNT SoftWeave" gap noted in the ArmorMod entry above),
  `concealability`, `skillsoftaccess`, `blackmarketdiscount`, `livingpersona` (all five sub-stats:
  response/signal/firewall/system/biofeedback), `spellcategory` (closes the "Cat's own
  `<spellcategory>` bonus no-ops" gap noted in the Mentor Spirit entry below - both affected
  tests updated to assert the now-correct behavior), and `weaponcategorydv`.
  Deliberately NOT ported despite high raw frequency, because they're architecturally orthogonal
  to this port's model rather than simple oversights: `enabletab`/`addattribute` toggle
  Magician/Adept/Technomancer tab visibility via Improvements in legacy, but this port derives
  those flags directly at character creation instead (and their real usage is dominated by
  critters.xml, which this port doesn't build characters from); vehicle-context stat bonuses
  (`flyspeed`/`speed`/`accel`/`handling`/vehicle `response`) would need per-vehicle Improvement
  scoping this port's Vehicle model doesn't have; `essencemax`/`nuyenamt`/
  `freepositivequalities`/`freenegativequalities`/`cyberwareessmultiplier` have no consuming
  calculation anywhere in this port yet, so parsing them now would just be unverifiable inert
  data - revisit once/if their consuming features get built.
- [x] Manual Improvement *add* — done for a curated 13-type subset of `frmCreateImprovement.cs`'s
  ~50-type catalog (Attribute, Skill, Condition Monitor Physical/Stun/Threshold/Threshold Offset,
  Initiative, Movement %, Concealability, Unarmed DV/AP, Reach, Lifestyle Cost) - the types that
  map onto an `ImprovementType` this port's `BonusApplier` already parses *and* that already have
  a real consuming calculation elsewhere (verified per-type, not just "the tag parses without
  crashing"). The other ~37 catalog types were excluded because they'd be unverifiable inert data
  right now (no consumer - same reasoning as the excluded bonus-node-types note above) or need a
  picker this port doesn't have yet (skillcategory/skillgroup/skillattribute need
  `frmSelectSkillCategory`/`frmSelectSkillGroup` equivalents, still on the picker backlog).
  Implementation reuses the existing bonus-application pipeline directly instead of hand-building
  `ImprovementSpec`s per type: `AddCustomImprovement` synthesizes the same `<bonus>` XML shape a
  rules-data item's own bonus node would have (e.g. Attribute builds `<specificattribute>`) and
  runs it through `ApplyBonus`/`BonusApplier`, exactly like every other bonus-granting item
  already does - legacy's own `frmCreateImprovement.AcceptForm` does the same thing (builds a
  `<bonus>` node and hands it to `ImprovementManager.CreateImprovements`), so this isn't a
  simplification, it's the actual mechanism, just re-triggered through this port's existing entry
  point.
  **Also fixed a real bug found while wiring this up**: `ImprovementManager.ValueOf`/
  `AugmentedValueOf`/`DescribeValueOf`/`DescribeAugmentedValueOf` all unconditionally skipped
  `Improvement.Custom` entries, so even if Manual Improvements had existed before now they would
  have silently done nothing - legacy's real behavior is the opposite: Custom Improvements are
  summed too (in a second pass with their own separate UniqueName dedup, then added to the
  non-Custom subtotal). Removed the skip; this port folds Custom Improvements into the same single
  pass instead of replicating the two-pass split, which is behaviorally equivalent for the
  overwhelming common case (a Custom Improvement's UniqueName not colliding with a rules-derived
  one). `AppendImprovement` also now writes the real `custom` flag (`eSource == Custom`) instead
  of always `"False"`.
  New `CreateImprovementDialog` (type ComboBox with dynamic field visibility, matching legacy's
  own show/hide-per-type UI) wired into the previously-disabled "Verbesserung hinzufügen" button
  on the Improvements tab. Verified against real data: adding an Attribute Improvement changes the
  Attribute's Augmented total; a Skill Improvement changes that skill's dice pool; a Condition
  Monitor Physical Improvement adds boxes; all three are removable via the pre-existing "Löschen"/
  `RemoveCustomImprovement` path.
- [x] Give Critter Powers a real Rating input — done. `CritterPowerDialog` now shows a Rating
  spinner (`NumericUpDown`, min 1) when the selected power's rules-data sets
  `<rating>yes</rating>` (ported from frmSelectCritterPower.cs's `nudCritterPowerRating`), and
  `AddCritterPower` scales the power's `<bonus>` by that Rating instead of always using 1;
  `CharacterCritterPowerData.Rating` persists the choice. Verified against real data: Armor
  (Ballistic) at Rating 4 grants +4 Ballistic Armor; Fear (no `<rating>` flag) ignores a passed-in
  Rating and stays at "0".
- [x] Exotic-skill-with-specialization matching in `selectskill` — done. `ExtractSkillSelectionOptions`
  now offers Exotic Skills (which all share the same bare Name, only distinguished by
  Specialization, e.g. two different "Exotic Ranged Weapon" instances for Bow vs. Grenade
  Launcher) as their full "Name (Specialization)" form instead of the ambiguous bare Name, ported
  from clsImprovement.cs's selectskill handler's explicit Exotic Melee/Ranged Weapon/Pilot Exotic
  Vehicle check (generalized here to any Exotic Skill, not just those three, since the underlying
  reason - shared bare Name - is the same for all of them). Also had to extend
  `SkillImprovementContributions`/`ComputeSkillDicePool` to check the specialized `"Name
  (Specialization)"` Improvement key alongside the bare Name (ported from clsUnique.cs's Skill
  pool calc, which always checks both forms) - without this, a bonus stored under the specialized
  key would never actually reach the dice pool. Verified against real data: Aptitude applied to
  one Exotic Ranged Weapon specialization only affects that specialization's own Improvement, not
  the other one sharing the same bare Name.
- [ ] `precedence` stacking rules beyond what `ImprovementManager` already handles (see its own
  documented scope note).

**Character sheet tabs**
- [x] Fahrzeuge: track which specific "Weapon Mount"/"Mechanical Arm" mod each vehicle weapon
  occupies — done, see § Character sheet tabs (Fahrzeuge und Drohnen).
- [x] Weapon dice pools: accessory/mod dice pool bonuses — done, see § Derived stats. The
  "which Gear item is loaded into this weapon" concept itself is now ported too, see `frmReload`
  below - loaded-ammo `<weaponbonus><pool>` dice-pool bonuses themselves still aren't consumed
  (only 2 real gear.xml entries use it at all, lowest priority of what this unblocked).
- [x] A real spellcasting dice pool per spell — done, see § Character sheet tabs (Sprüche und
  Geister). Separate from the Drain/Fading resistance pool, which was already done.

**House rules**
- [x] `AllowExceedAttributeBp` — done, see § House-rule awareness in calculations.
- [x] `MultiplyRestrictedCost`/`MultiplyForbiddenCost` — done. Added
  `ApplyRestrictedForbiddenCostMultiplier` in `Chummer.Core/src/CharacterFileService.cs`, wired
  into `DeductGearCost`/`DeductVehicleModCost` (both now take the item's raw Availability
  string): applied by `AddGear`/`AddNexus`, `AddVehicle`, `AddVehicleGear`, `AddVehicleWeapon`,
  `AddVehicleMod`, `AddWeaponAccessory`, `AddWeaponMod`, `AddArmorMod`, and (see below)
  `AddWeapon`/`AddArmor`/`AddCyberware`.
- [x] `AddWeapon`/`AddArmor`/`AddCyberware` (root-level item purchases, as opposed to their
  mods/accessories which already went through `DeductGearCost`) never deducted Nuyen in this
  port - adding a weapon, a suit of armor, or a piece of cyberware/bioware was completely free.
  Found while wiring up the multiplier above; fixed by adding the same `DeductGearCost` call
  these three root Add methods were missing (all other Add methods already had it). 3 new tests
  confirm each deducts its own cost; 3 pre-existing mod/accessory tests whose expected Nuyen
  totals had baked in the "root item is free" bug were corrected to include the parent item's
  own cost.
- [x] Print-output house rules — done for `Chummer.Core/src/CharacterSheetExporter.cs`. Added
  public passthrough properties on `CharacterDocument` (`PrintSkillsWithZeroRating`,
  `PrintExpenses`, `PrintLeadershipAlternates`, `PrintArcanaAlternates`, `PrintNotesEnabled`) and
  wired each into the exporter: `AppendSkills` filters out zero-rating Active Skills (Knowledge
  Skills always print) and staples on synthetic "Leadership, Command"/"Leadership, Direct Fire"
  (LOG/INT) and "Arcana, Metamagic"/"Arcana, Artificing" (INT/MAG) copies via the new
  `CharacterDocument.BuildAlternateSkillForPrint` (recomputes a real dice pool for the substitute
  Attribute, not just a copied number); `AppendExpenses` skips entirely when off; `AppendInfo`
  now emits the character's own `notes` field, gated by `PrintNotesEnabled` (legacy's PrintNotes
  also gates Contact/MartialArtManeuver notes, but the only shipped sheet so far, Text-Only.xsl,
  never reads those, so that part wasn't ported - noted in a comment rather than silently
  dropped).
- [x] `UnrestrictedNuyen` — done. `CharacterDocument.NuyenPointsMax` (consumed by
  `RaiseNuyenCreate`'s spend cap) now returns `StartingBuildPoints` (falling back to 1000 for
  saves with no recorded starting total) instead of the persisted `<nuyenmaxbp>` priority-table
  value when the house rule is on, matching `clsCharacter.cs`'s `NuyenMaximumBP`.
- [ ] Broader house-rule audit: re-checked every remaining `bool` in `Options.cs`/`CharacterOptions`
  for a real consumer (both of legacy's `frmOptions.cs` tabs - "House Rules" and "Optional Rules",
  the latter ported here as `OptionalRulesOptionsTab` with all 16 real checkboxes present, so the
  UI-level split itself was never the gap). This pass found 3 more already-wired than the previous
  version of this note claimed - **corrected, not still open**: `AllowSkillRegrouping` (gates
  `RaiseSkillGroupCreate`'s common-rating check), `CapSkillRating` (gates the Active Skill rating
  cap), `UseCalculatedVehicleSensorRatings`, and `NoSingleArmorEncumbrance` (already covered by
  `AllowExceedAttributeBp`'s neighboring row via `ComputeArmorEncumbrance`) are all real consumers
  in `CharacterFileService.cs` today.
  Of what's left, most are blocked on a whole missing subsystem rather than a one-line wire-up -
  listed here grouped by what's missing, so a future pass builds the subsystem once instead of
  re-discovering the same blocker 3 times:
  - No creation-mode BP/Karma budget tracker at all (this port's creation flow has no
    "points remaining" summary the way `frmCreate.cs` does): blocks `MetatypeCostsKarma`,
    `ExceedPositiveQualities`/`ExceedNegativeQualities`/`ExceedNegativeQualitiesLimit`,
    `FreeKarmaKnowledge`, `SpecialAttributeKarmaLimit`, `SpecialKarmaCostBasedOnShownValue`,
    `BreakSkillGroupsInCreateMode`.
  - No Complex Form Karma/BP cost calculation exists at all (Complex Forms have no cost tracking
    in this port, period): blocks `AlternateComplexFormCost`.
  - No Armor capacity-remaining tracking (flagged previously in the `frmSelectArmorMod` entry
    above too): blocks `ArmorSuitCapacity`, `MaximumArmorModifications`. `ArmorDegradation` is
    the same class of gap (no damage/condition-monitor-driven Armor rating reduction exists).
  - No Commlink Response/System/Firewall/Signal *derived* calculation (these are raw persisted
    fields, not computed from a formula): blocks `CalculateCommlinkResponse`.
  - No weapon recoil-compensation calculation exists: blocks `RestrictRecoil`/`StrengthAffectsRecoil`.
  - No Mystic Adept MAG-split (`MAGMagician`) ported: makes `SpiritForceBasedOnTotalMag` almost a
    no-op even if wired (legacy's own two branches only differ for Mystic Adepts) - low value
    until that split exists.
  - No Technomancer-specific gear/Complex-Form eligibility gating (Autosofts/Commlink-use
    restrictions aren't enforced against Technomancer status anywhere): blocks
    `TechnomancerAllowAutosoft`/`TechnomancerAllowCommlink`.
  - Standalone/small, no missing subsystem, just not yet done: `AllowBiowareSuites` (Cyberware
    Suite builder doesn't yet distinguish Bio- from Cyberware-suites for this gate),
    `AllowCustomTransgenics`, `AllowEditPartOfBaseWeapon`, `AllowHigherStackedFoci`,
    `AllowObsolescentUpgrade`, `AllowSkillDiceRolling` (dice-roller feature, not a calculation),
    `ErgonomicProgramLimit`, `ExtendAnyDetectionSpell`, `MoreLethalGameplay` (combat-tracker
    adjacent - see the Sell Item/Reload note below).
  Not included: app-behavior toggles that aren't character-calculation house rules at all
  (`ConfirmDelete`, `ConfirmKarmaExpense`, `CreateBackupOnCareer`, `DatesIncludeTime`,
  `LocalisedUpdatesOnly`, `AutomaticUpdate`, `AutomaticCopyProtection`, `AutomaticRegistration`,
  `BookEnabled`, `OmaeAutoLogin`, `PrintToFileFirst`, `SingleDiceRoller`, `StartupFullscreen`,
  `SuppressCloudUnreachableWarning`) - those belong with their respective UI/session features.

**Item picker dialogs**
- [x] Audit legacy's `frmSelectXxx` files against what's ported here — done. Of the ~39 distinct
  `frmSelectXxx.cs` pickers, `frmSelectNumber`/`frmSelectQuantity` don't need a dedicated dialog
  (plain `NumericUpDown` inline in this port's own dialogs), and `frmSelectAttribute`/
  `frmSelectSkill`/`frmSelectText`/`frmSelectItem` are all covered by the generic
  `ListSelectionDialog`/`TextSelectionDialog`. The remaining genuinely-missing pickers, each its
  own small backlog item since most are tied to a feature that doesn't exist at all yet, not just
  a missing dialog shell:
  - [x] `frmSelectArmorMod` — done. `AddArmorMod`/`RemoveArmorMod` (matched by name+category, same
    lookup approach as `RemoveArmor`/`SetArmorEquipped`, since this port's Armor items have no
    guid) apply the mod's own rules-data `<bonus>` block at the player-chosen Rating; the new
    `ArmorModDialog` (a `NumericUpDown` Rating spinner shown when `<maxrating>` &gt; 1, mirroring
    the Critter Power picker's pattern) is wired into the Panzerung sub-tab's new "Mod
    hinzufügen"/"Mod löschen" buttons. Not ported: Armor capacity enforcement (this port doesn't
    track Armor capacity remaining at all yet, unlike Gear/Weapon Mod slots). Verified against real
    data: Chemical Protection's Rating-scaled cost (`Rating * 250`) and correctly not crashing on
    YNT SoftWeave's `<softweave />` bonus (now applied as an `ImprovementType.SoftWeave`
    Improvement - see the Bonus-application engine entry above).
  - [x] `frmSelectAdvancedLifestyle` — done. Ported as `GetLifestyleAspectOptions`/
    `GetLifestyleQualityOptions`/`PreviewAdvancedLifestyle`/`AddAdvancedLifestyle`: total LP is
    the five aspects' (Comforts/Entertainment/Necessities/Neighborhood/Security) own `<lp>` plus
    each checked Quality's `<lp>` (lifestyles.xml has its own quality catalog, separate from
    qualities.xml, filtered to `<allowed>` containing "Advanced"; Negative Qualities carry
    negative `<lp>` in real data), Nuyen cost from the `<costs>` table (linear extrapolation past
    LP 30, matching legacy), scaled by Roommates%/Percentage. `PreviewAdvancedLifestyle` factors
    out the exact same computation `AddAdvancedLifestyle` uses so the dialog's live LP/Nuyen
    preview can never drift from what actually gets added.
    Also fixed a related gap this surfaced: `GetLifestyleNuyenRollInfo` used to re-look-up each
    owned Lifestyle's Dice/Multiplier by name against lifestyles.xml, which only works for plain
    Lifestyles whose name still matches a rules-data entry - an Advanced Lifestyle's
    player-chosen name never would, silently contributing 0 to the starting-Nuyen roll. `AddLifestyle`
    now optionally persists `<dice>`/`<multiplier>` directly (written by `AddAdvancedLifestyle`),
    and `GetLifestyleNuyenRollInfo` prefers those when present, falling back to the by-name
    lookup for older saves/plain Lifestyles without them.
    Not ported: legacy's separate Safehouse/BoltHole `<slp>` LP overrides (Advanced type only,
    matching this port's existing Lifestyle scope) and its "effective LP" (aspects-only, ignoring
    Qualities) used solely to pick Dice/Multiplier independently from the Nuyen-cost LP - this
    port uses one total LP for both, which only differs from legacy when Qualities push the
    character across a LP tier boundary. New `AdvancedLifestyleDialog`/
    `AdvancedLifestyleDialogViewModel` (five aspect ComboBoxes, Roommates/Percentage
    NumericUpDowns, Positive/Negative Quality checklists reusing `WeaponCategoriesDialog`'s
    checkbox-`ItemsControl` pattern) wired into `GearSectionTab`'s new "Erweiterten Lebensstil
    erstellen" button.
  - [x] `frmSelectCyberwareSuite` — done. Ported as `GetCyberwareSuiteNames`/`AddCyberwareSuite`
    (also used for Bioware Suites via `blnBioware`): a Suite fixes a single Grade for every part
    and real cyberware.xml/bioware.xml suites nest cyberware within cyberware (plugins, e.g.
    "Urban Kshatriya Alpha"'s Cybereyes Basic System carries six nested vision mods), so this
    builds the XML tree directly rather than reusing `AddCyberware`'s single-item flow -
    per-part Essence/Cost are evaluated at their own Rating (`RatingExpression`, matching
    formula items like Muscle Replacement's "Rating * 5000") then multiplied by the Suite's
    Grade multipliers, and each part's own `<bonus>` is still applied via the existing
    `ApplyBonus`. `CyberwareSectionTab` gained "Cyberware-Suite"/"Bioware-Suite" buttons that
    reuse the existing generic `ListSelectionDialog`. Not ported: legacy's own `TotalCost`
    Nuyen deduction (frmCareer.cs deducts it separately) - matches `AddCyberware`'s existing
    no-nuyen-deduction behavior for Cyberware/Bioware in this port.
  - [x] `frmSelectMentorSpirit` — done. Mentor Spirit is a Quality (`<bonus><selectmentorspirit /></bonus>`,
    same for the Technomancer "The Beast's Way" Quality's `<selectparagon />`) whose own `<bonus>`
    stays a no-op until a picker resolves it, ported here as `QualityMentorSpiritDataFile` (returns
    "mentors.xml"/"paragons.xml"/null) plus new `AddQuality` parameters `strMentorSpirit`/
    `strMentorChoice1` that apply the chosen mentor's own `<bonus>` and its selected `<choice>`'s
    `<bonus>` (both via the existing `ApplyBonus`/`BonusApplier`, recorded under
    `ImprovementSource.Quality`/the Quality's own name so `RemoveQuality`'s existing cleanup handles
    them for free). New `MentorSpiritDialog` (category filter, advantage/disadvantage text, a
    choice-of-N dropdown) wired into `GeneralSectionTab`'s Add/Swap Quality flows, shown whenever
    `QualityMentorSpiritDataFile` returns non-null. Not ported: legacy's `set="2"` second-independent-
    choice split (only 2 of ~40 mentors.xml entries use it) — this port offers one choose-one dropdown
    over the full `<choices>` list instead, so those 2 mentors only get their first pick applied.
    Verified against real data: Cat's own `<spellcategory>` bonus (Illusion +2) and its chosen
    `<choice>`'s `<specificskill>` bonus (Gymnastics +2) both apply and are fully cleaned up on
    `RemoveQuality` (spellcategory parsing added later - see the Bonus-application engine entry
    above).
  - [x] `frmSelectNexus` — done. Ported as `AddNexus(intProcessor, intResponse, intSystem,
    intFirewall, intSignal, intPersona, blnFree)`, reproducing `CalculateNexus`'s per-tier
    Cost formulas verbatim - including its Response 7-10 cost bug (multiplies its own
    still-zero running total instead of the rating, always landing on 0¥ for that tier) rather
    than fixing it, since a character built to match a legacy save must land on the exact same
    numbers. Legacy assembles five separate child Gear items per attribute; this port's Gear
    already carries direct Response/Signal/System/Firewall fields on a single node, so the
    whole Nexus is added as one root-level Gear item (category "Nexus", Avail "0", the Persona
    Limit folded into the name like legacy's own Processor-in-name convention). New
    `NexusDialog`/`NexusDialogViewModel` (six NumericUpDowns matching the real min/max bounds,
    live cost preview using the identical formula) wired into `GearSectionTab`'s new "Nexus
    zusammenstellen" button.
  - [x] `frmSelectPACKSKit` — done (majority-coverage pass). Ported as
    `GetPacksKitCategories`/`GetPacksKitNames`/`AddPacksKit`, reusing this port's existing
    higher-level `Add*` methods (Qualities, Spells, Adept Powers, Complex Forms, Armor, Weapons)
    so each item's bonus application comes for free, plus direct XML-tree building (reusing
    Cyberware Suite's `AppendCyberwareSuiteItem` verbatim for the Cyberware/Bioware sections,
    since a PACKS kit's per-item Grade/nesting shape is identical to a Suite's) for Gear, which
    needs its own recursive nested-plugin builder (`AppendPacksGearItem`) since real packs.xml
    gear items nest up to 2 levels deep (e.g. "Sony Emperor" commlink with a "Vector Xim"
    plugin). Attributes are applied via the existing `SetAttributeValue` (which takes an
    absolute target value directly, so legacy's "value - (6 - MetatypeMaximum)" human-scale
    translation isn't needed). `nuyenbp` is applied as a flat Nuyen add (doubled under Karma
    build, matching legacy). A smoke test (`AddPacksKit_EveryRealKit_AppliesWithoutThrowing`)
    verifies all ~172 real packs.xml entries apply without exceptions.
    Not ported (all rare in real data, so scoped out rather than half-implemented): Vehicles (7
    real kits), Martial Arts via `<selectmartialart>` (2), Spirits (1), Lifestyles (0 real kits
    use it) - and, within the ported sections, Armor Mods/nested Armor Gear, Weapon
    Accessories/Mods, and Exotic Skills are all skipped (base item only), consistent with how
    those are already separate follow-up adds elsewhere in this port. New "PACKS-Kit
    hinzufügen" button on `GeneralSectionTab` shows a two-step category-then-kit
    `ListSelectionDialog` pair, then refreshes the whole `CharacterTab` (not just its own
    section) since a kit can touch nearly every tab - reusing the same full-refresh path
    `MainWindow`'s Options-changed handler already uses.
  - [x] `frmSelectProgramOption` — done. Ported as `GetComplexFormOptionChoices` (filters
    programs.xml's `/chummer/options/option` list by `<programtypes>` matching the Complex Form's
    own `<category>`, exactly like `frmSelectProgramOption.cs`'s Load handler) plus
    `AddComplexFormOption(strGuid, strOptionName)` which appends a `<programoption>` to the
    matching `<techprogram>` with Rating 1 unless the option's `<maxrating>` is explicitly "0"
    (legacy defaults `maxrating` to 6 otherwise, so most real options start at Rating 1).
    `CharacterComplexFormData` gained a `Category` field to drive the filter.
    `SpellsSectionTab`'s new "Option" button reuses the existing generic `ListSelectionDialog`.
    Not ported: legacy's rare per-option `<bonus>` application (only 1 of 25 real programs.xml
    options has one) and the Complex Form capacity gating in `frmCreate.cs` (moot here since saved
    Complex Forms are always Rating 1, i.e. always `CalculatedCapacity` 0 under the "Rating/2"
    formula that gating uses — so this port never blocks adding an option on capacity grounds).
  - [x] `frmSelectSide` — done. Ported as `CyberwareRequiresSideSelection` (detects a bare
    `<selectside />` bonus, real usage: 17 paired Cyberware/Bioware items like Single Cybereye)
    plus a new `AddCyberware(..., strSide)` parameter that writes the pick straight to the item's
    own `<location>` field — matching legacy, which stores this on the item directly rather than as
    an Improvement. `CyberwareSectionTab`'s Add flow reuses the existing generic
    `ListSelectionDialog` (no new dialog needed) with the real "Left"/"Right" values.
  - [x] `frmSelectSkillGroup` — done (standalone group-only picker, distinct from `selectskill`'s
    skillgroup/skillcategory *filtering*, which was already done). Ported as
    `CyberwareRequiresSkillGroupSelection` (detects a bare `<selectskillgroup>` bonus; real usage:
    Reflex Recorder (Skill Group)/(Skill) in bioware.xml) plus `GetCyberwareSkillGroupOptions`
    (reads `/chummer/skillgroups/name` from skills.xml, filtered by the bonus's
    `excludecategory` attribute exactly like `frmSelectSkillGroup.cs`'s Load handler: a group is
    offered if at least one of its skills has a category not in the exclude list). The chosen
    group is applied as an `ImprovementType.SkillGroup` Improvement via a new
    `selectskillgroup` branch in `ApplySelectedImprovement` (reusing the same
    selecttext/selectskill/selectattribute dispatch `AddQuality`/`AddAdeptPower` already use), and
    a new `AddCyberware(..., strSelectedSkillGroup)` parameter feeds it. `CyberwareSectionTab`'s
    Add flow reuses the existing generic `ListSelectionDialog` (no new dialog needed).
  - [~] `frmSelectSkillCategory` — investigated: real `qualities.xml`/`cyberware.xml`/`bioware.xml`
    have no `<selectskillcategory>` bonus node anywhere; the only real caller is the Manual
    Improvement Creator (`frmCreateImprovement.cs`), already tracked as its own backlog item below,
    same situation as `frmSelectSpellCategory`. Nothing to build here until that item is tackled.
  - [~] `frmSelectSpellCategory` — investigated: real `qualities.xml` has no
    `<selectspellcategory>`/similar bonus node anywhere (Aspected Magician is 5 separately-named
    Qualities per category, not one Quality with a category picker); the only real caller is the
    Manual Improvement Creator (`frmCreateImprovement.cs`), which is its own already-flagged
    large backlog item below. Nothing to build here until that item is tackled.

**Small self-contained tools** (found via a fresh audit of all 70 legacy `frm*.cs` forms against
what's ported, not previously tracked anywhere in this file)
- [x] `frmSellItem` — done for Gear/Weapon/Armor/Cyberware/Bioware/Vehicle (the 5 most common
  root item types). Added `SellGear`/`SellWeapon`/`SellArmor`/`SellCyberware`/`SellVehicle` to
  `Chummer.Core/src/CharacterFileService.cs`, each computing the refund from the item's current
  total cost (its own cost plus installed mods/accessories/plugins, reusing the existing
  `CharacterTreeItemData.CalculatedCost`/`ReadTreeItem` machinery) times a 0.0-1.0 sell
  percentage, rounded to the nearest whole Nuyen (matching legacy's `Convert.ToInt32`), before
  removing the item and logging a Nuyen expense entry via the existing `AddExpense`. New
  `SellItemDialog` (a single 0-100% `NumericUpDown`, ported from `frmSellItem.cs`) wired into a
  "Verkaufen" button next to each item type's existing "Löschen" button in `GearSectionTab`
  (Gear/Weapon/Armor), `CyberwareSectionTab`, and `VehiclesSectionTab`. Not covered: Vehicle Mods/
  Gear/Weapons and Weapon Accessories/Mods sold individually (only their parent root item), and
  Lifestyles (legacy doesn't sell Lifestyles either - they're a recurring cost, not owned equipment).
- [ ] `frmCreateSpell` (867 lines) — player-facing tool to homebrew a new Spell by combining
  rules-data building blocks (type/range/duration/damage formula) instead of picking one from
  `spells.xml`. Sizable feature, similar in spirit to `AddNexus`/`AddAdvancedLifestyle` but
  bigger.
- [x] `frmNaturalWeapon` — done. Added `AddNaturalWeapon`/`GetCombatActiveSkillNames` to
  `Chummer.Core/src/CharacterFileService.cs`: assembles the Damage Value string from a base
  (a fixed rating or "(STR/2)"), an optional signed modifier, and a P/S type, formats AP the
  same signed way, and copies Source/Page from critterpowers.xml's "Natural Weapon" power entry -
  Avail 0/Cost 0, matching legacy. Also found and fixed a real pre-existing gap this surfaced:
  `AddWeapon` had no way to link a weapon to a specific Active Skill at all - `ComputeWeaponDicePool`
  only ever mapped dice pools by weapon Category, so a Natural Weapon's Category (not in the
  Category-&gt;Skill table) would silently roll an empty pool. Added a `strUseSkill` parameter to
  `AddWeapon` (persisted as `<useskill>`, previously always hardcoded empty) and made
  `ComputeWeaponDicePool` prefer it over the Category mapping when present - this also means any
  future weapon type needing a per-item Skill override (not just Natural Weapons) is now
  supported. New `NaturalWeaponDialog` (Name/Skill/DV base+mod+type/AP/Reach) wired into a
  "Natürliche Waffe erstellen" button on the Waffen tab next to "Waffe hinzufügen".
- [ ] `frmPrintMultiple` — batch-loads several `.chum` files and renders them together into one
  combined sheet. `SheetPreviewDialog` only handles one character at a time. Niche (GM tooling).
- [ ] `data/export/Squad Manager.xsl` — a second XSLT export pipeline separate from
  `data/sheets/` (`frmExport.cs`), currently completely unreachable from any UI in this port.
  Low value (one template) but literally dead data right now.
- [ ] `frmCreateCyberwareSuite`/`frmCreatePACKSKit` — save-your-own-loadout-as-a-reusable-template
  authoring tools (the inverse of `AddCyberwareSuite`/`AddPacksKit`, which only consume existing
  templates). Power-user data authoring, not core gameplay - low priority.
- [x] `frmReload` — done. Added `ReloadWeapon`/`GetWeaponAmmoOptions`/`GetWeaponAmmoCapacityChoices`
  to `Chummer.Core/src/CharacterFileService.cs`, giving this port its first "which specific Gear
  item is loaded into this weapon" concept (previously blocking loaded-ammo dice-pool bonuses and
  used only as a simplification note by `RestrictStickNShock`). Ammo compatibility is checked by
  name against the Weapon's AmmoCategory (`IsAmmunitionCompatible`, ported verbatim from
  `frmCareer.cs` - Arrows only fit Bows, Grenades only fit Grenade Launchers, etc., with a
  catch-all for plain Ammo: Regular/Stick-n-Shock/etc.) rather than legacy's Gear.Extra field,
  which this port has no equivalent purchase-time "restrict to this category" gear-picker mode
  for. Reloading returns any unspent rounds from the weapon's previous load back to that Gear
  item's Quantity first, then clamps the new load to whatever's actually available (matching
  legacy's forgiving "use whatever is left" behavior instead of failing outright).
  Stick-n-Shock is excluded from the options list when `RestrictStickNShock` excludes the
  weapon's category, same house rule `AddGear` already enforces at purchase time. New
  `ReloadDialog` (ammo + round-count pickers) wired into a "Nachladen" button on the Waffen tab,
  with the loaded ammo/remaining-rounds shown in the weapon detail panel. Extended to
  vehicle-mounted Weapons too (`FindWeaponNodeByGuid` checks both root and vehicle-mounted
  Weapons; the vehicle-weapon XML's previously-unused `ammoloaded`/`ammoremaining` placeholder
  fields are now the real thing, same shape as root Weapons - the legacy 4-round-slot
  `ammoloaded2-4` concept for belt-fed multi-barrel weapons wasn't ported, single load only),
  with a matching "Nachladen" button on the Fahrzeuge tab. The loaded-ammo
  `<weaponbonus><pool>` dice-pool bonus is now wired too (`SumLoadedAmmoDicePoolBonus`, verified
  against real data: Ammo: Deathdealer's +1 and Ammo: High-Power Rounds' -2 - only 2 gear.xml
  entries use it, but the mechanism was already built so wiring it in was nearly free).
- [~] `frmDiceHits`/Cyberzombie conversion — investigated: single ultra-niche SR4 special-rule
  form with exactly one caller (`frmCreate.cs`'s Cyberzombie conversion flow). Not worth building
  in isolation; would only make sense bundled with a hypothetical Cyberzombie-conversion feature.

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
- [x] Enabled-sourcebook filtering (`Options.BookXPath()`) — done. Added
  `CharacterDocument.IsBookEnabled(string strSourceCode)` in `Chummer.Core` (blank source is
  always allowed, matching legacy's "only filter items that declare a source" behavior; wraps
  `CharacterOptions.BookEnabled()`) and wired it into every rules-data item picker's `LoadOptions`:
  Gear, Weapon, WeaponAccessory, WeaponMod, Armor, ArmorMod, Vehicle, VehicleMod, Cyberware/
  Bioware, Lifestyle, Quality, Spell, Metamagic, ComplexForm, CritterPower, AdeptPower,
  MartialArt, MartialArtManeuver. The dialogs that previously took no character context
  (ArmorDialog, ArmorModDialog, GearDialog, LifestyleDialog, VehicleDialog, VehicleModDialog,
  WeaponDialog, WeaponAccessoryDialog, WeaponModDialog) now take an optional `CharacterDocument?`
  constructor param that both section tabs (`GearSectionTab`, `VehiclesSectionTab`) pass through
  from their existing `_character` field. Not yet covered: MentorSpirit picker (mentors.xml has
  no per-item source filtering need in legacy either) and PACKS Kits/Suites (their own
  suites.xml/kits.xml lookups are a separate, smaller surface not addressed here).
- [x] Cyberlimb attribute averaging (AGI/BOD/STR) — done. Added `ApplyCyberlimbAveraging` to
  `CharacterFileService.ReadAttributes()` (`Chummer.Core/src/CharacterFileService.cs`), ported
  from `clsUnique.cs`'s `Attribute.TotalValue`: for AGI/BOD/STR, sums each owned Cyberlimb's own
  Body/Strength/Agility (base 3, overridden/boosted by "Customized X"/"Enhanced X" child plugins
  via the new `ComputeCyberlimbStats`), skips limbs whose rules-data `<limbslot>` (looked up on
  demand via the new `GetCyberwareLimbSlot`, since it isn't persisted per-item) matches
  `Options.ExcludeLimbSlot`, and pads any unreplaced limbs with the meat value out to
  `Options.LimbCount` before averaging - same formula as legacy. 4 new tests cover full-vs-partial
  limb replacement, the Customized-child override, `ExcludeLimbSlot`, and that unrelated
  attributes are untouched.

**Interaction niceties**
- [x] Extend drag/drop reordering/reparenting to the Cyberware tree — done, see § Character sheet
  tabs. Weapons/Armor still don't support it.

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
  helper Composure/Judge Intentions/etc. already use. A real per-spell spellcasting dice pool is
  now computed too, ported from clsUnique.cs's Spell.DicePool: the character's Spellcasting skill
  TotalRating, +2 if that skill's own Specialization matches the spell's Category, plus any
  SpellCategory Improvements targeting that Category (`CharacterDocument.ComputeSpellDicePool`,
  shown with its tooltip breakdown in the spell detail pane). This is a different number from the
  Drain/Fading resistance pool covered above - Drain resists a spell's backlash, this pool is what
  you roll to cast it in the first place. Verified: Spellcasting(4) + specialization match(+2) +
  a SpellCategory Improvement(+1) = 7 for a matching-category spell, vs. 4 for a non-matching one
  on the same character.
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
  nests the new weapon under that specific mod node). `AddVehicleWeapon` now tracks which specific
  mount each weapon occupies too - it records the first not-already-claimed mount-type mod's guid
  on the new weapon (`<vehiclemountguid>`), simplified from legacy's approach of physically nesting
  the weapon node under the mount VehicleMod (this port's existing "direct onboard weapon" UI
  already attaches every weapon at the vehicle root, so restructuring that would be a bigger,
  riskier change for the same net eligibility behavior). Removing a weapon frees exactly its own
  mount rather than just decrementing a count, verified with two mounts/two weapons: removing one
  weapon lets a new one be added even though the other mount stays occupied, and both occupied
  again correctly blocks a third. Mod
  "class eligibility" (a mod's `<limit>` field, e.g.
  "Groundcraft Only") turns out not to be a real gap: checked frmSelectVehicleMod.cs and legacy
  itself never validates it either, it's purely informational text next to the mod name - already
  shown that way here too (`VehicleModOptionViewModel.Limit`).
- ✅ Charakter-Information — text fields and profile counters load, edit, and save back into the
  character file
- ✅ Karma und Nuyen (expense history + real running-total charts)
- ✅ Kalender (saved weeks, persistent notes editing, adding weeks, and shifting the calendar start date)
- ✅ Notizen
- ✅ Verbessern / Improvements list (type, target, value, source, active status). "Löschen" now
  works for `Custom`-sourced entries (matches legacy's own cmdDeleteImprovement_Click, which only
  ever lets the user delete manually-created Improvements - everything else is a side effect of
  some other owned item, e.g. a Quality or Cyberware, and gets removed by removing that item
  instead). Also fixed a real bug found while wiring this: the ListBox's SelectedItem binding
  wasn't Mode=TwoWay, so selecting a different row never actually updated the detail pane after
  the initial load. Manual Improvement *add* is now ported too, for a curated 13-type subset - see
  the Backlog section's own entry for the full writeup.

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
  Manual Improvement *add* is now ported too (curated subset, see Backlog section); most other
  operations remain unwired.

## Item picker dialogs (`frmSelectXxx` equivalents)

- 🟡 19 of ~41: selected-item flows exist for Quality, Spell, Gear, Cyberware/Bioware, Armor,
  Weapon, Weapon Accessory, Weapon Mod, Vehicle, Vehicle Mod, Lifestyle, exotic Skills, Martial
  Art, Martial Art Maneuver, Adept Power, Metamagic, CritterPower, ComplexForm, and
  ContactConnection (the last of these isn't a standard "pick an item" flow - it's
  `ContactGroupDialog`'s Group Network rating calculator: Membership/Area of Influence/Magical/
  Matrix Resources + group name/colour/free flag, wired to `UpdateContactGroup`). The
  implementations remain deliberately scoped (for example, no advanced lifestyle construction).
  Quality/Adept Power/Metamagic/Cyberware/Bioware/CritterPower/ComplexForm additions all apply
  their rules-data `<bonus>` Improvements now - see the bonus-application engine note below. Weapon
  Accessory/Mod additions now validate mount-slot eligibility, ported from
  `frmCareer.cs`'s `tsWeaponAddAccessory_Click`/`tsWeaponAddModification_Click`: accessories are
  rejected unless the weapon's `weapons.xml` entry allows accessories and lists a matching
  `<accessorymounts><mount>`, and mods are rejected if the weapon disallows mods, or - under the
  EnforceCapacity house rule - if installed mods' slots plus the new one would exceed the fixed
  6-slot cap every weapon has (`clsEquipment.cs`'s `Weapon.SlotsRemaining` hardcodes this as a
  constant, not a per-weapon data field). Neither check enforces exclusivity between multiple
  accessories/mods sharing the same mount, matching legacy.
- ❌ The remaining pickers - see the "Item picker dialogs" row in the Backlog section above for
  the full audited list (`frmSelectArmorMod`, `frmSelectCyberwareSuite`, `frmSelectMentorSpirit`,
  `frmSelectNexus`, `frmSelectPACKSKit`, `frmSelectProgramOption`, `frmSelectSide`,
  `frmSelectSkillCategory`, `frmSelectSkillGroup`, `frmSelectSpellCategory`). Skill beyond exotic
  skills doesn't need one - active/knowledge skills come from a fixed list plus freeform
  knowledge-skill entries.

## Derived stats / calculations

- ✅ Essence
- ✅ Condition monitor size (Physical/Stun), live damage-box display, and persisted heal/damage controls
- ✅ Armor encumbrance penalty
- ✅ Skill dice pools, including skill-rating augmentation display
- ✅ Weapon dice pools (category→Active Skill mapping, Smartgun System bonus, specialization
  match), shown in the Waffen tab's detail pane and included in the print sheet. Installed
  Accessory/Mod dice pool bonuses are now included too, ported from clsEquipment.cs's
  Weapon.DicePool: each installed Accessory's/Mod's own rules-data `<dicepool>` value (a plain
  integer, or `Rating`/`-Rating` for Mods scaling with their own Rating, reusing the existing
  `RatingExpression` evaluator) is looked up by name from weapons.xml and summed in, with each
  contributor listed in the tooltip. Verified against real data: Red Dot Sight's flat `+1` and
  Weapon Focus's Rating-scaled bonus (careful to look up the correct one when an Accessory and a
  Mod share a name, e.g. "Laser Sight" exists as both with different `<dicepool>` values). Loaded-
  ammo pool bonuses remain unported - the "which Gear item is loaded" concept itself now exists
  (see `frmReload` in the Backlog section), but very few gear.xml entries actually use this bonus.
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
  multiplier already is. `AllowExceedAttributeBp` is now honored too - `NewCharacterFactory` now
  persists a `startingbuildpoints` field (the starting BP/Karma total, unlike `Bp`/`Karma` which
  shrink as points are spent, exposed as `CharacterDocument.StartingBuildPoints`), and
  `RaiseAttributeCreate` gates each raise of the 8 primary attributes against
  `ComputeAttributeCreatePointsSpent` (which recomputes points-already-spent per attribute using
  the exact same per-step cost formulas `RaiseAttributeCreate`/`LowerAttributeCreate` already
  charge/refund, so it can't drift) plus the new raise's own cost exceeding half the starting
  total - ported from frmCreate.cs's `nud<Attribute>_ValueChanged` handlers. Older save files (or
  any character not created through `NewCharacterFactory`) have no `startingbuildpoints` and are
  left unrestricted, matching the "nothing to check against" case. Not ported: the
  `SpecialAttributeKarmaLimit` sub-house-rule (whether EDG/MAG/RES count toward the same cap) -
  the primary-attribute-only cap is the common case and already a real, working restriction.
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
  edit in this app). Extended to the Cyberware/Bioware tree too - `MoveCyberware`/
  `GetCyberwareNodeById` (both trees share one global `CyberwareId` numbering over the underlying
  `<cyberwares>` list, same scheme as `GearId`/`MoveGear`) plus a `CyberwareId` field added to
  `TreeNodeViewModel`/`CharacterTreeItemData`, and `CyberwareSectionTab`'s drag/drop wiring is a
  direct copy of `GearSectionTab`'s. Not extended to the Weapons/Armor trees, which still don't
  support drag/drop at all.

## Platform / packaging

- ❌ AppImage or other Linux distribution packaging (`docs/LINUX_PORT_PLAN.md` Phase 5)
- 🟡 Startup/runtime crashes have been fixed as found (data path layout, asset casing) but there's
  no smoke-test automation beyond manual `dotnet build` + kill-timeout runs done ad hoc in this
  session

## Test infrastructure

- ✅ `Chummer.Tests` has real coverage for `CharacterFileService`; targeted `dotnet test` runs
  successfully in this workspace. The project still emits a known `System.Net.Http` MSBuild
  conflict warning while building tests.
