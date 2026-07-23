# Linux/Avalonia port — next-phase plan

Snapshot: `Chummer.Core` now has a real Improvement engine (Phase 1 ✅) and computes most of
the derived stats the Avalonia UI shows through it (Phase 2 ✅) — attribute augmentation,
Essence, condition-monitor track size, encumbrance, skill dice pools, adept power point cost,
attribute karma-cost-to-increase, and Gear/Weapon cost & availability all read through
`Improvements`/`ImprovementManager` or a Rating-substituting expression evaluator instead of a
raw XML passthrough. What's left in that vein: Vehicles are still a flat name+category stub with
no mods/gear/cost tree (tracked as its own item below), and there's still no broad typed-model →
XML write path — Quality, Spell, Gear (root-level), and Karma/Nuyen mutations prove the pattern,
but most of the legacy game-logic (character creation math, item picker rules-data lookups) still
lives in the legacy WinForms files (`clsCharacter.cs` 6.2k lines, `clsUnique.cs` 7.4k,
`clsEquipment.cs` 15.7k) and in ~41 `frmSelectXxx` picker dialogs plus the two giant host forms
`frmCareer.cs`/`frmCreate.cs` (27.6k/23.6k lines).

That gap — "reads and displays" vs. "computes and edits" — is the real porting boundary.
One notable exception now exists: the Avalonia shell already has a real Cloud Documents dialog
backed by the shared RunnersPoint API/auth code, including folders, revisions, metadata, and
upload/download flows. Cloud parity is therefore no longer a "not started" area; it is a
partially-complete feature stream that still needs polish and some legacy-flow parity.
Everything below is sequenced around closing it a slice at a time, always keeping the app
buildable and runnable at each step.

## Phase 1 — Improvement engine (the thing everything else depends on) ✅

Port `clsImprovement.cs` (`Improvement` + `ImprovementManager`) into `Chummer.Core` first.
Nearly every derived stat in Shadowrun 4e — attribute bonuses, skill bonuses, dice pool
bonuses, condition-monitor boxes, essence effects, encumbrance penalties — is expressed as an
Improvement. Porting this before anything else means later steps (Essence, dice pools, karma
costs) have a real engine to sit on instead of each reinventing a slice of it.

- Step 1.1: Port the `Improvement` data model + XML load/save round-trip (read-only to start). ✅
- Step 1.2: Port `ImprovementManager`'s aggregation/query methods (sum bonuses by type+target). ✅
  `ValueOf`/`AugmentedValueOf`/`DescribeValueOf`/`DescribeAugmentedValueOf`, with UniqueName
  highest-value dedup ported; precedence0/1 overrides and the Technomancer/Gear
  MatrixInitiativePass exclusion are deliberately not (documented as follow-ups).
- Step 1.3: Wire `CharacterDocument` to expose computed values through it instead of raw XML
  reads, starting with the fields the UI already shows (attribute augmented values, essence). ✅
  Along the way, found and fixed a real bug: `Improvement.Load`'s `Enum.TryParse` was
  case-sensitive, so the legacy save format's `PhysicalCM` (capital M) silently failed to match
  `ImprovementType.PhysicalCm` and got marked disabled — every save with that Improvement was
  quietly losing its condition-monitor bonus.

## Phase 2 — Derived stats in Core (Essence, condition monitor, encumbrance, dice pools) ✅

With Phase 1 in place, port the calculation methods out of `clsCharacter.cs` /
`clsUnique.cs` / `clsEquipment.cs`:

- Step 2.1: Essence calc. ✅ Sums each Cyberware/Bioware item's already-computed `<ess>` (split
  by source, Essence Hole tracked separately), applies the higher-in-full/lower-at-half layering
  rule, adds Essence-type Improvements on top of the ESS attribute's metatype maximum. Doesn't
  re-derive `Cyberware.CalculatedESS`'s own grade-multiplier/discount/rating-cost formula (reads
  the save's resolved value instead) or the CyborgEssence fixed-at-0.1 override.
- Step 2.2: Physical/Stun condition monitor track size. ✅ `ComputePhysicalCm`/`ComputeStunCm`.
  A.I./technocritter/protosapient special cases aren't ported (Core doesn't read metatype
  category yet).
- Step 2.3: Armor/ballistic/impact encumbrance and displayed worn armor ratings. ✅
- Step 2.4: Skill dice pools (Skill + Attribute + Improvement + gear bonuses). ✅
  `ComputeSkillDicePool` — Skill/SkillGroup/SkillCategory contributions, rating vs. pool-only
  bonuses, wound modifiers, specialization note.
