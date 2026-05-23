# demo-npm-risk

该目录是 ShadowGuard 的 npm 风险扫描示例项目，用于演示依赖风险识别、风险评分、SBOM 生成和安全闸门判断。

## 示例内容

本示例包含以下依赖类型：

- 历史供应链事件相关依赖：`event-stream`
- 历史高风险依赖：`ua-parser-js`
- 未固定版本依赖：`left-pad@latest`
- 预发布版本依赖：`typescript@5.0.0-rc.1`
- 普通固定版本依赖：`lodash@4.17.21`

## 使用方法

1. 启动 ShadowGuard。
2. 点击“选择目录”。
3. 选择当前目录 `samples/demo-npm-risk`。
4. 点击“开始扫描”。
5. 查看依赖风险、SBOM 预览和安全闸门结果。

## 预期效果

扫描后，ShadowGuard 应能识别出历史高风险依赖、未固定版本依赖和预发布版本依赖，并在风险列表中给出相应说明和处理建议。

该目录仅用于本地扫描演示，不需要执行 `npm install`。
