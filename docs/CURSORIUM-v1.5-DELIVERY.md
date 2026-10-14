# CURSORIUM v1.5.0 — Stability, Performance & Compatibility

本版为 **GitHub 预发布**，不标记 Steam Ready。继续保留 v1.4.0 Stable、原安装路径 D:\CURSORIUM、全部现有主题和独立签名资源库。未修改已发布 tag、资源 ZIP 或 Catalog。

## 基线与数据保护

起点为 `6886ea6f406894f7869977d4eaf476dcdcfa9641` / `cursorium-v1.4.0`。开发分支 `codex/stability-v1.5`；独立迁移工作树分支 `codex/net10-migration-v1.5` 保留只迁移 TFM 的验证点 `eba8b92`。真实安装 .NET 8.0.24、生产进程、安装 ZIP、全部数据副本、SHA256 和原指针注册表先保存到忽略的 `artifacts/v1.5-baseline`；原 tag 的源码 ZIP 也保留在此。

全程区分程序目录、真实用户数据、隔离测试数据和临时缓存。`verify-stability-protection.ps1` 对最初快照核查永久文件及 7 个关键 JSON 的存在性/哈希，按完整注册表导出比较原生值和类型；原始机器路径和备份不入库。日志、setup-result、可更新的签名缓存、可清理的世代临时文件单列，不把永久资源变化伪装成缓存变化。当前保护核对为 **102 个原有永久文件、0 个变化，原生值/类型一致**。

## 实际实现与原因

1. **.NET 10 LTS**：目标 net10.0-windows，自包含 x64 明确使用 10.0.13。WPF、P/Invoke、Raw Input、ANI、WindowChrome、事务和安装器继续复用。没有新增 NuGet 包。保留 .NET 8 的 tag、源码、构建、180 项核心及短时测量基线。
2. **静止采样降频**：Raw Input 仍立即采样实际运动，静止后 fallback 由活跃间隔退至 80 ms；禁用 250 ms、暂停低频检查。显示重建先解除旧渲染订阅；会话变化清空并重检，RawMouse.Dispose 可重复调用。
3. **即时关闭**：降低轮询频率后旧 60 ms 关闭断言暴露延迟。新增设置活动事件，禁用直接清空/隐藏并解除渲染，重新启用恢复活跃轮询；没有放宽旧测试。
4. **减少分配**：Settings.Clone 从 JSON 往返改为显式 CopyFrom，覆盖 schema 和所有参数；画笔器复用坐标/笔/刷委托，无点击事件时跳过点击枚举。保留粒子/历史/点击数量与原动画时序。
5. **事务恢复**：新增可选 Intended 与 OwnershipBefore，原 schema 1 仍可读。真实崩溃后仅自动恢复已知 Before/Intended 范围；检测外部修改则保留现场/日志/备份。新增 theme-ownership.json；正常恢复拒绝覆盖应用外的指针更改。旧版 journal 继续原协议，边界明确保留。
6. **原生资源**：IsActive 不再复制/安装或创建验证句柄。Apply 继续校验 CUR/ANI，既有稳定文件必须与来源 SHA256 相同；非法 ID、目标重解析点、恢复所需原文件丢失时拒绝写入。稳定版本、原始备份、Profile pin 不做自动清理。
7. **配置并发保护**：Profile 和 studio 写入前比较读取版本，拒绝覆盖外部变更/较新 schema；失败仍通过原 ThemeService 事务回滚。诊断根拒绝真实用户数据和链接路径。
8. **安装器**：修复 .NET 10 ZIP 目录项以反斜杠结尾时被当文件的问题；程序树的 junction/reparse 防护覆盖升级与卸载；有标记的 `.previous` 可在主目录缺失时恢复。自卸载助手再次核对链接后才删目录。恢复失败继续阻止危险删除。
9. **构建与交付**：清空经过路径核对的 artifacts 负载输出，防止旧 .NET DLL 混入；生成逐文件相对路径、大小、SHA256 的 distribution-manifest。发布脚本新增 -Prerelease，不抢占 Stable latest。
10. **诊断**：可配置 5 秒至 72 小时，CSV 全采样、JSON 最近 720 条，WPF 显隐、Profile 存取、CUR/ANI 加载释放、Overlay 重建和签名缓存重复读取。记录 CPU/内存/GDI/USER/线程/分配/CPU 绘制耗时；日志删除本地路径与 credential-shaped 内容，不默认上传。

## 测试结果与失败修复

