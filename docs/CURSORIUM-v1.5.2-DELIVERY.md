# CURSORIUM v1.5.2 Preview — 性能验证与发布加固

交付日期：2026-10-15（Europe/Budapest）。这是部分验证的 Preview，不是 Stable 或 Steam Ready。用户明确暂停自动输入测试后没有继续驱动鼠标/键盘，也没有升级当前生产安装。性能验收仍未完成，允许后续在独立分支开发 v1.6，但不能把本版本作为已通过全部性能、兼容性和系统恢复验收的商业发行基线。

## 结论与真实基线

- 原正式安装实际已是 **v1.5.1 / 8da97e5**，保留在 `D:\CURSORIUM`，不是任务描述中的旧 v1.5.0。独立开发分支 `codex/performance-v1.5.2`，独立工作树与全新测试数据；生产安装、用户目录逐文件哈希和原生注册表快照保存在原工程忽略的 `artifacts/v1.5.2-baseline/`，未提交。
- 对照 v1.4.0 / `6886ea6` / .NET 8.0.24、v1.5.0 / `c59bd26` / .NET 10.0.13、v1.5.1 / `8da97e5` / .NET 10.0.13，以及冻结的 v1.5.2 候选。旧版本可回退，旧 tag/Release 不移动。
- v1.5.2 保持 .NET 10.0.13。没有因缺少根因证据而改动生产采样、渲染算法、视觉质量或放宽旧断言。ThemeService、StudioService、Profile、ANI、下载/事务和用户数据 schema 保留 v1.5.1 实现。
- **没有证实统一的主动 CPU 回归根因，也没有证实普遍 CPU 改善。** 当前可确认的是旧测试混合输入驱动开销、观察条件不完整，以及诊断/发布工具的具体缺陷。原生 CPU 栈、完整自然输入和缺失对照轮次仍是阻断。

## 性能数据与解释

真实环境：Windows 11 x64 build 26100，i7-14700KF / 28 逻辑处理器，RTX 5070，**单屏 2560×1440、96 DPI、约 180Hz**。交互式工作站存在后台负载；不能当作独占实验室。SDK 10.0.103；测量使用各版本准确的自包含运行时，不使用机器上较早的框架运行时冒充。

矩阵计划为 12 模式 × 4 版本 × 3 轮；每轮 5 秒预热、20 秒稳定，轮换版本顺序、独立进程。原主矩阵完成 144 轮，补测完成 9 轮后因 Escape 中止，随后用户选择暂停。总 **153 次原始完整记录、118 次接受、35 次按输入条件排除，21 个模式/版本组不足三轮**。严格 `--require-complete` 实际拒绝该矩阵；未把取消、未提交的目录算作通过。缺轮计划只按输入条件生成，没有按 CPU 高低筛选。

|模式|v1.4.0 单核 CPU 中位数 / P95|v1.5.0|v1.5.1|冻结 v1.5.2|有效轮次（依次）|
|---|---:|---:|---:|---:|---|
|Comet|6.22 / 14.08%|7.78 / 15.65%|6.99 / 10.97%|7.78 / 20.19%|2 / 2 / 2 / 2|
|Minimal|6.22 / 9.50%|6.22 / 11.20%|6.21 / 14.25%|3.89 / 12.43%|1 / 2 / 1 / 1|
|Neon|6.99 / 14.01%|6.21 / 10.97%|4.67 / 14.23%|6.22 / 13.95%|2 / 2 / 2 / 2|
|Stardust|5.44 / 12.50%|4.67 / 12.59%|6.21 / 10.98%|6.22 / 12.52%|3 / 2 / 3 / 3|
|120 Target|9.33 / 14.07%|6.99 / 13.98%|7.77 / 14.00%|6.22 / 14.09%|3 / 3 / 3 / 3|
|Power 30|4.67 / 18.63%|4.66 / 13.99%|4.66 / 10.96%|4.67 / 15.55%|3 / 3 / 3 / 3|
|高频移动/点击|7.77 / 17.09%|10.10 / 17.10%|8.56 / 18.73%|7.78 / 15.64%|3 / 3 / 3 / 3|

