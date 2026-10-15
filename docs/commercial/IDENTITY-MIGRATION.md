# FROST → CURSORIUM 兼容映射与隔离迁移

本轮确立产品身份，不部署破坏性重命名。命名集中在 ProductIdentity，以下旧值刻意保留，以免“换品牌”制造第二个写 Windows 的进程。

|现有标识|v1.6 行为|未来迁移门槛|
|---|---|---|
|FrostWhiteComet.exe|保留二进制入口，产品/UI 为 CURSORIUM|新 exe/alias 需验证旧快捷方式、启动项、Steam depot 和恢复入口|
|Frost.* namespace|保留；不是用户数据标识|没有必要做全局源码替换|
|LocalAppData/FrostWhiteComet|Community 原位读取，不搬原生稳定文件|新路径须显式迁移和恢复 handover|
|HKCU Run/FrostWhiteComet|Community 兼容；Development/Steam 不写该值|按渠道与平台协商，不覆盖旧启动行为|
|FrostWhiteComet- + SID 的 IPC/mutex|跨旧/新渠道共用实例排他；非 Community 不向另一个渠道转发 show/restore|真实跨版本进程与桌面回归仍 Pending|
|Local/FrostWhiteComet.Theme|完全相同的事务锁名称|不得分裂系统指针写锁|
|FrostWhiteComet.Setup/卸载注册项/旧快捷方式|Community 兼容，Steam 没有该安装器依赖|新部署需单独授权和生命周期验收|
|Profile、资源 ID、原始备份、不可变主题路径|保持既有 schema/内容，Windows 仍引用原资源|不能删除旧目录或把备份同步到另一台机器|

IdentityMigration 只在显式标记的 `CursoriumMigration-*` 隔离沙盒内运行：legacy → 受控 staging → cursorium；逐文件 SHA256 前后核对，禁止 live 数据/安装目录重叠、重解析点、超量、未知 schema 和未处理事务日志。它不会运行恢复、重写注册表路径、改原 Profile ID 或删除原数据。原生备份中的机器绝对路径只作为本地字节保留，绝不成为 Cloud 数据。

完成的 receipt 可识别 CopyPrepared/Prepared/RolledBack；发生半提交时保留证据。Rollback 先核对所有字节，发现用户后续修改即拒绝；正常回滚把准备副本保留为 rolled-back-copy，而不递归销毁它。原始 legacy 始终保留。真实安装没有接入自动迁移启动钩子。

```powershell
dotnet run --project tools/Frost.Foundation -c Release -- migration-create <new-isolated-data-root>
# 将测试数据或授权的备份副本准备到输出的沙盒 legacy 目录，再运行：
dotnet run --project tools/Frost.Foundation -c Release -- migration-prepare <isolated-tool-data> <marked-sandbox>
dotnet run --project tools/Frost.Foundation -c Release -- migration-rollback <isolated-tool-data> <marked-sandbox>
```

本轮实测是独立文件系统副本；不等同于旧安装已迁移、真实进程 handover 或 Windows 原始指针恢复通过。Steam 原生写入当前一律阻止，避免把不完整 handover 当作安全商用升级。生产 D:\CURSORIUM、旧快捷方式、用户数据和原生注册表保持不变。
