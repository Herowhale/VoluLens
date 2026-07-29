# GitHub 上传与发布教程

本仓库将源码和 Windows 程序包分开发布：源码提交到 Git 仓库，`VoluLens-win-x64.zip` 作为 GitHub Release 附件上传。

## 上传前检查

在仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-open-source.ps1 -RequireRelease
git status --short --ignored
```

确认 `release-assets/`、`.dotnet/`、`.packages/`、`.cache/`、`artifacts/`、`bin/`、`obj/` 和 `WebView2Data/` 没有被暂存。

## 方式一：GitHub 网页 + Git 命令行

1. 登录 GitHub，选择 **New repository**。
2. 仓库名称建议为 `VoluLens`，可选择 Public。
3. 不要勾选自动创建 README、License 或 `.gitignore`，因为本地目录已有这些文件。
4. 创建仓库后复制 HTTPS 地址，例如 `https://github.com/<YOUR_GITHUB_USERNAME>/VoluLens.git`。
5. 在本地仓库根目录运行：

```powershell
git config --global user.name "<YOUR_NAME>"
git config --global user.email "<YOUR_EMAIL>"
git add .
git commit -m "chore: prepare VoluLens open-source release"
git remote add origin https://github.com/<YOUR_GITHUB_USERNAME>/VoluLens.git
git push -u origin main
```

将尖括号中的内容替换为你的 GitHub 用户名、显示名称和邮箱；不要直接复制尖括号。

## 创建第一个 Release

1. 在 GitHub 仓库页面打开 **Releases**，选择 **Draft a new release**。
2. 创建标签，例如 `v0.1.0`，目标分支选择 `main`。
3. Release title 使用 `VoluLens v0.1.0`。
4. 上传本地 `release-assets\VoluLens-win-x64.zip`。
5. 发布前确认 ZIP 内只有 `VoluLens.App.exe`、`README.txt` 和 `Assets/LUCIDE-LICENSE.txt`。
6. 选择 **Publish release**。

## 后续更新

```powershell
git add .
git commit -m "feat: describe the change"
git push
git tag v0.1.1
git push origin v0.1.1
```

然后在 GitHub 创建对应的 Release 并上传新的 ZIP。不要使用 Git LFS，也不要把大型 EXE 直接提交到 Git 历史。

