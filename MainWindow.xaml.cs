using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace WindowSizerWinUI;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _sizeSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private SizeInt32 _lastRestoredSize;
    internal nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
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
        };
        NavView.SelectedItem = ResizeNav;
        RootFrame.Navigate(typeof(MainPage));
    }

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
        if (args.SelectedItemContainer?.Tag?.ToString() == "about")
        {
            if (RootFrame.CurrentSourcePageType != typeof(AboutPage))
                RootFrame.Navigate(typeof(AboutPage), null, new EntranceNavigationTransitionInfo());
        }
        else if (RootFrame.CurrentSourcePageType != typeof(MainPage))
            RootFrame.Navigate(typeof(MainPage), null, new EntranceNavigationTransitionInfo());
    }

    private void DarkModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        RootGrid.RequestedTheme = DarkModeToggle.IsOn ? ElementTheme.Dark : ElementTheme.Light;
    }

}
