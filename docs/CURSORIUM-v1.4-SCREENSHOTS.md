# v1.4 实际 WPF 视图与前后对照

以下图片来自运行中的、绑定真实服务与数据的 WPF 窗口。v1.4 使用窗口自身视觉树在实际 96 DPI 下栅格捕获，包含真实控件、资源封面和状态；不是概念图、AI 图或静态原型。当前会话的桌面 BitBlt 返回了其他窗口的画面，那批输出未纳入公开证据。视觉树捕获不代表无遮挡桌面截图，也不包含独立的系统 Popup/原生标题栏表面。真实键盘、原生窗口和系统指针事务另有自动化检查。

|同尺寸对照 · 1280×830|v1.3.1|v1.4.0|
|---|---|---|
|Explore|[历史实际截图](images/v1.3.1/explore.png)|[29 项与合集](images/v1.4/explore.png)|
|详情|[原详情](images/v1.3.1/details.png)|[原主题保留](images/v1.4/details.png)|
|Cursors|[原本地库](images/v1.3.1/cursors.png)|[新增 Brown Cow 已应用](images/v1.4/new-cursors-applied.png)|
|Trails|[原拖尾库](images/v1.3.1/trails.png)|[新增 Ice Crystal 已应用](images/v1.4/new-trails-applied.png)|

主题/拖尾新增行展示实际新增资源，不能把内容差异误认为底层引擎被重写。旧基线来自上一版已归档证据。

![Explore](images/v1.4/explore.png)

|实际流程|图片|
|---|---|
|Cute & Cozy · 12 个真实成员|[合集](images/v1.4/collection-cute-cozy.png)|
|Minimal Workspace · 5 个成员|[合集](images/v1.4/collection-minimal-workspace.png)|
|Neon & Cyber · 6 个成员|[合集](images/v1.4/collection-neon-cyber.png)|
|Motion Trails · 14 个预设|[筛选](images/v1.4/motion-filter.png)|
|本地元数据搜索|[搜索](images/v1.4/search.png)|
|新增主题信息、15 角色与 ANI 临时预览|[Brown Cow](images/v1.4/new-brown-cow-details.png)|
|真实下载后的校验阶段，显示实际字节|[Verifying](images/v1.4/new-download-progress.png)|
|Installed · Open in Cursors|[下载完成](images/v1.4/new-download-installed.png)|
|本地选择 · Draft|[Brown Cow 草稿](images/v1.4/new-cursors-draft.png)|
|经原有事务 Apply · Applied|[Brown Cow 已应用](images/v1.4/new-cursors-applied.png)|
|新拖尾只进入草稿|[Ice Crystal 草稿](images/v1.4/new-trails-draft.png)|
|新拖尾已应用，指针独立保留|[Ice Crystal 已应用](images/v1.4/new-trails-applied.png)|
|角色 ANI 与原始时序|[原主题 ANI](images/v1.4/ani-preview.png)|
|观察放大，不改变系统热点/时序|[4×](images/v1.4/observation-4x.png)|
|Mix Studio / Settings|[组合](images/v1.4/mix-studio.png)、[设置](images/v1.4/settings.png)|
|850×520 · 合集改为文字入口，内容可滚动|[紧凑](images/v1.4/compact-explore.png)|
|850×520 · 详情主操作可达|[紧凑详情](images/v1.4/compact-details.png)|
|1680×980 · 多列资源|[宽屏](images/v1.4/wide-explore.png)|

公开资源包当前均为 1.0.0，没有制造一个公开的假升级主题来截图。Update Available、失败保留、同 ID 新旧版本预览/Cancel/Apply 用明确的隔离升级夹具验证；它不进入 Explore 或公开目录，因此不提供冒充线上升级的截图。
