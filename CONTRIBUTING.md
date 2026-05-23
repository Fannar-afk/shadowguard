# Contributing to ShadowGuard

Thank you for helping improve ShadowGuard.

## Development Environment

Recommended environment:

- Windows 10 or Windows 11
- .NET 6 SDK
- Visual Studio 2022 or PowerShell

## Build

```powershell
dotnet restore .\shadowguard.sln
dotnet build .\shadowguard.sln --configuration Release
```

## Tests

ShadowGuard uses xUnit for automated tests.

```powershell
dotnet test .\ShadowGuard.Tests\ShadowGuard.Tests.csproj --configuration Release
```

## CLI Smoke Test

```powershell
dotnet run --project .\ShadowGuard.Cli\ShadowGuard.Cli.csproj --configuration Release -- --path .\samples\demo-npm-risk --plugins .\plugins --out .\artifacts\cli-report.json
```

## Source Line Count

```powershell
.\tools\Count-CodeLines.ps1
```

## Pull Request Checklist

Before opening a pull request, please check:

- The project builds successfully.
- xUnit tests pass.
- CLI smoke test passes when CLI or core behavior changes.
- New scanner or rule behavior has at least one test case.
- Documentation is updated if behavior or user workflow changes.
- New third-party dependencies are recorded in `THIRD_PARTY_NOTICES.md`.
- Security-sensitive changes are described clearly in the PR body.

## Coding Notes

- Keep scanner behavior deterministic and local-first.
- Do not execute code from scanned projects.
- Prefer explicit diagnostics over silent failures.
- Keep UI logic separate from reusable scanning and scoring logic when possible.
