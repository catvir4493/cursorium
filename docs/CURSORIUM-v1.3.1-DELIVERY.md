# CURSORIUM v1.3.1 — Visual Experience Refinement

基于正式 v1.3.0 `64d161745d0b884eec5497ea4a94ed425d31e0be`，在 `codex/visual-v1.3.1` 开发。保留 C# / .NET 8 / WPF / Win32、原生 WindowChrome、五个导航页面和现有设计语言。本轮是已实现工作流的呈现改进，没有新增商城、账户、主题算法或平行加载服务。v1.3.0 tag / Release 不移动。

## 已实现的视觉与交互

| 模块 | 结果 |
|---|---|
| Explore | 完成：4:3 封面成为卡片主体，常规约四列、宽窗口最多五列、紧凑约两列；按实际滚动内容宽度计算。名称、作者、类型、原生覆盖和获取状态分层，收藏独立可操作 |
| 真实封面 | 完成：六个在线指针资源和四个拖尾预设均由实际资源生成。在线媒体路径更新，不重新发布或要求下载已拥有的包 |
| Theme Details | 完成：左封面/角色缩略图，右名称、作者、类别、简介、状态、主要操作和 Overview。Download / Open 位于首屏，技术信息默认折叠 |
| 真实预览 | 完成：保留临时下载、签名目录、包哈希、ZIP/native 校验和资源释放。Actual size / 2× / 4× 只改变观察 Image，不修改实际 WPF Cursor、系统配置或热点 |
| Cursors | 完成：中等整体预览面板、封面卡片、来源与 Selected / Applied / Modified、15 个可点击角色、原生/继承说明，技术资料移至角色之后 |
| Customize | 完成：System recovery 可展开，原确认机制保留；Apply / Cancel 继续固定在底部，并由原状态投影驱动 |
| Trails | 完成：复用同一真实绘制器生成封面；来源、选择和应用状态明确，预设参数与四种算法不改 |
| 状态反馈 | 完成：离开详情或隐藏在线页面清理预览提示；取消后的旧进度回调不再污染 Explore，仍在运行的真实下载继续显示进度 |
| 性能边界 | 保留 24 项分页、4 个媒体请求并发和静态卡片；增加最多 24 张/40 MiB 的共享解码缓存，解码宽度不超过原图且上限 720，磁盘继续 24 MiB |

视觉复用已有 Window / Surface / Card / Accent 等 Token。没有第二套全局配色；局部封面颜色只帮助辨认真实资源。Controls 增加 ResourceCardButton 与 DestructiveButton，继续保留 Hover、Pressed、Focus 和 Disabled 状态。卡片沿用 140 ms 位移，加载图片和详情沿用 180 ms 淡入，尊重 Windows ClientAreaAnimation 和界面动画偏好。卡片不播放 ANI。

### 封面生成与复现

新增 `Presentation/ThemeCoverRenderer.cs` 和 `scripts/generate-covers.ps1`。输入是已经安全解包和验证的原主题目录：`theme.json` 加真实 CUR/ANI，或经过原白名单验证的 `preset.json`。指针使用 ANI 原始序列的第一展示帧，封面只解码所需一帧，不为静态卡片加载整套动画；优先采用作者原生角色，避免把 FROST 回退图像作为作者主视觉。整数像素复制放大保持像素清晰。720×540 背景使用有限的渐变和几何图形；不包含外来插画或 AI 图片。

拖尾使用生产 TrailModel / TrailPainter 和实际 preset 参数，以固定快手势采样生成静态快照。观察图整体放大两倍，既不修改 preset 数值，也不改变真实运行算法。两次独立离线生成的 10 张 PNG SHA256 完全一致，结果在 [cover-checksums.json](validation/v1.3.1/cover-checksums.json)。

```powershell
dotnet build Frost.sln -c Release
./scripts/generate-covers.ps1 -InputDirectory <validated-pack-directories> -OutputDirectory <cover-output>
```

