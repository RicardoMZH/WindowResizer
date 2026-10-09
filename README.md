# 窗口尺寸调整器 · WinUI 3

这是桌面版窗口尺寸调整器的当前源码。界面使用 WinUI 3、Windows App SDK、可透出桌面色彩的毛玻璃背景、原生导航和状态提示，并提供深色模式。背景由系统 Desktop Acrylic 提供模糊效果，半透明卡片和导航栏让效果可见。图标由 `tools/generate_icon.py` 绘制，生成的素材已包含在 `Assets` 中。

## 功能

- 按任务栏窗口规则列出已打开的应用窗口，包括本工具自身；可刷新列表、直接选择本工具或在 3 秒后捕获前台窗口。隐藏的托盘窗口、工具窗口和通常不显示在任务栏的窗口会被排除。
- 目标窗口使用可调列宽的表格，表头与内容居中、行间以虚线分隔；重复点击已选行可取消选择，刷新会清空选择。列宽保存在 `%LOCALAPPDATA%\WindowSizerWinUI\columns.json`，再次启动时恢复。
- 程序首次启动时使用 1600×900 的窗口尺寸；之后保存普通窗口状态下的最近尺寸到 `%LOCALAPPDATA%\WindowSizerWinUI\window.json`，下次启动自动恢复。最大化或最小化不会覆盖已保存的普通窗口尺寸。
- 右侧内容区保持至少 720 个 WinUI 有效像素的宽度；软件缩窄时出现横向滚动。页面边距和各区域间距固定，目标窗口表格会随右侧内容区扩展，默认三列完整显示。
- 选择 4:3、16:9、16:10 的常用分辨率，或手动输入并锁定所选长宽比；自定义比例可独立修改宽和高。
- 可将尺寸应用到整个窗口，或仅应用到内容区域（不含标题栏与边框）。
- 可将目标窗口居中在其所在显示器的完整屏幕区域，也可在应用尺寸后自动居中。
- 调整后读取实际尺寸；目标程序限制尺寸时显示实际结果。
- “关于”页可从 GitHub Releases 检查更新；发现新版本时可打开发布页面下载。

WGC/DXGI 用于画面捕获与渲染，本工具的尺寸调整功能不需要捕获画面。

## 运行与构建

压缩版交付物是单个 EXE，面向 Windows 10 2004 及以上版本的 x64 设备。支持的系统会呈现亚克力背景；旧系统会使用系统回退效果。外层原生启动程序允许任意修改 EXE 文件名，包括中文和空格。它从内嵌资源提取自包含的 WinUI 负载，始终以固定的 `WindowSizerWinUI.exe` 名称启动，因此 WinUI 资源定位不受外层文件名影响。运行时会在用户本地数据目录提取负载及 .NET 组件，最后一个同版本实例关闭后自动清理该版本的运行缓存。

重新构建需要 .NET 10 SDK、Windows SDK、Visual Studio C++ Build Tools 和 NuGet 依赖。Windows App SDK 包版本固定为 2.5.1。`build.cmd` 默认构建压缩版，也可以指定三种模式：

```cmd
build.cmd -Mode Uncompressed
build.cmd -Mode Compressed
build.cmd -Mode FrameworkDependent
```

- `Uncompressed`：单个可改名 EXE，内嵌未压缩的自包含负载。
- `Compressed`：单个可改名 EXE，内嵌压缩的自包含负载，是推荐交付版本。
- `FrameworkDependent`：输出 ZIP，包含小体积 EXE 和同目录必须文件；目标电脑需另行安装 .NET 10 与对应的 Windows App SDK 运行环境。

默认产物位于源码目录同级的 `WindowSizerReleases\<Mode>`，各模式复用各自的中间构建目录，避免每次构建额外留一套大型依赖。可以通过 `-OutputRoot <路径>` 和 `-BuildRoot <路径>` 改变产物及中间文件目录；已有 NuGet 还原结果时可使用 `-NoRestore`。单文件运行缓存位于 `%LOCALAPPDATA%\WindowSizerWinUI\Runtime\<负载哈希>`，按版本隔离并在程序退出后自动清理。

## GitHub 发布

将 `vX.X.X` 格式的版本标签推送到 GitHub 后，GitHub Actions 使用 `build.cmd` 的三种模式构建，并在对应的 Release 中发布三个文件：`WindowSizerWinUI_Uncompressed.exe`、`WindowSizerWinUI_Compressed.exe` 和 `WindowSizerWinUI_FrameworkDependent.zip`。其他格式的标签不会构建发布文件。

验证核心尺寸逻辑：

```powershell
dotnet run --project tests/CoreSmoke/CoreSmoke.csproj -c Release
```

自动测试会短暂创建测试窗口，验证任务栏窗口筛选、自身枚举、整个窗口 800×600、内容区域 1000×700 和屏幕居中。

窗口是否显示在任务栏由目标应用和 Windows Shell 决定。本工具按公开的窗口可见性、所有者及扩展样式规则筛选，少数采用特殊窗口机制的应用可能与任务栏实际显示不完全一致。
