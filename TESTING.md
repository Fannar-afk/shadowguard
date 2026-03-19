# Testing

最新验证时间：2026-03-19

## 已完成验证

1. 构建验证

执行命令：

```powershell
$env:DOTNET_CLI_HOME='d:\shadowguard\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet build .\ShadowGuard\ShadowGuard.csproj
```

结果：

- 构建成功
- 0 个警告
- 0 个错误

2. 启动验证

执行方式：启动调试版 `ShadowGuard.exe`，等待 3 秒后检查进程状态。

结果：

- 返回 `RUNNING:45440`
- 说明程序可以正常启动并保持运行

3. 自研代码行数统计

执行命令：

```powershell
.\tools\Count-CodeLines.ps1
```

结果：

- `TOTAL=2319`
- 已排除注释、空行、`bin/`、`obj/` 生成内容

4. 代码审查验证

已人工检查以下风险点：

- 远程执行
- 套接字通信
- 注册表篡改
- 未经用户授权的大规模写文件或删除文件
- 后门式隐藏逻辑

当前结论：未发现恶意代码或后门逻辑。

## 当前仍可继续增强的部分

以下项目不影响当前 5 条基本要求，但后续建议继续补充：

- 扫描器单元测试
- 风险评分规则测试
- 插件规则解析测试
- WPF UI 自动化测试
