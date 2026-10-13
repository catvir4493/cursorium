# CURSORIUM v1.1.0 — Interface Foundation & UX Refinement

日期：2026-10-13（Europe/Budapest）。保持 .NET 8 / WPF / Win32，未增加框架或包依赖。

## 已交付

- 三栏工作区重新分配空间，导航统一为矢量图标。小于 1000 DIP 时收为图标导航，带工具提示与可访问名称。
- 标题栏使用 WindowChrome：保留原生 caption/resize 命中区域、系统最大化/还原/最小化；关闭仍驻留托盘。Settings 提供原生系统深色标题栏回退。
- Live Preview 是实际的局部输入测试区，使用原来的独立模型和共享 painter。小图标仅作工具栏角色标识，替代原来的巨大静态指针。主题库按内容流紧跟预览，卡片自动换行。
- 右侧摘要显示指针、拖尾/点击与 Saved Profile。Trail / Cursor / Click 使用真实可滚动配置视图；主要参数优先，消散时间/粒子密度/点击时间与强度放入 Advanced settings。
- 自定义滑块、开关、色块、下拉框、按钮、卡片、导航、滚动条与焦点状态统一为设计 Token。没有全屏模糊、霓虹装饰或背景动画。
- Apply Changes / Cancel Changes 固定在右下方：无变化时禁用，草稿变化时启用，颜色错误时只有 Cancel 可用。取消重新载入已应用状态及对应 Profile ID，修复旧版刷新顺序导致的短暂状态偏差。
- 恢复原指针置于 Cursor → System recovery，保留真实备份状态判断与确认对话框；Profile 删除保留确认。
- 成功与错误以内联文本反馈。应用失败仍由原事务服务回滚，草稿保留。Saved Profile 与草稿内容不同时标记 modified。
- 卡片轻微位移 140 ms、内容淡入 180 ms，仅作用于 Transform/Opacity。Windows 禁用动画或 Settings 关闭动画时停止非必要动画；桌面特效生命周期不受其影响。
- 保留原有 CUR/ANI 导入、全部系统角色、Profile CRUD、托盘、登录启动及四种拖尾。Ctrl+Enter 应用，Escape 取消草稿；下拉框打开时 Escape 只关闭下拉框；F6 在工作区间切换焦点。

## 实施与保护

开始时 Git 工作区干净，基线为 `68dc329`，与远程 main 一致。回退分支：
`codex/rollback-cursorium-v1.0.0`；开发分支：`codex/interface-v1.1`。

A–E 每阶段完成后均构建 WPF 工程，最终整套 Release solution 构建通过，零警告/错误。
Phase A 除编译外运行了真实窗口截图探针，修复 ResourceDictionary 模板跨字典资源解析问题。
Phase F 运行原有核心/桌面回归、新增界面专项验证、八种短时性能场景和自包含发布。

`src/Frost.Core`、ThemeService/StudioService、TrailEngine、TrailPainter、OverlayWindow、
LivePreview 均无源代码差异。FROST 和 Obsidian 资源重新生成后与基线内容相同，无删除资源。
UI 修改沿用既有事件处理，通过小型 presentation state 统一反馈与按钮状态。

用户的 settings.json、studio.json、profiles.json、original-theme.json、interface.json
通过测试前后存在性与 SHA256 比较；全部保持一致。测试导入与 Profile 位于隔离数据目录，
原生主题回合恢复测试前配置。机器注册表快照、用户数据、回退安装归档不进入源码或 Release。

## 主要 UI 文件

| 文件 | 内容 |
|---|---|
| `src/Frost.App/MainWindow.xaml` | 标题栏、三栏导航、预览与内容流、主题卡片、设置 tabs、advanced、固定操作区 |
| `src/Frost.App/MainWindow.xaml.cs` | presentation projection、draft 操作、原服务连接、尺寸响应、窗口命令、键盘与 UI 偏好 |
| `src/Frost.App/Presentation/DesignTokens.xaml` | 颜色、字号、间距、圆角、键盘焦点 |
| `src/Frost.App/Presentation/Icons.xaml` | 统一矢量几何图标 |
| `src/Frost.App/Presentation/Controls.xaml` | NavigationItem / PageHeader / ThemeCard / PreviewPanel / SettingsSection / StyledSlider / StyledToggle / ColorSelector / PrimaryButton / SecondaryButton / StatusIndicator 等 |
| `src/Frost.App/Presentation/StudioViewState.cs` | INotifyPropertyChanged 的 UI 状态投影，不持有 native/render 服务 |
| `src/Frost.App/Presentation/UiPreferences.cs` | schemaVersion 1 的独立 interface.json，原配置/Profile 不迁移 |
| `src/Frost.App/Presentation/UiMotion.cs` | 尊重系统偏好的有限微交互，无额外渲染循环 |
| `src/Frost.App/Presentation/WindowAppearance.cs` | WindowChrome 和可失败回退的 DWM 深色属性 |

配套修改：App.cs 增加隔离 `--ui-review` 入口；InterfaceVerification.cs 为真实 WPF/native
输入、窗口和截图验证；DesktopVerification.cs 保留原检查并提前记录显示器元数据，补充共享
输入探针；脚本增加报告目录、UI 检查与可靠进程退出码读取。App/Setup 与发布默认版本为 1.1.0；
安装器版本显示从程序集读取，避免构建版本与注册版本不一致。

## 构建与测试证据

