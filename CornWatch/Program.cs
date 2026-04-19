using CornWatch.UI.Dashboard;

namespace CornWatch;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var startMinimized = args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
        Application.Run(new MainForm(startMinimized));
    }
}
