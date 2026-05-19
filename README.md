# ShadowGuard

ShadowGuard 是一个基于 .NET 6 WPF 构建的桌面端供应链安全工作台，用于在本地开发、联调和发布前扫描项目依赖，识别风险，生成 SBOM，并给出安全闸门结论。

它面向的不是“事后审计”场景，而是研发过程中的“构建前安全关口”。开发、安全、测试和运维人员可以在同一个界面中完成依赖检查、风险分析、策略判断和结果导出。

## 目录

- [项目简介](#项目简介)
- [核心功能](#核心功能)
- [支持的依赖清单](#支持的依赖清单)
- [运行环境](#运行环境)
- [安装与运行](#安装与运行)
- [使用方法](#使用方法)
- [插件机制](#插件机制)
- [项目结构](#项目结构)
- [技术架构](#技术架构)
- [安全说明](#安全说明)
- [验证与文档](#验证与文档)

## 项目简介

ShadowGuard 的目标是帮助用户在本地尽早发现依赖风险，减少恶意包、来源不可信包、版本未固定包、预发布包等问题进入测试和发布流程。

项目当前提供以下价值：

- 统一扫描多种生态的依赖清单
- 识别风险并输出处理建议
- 生成 CycloneDX 风格 SBOM
- 根据策略输出通过、告警或阻断结论
- 通过插件规则扩展检测能力
- 导出扫描报告与 SBOM 文件

## 核心功能

### 1. 依赖扫描与风险发现

- 递归扫描项目目录中的依赖清单文件
- 识别直接依赖和传递依赖
- 标记来源类型，例如仓库、Git、URL、本地路径
- 展示风险等级、风险说明、处理建议和证据文件
- 提供搜索和筛选能力，便于定位重点风险

### 2. 风险评分与综合评估

- 对单个组件计算风险分
- 对项目整体计算综合风险分和风险等级
- 内置历史供应链事件包规则
- 检测未固定版本、预发布版本、可疑来源等常见风险信号

### 3. SBOM 生成与导出

- 生成 CycloneDX 1.5 风格的 SBOM 数据
- 展示组件清单、PURL、作用域、风险分等信息
- 支持导出 SBOM JSON 文件

### 4. 安全闸门决策

- 根据综合风险阈值输出闸门结论
- 支持按恶意依赖、许可证风险等条件阻断
- 输出 Pass、Warn、Block 三类结论
- 显示触发策略和结论说明

### 5. 插件扩展

- 支持从 `plugins/` 目录加载 JSON 规则包
- 支持在界面中启停插件
- 支持重新加载插件规则
- 可扩展新的名称、版本、来源和生态检测规则

### 6. 报告导出与样例演示

- 支持导出完整扫描报告
- 支持导出 SBOM 文件
- 内置示例项目，便于展示和验收

## 支持的依赖清单

当前版本支持以下清单或锁文件：

| 生态 | 文件 |
| --- | --- |
| npm | `package.json`、`package-lock.json`、`yarn.lock`、`pnpm-lock.yaml` |
| Python | `requirements*.txt` |
| Go | `go.mod` |
| Rust | `Cargo.toml` |
| PHP | `composer.json` |
| Java | `pom.xml` |
| .NET | `*.csproj` |

说明：

- 扫描器会自动过滤 `node_modules`、`bin`、`obj`、`.git`、虚拟环境等第三方或生成目录，避免把外部内容误计入项目结果。

## 运行环境

请在 Windows 环境下运行本项目。

- 操作系统：Windows 10 或 Windows 11
- SDK：.NET 6 SDK
- 桌面环境：支持 WPF
- 推荐终端：PowerShell

## 安装与运行

### 方式一：使用安装包

项目 CI 会自动构建 Windows 安装包 `ShadowGuard-Setup.exe`。

推荐普通用户使用安装包：

1. 打开 GitHub 仓库的 `Actions` 页面。
2. 选择最新一次通过的 `CI` 工作流。
3. 在 `Artifacts` 区域下载 `ShadowGuard-Setup`。
4. 解压 artifact，双击 `ShadowGuard-Setup.exe`。
5. 根据安装向导完成安装。
6. 从开始菜单或桌面快捷方式启动 ShadowGuard。

如果仓库创建了 `v*` 形式的版本标签，例如 `v1.0.0`，CI 会自动创建 GitHub Release，并把 `ShadowGuard-Setup.exe` 附加到 Release Assets 中。普通用户可以直接从 Releases 页面下载安装包。

### 方式二：使用便携版

CI 也会生成便携版 artifact：`ShadowGuard-portable-win-x64`。

使用方法：

1. 打开 GitHub Actions 中最新通过的 CI 工作流。
2. 下载 `ShadowGuard-portable-win-x64`。
3. 解压后运行其中的 `ShadowGuard.exe`。

### 方式三：源码编译运行

确保当前工作目录为项目根目录：

```powershell
cd d:\shadowguard
```

建议在 PowerShell 中设置以下环境变量：

```powershell
$env:DOTNET_CLI_HOME='d:\shadowguard\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:LOCALAPPDATA='d:\shadowguard\.localappdata'
$env:APPDATA='d:\shadowguard\.localappdata'
```

这些变量用于将缓存和本地数据限制在项目目录下，便于演示、打包和验收。

编译项目：

```powershell
dotnet build .\ShadowGuard\ShadowGuard.csproj
```

运行项目：

```powershell
dotnet run --project .\ShadowGuard\ShadowGuard.csproj
```

发布为 win-x64 便携程序：

```powershell
$env:DOTNET_CLI_HOME='d:\shadowguard\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'

dotnet publish .\ShadowGuard\ShadowGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
```

发布产物默认位于：

```text
ShadowGuard\bin\Release\net6.0-windows\win-x64\publish\
```

### 方式四：本地生成安装包

本项目提供 Inno Setup 安装脚本：

```text
package/ShadowGuard.iss
```

本地生成安装包步骤：

1. 安装 Inno Setup 6。
2. 执行 `dotnet publish` 生成 win-x64 发布目录。
3. 设置环境变量并调用 `ISCC.exe`：

```powershell
$env:SHADOWGUARD_VERSION='1.0.0'
$env:SHADOWGUARD_PUBLISH_DIR=(Resolve-Path '.\ShadowGuard\bin\Release\net6.0-windows\win-x64\publish').Path
$env:SHADOWGUARD_OUTPUT_DIR=(Resolve-Path '.\artifacts\installer').Path
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' .\package\ShadowGuard.iss
```

生成的安装程序为：

```text
artifacts\installer\ShadowGuard-Setup.exe
```

## 使用方法

### 快速体验

1. 启动 ShadowGuard。
2. 点击“加载示例”，加载 `samples/demo-workspace`。
3. 点击“开始扫描”。
4. 查看依赖风险、SBOM 和安全闸门结果。
5. 如有需要，导出报告或 SBOM 文件。

### 扫描自己的项目

1. 点击“选择目录”。
2. 选择待扫描的项目根目录。
3. 点击“开始扫描”。
4. 等待扫描完成后查看结果。

### 查看扫描结果

程序主界面分为三个主要区域：

#### 依赖防火墙

- 查看风险发现列表
- 查看组件清单
- 使用搜索框筛选风险或组件
- 查看来源文件与处理建议

#### SBOM 与风险

- 查看综合风险分
- 查看整体风险等级
- 查看 SBOM 组件清单
- 查看 SBOM JSON 预览

#### 安全闸门与插件

- 调整阻断阈值
- 配置恶意依赖、许可证风险、来源风险策略
- 查看最终闸门结论
- 启停插件并重新加载插件

### 导出结果

程序支持两类导出：

- 导出扫描报告
- 导出 SBOM JSON

导出文件由用户主动选择保存路径，程序不会自动写入被扫描项目目录。

## 插件机制

插件采用 JSON 格式，放置在 `plugins/` 目录后会被自动发现并加载。

### 当前支持的匹配方式

- `ExactName`
- `ContainsName`
- `RegexName`
- `SourceType`
- `VersionPattern`
- `Ecosystem`

### 规则字段说明

| 字段 | 含义 |
| --- | --- |
| `id` | 规则唯一标识 |
| `name` | 规则名称 |
| `matchType` | 匹配方式 |
| `pattern` | 匹配模式 |
| `severity` | 风险等级 |
| `score` | 风险分 |
| `category` | 风险分类 |
| `message` | 风险说明 |
| `recommendation` | 处置建议 |

### 插件使用步骤

1. 在 `plugins/` 目录中放入规则文件。
2. 启动程序或点击“重新加载插件”。
3. 在“安全闸门与插件”页查看插件状态。
4. 勾选或取消勾选插件启用状态。
5. 重新扫描项目以应用最新规则。

## 项目结构

```text
shadowguard/
├─ ShadowGuard/                 WPF 桌面应用与核心业务代码
├─ ShadowGuard.Tests/           轻量验证项目
├─ package/                     Windows 安装包脚本
├─ plugins/                     插件规则目录
├─ samples/                     示例项目目录
├─ tools/                       辅助脚本
├─ README.md                    项目说明文档
├─ TESTING.md                   构建与验证记录
├─ REQUIREMENTS_CHECK.md        验收要求核对说明
├─ SECURITY.md                  安全政策说明
├─ THIRD_PARTY_NOTICES.md       第三方依赖与许可证说明
└─ shadowguard.sln              解决方案文件
```

## 技术架构

核心代码职责如下：

- `MainWindow.xaml`
  负责界面布局、功能入口和结果展示。
- `MainWindow.xaml.cs`
  负责按钮事件、交互逻辑、扫描触发和导出操作。
- `Services/ProjectScanner.cs`
  负责解析多生态依赖清单并抽取组件信息。
- `Services/RiskScoringService.cs`
  负责生成风险发现、组件风险分和项目综合风险。
- `Services/GateDecisionService.cs`
  负责根据策略生成安全闸门结论。
- `Services/PluginService.cs`
  负责插件加载和管理。
- `Models/ScanModels.cs`
  负责统一数据模型。
- `Utilities/`
  提供本地化、哈希、工作区定位等公共能力。

## 安全说明

当前版本具有以下安全边界：

- 仅进行离线本地扫描
- 不执行远程脚本
- 不主动修改被扫描项目源码
- 不注入后门逻辑
- 仅在用户主动导出时写出报告或 SBOM 文件
- 仅在用户主动操作时打开插件目录

说明：

- 本项目是本地静态分析和策略判断工具，不是联网漏洞数据库同步工具。
- 许可证风险识别和补充规则主要来自本地插件包。

## 验证与文档

项目内已提供相关说明文件：

- `TESTING.md`
  构建、启动和代码审查验证记录。
- `REQUIREMENTS_CHECK.md`
  对照验收要求的逐条说明。
- `SECURITY.md`
  安全政策与漏洞反馈说明。
- `THIRD_PARTY_NOTICES.md`
  第三方依赖和许可证说明。
- `case-studies/demo-workspace-scan.md`
  多生态示例工作区应用案例。
- `samples/README.md`
  示例项目说明。
- `tools/Count-CodeLines.ps1`
  用于统计自研有效代码行数。

执行代码量统计：

```powershell
.\tools\Count-CodeLines.ps1
```

截至 2026-03-19，脚本统计结果为 `TOTAL=2319`。