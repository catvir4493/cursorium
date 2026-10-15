# CURSORIUM v1.6.0 Preview — 商业基础与资源库架构

日期：2026-10-15，基线 `7ed2719` / v1.5.2 Preview，.NET 10.0.13 / WPF / Win32。独立分支 `codex/commercial-v1.6` 与独立工作树执行；生产 D:\CURSORIUM 仍为 v1.5.1。本轮没有恢复自动鼠标/键盘输入，没有覆盖生产安装或真实数据，也没有执行 Windows Apply/Restore。只允许 Preview，不能标 Stable 或 Steam Ready。

## 实施结果

|任务|已实现|边界|
|---|---|---|
|代码授权审计|MIT/FROST 历史、依赖、资源/工具/发布边界台账；准确运行时许可与 NOTICE 入包|LICENSE 不变，不撤销旧授权，不自动建立专有许可|
|发行渠道|编译身份 Development / Community / Steam，独立构建负载、清单和无窗口探针|Steam 无 AppID/SDK/SteamPipe，原生写入禁用；不公开实验负载为正式 Steam 文件|
|Library v2|现有密钥独立 RSA-PSS 签名权利清单，严格绑定 v1 字节/作者/版本/包 SHA256，缓存与获取策略实际连接 DownloadManager|版权声明不等于权利证明；第三方商业审批仍待完成|
|身份迁移|ProductIdentity 兼容映射、隔离 copy/stage/receipt/rollback 工具与真实文件系统测试|未启用生产自动迁移、未改 exe/namespace/注册项/快捷方式|
|Cloud 数据前置|独立 PortableWorkspace DTO、公开资源/版本引用、白名单导出、缺资源解析为 unresolved、可运行 CLI|不接 Steam Cloud、不自动保存或 Apply 草稿，不上传备份/路径/私人文件|
|兼容与保护|保留旧检查，实际运行无输入安全子集及精确运行时基础检查|完整 WPF/原生事务/性能/硬件与长期门槛仍 Pending|

## 代码与素材许可

[代码授权审计](commercial/CODE-LICENSE-AUDIT.md)列出 MIT 公开历史、准确运行时依赖、NOTICE 要求、工具/字体/图像边界和 Community/Steam 方案。旧 FROST `6efcd1b` 与当前 MIT 保持；本轮新代码也按既有 MIT 公开。未来专有模块或双许可只提交方案，没有修改授权或历史 Release。

实际核对 **29 个现有包**的大小、SHA256、NOTICE、角色与参数；重新匿名获取 **14 个不同第三方指针来源页**及站点条款，观察到的 PD/CC BY 标签与既有记录一致。15 个第三方指针（含 Cool Cat 独立变体）商业权利链仍为 **Needs Review**，未因可下载或站点标签自动放行。CC BY 两项缺具体版本、Pastel 派生形状及作者别名等问题逐项记录。

**14 个原创 MIT 在线拖尾预设**进入内容白名单，另有 **6 个内置身份**（FROST、Obsidian、四个效果）的 MIT 记录。总计 **35 条真实权利记录：20 条原创/内置 Approved，15 条 Needs Review**；Steam 审核尚未完成，Approved 仅指记录的内容许可条件。全部 WithdrawalStatus=Active，没有删除旧资源。内置资源未曾声明版本，因此记录 unversioned，而非伪造 1.0 版本。

见[逐项素材矩阵](commercial/ASSET-LICENSE-MATRIX.md)、[原始哈希与来源台账](commercial/RESOURCE-LICENSE-AUDIT.json)、[准确依赖清单](commercial/DEPENDENCY-INVENTORY.json)。无额外 NuGet PackageReference 不代表没有运行时依赖；Core runtime、Windows Desktop 与 apphost 10.0.13 的供应商许可/通知均复制，SHA512 对应保留包；coreclr 实际 Authenticode 为 Valid。完整供应链/贡献权属与商店素材审阅仍是商业门槛。

## 在线资源兼容与公开结果

