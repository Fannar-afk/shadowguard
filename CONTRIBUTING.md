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

## Lightweight Verification

```powershell
dotnet run --project .\ShadowGuard.Tests\ShadowGuard.Tests.csproj --configuration Release
```

## Source Line Count

```powershell
.\tools\Count-CodeLines.ps1
```

## Pull Request Checklist

Before opening a pull request, please check:

- The project builds successfully.
- Lightweight verification passes.
- New scanner or rule behavior has at least one verification case.
- Documentation is updated if behavior or user workflow changes.
- New third-party dependencies are recorded in `THIRD_PARTY_NOTICES.md`.
- Security-sensitive changes are described clearly in the PR body.

## Coding Notes

- Keep scanner behavior deterministic and local-first.
- Do not execute code from scanned projects.
- Prefer explicit diagnostics over silent failures.
- Keep UI logic separate from reusable scanning and scoring logic when possible.
