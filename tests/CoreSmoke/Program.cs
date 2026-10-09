using System.Drawing;
using System.Windows.Forms;
using WindowSizerWinUI;
using Windows.Graphics;

Application.EnableVisualStyles();
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

if (args.Contains("--verify-persisted"))
{
    SizeInt32 persisted = WindowLayoutStore.Load();
    if (persisted.Width != 1600 || persisted.Height != 900)
        throw new Exception($"Saved window size was not restored: {persisted.Width}×{persisted.Height}");
    Console.WriteLine("Window size restored across processes OK");
    return;
}

if (!WindowLayoutStore.Save(new SizeInt32(1600, 900)) ||
    WindowLayoutStore.Load().Width != 1600 ||
    WindowLayoutStore.Load().Height != 900)
    throw new Exception("Window size persistence failed.");
Console.WriteLine("Window size persistence OK");

using Form target = new()
{
    Text = "WindowSizer WinUI smoke target",
    StartPosition = FormStartPosition.Manual,
    Location = new Point(80, 80),
    Size = new Size(640, 480)
};
target.Show();
Application.DoEvents();

WindowItem? listed = WindowResizer.Enumerate(nint.Zero).Find(x => x.Handle == target.Handle);
if (listed is null) throw new Exception("Target window was not enumerated.");
if (WindowResizer.Enumerate(target.Handle).All(x => x.Handle != target.Handle))
    throw new Exception("The utility's own window was not enumerated.");
Console.WriteLine("Visible taskbar window and self enumeration OK");

using Form owned = new() { Text = "WindowSizer owned dialog", Size = new Size(280, 160), ShowInTaskbar = false };
owned.Show(target);
Application.DoEvents();
if (WindowResizer.Enumerate(nint.Zero).Any(x => x.Handle == owned.Handle))
    throw new Exception("Owned non-taskbar window was enumerated.");
owned.Close();
Console.WriteLine("Owned non-taskbar window excluded OK");

using Form trayOnly = new() { Text = "WindowSizer hidden tray window", ShowInTaskbar = false };
nint trayHandle = trayOnly.Handle;
if (WindowResizer.Enumerate(nint.Zero).Any(x => x.Handle == trayHandle))
    throw new Exception("Hidden tray window was enumerated.");
Console.WriteLine("Hidden tray window excluded OK");

ResizeResult outer = WindowResizer.Resize(target.Handle, 800, 600, false);
if (!outer.IsExact || !WindowResizer.TryGetSizes(target.Handle, out WindowSizes outerSizes) ||
    outerSizes.OuterWidth != 800 || outerSizes.OuterHeight != 600)
    throw new Exception("Outer size check failed: " + outer.Message);
Console.WriteLine("Outer size: 800×600 OK");

ResizeResult client = WindowResizer.Resize(target.Handle, 1000, 700, true);
if (!client.IsExact || !WindowResizer.TryGetSizes(target.Handle, out WindowSizes clientSizes) ||
    clientSizes.ClientWidth != 1000 || clientSizes.ClientHeight != 700)
    throw new Exception("Client size check failed: " + client.Message);
Console.WriteLine("Client size: 1000×700 OK");

ResizeResult centered = WindowResizer.Center(target.Handle);
if (!centered.IsExact) throw new Exception("Center check failed: " + centered.Message);
Rectangle screen = Screen.FromHandle(target.Handle).Bounds;
Point actualCenter = new(target.Left + target.Width / 2, target.Top + target.Height / 2);
Point expectedCenter = new(screen.Left + screen.Width / 2, screen.Top + screen.Height / 2);
if (Math.Abs(actualCenter.X - expectedCenter.X) > 16 ||
    Math.Abs(actualCenter.Y - expectedCenter.Y) > 16)
    throw new Exception($"Center differs too much: {actualCenter} vs {expectedCenter}");
Console.WriteLine("Center on target monitor OK");

target.Close();