- Step 2.5: Adept power point cost, attribute karma-cost curves, Gear/Weapon availability &
  cost trees. ✅ `CharacterPowerData.TotalPoints` (Adept Way/Geas discounts);
  `CharacterAttributeData.KarmaCostToIncrease` (wired through a newly-loaded `CharacterOptions` —
  along the way, fixed a settings-file path bug: `CharacterOptions.Load()` expects profiles at
  `<app>/settings/*.xml`, but Avalonia/Tests were only copying them to `<app>/data/settings/*.xml`,
  so a character's referenced settings/book list was silently falling back to hardcoded defaults);
  `CharacterTreeItemData.CalculatedCost`/`CalculatedAvail` (Rating-substituted expression
  evaluator via `XPathNavigator`, same technique as Essence — works for both raw formulas (Gear)
  and pre-resolved values (Weapon/accessories/mods) with no rules-data XML lookup needed, since
  the character file already carries whichever form that item type saves). **Vehicles are still
  a flat name+category stub with no mods/gear/cost tree** — tracked as its own follow-up, not
  a quick extension of the above (needs a proper `CharacterTreeItemData` tree plus Avalonia
  `VehiclesSectionTab`/`ViewModel` changes to consume it).

Each step landed as: Core method + a couple of xUnit tests against a known save file, then
(where a UI slot already existed) one Avalonia section tab wired to stop showing a hardcoded
number — e.g. `GeneralSectionViewModel`'s attribute row now shows the real augmented value
instead of the raw `TotalValue` passthrough. Several of these (karma cost, gear cost/avail)
don't have a consuming UI slot yet since the relevant buttons/panels haven't been built in
Avalonia — the Core computation is ready for when they are.

## Phase 3 — A real write path

`CharacterFileService.Save()` preserves mutations made directly to its backing `XmlDocument`.
There is still no broad typed-model → XML layer, but Quality, Spell, and Karma/Nuyen mutations
now prove a safe incremental write pattern that survives save/reload.

**Fixed along the way:** `Save()` called plain `XmlDocument.Save(Stream)`, which re-indents with
wider whitespace and expands empty elements (`<bonus />` → `<bonus>\n\t</bonus>`) on every write —
a real character file that round-tripped through this path (cloud download/reload) grew from
1.5MB to 2.1MB with zero content change. Now writes through an explicit `XmlWriter` configured to
match `clsCharacter.cs`'s legacy format (tab indent, UTF-16, `CloseOutput = false` so callers that
read the stream back immediately still work).

- Step 3.1: Pick one simple, low-risk mutation to prove the pattern end-to-end: adding a
  Quality. Port `Quality.Save(XmlWriter)` semantics into Core, add
  `CharacterDocument.AddQuality(...)`, wire `GeneralSectionTab`'s "Gabe hinzufügen" button to
  it, confirm round-trip save/reload preserves it. ✅ Selected Qualities can also be deleted.
- Step 3.1a: Spell add/delete is now the second end-to-end mutation, including its real picker and
  a Core save/reload test. ✅
- Step 3.2: Repeat for Karma/Nuyen expense entries (also simple, and the
  `KarmaNuyenSectionTab` "verdient"/"ausgegeben" buttons are already stubbed for it).
- Step 3.3: Once the pattern is proven, generalize to gear/cyberware/spells/contacts — these
  are all "append a `<foo>` node with these fields" in the legacy `Save()`, same shape as
  Quality. 🟡 Spell and Gear are complete (Gear's quantity/containment semantics still need
  porting). Cyberware/Bioware now has a tested Core `AddCyberware`/`RemoveCyberware` API (shared
  `<cyberwares>` list, split by `<improvementsource>`) plus a real Avalonia picker
  (`CyberwareDialog`, reads `cyberware.xml`/`bioware.xml`) wired into `CyberwareSectionTab`'s
  add/delete buttons and live Essence-consumed totals, plus a Grade selector
  (Standard/Alphaware/Betaware/Deltaware and their Second-Hand/Adapsin variants) that live-
  multiplies Essence/cost/availability from each file's `<grades>` data. Weapon now has the same
  `AddWeapon`/`RemoveWeapon` + `WeaponDialog` treatment, wired into the previously-dead "Waffe
  hinzufügen"/"Löschen" buttons in `GearSectionTab`'s Waffen sub-tab (no accessory/mod cost math,
  no STR-substituted damage code resolution). Contacts and Armor still need their own write path
  - Armor's "Panzerung hinzufügen" button is still dead, same shape as Weapon was before this.

## Phase 4 — Item picker dialogs (the `frmSelectXxx` → real Avalonia dialogs)

~41 files, no shared base class in the legacy code, but a uniform pattern: constructor takes
the current character for context/filtering, caller does a modal show, reads a
`SelectedXxx` property back on OK. That pattern maps cleanly onto a small Avalonia dialog
service (`Task<T?> ShowDialogAsync<TDialog, T>(...)`).

- Step 4.1: Build the dialog-service scaffolding once (one shared "picker" pattern: a
  filterable list + detail pane, matching the look the mockup dialogs already have).
- Step 4.2: Wire `QualityDialog` for real (list from `qualities.xml` via `XmlManager`, filter
  by already-taken/incompatible qualities, return the selection) — it's the one button already
  wired to open a dialog with nothing behind it, so it's the natural first target.
