using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

internal static class LaunchFixture
{
    [STAThread]
    private static int Main(string[] args)
    {
        string directory = AppDomain.CurrentDomain.BaseDirectory;
        bool companion = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName) == "QuotaOrb.exe";
        File.WriteAllText(Path.Combine(directory, companion ? "companion-started.json" : "codex-started.json"),
            new JavaScriptSerializer().Serialize(new { arguments = args, workingDirectory = Environment.CurrentDirectory }));
        if (companion) Thread.Sleep(1500);
        return companion ? 2 : 0;
    }
}
