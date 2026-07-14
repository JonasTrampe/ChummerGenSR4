# ChummerGenSR4 – Plan for a Linux Version

Date: 2026-07-13 · Based on the current state of `D:\Daten\Projekte\Roleplay\chummer` (branch with today's commits `c511e47` and `6f7575d`)

## Starting Point

ChummerGenSR4 is a WinForms application targeting **.NET Framework 4.8**. That's the decisive constraint for the whole plan: WinForms — even under modern .NET (6/8/9) — only runs on Windows. Microsoft never ported it to Linux and has no plans to. So a "native" Linux program from this codebase only has two realistic paths: **Mono** (an alternative .NET runtime that can run WinForms on Linux via a GDI+ reimplementation/`libgdiplus`) or a full **UI rewrite** onto a genuinely cross-platform toolkit such as Avalonia.

You already started down the Mono path last night with Codex (`clsRuntimeInfo.cs` with `RuntimeInfo.IsMono`, a fallback for the Karma/Nuyen charts in `frmCareer.cs`, a sorting fix in `SkillControl.cs`). That's the pragmatic approach, and this plan builds on it rather than starting from zero.

What I additionally looked at, to base the plan on actual findings instead of assumptions:

| Area | Finding |
|---|---|
| Target framework | `.NET Framework 4.8`, `OutputType=WinExe`, old-style csproj |
| P/Invoke (`DllImport`) | 0 hits – no direct Windows API access, a good sign for Mono |
| Windows Registry | ~90 calls, almost entirely in `clsOptions.cs`, `frmOptions.cs`, `frmOmae.cs` – pure settings storage, easy to encapsulate |
| WCF (`System.ServiceModel`) | 9 files, entirely tied to the old **Omae** feature (online sharing of data packs/sheets) and the translation service |
| Omae/update endpoints | Point at `www.chummergen.com/dev/chummer/...` – per your own README the domain is dead ("original site directs to spam"). These endpoints are already non-functional even on Windows |
| `System.Windows.Forms.DataVisualization` | Only in `frmCareer.cs` (Karma/Nuyen history charts) – unreliable under Mono, already disabled via fallback last night |
| Path handling | No hardcoded `\` concatenations found; `Path.DirectorySeparatorChar` is already used in 17 places – better shape than expected |
| CI | `autobuild.yml` builds exclusively on `windows-latest` via `msbuild` |

## Recommendation: Keep following the Mono path consistently (no Wine, no Avalonia rewrite – for now)

Reasoning, briefly and honestly:

- **Wine-only** would just run the Windows exe under a compatibility layer. No gain over what's already technically possible today, and not a "real" Linux variant in the sense of a native process with native packaging.
- **Avalonia rewrite** would be the cleanest long-term solution (also gets you macOS, no more Mono baggage), but with ~80 designer files and a 4,000-line options class, that's a project of several weeks to months – disproportionate to "stable & ready to hand to other users" in a reasonable timeframe.
- **Mono** is the pragmatic middle ground: no UI rework needed, you already have a first working compatibility fix from tonight, and the remaining work items (registry, WCF, graphics edge cases) are clearly scoped and manageably sized.

I think this is the right call for this project – with the honest caveat that Mono is a legacy technology (Microsoft/.NET Foundation only maintain it minimally) and an Avalonia rewrite could still make sense mid-term if the project grows. I'd deliberately defer that rather than fold it into this plan.

## Phase Plan

### Phase 0 – Drop the dead weight (WCF/Omae/update legacy)

This is the lever with the best effort-to-benefit ratio, since the affected endpoints are dead anyway:

- Remove `System.ServiceModel`, `Service References/OmaeService`, `Service References/TranslationService`, and all `frmOmae*.cs` dialogs (or gate them behind a feature flag). This eliminates the WCF dependency entirely – WCF client stacks are, in practice, one of the least reliable corners under Mono.
- `frmUpdate.cs` currently checks against `http://www.chummergen.com/dev/chummer/manifestdata.xml` (also dead). Proposal: replace it with a simple `HttpClient` call against your own repo's **GitHub Releases API** (`autobuild.yml` already produces releases with zip artifacts). That's cross-platform, needs no WCF/SOAP, and works reliably under Mono.
- Result: a noticeably smaller attack surface for Phase 2, and you get rid of two broken features nobody can use anyway.

*For the question of actually replacing the Omae sharing feature itself, see Phase 5 – deliberately split off from the Linux port, see below.*

### Phase 1 – Settings storage: Registry → cross-platform config file

As discussed: instead of relying on Mono's registry emulation (`~/.mono/registry`), the settings layer gets cleaned up once, for both Windows and Linux.

- A new class (e.g. `SettingsStore`) encapsulates reading/writing to an XML file under `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` (this correctly maps to `~/.config`/`~/.local/share` under Mono/.NET, and stays `%AppData%` on Windows).
- Migrate `clsOptions.cs`, `frmOptions.cs`, `frmOmae.cs` to the new store API (the registry-call mechanics are highly repetitive, so this is largely mechanical to replace).
- **Migration:** on first start after the update, read existing registry values once and carry them over into the new file, so existing Windows users don't lose their settings. Leave the old registry values alone afterward (don't delete them), in case anyone needs to roll back.

