using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EarClarinet.App.Services;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event Action? ToggleMixerRequested;
    public event Action? SettingsRequested;
    public event Action? AboutRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show/Hide Mixer", null, (_, _) => ToggleMixerRequested?.Invoke());
        menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add("About", null, (_, _) => AboutRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Icon = LogoService.GetIcon() ?? DrawIcon(),
            Text = "EarClarinet - per-app volume mixer",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ToggleMixerRequested?.Invoke();
    }

    public void ShowBalloon(string title, string text)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Warning);
    }

    private static Icon DrawIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var background = new SolidBrush(Color.FromArgb(40, 44, 52));
            g.FillEllipse(background, 1, 1, 30, 30);

            using var white = new SolidBrush(Color.FromArgb(232, 232, 236));
            using var pen = new Pen(Color.FromArgb(232, 232, 236), 2);

            g.FillRectangle(white, 8, 13, 6, 6);
            g.FillPolygon(white, new[]
            {
                new PointF(14, 13),
                new PointF(21, 8),
                new PointF(21, 24),
                new PointF(14, 19),
            });
            g.DrawArc(pen, 18, 10, 10, 12, -60, 120);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
