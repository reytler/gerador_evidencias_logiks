# AGENTS.md

## Project

.NET 8 solution (`Evidenciador.slnx`). CLI + WinForms app that scrapes GitHub PR diffs (via Playwright/Chromium) and generates DOCX evidence documents.

## Architecture

| Project | Role |
|---|---|
| `Evidenciador.Core` | Domain models, abstractions (no dependencies) |
| `Evidenciador.Infra.Playwright` | GitHub/Redmine browser automation (Playwright) |
| `Evidenciador.Infra.Docx` | DOCX rendering via `DocumentFormat.OpenXml` |
| `Evidenciador.Cli` | Entrypoint — `Program.cs` / `EvidenceApp.cs` |
| `Evidenciador.UI` | WinForms alternative entrypoint (`Program.cs`) |

Dependency flow: `Cli` -> `Core` + both `Infra.*`. Each `Infra.*` -> `Core`. `UI` -> `Core` only.

## Commands

```powershell
# Build
dotnet build

# Run CLI (defaults from appsettings.json)
dotnet run --project Evidenciador.Cli

# Run with explicit PR URL
dotnet run --project Evidenciador.Cli -- --pr-url "https://github.com/OWNER/REPO/pull/123/files?w=1" --out-dir ".\out"

# Install Playwright Chromium (run once after build)
pwsh .\Evidenciador.Cli\bin\Debug\net8.0\playwright.ps1 install chromium
```

No test projects, linter, or CI exist in this repo.

## Credentials (NEVER commit)

Two sets required for Redmine mode; only GitHub for single-PR mode:

| Source | GitHub | Redmine |
|---|---|---|
| Env vars | `GITHUB_USERNAME`, `GITHUB_PASSWORD` | `REDMINE_USERNAME`, `REDMINE_PASSWORD` |
| user-secrets (dev only) | `GITHUB_USERNAME`, `GITHUB_PASSWORD` | — |
| appsettings.json | `GITHUB_USERNAME`, `GITHUB_PASSWORD` | `Redmine:Username`, `Redmine:Password` |

`appsettings.json` contains live credentials — **never commit changes to it**. Set `$env:DOTNET_ENVIRONMENT = "Development"` to also load `appsettings.Development.json` and user-secrets.

## Operation Modes

- **SinglePr** (default): one PR URL → one DOCX with diff screenshots
- **RedmineQuery**: scrape Redmine issues matching a query, collect linked PRs, generate one DOCX per issue
- **RedmineIssue**: same but for a specific issue ID

Configurable via `Evidenciador:Mode` in appsettings or CLI flags (`--pr-url`, `--redmine-url`, `--redmine-query`, `--issue-id`).

## Key Files

- `Evidenciador.Cli/Program.cs` — DI setup, config loading, entrypoint
- `Evidenciador.Cli/EvidenceApp.cs` — main orchestration, output resolution
- `Evidenciador.Cli/TEMPLATE_EVIDENCIA.docx` — DOCX template (copied to output on build)
- `Evidenciador.Cli/appsettings.json` — defaults + credentials (**do not commit changes**)

## Output

- When using `--out-dir`, DOCX filenames are auto-generated: `{owner}_{repo}_pr{number}_{timestamp}_{slug}.docx`
- Collisions get a `-2`, `-3` suffix
- `--out` accepts either a `.docx` file path or a directory (must end with separator)
