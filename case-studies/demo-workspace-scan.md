# ShadowGuard 应用案例：多生态示例工作区发布前依赖检查

> 说明：本案例基于仓库内置示例目录 `samples/demo-workspace`，用于演示 ShadowGuard 的完整扫描流程。该案例可作为演示与验收材料，但不应被表述为第三方生产环境案例。

## 应用场景

在一个包含多种语言和包管理生态的项目发布前，开发或安全人员需要快速了解依赖清单、潜在供应链风险、SBOM 内容以及安全闸门结论。

`demo-workspace` 覆盖的依赖清单类型包括：

- npm：`package.json`、`package-lock.json`、`yarn.lock`、`pnpm-lock.yaml`
- Python：`requirements*.txt`
- Go：`go.mod`
- Rust：`Cargo.toml`
- PHP：`composer.json`
- Java：`pom.xml`
- .NET：`*.csproj`

## 使用方式

1. 在 Windows 环境中启动 ShadowGuard。
2. 点击“加载示例”，或手动选择 `samples/demo-workspace` 目录。
3. 点击“开始扫描”。
4. 在“依赖防火墙”页查看风险发现和组件清单。
5. 在“SBOM 与风险”页查看综合风险分、整体风险等级和 SBOM JSON 预览。
6. 在“安全闸门与插件”页查看 Pass、Warn 或 Block 结论。
7. 根据需要导出扫描报告或 SBOM JSON 文件。

## 预期效果

完成扫描后，用户可以获得：

- 多生态依赖组件清单。
- 每个组件的来源文件、生态、版本、依赖类型和风险等级。
- 风险发现列表及处置建议。
- CycloneDX 风格 SBOM JSON。
- 安全闸门结论和触发策略说明。

## 价值说明

该案例展示了 ShadowGuard 在发布前检查中的价值：

- 帮助开发者在构建前识别不可信来源、未固定版本、预发布版本和历史高风险依赖。
- 为安全、测试和运维人员提供统一的本地检查界面。
- 为交付和归档提供 SBOM 与扫描报告。

## 局限说明

当前案例来自仓库内置示例目录，不代表真实第三方生产环境接入。若用于正式验收中的“真实应用案例”要求，建议补充实际项目名称、使用人员、扫描时间、扫描结果、整改过程和使用效果截图。
