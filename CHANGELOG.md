# 变更日志

## Unreleased

### 新增

- 新增 MIT License。
- 新增 `SECURITY.md`，说明安全边界和漏洞反馈方式。
- 新增 `THIRD_PARTY_NOTICES.md`，记录第三方依赖与许可证信息。
- 新增 `CONTRIBUTING.md`，说明构建、测试、CLI smoke test 和贡献流程。
- 新增 GitHub Actions CI 与 Release 工作流。
- 新增 `package/ShadowGuard.iss`，支持使用 Inno Setup 生成 Windows 安装包。
- 新增 CI 构建产物上传，包括 Windows 安装包、win-x64 便携版和 xUnit 测试结果。
- 新增 `ShadowGuard.Core` 可复用核心类库，提供扫描、评分、SBOM、安全闸门、插件规则和漏洞查询相关能力。
- 新增 `ShadowGuard.Cli` 命令行工具，支持报告导出、SBOM 导出、SBOM 校验、OSV 漏洞查询和 CI 阻断模式。
- 新增 `ShadowGuard.Tests` xUnit 自动化测试项目。
- 新增 CycloneDX SBOM 核心结构校验能力。
- 新增 OSV 漏洞查询能力，并支持提取 `CVE-*` 与 `GHSA-*` aliases。
- 新增 `samples/demo-npm-risk`，用于演示 npm 依赖风险扫描。
- 新增 `tools/Verify-ReleasePayload.ps1`，用于发布前校验安装包必需资源是否完整。

### 变更

- 优化 README，补充项目介绍、功能特性、安装方式、源码构建、命令行使用、SBOM 校验、漏洞查询、插件规则、技术架构、开发验证、安全说明和许可证信息。
- CI 与 Release 流程改为执行 `dotnet test`，并在发布前运行 CLI smoke test 与 SBOM 校验。
- 发布产物包含 `samples/`、`plugins/` 和 `docs/`，确保安装后示例、插件和用户文档可用。
- `.gitignore` 增加对 `artifacts/`、测试结果和 NuGet 包产物的忽略规则。

### 说明

- OSV 漏洞查询仅在用户主动使用 CLI 参数 `--vuln` 时联网执行。
- 当前通过 OSV 返回的 `GHSA-*` aliases 关联 GitHub Security Advisory 编号；暂未直接接入需要认证的 GitHub Advisory GraphQL API。
- 当前 CycloneDX 校验覆盖核心结构、必填字段和常见一致性规则；尚未引入官方 CycloneDX JSON Schema 文件进行完整 schema validation。
