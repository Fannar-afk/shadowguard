# Third Party Notices

This document records third-party dependencies and license considerations for ShadowGuard.

## Runtime Dependency Status

`ShadowGuard` and `ShadowGuard.Core` primarily use the .NET SDK, WPF, Windows Forms, and .NET runtime libraries. The desktop application and core library do not currently declare additional third-party runtime NuGet package dependencies.

## Development and Test Dependencies

`ShadowGuard.Tests` uses the following NuGet packages for automated testing and test coverage support:

| Package | Version | Purpose | Scope |
| --- | --- | --- | --- |
| `Microsoft.NET.Test.Sdk` | `17.11.1` | .NET test execution infrastructure | Development/test only |
| `xunit` | `2.9.2` | Unit testing framework | Development/test only |
| `xunit.runner.visualstudio` | `2.8.2` | Visual Studio and `dotnet test` runner integration | Development/test only |
| `coverlet.collector` | `6.0.2` | Code coverage data collection | Development/test only |

These packages are used by the test project and are not bundled as application runtime plugins or rule packs.

## Platform Dependencies

ShadowGuard targets:

- .NET 6
- Windows WPF
- Windows Forms folder selection dialog

These are platform/framework dependencies and are not vendored into this repository.

## Bundled Assets and Samples

The repository contains project source code, sample dependency manifests, plugin rule samples, documentation, and local tooling scripts. If future versions add third-party code, datasets, icons, rules, or binary assets, they should be recorded in this file.

## Maintenance Rules

When adding a third-party package, library, code snippet, binary, dataset, or asset, update this file with:

- Name
- Version or source commit
- Source URL
- License
- Whether it is bundled, linked, or only used during development
- Any redistribution or attribution requirements

## License Compatibility Note

The repository includes an MIT License for ShadowGuard's own source code. Future contributors should verify that any added third-party dependency is compatible with MIT-licensed redistribution and with the intended packaging model.
