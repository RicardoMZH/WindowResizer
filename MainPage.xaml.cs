using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;

namespace WindowSizerWinUI;

public sealed partial class MainPage : Page
{
    private readonly ObservableCollection<WindowItem> _windows = new();
    private readonly HashSet<Grid> _rowGrids = new();
    private readonly HashSet<Border> _rowSurfaces = new();
    private readonly HashSet<Line> _rowSeparators = new();
    private readonly double[] _columnWidths = ColumnLayoutStore.Load();
    private readonly double[] _displayWidths = new double[3];
    private bool _updatingInputs;
    private WindowItem? _selectedAtPointerPress;

    public MainPage()
    {
        InitializeComponent();
        WindowList.ItemsSource = _windows;
        ApplyColumnWidths();
        RatioBox.SelectedIndex = 1;
        Loaded += (_, _) =>
        {
            UiText.Apply(this);
            RefreshWindowList(nint.Zero);
        };
    }

    private void PageScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ContentRoot is null) return;
        // Keep both side paddings fixed; scroll the entire right pane below its minimum width.
        ContentRoot.Width = Math.Max(720, e.NewSize.Width - 68);
    }

    private void TableViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (TableLayout is not null) ApplyColumnWidths();
    }

    private void RefreshWindowList(nint preferred)
    {
        nint own = App.MainWindowInstance?.WindowHandle ?? nint.Zero;
        _selectedAtPointerPress = null;
        WindowList.SelectedItem = null;
        _windows.Clear();
        foreach (WindowItem item in WindowResizer.Enumerate(own))
            _windows.Add(item);

        WindowList.SelectedItem = _windows.FirstOrDefault(item => item.Handle == preferred);
        UpdateSelectedInfo();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshWindowList(nint.Zero);

    private void SelectSelfButton_Click(object sender, RoutedEventArgs e)
    {
        nint own = App.MainWindowInstance?.WindowHandle ?? nint.Zero;
        RefreshWindowList(own);
        if (WindowList.SelectedItem is null)
            ShowStatus(LanguageSettings.Text("无法找到本工具的窗口。", "Could not find this app's window."),
                InfoBarSeverity.Warning);
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        MainWindow? main = App.MainWindowInstance;
        if (main is null) return;
        CaptureButton.IsEnabled = false;
        ShowStatus(LanguageSettings.Text("请在 3 秒内切换到目标窗口…",
            "Switch to the target window within 3 seconds…"), InfoBarSeverity.Informational);
        try
        {
            WindowResizer.Minimize(main.WindowHandle);
            await Task.Delay(3000);
            nint target = WindowResizer.GetForeground();
            WindowResizer.Restore(main.WindowHandle);
            main.Activate();
            RefreshWindowList(target);
            ShowStatus(WindowList.SelectedItem is WindowItem
                ? LanguageSettings.Text("已捕获目标窗口。", "Target window captured.")
                : LanguageSettings.Text("未找到目标窗口。请确认它在任务栏中显示。",
                    "Target window not found. Make sure it appears on the taskbar."),
                WindowList.SelectedItem is WindowItem ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        }
        finally { CaptureButton.IsEnabled = true; }
    }

    private void WindowList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedInfo is null) return;
        UpdateSelectedInfo();
        foreach (Border row in _rowSurfaces) UpdateRowStyle(row);
    }

    private void RowSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _selectedAtPointerPress = sender is Border row && ReferenceEquals(WindowList.SelectedItem, row.DataContext)
            ? row.DataContext as WindowItem : null;
    }

    private void WindowList_ItemClick(object sender, ItemClickEventArgs e)
    {
        WindowItem? clickedAgain = ReferenceEquals(e.ClickedItem, _selectedAtPointerPress)
            ? _selectedAtPointerPress : null;
        _selectedAtPointerPress = null;
        if (clickedAgain is null) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ReferenceEquals(WindowList.SelectedItem, clickedAgain))
                WindowList.SelectedItem = null;
        });
    }

    private void UpdateSelectedInfo()
    {
        if (WindowList.SelectedItem is not WindowItem selected)
        {
            SelectedInfo.Text = LanguageSettings.Text("尚未选择窗口", "No window selected");
            return;
        }
        if (WindowResizer.TryGetSizes(selected.Handle, out WindowSizes sizes))
            SelectedInfo.Text = LanguageSettings.Text(
                $"已选：{selected.Title}    ·    窗口 {sizes.OuterWidth}×{sizes.OuterHeight}    ·    内容区域 {sizes.ClientWidth}×{sizes.ClientHeight}",
                $"Selected: {selected.Title}    ·    Window {sizes.OuterWidth}×{sizes.OuterHeight}    ·    Content {sizes.ClientWidth}×{sizes.ClientHeight}");
        else
            SelectedInfo.Text = LanguageSettings.Text(
                $"已选：{selected.Title}    ·    无法读取尺寸",
                $"Selected: {selected.Title}    ·    Size unavailable");
    }

    private void RowSurface_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border row)
        {
            _rowSurfaces.Add(row);
            UpdateRowStyle(row);
        }
    }

    private void RowSurface_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border row) _rowSurfaces.Remove(row);
    }

    private void UpdateRowStyle(Border row)
    {
        if (row.DataContext is not WindowItem item) return;
        string key = ReferenceEquals(WindowList.SelectedItem, item) ? "AppRowSelectedStyle"
            : "AppRowNormalStyle";
        row.Style = (Style)Application.Current.Resources[key];
    }

    private void WindowRowGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Grid row)
        {
            _rowGrids.Add(row);
            ApplyGridWidths(row);
        }
    }

    private void WindowRowGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Grid row) _rowGrids.Remove(row);
    }

    private void RowSeparator_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Line separator)
        {
            _rowSeparators.Add(separator);
            separator.X2 = _displayWidths.Sum();
        }
    }

    private void RowSeparator_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Line separator) _rowSeparators.Remove(separator);
    }

    private void HeaderThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb thumb || !int.TryParse(thumb.Tag?.ToString(), out int index) ||
            index < 0 || index >= _columnWidths.Length) return;
        double[] adjusted = (double[])_displayWidths.Clone();
        if (index < 2)
        {
            double leftMinimum = index == 0 ? 180 : 100;
            double delta = Math.Clamp(e.HorizontalChange,
                leftMinimum - adjusted[index], adjusted[index + 1] - 100);
            adjusted[index] += delta;
            adjusted[index + 1] -= delta;
        }
        else
            adjusted[index] = Math.Clamp(adjusted[index] + e.HorizontalChange, 100, 10000);
        Array.Copy(adjusted, _columnWidths, 3);
        ApplyColumnWidths();
    }

    private void HeaderThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!ColumnLayoutStore.Save(_columnWidths))
            ShowStatus(LanguageSettings.Text("列宽已调整，但无法保存到本机设置。",
                "Column widths changed, but could not be saved."), InfoBarSeverity.Warning);
    }

    private void ApplyColumnWidths()
    {
        Array.Copy(_columnWidths, _displayWidths, 3);
        double viewport = Math.Max(0, TableViewport.ActualWidth - 2);
        _displayWidths[0] += Math.Max(0, viewport - _columnWidths.Sum());
        double total = _displayWidths.Sum();
        TableLayout.Width = total;
        TableHeader.Width = total;
        WindowList.Width = total;
        SelectedFooter.Width = total;
        ApplyGridWidths(TableHeader);
        foreach (Grid row in _rowGrids) ApplyGridWidths(row);
        foreach (Line separator in _rowSeparators) separator.X2 = total;
    }

    private void ApplyGridWidths(Grid grid)
    {
        grid.Width = _displayWidths.Sum();
        for (int i = 0; i < _displayWidths.Length; i++)
            grid.ColumnDefinitions[i].Width = new GridLength(_displayWidths[i]);
    }

    private void RatioBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetBox is null || RatioHint is null) return;
        _updatingInputs = true;
        PresetBox.Items.Clear();
        PresetBox.Items.Add(new ComboBoxItem
        {
            Content = LanguageSettings.Text("手动输入", "Enter manually")
        });
        foreach ((int width, int height) in GetPresets())
            PresetBox.Items.Add(new ComboBoxItem
            {
                Content = $"{width} × {height}",
                Tag = $"{width},{height}"
            });
        bool custom = RatioBox.SelectedIndex == 3;
        PresetBox.SelectedIndex = custom ? 0 : 1;
        RatioHint.Text = custom
            ? LanguageSettings.Text("自定义比例：宽和高可独立输入。",
                "Custom ratio: enter width and height independently.")
            : LanguageSettings.Text(
                $"已锁定 {((ComboBoxItem)RatioBox.SelectedItem).Content}；修改宽或高会自动更新另一项。",
                $"Ratio locked to {((ComboBoxItem)RatioBox.SelectedItem).Content}; changing one dimension updates the other.");
        if (!custom && PresetBox.SelectedItem is ComboBoxItem option)
            ApplyPreset(option);
        _updatingInputs = false;
    }

    private IEnumerable<(int Width, int Height)> GetPresets() => RatioBox.SelectedIndex switch
    {
        0 => [(800, 600), (1024, 768), (1280, 960), (1600, 1200), (1920, 1440)],
        1 => [(1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160)],
        2 => [(1280, 800), (1440, 900), (1680, 1050), (1920, 1200), (2560, 1600)],
        _ => []
    };

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingInputs || PresetBox.SelectedItem is not ComboBoxItem option) return;
        _updatingInputs = true;
        ApplyPreset(option);
        _updatingInputs = false;
    }

    private void ApplyPreset(ComboBoxItem option)
    {
        if (option.Tag is not string value) return;
        string[] parts = value.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
        {
            WidthBox.Value = width;
            HeightBox.Value = height;
        }
    }

    private void WidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        UpdatePairedDimension(true);

    private void HeightBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        UpdatePairedDimension(false);

    private void UpdatePairedDimension(bool widthChanged)
    {
        if (_updatingInputs || RatioBox is null || PresetBox is null || WidthBox is null || HeightBox is null) return;
        NumberBox source = widthChanged ? WidthBox : HeightBox;
        NumberBox target = widthChanged ? HeightBox : WidthBox;
        if (!double.IsFinite(source.Value) || source.Value < 1 || source.Value > 32767) return;

        _updatingInputs = true;
        if (PresetBox.Items.Count > 0) PresetBox.SelectedIndex = 0;
        (int x, int y) = RatioBox.SelectedIndex switch
        {
            0 => (4, 3),
            1 => (16, 9),
            2 => (16, 10),
            _ => (0, 0)
        };
        if (x != 0)
        {
            int other = (int)Math.Round(source.Value * (widthChanged ? (double)y / x : (double)x / y),
                                        MidpointRounding.AwayFromZero);
            if (other > 32767)
            {
                other = 32767;
                source.Value = Math.Round(other * (widthChanged ? (double)x / y : (double)y / x),
                                          MidpointRounding.AwayFromZero);
            }
            target.Value = Math.Max(1, other);
        }
        _updatingInputs = false;
    }

    private void CenterButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowList.SelectedItem is not WindowItem selected)
        {
            ShowStatus(LanguageSettings.Text("请先选择目标窗口。", "Select a target window first."),
                InfoBarSeverity.Warning);
            return;
        }
        ResizeResult result = WindowResizer.Center(selected.Handle);
        UpdateSelectedInfo();
        ShowStatus(result.Message, result.IsExact ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowList.SelectedItem is not WindowItem selected)
        {
            ShowStatus(LanguageSettings.Text("请先选择目标窗口。", "Select a target window first."),
                InfoBarSeverity.Warning);
            return;
        }
        if (!double.IsFinite(WidthBox.Value) || !double.IsFinite(HeightBox.Value) ||
            WidthBox.Value < 1 || HeightBox.Value < 1 ||
            WidthBox.Value > 32767 || HeightBox.Value > 32767 ||
            WidthBox.Value % 1 != 0 || HeightBox.Value % 1 != 0)
        {
            ShowStatus(LanguageSettings.Text("请输入 1 到 32767 之间的整数宽高。",
                "Enter whole-number dimensions between 1 and 32767."), InfoBarSeverity.Warning);
            return;
        }

        ResizeResult result = WindowResizer.Resize(selected.Handle, (int)WidthBox.Value, (int)HeightBox.Value,
                                                    ClientMode.IsChecked == true);
        if (result.Sizes is WindowSizes sizes)
        {
            selected.SizeText = $"{sizes.OuterWidth}×{sizes.OuterHeight}";
            UpdateSelectedInfo();
            if (CenterAfterApply.IsChecked == true)
            {
                ResizeResult centered = WindowResizer.Center(selected.Handle);
                result = new ResizeResult(result.IsExact && centered.IsExact,
                    $"{result.Message} {centered.Message}", centered.Sizes ?? result.Sizes);
            }
        }
        ShowStatus(result.Message, result.IsExact ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }
}
