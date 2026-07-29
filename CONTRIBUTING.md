# Contributing to VoluLens

感谢你参与 VoluLens。

## 开始前

1. 先搜索已有 Issue，避免重复报告。
2. 为一个明确问题创建分支并保持改动范围小。
3. 在提交 Pull Request 前运行全部测试。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-dotnet.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dotnet.ps1 test VoluLens.sln -c Release
```

## 安全边界

涉及扫描、路径访问或清理的改动必须保持以下规则：

- 扫描保持只读，不读取文件内容，不请求管理员提权。
- 跳过重解析点，不跟随目录链接。
- 清理请求必须来自当前扫描结果，并验证 finding ID、路径范围和规范化路径。
- Safe 项只能移入回收站；Review 项需要审核页确认；Protected 项只能通过系统或应用引导处理。
- 不增加永久删除入口，不绕过 Windows 原生确认。
- 导出的 HTML 报告不得包含 WebView2 原生桥接或清理能力。

## Pull Request

请在 PR 描述中说明：问题背景、行为变化、验证命令与结果。对界面修改，请附上不含真实用户名、文件路径或私人目录的截图。

