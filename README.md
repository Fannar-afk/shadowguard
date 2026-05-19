# ShadowGuard

ShadowGuard is a Windows desktop supply-chain security workbench built with .NET 6 and WPF. It helps developers inspect project dependency manifests before release, identify risky packages, generate SBOM data, and make a simple pass/warn/block gate decision from one local UI.

The application is designed for local development and pre-release checks. It does not execute code from the scanned project and does not modify the project being scanned.

## Features

- **Multi-ecosystem dependency discovery**: scans common manifest and lock files across npm, Python, Go, Rust, PHP, Java, and .NET projects.
- **Risk findings**: detects suspicious package sources, unpinned versions, pre-release versions, and selected historical supply-chain incident packages.
- **Project risk summary**: calculates component-level and project-level risk scores.
- **SBOM export**: generates CycloneDX-style SBOM JSON with component metadata, PURL, source type, risk score, and evidence files.
- **Security gate**: returns `Pass`, `Warn`, or `Block` based on policy settings.
- **Plugin rules**: loads JSON rule packs from the local `plugins/` directory.
- **Reports**: exports scan reports and SBOM files from the desktop UI.
- **Installer build**: CI can build a Windows installer and a portable win-x64 package.

## Supported Manifest Files

| Ecosystem | Files |
| --- | --- |
| npm | `package.json`, `package-lock.json`, `yarn.lock`, `pnpm-lock.yaml` |
| Python | `requirements*.txt` |
| Go | `go.mod` |
| Rust | `Cargo.toml` |
| PHP | `composer.json` |
| Java | `pom.xml` |
| .NET | `*.csproj` |

Generated or third-party directories such as `node_modules`, `bin`, `obj`, `.git`, virtual environments, and build output folders are skipped during scanning.

## Requirements

- Windows 10 or Windows 11
- .NET 6 SDK for source builds
- WPF-capable desktop environment
- PowerShell for build scripts

End users who install the self-contained release build do not need to install the .NET SDK separately.

## Installation

### Install from GitHub Actions artifact

The CI workflow builds a Windows installer named `ShadowGuard-Setup.exe`.

1. Open the repository's **Actions** tab.
2. Select the latest successful `CI` run.
3. Download the `ShadowGuard-Setup` artifact.
4. Extract the artifact archive.
5. Run `ShadowGuard-Setup.exe` and follow the installer wizard.
6. Launch ShadowGuard from the Start menu or desktop shortcut.

### Install from GitHub Releases

When a version tag such as `v1.0.0` is pushed, the CI workflow creates a GitHub Release and attaches `ShadowGuard-Setup.exe` to the release assets.

Download the installer from the repository's **Releases** page and run it on Windows.

### Use the portable build

The CI workflow also uploads `ShadowGuard-portable-win-x64`.

1. Download the artifact from a successful CI run.
2. Extract the archive.
3. Run `ShadowGuard.exe` from the extracted folder.

## Build from Source

Clone the repository and build the solution:

```powershell
git clone https://github.com/Fannar-afk/shadowguard.git
cd shadowguard
dotnet restore .\shadowguard.sln
dotnet build .\shadowguard.sln --configuration Release
```

Run the desktop app from source:

```powershell
dotnet run --project .\ShadowGuard\ShadowGuard.csproj
```

Create a self-contained win-x64 portable build:

```powershell
dotnet publish .\ShadowGuard\ShadowGuard.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -o .\artifacts\publish
```

## Build the Windows Installer Locally

ShadowGuard uses Inno Setup for the Windows installer. The installer script is located at:

```text
package/ShadowGuard.iss
```

Install Inno Setup 6, publish the app, and compile the installer:

```powershell
New-Item -ItemType Directory -Force -Path .\artifacts\installer | Out-Null

$env:SHADOWGUARD_VERSION='1.0.0'
$env:SHADOWGUARD_PUBLISH_DIR=(Resolve-Path '.\artifacts\publish').Path
$env:SHADOWGUARD_OUTPUT_DIR=(Resolve-Path '.\artifacts\installer').Path

& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' .\package\ShadowGuard.iss
```

The generated installer will be written to:

