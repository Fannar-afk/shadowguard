# 变更日志

## Unreleased

### 新增

- 新增 MIT License。
- 新增 `SECURITY.md`，说明安全边界和漏洞反馈方式。
- 新增 `THIRD_PARTY_NOTICES.md`，记录第三方依赖与许可证信息。
- 新增 `case-studies/demo-workspace-scan.md`，展示示例工作区扫描流程。
- 新增 GitHub Actions CI 工作流。
- 新增 `package/ShadowGuard.iss`，支持使用 Inno Setup 生成 Windows 安装包。
- 新增 CI 构建产物上传，包括 Windows 安装包和 win-x64 便携版。
- 新增 `ShadowGuard.Tests` 轻量级行为验证项目。
- 新增 `CONTRIBUTING.md`，说明构建、验证和贡献流程。

### 变更

- 优化 README，补充项目介绍、功能特性、安装方式、源码构建、安装包生成、使用方法、插件规则、技术架构、开发验证、安全说明和许可证信息。

### 说明

`ShadowGuard.Tests` 当前采用轻量级控制台验证方式，避免为基础行为检查引入额外测试框架依赖。