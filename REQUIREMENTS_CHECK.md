# ShadowGuard 验收要求核对说明

本文档用于对照项目常见验收材料要求，说明当前仓库已经具备的证据、仍不满足的内容以及后续补强方向。

## 一、功能介绍

当前状态：满足。

ShadowGuard 是基于 .NET 6 WPF 构建的 Windows 桌面端供应链安全工作台，具备用户交互界面，可通过 `dotnet build` 编译，并可通过 `dotnet publish` 发布为可执行程序。

项目已实现的相互独立功能包括：

1. 依赖扫描与风险发现：扫描项目目录中的多生态依赖清单，识别直接依赖、传递依赖、来源类型和证据文件。
2. 风险评分与综合评估：根据内置规则、来源类型、版本形态和插件规则计算组件风险分与项目综合风险分。
3. SBOM 生成与导出：生成 CycloneDX 风格 SBOM 数据，并支持导出 JSON 文件。
4. 安全闸门决策：根据阻断阈值、恶意依赖、许可证风险和来源风险输出 Pass、Warn、Block 结论。
5. 插件扩展：从 `plugins/` 目录加载 JSON 规则包，支持启停插件和重新加载插件。
6. 报告导出：支持导出完整扫描报告和 SBOM 文件。

主要证据文件：

- `ShadowGuard/ShadowGuard.csproj`
- `ShadowGuard/MainWindow.xaml`
- `ShadowGuard/MainWindow.xaml.cs`
- `ShadowGuard/Services/ProjectScanner.cs`
- `ShadowGuard/Services/RiskScoringService.cs`
- `ShadowGuard/Services/GateDecisionService.cs`
- `ShadowGuard/Services/PluginService.cs`

## 二、社会价值

当前状态：部分满足。

项目的社会价值主要体现在帮助开发者在构建前发现依赖供应链风险，减少恶意包、来源不可信包、未固定版本包和预发布包进入测试或发布流程的概率，并通过 SBOM 输出提升软件供应链透明度。

但“被其他软件引用、依赖和关联”这一要求需要额外证据。目前项目以桌面应用为主，尚未拆分独立类库或发布 NuGet 包，也未提供第三方项目依赖本项目的公开记录。因此该项如按严格标准审核，应说明为“社会价值具备，但第三方引用/依赖证据仍需补充”。

建议后续补强：

- 拆分 `ShadowGuard.Core` 类库，暴露扫描、评分、SBOM 和闸门判定接口。
- 新增 `ShadowGuard.Cli` 命令行工具，方便 CI/CD 和第三方系统调用。
- 发布 NuGet 包或提供可被第三方项目引用的 SDK 示例。
- 增加至少一个真实项目使用案例。

## 三、文档说明

当前状态：满足。

`README.md` 已说明项目简介、核心功能、支持的依赖清单、运行环境、安装运行步骤、使用方法、插件机制、项目结构、技术架构、安全说明和验证方式。

主要安装与运行步骤包括：

1. 准备 Windows 10/11 与 .NET 6 SDK 环境。
2. 进入项目根目录。
3. 配置本地缓存相关环境变量。
4. 执行 `dotnet build .\ShadowGuard\ShadowGuard.csproj` 编译项目。
5. 执行 `dotnet run --project .\ShadowGuard\ShadowGuard.csproj` 启动桌面应用。
6. 可通过 `dotnet publish` 发布为 win-x64 单文件程序。

## 四、安全测试

当前状态：基本满足。

`TESTING.md` 已记录构建验证、启动验证、代码行数统计和人工代码审查结论。根据现有记录，项目已人工检查远程执行、套接字通信、注册表篡改、未经授权的大规模写文件或删除文件、后门式隐藏逻辑等风险点，当前未发现恶意代码或后门逻辑。

建议后续补强：

- 增加 GitHub Actions 自动构建验证。
- 增加单元测试与集成测试。
- 增加安全扫描工具，例如 CodeQL 或依赖审计。
- 将扫描失败、解析失败等异常信息纳入报告，避免静默忽略。

## 五、代码截图

当前状态：满足。

项目提供 `tools/Count-CodeLines.ps1` 用于统计自研有效代码行数。脚本统计 `.cs` 和 `.xaml` 文件，并排除 `bin/`、`obj/`、空行和注释。

`TESTING.md` 记录截至 2026-03-19 的统计结果为：

```text
TOTAL=2319
```

该结果超过 1000 行要求。

## 六、贡献者的贡献记录

当前状态：部分满足。

仓库已有提交和 Pull Request 记录，可作为贡献记录证据。当前可见贡献者包括：

- `Fannar-afk`：仓库维护、主分支合并、文档版本调整。
- `hui0323`：提交扫描器与规则匹配性能优化相关 PR。
- `czj-code`：提交 README 文档优化和 UI/扫描结果性能优化相关 PR。

建议提交材料时补充 GitHub Contributors、Commits、Pull Requests 页面截图，并在截图旁配文字说明各贡献者负责内容。

## 七、其他佐证材料

当前状态：部分满足。

### 许可证

当前仓库已补充 MIT License。项目当前 `.csproj` 未引入第三方 NuGet 包，主要使用 .NET SDK、WPF、Windows Forms 等平台能力。若后续引入第三方包，应补充 `THIRD_PARTY_NOTICES.md` 并检查许可证兼容性。

### 应用案例

仓库提供 `samples/demo-node` 与 `samples/demo-workspace`，可用于演示多生态依赖扫描流程。该案例适合作为演示案例，但不能完全替代真实生产使用案例。

建议后续补充真实应用案例，包括应用场景、使用方式、扫描结果、整改动作和使用效果。

### 证书、专利或权威认可

当前未提供与核心功能相关的证书、专利或权威认可材料。如后续取得相关证明，可在本节补充。
