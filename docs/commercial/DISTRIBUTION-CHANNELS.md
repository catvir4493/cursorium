# 渠道与程序更新边界

`CursoriumChannel` 是编译属性，生成 App/Core 的 AssemblyMetadata。运行时不从可修改 JSON 或环境变量切换渠道。默认 Development；现有 `build-release.ps1` 明确构建 Community，`publish-github.ps1` 不参与构建，也不调用 Steam 上传工具。

|渠道|数据根模板|程序文件更新权|本轮可用性|
|---|---|---|---|
|Development|%LOCALAPPDATA%/CURSORIUM/Development|开发工具|可构建、可探测；非隔离环境禁止原生指针写入|
|Community|%LOCALAPPDATA%/FrostWhiteComet|手动 Community 安装器；无第二个自动更新器|旧数据与安装路径兼容；只发布 Preview|
|Steam|%LOCALAPPDATA%/CURSORIUM/Steam|SteamPipePlanned|仅本地实验负载，没有 AppID/SDK/SteamPipe；原生写入禁用|

所有渠道使用同一份核心源代码。正式 Steam 未来直接交付 depot payload，不依赖 GitHub Setup/Uninstall。Steam 实验包没有 Community 安装器、卸载器、steam_appid.txt、Steam SDK 或私钥；仅含审核过的 FROST/Obsidian 与四种内置效果，以及准确运行时通知。不带五套尚待商业确认的 Cute 文件，也不带整份可能包含第三方截图的 docs。Community 仍保留五套主题和其 NOTICE。

官方 Library 是**数据资源**更新，与程序更新分开；GitHub 固定公钥/RSA-PSS、包 SHA256、HTTPS/重定向白名单、安全 ZIP/CUR/ANI 保持。Steam 的新资源获取必须获取当前有效商业权利签名，失败即停止；本地已安装资源不因离线被删除。没有写 Steam 程序文件的 GitHub 自动更新器，也没有将 Community 安装注册指向 Steam。

```powershell
scripts/build-channel.ps1 -Channel Development -Version 1.6.0 -OutputDirectory artifacts/dev-fresh
scripts/build-channel.ps1 -Channel Community -Version 1.6.0 -OutputDirectory artifacts/community-fresh
scripts/build-channel.ps1 -Channel Steam -Version 1.6.0 -OutputDirectory artifacts/steam-fresh -RightsDirectory artifacts/audit/rights-stage
scripts/build-release.ps1 -Version 1.6.0
```

前两类 payload 可运行；Steam 输出明确标为 ExperimentalSteamFoundation。`--foundation-probe --isolated-test --test-data-root <fresh-safe-root>` 只读取编译身份并写探针，不建 WPF 窗口、不申请正常实例、不写原生指针。发布闸门实际运行探针，不能仅靠编辑 distribution-manifest.json 把 Steam 冒充 Community。源 ZIP 仍绑定当前提交的规范 Git 对象。

Steam 商店、AppID、depot、SteamPipe 上传、更新/卸载、账号数据分区及授权合同未验证；此文不是已集成声明。当前 D:\CURSORIUM 的 v1.5.1 未部署或升级。