```text
artifacts\installer\ShadowGuard-Setup.exe
```

## Usage

### Scan the sample workspace

1. Launch ShadowGuard.
2. Click **加载示例** to load `samples/demo-workspace`.
3. Click **开始扫描**.
4. Review dependency findings, the component list, SBOM preview, and gate result.
5. Export the scan report or SBOM JSON if needed.

### Scan your own project

1. Click **选择目录**.
2. Choose the root directory of the project to scan.
3. Click **开始扫描**.
4. Review findings, source files, recommendations, and the final gate decision.

### Export results

ShadowGuard supports two export formats from the UI:

- Full scan report JSON
- SBOM JSON

Export files are written only when the user selects a save path.

## Plugin Rules

ShadowGuard loads plugin rule packs from the local `plugins/` directory. Plugin files are JSON documents and are loaded when the application starts or when the user clicks **重新加载插件**.

Supported match types:

- `ExactName`
- `ContainsName`
- `RegexName`
- `SourceType`
- `VersionPattern`
- `Ecosystem`

Rule fields:

| Field | Description |
| --- | --- |
| `id` | Unique rule identifier |
| `name` | Rule display name |
| `matchType` | Matching strategy |
| `pattern` | Match pattern |
| `severity` | Risk severity |
| `score` | Risk score |
| `category` | Risk category |
| `message` | Finding message |
| `recommendation` | Suggested action |

Example rule file:

```json
{
  "pluginId": "custom-risk-rules",
  "displayName": "Custom Risk Rules",
  "version": "1.0.0",
  "author": "ShadowGuard",
  "description": "Example local dependency rules.",
  "enabled": true,
  "rules": [
    {
      "id": "custom.block.package",
      "name": "Blocked package name",
      "matchType": "ExactName",
      "pattern": "example-risky-package",
      "severity": "High",
      "score": 75,
      "category": "Plugin",
      "message": "This package is blocked by a local rule.",
      "recommendation": "Replace it with an approved package."
    }
  ]
}
```

## Project Structure

```text
shadowguard/
├─ ShadowGuard/                 WPF desktop application and core implementation
├─ ShadowGuard.Tests/           Lightweight behavior verification project
├─ package/                     Windows installer script
├─ plugins/                     Local JSON plugin rules
├─ samples/                     Example projects for scanning
├─ tools/                       Utility scripts
├─ .github/workflows/           CI workflow
├─ README.md                    Project documentation
├─ CHANGELOG.md                 Release notes
├─ CONTRIBUTING.md              Contribution guide
├─ SECURITY.md                  Security policy
├─ THIRD_PARTY_NOTICES.md       Third-party dependency notes
└─ shadowguard.sln              Visual Studio solution
```

## Architecture

Key components:

- `MainWindow.xaml` and `MainWindow.xaml.cs`: desktop UI, commands, result binding, and export actions.
- `Services/ProjectScanner.cs`: manifest discovery and dependency extraction.
- `Services/RiskScoringService.cs`: finding generation, scoring, and SBOM creation.
- `Services/GateDecisionService.cs`: pass/warn/block decision logic.
- `Services/PluginService.cs`: local JSON plugin loading.
- `Models/ScanModels.cs`: scan result, finding, dependency, SBOM, policy, and plugin models.
- `Utilities/`: localization, severity helpers, hashing, observable collections, and workspace path helpers.

## Development

Run the lightweight verification project:

```powershell
dotnet run --project .\ShadowGuard.Tests\ShadowGuard.Tests.csproj --configuration Release
```

Count effective source lines:

```powershell
.\tools\Count-CodeLines.ps1
```

Build everything through the solution:

```powershell
dotnet build .\shadowguard.sln --configuration Release
```

## Security Notes

ShadowGuard is a local static analysis and policy evaluation tool.

- It scans dependency manifest files.
- It does not execute code from scanned projects.
- It does not automatically modify scanned source files.
- It writes reports and SBOM files only when the user exports them.
- Plugin rules are local JSON rules loaded from `plugins/`.

For vulnerability reporting, see `SECURITY.md`.

## License

ShadowGuard is licensed under the MIT License. See `LICENSE` for details.
