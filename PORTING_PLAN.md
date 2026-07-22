# Linux/Avalonia port — next-phase plan

Snapshot: `Chummer.Core` currently round-trips the raw save XML and exposes read-only,
string-based projections of it (attributes, qualities, gear, skills, spells, contacts,
expenses, etc.). Nothing in Core computes anything — every derived number the Avalonia UI
shows today (Essence, dice pools, condition-monitor size, karma costs) is either a value
read straight out of the save file or hardcoded mockup text. All the actual game-logic lives
in the legacy WinForms files (`clsCharacter.cs` 6.2k lines, `clsUnique.cs` 7.4k,
`clsEquipment.cs` 15.7k, `clsImprovement.cs` 2.9k) and in ~41 `frmSelectXxx` picker dialogs
plus the two giant host forms `frmCareer.cs`/`frmCreate.cs` (27.6k/23.6k lines).

That gap — "reads and displays" vs. "computes and edits" — is the real porting boundary.
One notable exception now exists: the Avalonia shell already has a real Cloud Documents dialog
backed by the shared RunnersPoint API/auth code, including folders, revisions, metadata, and
upload/download flows. Cloud parity is therefore no longer a "not started" area; it is a
partially-complete feature stream that still needs polish and some legacy-flow parity.
Everything below is sequenced around closing it a slice at a time, always keeping the app
buildable and runnable at each step.

## Phase 1 — Improvement engine (the thing everything else depends on)

Port `clsImprovement.cs` (`Improvement` + `ImprovementManager`) into `Chummer.Core` first.
Nearly every derived stat in Shadowrun 4e — attribute bonuses, skill bonuses, dice pool
bonuses, condition-monitor boxes, essence effects, encumbrance penalties — is expressed as an
Improvement. Porting this before anything else means later steps (Essence, dice pools, karma
costs) have a real engine to sit on instead of each reinventing a slice of it.

- Step 1.1: Port the `Improvement` data model + XML load/save round-trip (read-only to start).
- Step 1.2: Port `ImprovementManager`'s aggregation/query methods (sum bonuses by type+target).
- Step 1.3: Wire `CharacterDocument` to expose computed values through it instead of raw XML
  reads, starting with the fields the UI already shows (attribute augmented values, essence).

## Phase 2 — Derived stats in Core (Essence, condition monitor, encumbrance, dice pools)

With Phase 1 in place, port the calculation methods out of `clsCharacter.cs` /
`clsUnique.cs` / `clsEquipment.cs`:

- Step 2.1: Essence calc (`Essence`, `CyberwareEssence`, `BiowareEssence`, `EssenceHole`) —
  replaces the `Condition.Essence` passthrough already wired into `CharacterSidebar`.
- Step 2.2: Physical/Stun condition monitor track size — replaces the hardcoded `Value="10"`
  in `CharacterSidebar.axaml`.
- Step 2.3: Armor/ballistic/impact encumbrance and displayed worn armor ratings. ✅
- Step 2.4: Skill dice pools (Skill + Attribute + Improvement + gear bonuses) — this is the
  one the `SkillRow`/`InfoRow` "Würfelpool" placeholders in `SkillsSectionTab` are waiting on.
- Step 2.5: Adept power point cost, attribute karma-cost curves (`clsUnique.cs`), Cyberware/
  Bioware essence cost, Gear/Weapon/Vehicle availability & cost trees (`clsEquipment.cs`).

Each step should land as: Core method + a couple of xUnit tests against a known save file,
then one Avalonia section tab wired to stop showing a hardcoded number.

## Phase 3 — A real write path

`CharacterFileService.Save()` preserves mutations made directly to its backing `XmlDocument`.
There is still no broad typed-model → XML layer, but Quality, Spell, and Karma/Nuyen mutations
now prove a safe incremental write pattern that survives save/reload.

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
  Quality. 🟡 Spell is complete; Gear now has a tested Core add/remove/save/reload API plus a
  root-level picker/UI flow. Its quantity and containment semantics still need porting.

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
- **Test coverage as a porting gate, not an afterthought.** `Chummer.Tests` currently only
  covers `RunnersPointApiClient`/`Auth`. Every Phase 1–3 Core addition should land with a test
  against a real (anonymized) `.chum` fixture — there isn't one in the repo yet, so creating a
  small fixture file is a prerequisite step before Phase 1 starts, not a nice-to-have.

## Suggested order of attack

1. Nullable + test-fixture prep (half a day, unblocks everything else)
2. Phase 1 (Improvement engine) — biggest unblock, do it once
3. Phase 2 in the order listed (Essence → CM → encumbrance → dice pools → costs)
4. Phase 3.1–3.2 (prove the write path on Quality + expenses)
5. Phase 4.1–4.3 (dialog service + the two already-half-wired dialogs)
6. Everything else (remaining pickers, creation flow, print pipeline) opportunistically,
   prioritized by what a usable end-to-end "open → edit → save → reopen" character loop needs
