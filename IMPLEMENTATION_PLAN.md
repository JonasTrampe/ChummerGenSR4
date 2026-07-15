# Implementation Plan: Refactoring "Improved Sense" Power

## Objective
Refactor the "Improved Sense" power to replace the generic `selecttext` with a specialized `selectcyberware` mechanism. This mechanism will allow users to select specific cyberware (eyewear or earware) from a filtered list (excluding technical/radio items) and ensure that the selected item's sensory bonuses are correctly applied to the character.

## 1. Analysis Phase
### 1.1 Data Structure Analysis
*   **File**: `Chummer\data\data\powers.xml`
*   **Goal**: Understand the current implementation of "Improved Sense" and how `selecttext` is being used in the XML.

### 1.2 Implementation Analysis
*   **File**: `Chummer\code\clsImprovement.cs`
*   **Goal**: Analyze the current `selectcyberware` implementation. Determine how it handles category filtering and how it interacts with the UI and data model.

### 1.3 Cyberware Data Analysis
*   **File**: `Chummer\data\data\cyberware.xml`
*   **Goal**: 
    *   Identify the data attributes used to categorize items (e.g., `eyewear`, `earware`).
    *   Identify the mechanism for marking items as "ignored" (e.g., a new XML tag or attribute).
    *   Identify how to distinguish "technical" items (like radar) from sensory items.
    *   Identify which bonuses are considered "sensory bonuses" (e.g., those affecting perception-related attributes or skills).

### 1.4 Schema Verification
*   **File**: `Chummer\data\data\powers.xsd` and `Chummer\data\data\cyberware.xsd`
*   **Goal**: Check if the existing schemas support:
    *   The new "ignored" flag for cyberware.
    *   Additional attributes for advanced filtering in `selectcyberware` (e.g., multiple categories or exclusion lists).

## 2. Proposed Changes
### 2.1 XML Modification
*   **Target**: `Chummer\data\data\powers.xml`, `Chummer\data\data\cyberware.xml`, and relevant XSD files.
*   **Action**: 
    *   Add an "ignored" flag to `cyberware.xml` to mark technical/non-sensory items.
    *   Replace `<selecttext />` within the "Improved Sense" power definition with a `<selectcyberware />` element.
*   **New Attributes/Elements**: Implement/use attributes to:
    *   Filter by multiple categories (e.g., `categories="eyewear, earware"`).
    *   Filter out items marked with the "ignored" flag.

### 2.2 Code Logic Update
*   **Target**: `Chummer\code\clsImprovement.cs`
*   **Action**:
    *   **Filtering Logic**: Update the UI/selection logic to respect the new filtering attributes (categories and "ignored" flag) from the XML.
    *   **Data Application**: Implement logic to ensure that when an item is selected, **only its sensory bonuses** (e.g., perception/sensory-related attributes or skills) are applied to the character.

## 3. Verification Phase
### 3.1 Schema Validation
*   **Action**: Ensure the modified XML structure remains compliant with the updated XSD schemas.

### 3.2 Functional Testing
*   **Action**:
    *   Verify the "Improved Sense" selection dialog in the UI displays only the allowed cyberware (correct categories, no "ignored" items).
    *   Confirm that selecting a specific item correctly updates the character's attributes/skills, applying only the intended sensory bonuses.
    *   Ensure "technical" items (like radar) are correctly excluded from the list.