这些是稳定阶段**秒级 CPU 观测**，不是逐帧或 GPU 耗时；整机占用约为单核数值除以 28。不能据此宣称 Minimal 提升 37% 或 v1.5.2 比相同生产实现的 v1.5.1 更快。全部模式、整机 CPU、机器忙碌度、内存、GDI/USER、线程、GC、分配速率、采样与绘制计数、每轮均值、驱动和观察器校准，见[实际观测表](validation/v1.5.2/performance-table.md)和[完整脱敏统计](validation/v1.5.2/performance-partial.json)。

已确认的测量干扰：

1. 旧四秒基准在目标进程内以 8ms 驱动输入，混合驱动、定时调度、目标绘制开销，且预热短。本轮把同一个 .NET 10 驱动放到独立进程，分别计 CPU，并以空宿主/8ms 计时循环校准。不能将旧总数直接解释为引擎回归。
2. 意图空闲的记录出现指针位置变化；早期工具未记录位置 API 成功标志，不能倒推它是用户输入还是查询失败。主矩阵之前用户配合前的 21 轮不用于验收；其后仍按实际条件筛除，不删除原记录。
3. 部分活动轮次驱动报告尝试移动但自己的目标窗口未收到移动。不能据此认定相同负载；本轮按实际到达事件筛选。未来驱动增加目标窗口位置、前台/命中检查，点击仅允许自己的前台目标，关闭窗口/Escape 停止并等待工作线程退出后恢复鼠标。这些新输入保护编译通过，**未在暂停后执行自动输入验证**。
4. 同实现候选也有波动，不能把版本号之间的单次差别当作优化效果。

自然输入单独记录：v1.4 一次完成，约 26 秒、6678 移动、18 点击、零合成事件；v1.5.0 一次因 Escape 中止（8077 移动、36 点击），不算有效对照；其余未完成。自然输入对照 **Pending**。

Profiling 实际完成 5 次独立 EventPipe trace：三个版本 Comet、v1.5.1 Idle 与空 8ms 循环。采样的是线程**墙钟**栈，不是 CPU 栈；等待栈占比不能被当成 CPU 热点。测量阶段没有 GC，不能归咎于 GC；也没有足够证据归因于 JIT、Raw Input、80ms fallback、WPF native render 或某个 Dispatcher 方法。WPR 内核 CPU 跟踪实际失败 `0xc5585011`；原生线程热点尚未获得可靠符号归因。新 no-paint/no-render 控制仅供后续定位，未执行，更不是通过降低画质获得的优化。

## 启动性能

原始正式 EXE 的 state-probe 每版本 15 次交替、共 60 次真实测量完成：

|版本|整个状态探针进程寿命中位数|范围|
|---|---:|---:|
|v1.4.0|200.37ms|176.93–350.73ms|
|v1.5.0|232.49ms|211.33–547.17ms|
|v1.5.1|226.93ms|208.84–382.24ms|
|冻结 v1.5.2|235.23ms|211.93–992.67ms|

这是暖文件系统条件下的进程启动、读取、报告写入和退出总时间，非首次 UI、非冷启动；初轮没有明确排除专门的预热轮。历史 158.18/189.33ms 不能与本协议直接混比，约 30ms 信号仍值得后续定位，不能凭这些数值认定是状态读取构造器变慢。

公共宿主早期 Stopwatch 因 beforefieldinit 延后初始化，`mainAssemblyLoadedMs≈0.015` 不能当作 managed Main 绝对时间；NotifyIcon ID 实际为 UInt32，早期 int-only 判断漏掉 Shell 查询。新工具修正显式 Main 时钟与 uint ID，分开原 EXE Process.Start、状态构造、托盘注册、ContentRendered、Input 调度，新增明确预热轮。**新启动协议未复测**，不回填早期数据；首次开窗的相对时间仍保留原始记录，不称作精确屏幕呈现时间。见[原始脱敏探针记录](validation/v1.5.2/startup-original-probes.json)。

