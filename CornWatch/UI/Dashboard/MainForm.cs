using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using CornWatch.Core;
using CornWatch.Models;

namespace CornWatch.UI.Dashboard;

public partial class MainForm : Form
{
    private readonly SystemMonitor _monitor;
    private WebView2? _webView;
    private bool _webViewReady = false;

    // JSON serialiser options (camelCase for JS compatibility)
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MainForm()
    {
        InitializeComponent();
        _monitor = new SystemMonitor();
        _monitor.SnapshotReady += OnSnapshotReady;
    }

    private void InitializeComponent()
    {
        Text            = "🌽 CornWatch — System Health Dashboard";
        Size            = new Size(1280, 800);
        MinimumSize     = new Size(960, 640);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = Color.FromArgb(10, 10, 10);

        // Borderless custom chrome (optional, can revert to standard)
        // FormBorderStyle = FormBorderStyle.None;

        _ = InitWebViewAsync();
    }

    private async Task InitWebViewAsync()
    {
        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
        };
        Controls.Add(_webView);

        await _webView.EnsureCoreWebView2Async();

        // Allow the C# side to call JS and vice versa
        _webView.CoreWebView2.AddHostObjectToScript("cornBridge", new CornBridge(this));

        // Disable default context menu in release
#if !DEBUG
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

        // Load the dashboard HTML from the embedded resource or file
        var htmlPath = Path.Combine(AppContext.BaseDirectory, "UI", "Dashboard", "dashboard.html");
        if (File.Exists(htmlPath))
            _webView.CoreWebView2.Navigate("file:///" + htmlPath.Replace('\\', '/'));
        else
            _webView.CoreWebView2.NavigateToString(GetFallbackHtml());

        _webView.CoreWebView2.NavigationCompleted += (_, _) => _webViewReady = true;
    }

    // ── Push snapshot to JS ──────────────────────────────────────────────────
    private void OnSnapshotReady(SystemSnapshot snap)
    {
        if (!_webViewReady || _webView is null) return;

        var json = JsonSerializer.Serialize(snap, _jsonOpts);

        // Marshal to UI thread
        if (InvokeRequired)
            Invoke(() => PushToJs(json));
        else
            PushToJs(json);
    }

    private void PushToJs(string json)
    {
        // Calls window.cornWatch.onSnapshot(jsonString) in the dashboard
        _ = _webView?.CoreWebView2.ExecuteScriptAsync(
            $"window.cornWatch?.onSnapshot({json})");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _monitor.Dispose();
        base.OnFormClosing(e);
    }

    private static string GetFallbackHtml() => """
        <html><body style="background:#0a0a0a;color:#6fcf6f;font-family:monospace;
        display:flex;align-items:center;justify-content:center;height:100vh;margin:0">
        <div style="text-align:center">
          <div style="font-size:48px;margin-bottom:16px">🌽</div>
          <div style="font-size:20px">CornWatch loading...</div>
          <div style="font-size:13px;opacity:.5;margin-top:8px">
            dashboard.html not found — place it in UI/Dashboard/
          </div>
        </div></body></html>
        """;
}

/// <summary>
/// Host object exposed to JavaScript as window.chrome.webview.hostObjects.cornBridge
/// Lets the JS dashboard call back into C#.
/// </summary>
[System.Runtime.InteropServices.ComVisible(true)]
public class CornBridge(MainForm _)
{
    public void RequestSnapshot() { /* force an immediate refresh */ }
    public void OpenProcessManager() => System.Diagnostics.Process.Start("taskmgr.exe");
    public void OpenResourceMonitor() => System.Diagnostics.Process.Start("resmon.exe");
}
