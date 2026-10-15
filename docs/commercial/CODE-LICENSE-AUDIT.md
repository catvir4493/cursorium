# 代码、依赖与分发授权审计

日期：2026-10-15。基线 `7ed2719`；本轮保留根 LICENSE 全文和 `Copyright (c) 2026 FROST Project`。这是工程证据台账，未替代权利人的商业决定。

## 已公开代码与历史

原 `frost-legacy-main` / `6efcd1b` 的 LICENSE 与当前根许可同为 MIT。现有 src、installer、tools、tests、scripts 中的项目代码按根 MIT 公开；未发现单独 vendored 第三方源码目录或额外 PackageReference。v1.6 本轮新增代码继续适用既有 MIT，未建立专有 EULA，也未更改旧文件的许可声明。Git 历史、旧 Release、旧 tag 和 FROST 远端保留。

MIT 明确允许销售软件副本，要求保留版权与许可通知；收费渠道不自动将它变为独占授权。项目不会声称已公开版本的 MIT 可以追溯撤销，也不会把第三方指针的图像版权归入项目 MIT。[MIT 原文](https://opensource.org/license/mit)。

|对象|证据|商业分发边界|需要保留|
|---|---|---|---|
|FROST/CURSORIUM 项目代码、生成器与手写 XAML 几何|根 LICENSE、6efcd1b/7ed2719 历史、Icons.xaml、CursorArtwork|按现有 MIT 可收费分发；贡献权属仍须在正式商业签署前复核|原版权、MIT 全文|
|FROST/Obsidian 原生资源、项目 ico/preview/vector|生成器源码和真实资源摘要；非从概念 PNG 截取|项目 MIT；保留源材料记录|项目 LICENSE|
|.NET Core Runtime 10.0.13|准确版本 NuGet runtime pack 的 LICENSE.TXT、THIRD-PARTY-NOTICES.TXT；本机 coreclr 签名 Valid|主许可 MIT，内含组件适用各自附带条款；不能仅保留一句“MIT”|供应商原始 LICENSE、完整 THIRD-PARTY-NOTICES|
|Windows Desktop runtime 10.0.13 / WPF / WinForms|准确包 LICENSE，.NET Foundation 与 Contributors 版权；公开 WPF 源码许可|MIT 分发条件；正式 Steam 前还需完整供应链清单复核|包内 LICENSE；供应商附带通知|
|Windows 系统 DLL、Segoe UI/系统图标字体|Win32/WPF 系统调用与字体引用；未发现随包复制的字体或 OS DLL 集合|使用系统设施，不将 OS/字体重新授权或单独打包|适用 Windows 环境许可；不是项目 MIT 素材|
|Cute Collection 及其它在线指针|各包 NOTICE、原作者页面、签名 Catalog、素材矩阵|十五项第三方指针商业权利链 Needs Review；不进 Steam 白名单|来源、作者、原图像许可及继承 FROST 的 MIT|
|项目内 UI 截图与概念参考图|docs/images 与用户提供参考；部分截图显示第三方指针|不自动作为 Steam 商店/营销素材；实验 Steam payload 不复制完整 docs|逐张营销图权利复核，未获得新授权的图不公开声称可商用|
|音效|本轮源码/资产扫描无 wav/mp3/ogg 音效负载|没有新增音效分发授权结论|将来添加时另审|

`dotnet list Frost.sln package --include-transitive` 实际返回各项目无 NuGet PackageReference；框架引用和自包含运行时包仍然是分发依赖，不因这个结果被忽略。本轮修复此前便携负载缺少运行时供应商通知的问题：`licenses/dotnet-runtime-LICENSE.TXT`、`dotnet-runtime-THIRD-PARTY-NOTICES.TXT`、`windowsdesktop-LICENSE.txt` 从准确 10.0.13 缓存包原样复制，并纳入发布清单与 SHA256。来源文本参见 [.NET runtime](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) 与 [WPF](https://github.com/dotnet/wpf/blob/main/LICENSE.TXT)；真正随包的文本来自该构建准确版本的 pack，而非抓取 main 来代替。

apphost 的独立 LICENSE/THIRD-PARTY-NOTICES 也随包保留。[依赖清单](DEPENDENCY-INVENTORY.json)记录三个准确版本供应商包的实际 SHA512 与许可文件 SHA256；这些摘要验证保留包的一致性，不单独证明所有第三方权利已获商业终审。

## 构建工具与未完成权利

MSBuild/.NET SDK、PowerShell、Python 标准库、Git 用作开发/构建工具；没有把这些工具的完整发行包塞进应用。Git 的 GPL 与 Python/PowerShell 的各自许可不被重写为项目 MIT；未来若捆绑工具，须单独履行其条款。dotnet-trace 是本地诊断工具，不在应用负载。未接入 Steamworks SDK，不存在声称已获得其分发资格的文件。

正式商业前尚需确认：所有贡献者的权属、项目品牌/商店素材、精确第三方 CC BY 版本、Public Domain 上传者是否有权作声明、派生图像来源、完整 SBOM 与供应商通知审阅。既有 MIT 授权不包含商标授权或对第三方权利的保证。这些项目没有通过自动下载或安全 CUR 校验被“解决”。

## Community / Steam 决策方案（未执行授权变更）

1. **保持统一 MIT，按服务和渠道收费**：Community 与 Steam 共享源代码；Steam 提供平台更新、审核资源、支持与便利。许可最清晰，不能承诺阻止合法 MIT 再分发。当前实现只采用这一方案的技术兼容边界。
2. **将来新增、尚未发布的专有模块**：既有 MIT 核心保留；对未来全新模块另定许可、贡献与依赖边界。必须先由用户决定并复核权属，不把当前 v1.6 已按 MIT 发布的代码追溯变为专有。
3. **双许可/额外商用素材协议**：仅在确实拥有全部所需权利、或获得对应权利人授权后考虑；不是对公开 MIT 副本的撤销。

本轮没有修改 LICENSE、购买素材授权、联系作者、签署合同、添加 DRM 或改变历史下载权限。
