# CLAUDE.md

Chummer WinForms→Avalonia/Linux port. `PORTING_PLAN.md` is the single source of truth for scope — update it after every change to reflect exactly what was verified.

## Scope
- Full legacy feature parity is the goal, not an MVP subset.
- Never trim scope to avoid complexity. If a feature needs an underlying subsystem that's missing, build it too — don't skip or simplify it away.
- When investigating before a change, delegate research to a subagent so findings don't bloat context; synthesize its report yourself before implementing.
- Subagent prompts must explicitly state "you are a subagent, do not spawn further agents — investigate directly yourself using Grep/Read/Bash and report concrete findings" — subagents have repeatedly deflected research back into another layer of delegation (returning a placeholder like "I'll wait for the agent to finish") instead of doing the work themselves.

## Verification protocol (every change, no exceptions)
1. `dotnet build Chummer.Core/Chummer.Core.csproj -v:q` and `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v:q` — expect `0 Fehler`.
2. `dotnet test Chummer.Tests/Chummer.Tests.csproj` — run once, expect 100% passing.
3. Smoke test: `timeout -s KILL 6 dotnet .artifacts/bin/Chummer.Avalonia/Debug/net10.0/Chummer.Avalonia.dll` — success = exit code 137, no exception text in output.
4. Update `PORTING_PLAN.md` to match what was actually verified.
5. Commit via heredoc (`git add -A && git commit -q -F -`), message ends with `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
6. Never batch multiple steps' worth of changes before testing — run the full protocol and commit after each step, not at the end of a series of steps.
7. Redirect build/test/smoke-test output to a log file (scratchpad) and inspect it with the Read tool instead of chaining bash `tail`/`grep` pipelines.

## Git
- Never push. Never `git commit --amend`. Only commit fully-verified slices — one logical change per commit.
- If a heredoc commit garbles the message, leave it (don't amend); the diff content is what matters.

## Code conventions
- Every ported behavior gets a comment citing the legacy source (`// Ported from frmCareer.cs's cmdX_Click: ...`) explaining *why*, not what.
- Keep comments short — one line, explaining *why* not *what*. No paragraphs, no restating the code.
- Use speaking (self-documenting) names for functions, variables, and constants so the code needs fewer comments to be understood.
- New Core logic needs xUnit test coverage before being considered done.
- Match existing file-splitting conventions: large files split via C# partial classes by domain (see `CharacterFileService.*.cs`, `CharacterOptions.*.cs`), one type per file for small data-holder/row-viewmodel classes.
- Strict MVVM: views (`.axaml`/`.axaml.cs`) stay dumb — layout, bindings, and thin event-forwarding only. All logic, state, and decisions belong in the ViewModel.
- For Avalonia UI work, use the Avalonia skills for design/layout guidance.
