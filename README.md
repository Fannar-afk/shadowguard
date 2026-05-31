# ShadowGuard

![CI](https://github.com/Fannar-afk/shadowguard/actions/workflows/ci.yml/badge.svg)

ShadowGuard 是一款面向软件供应链安全场景的本地化依赖风险分析工具。它可以在项目开发、测试和发布前，对第三方依赖进行扫描、识别和风险评估，帮助开发者了解项目中使用了哪些依赖组件、这些依赖是否存在潜在风险，并生成可用于交付、审计和自动化检查的安全报告。

项目采用 **桌面端应用 + 命令行工具 + 核心类库** 的三层结构：

- **ShadowGuard**：Windows 桌面端应用，适合普通用户通过图形界面完成项目扫描、风险查看和报告导出。
- **shadowguard-cli**：命令行工具，适合接入 PowerShell、GitHub Actions、Jenkins 等自动化流程。
- **ShadowGuard.Core**：核心类库，适合第三方 .NET 程序复用依赖扫描、风险分析、SBOM 生成和安全闸门判断能力。

ShadowGuard 专注于本地静态分析：默认不会执行被扫描项目中的代码，也不会主动修改被扫描项目源码。报告、SBOM 和漏洞查询结果只有在用户主动导出，或通过命令行参数指定输出路径时，才会写入磁盘。

## 核心功能

ShadowGuard 的功能可以概括为三部分：**依赖扫描与风险分析、SBOM 与安全闸门、插件扩展与漏洞数据关联**。

### 1. 项目依赖扫描与供应链风险分析

ShadowGuard 可以扫描本地项目目录，自动识别常见依赖清单文件，并提取依赖名称、版本号、生态类型、来源信息和证据文件。

当前支持识别的依赖文件包括：

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

扫描时，工具会自动跳过 `.git`、`node_modules`、`bin`、`obj`、`dist`、`build`、虚拟环境目录等常见无关目录，减少构建产物、缓存目录和第三方源码对扫描结果的干扰。

在识别依赖后，ShadowGuard 会基于内置规则进行供应链风险分析，包括但不限于：

- 历史高风险或供应链事件相关依赖
- 未固定版本依赖，例如 `latest`
- 预发布版本依赖，例如 `alpha`、`beta`、`rc`
- Git、URL 等异常来源依赖
- 许可证或来源不明确的依赖

每个风险项会给出风险等级、风险分、风险说明和处理建议，帮助开发者快速判断哪些依赖需要优先处理。

### 2. SBOM 生成、校验与安全闸门判断

ShadowGuard 可以根据扫描结果生成 CycloneDX 1.5 风格的 SBOM（Software Bill of Materials，软件物料清单），用于记录项目中的第三方组件信息。

生成的 SBOM 主要包含：

- 项目信息
- 组件名称与版本
- 组件生态类型
- PURL 信息
- 依赖来源
- 风险等级与风险分
- 证据文件路径

SBOM 可用于项目交付、安全审计、合规检查或后续安全平台处理。

ShadowGuard 还提供 SBOM 核心结构校验能力，可以检查：

- `bomFormat`
- `specVersion`
- `metadata`
- `components`
- `bom-ref`
- `purl`
- 组件名称、版本和类型
- 组件 scope 等关键字段

除 SBOM 外，ShadowGuard 会根据整体风险情况生成安全闸门结论：

| 结论 | 含义 |
| --- | --- |
| Pass | 未发现明显风险，项目可继续发布。 |
| Warn | 存在需要人工复核的问题。 |
| Block | 命中高风险策略，不建议继续发布。 |

在 CI/CD 场景中，可以通过命令行参数让工具在风险达到阻断条件时返回非零退出码，从而终止后续构建或发布流程。

### 3. 插件规则扩展与漏洞数据关联

ShadowGuard 支持通过本地 JSON 插件规则扩展风险识别逻辑。用户可以在 `plugins/` 目录下维护自定义规则，无需修改源码即可扩展风险判断能力。

插件规则可以匹配：

- 依赖名称
- 版本号
- 来源类型
- 生态类型
- 正则表达式规则
- 预发布版本模式
- 自定义风险分和处理建议

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

此外，ShadowGuard.Cli 支持可选接入 OSV 漏洞数据库。用户显式启用 `--vuln` 参数后，工具会根据依赖名称和版本查询公开漏洞信息，并输出漏洞编号、摘要、组件名称、版本、CVE / GHSA 别名等信息。

默认情况下，ShadowGuard 不会自动联网。只有在命令行中显式传入 `--vuln` 参数时，才会执行在线漏洞查询。项目当前通过 OSV 返回结果中的 `CVE-*` 与 `GHSA-*` aliases 关联漏洞编号，没有直接调用需要认证的 GitHub Advisory GraphQL API。

## 运行环境

- 普通用户使用安装包或自包含便携版时，无需单独安装 .NET SDK。
- 源码构建需要 Windows 10/11、.NET 6 SDK、支持 WPF 的 Windows 桌面环境和 PowerShell。
- 开发工具推荐 Visual Studio 2022 或 VS Code。
- 安装包构建需要 Inno Setup。

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

1. 启动 `ShadowGuard.exe`。
2. 点击 **加载示例**，载入内置示例项目。
3. 点击 **开始扫描**。
4. 查看依赖列表、风险说明、处理建议和安全闸门结果。
5. 根据需要导出扫描报告或 SBOM JSON。

### 使用桌面端扫描自己的项目

1. 点击 **选择目录**。
2. 选择待扫描项目的根目录。
3. 点击 **开始扫描**。
4. 在界面中查看依赖风险、证据文件、处理建议和最终闸门结论。
5. 根据结果决定继续发布、人工复核或修复依赖问题。

### 使用 CLI 扫描项目

`shadowguard-cli.exe` 适合脚本、自动化检查和 CI/CD 集成。安装版默认位于：

```text
C:\Program Files\ShadowGuard\shadowguard-cli.exe
```

扫描安装目录中的示例项目并输出报告：

```powershell
cd "C:\Program Files\ShadowGuard"
.\shadowguard-cli.exe --path .\samples\demo-npm-risk --plugins .\plugins
```

从源码运行 CLI：

```powershell
dotnet run --project .\ShadowGuard.Cli\ShadowGuard.Cli.csproj -- --path .\samples\demo-npm-risk --plugins .\plugins
```

## 命令行使用

### 基础扫描

```powershell
.\shadowguard-cli.exe `
  --path .\samples\demo-npm-risk `
  --plugins .\plugins `
  --out .\artifacts\report.json
```

### 生成 SBOM

```powershell
.\shadowguard-cli.exe `
  --path .\samples\demo-npm-risk `
  --plugins .\plugins `
  --format sbom `
  --out .\artifacts\sbom.json
```

### 校验 SBOM

校验 CycloneDX SBOM，并在校验失败时返回非零退出码：

```powershell
.\shadowguard-cli.exe `
  --path .\samples\demo-npm-risk `
  --plugins .\plugins `
  --validate-sbom `
  --fail-on-invalid-sbom `
  --out .\artifacts\validated-report.json
```

### 安全闸门阻断发布

当安全闸门结果为 Block 时返回非零退出码：

```powershell
.\shadowguard-cli.exe `
  --path .\samples\demo-npm-risk `
  --plugins .\plugins `
  --fail-on-block
```

### 查询 OSV 漏洞数据

```powershell
.\shadowguard-cli.exe `
  --path .\samples\demo-npm-risk `
  --plugins .\plugins `
  --vuln `
  --vuln-provider osv `
  --out .\artifacts\vulnerability-report.json
```

发现漏洞时阻断流程：

```powershell
.\shadowguard-cli.exe `
  --path .\samples\demo-npm-risk `
  --plugins .\plugins `
  --vuln `
  --vuln-provider osv `
  --fail-on-vulnerability
```

### 输出 SARIF 供 GitHub Code Scanning 使用

ShadowGuard CLI 支持 `--format sarif`，可将依赖风险结果输出为 SARIF 2.1.0。该格式适合上传到 GitHub Code Scanning 或其他支持 SARIF 的安全平台。

```powershell
.\shadowguard-cli.exe `
  --path .\samples\demo-npm-risk `
  --plugins .\plugins `
  --format sarif `
  --out .\artifacts\shadowguard.sarif
```

如需在 GitHub Actions 中上传 SARIF，可结合 `github/codeql-action/upload-sarif` 使用。

```yaml
- name: Run ShadowGuard
  run: .\shadowguard-cli.exe --path . --plugins .\plugins --format sarif --out shadowguard.sarif

- name: Upload SARIF
  uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: shadowguard.sarif
```

### 常用参数

| 参数 | 说明 |
| --- | --- |
| `--path` / `-p` | 指定待扫描项目目录 |
| `--plugins` | 指定本地插件规则目录 |
| `--out` / `-o` | 指定 JSON 输出路径 |
| `--format` | 指定输出格式，支持 `report`、`sbom`、`validation`、`vuln`、`sarif` |
| `--validate-sbom` | 启用 CycloneDX 1.5 风格 SBOM 核心结构校验 |
| `--fail-on-invalid-sbom` | SBOM 校验失败时返回非零退出码 |
| `--fail-on-block` | 安全闸门结果为 Block 时返回非零退出码 |
| `--fail-on-warn` | 安全闸门结果为 Warn 或 Block 时返回非零退出码 |
| `--block-threshold` | 触发阻断的综合风险分阈值 |
| `--vuln` | 启用漏洞数据查询 |
| `--vuln-provider` | 漏洞数据源，当前支持 `osv` |
| `--fail-on-vulnerability` | 查询到漏洞时返回非零退出码 |

## GitHub Action 集成

本仓库提供 `action.yml`，可作为复合 GitHub Action 在其他仓库中调用。它会恢复 ShadowGuard 项目并运行 CLI，适合在 pull request 或发布前执行供应链风险检查。

```yaml
name: ShadowGuard

on:
  pull_request:
  push:
    branches: [main]

jobs:
  scan:
    runs-on: windows-latest
    permissions:
      contents: read
      security-events: write
    steps:
      - uses: actions/checkout@v4

      - uses: Fannar-afk/shadowguard@main
        with:
          path: .
          plugins: plugins
          format: sarif
          output: shadowguard.sarif
          validate-sbom: "true"
          fail-on-invalid-sbom: "true"
          fail-on-block: "true"

      - uses: github/codeql-action/upload-sarif@v3
        if: always()
        with:
          sarif_file: shadowguard.sarif
```

## 作为 .NET 类库集成

ShadowGuard 的核心扫描能力封装在 `ShadowGuard.Core` 中，第三方 .NET 程序可以通过项目引用或 DLL 引用方式接入。

在 `.csproj` 中添加引用：

```xml
<ItemGroup>
  <ProjectReference Include="..\ShadowGuard.Core\ShadowGuard.Core.csproj" />
</ItemGroup>
```

在 C# 代码中调用：

```csharp
using ShadowGuard;

var engine = new ShadowGuardEngine();

var result = engine.Scan(
    targetPath: @"D:\Projects\demo-project"
);

Console.WriteLine(result.Summary.TotalDependencies);
Console.WriteLine(result.GateDecision.Outcome);
```

这种方式适合将 ShadowGuard 集成到企业内部项目检查工具、实验教学平台、代码质量检测系统或发布审批系统中。

## 适用场景

ShadowGuard 适合用于以下场景：

- 开源项目发布前的依赖风险检查
- 企业内部项目的供应链安全预检查
- CI/CD 构建流程中的安全闸门
- 生成 SBOM 供交付、审计或合规检查使用
- 对第三方依赖进行本地化风险分析
- 教学、实验或安全工具链集成

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
├─ action.yml                   可复用 GitHub Action 入口
└─ .github/workflows/           GitHub Actions 工作流
```

## 设计特点

- **本地优先**：默认扫描不联网，适合本地项目和内网环境使用。
- **可视化操作**：桌面端支持项目选择、扫描、风险查看和报告导出。
- **自动化友好**：CLI 支持非零退出码，可接入 CI/CD 流程。
- **平台集成**：CLI 支持 SARIF 输出，仓库提供可复用 GitHub Action 入口。
- **规则可扩展**：通过 JSON 插件规则扩展风险识别逻辑。
- **组件可复用**：核心能力封装为 .NET 类库，便于第三方程序集成。
- **SBOM 支持**：可生成 CycloneDX 1.5 风格的软件物料清单。

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


## 贡献者说明

欢迎围绕依赖识别规则、示例项目、风险说明和文档细节提交改进。为了便于审阅，建议每次提交聚焦一个主题，并在描述中说明变更动机和验证方式。

## 许可证

本项目基于 MIT License 开源，详见 `LICENSE`。
