# CURSORIUM v1.5.1 — 下载目录兼容性修复

2026-10-14。基线 c59bd2602bdbd8d90c12bec501c8519c852c85ce / cursorium-v1.5.0。
本次仍为 Stability preview，继承 v1.5 的硬件及长期测试限制，不能标记 Steam Ready。

## 故障与修复

用户下载 Simple Dot or Star 时出现临时 resource 目录“Access is denied”。
已从公开网络下载原资源，并在独立的 LocalAppData 测试目录重现：签名、包 SHA256、
CUR/ANI 与角色验证成功，随后 DownloadManager.InstallCursor 的 Directory.Move 失败。
该机器 LocalAppData 目录具有 Encrypted 属性；独立的普通目录移动也失败，同一测试
目录内的文件复制、文件重命名与递归清理成功。此结果不代表所有 EFS 环境都会失败。

改用现有 CursorPack 内的受控提交方法：仅复制 CUR/ANI、根 manifest 和 NOTICE；
检查重解析点、嵌套深度、条目数、单文件和总体积；逐文件核对 SHA256；
验证目标 CUR/ANI 与临时 manifest 后，才原子重命名 manifest 为 theme.json。
在线资源仍以不可变 SHA256 版本目录保存，根安装记录仍是最终提交点。
取消或复制失败时删除本次新建的不完整目标，不覆盖已有目录及旧版记录。
同一修复用于本地导入，避免其整目录移动遇到同样故障。

另修复重复点击 Try Live Preview 时旧预览包被替换但未释放的问题：创建下一次预览前，
按既有顺序释放原生光标、动画与暂存资源。下载与 Apply 保持分离，ThemeService 和
StudioService 的事务机制未修改；没有改变用户文件加密设置或要求管理员权限。

## 已执行验证

| 检查 | 实际结果 |
| --- | --- |
| dotnet build Frost.sln -c Release | 0 error / 0 warning |
| 新增真实网络与文件系统检查 | 14 项通过，独立 LocalAppData，Encrypted=true |
| 原核心检查（未启用原生 roundtrip） | 162 项通过；没有冒称本次运行全部 180 项 |
| 在线下载与 ZIP 安全检查 | 51 项通过 |
| 资源策展、版本更新兼容检查 | 104 项通过 |
| 真实网络/WPF 工作流 | 107 项通过，在加密的隔离用户数据目录运行 |

新增检查覆盖真实包、15 角色、2 个动态角色、预览打开时安装、重复下载、重启发现、
本地导入回退、取消、复制 I/O 失败清理和已有目录保护。
WPF 验证包含 Simple Dot or Star 的真实预览和下载、重复预览释放、Installed 按钮、
既有主题/拖尾下载、Apply/Cancel、Profile、更新失败保护和结束后的原生快照恢复。
单纯下载检查对比全部 Windows 指针值及类型，确认无系统变更。

证据：[专项报告](validation/v1.5.1/download-filesystem-report.json)；
[实际 WPF 截图](validation/v1.5.1/simple-dot-star-installed.png)。
原始失败堆栈、完整回归报告及本机恢复资料仅保存在忽略的 artifacts/v1.5.1-* 下，
不上传含本机路径、原始注册表或用户设置的资料。

## 复现与交付工具

```powershell
dotnet build Frost.sln -c Release
dotnet run --project tests/Frost.Verify -c Release --no-build -- --download-filesystem-tests D:/CURSORIUM artifacts/download-proof "$env:LOCALAPPDATA/CursoriumDownloadProbe-$([guid]::NewGuid().ToString('N'))"
dotnet run --project tests/Frost.Verify -c Release --no-build -- --studio-tests D:/CURSORIUM artifacts/core-proof
dotnet run --project tests/Frost.Verify -c Release --no-build -- --online-tests D:/CURSORIUM tests/fixtures/online artifacts/online-proof
./scripts/build-release.ps1 -Version 1.5.1
./scripts/verify-release.ps1 -IsolatedUpgrade -CrossVolume -Version 1.5.1
./scripts/verify-installed-upgrade.ps1 -Version 1.5.1
./scripts/publish-github.ps1 -Release -Prerelease -Version 1.5.1 -NotesPath docs/releases/1.5.1.md
```

完整包、隔离升级与真实安装报告分别生成于 artifacts/cursorium-release、
artifacts/cute-v1.2/upgrade-isolated、artifacts/v1.5.1-installed。
升级前的程序 ZIP、用户数据副本和哈希、原生注册表快照已在
artifacts/v1.5.1-baseline 创建并校验。实际安装流程再次建立独立回退点。
本次无需修改官方 library Catalog、主题包或旧 Release。

## 限制

实际环境为 Windows 11、单屏 2560×1440、100% DPI；未新增 Windows 10、双屏、
物理混合 DPI 或 24/72 小时测试。v1.5 的其他兼容性和性能限制继续有效。
更新后，原先失败的主题需在 Explore 再次点击 Download；不会自动替用户 Apply。
