# ChummerGenSR4 – Linux/Cross-Platform Port Plan

## Current Status (as of 2026-07-20)
**Target UI Framework:** Avalonia (Cross-platform)
**Status:** 
- [x] **Phase 0 (Legacy Cleanup):** WCF/Omae/Update legacy removed/modernized. REST API implemented.
- [x] **Phase 1 (Settings Migration):** `SettingsStore` implemented and migrated.
- [x] **Phase 2 (Mono Hardening/Intermediate):** Completed (essential for stability and verifying logic before the full rewrite).
- [ ] **Phase 3 (The Big Refactor):** Model-factory `TreeNode` decoupling is complete; incrementally move the resulting UI-agnostic model code into `Chummer.Core`.
- [ ] **Phase 4 (Avalonia Rewrite):** Migrating the UI layer from WinForms to Avalonia (leveraging the pure core).
- [ ] **Phase 5 (Packaging):** AppImage/Linux distribution.

---

## Historical Context & Decision Process

Initially, a choice was presented between a Mono-based port and an Avalonia rewrite. 
- **Mono Path:** Pragmatic, low effort, but relies on legacy technology and provides no native macOS/modern .NET benefits.
- **Avalonia Path:** Significant effort (UI rewrite), but provides a modern, truly cross-platform (Windows/Linux/macOS) solution using a modern UI stack.

**Decision:** **Avalonia** was chosen after a successful risk-area spike. The project moved to the `feature/linux-port` branch.

---

## The Primary Technical Blocker: Model-UI Coupling

During the transition to a shared `Chummer.Core` library, a critical structural issue was discovered:

**The Issue:** Many domain models (`clsEquipment.cs`, `clsUnique.cs`, etc.) have `Create()` methods that directly instantiate and manipulate WinForms-specific objects like `TreeNode` and `ContextMenuStrip` as side effects of object construction.

**The Impact:** This prevents the core logic from being compiled into a pure, UI-agnostic `Chummer.Core` library, which is required for the Avalonia target.

**The Solution (In Progress):** Implement an MVVM-like separation. The `Create()` methods in the core model must be refactored to be "pure" (constructing only the data object and metadata). The responsibility of building the UI representation (e.g., a TreeView item in Avalonia) is moved to the UI layer (ViewModels or the View itself).

**Completed slice:** No domain-model `Create()` or `Copy()` method accepts or creates a
`TreeNode`, `TreeView`, or `ContextMenuStrip`. `Character`, `Quality`, and the equipment-model
factories are UI-agnostic; the legacy WinForms call signatures are retained as adapters in
`WinFormsEquipmentTreeExtensions.cs`. Dedicated WinForms helpers (`CommonFunctions`, language
translation, and list sorting) continue to own tree rendering and interaction.

**Current shared-services slice:** Cloud document DTOs, authentication (DPAPI on Windows and
libsecret on Linux where available), endpoint options, and the `IRunnersPointApiClient` contract
are in `Chummer.Core`. The WinForms cloud screens now consume the interface; the HTTP client
implementation remains in the legacy application while its JSON/logging dependencies are removed
or made shared.

---

## Detailed Phase Plan (Ongoing)

### Phase 3 – Core Model Refactoring (The Blocker)
The factory-decoupling audit is complete. The current work is incremental extraction of the
UI-agnostic model code into `Chummer.Core`, keeping dedicated WinForms helpers in the legacy
application. The model types involved include:
- `Quality`, `Spell`, `Metamagic`, `TechProgram`, `Art`, `MartialArtAdvantage`, `MartialArtManeuver`, `Power`, `Armor`, `ArmorMod`, `Cyberware`, `Weapon`, `WeaponAccessory`, `WeaponMount`, `Lifestyle`, `Gear`, `Vehicle`, etc.
- **Approach:** Refactor one entity type at a time.
- **Validation:** Ensure `ChummerGenSR4.sln` still builds and the existing WinForms app remains functional (using a shim or simply updating the WinForms side to handle the new "pure" objects).

### Phase 4 – Avalonia UI Rewrite
Once `Chummer.Core` is pure, the WinForms UI is replaced with Avalonia.
- **Key Challenges:** 
    - MDI (Main window) $\to$ Tabs.
    - WebBrowser/Sheet Preview $\to$ Embedded Chromium WebView.
    - Custom GDI+ drawing $\to$ Avalonia primitives/Skia.
    - Drag-and-drop reordering (Gear/Cyberware).
- **Strategy:** Use the `Chummer.Core` logic to drive the new Avalonia Views/ViewModels.

### Phase 5 – Linux Packaging
- **Target:** A single, user-friendly `.AppImage` that bundles the required runtimes (including the Avalonia runtime) so users can run it on any modern Linux distribution without manual dependency management.

---

## Risks and Mitigation

| Risk | Mitigation |
|---|---|
| **Visual Fidelity** | Avalonia renders differently than WinForms. We accept a "functional parity" goal rather than "pixel-perfect" to avoid infinite polish loops. |
| **Core Logic Regression** | Strict adherence to "Build & Test" after every major refactor of a `Create()` method to ensure the WinForms app (the current baseline) still works. |
| **Complexity of `Create()` Refactoring** | Breaking the work into small, manageable chunks (one entity type per task) to prevent a "big bang" breakage. |

---

## Decisions Already Made

- **UI Framework:** Avalonia.
- **Core Library:** `Chummer.Core` (SDK-style, multi-targeting `net48;net8.0`).
- **Sharing Feature:** A modern REST API is already implemented (replacing the old Omae SOAP service).
- **Charts:** ScottPlot (cross-platform compatible).

***