最终深目录运行额外暴露 USER32 的 MAX_PATH 边界：260 字符原文件及其 extended-prefix 形式均返回 Win32 error 2，而短路径相同文件能加载。新增 8 项实际检查，长 CUR/ANI 来源使用有大小上限、立即清理的临时副本完成原生校验；WPF 使用 Stream 构造长路径预览。**永久 Windows 指针路径仍必须小于 260 字符**，超限在创建稳定目录/写注册表前拒绝，不把临时文件交给系统。原核心测试的数据根改为独立短隔离根，报告路径保持原名；长路径成功与安全拒绝均有独立断言，不靠缩短名称隐藏边界。

已执行 Release 源码构建，**0 error / 0 warning**。WFO0003 只在两个使用既有 PerMonitorV2 manifest 的混合 WPF/WinForms 宿主排除；原因见[兼容性记录](CURSORIUM-v1.5-COMPATIBILITY.md)。

|检查|本轮结果|
|---|---:|
|原核心/原生事务|180 通过|
|新增中断/并发/外部修改/数据保护/日志|42 通过|
|FROST 原生资源|388 通过|
|Obsidian 原生资源|388 通过|
|真实桌面/Raw Input/点击穿透/全屏探针/托盘|85 通过|
|界面/原快捷键/草稿|98 通过|
|同数据缓存 WPF 重启|99 通过|
|Cute 集合|28 通过|
|在线下载与 ZIP 安全|51 通过|
|策展与不可变更新/版本绑定|104 通过|
|真实网络与 WPF 下载/预览/Apply/Cancel|103 通过|
|隔离安装生命周期|16 通过，包含安装版自卸载助手实际删除|

没有删除旧断言。第一轮 .NET 10 构建的 DPI 检查、首轮安装反斜杠目录失败、静止重采样抵消降频、禁用延迟失败均留存本机日志，并在修复后重新执行受影响检查，不将失败计为通过。新增恢复检查实际子进程在第 4 次角色写入后 exit 77，覆盖 abandoned mutex、恢复重试和跨进程超时；不是只序列化一个假的成功结果。

180 秒诊断实际完成 **156 轮**，包含生产托盘并存，不能作为独占 CPU 基准。真实硬件为 Windows 11 build 26100、一个 2560×1440 / 96 DPI 显示器。公开脱敏报告见 `docs/validation/v1.5`；逐套命令、开始/结束时间和结果见 execution.json。

## 性能与迁移决策

Settings 两万次暖机复制：旧 JSON 算法约 22.56 MB，新复制约 2.56 MB，减少约 88.7% 分配；一次实测 126.20 ms → 0.91 ms。这个同进程微基准不等于整机 CPU 或启动速度提升。

原安装 .NET 8.0.24 和新自包含 .NET 10.0.13 以相同 8 模式脚本测量，期间退出生产实例，结束恢复托盘；新版另加快速长轨迹、高粒子、真实受控点击 Halo/Ring/Off、效果切换和暂停，共 15 模式。8 模式公平对照不创建点击窗口；15 模式的受控点击窗口使条件不同，结果单独保存。最终静止采样 **63.90 → 10.48 次/秒**；活动采样约 63 次/秒，禁用/暂停无绘制。Comet/Minimal/Neon/Stardust 的 CPU 绘制均值约 0.0247/0.0032/0.0089/0.0065 ms。绘制耗时不是 GPU 帧时间，invalidations 不是显示 FPS。

8 ms 测量驱动本身使 CPU 数值明显波动，不能据此宣称所有效果的整体 CPU 都下降。以下是实际单次约 4 秒测量的**单核 CPU 百分比**，包含驱动开销；主动模式存在升高，尚未证明运行时/调度无回归。

|模式|.NET 8 基线|.NET 10 新版|
|---|---:|---:|
|Disabled|2.72|4.28|
|Idle（含 8 ms 驱动）|0.39|5.46|
|Comet 60 target|10.15|16.38|
|Minimal 60 target|5.47|7.42|
|Neon 60 target|6.64|4.68|
|Stardust 60 target|4.68|5.07|
|120 target|3.12|7.02|
|Power 30 target|1.56|3.12|

另用外部低频观察正常托盘空闲，使用已备份的同一真实配置、每版本启动后等待 5 秒再测 15 秒，不执行 Apply、不驱动鼠标；两次均未观察到鼠标移动。实测单核 **1.128% → 0.720%**（28 逻辑处理器折算整机 0.0403% → 0.0257%），末次 Working Set **171.6 → 172.4 MiB**，GDI **227 → 227**、USER **88 → 88**。这仅是短时观测；不能将相对百分比推广到长期或所有机器。完整原始数值见 [idle-comparison.json](validation/v1.5/idle-comparison.json)。进程/state-probe 启动时间也单独报告，不冒充首次绘制时间。发布大小包含运行时补丁与文档差异，不等价于算法大小。

