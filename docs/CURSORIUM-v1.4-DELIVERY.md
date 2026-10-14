# CURSORIUM v1.4.0 — Online Library Expansion & Curation

## 实际内容成果

在线库从 10 项扩充为 **29 项：15 套指针、14 套拖尾**。新增 9 套指针与 10 套原创数值预设；未达到 30+ 的规划目标，没有用无授权、错误热点、弱可读性或空主题凑数。

审核了 28 个候选页面，实际下载 26 个作者 ZIP，批准 9 套指针，19 个候选暂缓、拒绝或无有效下载。所有实际下载的 CUR/ANI 均经生产校验器及 Windows 原生加载，但这不自动构成授权或质量批准。9 套获批作品含原始 **119 CUR、73 ANI**，共 135 个原生角色映射、**0 个继承角色**，其中 48 个角色使用 ANI。完整台账逐项列出作者、原始日期、下载链接、许可、原始包/逐文件/发布包 SHA256、热点、ANI 时间数据和拒绝理由。

新增指针为 Basic、Dot Minimal、GOLDEN、AmberRose、Brown Cow、Little Chicken、Discrete、Lovely Sunshine、Simple Dot or Star。其中 Little Chicken/Sirea、Discrete/Vlasta 按来源所述 CC by 署名；其余按来源 PD 声明并核查原创/通用形象背景。没有把未知角色 IP 的 PD 上传标签当作权利证明。CC by 来源未注明版本，不擅自补造版本号。每个包保留作者、来源及 NOTICE，原始像素、热点、时序与额外变体均不修改。

新增拖尾为 Sakura Blossom、Ice Crystal、Lavender Dream、Emerald Stream、Sunset Glow、Cyber Pulse、Moonlight、Stardust Pink、Ocean Mist、Amber Whisper。十套参数各不相同，覆盖 Comet/Minimal/Neon/Stardust，仅含受限数值与颜色，不含可执行渲染代码。封面由生产 TrailModel/TrailPainter 生成。

原 FROST、Obsidian、五套 Cute 的文件与身份保留。五个旧作者原始 ZIP 在 2026-10-14 重新下载，SHA256 全部与既有审核记录一致。原来的 10 个公开资源包、版本、下载 URL 与哈希未变。

完整数据：[来源、授权和逐项质量结论](CURSORIUM-v1.4-SOURCES.md)、[审核台账](curation/acquisition-ledger-1.1.0.json)、[原生逐文件审计](curation/native-audit-1.1.0.json)、[明确角色映射](curation/library-1.1.0.review.json)。

## 功能与架构

|模块|结果|
|---|---|
|Explore|保留 v1.3.1 网格、设计 Token、分页、静态封面与异步媒体；加入合集与 Animated / Installed / Not Installed 过滤|
|精选合集|Cute & Cozy、Minimal Workspace、Pixel Nostalgia、Neon & Cyber、Animated Favorites、Motion Essentials；显式真实 ID，独立签名/缓存|
|搜索|本地搜索名称、作者、描述、标签和合集名称，不按键发远程查询|
|详情与预览|原 CUR/ANI 时序/热点、按需临时校验预览和 Download/Apply 分离保留|
|资源更新|同 ID 比较包版本；拒绝降级、同版本内容替换、作者/来源冲突；旧哈希目录保留|
|新旧应用版本|配置增加可选包哈希绑定；预览新版启用 Apply，Cancel 返回旧版；只调拖尾不顺带更新指针|
|Profile|已有文件不在下载时重写；保存的新配置可绑定不可变版本；旧 SchemaVersion 1 兼容|
|本地库|新包进入 Cursors / Trails；收藏不下载，下载不应用；Remove 保留既有使用/备份/Profile 保护|
|窗口与引擎|保留 WindowChrome、托盘、快捷键、四算法、输入穿透及原生回滚事务；未重写 ThemeService|

旧版本的已应用资源绑定在内存中根据实际原生状态推断，迁移本身不重写用户 Profile 或设置。版本固定通过既有受管理目录中的已验证清单解析，拒绝路径逃逸。

## 云端发布与维护