## 修改与修复

- `tools/Frost.PerformanceHost/`：双运行时反射宿主、独立输入进程、进程/线程资源观测、诊断控制、资源与托盘里程碑、managed 实现核对。
- `scripts/prepare-performance.ps1`：公开归档/校验清单绑定，冻结 App/Core/host 哈希，拒绝覆写；不从已升级安装冒充历史版本。
- `scripts/measure-performance.ps1`：轮换顺序、预热/稳定、固定驱动、可恢复记录、缺轮计划、实际输入到达检查；修正 PowerShell 空备份参数传入 File.Replace 变成空字符串的问题，改为 NullString。
- `scripts/summarize-performance.py`、`tests/test_performance_summary.py`：17 项协议测试；拒绝 NaN/Infinity/溢出、失败/缺样本/重复证据/路径逃逸、无驱动的活动负载；保留缺失计数为 null，明确排除原因与不足轮次。
- `scripts/measure-startup.ps1`、`scripts/profile-performance.ps1`：分离测量含义、固定驱动和失败证据；不能用采样墙钟冒充 native CPU。
- `scripts/performance-storage.ps1`：输出限制到忽略的 artifacts，拒绝正式安装、用户数据、上级目录逃逸与重解析点。工具不默认上传遥测。
- `scripts/verify-v1.5.ps1`、`scripts/verify-cursorium.ps1`：可选准确 10.0.13 自包含验证器，核对部署 Core SHA256；旧测试保留。
- `scripts/verify-stability.ps1`：用真实签名 fixture 初始化隔离缓存；新长压路径未实际执行。
- `scripts/package-source.ps1`：仅打包当前提交的 allowlist Git 对象，排除未跟踪私人文件，避免工作树 CRLF 差异；要求已提交的源码。
- `tools/Frost.CursorGen/Program.cs`：实际打包时发现 Windows JSON 换行使四个生成清单显示脏工作树、阻断源码身份闸门。生成工具将两个 JSON 固定为仓库要求的 LF；光标/ANI/PNG 字节与生产 Core 不变。
- `scripts/verify-release-assets.ps1`、`scripts/verify-release-hardening.ps1`、`scripts/publish-github.ps1`：发布前核对版本/提交、准确运行时、portable 清单、规范源对象、四个资产 SHA256；错误版本/运行时/过期源码负例、精确还原；Preview 缺少 -Prerelease 时拒绝远端修改。
- App/installer 版本、导航版本标签、HTTP User-Agent 和构建/发布默认版本更新为 1.5.2；设计系统、主题资源和生产算法不变。

## 实际验证及未完成项

`dotnet build Frost.sln -c Release --no-incremental`：0 error、0 warning。双运行时 PerformanceHost 实际 publish 成功。WPF/WinForms 混合宿主保留 PerMonitorV2 manifest，继续仅显式排除 **WFO0003、WFAC010**，没有关闭其他警告。PowerShell 脚本全量语法解析通过。17 项 Python 协议测试通过；完整性能闸门对不足矩阵按预期拒绝。

以准确 10.0.13 自包含 Frost.Verify 加载冻结候选的原 Core 字节：

