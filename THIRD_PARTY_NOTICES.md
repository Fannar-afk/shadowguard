# Third Party Notices

This document records third-party dependencies and license considerations for ShadowGuard.

## Current Dependency Status

At the time this notice was added, `ShadowGuard/ShadowGuard.csproj` does not declare third-party NuGet `PackageReference` dependencies. The project primarily uses the .NET SDK, WPF, Windows Forms, and .NET runtime libraries.

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

The repository now includes an MIT License for ShadowGuard's own source code. Future contributors should verify that any added third-party dependency is compatible with MIT-licensed redistribution and with the intended packaging model.
