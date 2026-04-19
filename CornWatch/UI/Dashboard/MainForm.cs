using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using CornWatch.Core;
using CornWatch.Models;

namespace CornWatch.UI.Dashboard;

public partial class MainForm : Form
{
    private readonly SystemMonitor _monitor;
    private WebView2?    _webView;
    private NotifyIcon?  _trayIcon;
    private bool         _webViewReady = false;
    private SystemSnapshot? _lastSnap;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MainForm(bool startMinimized = false)
    {
        InitializeComponent();
        InitTrayIcon();
        _monitor = new SystemMonitor();
        _monitor.SnapshotReady += OnSnapshotReady;

        if (startMinimized)
        {
            WindowState   = FormWindowState.Minimized;
            ShowInTaskbar = false;
            Visible       = false;
        }
    }

    private void InitializeComponent()
    {
        Text          = "🌽 CornWatch — System Health Dashboard";
        Size          = new Size(1280, 800);
        MinimumSize   = new Size(960, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor     = Color.FromArgb(10, 10, 10);

        // Load custom icon
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "cornwatch.ico");
        if (File.Exists(icoPath))
            Icon = new Icon(icoPath);

        _ = InitWebViewAsync();
    }

    // ── System Tray ──────────────────────────────────────────────────────────
    private void InitTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open CornWatch",  null, (_, _) => ShowWindow());
        menu.Items.Add(new ToolStripSeparator());
        var startupItem = new ToolStripMenuItem("Launch at startup")
        {
            Checked      = StartupManager.IsEnabled,
            CheckOnClick = true,
        };
        startupItem.CheckedChanged += (_, _) =>
        {
            try { StartupManager.Toggle(); }
            catch (Exception ex) { MessageBox.Show("Could not update startup: " + ex.Message); }
        };
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { _trayIcon!.Visible = false; Application.Exit(); });

        _trayIcon = new NotifyIcon
        {
            Text    = "CornWatch",
            Icon    = this.Icon ?? SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        ShowInTaskbar = true;
        Show();
        WindowState   = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private bool _balloonShown = false;
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            ShowInTaskbar = false;
            Hide();
            if (!_balloonShown)
            {
                _trayIcon!.ShowBalloonTip(2000, "CornWatch", "Minimized to tray — double-click to restore", ToolTipIcon.Info);
                _balloonShown = true;
            }
        }
    }

    // ── WebView2 ─────────────────────────────────────────────────────────────
    private async Task InitWebViewAsync()
    {
        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.AddHostObjectToScript("cornBridge", new CornBridge(this, _monitor));

#if !DEBUG
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled            = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled            = false;
#endif

        var htmlPath = Path.Combine(AppContext.BaseDirectory, "UI", "Dashboard", "dashboard.html");
        _webView.CoreWebView2.Navigate(File.Exists(htmlPath)
            ? "file:///" + htmlPath.Replace('\\', '/')
            : "about:blank");

        _webView.CoreWebView2.NavigationCompleted += (_, _) => _webViewReady = true;
    }

    // ── Data bridge ──────────────────────────────────────────────────────────
    private void OnSnapshotReady(SystemSnapshot snap)
    {
        _lastSnap = snap;
        if (!_webViewReady || _webView is null) return;
        var json = JsonSerializer.Serialize(snap, _jsonOpts);
        if (InvokeRequired) Invoke(() => PushToJs(json));
        else PushToJs(json);
    }

    internal void PushToJs(string json) =>
        _ = _webView?.CoreWebView2.ExecuteScriptAsync($"window.cornWatch?.onSnapshot({json})");

    internal SystemSnapshot? LastSnapshot => _lastSnap;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Close button minimizes to tray; tray Exit menu actually quits
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            return;
        }
        _trayIcon?.Dispose();
        _monitor.Dispose();
        base.OnFormClosing(e);
    }
}

// ── JS ↔ C# bridge ───────────────────────────────────────────────────────────
[System.Runtime.InteropServices.ComVisible(true)]
public class CornBridge(MainForm form, SystemMonitor monitor)
{
    public void OpenProcessManager()  => System.Diagnostics.Process.Start("taskmgr.exe");
    public void OpenResourceMonitor() => System.Diagnostics.Process.Start("resmon.exe");
    public string GetGpuSensorDump()  => monitor.GpuSensorDump;

    public bool GetStartupEnabled() => StartupManager.IsEnabled;
    public void SetStartupEnabled(bool enabled)
    {
        if (enabled) StartupManager.Enable();
        else         StartupManager.Disable();
    }

    /// <summary>Exports the last snapshot to Documents/CornWatch/Snapshots/ and returns the path.</summary>
    public string ExportSnapshot()
    {
        var snap = form.LastSnapshot;
        if (snap is null) return "No snapshot available yet.";
        try
        {
            var path = SnapshotExporter.Export(snap);
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            return path;
        }
        catch (Exception ex) { return "Export failed: " + ex.Message; }
    }
}