- Step 4.3: `SpellDialog` next (same shape, categories already modeled in `CharacterTab`'s
  spell tree). ✅ The picker reads `spells.xml`, filters already-known spells, and adds the
  selected spell to character XML; the tab also supports deletion, and save/reload preserves changes.
- Step 4.4: Prioritize the rest by how often they're needed for a usable character: Gear,
  Cyberware, Weapon, Armor before the long tail (Nexus, CritterPower, AdvancedLifestyle, ...).
  🟡 Gear now has a rules-data picker and root-level add/delete/save/reload flow; its rating,
  quantity, and containment semantics still need porting.

## Phase 5 — Character creation flow

`frmCreate.cs` (23.6k lines) is a separate, larger beast from career mode — priority piggy on
Phase 4's picker dialogs since creation is mostly "pick things and spend build points." Treat
as its own later milestone once enough pickers exist to make it useful; don't block on
finishing all 41 first.

## Phase 6 — Print/character-sheet output

XSLT-based (`Chummer/data/sheets/*.xsl`, `XslCompiledTransform` over an exported character
XML), triggered from `frmCareer`'s print toolbar/menu. Nothing in Core produces that export
XML yet. This only becomes worth starting once Phase 2/3 make the character data
trustworthy — otherwise the printed sheet just reproduces the same "reads the raw file"
limitation the UI has today. `SheetPreviewDialog` already exists as a mockup entry point.

## Modernization steps (independent of feature parity, can interleave anywhere above)

These don't block porting but are worth doing as the surrounding code is touched anyway,
rather than as a separate cleanup pass later:

- **Kill the Hungarian notation as files get ported.** Every `cls`/`frm`-prefixed legacy file
  that gets ported to Core should drop `_str`/`_int`/`_bln`/`_lst` prefixes and `cls`-prefixed
  type names in favor of plain C# (the way `CharacterFileService.cs` and `LanguageManager.cs`
  already did) — don't carry the naming convention forward into new Core code.
- **Nullable reference types.** Chummer.Core doesn't have `<Nullable>enable</Nullable>` yet;
  turning it on (even file-by-file with `#nullable enable`) before Phase 1/2 land a lot of new
  Core code would catch a class of bug for free, especially around the XML-node-missing cases
  this codebase currently handles with try/catch-and-ignore.
  - `CharacterAttributeData`, `CharacterQualityData` etc. already use `{ get; private set; }`
    with mutable setters left over from the old Load()-mutates-fields pattern — worth making
    genuinely immutable (`{ get; }` + constructor-only) as each gets touched.
- **Replace `XmlDocument`/XPath string-building with `System.Xml.Linq` (`XDocument`)** as each
  section of `CharacterFileService` gets extended for Phase 3's write path — string-concatenated
  XPath queries like `"/character/cyberwares/cyberware[improvementsource = '" + x + "']"` are
  exactly the kind of thing that's easy to get subtly wrong and `XDocument`/LINQ-to-XML reads
  much more safely.
- **Async file I/O consistency.** `CharacterFileService.Load/Save` are synchronous over a
  `Stream` that's already opened async in `MainWindow.axaml.cs`; fine at current file sizes,
  but worth flagging if large `.chum` files or the cloud-save (`RunnersPointApiClient`) path
  end up sharing this code.
- **Replace remaining `MessageBox`/`Application.Exit()`-shaped error handling patterns** (the
  legacy `LanguageManager` had exactly this, already stripped) — as more legacy classes get
  ported, watch for the same "pop a dialog and kill the process" pattern and replace it with
  something the caller can react to (return value / exception the UI layer decides how to
  surface, as done with `MainWindow`'s new `ErrorStatus` line).
- **Test coverage as a porting gate, not an afterthought.** `Chummer.Tests/Fixtures/sample.chum`
  (a small, hand-built fixture, not a real anonymized save) now backs every Phase 1/2
  `CharacterFileServiceTests` addition alongside the existing `RunnersPointApiClient`/`Auth`
  coverage — keep landing every new Core computation with a test against it or an inline
  `LoadXml(...)` snippet, not as an afterthought.

## Suggested order of attack

1. ~~Nullable + test-fixture prep~~ ✅ (fixture exists, used throughout Phase 1/2)
2. ~~Phase 1 (Improvement engine)~~ ✅
3. ~~Phase 2 (Essence → CM → encumbrance → dice pools → costs)~~ ✅ except the Vehicles tree
   follow-up noted above
4. Phase 3.3 next: generalize the proven write path (Quality/Spell/Karma-Nuyen/Gear-root) to
   cyberware and contacts, and finish Gear's quantity/containment semantics
5. Phase 4.1–4.3 (dialog service + the two already-half-wired dialogs)
6. Everything else (remaining pickers, creation flow, print pipeline, Vehicles tree)
   opportunistically, prioritized by what a usable end-to-end "open → edit → save → reopen"
   character loop needs