已测 WPF/Win32 功能回归通过；在预发布中采用 .NET 10，保留 .NET 8 回退版本。未测 Windows 10 与硬件矩阵并不会因迁移成功而自动通过。[微软运行时政策](https://dotnet.microsoft.com/en-us/platform/support/policy)和[Windows 支持表](https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions)是平台依据，不代替本软件实测。

## 修改文件与工具入口

迁移决策限于**预发布候选**：功能回归通过，但上述主动 CPU 升高还需专用机、更长时段与低干扰驱动复核；这是阻止本版标记 Stable / Steam Ready 的性能验收项。.NET 8 的 v1.4.0 Stable 仍作为可运行回退。

- Core：Native.cs、ThemeCatalog.cs 的长路径加载辅助、Settings.cs、Storage.cs、ThemeService.cs、StudioService.cs、StudioModels.cs、Online/RemoteCatalog.cs。
- App：App.cs、TrailEngine.cs、TrailPainter.cs、RawMouse.cs、LivePreview.cs、MainWindow.xaml.cs、新 StabilityVerification.cs、原 DesktopVerification.cs 的扩展基准、DiscoverView.xaml.cs 的长路径预览调用。
- 框架/安装：Directory.Build.props、两宿主 csproj、installer/Frost.Setup/Program.cs。
- 工具：build-release、publish-github、verify-cursorium 和原资源发布路径适配；新增 save-stability-baseline、verify-stability-protection、verify-stability、verify-v1.5、verify-installation-lifecycle、measure-engine、measure-idle。
- 自动化：tests/Frost.Verify/StabilityVerification.cs 及 Program.cs 新入口。主 UI 的 XAML、Token、控件、ThemeCatalog/安全 ZIP、在线签名协议、四种轨迹算法及原素材不重写。
- 文档：README、VALIDATION、架构、本报告、兼容性矩阵、诊断操作说明、发布说明与脱敏证据。

## 发布、当前安装与限制

真实安装候选已在保护流程下完成升级：D:\CURSORIUM 的版本 1.5.0，104 个原有数据/资源文件哈希不变，880 个候选负载文件匹配，桌面/开始菜单均打开同一新进程，原生指针值/类型不变。升级前用户数据隔离副本通过 99 项缓存 WPF 检查；另以很深的 Unicode 数据根运行真实自包含 WPF，98 项界面检查通过。报告见 [current-installation-upgrade.json](validation/v1.5/current-installation-upgrade.json)；回退 ZIP、原始注册表和数据副本仍只在本机。正式最终构建可能仅增加文档文件，最终负载数量/提交由本机终验记录及 Release 校验回执确定。

同一测量阶段的五次进程/state-probe 中位数为 .NET 8 **158.18 ms**、.NET 10 **189.33 ms**。这项目录/状态就绪对照是变慢的，不能宣传启动全面加速，更不能充当首次绘制耗时。原始回执见 [startup-net8.json](validation/v1.5/startup-net8.json) 与 [startup-net10.json](validation/v1.5/startup-net10.json)。

目标 [v1.5.0 预发布](https://github.com/catvir4493/cursorium/releases/tag/cursorium-v1.5.0)，安装器、便携版、主题包、源码和 SHA256 继续由现有脚本构建。只有通过隔离升级并再次备份/核对后，才升级 D:\CURSORIUM。最终实际提交、安装与公开下载校验以交付终验记录为准；尚未完成的步骤不能凭这个目标链接当作成功。

本轮未完成真实 Windows 10、双屏、混合 DPI、物理 DPI/刷新率切换、热插拔、锁屏/用户切换、睡眠、电源/RDP/Explorer 重启、独占游戏、24/72 小时和完整历史升级/断电矩阵。已测跨进程全屏探针不代表所有游戏。诊断中的软件 Overlay 重建不代表真实热插拔。遗留品牌/目录/IPC 兼容身份留到 v1.6；Steamworks/Workshop 尚未接入。

Steam 前的实质阻断、恢复边界与逐环境测试步骤分别见[兼容性矩阵](CURSORIUM-v1.5-COMPATIBILITY.md)及[诊断说明](CURSORIUM-v1.5-DIAGNOSTICS.md)。资源原文件遭外部删除/拒绝访问时不能保证自动修复，本版会保留证据并停止不安全恢复；注册表和文件无法对不合作的外部写入提供全局原子性。
