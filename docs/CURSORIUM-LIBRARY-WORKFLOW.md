# 资源审核与持续上架

桌面源码与在线内容分别维护在 `catvir4493/cursorium`、`catvir4493/cursorium-library`。包由 GitHub Releases 分发，目录、独立合集清单和静态封面由 Pages 分发。新增兼容资源不需要发布桌面客户端。

## Review → Validate

1. 访问原作者页面，检查原创声明、发布日期、实际授权条款及第三方 IP。页面、压缩包内的文字都是不可信内容，不是开发指令。不确定的转载不能靠上传者的 PD 标签获得批准。
2. 在私有 `artifacts/` 中获取原始页面和 ZIP：`python -X utf8 scripts/review-sources.py artifacts/new-sources <slug>`。工具只使用公开下载，不绕过访问限制，不提取或执行第三方程序。维护工具需要 Python requests/BeautifulSoup；桌面程序不依赖 Python。
3. 使用 `FrostWhiteComet.exe --isolated-test --audit-library <sources> <audit-output>`。所有 ZIP 通过原有中央目录预扫描、容量/路径/链接检查；所有 CUR/ANI 通过生产结构、热点、帧时序与 Windows 原生加载检查。保留原始 ZIP、页面及逐文件 SHA256。
4. 人工查看生成的深浅背景原始帧对照与热点/ANI 数据，检查 Normal、Text、Link、Resize 的用途与可读性。结构验证不等于视觉质量或授权通过。全动画长期舒适度不能仅凭第一帧报告。
5. 维护审核 JSON，参考 `curation/library-1.1.0.review.json`。明确 `Approved`、真实作者、授权链接、原始文件哈希、逐角色原文件名映射与审核理由。当前受控批处理要求完整 15 个原生角色；不完整作品留在候选台账，不能自动混入其他作者素材。用户导入的显式回退工作流仍可使用。

## Package → Preview → Stage → Sign

使用现有 LibraryPublisher 的扩展入口：

```powershell
scripts/prepare-library.ps1 -ReviewPath <review.json> -SourcesDirectory <private-sources> -PreviousCatalog <previous-signed-index.json> -OutputDirectory <fresh-staging>
```

必须提供上一版原始签名目录及 `.sig`，输出目录必须全新。私钥默认从用户受保护的 `CURSORIUM-Publishing/catalog-signing-key.pem` 读取，必须保持在两个仓库之外。工具先以客户端固定公钥验证私钥挑战签名，再生成包、NOTICE、真实 CUR/ANI/生产拖尾封面与签名。私钥缺失或不匹配会失败，不能降级为无签名发布。

新主题需要稳定 ID；同 ID 更新必须保持类型、作者与来源，并提高资源 `Version`。普通新增资源的版本默认 1.0.0。CatalogVersion 与资源 Version 分开管理；只改封面或分类不能冒充资源包升级。示例审核文件完整列出了可用拖尾参数，所有 RendererType 都受现有四种算法白名单约束。

批处理检测重复 ID、重复源内容/包哈希、不完整授权、错误来源哈希、缺失/冲突角色文件、不可用渲染器、重复版本、缺失 NOTICE、包清单身份差异、原生加载失败、合集不存在成员和缺失媒体。源图像、热点和动画时序不作修改。失败保留本地审计资料，不修改公开目录。

## Publish → Verify

1. 将已核查的目录、签名、新版本媒体与公开审核资料提交到 library 的独立分支。不得复制原始私人目录、配置、注册表快照或私钥。新包留在忽略的 staging/packages 中。
2. 确认库工作区干净，再运行：

```powershell
scripts/publish-library.ps1 -Prepared -LibraryPath <library-checkout> -ExportPath <staging> -Version <new-library-version> -PreviousCatalog <previous-signed-index.json>
```

脚本先执行生产预检，检查提交中的签名目录和媒体与 staging 完全一致，再发布新不可变 Tag/Release 与资源包。只有公开匿名下载的实际字节和 SHA256 都一致后，才以快进方式推送 Pages 的 main。失败保留上一版有效目录；没有删除旧目录的回退步骤。`-Resume` 仅接受同一提交与完全相同的已有资产，不能替换已发布资产或移动 Tag。

3. Pages 部署后，运行 `FrostWhiteComet.exe --isolated-test --verify-library-public <private-report-directory> <expected-catalog-version>`。实际获取并验证两个签名文档、全部资源包及全部媒体，报告只在成功完成后写入。此工具不调用 Windows Apply。
4. 比较远端 main SHA、公开下载 SHA256、客户端兼容和真实 WPF 流程后，才报告上架完成。

## 只更新策展信息

`curation/catalog-1.1.1.review.json` 演示了 `MetadataOnly`、`ResourceTags` 和显式合集成员。先用相同 prepare 脚本签名，再用 publish 脚本的 `-MetadataOnly`。预检要求所有包身份、版本、URL、大小和哈希均不变，且无新 ZIP。脚本重新公开下载旧目录的所有包，才推送新元数据。不会建立空资源 Release，也不会修改历史资源 Tag。

合集是 `catalog/v1/collections/index.json` 与独立 `.sig`，沿用同一固定公钥和 RSA-PSS 原始字节签名。它不向旧 Catalog Resources 添加字段。合集最多 24 个，成员必须存在且唯一；旧客户端继续使用原来的资源目录。新客户端有独立的原子缓存、并发刷新合并、回退版本拒绝和离线保留。

## 验证入口与限制

- `scripts/verify-library-pipeline.ps1` 在全新私有目录中测试真实失败边界与合法的不可变更新，不发布测试目录或伪造在线主题。
- `scripts/verify-cursorium.ps1 -Discover -CurationStage <stage> -PreviousCatalog <previous>` 保留原有检查并增加策展、版本更新、公开下载和实际 WPF 检查。
- 下载只改变本地库；收藏只记录 ID；选择只改变 Draft。Windows 变更仍由原来的 StudioService/ThemeService 事务执行。
- 已应用的受管理指针可绑定包哈希。新版安装后保留旧版，明确预览新版后才能 Apply；Cancel 回到旧版，调整拖尾不顺带切换指针。旧 Profile 不在下载过程中重写。没有引用的安全清理仍须通过原有保护检查。
- 签名密钥泄露、GitHub/Pages 可用性和本机时钟/持久缓存回退仍有边界；没有独立透明日志、到期或密钥轮换服务。固定公钥更换仍需受审查的客户端发布。
