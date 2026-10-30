using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using QuotaOrb;

internal static class LauncherTests
{
    internal static void Run(Action<bool, string> check)
    {
        string artifacts = Path.GetDirectoryName(typeof(QuotaSnapshot).Assembly.Location);
        string root = Path.Combine(Path.GetTempPath(), "quota-orb-launch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            foreach (string mode in new[] { "missing", "failing", "legacy" }) {
                string directory = Path.Combine(root, mode + " with spaces");
                Directory.CreateDirectory(directory);
                string fixture = Path.Combine(artifacts, "QuotaOrb.LaunchFixture.exe");
                string codex = Path.Combine(directory, "Codex fixture.exe");
                string launcher = Path.Combine(directory, "QuotaOrb.Launcher.exe");
                File.Copy(fixture, codex);
                File.Copy(Path.Combine(artifacts, "QuotaOrb.Launcher.exe"), launcher);
                if (mode != "missing") File.Copy(fixture, Path.Combine(directory, "QuotaOrb.exe"));
                string shortcut = Path.Combine(directory, "Original Codex.lnk");
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic link = shell.CreateShortcut(shortcut);
                link.TargetPath = codex;
                link.Arguments = "--fixture \"two words\"";
                link.WorkingDirectory = directory;
                link.Save();
                string arguments = "--shortcut \"" + shortcut + "\"";
                if (mode == "legacy") {
                    string config = Path.Combine(directory, "launcher.json");
                    File.WriteAllText(config, new JavaScriptSerializer().Serialize(new { OriginalShortcut = shortcut }));
                    arguments = "--config \"" + config + "\"";
                }
                using (var process = Process.Start(new ProcessStartInfo(launcher, arguments) { UseShellExecute = false, CreateNoWindow = true })) {
                    check(process.WaitForExit(5000), "The linked launcher must return without waiting for the companion: " + mode);
                    check(process.ExitCode == 0, "Optional companion failure must not report Codex launch failure: " + mode);
                }
                string marker = Path.Combine(directory, "codex-started.json");
                check(WaitForFile(marker, 5000), "The original Codex shortcut must launch even with no quota executable: " + mode);
                var result = new JavaScriptSerializer().Deserialize<FixtureResult>(File.ReadAllText(marker));
                check(result.arguments.Length == 2 && result.arguments[1] == "two words", "Original shortcut argument quoting must be preserved: " + mode);
                check(String.Equals(result.workingDirectory, directory, StringComparison.OrdinalIgnoreCase), "Original shortcut working directory must be preserved: " + mode);
                if (mode != "missing") check(WaitForFile(Path.Combine(directory, "companion-started.json"), 5000), "The optional failing companion must be launched independently: " + mode);
            }
        } finally {
            // The deliberately failing fixture lives briefly after the launcher returns.
            Thread.Sleep(1800);
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    private static bool WaitForFile(string path, int timeout)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < timeout) {
            if (File.Exists(path) && new FileInfo(path).Length > 0) return true;
            Thread.Sleep(25);
        }
        return false;
    }

    private sealed class FixtureResult
    {
        public string[] arguments { get; set; }
        public string workingDirectory { get; set; }
    }
}
