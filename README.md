# ShadowGuard

![CI](https://github.com/Fannar-afk/shadowguard/actions/workflows/ci.yml/badge.svg)

ShadowGuard 是一款基于 .NET 6 的本地供应链安全分析工具，包含 Windows 桌面端、命令行工具和可复用核心类库。它面向本地开发、联调、发布前检查和 CI/CD 集成场景，帮助开发者扫描项目依赖清单、识别潜在风险、生成 SBOM、校验 CycloneDX 结构，并可选择接入 OSV 漏洞数据源查询公开漏洞信息。

ShadowGuard 专注于本地静态分析：不会执行被扫描项目中的代码，也不会主动修改被扫描项目源码。所有报告、SBOM 和漏洞查询结果都需要用户主动导出或通过命令行参数指定后才会写入磁盘。

## 功能特性

- **桌面端使用**：提供 Windows WPF 图形界面，支持选择目录、加载示例、开始扫描、查看风险、导出报告和 SBOM。
- **命令行使用**：安装包和便携版内置 `shadowguard-cli.exe`，可用于脚本、自动化检查和 CI/CD 集成。
- **核心类库复用**：`ShadowGuard.Core` 提供扫描、评分、SBOM、漏洞查询与安全闸门能力，可被第三方 .NET 程序引用。
- **多生态依赖扫描**：自动识别 npm、Python、Go、.NET 等项目中的常见依赖清单；桌面端保留更完整的多生态扫描能力。
- **风险发现与处置建议**：识别可疑来源、未固定版本、预发布版本、历史供应链事件相关依赖等风险信号，并给出处理建议。
- **SBOM 生成与校验**：生成 CycloneDX 1.5 风格 SBOM，并提供核心结构校验。
- **漏洞数据源接入**：CLI 支持可选接入 OSV 查询漏洞；返回结果中的 `CVE-*` 与 `GHSA-*` aliases 可用于关联 CVE 与 GitHub Security Advisory 编号。
- **插件规则扩展**：支持从本地 `plugins/` 目录加载 JSON 规则包。
- **安装包与便携版构建**：通过 GitHub Actions 自动生成 Windows 安装包、win-x64 便携版产物和 SHA256 校验文件。

## 支持的依赖清单

| 生态 | 支持文件 |
| --- | --- |
| npm | `package.json`、`package-lock.json`、`yarn.lock`、`pnpm-lock.yaml` |
| Python | `requirements*.txt` |
| Go | `go.mod` |
| Rust | `Cargo.toml` |
| PHP | `composer.json` |
| Java | `pom.xml` |
| .NET | `*.csproj` |

说明：桌面端保留原有完整扫描器；`ShadowGuard.Core` 与 `ShadowGuard.Cli` 当前优先支持 npm、Python、Go 和 .NET 的核心扫描能力，后续会继续与桌面端扫描能力对齐。

## 运行环境

- 普通用户使用安装包或自包含便携版时，无需单独安装 .NET SDK。
- 源码构建需要 Windows 10/11、.NET 6 SDK、支持 WPF 的 Windows 桌面环境和 PowerShell。

## 安装方式

### 从 Releases 下载

发布版本会在 GitHub Releases 中提供：

```text
ShadowGuard-Setup.exe
ShadowGuard-portable-win-x64.zip
SHA256SUMS.txt
```

安装版双击 `ShadowGuard-Setup.exe` 后按向导安装；便携版解压后运行 `ShadowGuard.exe`。

安装包和便携版同时包含命令行工具：

```text
shadowguard-cli.exe
```

安装后可在 PowerShell 中进入安装目录使用 CLI：

```powershell
cd "C:\Program Files\ShadowGuard"
.\shadowguard-cli.exe --help
```

可以使用 PowerShell 校验下载文件完整性：

```powershell
Get-FileHash -Algorithm SHA256 .\ShadowGuard-Setup.exe
Get-Content .\SHA256SUMS.txt
```

确认输出的 SHA256 值与 `SHA256SUMS.txt` 中对应文件名的记录一致。

## 快速开始

### 使用桌面端扫描示例项目

1. 启动 ShadowGuard。
2. 点击 **加载示例**，载入内置示例项目。
3. 点击 **开始扫描**。
4. 查看风险发现、组件清单、SBOM 预览和安全闸门结果。
5. 根据需要导出扫描报告或 SBOM JSON。

### 使用桌面端扫描自己的项目

1. 点击 **选择目录**。
2. 选择待扫描项目的根目录。
3. 点击 **开始扫描**。
4. 在界面中查看依赖风险、证据文件、处理建议和最终闸门结论。

## 命令行使用

`shadowguard-cli.exe` 适合脚本、自动化检查和 CI/CD 集成。安装版默认位于：

```text
C:\Program Files\ShadowGuard\shadowguard-cli.exe
```

### 安装后直接运行

扫描安装目录中的示例项目并输出报告：

```powershell
cd "C:\Program Files\ShadowGuard"
.\shadowguard-cli.exe --path .\samples\demo-npm-risk --plugins .\plugins
```

校验 CycloneDX SBOM 并将报告输出到桌面：

```powershell
cd "C:\Program Files\ShadowGuard"
.\shadowguard-cli.exe `
  --path .\samples\demo-npm-risk `
  --plugins .\plugins `
  --validate-sbom `
  --fail-on-invalid-sbom `
  --out "$env:USERPROFILE\Desktop\validated-report.json"
```

导出 SBOM：

```powershell
.\shadowguard-cli.exe --path .\samples\demo-npm-risk --plugins .\plugins --format sbom --out "$env:USERPROFILE\Desktop\sbom.json"
```

查询 OSV 漏洞数据：

```powershell
.\shadowguard-cli.exe --path .\samples\demo-npm-risk --plugins .\plugins --vuln --vuln-provider osv --out "$env:USERPROFILE\Desktop\vulnerability-report.json"
```

### 从源码运行 CLI

源码开发环境中也可以通过 `dotnet run` 执行 CLI：

```powershell
dotnet run --project .\ShadowGuard.Cli\ShadowGuard.Cli.csproj -- --path .\samples\demo-npm-risk --plugins .\plugins
```

常用参数：

| 参数 | 说明 |
| --- | --- |
| `--path` / `-p` | 待扫描项目目录 |
| `--plugins` | 插件规则目录 |
| `--out` / `-o` | JSON 输出文件路径 |
| `--format` | 输出格式，支持 `report`、`sbom`、`validation`、`vuln` |
| `--validate-sbom` | 对生成的 CycloneDX SBOM 执行结构校验 |
| `--fail-on-invalid-sbom` | SBOM 校验失败时返回非零退出码 |
| `--vuln` | 启用漏洞数据查询 |
| `--vuln-provider` | 漏洞数据源，当前支持 `osv` |
| `--fail-on-vulnerability` | 查询到漏洞时返回非零退出码 |
| `--block-threshold` | 触发阻断的综合风险分阈值 |
| `--fail-on-block` | 安全闸门为 Block 时返回非零退出码 |
| `--fail-on-warn` | 安全闸门为 Warn 或 Block 时返回非零退出码 |

## OSV 与 GitHub Advisory 说明

当前版本通过 OSV API 进行在线漏洞查询。OSV 返回的漏洞结果中可能包含 `CVE-*`、`GHSA-*` 等 aliases；其中 `GHSA-*` 编号可用于关联 GitHub Security Advisory。项目当前没有直接调用需要认证的 GitHub Advisory GraphQL API，也不会在用户未启用 `--vuln` 时自动联网。

## 从源码构建

```powershell
git clone https://github.com/Fannar-afk/shadowguard.git
cd shadowguard

dotnet restore .\shadowguard.sln
dotnet build .\shadowguard.sln --configuration Release
```

启动桌面应用：

```powershell
dotnet run --project .\ShadowGuard\ShadowGuard.csproj
```

发布桌面端和 CLI 到同一个便携目录：

```powershell
dotnet publish .\ShadowGuard\ShadowGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o .\artifacts\publish

dotnet publish .\ShadowGuard.Cli\ShadowGuard.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o .\artifacts\publish
```

## 生成安装包

项目使用 Inno Setup 生成 Windows 安装程序，安装脚本位于：

```text
package/ShadowGuard.iss
```

本地生成安装包：

```powershell
New-Item -ItemType Directory -Force -Path .\artifacts\installer | Out-Null

$env:SHADOWGUARD_VERSION='1.0.0'
$env:SHADOWGUARD_PUBLISH_DIR=(Resolve-Path '.\artifacts\publish').Path
$env:SHADOWGUARD_OUTPUT_DIR=(Resolve-Path '.\artifacts\installer').Path

& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' .\package\ShadowGuard.iss
```

生成结果：

```text
artifacts\installer\ShadowGuard-Setup.exe
```

## 插件规则

ShadowGuard 会从本地 `plugins/` 目录加载 JSON 规则包。插件在程序启动时加载，也可以在界面中点击 **重新加载插件** 手动刷新。

支持的匹配方式：`ExactName`、`ContainsName`、`RegexName`、`SourceType`、`VersionPattern`、`Ecosystem`。

插件规则示例：

```json
{
  "pluginId": "custom-risk-rules",
  "displayName": "Custom Risk Rules",
  "version": "1.0.0",
  "author": "ShadowGuard",
  "description": "Local dependency risk rules.",
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

## 项目结构

```text
shadowguard/
├─ ShadowGuard/                 WPF 桌面应用
├─ ShadowGuard.Core/            可复用扫描、评分、SBOM、漏洞查询与安全闸门核心类库
├─ ShadowGuard.Cli/             命令行扫描工具，发布后生成 shadowguard-cli.exe
├─ ShadowGuard.Tests/           xUnit 自动化测试项目
├─ package/                     Windows 安装包脚本
├─ plugins/                     本地 JSON 插件规则
├─ samples/                     示例项目
├─ tools/                       工具脚本
└─ .github/workflows/           GitHub Actions 工作流
```

## 开发与验证

构建解决方案：

```powershell
dotnet build .\shadowguard.sln --configuration Release
```

运行 xUnit 测试：

```powershell
dotnet test .\ShadowGuard.Tests\ShadowGuard.Tests.csproj --configuration Release
```

运行 CLI smoke test：

```powershell
dotnet run --project .\ShadowGuard.Cli\ShadowGuard.Cli.csproj --configuration Release -- --path .\samples\demo-npm-risk --plugins .\plugins --validate-sbom --fail-on-invalid-sbom --out .\artifacts\cli-report.json
```

统计有效源码行数：

```powershell
.\tools\Count-CodeLines.ps1
```

CI 工作流会自动执行恢复依赖、构建、xUnit 测试、CLI smoke test、SBOM 校验、源码行数统计、发布产物完整性检查、便携版发布和安装包构建。Release 工作流还会生成 `SHA256SUMS.txt`，用于校验安装包和便携版文件完整性。

## 安全说明

- 扫描依赖清单文件，不执行被扫描项目中的代码。
- 不自动修改被扫描项目源码。
- 仅在用户主动导出或通过 CLI 指定输出路径时写入报告、SBOM 或漏洞查询结果。
- 插件规则来自本地 JSON 文件，不会自动从远程下载执行规则。
- 插件正则匹配包含超时保护，避免异常规则长时间阻塞扫描流程。
- OSV 漏洞查询仅在用户主动传入 `--vuln` 时联网执行。

安全问题反馈请参考 `SECURITY.md`。

## 许可证

本项目基于 MIT License 开源，详见 `LICENSE`。