Windows 11 build 26100，SDK 10.0.103，目标 .NET 8；实际双屏：2560×1440 与 1920×1080，
两者均 96 DPI。本次不是混合 DPI 验证。

| 检查 | 结果 |
|---|---|
| Release solution / Windows x64 self-contained app + installer | 0 warnings / 0 errors |
| FROST / Obsidian 原生资源 | 各 388 checks |
| 核心、导入、Profile、实际原生主题切换、失败/中断回滚 | 66 checks passed |
| 原有桌面套件，实际鼠标/键盘、双屏覆盖层、四种渲染、Profile/导入/重启/释放 | 89 checks passed（双屏原有 88 项，加 1 项隐藏预览释放桌面排除区域检查） |
| 新增 UI、草稿/按钮、真实键盘、标题栏、动画保存、九组逻辑尺寸和五页可达性 | 98 checks passed |
| 用户数据保护 | 五个文件的存在性/SHA256 一致 |
| 短时性能 | 8 scenarios completed；GDI 33→33，各场景 USER 34–35，无持续递增 |

原始可复查报告：`artifacts/interface-v1.1/verified/` 中的 core-report.json、desktop-report.json、
restart-report.json、performance-report.json、user-data-protection.json 与控制台输出。
最终 UI 截图和 98 项检查：`artifacts/interface-v1.1/final-validated/interface/capture-report.json`。
脱敏可分享结果：[`verification/cursorium-v1.1-2026-10-13.json`](verification/cursorium-v1.1-2026-10-13.json)。

新增真实键盘检查曾因窗口未获得前台而失败；加入对测试窗口的实际点击并断言前台与键盘焦点后通过。
初次测试启动器报告的退出码异常，改用直接 ProcessStartInfo 等待/读取并复查实际退出 0。
失败过程保存在本地，不作为通过证据。截图探针等待最终页面布局/淡入完成，避免截到上一页。补测发现最大化后 caption 按钮超出工作区，已用实际物理工作区与当前 DPI 计算最大化内边距，并验证标题栏按钮完整可见。长主题/Profile 名称使用省略与工具提示，所有原名称仍完整保存。

短时 engine-only 测量：禁用时 0 invalidations/s、机器 CPU 0.33%，静止时 0 invalidations/s、
机器 CPU 0.13%（28 logical processors）；四个 60 FPS 目标场景约 53–55 WPF invalidations/s，
120 目标约 69，省电目标约 30。指标包括 WPF、双屏 layered surfaces 和可能的延迟初始化；
不能当作 GPU 展示 FPS、UI 全开内存或长期性能保证。

## 截图与布局

[`同尺寸对照与子页面`](CURSORIUM-v1.1-SCREENSHOTS.md) 提供 v1.0/v1.1 1280×830 原生窗口截图、
五个主要页面、三种配置视图、进阶设置、系统标题栏回退与紧凑窗口。全部来自运行中的 WPF 程序，
预览流光来自真实鼠标采样，未制作静态替代控件。

九组矩阵采用 `(screen width − 32) / scale`、`(screen height − 64) / scale` 的等效 DIP 工作区，
覆盖 1366×768、1920×1080、2560×1440 × 100%/125%/150%。各组五页 Apply/Cancel 均在窗口内，
预览与配置不重叠；另验证 850×520 DIP。它们验证布局退化与可达性，**不是物理 DPI 切换测试**。

## 未解决与未验证

- 尚未实测 Windows 10、真实 125%/150% 与混合 DPI、负坐标显示器、物理热插拔、睡眠/恢复。
- 安全/保护桌面、游戏独占全屏、真实高刷新率展示和长期资源泄漏未实际覆盖。已验证普通外部无边框全屏暂停。
- 小窗口将内容和配置分别滚动，导航收为图标；高级控件和导入/恢复等内容可能需要滚动，Apply/Cancel 保持可达。
- 本地按角色导入与目录主题库保持原范围，不枚举任意外部 Windows 命名方案或加载压缩主题包。
- 包仍未代码签名。UI 目前为英文；中文字体与国际化布局不是本次已完成的验收项目。
- 全新安装、卸载、升级失败回滚往返未在用户现有安装上执行；现有安装升级记录另行标记，不能推导这些测试通过。

详细模块关系继续见 [架构说明](CURSORIUM-ARCHITECTURE.md)，本版本及历史证据见 [VALIDATION](../VALIDATION.md)。


## 现有安装升级

使用 v1.1.0 安装器实际升级已有的标记安装（`--silent --no-launch`，退出 0），随后从开始菜单启动并由桌面快捷方式重复打开。
第一次升级逐一验证 **611 个发布文件 SHA256 匹配**，Windows 注册版本为 1.1.0，实际进程来自固定安装目录且标题为 CURSORIUM。
**58 个已有数据/资源文件**的哈希全部保持，原生指针注册表值和类型保持。新旧安装和数据的完整回退资料位于本地 artifacts，未上传。

重复打开保持同一进程。桌面 `CURSORIUM.lnk` 继续指向已升级的程序，不再唤起旧的 v1.0 UI。
本节及脱敏结果在升级后补入文档，最终包重打仅增加文档证据；软件源代码没有变化。
原始结果：`artifacts/interface-v1.1/installed-upgrade-report.json`；
可分享结果：[v1.1 installed upgrade](verification/cursorium-v1.1-installed-upgrade-2026-10-13.json)。

升级成功不等于全新安装、卸载或失败回滚往返经过验证。