在线资源仓库 [cursorium-library](https://github.com/catvir4493/cursorium-library) 已提交媒体更新，远程提交 `8cee4b15643193e8dd5342a75a1b18f740f53dde`。签名目录 CatalogVersion 1.0.4 指向 `catalog/v1/previews/v1.3.1-r2/`，避免旧 URL 缓存留住旧图。10 个 PackageUrl、PackageSize、SHA256、Version、角色映射和授权均不改；原 `library-v1.0.0` 包和 tag 保留。正常刷新目录即可更新封面。客户端仍验证固定公钥 RSA-PSS、官方 HTTPS 路径与下载哈希。

### 状态与资源生命周期

Selected 是 Draft，Applied 是成功应用状态，Modified 是未应用修改。Download 只安装到本地库，Try 只使用受限预览缓存。角色画廊、放大和主题封面均不调用 Windows Apply。Cancel 保持恢复已应用主题、参数和 Profile ID 的含义。

ANI 展示改为按源帧真实像素尺寸解码，不再固定重采样到 48×48。原 Sequence、Rate、Jiffies 不改；真实 WPF Cursor 仍从原资源加载。隐藏或离开详情停止动画并释放 Cursor、帧引用和临时包。Technical Details 显示实际哈希、来源、兼容要求、验证后的逐角色文件映射、继承来源和原文件署名/哈希。

蓝色桌面拖尾经过导航是原本的已应用桌面效果，预览范围之外使用 Applied 参数。本轮选择保留此行为，没有增加未经充分验证的全窗口覆盖抑制选项；局部预览仍按物理坐标、滚动裁剪矩形排除桌面效果。全局输入穿透、非激活、采样和算法代码没有修改。

## 功能保护、测试与证据

`ThemeService`、`StudioService`、`DownloadManager`、`RemoteCatalogService`、安全 CursorPack、ProfileManager、四种渲染算法与原生输入代码均未修改。FROST、Obsidian、五套 Cute 资源及旧清单保持字节一致。发布构建仍通过两套原资源各 388 项检查。

Release solution 构建 0 错误、0 警告；自包含 Windows x64 应用及单文件安装器沿用原流水线。以下本轮真实重新运行，报告在 [validation/v1.3.1](validation/v1.3.1)：

| 检查 | 通过数 |
|---|---:|
| 核心及原生事务 | 180 |
| 桌面、输入、覆盖层、托盘 | 85 |
| 界面、键盘、窗口和逻辑布局 | 98 |
| Cute 集合 | 28 |
| 在线下载安全故障注入 | 51 |
| 公开网络与实际 WPF | 80 |

旧在线 51 项全部保留，新增 29 项：首屏主要操作、概览与技术层次、1×/2×/4× 观察不改变 Cursor 或 ANI 播放、真实角色缩略图、作用域反馈清理、15 个角色按钮逐一选择但不改 Draft、恢复入口展开、小窗口操作位置及快速导航资源边界。没有删除或跳过安全、输入、Apply / Cancel、Profile 和原生恢复断言。

线上验证实际获取 Pages 签名目录和 GitHub Release 包，再临时预览、下载、进入本地库、通过原事务应用 Windows、保存 Profile、阻止删除被引用资源、取消草稿和恢复原指针。测试使用隔离数据，在 finally 比较并恢复测试前 Windows 指针值；不会把测试备份混入用户最初备份。

100 / 300 / 500 项明确标记的模拟目录均只创建 24 张卡片；本轮最终媒体验证测得布局 25.94 / 25.87 / 26.65 ms，托管增量约 3.0 MiB，USER 增量 0。30 次详情/搜索/返回循环约 443 ms，GDI 增量 -4、USER 增量 0，回收后托管增量 -7648 bytes。30 次真实可爱主题切换 GDI 187→173，USER 101→81。数据为短时实机诊断，不能推断长期无泄漏。

原引擎 8 模式各约 4 秒诊断继续执行；关闭模式没有绘制失效。隔离测试可能与生产托盘共存，不当作独占后台资源或长期压力结果。性能原始报告保留供复查。

## 数据、安装与发布

开发前备份 `D:\CURSORIUM` 安装、快捷方式、用户数据和原生指针值，只留在忽略的本地 artifacts。重新计数为 72 个既有数据/资源文件，不套用旧 61 个数量。开发和隔离回归后，这 72 个原文件 SHA256 全部保持一致；五个关键配置文件的存在性和 SHA256 另有验证。

正式升级继续保留注册安装路径 `D:\CURSORIUM`、目标同卷且带归属标记的安装暂存和回滚机制。先运行 C: Windows TEMP / D: 目标的隔离升级，再创建真实安装和用户数据回退副本。安装器本身必须保持所有既有文件哈希；应用启动允许正常更新唯一的签名 `Online/catalog-cache.json`，其余原用户配置、收藏、Profile、原备份和原生资源继续逐文件核对。缓存变化单独记录，不冒称所有启动缓存永久不变。

C: TEMP / D: 目标的隔离安装升级已通过，公共摘要见同目录验证资料。本机最终真实升级报告写入忽略的 `artifacts/v1.3.1-installed/installed-upgrade-report.json`，记录正式安装文件匹配、快捷方式和用户数据结果；远程资产下载复核另存本机报告，最终状态以实际结果为准。安装器、便携版、主题 ZIP、源码 ZIP、SHA256SUMS 均经已有脚本生成并校验；新增 `cursorium-v1.3.1`，不修改旧版本历史。

- [正式 Release](https://github.com/catvir4493/cursorium/releases/tag/cursorium-v1.3.1)
- [源码提交记录](https://github.com/catvir4493/cursorium/commits/main/)
- [实际截图及前后对照](CURSORIUM-v1.3.1-SCREENSHOTS.md)

### 修改文件

主要 UI：MainWindow.xaml / .xaml.cs，Presentation/DiscoverView.xaml / .xaml.cs，Controls.xaml，ThemeCardView.cs，AnimatedCursorPreview.cs。新增 ThemeCoverRenderer.cs。DesignTokens.xaml、Icons.xaml、StudioViewState.cs、LivePreview.cs 保持原有实现。

工具/检查：App.cs 增加隔离封面导出入口，LibraryPublisher.cs 增加媒体独立导出，DiscoverVerification.cs 扩展 WPF 检查，generate-covers.ps1；既有发布脚本版本更新，verify-installed-upgrade.ps1 区分安装原文件保护与启动后的签名缓存刷新。App / Setup csproj 更新版本。没有第二套发布流水线。

## 未解决及未验证范围

真实环境为 Windows 11 build 26100、单屏 2560×1440 / 96 DPI。1280×830、1680×980、850×520、最大化、最小化还原及托盘已实际运行。1366×768、1920×1080、2560×1440 × 100%/125%/150% 是等效逻辑布局检查，不是物理 DPI 切换。

Windows 10、当前双屏、混合 DPI、长期压力、独占游戏、安全桌面、完整新安装/卸载/故障回滚矩阵仍未建立，本轮没有声称通过。在线目录固定密钥轮换、首次安装防重放/无过期或透明日志的既有限制仍保留。正常较小窗口需要纵向滚动浏览完整主题库和技术信息；主要操作和固定 Apply / Cancel 保持可达。封面忠于原像素资源，并不将概念图的插画替换成作者作品。
