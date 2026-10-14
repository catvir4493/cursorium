# v1.5 可复现诊断与验证

所有命令在工程根目录执行，输出放忽略的 artifacts。测试私有副本、原始注册表和回退安装包不得上传。

```powershell
# 首次只读保护：复制正式安装和用户数据，逐文件验证副本，再导出原生注册表
scripts/save-stability-baseline.ps1 -OutputDirectory artifacts/my-baseline
dotnet build Frost.sln -c Release
scripts/build-release.ps1 -Version 1.5.0
scripts/verify-v1.5.ps1
scripts/verify-installation-lifecycle.ps1 -Version 1.5.0
scripts/verify-release.ps1 -IsolatedUpgrade -CrossVolume -Version 1.5.0

# 输入驱动基准与真实正常托盘空闲分开；-Exclusive 会退出并恢复原安装进程
scripts/measure-engine.ps1 -ApplicationDirectory D:\CURSORIUM -Exclusive -ReportDirectory artifacts/engine-baseline
scripts/measure-engine.ps1 -Extended -Exclusive -ReportDirectory artifacts/engine-extended
# 必须先有默认位置的保护快照；短时正常启动候选，结束核对永久数据与原生状态
scripts/measure-idle.ps1

# 短时：实际 WPF 显隐、Profile 持久化、CUR/ANI 加载释放、Overlay 重建
scripts/verify-stability.ps1 -DurationSeconds 120 -StartupCycles 5
# 不会自动改 Windows 指针，也不发送鼠标点击；运动是明确的本地预览夹具
scripts/verify-stability.ps1 -DurationSeconds 86400 -StartupCycles 50 -ReportDirectory artifacts/stress-24h
scripts/verify-stability.ps1 -DurationSeconds 259200 -StartupCycles 100 -ReportDirectory artifacts/stress-72h
```

长测期间关闭其他测试进程；生产托盘并存时记录这个条件，不宣称独占能耗。可取消测试进程；测试数据与用户数据分离，Windows 指针未由该诊断 Apply。主动真实原生主题切换循环由核心/桌面回归承担，会短暂改变系统，执行前须保存快照，结束必须核对恢复结果。

诊断 `samples.csv` 保存全部采样，JSON 最多保留最近 720 条。记录 CPU 单核百分比、Working Set、Private Bytes、GDI/USER、线程、总分配、invalidations、绘制次数及平均 CPU 绘制耗时。CPU 不是整机百分比，绘制耗时不是 GPU 呈现时间，invalidations 不是屏幕 FPS。总分配包含测试夹具、Profile 序列化、WPF 和网络缓存读取，不是引擎单独分配。

启动循环的 `--state-probe` 测量进程启动和目录/状态读取完成，**不是首帧显示时间**。实际窗口输入回归仍由原 --self-test / --ui-review / --discover-review 执行，包括真实 SendInput、原生点击穿透、Ctrl+Enter/Escape/F6、ANI、Apply/Cancel 和托盘。

`tests/Frost.Verify --stability-tests <app-directory> <output>` 实际启动子进程，在第四次注册表写入时以 exit 77 终止，验证 durable journal、abandoned mutex、外部修改拒绝、重试、跨进程互斥、被替换的有效 CUR，以及较新配置不会被陈旧实例覆盖。不是仅 mock 文件的恢复测试。

安装生命周期工具使用临时 Program/Data/快捷方式/独立卸载注册键。覆盖全新、同版本重复、重命名阶段中断恢复、程序目录 junction 拒绝删除、恢复错误阻止卸载，以及成功卸载保留导入文件。它没有卸载真实 D:\CURSORIUM。软件重命名模拟不等于真实掉电。

## 24/72 小时验收步骤

`verify-cursorium.ps1` 另外自动执行 --tray-lifecycle-review：首次没有窗口，分进程 IPC 打开、编辑草稿后关闭/重开、签名缓存首次显示及 --quit-without-panel 的真实托盘退出。11/12/5 项断言不修改 Windows 指针。正常 --tray 在首次打开前节省 UI 资源；已经打开后的草稿保留意味着不会立即销毁整个 UI。

1. 在专用 Windows 测试机保存安装、数据、原生值/类型与资源哈希；记录 OS、CPU、显示器/DPI、刷新率和进程。
2. 执行对应长测，保存 CSV/JSON；至少每小时观察资源趋势，不只比较首尾。
3. 另行执行真实输入、跨屏、全屏暂停、锁屏、睡眠、RDP、Explorer 重启场景；记录物理操作和时间。当前短测不替代这些操作。
4. 重启后读取签名缓存，验证已有主题/Profile、事务恢复和离线能力，比较永久数据与原生快照；正常缓存更新单独分类。
5. 记录失败、日志、崩溃转储的私有位置；公开报告只放脱敏数值和检查结果，不包含私人路径、配置或密钥。

没有默认上传。诊断输出不采集 Steam 凭据、签名私钥或完整用户配置。新 Storage.Log 对本地绝对路径、Bearer/token/password/secret 形式做脱敏；旧版已有日志和工具控制台仍须只留本地，公开前审查。
