# v1.5.2 性能诊断的复现与证据边界

在独立工作树中执行；保护快照、用户数据副本、trace、安装回退 ZIP 都只保留本地 artifacts，不上传。生产安装不得用作可写的诊断目录。

本轮用户已明确暂停自动输入。以下输入基准、profiling、完整 WPF/原生回归和压力命令是将来恢复测试后的操作说明，不能在暂停期间自动继续；交付报告列明已执行的无输入子集。现有生产安装保留 v1.5.1。

## 准备冻结目标

保留三个正式 GitHub Release 的 win-x64 ZIP 及各自的 `CURSORIUM-SHA256SUMS.txt`，分别放入旧工程的 `artifacts/remote-v1.4.0/`、`remote-v1.5.0/`、`remote-v1.5.1/`。不要从可能已升级的生产安装提取旧版本。准备工具核对归档 SHA256 和可执行文件 ProductVersion，拒绝覆盖已经冻结的 targets。

```powershell
scripts/save-stability-baseline.ps1 -OutputDirectory artifacts/my-v1.5.2-baseline
# 候选先独立 publish，不能覆盖正在被测的程序目录
dotnet publish src/Frost.App -c Release -r win-x64 --self-contained true -p:Version=1.5.2 -o artifacts/candidate
scripts/prepare-performance.ps1 -PreviousCheckout '<retained-release-checkout>' -CandidateDirectory '<absolute-candidate-directory>' -OutputDirectory artifacts/performance-reproduction
scripts/measure-performance.ps1 -TargetsPath artifacts/performance-reproduction/targets.json -ReportDirectory artifacts/performance-reproduction/alternating -Rounds 3 -WarmupSeconds 5 -Seconds 20
```

测量工具会退出正常生产实例，独占正常 SID 的 CURSORIUM mutex，结束或异常时恢复原托盘进程。所有应用状态写入新建隔离目录；调用原生产 App.Start/TrailEngine，不调用 Apply。当前生产配置不会因为试验参数而保存。按 Escape 取消的输入轮次不会进入完成记录；用 `-Resume` 保留已完成记录并为不完整轮次创建新的 retry 目录。

运行时不要操作鼠标，不同时编译、profiling 或执行其他桌面验证。自动输入只有自己的目标窗口可接收点击，输入驱动进程与被测应用分别计费。程序性位移不是物理 Raw Input 验证；自然输入必须单独进行并记录实际到达的移动/点击。

```powershell
python -B scripts/summarize-performance.py artifacts/performance-reproduction/alternating --require-complete
# 若发生真实输入干扰：保留原记录，用明确的时间边界排除，再补缺失轮次
python -B scripts/summarize-performance.py artifacts/performance/alternating-final --additional-directory artifacts/performance/supplement --exclude-before 2026-10-14T20:38:47.9Z --require-complete
python -B -m unittest discover -s tests -p test_performance_summary.py
```

第二个汇总命令中的时间边界专属于本次实际试验，不能照抄为后续试验的有效时间。补测必须使用相同冻结目标、相同固定外部驱动、预热和稳定阶段。汇总拒绝失败、取消、证据重复、缺失样本、不同应用 SHA256 混组、路径逃逸及非有限 JSON 数值；`--require-complete` 不允许把缺少轮次的结果称作完整矩阵。

可将 `insufficientGroups` 转成 SchemaVersion 1 的补测计划：`entries` 中每项为 `mode`、`target`、`requiredRounds`（原 requestedRounds 减 acceptedRounds）。使用 `measure-performance.ps1 -RepairPlanPath <plan.json> -Rounds <最大所需轮数>`，只补输入条件无效或尚缺的记录。计划、来源汇总 SHA256 和实际执行记录保留，不按 CPU 高低挑轮次。参数 `-Resume` 保留原完成记录；写记录失败后的未提交目录会在新 retry 目录重测。

## 启动和 profiling

```powershell
scripts/measure-startup.ps1 -TargetsPath artifacts/performance-reproduction/targets.json -ReportDirectory artifacts/performance-reproduction/startup -Rounds 15
# 在稳态计时结束后执行，禁止与主矩阵重叠
dotnet tool install dotnet-trace --tool-path artifacts/diagnostic-tools --version 10.0.750501
scripts/profile-performance.ps1 -TargetsPath artifacts/performance-reproduction/targets.json -ReportDirectory artifacts/performance-reproduction/profiles
```

原可执行文件状态探针测量 Process.Start API 和整个探针进程寿命，不等于 UI 首帧。公共宿主另测状态/托盘启动返回、Shell 注册矩形可查询、ContentRendered、Input 优先级调度。修正宿主时钟初始化与 NotifyIcon 的 UInt32 ID 后的里程碑必须另做试验，不能回填早期数据。Shell 矩形可查询也不等于已经观察到图标像素，更不保证图标没有进入溢出区。

dotnet-trace 的 sampled-thread-time 是墙钟线程栈采样；等待线程可占据大比例。不要把其百分比写成进程 CPU 热点。当前权限下 WPR 内核 CPU 跟踪返回 0xc5585011，原始失败和墙钟 trace 留作私有证据；必要的 native CPU 栈仍是诊断缺口。

若最终发行提交只修改说明和版本元数据，使用 PerformanceHost 的 `--compare-implementation --left <measured.dll> --right <final.dll> --report <report.json>` 分别核对 App/Core：IL、方法签名、异常区、用户字符串和嵌入资源一致。它不证明 JIT/native code 完全相同，也不代替最终发行版回归。实现发生变化则必须重新测量相关模式。

## 发布闸门

```powershell
scripts/verify-v1.5.ps1 -SelfContainedVerifier
scripts/verify-installation-lifecycle.ps1 -Version 1.5.2
scripts/verify-release.ps1 -IsolatedUpgrade -CrossVolume -Version 1.5.2
scripts/verify-stability.ps1 -DurationSeconds 1200 -StartupCycles 10 -ReportDirectory artifacts/v1.5.2-stability
scripts/verify-release-assets.ps1 -Version 1.5.2
scripts/verify-release-hardening.ps1 -Version 1.5.2
scripts/publish-github.ps1 -Release -Prerelease -Version 1.5.2 -NotesPath docs/releases/1.5.2.md
```

这里是可执行操作说明，命令出现不表示已经执行通过；实际命令、时间和结果由交付记录给出。长压继续使用 v1.5 原工具的 86400/259200 秒参数。20 分钟诊断、约一小时矩阵、逻辑 DPI 模拟分别不能替代 24/72 小时、双屏、Windows 10 或物理混合 DPI 验收。v1.5.2 维持 Preview，v1.4 保留 Latest Stable。
