# ChummerGenSR4

A WinForms character generator/tracker for Shadowrun 4th Edition — build characters, then track Karma, Nuyen, ammo, and gear through their shadowrunning career.

## Development Setup

```bash
# Restore NuGet packages (classic packages.config, not PackageReference)
nuget restore ChummerGenSR4.sln

# Build (Release)
msbuild ChummerGenSR4.sln /p:Configuration=Release

# Build (Debug)
msbuild ChummerGenSR4.sln /p:Configuration=Debug

# Run
bin/ChummerGenSR4.exe
```

There is no separate test project/`npm test`-equivalent — validation is build success + manual smoke testing (launch the exe, exercise the changed flow). Prefer building via `ChummerGenSR4.sln`, not the bare `.csproj` — building the `.csproj` directly skips `$(SolutionDir)`-relative post-build steps (e.g. the changelog copy) and produces a harmless-but-confusing error.

## Tech Layers

- **Framework**: WinForms, .NET Framework 4.8, old-style (non-SDK) `.csproj`
- **Language**: C#
- **UI**: WinForms designer files (`*.Designer.cs` + matching `.resx`), no XAML/MVVM
- **Data**: XML-driven ruleset (`Chummer/data/data/*.xml` + matching `.xsd` schemas) loaded via `clsXmlManager.cs`
- **Character sheets**: XSLT (`Chummer/data/sheets/*.xsl`) transform saved characters into printable output
- **Settings storage**: `SettingsStore` (`Chummer/code/clsSettingsStore.cs`) — an XML file under `ApplicationData`, replacing the old Windows Registry storage (see `docs/LINUX_PORT_PLAN.md` Phase 1)
- **Packages**: classic `packages.config` (not `PackageReference`) — restore via `nuget restore`, not `dotnet restore`
- **CI**: GitHub Actions (`.github/workflows/autobuild.yml`) — builds on `windows-latest` and (in progress) a Mono/`ubuntu-latest` job for the Linux port

There is also a `Chummer.AvaloniaSpike` project — an experimental/prototype Avalonia UI rewrite, kept separate from the stabilization work on the main WinForms app. Don't conflate changes between the two unless the task is explicitly about the Avalonia spike.

## Project Structure

```
Chummer/
├── code/          # All application logic and WinForms code-behind (forms, controls, core classes)
│   ├── frm*.cs           # Forms (+ matching *.Designer.cs, *.resx)
│   ├── cls*.cs           # Core domain/service classes (clsOptions, clsEquipment, clsCharacter, ...)
│   └── *Control.cs       # Reusable WinForms UserControls
├── data/
│   ├── data/      # Ruleset XML + XSD schemas (armor, weapons, qualities, books, ...)
│   ├── sheets/    # XSLT character sheet templates
│   └── lang/      # Localization XML (en-us.xml is the mandatory base; others are overlays)
├── Properties/    # AssemblyInfo.cs, Resources
└── icons/         # Embedded icon/image resources

docs/                          # Planning docs, incl. LINUX_PORT_PLAN.md (Mono/Linux port roadmap)
.github/workflows/autobuild.yml  # CI: build, version, zip, release
```

## Code Standards

