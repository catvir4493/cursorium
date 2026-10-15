# Steam Cloud 前置模型

没有 Steamworks SDK、AppID、自动上传、账号 API 或 Cloud 配置。只实现可在本地运行和验证的显式 portable 导出/解析/草稿解析；传输与同步冲突策略尚未实现。

|数据|处理|
|---|---|
|可移植 Profile|新 PortableWorkspace DTO：ID、名称、公开/内置资源 ID、可选版本/包哈希、视觉/点击参数|
|收藏|已知官方 ID，可未下载；私人 import ID 排除并计数|
|机器设置|FrameRate、PowerSaving、PauseFullscreen、窗口/DPI、托盘/开机行为不进入 DTO|
|原生状态|original-theme、theme-transaction、theme-ownership、注册表类型和值、完整路径全部排除|
|资源文件|私人 CUR/ANI、下载文件和不可变目录不上传；只携带安全版本引用|
|缓存与诊断|Online 缓存、下载会话、日志、raw trace 不导出|

导出只接受白名单字段；不能简单序列化整个 Settings/StudioState。路径样式/敏感赋值样式名称、私人资源引用、无效数值不进入候选。导入有大小/深度/重复字段/未知字段/schema 与参数边界验证，未知新版本拒绝。缺主题或缺固定版本返回 unresolved，不悄悄换另一套；Resolve 仅返回可选草稿，不调用 Profile.Save、Apply 或恢复 Windows。

```powershell
# data-copy 必须是隔离副本，不是现有 LocalAppData；原文件不作云同步目标
dotnet run --project tools/Frost.Foundation -c Release -- portable-export <isolated-data-copy> <application-directory> <verified-catalog-index>
dotnet run --project tools/Frost.Foundation -c Release -- portable-preview <fresh-isolated-report> <application-directory> <portable-workspace.json>
```

输出带 ExcludedProfiles/ExcludedFavorites，不能把排除私人引用当作成功同步全部数据；已有导出文件拒绝覆盖。Steam Cloud 将来需要按真实 Steam 账号隔离 portable 容器，处理冲突/合并/删除和 quota，且只白名单一个专用 portable 文件，不能把整个 FrostWhiteComet 或 CURSORIUM 目录设为 Auto-Cloud。Steam 官方说明也建议避免同步机器专属配置：[Steam Cloud](https://partner.steamgames.com/doc/features/cloud)。当前 Steam 数据根只是单 Windows 用户的前置隔离，不声称已完成 Steam 多账号隔离。
