using System.Drawing;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace UsageWidget;

/// <summary>Tray icon: left click toggles the widget, right click opens the same actions as the widget menu.</summary>
sealed class TrayIcon : IDisposable
{
    readonly Forms.NotifyIcon icon;
    readonly Icon image;

    public TrayIcon(MainWindow window)
    {
        image = DrawIcon();
        var menu = new Forms.ContextMenuStrip();
        var startup = new Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        menu.Items.Add("Refresh now", null, (_, _) => window.RequestRefresh());
        menu.Items.Add("Show / hide", null, (_, _) => window.ToggleVisible());
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => window.RequestExit());
        menu.Opening += (_, _) => startup.Checked = StartupEntry.IsEnabled;
        startup.Click += (_, _) => StartupEntry.Set(startup.Checked);

        icon = new Forms.NotifyIcon
        {
            Icon = image,
            Text = "AI usage",
            ContextMenuStrip = menu,
            Visible = true,
        };
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) window.ToggleVisible();
        };
    }

    /// <summary>Three rising bars on a dark rounded square, drawn so no icon file is needed.</summary>
    static Icon DrawIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Color.FromArgb(255, 32, 33, 36));
            using var path = new GraphicsPath();
            path.AddArc(1, 1, 10, 10, 180, 90);
            path.AddArc(21, 1, 10, 10, 270, 90);
            path.AddArc(21, 21, 10, 10, 0, 90);
            path.AddArc(1, 21, 10, 10, 90, 90);
            path.CloseFigure();
            g.FillPath(bg, path);
            using var bar = new SolidBrush(Color.FromArgb(255, 92, 200, 255));
            g.FillRectangle(bar, 7, 18, 5, 8);
            g.FillRectangle(bar, 14, 13, 5, 13);
            g.FillRectangle(bar, 21, 7, 5, 19);
        }
        var handle = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        image.Dispose();
    }
}
