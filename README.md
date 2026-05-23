# ShadowGuard

![CI](https://github.com/Fannar-afk/shadowguard/actions/workflows/ci.yml/badge.svg)

ShadowGuard 是一款基于 .NET 6 的本地供应链安全分析工具，包含 Windows 桌面端、可复用核心类库和命令行工具。它面向本地开发、联调、发布前检查和 CI/CD 集成场景，帮助开发者扫描项目依赖清单、识别潜在风险、生成 SBOM，并基于策略输出安全闸门结论。

ShadowGuard 专注于本地静态分析：不会执行被扫描项目中的代码，也不会主动修改被扫描项目源码。所有报告和 SBOM 文件都需要用户主动选择导出路径或通过命令行参数指定后才会写入磁盘。

## 目录

- [功能特性](#功能特性)
- [支持的依赖清单](#支持的依赖清单)
- [运行环境](#运行环境)
- [安装方式](#安装方式)
- [快速开始](#快速开始)
- [命令行使用](#命令行使用)
- [从源码构建](#从源码构建)
- [生成安装包](#生成安装包)
- [插件规则](#插件规则)
- [项目结构](#项目结构)
- [技术架构](#技术架构)
- [开发与验证](#开发与验证)
- [安全说明](#安全说明)
- [许可证](#许可证)

## 功能特性

- **多入口使用方式**：提供 WPF 桌面应用、`ShadowGuard.Core` 可复用类库和 `ShadowGuard.Cli` 命令行工具。
- **多生态依赖扫描**：自动识别 npm、Python、Go、.NET 等项目中的常见依赖清单；桌面端保留更完整的多生态扫描能力。
- **风险发现与处置建议**：识别可疑来源、未固定版本、预发布版本、历史供应链事件相关依赖等风险信号，并给出处理建议。
- **风险评分**：对单个组件和整个项目计算风险分，辅助判断优先处理对象。
- **SBOM 生成**：生成 CycloneDX 风格的 SBOM JSON，包含组件名称、版本、生态、PURL、来源类型、风险等级和证据文件等信息。
- **安全闸门**：根据综合风险分、恶意依赖、许可证风险和来源风险等策略输出 `Pass`、`Warn`、`Block` 结论。
- **插件规则扩展**：支持从本地 `plugins/` 目录加载 JSON 规则包，扩展名称、版本、来源和生态匹配规则。
- **报告导出**：支持从桌面端或 CLI 导出完整扫描报告和 SBOM 文件。
- **安装包与便携版构建**：通过 GitHub Actions 自动生成 Windows 安装包和 win-x64 便携版产物。

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

扫描过程中会自动跳过常见第三方目录和生成目录，例如 `node_modules`、`bin`、`obj`、`.git`、虚拟环境目录和构建输出目录，避免把外部依赖源码或生成文件误当作项目源文件处理。

## 运行环境

普通用户使用安装包或自包含便携版时，无需单独安装 .NET SDK。

源码构建需要：

- Windows 10 或 Windows 11
- .NET 6 SDK
- 支持 WPF 的 Windows 桌面环境
- PowerShell

## 安装方式

### 方式一：从 Releases 下载

发布版本会在 GitHub Releases 中提供 Windows 安装包：

```text
ShadowGuard-Setup.exe
```

下载后双击安装，根据向导完成安装即可。安装完成后，可从开始菜单或桌面快捷方式启动 ShadowGuard。

### 方式二：从 GitHub Actions 下载构建产物

每次 CI 构建会生成两个产物：

- `ShadowGuard-Setup`：Windows 安装包
- `ShadowGuard-portable-win-x64`：便携版程序目录

下载步骤：

1. 打开仓库的 **Actions** 页面。
2. 选择最新一次通过的 `CI` 工作流。
3. 在 **Artifacts** 区域下载需要的产物。
4. 如果下载的是安装包，解压后运行 `ShadowGuard-Setup.exe`。
5. 如果下载的是便携版，解压后运行 `ShadowGuard.exe`。

## 快速开始

### 使用桌面端扫描示例项目

1. 启动 ShadowGuard。
2. 点击 **加载示例**，载入 `samples/demo-workspace`。
3. 点击 **开始扫描**。
4. 查看风险发现、组件清单、SBOM 预览和安全闸门结果。
5. 根据需要导出扫描报告或 SBOM JSON。

### 使用桌面端扫描自己的项目

1. 点击 **选择目录**。
2. 选择待扫描项目的根目录。
3. 点击 **开始扫描**。
4. 在界面中查看依赖风险、证据文件、处理建议和最终闸门结论。

## 命令行使用

`ShadowGuard.Cli` 适合脚本、自动化检查和 CI/CD 集成。

扫描项目并输出报告到控制台：

```powershell
dotnet run --project .\ShadowGuard.Cli\ShadowGuard.Cli.csproj -- --path .\samples\demo-npm-risk --plugins .\plugins
```

导出完整扫描报告：

```powershell
dotnet run --project .\ShadowGuard.Cli\ShadowGuard.Cli.csproj -- --path .\samples\demo-npm-risk --plugins .\plugins --out .\artifacts\report.json
```

导出 SBOM：

```powershell
dotnet run --project .\ShadowGuard.Cli\ShadowGuard.Cli.csproj -- --path .\samples\demo-npm-risk --plugins .\plugins --format sbom --out .\artifacts\sbom.json
```

用于 CI 阻断：

```powershell
dotnet run --project .\ShadowGuard.Cli\ShadowGuard.Cli.csproj -- --path .\samples\demo-npm-risk --plugins .\plugins --fail-on-block
```

常用参数：

| 参数 | 说明 |
| --- | --- |
| `--path` / `-p` | 待扫描项目目录 |
| `--plugins` | 插件规则目录 |
| `--out` / `-o` | JSON 输出文件路径 |
| `--format` | 输出格式，支持 `report` 或 `sbom` |
| `--block-threshold` | 触发阻断的综合风险分阈值 |
| `--fail-on-block` | 安全闸门为 Block 时返回非零退出码 |
| `--fail-on-warn` | 安全闸门为 Warn 或 Block 时返回非零退出码 |

## 从源码构建

克隆仓库并构建解决方案：

```powershell
git clone https://github.com/Fannar-afk/shadowguard.git
cd shadowguard

dotnet restore .\shadowguard.sln
dotnet build .\shadowguard.sln --configuration Release
```

从源码启动桌面应用：

```powershell
dotnet run --project .\ShadowGuard\ShadowGuard.csproj
```

发布 win-x64 自包含便携版：

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

发布产物位于：

```text
artifacts\publish
```

## 生成安装包

项目使用 Inno Setup 生成 Windows 安装程序，安装脚本位于：

```text
package/ShadowGuard.iss
```

本地生成安装包前，请先安装 Inno Setup 6，并确保已经完成 `dotnet publish`。

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

### 匹配方式

| 匹配方式 | 说明 |
| --- | --- |
| `ExactName` | 精确匹配依赖名称 |
| `ContainsName` | 判断依赖名称是否包含指定片段 |
| `RegexName` | 使用正则表达式匹配依赖名称 |
| `SourceType` | 匹配依赖来源类型 |
| `VersionPattern` | 使用正则表达式匹配版本号 |
| `Ecosystem` | 匹配依赖生态 |

### 规则字段

| 字段 | 说明 |
| --- | --- |
| `id` | 规则唯一标识 |
| `name` | 规则名称 |
| `matchType` | 匹配方式 |
| `pattern` | 匹配内容或表达式 |
| `severity` | 风险等级 |
| `score` | 风险分 |
| `category` | 风险分类 |
| `message` | 风险说明 |
| `recommendation` | 处理建议 |

### 插件示例

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
├─ ShadowGuard.Core/            可复用扫描、评分、SBOM 与安全闸门核心类库
├─ ShadowGuard.Cli/             命令行扫描工具
├─ ShadowGuard.Tests/           轻量级行为验证项目
├─ package/                     Windows 安装包脚本
├─ plugins/                     本地 JSON 插件规则
├─ samples/                     示例项目
├─ tools/                       工具脚本
├─ .github/workflows/           GitHub Actions 工作流
├─ README.md                    项目说明
├─ CHANGELOG.md                 变更日志
├─ CONTRIBUTING.md              贡献指南
├─ SECURITY.md                  安全政策
├─ THIRD_PARTY_NOTICES.md       第三方依赖说明
└─ shadowguard.sln              Visual Studio 解决方案
```

## 技术架构

- `ShadowGuard.Core`：提供核心扫描器、风险评分、SBOM 构建、安全闸门和插件规则能力，适合被 CLI、测试项目和第三方程序引用。
- `ShadowGuard.Cli`：基于 `ShadowGuard.Core` 的命令行入口，适合脚本和 CI/CD 使用。
- `ShadowGuard`：WPF 桌面应用，提供图形界面、结果展示、报告导出和插件操作。
- `ShadowGuard.Tests`：轻量级核心行为验证项目，用于 CI 中快速检查关键逻辑。

## 开发与验证

构建解决方案：

```powershell
dotnet build .\shadowguard.sln --configuration Release
```

运行轻量级验证：

```powershell
dotnet run --project .\ShadowGuard.Tests\ShadowGuard.Tests.csproj --configuration Release
```

运行 CLI smoke test：

```powershell
dotnet run --project .\ShadowGuard.Cli\ShadowGuard.Cli.csproj --configuration Release -- --path .\samples\demo-npm-risk --plugins .\plugins --out .\artifacts\cli-report.json
```

统计有效源码行数：

```powershell
.\tools\Count-CodeLines.ps1
```

CI 工作流会自动执行恢复依赖、构建、轻量级验证、CLI smoke test、源码行数统计、发布产物完整性检查、便携版发布和安装包构建。

## 安全说明

ShadowGuard 是本地静态分析与策略判断工具，安全边界如下：

- 扫描依赖清单文件，不执行被扫描项目中的代码。
- 不自动修改被扫描项目源码。
- 仅在用户主动导出或通过 CLI 指定输出路径时写入报告或 SBOM 文件。
- 插件规则来自本地 JSON 文件，不会自动从远程下载执行规则。
- 插件正则匹配包含超时保护，避免异常规则长时间阻塞扫描流程。

安全问题反馈请参考 `SECURITY.md`。

## 许可证

本项目基于 MIT License 开源，详见 `LICENSE`。
