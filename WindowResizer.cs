using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowSizerWinUI;

public sealed class WindowItem : INotifyPropertyChanged
{
    private string _sizeText;
    public nint Handle { get; }
    public string Title { get; }
    public string ProcessName { get; }
    public string SizeText
    {
        get => _sizeText;
        set { if (_sizeText != value) { _sizeText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeText))); } }
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public WindowItem(nint handle, string title, string processName, string sizeText)
    {
        Handle = handle;
        Title = title;
        ProcessName = processName;
        _sizeText = sizeText;
    }

    public override string ToString() => Title;
}

public readonly record struct WindowSizes(int OuterWidth, int OuterHeight, int ClientWidth, int ClientHeight);
public readonly record struct ResizeResult(bool IsExact, string Message, WindowSizes? Sizes);

internal static class WindowResizer
{
    private delegate bool EnumWindowsCallback(nint hwnd, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int DwmGetWindowBounds(nint hwnd, int attribute, out Rect value, int size);

    public static List<WindowItem> Enumerate(nint own)
    {
        List<WindowItem> result = new();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || IsCloaked(hwnd) || (hwnd != own && !IsTaskbarWindow(hwnd))) return true;
            int length = GetWindowTextLength(hwnd);
            if (length == 0) return true;
            StringBuilder title = new(length + 1);
            GetWindowText(hwnd, title, title.Capacity);
            string name = title.ToString().Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (name.Length == 0 || name == "Program Manager") return true;
            if (!GetWindowRect(hwnd, out Rect rect)) return true;
            GetWindowThreadProcessId(hwnd, out uint pid);
            string processName = "未知程序";
            try { using Process process = Process.GetProcessById((int)pid); processName = process.ProcessName; }
            catch { }
            string size = IsIconic(hwnd) ? "最小化" : $"{rect.Right - rect.Left}×{rect.Bottom - rect.Top}";
            result.Add(new WindowItem(hwnd, name, processName, size));
            return true;
        }, nint.Zero);
        return result;
    }

    public static bool TryGetSizes(nint hwnd, out WindowSizes sizes)
    {
        sizes = default;
        if (!IsWindow(hwnd) || !GetWindowRect(hwnd, out Rect outer) || !GetClientRect(hwnd, out Rect client)) return false;
        sizes = new WindowSizes(outer.Right - outer.Left, outer.Bottom - outer.Top,
                                client.Right - client.Left, client.Bottom - client.Top);
        return true;
    }

    public static ResizeResult Resize(nint hwnd, int width, int height, bool clientArea)
    {
        if (!IsWindow(hwnd)) return new(false, "目标窗口已关闭。请刷新列表。", null);
        if (IsIconic(hwnd) || IsZoomed(hwnd)) ShowWindow(hwnd, 9); // SW_RESTORE
        if (!TryGetSizes(hwnd, out WindowSizes before)) return new(false, "无法读取目标窗口的尺寸。", null);
        int outerWidth = clientArea ? width + before.OuterWidth - before.ClientWidth : width;
        int outerHeight = clientArea ? height + before.OuterHeight - before.ClientHeight : height;
        if (!Valid(outerWidth, outerHeight)) return new(false, "目标尺寸超出 Windows 窗口可用范围。", before);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            // SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE. Do not set SWP_NOSIZE.
            if (!SetWindowPos(hwnd, nint.Zero, 0, 0, outerWidth, outerHeight, 0x0002 | 0x0004 | 0x0010))
                return new(false, $"调整失败（Windows 错误 {Marshal.GetLastWin32Error()}）。目标程序若以管理员权限运行，本工具也需要管理员权限。", null);
            if (!TryGetSizes(hwnd, out WindowSizes actual)) break;
            int actualWidth = clientArea ? actual.ClientWidth : actual.OuterWidth;
            int actualHeight = clientArea ? actual.ClientHeight : actual.OuterHeight;
            if (actualWidth == width && actualHeight == height) break;
            if (!clientArea) break;
            outerWidth += width - actualWidth;
            outerHeight += height - actualHeight;
            if (!Valid(outerWidth, outerHeight)) break;
        }

        if (!TryGetSizes(hwnd, out WindowSizes final)) return new(false, "调整后无法读取目标窗口的尺寸。", null);
        int finalWidth = clientArea ? final.ClientWidth : final.OuterWidth;
        int finalHeight = clientArea ? final.ClientHeight : final.OuterHeight;
        bool exact = finalWidth == width && finalHeight == height;
        string scope = clientArea ? "内容区域" : "整个窗口";
        string message = exact
            ? $"已将{scope}设置为 {finalWidth}×{finalHeight}。"
            : $"目标程序限制了窗口尺寸；{scope}实际为 {finalWidth}×{finalHeight}，目标为 {width}×{height}。";
        return new(exact, message, final);
    }

    public static ResizeResult Center(nint hwnd)
    {
        if (!IsWindow(hwnd)) return new(false, "目标窗口已关闭。请刷新列表。", null);
        if (IsIconic(hwnd) || IsZoomed(hwnd)) ShowWindow(hwnd, 9);
        if (!GetWindowRect(hwnd, out Rect outer)) return new(false, "无法读取目标窗口的位置。", null);

        nint monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
        MonitorInfo info = new() { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == nint.Zero || !GetMonitorInfo(monitor, ref info))
            return new(false, "无法确定目标窗口所在的显示器。", null);

        // The visible frame excludes Windows' invisible resize border.
        Rect visible = outer;
        if (DwmGetWindowBounds(hwnd, 9, out Rect frame, Marshal.SizeOf<Rect>()) == 0)
            visible = frame;
        int screenWidth = info.Monitor.Right - info.Monitor.Left;
        int screenHeight = info.Monitor.Bottom - info.Monitor.Top;
        int left = info.Monitor.Left + (screenWidth - (visible.Right - visible.Left)) / 2;
        int top = info.Monitor.Top + (screenHeight - (visible.Bottom - visible.Top)) / 2;
        int x = outer.Left + left - visible.Left;
        int y = outer.Top + top - visible.Top;
        if (!SetWindowPos(hwnd, nint.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010))
            return new(false, $"居中失败（Windows 错误 {Marshal.GetLastWin32Error()}）。", null);
        TryGetSizes(hwnd, out WindowSizes sizes);
        return new(true, "窗口已居于所在显示器的屏幕正中央。", sizes);
    }

    public static void Minimize(nint hwnd) => ShowWindow(hwnd, 6);
    public static void Restore(nint hwnd) => ShowWindow(hwnd, 9);
    public static nint GetForeground() => GetForegroundWindow();
    private static bool Valid(int width, int height) => width is >= 1 and <= 32767 && height is >= 1 and <= 32767;

    private static bool IsCloaked(nint hwnd)
    {
        try { return DwmGetWindowAttribute(hwnd, 14, out int cloak, sizeof(int)) == 0 && cloak != 0; }
        catch { return false; }
    }

    private static bool IsTaskbarWindow(nint hwnd)
    {
        long style = GetWindowLongPtr(hwnd, -20).ToInt64(); // GWL_EXSTYLE
        if ((style & 0x80) != 0) return false; // WS_EX_TOOLWINDOW
        if ((style & 0x40000) != 0) return true; // WS_EX_APPWINDOW
        if ((style & 0x08000000) != 0) return false; // WS_EX_NOACTIVATE
        return GetWindow(hwnd, 4) == nint.Zero; // GW_OWNER
    }
}