|检查|本轮结果|边界|
|---|---:|---|
|核心、Profile、数据/资源安全|162 通过|未加 --native-roundtrip，不修改真实指针|
|在线签名/URL/取消/ZIP 等安全|51 通过|受控 handler，不冒充全为公网|
|合集/不可变版本/更新失败保持旧版|104 通过|真实包与签名 fixture；初次缺旧签名文件失败，补回原公开 fixture 后重跑通过|
|Simple Dot or Star 公网下载/加密目录|14 通过|实际官方网络、独立 LocalAppData，下载不应用|
|FROST 原生文件|388 通过|Windows LoadImage/ANI 校验，不是系统应用|
|Obsidian 原生文件|388 通过|同上|
|managed 实现比较器正/负例|通过|同文件接受，v1.4 与候选 Core 差异拒绝；不证明 native JIT 相同|
|完整桌面/WPF、托盘输入、原生事务中断恢复|Pending|用户暂停自动输入；保留全部原断言，不用历史通过冒充本轮|
|新版输入保护、no-paint/no-render 归因|Pending|仅编译，不自动恢复输入|
|24h / 72h 长压|Pending|没有运行对应时长|

真实数据复核：201 个受保护永久文件哈希未变，7 项关键 JSON 的存在性/哈希一致，注册表所有原生指针值及类型一致。`theme-transaction.json`、`interface.json` 在基线不存在，仍不存在；没有为补齐列表创建文件。用户设置和注册表具体内容未公开。[脱敏保护报告](validation/v1.5.2/user-protection.json)、[执行命令与时间](validation/v1.5.2/passive-regression.json)。

隔离安装和最终发行字节验证的完成结果在下方发布附记记录；没有对 `D:\CURSORIUM` 执行卸载或覆盖。

## 兼容性与 v1.6 决策

本轮真实覆盖仅 Windows 11 当前单屏/100% 环境。双屏、Windows 10、物理 125/150%/混合 DPI、不同刷新率组合、热插拔、锁屏/睡眠、会话切换、RDP、Explorer 重启、独占全屏游戏均不新增通过声明；[原矩阵](CURSORIUM-v1.5-COMPATIBILITY.md)继续适用。20 秒/多轮观测不证明长时无泄漏，GDI/USER 变化和 GC/内存样本只能说明实际窗口，不能外推 24/72h。

v1.6 可以在独立分支开展设计和兼容迁移；**不得将 v1.5.2 提升 Stable，或把进入 v1.6 当作性能验收通过**。下一轮须恢复足量交替/自然输入、验证新驱动、取得 native CPU 热点证据，再考虑有针对性的优化；同时补全完整 WPF/原生安全回归、真实硬件和长期测试。Steam 证书/商业素材授权/历史安装生命周期等既有阻断仍未关闭。

## 证据、构建与发布

- 公开脱敏证据：`docs/validation/v1.5.2/`；[协议](CURSORIUM-v1.5.2-PROTOCOL.md)、[可执行复现命令](CURSORIUM-v1.5.2-DIAGNOSTICS.md)。说明中的命令不表示全部已执行。
- 原始 CSV、execution、trace 和取消目录：独立工作树 `artifacts/performance/alternating-final/`、`supplement/`、`natural/`、`startup/`、`profiles/`；本地保留，不打包私人路径和 trace。
- 无输入验证：`artifacts/v1.5.2-passive/`；生产保护：`artifacts/v1.5.2-protection-paused/`；正式安装和注册表回退资料仍在原工程的 `artifacts/v1.5.2-baseline/`。
- 新发行文件按现有脚本生成 `artifacts/CURSORIUM-Setup-1.5.2-Release.exe`、`CURSORIUM-1.5.2-win-x64.zip`、`CURSORIUM-Themes-1.5.2.zip`、`CURSORIUM-Source-1.5.2.zip`、`CURSORIUM-SHA256SUMS.txt`。
- 正式发布入口：[cursorium-v1.5.2](https://github.com/catvir4493/cursorium/releases/tag/cursorium-v1.5.2)，明确 Prerelease，v1.4.0 保留 Latest Stable。实际提交以 tag 指向和发布回执为准；只有公开下载重新核对字节后才报告发布完成。

### 发布附记

构建、隔离安装与发行字节闸门尚待本轮后续执行；此行会在实际完成后更新。生产安装保持 v1.5.1，桌面快捷方式继续指向原位置。
