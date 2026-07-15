# Implementation Plan: "Improved Sense" via `selectcyberware`

## Decisions locked in
- **Eligibility filter**: new data flag `<senseimprovement>yes</senseimprovement>` on qualifying `cyberware.xml` entries (not a category-only filter, not a C# name blacklist). Extending the sense list later is a data edit, not a code change.
- **Rating**: granted bonus defaults to Rating 1. A houserule toggle (`chummer.xml` options, mirroring existing houserule booleans) lets a group opt into the item's full rating bonus instead — checked at improvement-apply time, not baked into the data.

## 1. Data changes

### 1.1 `Chummer/data/data/cyberware.xsd`
Add optional `<xs:element name="senseimprovement" type="xs:string" minOccurs="0" maxOccurs="1" />` to the cyberware complexType (string "yes"/"no" pattern, matching how other boolean flags like `<levels>` are already modeled in this codebase).

### 1.2 `Chummer/data/data/cyberware.xml`
Tag entries with `<senseimprovement>yes</senseimprovement>`:
- Eyeware: Low-Light Vision, Thermographic Vision, Flare Compensation, Microscopic Vision, Vision Magnification, Vision Enhancement.
- Earware: Audio Enhancement, Balance Augmenter, Increased Sensitivity, Spatial Recognizer, Select Sound Filter, Damper.

Explicitly NOT tagged (stay filtered out): Cybereyes/Cyberears base systems, Eye/Ear Recording Unit, Eye Laser Designator/Microphone/Range Finder/System, Eye Tool Laser, Ocular Drone, Retinal Duplication, Protective Covers, Single Cybereye, Smartlink, Eyeband, Sound Link, Radio (2050), Telephone (2050), Sound Recorder (2050), Cosmetic Modification variants — these are combat, comms, recording, or cosmetic items, not senses.

If other sourcebooks in this data file add more Eyeware/Earware later, tagging is the only step needed to include them — no code change.

### 1.3 `Chummer/data/data/powers.xsd`
Extend the existing `<selectcyberware>` complexType:
- Change `cyberwarecategory` to accept a comma-separated list (e.g. `"Eyeware,Earware"`) — keep the attribute name/type so existing single-category usages (if any) still parse.
- Add optional attribute `requiresenseimprovement` (`xs:string`, "yes"/"no"), defaulting to "no" for backward compatibility with any other future use of `selectcyberware` that isn't sense-related.

### 1.4 `Chummer/data/data/powers.xml`
Replace the "Improved Sense" power's bonus:
```xml
<bonus>
    <selectcyberware cyberwarecategory="Eyeware,Earware" requiresenseimprovement="yes" />
</bonus>
```

## 2. Code changes — `Chummer/code/clsImprovement.cs`

### 2.1 Clean up existing partial edit
The prior edit that added `SelectCyberware = 89` and its switch case left broken indentation and a stray closing brace (enum block ~line 101-105, switch block ~line 320-345). Fix formatting as part of this change, don't layer more on top of it.

### 2.2 Add selection + application logic
In the bonus-processing method where `selecttext`/`selectweapon` nodes are handled (same method around clsImprovement.cs:1174 and the career/create duplicate path around 2400):
1. Detect `nodBonus["selectcyberware"]`.
2. Read `cyberwarecategory` attribute, split on `,` into a category list.
3. Read `requiresenseimprovement` attribute.
4. Load `cyberware.xml` via the existing `XmlManager`/`XmlDocument` pattern already used elsewhere in this file, `.SelectNodes("/chummer/cyberwares/cyberware")`, filter to nodes whose `<category>` is in the category list, and (if `requiresenseimprovement="yes"`) whose `<senseimprovement>` == "yes".
5. Present the filtered set to the user (see UI section) and get back the selected `XmlNode`.
6. Determine the rating to apply: 1, unless the houserule toggle (2.4) is enabled and the item defines a `<rating>`, in which case use that max rating.
7. If the selected node has a `<bonus>` child, feed that XmlNode into the same bonus-application routine this method already uses for its own `nodBonus` (recursive call), substituting `Rating` in any bonus values with the value from step 6 — mirror how `applytorating`/`Rating` substitution is already done elsewhere in this file for cyberware bonuses.
8. Store the selected cyberware's `<name>` as the improvement's display value (same field `SelectWeapon`/`SelectText` already use) so it shows in the character sheet/improvement list and can be removed when the power is removed.

### 2.3 Load/Save & legacy compatibility
Existing saved characters have `Improved Sense` stored via `selecttext`'s free-text value. Load() must not crash on old save files — keep the `selecttext` load path intact and unreferenced from powers.xml going forward; it's dead only for *new* selections, not for reading old ones.

### 2.4 Houserule toggle
Add a boolean option (e.g. `_blnImprovedSenseFullRating`) to the character-options class alongside the other houserule flags, with an options-dialog checkbox, defaulting to off (rating 1 behavior). Reference it in step 2.2.6.

## 3. UI

Build a small dedicated picker rather than reusing `frmSelectCyberware` (that form is for purchasing/installing cyberware — essence, cost, grade, capacity — none of which applies here). Pattern it after `frmSelectText`: title/description label, a `ListBox` populated with the filtered item display names (+ source/page), OK/Cancel. Wire it into the single point in clsImprovement.cs identified in 2.2 (shared by `frmCreate.cs` and `frmCareer.cs`, so no duplicate UI wiring needed).

Add language keys to `en-us.xml` (and leave placeholders for other language files, consistent with how other recent additions were handled): `Title_SelectSenseCyberware`, `String_Improvement_SelectSenseCyberware`.

## 4. Verification
1. Validate `powers.xsd`/`cyberware.xsd` changes: existing `powers.xml`/`cyberware.xml` still parse without schema errors.
2. Manual test in-app:
   - Take "Improved Sense", confirm the picker lists exactly the 12 tagged items and nothing else (no Radio, Telephone, Smartlink, Cyberears base, recording units, weapon items).
   - Select "Low-Light Vision" → confirm the character gets the sense with no essence cost and no cyberware-list entry.
   - Select "Vision Enhancement" → confirm a Rating-1 Perception (Visual) bonus is applied; toggle the houserule and confirm it switches to the item's max rating.
   - Take the power twice (limit 100 allows multiple purchases) with two different senses, confirm both bonuses stack independently and removing one power instance removes only its bonus.
   - Load an old save file with a legacy `selecttext`-based Improved Sense entry, confirm it still loads and displays without error.
3. Regression: confirm other powers/cyberware bonuses that share the bonus-application method still apply correctly (nothing in the recursive-call change should affect non-selectcyberware bonuses).

## Open items to confirm during implementation
- Exact XPath/helper used elsewhere in this file for loading `cyberware.xml` (there may already be a shared helper — reuse it rather than opening the file directly, to match existing conventions).
- Confirm character-options class name/location for the houserule flag (follow the existing pattern of the nearest similar boolean option).
