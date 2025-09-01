# Repository Guidelines

## Project Structure & Modules
- Root: `PixelAutomation.sln`, `config.json`, `logs/`.
- Source: `src/`
  - `Core/` — shared domain models and services (`net8.0`).
  - `Capture.Win/` — Windows capture utilities (WinForms, P/Invoke).
  - `Host.Console/` — CLI host and orchestration.
  - `Tool.Overlay.WPF/` — WPF UI (MVVM via CommunityToolkit.Mvvm).
- Build artifacts: `bin/`, `obj/` (ignored).

## Build, Run, Test
- Build solution: `dotnet build PixelAutomation.sln -c Release`
- Run console app: `dotnet run --project src/Host.Console -c Debug -- --help`
- Run WPF app: `dotnet run --project src/Tool.Overlay.WPF -c Debug`
- Publish single-file console: `dotnet publish src/Host.Console -c Release -r win-x64 --self-contained false`
- Tests: no test project yet. Suggested layout: `tests/` with xUnit. Run with `dotnet test`.

## Coding Style & Naming
- C# 12 / .NET 8 with `ImplicitUsings` and `Nullable` enabled.
- Indentation: 4 spaces; braces on new lines; file-scoped namespaces preferred.
- Naming: PascalCase for types/properties/methods; camelCase for locals/parameters; `_camelCase` for private fields.
- WPF: MVVM pattern; commands/properties in `ViewModels/`; UI-only logic in `*.xaml.cs` kept minimal.
- Formatting/lint: keep consistent with existing style; run `dotnet format` if available.

## Testing Guidelines
- Framework: xUnit recommended. Example: `dotnet new xunit -n Core.Tests -o tests/Core.Tests` and reference `src/Core/Core.csproj`.
- Conventions: test project name `*.Tests`; class `ClassNameTests`; method `MethodName_Should_DoThing`.
- Coverage: target meaningful coverage of `Core` services and critical Win interop wrappers.

## Commit & Pull Requests
- Commits: concise, imperative subject (<= 72 chars), e.g., `Add color sampling service`. Group related changes.
- Prefer small PRs with:
  - Clear description, rationale, and scope.
  - Linked issues (e.g., `Fixes #123`).
  - For UI changes, include screenshots/gifs.
  - Steps to validate locally.

## Security & Configuration
- Configuration: `config.json` at repo root; avoid committing secrets. Provide safe defaults.
- Platform: Windows-focused (`win-x64`, WinForms/WPF, Vanara P/Invoke). Test on Windows 11 SDK 22621.
- Logs: written to `logs/`. Do not commit sensitive log files.

## Architecture Overview
- Dependencies: `Host.Console` and `Tool.Overlay.WPF` depend on `Core` and `Capture.Win`.
- Cross-cutting: use `Core` for models/services; avoid UI references in lower layers.