- [独立库](https://github.com/catvir4493/cursorium-library)
- [新资源 Release：library-v1.1.0](https://github.com/catvir4493/cursorium-library/releases/tag/library-v1.1.0)，19 个新数据 ZIP 与校验表
- [签名 Catalog v1](https://catvir4493.github.io/cursorium-library/catalog/v1/index.json)，当前 CatalogVersion 1.1.1
- [独立签名合集](https://catvir4493.github.io/cursorium-library/catalog/v1/collections/index.json)、[Schema](collections-v1.schema.json)
- [资源提交 ec6b6ff](https://github.com/catvir4493/cursorium-library/commit/ec6b6ff6c06f381e4de0554387b6275466c3580e)
- [仅元数据提交 2ca809e](https://github.com/catvir4493/cursorium-library/commit/2ca809e102fcbd50feeeec0066ece89485aae63a)：修正 Minimal 分类、合集成员和真实拼贴封面，**0 个包变化**

真实下载示例：[Brown Cow](https://github.com/catvir4493/cursorium-library/releases/download/library-v1.1.0/brown-cow.zip)、[Ice Crystal](https://github.com/catvir4493/cursorium-library/releases/download/library-v1.1.0/ice-crystal.zip)。后续新增兼容主题只需复用[审核/验证/打包/封面/签名/发布流程](CURSORIUM-LIBRARY-WORKFLOW.md)。发布顺序已修复：先预检并公开验证包，再推送目录；不删除旧目录，不修改已发布 ZIP 或旧 Tag。RSA-PSS 继续使用既有固定公钥，私钥不进入仓库、安装包或报告。

## 真实检查与证据

|检查|本轮通过数|
|---|---:|
|核心与原生事务|180|
|桌面/输入|85|
|界面|98|
|真实缓存 WPF 重启|99|
|原集合回归|28|
|原在线安全|51|
|新增策展/签名/安全更新/版本绑定|104|
|实际网络与 WPF 全流程，含新资源 Apply|103|
|发布工具失败边界与合法更新|10|
|真实旧 v1.3.1 二进制读取扩充目录及 UI|98|

Release 源码构建零错误、零警告；FROST 与 Obsidian 各 388 项原生资源检查通过。保留全部历史测试，没有删除或跳过旧断言。旧 Discover 的固定 10/4 内容断言改为验证全部旧 ID 保留及实际扩充内容。最终完整流水线通过；其后紧凑合集视觉调整单独重新运行全部 103 项在线 WPF 检查。

公开网络重新获取全部 **29 包、35 张媒体**，精确 RSA-PSS、SHA256、实际大小与原生资源验证通过；Catalog 1.1.1 再次独立验证。旧客户端实际获取并验签了扩充 Catalog 1.1.0，Resources 协议未增加字段。测试使用隔离目录；原生事务结束后与执行前指针快照完全一致。五个受保护用户 JSON 的存在性/哈希检查通过。

100/300/500 项模拟目录均只创建 24 张资源卡；布局时间、句柄和内存的实际值见[公开测试证据](validation/v1.4/summary.json)与[WPF 性能](validation/v1.4/discover-report.json)。它们是明确的测试夹具，不是虚假在线主题。30 次详情/搜索切换未出现持续 GDI/USER 增长。短时引擎采样涵盖关闭、空闲、四算法、120 目标与省电模式；数据是 WPF invalidation/CPU 观测，不能当作 GPU 呈现帧率或长期性能承诺。生产托盘进程仍在运行，这些隔离进程采样不是洁净、独占的整机能耗基准。

所有界面图片见[实际 WPF 截图与同尺寸对照](CURSORIUM-v1.4-SCREENSHOTS.md)。首次桌面捕获返回无关窗口的问题已经识别，那批图片未上传；当前公开图来自实际运行的 WPF 视觉树。

## 构建、安装与正式交付

沿用 build-release.ps1、verify-release.ps1、verify-installed-upgrade.ps1 和 publish-github.ps1。正式目标为 [cursorium-v1.4.0](https://github.com/catvir4493/cursorium/releases/tag/cursorium-v1.4.0)，包含安装器、自包含 x64 便携版、内置主题包、源码包及 SHA256。新增在线数据包由独立 library Release 分发，不假称已全部内置到桌面主题 ZIP。

安装路径保持 **D:\CURSORIUM**。候选安装器已在真实旧安装与用户数据的隔离副本中完成跨卷升级：84 个受保护文件哈希不变、839 个负载文件一致、五套 Cute 存在，真实系统指针值/类型不变；[已脱敏报告](validation/v1.4/isolated-upgrade-report.json)。升级必须先通过真实现有安装/数据副本的隔离跨卷验证，再备份当前安装、数据、快捷方式和注册表，使用原有安全安装器替换，核对版本、负载哈希、快捷方式、原生指针与用户数据。首次真实升级已成功启动 v1.4，配置/收藏/Profile/原始备份哈希不变；检查器曾将旧进程的两份临时预览文件清理误判为数据损坏，现已将允许范围收紧为可证明已结束会话下的缺失临时文件，永久资源与已有文件内容变化仍一律失败。见[保护核对](validation/v1.4/current-upgrade-protection-audit.json)。原始机器备份及最终安装报告留在本机忽略的 artifacts/v1.4.0-installed，不进入 GitHub；最终交付消息报告实际执行结果。正式资产校验表与包分别发布，不能用预先写入文档的哈希冒充最终文件验证。

## 未解决与未实测

- 资源总量为 29，未达到规划的 30+；可爱/像素类比例低于最初目标，拒绝候选与后续复核理由已列入台账。
- 仅实测 Windows 11、单个 2560×1440 显示器、96 DPI/100%。1920 等窗口/逻辑布局、125%/150% 是逻辑模拟，不能当作实际屏幕模式或物理 DPI 切换。
- 双屏、Windows 10、真实混合 DPI、长期压力、完整安装/卸载矩阵没有实测。
- 暂无公开的新包升级版本；更新用明确隔离夹具测试，不制作虚假的线上 Update Available 截图。
- 部分原作有柔和色彩、细点状 Text 或图画式角色的取舍，已披露；未声称完成长期人体工学/动画舒适度评估。
- 继续依赖固定签名公钥及受控 GitHub 来源，没有独立透明日志、目录到期服务或自动密钥轮换。
