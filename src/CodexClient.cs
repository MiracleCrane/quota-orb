using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace QuotaOrb
{
    public sealed class CodexClient : IDisposable
    {
        private readonly object gate = new object();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 2097152 };
        private Process process;
        private BlockingCollection<string> messages;
        private int sequence;
        private bool disposed;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        public string Error { get; private set; }

        public QuotaSnapshot Read(bool creditDetails)
        {
            lock (gate)
            {
                if (disposed) return null;
                try
                {
                    EnsureConnection();
                    var result = Request("account/rateLimits/read", new { excludeResetCreditDetails = !creditDetails });
                    var snapshot = QuotaSnapshot.Parse(json.Serialize(result), DateTimeOffset.Now);
                    Error = null;
                    return snapshot;
                }
                catch (OperationCanceledException) { Error = null; }
                catch (TimeoutException) { Error = "读取超时"; }
                catch (FileNotFoundException) { Error = "未找到 Codex，请在设置中指定 CLI"; }
                catch (FormatException) { Error = "未返回有效额度，请检查 Codex 登录状态"; }
                catch (Exception) { Error = "额度读取失败，请检查 Codex 登录和网络"; }
                Disconnect();
                return null;
            }
        }

        private void EnsureConnection()
        {
            if (process != null && !process.HasExited) return;
            Disconnect();
            messages = new BlockingCollection<string>(256);
            var queue = messages;
            var start = new ProcessStartInfo(FindExecutable(), "app-server --stdio") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false)
            };
            // Inherit Codex home and the user's proxy environment without reading credentials.
            process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                if (e.Data == null) { try { queue.CompleteAdding(); } catch { } return; }
                try { queue.TryAdd(e.Data); } catch (InvalidOperationException) { }
            };
            process.ErrorDataReceived += delegate { };
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            Request("initialize", new { clientInfo = new { name = "quota-orb", title = "Quota Orb", version = "0.1.6" },
                capabilities = new { experimentalApi = true } });
            Send(new { method = "initialized" });
        }

        private Dictionary<string, object> Request(string method, object parameters)
        {
            int id = ++sequence;
            Send(new { id = id, method = method, @params = parameters });
            var deadline = Stopwatch.StartNew();
            while (deadline.ElapsedMilliseconds < 25000)
            {
                string line;
                if (!messages.TryTake(out line, Math.Max(1, 25000 - (int)deadline.ElapsedMilliseconds), stop.Token)) break;
                Dictionary<string, object> response;
                try { response = Json.Object(json.DeserializeObject(line)); } catch (ArgumentException) { continue; }
                if (response == null || Json.Number(response, "id") != id) continue;
                if (response.ContainsKey("error")) throw new FormatException("Codex rejected the read request.");
                return Json.Child(response, "result");
            }
            throw new TimeoutException();
        }

        private void Send(object value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json.Serialize(value) + "\n");
            process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
            process.StandardInput.BaseStream.Flush();
        }

        public static string FindExecutable()
        {
            var configured = Settings.Load().CodexPath;
            foreach (string candidate in new[] { configured, Environment.GetEnvironmentVariable("CODEX_CLI_PATH") })
                if (!String.IsNullOrWhiteSpace(candidate) && File.Exists(candidate) && candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return candidate;
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var directory = Path.Combine(local, "OpenAI", "Codex", "bin");
            if (Directory.Exists(directory))
                foreach (var folder in new DirectoryInfo(directory).GetDirectories().OrderByDescending(d => d.LastWriteTimeUtc))
                { string exe = Path.Combine(folder.FullName, "codex.exe"); if (File.Exists(exe)) return exe; }
            foreach (var candidate in Process.GetProcessesByName("ChatGPT"))
            {
                try {
                    string path = candidate.MainModule.FileName;
                    if (path.IndexOf("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string exe = Path.Combine(Path.GetDirectoryName(path), "resources", "codex.exe");
                    if (File.Exists(exe)) return exe;
                } catch { }
                finally { candidate.Dispose(); }
            }
            foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            { try { string exe = Path.Combine(folder.Trim('"'), "codex.exe"); if (File.Exists(exe)) return exe; } catch (ArgumentException) { } }
            throw new FileNotFoundException("Codex executable not found.");
        }

        private void Disconnect()
        {
            var owned = process; process = null;
            var queue = messages; messages = null;
            if (owned != null) {
                try { owned.StandardInput.Close(); if (!owned.WaitForExit(1500)) owned.Kill(); } catch { }
                owned.Dispose();
            }
            if (queue != null) queue.Dispose();
            // Only the child process created above is stopped, never the desktop or shared host.
        }
        public void Dispose() { stop.Cancel(); lock (gate) { disposed = true; Disconnect(); } }
    }
}
