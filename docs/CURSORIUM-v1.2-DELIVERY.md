# CURSORIUM v1.2.0 — Cute Collection & Theme Library Expansion

日期：2026-10-13。基线 main `76c3bbf`（已发布 v1.1.0 及发布脚本修复），工作区开始时干净。
回退分支 `codex/rollback-cursorium-v1.1.0`；开发分支 `codex/cute-collection-v1.2`。
保持 C# / .NET 8 / WPF / Win32，没有新框架或运行时依赖。

## 实施结果

五个候选的作者 ZIP 全部实际下载并经安全 staging 验证，共 75 个原始 CUR/ANI。
每个资源及原始 ZIP 均记录 SHA256；来源页与包内授权声明一致，均为 Release to Public Domain。
[完整来源、作者、授权、原文件哈希与角色表](CURSORIUM-v1.2-SOURCES.md)。

| 名称 / 作者 | 原始 CUR | 原始 ANI | 原生标准角色 | 继承角色 | 完整应用 |
|---|---:|---:|---:|---:|---|
| [Kawaii Cat / MUFFET!!，归档 Sun_l1ght](https://www.rw-designer.com/cursor-set/kawaii-cat-curs0rs) | 1 | 14 | 15 | 0 | 是 |
| [Kawaii Pink Heart / bobalover，归档 yurilover](https://www.rw-designer.com/cursor-set/kawaii-pink-heart) | 6 | 10 | 15 | 0 | 是 |
| [Bunny Cute Kawaii / goat](https://www.rw-designer.com/cursor-set/bunny-cute-kawaii) | 5 | 7 | 12 | 3 | 是，明确继承 FROST |
| [Pastel / saraantie，部分形状 quackyduck](https://www.rw-designer.com/cursor-set/pastel) | 13 | 0 | 12 | 3 | 是，明确继承 FROST |
| [Cool Cat / Bobcat](https://www.rw-designer.com/cursor-set/cool-cat) | 0 | 19 | 15 | 0 | 是 |

角色数量以映射到 15 个标准 Windows 角色的不同角色为准，不以文件数充数。
Bunny 缺少两种对角缩放及 Link；Pastel 缺少 Working、Move、Alternate。
作者 CRS 指定 Pastel Busy 2，另一个 Busy 是附加文件。Pink 的 Location 及 Cool Cat 的四个额外文件保留在 Extras。
Kawaii Cat 的特殊 ZIP 路径按作者 CRS 明确映射并重命名，字节未变。
没有执行任何第三方 CRS/INF/脚本/安装器，没有替代下载或空白主题。

## 架构与交互

- 扩展原 `CursorTheme` SchemaVersion 1 的可选元数据，旧 FROST、Obsidian 和用户清单继续可读。
  `SourceFiles` 记录原文件名、保留路径和 SHA256；`InheritedRoles` 明确每个回退角色的主题 ID。
- `ThemeCatalog` 仍是唯一主题发现/读取/导入服务。读取时验证实际文件与声明哈希。
  导入先写独立 GUID staging，验证完整清单后原子移动到用户 Themes；重复导入不会覆盖。
- `CursorPack` 是原导入服务的受控输入适配器，不是第二套主题系统。
  支持 ZIP 和目录；限制归档 64 MiB、256 entries、128 cursors、单资源 32 MiB、总资源 64 MiB、压缩比 200。
  在 ZipArchive 分配目录对象前检查 EOCD entry 数；拒绝 multipart/ZIP64、路径穿越、根路径、链接与资源耗尽。
  正式目录只接收验证后的 CUR/ANI，所有成功、失败、取消路径清理 staging。
- 名称识别与枚举顺序无关；矛盾、自定义或重复名称保留为待确认。
  Import Cursor Pack 的真实 WPF review 可逐角色选文件、指定回退、确认缺失角色；旧单角色导入仍保留。
  通用包不自动执行或解释安装脚本，作者 CRS 仅在内置资源审计时作为映射数据读取。
- v1.1 的三栏、WindowChrome、DesignTokens、公共控件样式与固定 Apply/Cancel 区均保留。
  ThemeCard 基于原样式增加真实缩略图、作者、静态/动态说明、角色数量、收藏及独立状态。
  All / Built-in / Cute / Imported / Favorites 来源于同一目录；过滤和收藏只保存 ID 到 `library.json`。
- `Selected` 是草稿选中；`Applied` 是成功应用的主题；`Modified` 表示草稿改动。
  筛选和收藏不会改变草稿、Windows 或 Profile。选择主题仅更新预览；Apply 仍调用 StudioService 原事务。
  Cancel 保持恢复已应用主题、Profile ID 和参数的含义；Ctrl+Enter、Escape、F6 保留。
- 实际预览鼠标仍使用原始 WPF/Windows CUR/ANI，保留热点与系统动画行为。
  `AniTimeline` 验证帧索引、seq、rate 和 1/60 秒 jiffies。
  预览栏的原第一帧图像扩展为实际 ANI 帧：一次解码成最多 256 个 48×48 frozen images，立即释放临时 native handles；按原序列与时间显示。
  滚出 viewport、窗口隐藏或切换页面时停止 timer；切换角色/主题释放旧帧引用。关闭可选 UI 动画不停止功能性光标动画。
- `ThemeService`、`StudioService`、ProfileManager、TrailEngine、LivePreview、透明覆盖层和点击渲染代码保持原逻辑。
  资源仍应用到不可变的 `Theme/installed/<id>/<hash>`；安装包路径不作为已应用原生资源路径。
  生成器只调整清单序列化投影，确保 FROST/Obsidian 原资源和原清单字节稳定。

## 构建与验证证据

[可公开的真实报告](validation/v1.2/)；原始机器注册表快照和用户数据不包含在这些报告中。

| 检查 | 实际结果 |
|---|---|
| Release solution、Windows x64 自包含 app / setup | 0 warnings / 0 errors |
| FROST / Obsidian 资源原检查 | 各 388 项通过；资源与旧清单无 Git 差异 |
| 核心与原生事务 | 178 项通过，包含原 66 项和新 112 项；五套主题逐一真实 Apply 并恢复 |
| 原桌面回归 | 85 项通过；原脚本未删改，当前仅一屏，历史双屏 89 项中的四个第二屏实例本轮无法执行 |
| 原界面检查 | 98 项通过，原检查全部保留 |
| Cute Collection WPF 专项 | 28 项通过；真实移动、收藏、筛选、ANI 动画、Apply/Cancel、Profile、重启、句柄释放 |
| 原始下载包 | 5 ZIP / 75 cursor resources 全部经 production staging 与 Windows 加载验证；逐文件哈希一致 |
| 受保护用户文件 | settings / studio / profiles / original-theme / interface：存在性与 SHA256 全部不变 |
| 失败安全 | ZIP 穿越/绝对路径/链接/数量/单文件/压缩比/坏 CUR 拒绝，重复导入、映射冲突、坏 ANI seq/rate 验证；原生部分失败与持久化失败回滚保留 |
| 安装升级 | 先复制当前 v1.1 安装与 59 个受保护数据/资源到 TEMP 隔离升级；安装内容逐文件匹配、五主题可读、原生状态不变。通过后备份真实安装和数据并升级当前安装，快捷方式与数据保留 |

执行入口：`scripts/build-release.ps1 -Version 1.2.0`、`scripts/verify-cursorium.ps1`、
`scripts/verify-release.ps1 -IsolatedUpgrade -Version 1.2.0`、Frost.Verify `--pack-audit`。
主测试与安装测试使用独立数据目录；真实升级前备份完整原安装、用户目录与注册表值/类型到本机 artifacts。
安装隔离模式限制 TEMP 中的 `CursoriumUpgradeTest-*` 目录，独立 shortcut/注册项，不向真实运行实例发送 shutdown、不修改用户启动项。

## 真实截图

[运行截图说明](CURSORIUM-v1.2-SCREENSHOTS.md) 包含 Cute 列表、Cat/Pink/Bunny 预览、详情、
Applied 与 Selected 对照、映射 review 和小窗口。均为实际运行的 WPF native window 屏幕捕获。

UI 文件变更：`MainWindow.xaml`、`MainWindow.xaml.cs`；新增 presentation 的
`ThemeCardView.cs`、`PackImportDialog.cs`、`AnimatedCursorPreview.cs`。
`DesignTokens.xaml`、`Controls.xaml`、`StudioViewState.cs`、`UiMotion.cs`、WindowAppearance 与原导航/标题栏框架没有重写。

## 性能与边界

短时 benchmark 为每场景约 4 秒、28 个逻辑处理器、单屏。禁用/idle 场景无渲染 invalidation；
机器 CPU 约 0.18% / 0.19%；四个 60-FPS 场景约 0.18–0.56%，实测约 54–55 invalidations/s。
120 target 约 69，省电约 30；这些是 WPF invalidations，不是 GPU present 或恒定 FPS 保证。
GDI 各场景保持 18；USER 约 19–21。30 次动画主题切换后资源计数未持续增长，具体数值见 collection-report。
工作集与 CPU 包含当前运行环境、WPF、测试及截图开销，不能作为长期或跨设备保证。

本轮真实环境是 Windows 11 build 26100、2560×1440、96 DPI。小窗口、最大化与真实输入完成。
1920×1080 及 125%/150% 使用等效逻辑布局验证，不冒称本轮物理分辨率/DPI 切换。
当前第二块屏未被 Windows 枚举，本轮双屏未验证；v1.1 的历史双屏证据不替代本轮证据。
Windows 10、混合 DPI、安全桌面、独占全屏、长期压力和真实卸载/全新安装本轮未验证。
用户导入资源的再分发权不由软件自动认定；该版本的公开素材仅来自已核查的五个 Public Domain 包。

## 发布与回退

使用原 `build-release.ps1` / `publish-github.ps1`，生成 installer、portable、themes、source 和 SHA256 五个 assets。
版本目标与新 tag 为 `cursorium-v1.2.0`；旧 tag、旧 Release 和发布历史不改动。
[v1.2.0 Release](https://github.com/catvir4493/cursorium/releases/tag/cursorium-v1.2.0)。
发布脚本验证远程提交、上传大小与 GitHub SHA256 digest；本轮也下载公开资产与本地校验表逐一比对。
精确发布 commit、最终资产 SHA256、最后一次安装核对报告以本机 `artifacts/github-publication.json`、
`artifacts/CURSORIUM-SHA256SUMS.txt` 和 `artifacts/cute-v1.2/installed-upgrade-report.json` 为准。
私有回退归档留在 artifacts；没有上传用户设置、Profile、注册表快照或备份。

安装核对补充：开发开始时指定的桌面 CURSORIUM.lnk 已不存在（原安装的开始菜单快捷方式存在）。
在完整备份与数据/原生值核对后，复制正确的开始菜单入口重建桌面快捷方式；实际启动并再次点击桌面入口确认同一进程。
