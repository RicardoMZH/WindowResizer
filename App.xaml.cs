using Microsoft.UI.Xaml;

namespace WindowSizerWinUI;

public partial class App : Application
{
    private Window? _window;
    internal static MainWindow? MainWindowInstance { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindowInstance = new MainWindow();
        _window = MainWindowInstance;
        _window.Activate();
    }
}
