# Library v2 权利扩展

v1 的 index.json、index.json.sig、六个合集、封面、29 个包 URL/版本/ID/哈希保持原字节。新入口 `catalog/v2/rights.json` 与 `rights.json.sig` 独立采用既有 3072-bit RSA-PSS/SHA256 固定公钥；SchemaVersion=2，权利记录 revision 首版为 1.0.0，不等于 CatalogVersion 升级。公开结构见 [Schema](../library-v2-rights.schema.json)。

包含 StableId、Version、Author、SourceUrl、License、LicenseEvidence（URL/种类/内容摘要）、CommercialClearance、RedistributionPermission、AttributionRequired、ReviewStatus、MinimumClientVersion、DistributionChannel、WithdrawalStatus；额外 Scope 和 PackageSha256 区分在线包与历史无版本内置资源。`CatalogSha256` 绑定精确签名 v1 字节，每个在线记录绑定实际作者、来源、资源版本与包 SHA256，不仅按显示名授权。

客户端先验证 v1 与 v2 两份签名，再做有界、禁止重复字段/未知字段的解析。签名证明被官方密钥审核过的记录字节，不证明作者权利声明一定真实。签名私钥只由本机既有受保护位置读入内存，没有写进仓库、安装器、日志或 Steam 负载。

RemoteRightsService 保留独立原子缓存，启动重新验签，拒绝倒退 revision/同版换字节及未知缓存 schema 覆写；刷新合并并发。Community 缺少 v2 时继续原 v1 工作流；Steam 新获取要求当前有效签名且记录显式 Approved/Reviewed、允许分发、含 Steam、非 Withdrawn。未确认资料默认 Needs Review。既有下载状态、收藏、Draft/Applied、包安装与 Windows Apply 仍各自独立。

撤回阻止新获取，不删除已安装的 CUR/ANI、不可变目录、Profile、原始备份或历史 Release。v1 老客户端不认识新字段，因此无法声称 v2 能追溯撤销旧客户端的下载能力；本轮所有 WithdrawalStatus 均为 Active。离线缓存用于已知权利展示和本地使用，Steam 新获取不能用失败刷新的旧缓存冒充当前授权。首次信任仍无独立透明日志/有效期撤销服务，密钥轮换和更强反回放持久化是后续商业安全项。

流程沿用原工具：

```powershell
python scripts/audit-commercial-library.py --catalog <signed-v1-index> --packages <retained-packages-a> --packages <retained-packages-b> --fetch-record <fresh-page-hashes> --library-root <library-checkout> --output <new-rights-stage>
dotnet run --project tools/Frost.Foundation -c Release -- sign-rights <isolated-tool-data> <rights-stage> <protected-key-path>
dotnet run --project tools/Frost.Foundation -c Release -- verify-rights <isolated-report> <rights-stage>
# 先审查并提交 library 独立分支中的新 metadata；脚本不替代人工权利判断
scripts/publish-library.ps1 -RightsOnly -LibraryPath <reviewed-library-checkout> -ExportPath <rights-stage> -Version 1.0.0
```

RightsOnly 只允许 v2/审核台账/说明变更；先核对公开签名 v1 及六个合集，再普通 fast-forward 推送，轮询并验证公开 v2 字节，最后再次验证 v1。没有重打包旧 ZIP、修改历史 tag 或创建平行签名密钥。未来兼容的资源与权利上架可以独立更新，无需更换桌面二进制；v1 catalog 字节变化时必须同时准备重新绑定的 v2 文档。