### General Rules
- This is a large, long-lived legacy codebase (old-style csproj, .NET Framework 4.8) — match existing patterns rather than introducing modern idioms wholesale (no async/await retrofits, no sudden dependency injection, etc.) unless the task specifically calls for it.
- Minimize new NuGet dependencies. Prefer types already shipped with .NET Framework (e.g. `System.Web.Extensions`'s `JavaScriptSerializer` over adding `System.Text.Json`) — every new dependency needs a real `packages.config` entry (see Common Pitfalls) and adds risk for the in-progress Mono/Linux port.
- Follow the Hungarian-ish naming already used throughout (`str`, `int`, `bln`, `obj`, `lst`, `dic` prefixes) — this is inconsistent with modern C# style guides but consistent with the rest of the codebase.
- New user-facing strings driving `Tag`-based translation MUST be added as a key to `Chummer/data/lang/en-us.xml` — `LanguageManager.GetString` does an unguarded dictionary lookup and throws `KeyNotFoundException` if the base English key is missing. Other language files are optional overlays; missing keys there just fall back to English.

### Naming Conventions
- Forms: `frm*` (e.g. `frmCareer.cs`), always paired with `frm*.Designer.cs` and (usually) `frm*.resx`
- UserControls: `*Control.cs` (e.g. `SkillControl.cs`), same Designer/resx pairing
- Core classes: `cls*` (e.g. `clsOptions.cs`, `clsEquipment.cs`)
- Fields: Hungarian-style prefixes (`_strFoo`, `_intFoo`, `_blnFoo`, `_objFoo`, `_lstFoo`, `_dicFoo`)

### File Organization
- A WinForms form/control is always three files: `X.cs` (logic), `X.Designer.cs` (generated layout — edit by hand only for structural changes, never regenerate blindly), `X.resx` (resources/strings). Deleting or renaming one means updating all three **and** the `<Compile>`/`<EmbeddedResource>` entries in `Chummer.csproj` (this old-style csproj lists files explicitly — nothing is auto-globbed).
- Ruleset XML lives in `Chummer/data/data/`; every file has a matching `.xsd`. Keep them in sync when adding new elements/attributes.

## Important Patterns

### Loading ruleset XML
```csharp
XmlDocument objXmlDocument = XmlManager.Instance.Load("weapons.xml");
```
Always go through `XmlManager.Instance.Load(...)`, never read `Chummer/data/data/*.xml` directly — it handles caching and (for the Linux port) case-sensitive filename matching.

### Settings storage
```csharp
SettingsRegistryKey objRegistry = SettingsStore.CurrentUser.CreateSubKey("Software\\Chummer");
objRegistry.SetValue("somekey", value.ToString());
```
Mirrors the old `Microsoft.Win32.Registry` API deliberately, backed by a cross-platform XML file instead — see `Chummer/code/clsSettingsStore.cs`. Don't reintroduce direct `Microsoft.Win32.Registry` calls.

### Language/localization
```csharp
LanguageManager.Instance.GetString("Some_Key")
```
Always add new keys to `en-us.xml` first (mandatory), then optionally to other language files.

## Testing Guidelines

- No automated unit/integration test suite exists. "Testing" here means: (1) a clean build via `msbuild ChummerGenSR4.sln`, and (2) a manual smoke test — launch `bin/ChummerGenSR4.exe`, confirm it starts and stays responsive, and exercise the specific screen/flow you changed.
- For anything touching CI (`.github/workflows/autobuild.yml`), remember changes can't be validated locally — they need a real Actions run (`gh workflow run autobuild.yml --ref <branch>` works for on-demand testing without waiting for a PR).

## Common Pitfalls to Avoid

- DON'T: Add a NuGet reference (`<Reference Include="...">` with a `HintPath`) without also adding it to `Chummer/packages.config`. A HintPath into `$(NuGetPackageRoot)` or a global cache only works if that package is already restored there by something else — it will build fine locally (stale global cache) and fail on a clean CI runner. Always verify with a clean `nuget restore` + rebuild.
- DON'T: Build `Chummer/Chummer.csproj` directly when checking for real errors — build via `ChummerGenSR4.sln` instead.
- DON'T: Touch the Windows Registry directly (`Microsoft.Win32.Registry`) — use `SettingsStore` instead (Linux/Mono has no real registry).
- DON'T: Hardcode `\` path separators — use `Path.DirectorySeparatorChar` / `Path.Combine` (Linux filesystems are also case-sensitive; XML/sheet/icon filenames must match on-disk casing exactly).
- DON'T: Delete/rename a form or control's `.cs`/`.Designer.cs`/`.resx` trio without updating `Chummer.csproj`'s explicit `<Compile>`/`<EmbeddedResource>` entries — nothing is glob-included.
- DO: Check `docs/LINUX_PORT_PLAN.md` before touching anything WinForms/Mono-compatibility related — it tracks the phased Linux port plan and what's already done vs. pending.
- DO: Keep the Windows CI job (`build` in `autobuild.yml`) and the Mono CI job (`build-mono`) both green — a change that breaks Mono compatibility should be caught there, not just on `windows-latest`.

## Performance Considerations

- This is a desktop WinForms app — the usual web-perf concerns (bundle size, lazy loading) don't apply. Watch instead for: large XML documents being reloaded repeatedly instead of cached via `XmlManager`, and O(n²) patterns in list/grid population code (`frmCareer.cs`, `clsEquipment.cs` are large and performance-sensitive on character load).

## Deployment

- CI (`.github/workflows/autobuild.yml`) builds on `push`/`pull_request` to `main` and on `release` events; a `workflow_dispatch` trigger allows on-demand runs on any branch/tag.
- Versioning via `minver` (`--tag-prefix v`), producing tags like `v0.1.501`; `AssemblyVersion`/`AssemblyFileVersion` are set to match at build time.
- Release artifacts are a single zip (`ChummerGenSR4-{version}.zip` containing `bin/*`) attached to GitHub Releases.
- In-app update checks hit the GitHub Releases API directly (`frmUpdate.cs`) — no separate update server.

## Additional Resources

- Linux/Mono port plan and phase tracking: `docs/LINUX_PORT_PLAN.md`
- Implementation notes/scratch planning: `IMPLEMENTATION_PLAN.md`
- Changelog: `changelog.txt`
