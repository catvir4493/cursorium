# v1.5 兼容性与 Steam 前置边界

本轮以 Windows 11 x64、单屏 2560×1440、96 DPI 的实际桌面为依据。软件布局模拟不能证明硬件兼容性。此版本为验证预发布，不标记 Steam Ready。

|场景|覆盖方式与结论|
|---|---|
|Windows 11 / 当前显示器 / 100%|真实 WPF、Raw Input、透明层、窗口管理、托盘、指针事务回归|
|1920×1080、1366×768 对应工作区及小窗口|既有逻辑布局检查；没有声称物理显示模式切换|
|125%、150%|既有等效 DIP 布局模拟；物理 DPI 未测|
|Windows 10 Enterprise LTSC 2021 x64|候选测试目标；未实测，不列为已验证支持|
|双屏、横/纵排列、交换主屏、不同刷新率、混合 DPI|缺少对应硬件实测，保留验收项|
|物理热插拔|未测；诊断覆盖软件触发 Overlay 重建，不等同于热插拔|
|锁屏/解锁、用户会话切换|新增 SessionSwitch 清空和重检；实际会话切换未测|
|睡眠/唤醒、显示器电源变化、RDP|现有安全检查保留；真实生命周期未测|
|普通全屏窗口暂停|既有真实跨进程探针测试；独占游戏、安全桌面未测|
|Explorer 重启|NotifyIcon 由 WinForms 管理；实际 Explorer 重启未测|
|24h / 72h|提供运行命令和输出；没有完成对应时长，不标为通过|

## .NET 10 决策依据

独立工作树保留原 v1.4 源码，使用 `codex/net10-migration-v1.5` 分支评估；原 `cursorium-v1.4.0` 标签及已发布资产不变。迁移目标 `net10.0-windows`，保持 C# 12、WPF/Win32、原二进制/命名空间/IPC/数据路径，不引入第三方框架。自包含发布明确使用 **10.0.13**；本机 SDK 为 10.0.103，框架依赖开发检查使用本机 10.0.3，正式自包含 WPF 需另行回归。

微软[运行时支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy)确认 .NET 8 于 2026-11-10 结束支持，.NET 10 为 LTS；[Windows 支持表](https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions)将 Windows 10 限定在列出的 LTSC/Enterprise 版本。运行时官方支持并不证明本软件已经实测对应系统。补丁版本以[官方发布元数据](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)及交付 runtimeconfig 为准。

混合 WPF/WinForms 工程继续由既有 manifest 设置 PerMonitorV2。WinForms 仅承担托盘/安装器消息框；WFO0003 在这两个宿主中单独排除，避免按 WinForms 建议删除 WPF 的原生 DPI 声明。没有禁用通用编译警告；须以实际 DPI 和输入测试证明行为。

## Steam 文件与生命周期边界

USER32 永久指针路径上限：本机实测达到 260 字符时 LoadImage 失败，extended prefix 也失败。长来源文件可校验/预览，但永久系统资源路径必须 <260；否则 Apply 在注册表操作前明确拒绝。正常 LocalAppData 默认路径已验证；特别长的 Windows 用户名/数据根仍需额外验证。本版不声称支持任意长度的系统指针路径。

- 程序和官方内置资源：安装目录内的 `FrostWhiteComet.exe`、自包含 .NET/WPF 依赖、Theme、Themes、Catalog、图标与文档。每个正式负载包含 `distribution-manifest.json`，列出相对路径、大小、SHA256；清单不包含自身哈希。
- 可写数据：`%LOCALAPPDATA%/FrostWhiteComet`；导入资源 Themes、下载的 MotionLibrary、不可变 Theme/installed、Profile、收藏、原始备份、事务和界面偏好保留在此。不能随 Steam depot 更新覆盖。
- 缓存：Online/Media 与进程世代标识 Staging；不是正式原生指针存储。Windows 当前引用的不可变文件禁止按普通缓存清理。
- 托盘：关闭面板隐藏，Quit 停止引擎并释放窗口/输入/托盘；已安装 Windows 光标独立保留。
- 将来卸载接口：复用 `--restore-theme` 与 `StudioService.RestoreSavedState`；必须先成功恢复，再删除受管理程序。外部指针修改、缺失资源、无权限、未知 schema 导致恢复失败时停止删除。
- 本轮保留 FrostWhiteComet.exe、Frost.*、原 HKCU Run/卸载键和管道/互斥体。商业品牌/路径迁移留给 v1.6，不能直接批量重命名。
- 未集成 Steamworks、Workshop、默认遥测或账户。GitHub 固定公钥、RSA-PSS、官方域名、SHA256 和安全导入边界不变。

## 尚需解决的发行阻断

部分主动模式的单次 4 秒驱动式 CPU 测量高于 .NET 8 基线；正常托盘的 15 秒低频观察较低，但不足以排除主动运行时/调度回归。必须做更长、低干扰复核。本轮只采用 .NET 10 预发布候选，不据此宣布 Stable。安装器尚未配置商业 Authenticode 签名；Steam 商用授权和发行证书也未在本轮验收。

Steam 前必须完成真实 Windows 10 决策与硬件矩阵、24/72 小时运行、完整旧版本/便携升级与卸载矩阵、运行中安装/电源中断和资源不可访问恢复、游戏/远程会话，以及商用素材授权逐项复核。原始备份所引用的外部文件不能保证用户永不删除；此时保留日志/备份并停止恢复，不能保证自动修复。旧版 journal 没有 Intended 字段，继续沿用原恢复协议；新日志才能识别已知操作以外的修改。注册表与文件系统不能构成全局原子事务，外部程序仍可能在检查后并发写入。