### Phase 2 – Mono hardening (continuing tonight's approach) + fix the charts now instead of just disabling them

Per your preference, the chart question isn't deferred – it gets solved here directly, instead of stopping at tonight's plain disablement:

- **Replace `System.Windows.Forms.DataVisualization.Charting`** instead of only switching it off via the `_blnExpenseChartsAvailable` flag. Recommendation: **ScottPlot** (alternative: **OxyPlot.WindowsForms**) – both are pure managed .NET libraries with no native dependencies beyond GDI+, offer a WinForms control that's a 1:1 drop-in for `chtKarma`/`chtNuyen`, and carry no WCF/COM baggage. That gets the Karma/Nuyen history charts working identically on Windows *and* Linux/Mono, instead of missing on Linux.
- **Important, to be upfront about it:** I can't actually test against a Mono runtime from here. Before reworking the full chart logic in `frmCareer.cs`/`frmCareer.Designer.cs`, do a short spike first – get a minimal WinForms test form with the chosen chart control running under Mono on Linux, and verify rendering/interaction work cleanly. Only then tackle the full rework of the ~200 lines of chart-related code in `frmCareer.cs`.
- Migration step: the `_blnExpenseChartsAvailable` fallback from tonight's commits can stay as a safety net for now (e.g. in case the new chart control still has trouble on some distro), but after a successful spike it becomes the exception rather than the rule.
- Reuse the fallback pattern already started (`_blnExpenseChartsAvailable`) for any other, smaller GDI+-heavy spots if similar issues turn up there.
- **Case-sensitivity audit:** Linux filesystems are case-sensitive, Windows isn't. Every reference to `data\*.xml`, `icons\*`, XSLT sheets, etc. has to match the on-disk filename exactly – this is a classic "works on my Windows machine, crashes on the Linux box" bug and should be actively grepped/tested for.
- **Verify printing:** `PrintDocument`/`PrintPreviewDialog` have historically had gaps under Mono. Needs real testing (printing a character sheet / exporting to PDF); consider a fallback of "PDF export only, no native print dialog" on Linux if needed.
- Spot-check font rendering/DPI differences (layout shifts aren't unusual under Mono).
- Document `libgdiplus` as a system dependency (package name per distro, e.g. `libgdiplus` on Debian/Ubuntu).

### Phase 3 – CI extension

- Add a second job to `autobuild.yml` on `ubuntu-latest`: install `mono-complete` (or the leaner `mono-devel` + `msbuild` package), run `msbuild ChummerGenSR4.sln /p:Configuration=Release` under Mono.
- Add a minimal smoke test that starts the app headless (`xvfb-run`) briefly and shuts it down cleanly, to automatically catch crash-on-startup regressions – deeper test coverage isn't realistic without a UI test framework, but this catches the most common failure class.
- The existing `windows-latest` job stays unchanged; both artifacts get attached to the release at the end.

### Phase 4 – Packaging for Linux users

- **tar.gz** with the built binaries plus a launch script (`#!/bin/sh` → `mono ChummerGenSR4.exe "$@"`) as a quick intermediate deliverable, uploaded automatically via the CI release.
- **AppImage** is now firmly planned (no longer optional): bundles the Mono runtime so users don't have to install anything themselves. Building block: `linuxdeploy`/`appimagetool` in the CI job from Phase 3, packaging the built binaries plus a matching Mono runtime (or alternatively a Mono AppDir template) into a `.AppImage` file. This makes the Phase 3 CI job somewhat more involved, but the end result for your fellow players is a single double-clickable artifact.

### Phase 5 – Full-fledged REST API replacing the old Omae sharing feature

The WSDL contract of the old `omae.asmx` SOAP service shows what actually needs to be covered functionally – this is the basis for the API design, not a reinvention:

| Area | Old SOAP operations | Meaning |
|---|---|---|
| Accounts | `RegisterUser`, `Login`, `ResetPassword`, `GetEmailAddress`, `SetEmailAddress` | User accounts, login, password reset |
| Characters | `FetchCharacters`, `FetchCharacters153`, `UploadCharacter`, `UploadCharacter153`, `DownloadCharacter`, `DeleteCharacter` | Sharing character sheets (two format versions – a rebuild only needs the current format) |
| Data packs | `FetchDataFiles`, `UploadDataFile`, `DownloadDataFile`, `DeleteDataFile` | Custom data XML packs (house rules, extra gear, etc.) |
| Sheets | `FetchSheets`, `UploadSheet`, `DeleteSheet`, `DownloadSheet` | XSLT character sheet templates |
| Misc | `GetCharacterTypes` | Categorization of character types |

Worth flagging: in the old client (`frmOmae.cs`), the Omae password was only Base64-encoded (not hashed/encrypted) in the registry – that was already conceptually insecure and shouldn't be repeated in the rebuild.

**Proposed design:**

- **Tech stack:** ASP.NET Core Minimal API on .NET 8 (cross-platform, runs natively on your Linux server, no WCF/SOAP baggage). SQLite or Postgres for metadata (users, files, timestamps), files stored as blobs on disk (or S3-compatible storage later if space needs grow).
- **Endpoints** (rough sketch, REST instead of SOAP-RPC):
  - `POST /api/auth/register`, `POST /api/auth/login` (returns a JWT), `POST /api/auth/reset-password`, `GET/PUT /api/account/email`
  - `GET /api/characters`, `POST /api/characters`, `GET /api/characters/{id}`, `DELETE /api/characters/{id}`
  - `GET /api/datafiles`, `POST /api/datafiles`, `GET /api/datafiles/{id}`, `DELETE /api/datafiles/{id}`
  - `GET /api/sheets`, `POST /api/sheets`, `GET /api/sheets/{id}`, `DELETE /api/sheets/{id}`
  - Bearer-token (JWT) auth after login, instead of sending the username/password on every request.
- **Security:** password hashing with BCrypt or Argon2 (not Base64 like the original), rate limiting on the login route, TLS via a reverse proxy (Caddy/nginx, which you presumably already run in front of Gitea), e.g. under its own subdomain like `omae.jonas-trampe.de`.
- **Deployment:** as its own Docker container alongside your existing Gitea instance (a `docker-compose` service), so operations/backups/updates fit into the same existing infrastructure.
- **Client migration:** move `clsOmaeHelper.cs` and all `frmOmae*.cs` dialogs off `omaeSoapClient`/`translationSoapClient` (WCF) onto a slim `HttpClient` + `System.Text.Json` client. This is needed anyway because WCF is unreliable under Mono – so the REST migration solves a Linux compatibility problem and modernizes the feature at the same time.
- **Data migration:** not needed – the old service has been offline for years (per the README), so there's no existing data to carry over. Fresh start with an empty database.
- This phase is planned as a **standalone project after the core Linux port** (separate repo or subfolder, its own deploy cycle), so it doesn't delay the pure porting timeline (Phases 0–4).

## Risks and Known Limitations

| Risk | Assessment |
|---|---|
| Karma/Nuyen charts on Linux | Replaced in Phase 2 via ScottPlot/OxyPlot instead of staying permanently disabled – residual risk: untested whether the chart control renders cleanly under Mono, so verify via a spike before the full rework |
| REST API (Phase 5) as a new attack surface | A publicly reachable service with user accounts brings its own operational/security obligations (patching, backups, abuse protection) – realistically weigh whether the effort is justified for the size of your group |
| Registry → config migration | Needs careful testing so existing Windows users don't lose settings on update |
| Case sensitivity | The biggest "silent failure" risk – actively test on a real Linux filesystem, not just via code review |
| Mono's future | Only minimally maintained by Microsoft/.NET Foundation these days; works well enough for WinForms legacy today, but isn't a path with a long-term guarantee |
| Print dialog | Unclear until tested – worst case, offer PDF export only on Linux |

## Rough Effort Estimate

| Phase | Scope |
|---|---|
| 0 – Remove WCF/Omae/update | small (mostly deletion + one new HttpClient call) |
| 1 – Settings migration | medium (repetitive, but many call sites: ~90 calls across 3 files) |
| 2 – Mono hardening + chart replacement | medium to large (the chart rework in `frmCareer.cs` adds to the existing hardening effort; only reliably estimable after a successful Mono spike) |
| 3 – CI (incl. AppImage build) | medium (AppImage packaging in the CI job is the more involved part compared to plain tar.gz) |
| 4 – Packaging | integrated into the Phase 3 CI job |
| 5 – REST API replacement for Omae | large – standalone project (server implementation, auth/security, deployment, client migration in `clsOmaeHelper.cs`/`frmOmae*.cs`); realistically several days to low weeks, plannable independently of the core port |

## Suggested Next Concrete Steps

1. Implement Phase 0 (remove WCF/Omae/update legacy) – immediately reduces complexity and risk for everything that follows.
2. Phase 1 (settings store) right after, since it's independent of the rest and benefits both platforms.
3. Then bring in Phase 3 (Linux CI job), so every subsequent step is automatically tested against a real Linux build from that point on, instead of finding out at the end that something doesn't compile.
4. Only then tackle Phase 2 (Mono hardening, including the chart replacement) with real Linux test runs – the CI from step 3 is what makes this work measurable.
5. Phase 4 (packaging) is folded into the Phase 3 CI job; Phase 5 (Omae REST API replacement) follows afterward as its own project once the core port is stable.

## Decisions Already Made (for reference)

- Charts: fix now with a cross-platform library (ScottPlot/OxyPlot), not deferred – see Phase 2.
- Packaging: AppImage is in scope from the start, alongside tar.gz – see Phase 4.
- Omae replacement: build a full REST API (not just a read-only Gitea file share) – see Phase 5.
- This plan document is committed to the repo at `docs/LINUX_PORT_PLAN.md`.

## Addendum (2026-07-13): Mono vs. Avalonia is not fully settled – Spike Plan

Discussion after the initial plan surfaced that the Mono-vs-Avalonia call, taken as settled above, deserves a second look: Mono is the pragmatic low-effort path but pins the app to a legacy, minimally-maintained runtime indefinitely; Avalonia is a real cross-platform, actively-maintained UI stack (and gets macOS for free) but requires a genuine UI rewrite. Given the fidelity/function bar for this decision is strict ("both equally, no compromise" per the project owner), this needs real evidence, not a judgment call from the code alone – hence a side-by-side spike before committing.

### Avalonia risk audit (informs spike scope)

A full audit of all 80 `*.Designer.cs` files was run to find WinForms patterns with no clean Avalonia equivalent. Ranked highest to lowest risk:

| # | Risk area | Files | Scale | Avalonia concern |
|---|---|---|---|---|
| 1 | `WebBrowser` control (ActiveX/IE) | `frmUpdate`, `frmViewer` | ~9 hits | No Avalonia equivalent at all – used to preview/print the XSLT-rendered character sheet (`frmViewer.cs`); needs an embedded Chromium WebView replacement |
| 2 | MDI (`IsMdiContainer`/`MdiParent`) | `frmMain` | 6 hits | No native MDI in Avalonia – main-window/child-character-window model needs re-architecting (tabs) |
| 3 | `DataVisualization.Charting` | `frmCareer` | 7 hits | Bundled assembly, no Avalonia port – needs ScottPlot (already the plan's choice regardless of UI path) |
| 4 | Custom GDI+ painting | `SplitButton.cs` | ~5 hits | Hand-rolled owner-draw button – needs re-authoring as an Avalonia custom control/template |
| 5 | Custom `UserControl` subclasses | `ContactControl`, `PetControl`, `PowerControl`, `SkillControl`, `SkillGroupControl`, `SpiritControl`, `OmaeRecord` | 7 classes | Logic is portable; WinForms pixel/anchor layout needs re-expression in XAML per control |
| 6 | Drag-and-drop | `frmCareer`, `frmCreate` | ~81 hits total | Concentrated in the 2 largest forms (gear/cyberware reordering); different API shape, same concept |
| 7 | Layout containers (`SplitContainer`/`FlowLayoutPanel`/`TableLayoutPanel`) | `frmCareer`, `frmCreate`, `frmOmae`, `frmAbout` | 32 hits | Direct Avalonia equivalents exist (`Grid`/`StackPanel`/`WrapPanel`) but re-expression is tedious in the two largest forms |
| 8 | `ToolStrip`/`MenuStrip`/`ContextMenuStrip`/`StatusStrip` | ~14-15 files | very high raw count, mostly designer boilerplate | Structurally different idioms in Avalonia, no direct `StatusStrip` equivalent – wide but shallow |
| – | Owner-draw lists, `NotifyIcon`, `DataGridView`/`BindingSource`, `PrintDocument` | – | 0 hits | Non-issues |

No third-party WinForms control libraries are referenced anywhere in the `.csproj` files – only stock `System.Windows.Forms`, `System.Drawing`, and the bundled `DataVisualization` assembly. That's the one piece of unambiguous good news for either path.

### Decisions from this discussion (feed into the spike)

- **MDI → tabs**: acceptable if done well; not a hard blocker for the Avalonia path.
- **WebBrowser / sheet preview**: the feature is essential (not droppable). The Avalonia side of the spike must prototype an **embedded Chromium WebView** (e.g. a CEF-based Avalonia control), not the lighter system-browser/PDF-export alternative – accepting that this itself adds a native-dependency packaging concern to weigh against Mono's own packaging cost.
- **Charts**: ScottPlot regardless of which UI path wins – it has both a WinForms/Mono control and an Avalonia control, so this choice isn't wasted either way.
- **Phase 5 (Omae REST API)**: stays out of scope for this decision; noted only so the Avalonia/Mono client migration work in Phase 5 doesn't get blindsided later.

### Spike scope

Time-boxed to roughly half a day per side, both prototyping the same representative slice rather than a trivial "hello world," since the risk areas are specific and concentrated in a few forms:

**Target: a slice of `frmCareer.cs`**, chosen because it alone exercises most of the highest-risk areas found above: the karma/nuyen chart (#3), drag-and-drop reordering (#6), `SplitContainer`/layout nesting (#7), and toolstrip/menu chrome (#8). It's the single most representative form in the app.

1. **Mono side** (on the existing, already-set-up Linux/Mono machine):
   - Build the current `frmCareer.cs` slice under Mono as-is.
   - Measure: does it render correctly, any layout/font/DPI glitches, how long to get it running, any GDI+ edge cases.
2. **Avalonia side**:
   - Rewrite the same slice: the chart (via ScottPlot's Avalonia control), the drag-and-drop list reordering, the split/layout structure, and a minimal toolbar/menu.
   - Additionally prototype the `frmMain` MDI→tabs replacement and an embedded Chromium WebView showing a sample XSLT-rendered character sheet (the frmViewer replacement), since both were flagged as essential, non-mechanical rework above.
   - Measure: how much XAML/code, how alien it feels vs. WinForms, whether ScottPlot-Avalonia and the WebView control work cleanly, packaging weight of bundling a Chromium runtime.
3. **Compare**: rendering fidelity, functional parity, packaging story (Mono runtime bundling vs. Chromium WebView bundling), and gut feel for long-term maintainability. Given the strict "no compromise" fidelity/function bar, be explicit about where each path falls short – neither is likely to be a clean pass.

### Open question carried forward

Pixel-perfect visual fidelity was set as a hard requirement alongside functional parity. Avalonia fundamentally cannot render pixel-identical to WinForms (different text rendering, control chrome, spacing) – this is a structural, not incidental, limitation. The spike should surface concretely how large that visual gap actually is in practice, since a strict no-compromise bar may end up favoring Mono by default regardless of Avalonia's other merits.
