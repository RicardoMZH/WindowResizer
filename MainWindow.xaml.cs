using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace WindowSizerWinUI;

public sealed partial class MainWindow : Window
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(
        string fileName, int iconIndex, out nint largeIcon, out nint smallIcon, uint count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint window, uint message, nint parameter, nint value);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);

    private readonly DispatcherTimer _sizeSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private SizeInt32 _lastRestoredSize;
    private nint _largeIcon;
    private nint _smallIcon;
    internal nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SetWindowIcons();
        ApplyLanguage();
        ApplyTheme();
        AppWindow.Resize(WindowLayoutStore.Load());
        _lastRestoredSize = AppWindow.Size;
        _sizeSaveTimer.Tick += (_, _) =>
        {
            _sizeSaveTimer.Stop();
            WindowLayoutStore.Save(_lastRestoredSize);
        };
        AppWindow.Changed += AppWindow_Changed;
        Closed += (_, _) =>
        {
            _sizeSaveTimer.Stop();
            WindowLayoutStore.Save(_lastRestoredSize);
            if (_largeIcon != nint.Zero) DestroyIcon(_largeIcon);
            if (_smallIcon != nint.Zero) DestroyIcon(_smallIcon);
        };
        NavView.SelectedItem = ResizeNav;
        RootFrame.Navigate(typeof(MainPage));
    }

    private void SetWindowIcons()
    {
        string? executable = Environment.ProcessPath;
        if (executable is null ||
            ExtractIconEx(executable, 0, out _largeIcon, out _smallIcon, 1) == 0)
            return;

        nint window = WindowHandle;
        if (_largeIcon != nint.Zero)
        {
            IconId iconId = Win32Interop.GetIconIdFromIcon(_largeIcon);
            AppWindow.SetIcon(iconId);
            AppWindow.SetTaskbarIcon(iconId);
            SendMessage(window, 0x0080, 1, _largeIcon); // WM_SETICON, ICON_BIG
        }
        if (_smallIcon != nint.Zero)
            SendMessage(window, 0x0080, 0, _smallIcon); // WM_SETICON, ICON_SMALL
    }

    internal void ApplyLanguage()
    {
        string title = LanguageSettings.Text("窗口尺寸调整器", "Window Sizer");
        Title = title;
        AppTitleBar.Title = title;
        ResizeNav.Content = LanguageSettings.Text("窗口调整", "Resize windows");
        SettingsNav.Content = LanguageSettings.Text("设置", "Settings");
    }

    internal void ApplyTheme() => RootGrid.RequestedTheme =
        LanguageSettings.DarkMode ? ElementTheme.Dark : ElementTheme.Light;

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange ||
            sender.Presenter is not OverlappedPresenter presenter ||
            presenter.State != OverlappedPresenterState.Restored) return;
        SizeInt32 size = sender.Size;
        if (size.Width < 200 || size.Height < 150) return;
        _lastRestoredSize = size;
        _sizeSaveTimer.Stop();
        _sizeSaveTimer.Start();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (RootFrame is null) return;
        if (args.SelectedItemContainer?.Tag?.ToString() == "settings")
        {
            if (RootFrame.CurrentSourcePageType != typeof(SettingsPage))
                RootFrame.Navigate(typeof(SettingsPage), null, new EntranceNavigationTransitionInfo());
        }
        else if (RootFrame.CurrentSourcePageType != typeof(MainPage))
            RootFrame.Navigate(typeof(MainPage), null, new EntranceNavigationTransitionInfo());
    }

}