- Library 仓库：[cursorium-library](https://github.com/catvir4493/cursorium-library)。新提交：[5a62639](https://github.com/catvir4493/cursorium-library/commit/5a62639ff061401b458c2edd5a21327ba2022432)。
- 实际公开 [rights.json](https://catvir4493.github.io/cursorium-library/catalog/v2/rights.json)、[分离签名](https://catvir4493.github.io/cursorium-library/catalog/v2/rights.json.sig)、[Schema](https://catvir4493.github.io/cursorium-library/catalog/v2/rights.schema.json)。SchemaVersion=2，独立权利 revision=1.0.0。
- [公开网络回执](validation/v1.6/library-publication.json)：新 JSON SHA256 为 `8a0a29dba1275b838f67135e785665ab714319c9246ac28484b6e0679d5310d8`，现有 v1 SHA256 为 `03ecff4b1cc6899717c2a7552930baf98a535911e018fa33a021b5c59a73a018`。29 个资源与六个合集在发布前后实际取回并验签。
- v1 index、签名、合集、封面路径与资源 ZIP 没有变更，旧客户端兼容。没有移动旧 tag、重打旧 ZIP 或删除 Windows 使用的文件。
- `publish-library.ps1 -RightsOnly` 扩展现有发布入口，先审查/本地验签、核对公开 v1，再 fast-forward 发布并验证 Pages 字节；短暂 CDN 未更新的失败记录保留，最终公开验证成功。私钥只在受保护位置读入签名过程，未上传。

[v2 协议与以后上架命令](commercial/LIBRARY-V2.md)说明逐资源身份、权利审核、撤回、独立缓存、离线和密钥边界。Community 缺 v2 时保持 v1 使用；Steam 新获取必须有当前有效清单和显式商业许可。新下载仍不应用 Windows；选择/Apply/Cancel 与 Profile 语义不变。撤回不会删本地原生资源，v2 不能追溯撤销老 v1 客户端或旧 MIT 分发。

## 渠道、迁移与可移植数据

[渠道设计](commercial/DISTRIBUTION-CHANNELS.md)与 [实际编译探针](validation/v1.6/channel-probes.json)：三个渠道真实 publish 成功，不是 JSON 静态模拟。Community 保留旧 LocalAppData 和五套 Cute；Steam 使用独立数据根，打包白名单验证实际字节，实验负载只有系统占位、FROST/Obsidian 与四种内置效果，不含 Cute/安装器/私钥/Steam SDK。没有向 D:\CURSORIUM 部署。

[身份映射与迁移工具](commercial/IDENTITY-MIGRATION.md)：保留旧实例/事务锁，非 Community 不向其它渠道转发 restore/show；不做全局字符串替换。真实隔离文件系统验证了 SHA256 复制、ID/备份字节保留、取消、未知 schema、未处理事务、重复迁移、修改冲突拒绝回滚与 NTFS junction 拒绝。模拟旧/新路径不是生产迁移验收。

另从新建私有备份复制现有 Profile/原始指针备份，仅在独立目录用 **.NET 10.0.13** 解析：1 个现有 Profile 兼容，原始备份 schema 兼容，15 个原始角色文件当前存在。[脱敏结果](validation/v1.6/legacy-private-copy-inspection.json)。没有调用真实恢复，不能把文件存在性当作完整回滚验收。

[Cloud 前置边界](commercial/CLOUD-DATA-BOUNDARY.md)与 CLI 本地导出/草稿预览实际运行，结果明确 `cloudUploadPerformed=false`、`systemApplyPerformed=false`。修复了固定旧版本导出被误标为当前版本的问题，保留真正的版本/SHA256；缺资源/缺固定版本不替换。新增文档要求显式 schema、拒绝重复/未知字段，不覆盖已有导出；机器帧率、启动、DPI/窗口、注册表、备份、完整路径、私人资源、日志/缓存不在 portable DTO。

## 构建与实际测试

阶段构建均为 `dotnet build Frost.sln -c Release`，0 error、0 warning；混合 WPF/WinForms 原有 WFO0003 / WFAC010 特定例外保留，没有扩大禁用警告范围。没有改四种拖尾算法、点击绘制、Raw Input 或采样频率去追逐性能数字。

|检查|实际结果|环境/范围|
|---|---:|---|
|新增基础专项最终版|**147 通过**|精确 .NET 10.0.13 自包含验证器；发行前另核对实现；真实签名与 29 个包、本地文件系统、受控 HTTP、明确模拟版本/schema 状态|
|保留核心|162 通过|不带 --native-roundtrip；18 项原生应用/回滚不冒充通过|
|保留在线安全|51 通过|真实固定公钥、受控网络故障/ZIP/URL 测试|
|保留合集/更新|104 通过|原签名 stage 与旧 catalog，未删断言|
|真实公网/加密目录下载|14 通过|Simple Dot or Star，独立 LocalAppData 目录，未 Apply|
|FROST / Obsidian|388 / 388 通过|实际 Windows CUR/ANI 加载校验，不是切换系统主题|
|原性能协议测试|17 通过|仅合成协议夹具，没有驱动鼠标或跑性能矩阵|
|未知渠道编译|按预期拒绝|实际 MSBuild 负例|
|NTFS junction 迁移|按预期拒绝|实际受控 junction 与外部哨兵文件保留|
|三个渠道身份|探针通过|无 WPF 窗口/输入/原生应用，不等于 Steam 客户端验证|

最初安全套件的基础版 142 项已通过；之后增加固定旧版本、显式 schema、明确拒绝再分发、渠道排除、LocalAppData 绝对路径五项，最终 147 项在准确自包含运行时重跑。不同批次不累计成更多“功能通过数”。执行命令、UTC 起止时间及范围见[安全执行记录](validation/v1.6/safe-execution.json)、[保留检查结果](validation/v1.6/retained-checks.json)与最终基础专项记录。受控 HTTP、模拟版本清单和真实公网/Windows 文件系统分别标识。

可复现命令（工作树下，全新 artifacts 输出）：

```powershell
dotnet build Frost.sln -c Release
scripts/build-channel.ps1 -Channel Community -Version 1.6.0 -OutputDirectory artifacts/community-test
scripts/verify-commercial.ps1 -ApplicationDirectory artifacts/community-test -RetainedArtifacts <retained-public-fixtures> -ReportDirectory artifacts/commercial-tests
scripts/build-release.ps1 -Version 1.6.0
scripts/verify-release-assets.ps1 -Version 1.6.0
scripts/verify-release-hardening.ps1 -Version 1.6.0
scripts/publish-github.ps1 -Release -Prerelease -Version 1.6.0 -NotesPath docs/releases/1.6.0.md
```

只有实际回执才表示对应命令完成；后两类发行结果在下方附记更新。原 `verify-v1.5.ps1`、完整 UI/原生验证入口全部保留，但本轮没有因文档列出它们而擅自恢复输入。

## 未完成的商业与安全门槛

- v1.5.2 的 21 个不足轮次的性能组、完整自然输入、native CPU profiling、完整 WPF/原生桌面回归继续 Pending。没有宣布 CPU 根因已解决。
- Windows 10、真实双屏/混合 DPI、24h/72h 未执行；当前真实机器仍是 Windows 11 / 单屏 100%，逻辑模拟不冒充硬件。
- 15 个第三方指针商业权利链、CC BY 明确条款、营销图/品牌/贡献权属、完整供应链法律审阅、发行证书未完成。
- Steam AppID/协议、SDK、SteamPipe/depot、真实更新/卸载、账号数据分区、Cloud 传输/冲突/quota 没有集成。
- 真实身份迁移与备份 handover、旧新进程跨版本桌面行为未完整验收，Steam 原生写入仍禁用。没有承诺已可安全部署付费版本。

## 文件、证据与发行附记

工程修改主要在 Distribution、LibraryRights/BundledRights、IdentityMigration、PortableWorkspace/FoundationJson、Foundation CLI、渠道/发布/安全验证脚本及 docs/commercial；ThemeService 只增加渠道阻断与使用原锁常量，没有重写事务。完整清单见本轮修改文件记录。

原始安全回执、示例导出、私有迁移复制与精确运行时工具留在独立工作树 `artifacts/`。最初生产回退点在原工程 `artifacts/v1.6-baseline/`，不提交；公开证据不包含真实 Profile、注册表值、资源私人路径或私钥。当前用户只会获得 Community Preview 的安装器/便携/主题/源码/校验文件；本地实验 Steam payload 不作为正式版发布。

发行附记：最终社区构建、安装器隔离检查、资产字节/公开下载核验尚待后续完成并更新。本机生产安装保持 v1.5.1，v1.4.0 保留 Latest Stable；新版本只能是 [cursorium-v1.6.0 Preview](https://github.com/catvir4493/cursorium/releases/tag/cursorium-v1.6.0)。
