using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WindowSizerWinUI;

internal static class UiText
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["窗口调整"] = "Resize windows",
        ["精确设置任务栏中应用窗口的尺寸与位置。"] = "Set the size and position of windows shown on the taskbar.",
        ["尺寸范围"] = "Size area",
        ["整个窗口"] = "Whole window",
        ["包含标题栏与边框"] = "Includes title bar and borders",
        ["内容区域"] = "Content area",
        ["不包含标题栏与边框"] = "Excludes title bar and borders",
        ["目标窗口"] = "Target window",
        ["仅显示任务栏中的窗口；拖动表头右侧的分隔线可调整列宽。"] = "Only taskbar windows are shown. Drag the header dividers to resize columns.",
        ["刷新"] = "Refresh",
        ["选择本工具"] = "Select this app",
        ["3 秒后捕获"] = "Capture in 3 seconds",
        ["窗口标题"] = "Window title",
        ["程序"] = "App",
        ["当前尺寸"] = "Current size",
        ["拖动调整列宽"] = "Drag to resize column",
        ["尚未选择窗口"] = "No window selected",
        ["目标分辨率"] = "Target resolution",
        ["长宽比"] = "Aspect ratio",
        ["自定义比例"] = "Custom ratio",
        ["常用分辨率"] = "Common resolutions",
        ["宽 × 高（像素）"] = "Width × height (pixels)",
        ["应用尺寸后居中"] = "Center after resizing",
        ["仅居中窗口"] = "Center window",
        ["应用尺寸"] = "Apply size",
        ["设置"] = "Settings",
        ["深色模式"] = "Dark mode",
        ["窗口尺寸调整器"] = "Window Sizer",
        ["为已打开的 Windows 应用设置指定的窗口尺寸。"] = "Set an exact size for any open Windows app.",
        ["使用 WinUI 3 与 Windows App SDK 构建。"] = "Built with WinUI 3 and Windows App SDK.",
        ["文档"] = "Documentation",
        ["GitHub 发布"] = "GitHub Releases",
        ["报告 bug"] = "Report a bug",
        ["申请功能"] = "Request a feature",
        ["外观与语言"] = "Appearance and language",
        ["界面语言"] = "Display language",
        ["选择应用中使用的语言；更改立即生效。"] = "Choose the language used by the app. Changes take effect immediately.",
        ["系统默认"] = "System default",
        ["简体中文"] = "Simplified Chinese",
        ["英语"] = "English",
        ["为应用使用深色外观。"] = "Use a dark appearance for the app.",
        ["版本和更新"] = "Version and updates",
        ["从 GitHub Releases 查询最新发布版本。"] = "Check GitHub Releases for the latest version.",
        ["检查更新"] = "Check for updates",
        ["查看并下载新版本"] = "View and download update",
        ["发布说明"] = "Release notes"
    };

    public static string Translate(string chinese) =>
        LanguageSettings.IsEnglish && English.TryGetValue(chinese, out string? english)
            ? english : chinese;

    public static void Apply(DependencyObject root)
    {
        if (!LanguageSettings.IsEnglish) return;

        if (root is TextBlock text)
            text.Text = Translate(text.Text);
        if (root is ContentControl content && content.Content is string label)
            content.Content = Translate(label);
        if (root is ToggleSwitch toggle && toggle.Header is string header)
            toggle.Header = Translate(header);

        object tooltip = ToolTipService.GetToolTip(root);
        if (tooltip is string hint)
            ToolTipService.SetToolTip(root, Translate(hint));

        if (root is ComboBox combo)
            foreach (object item in combo.Items)
                if (item is DependencyObject element) Apply(element);

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            Apply(VisualTreeHelper.GetChild(root, index));
    }
}
