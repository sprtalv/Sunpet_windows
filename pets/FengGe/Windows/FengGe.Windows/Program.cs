using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SunPet;
public static class Diagnostics
{
    public static bool Trace;
    public static string? TraceFile;
    private static readonly object Gate = new();
    public static void Log(string text)
    {
        if (!Trace) return;
        var line = DateTimeOffset.Now.ToString("O") + " " + text;
        lock (Gate)
        {
            Console.WriteLine(line);
            try { Directory.CreateDirectory(SettingsStore.DirectoryPath); File.AppendAllText(TraceFile ?? Path.Combine(SettingsStore.DirectoryPath, "trace.log"), line + Environment.NewLine); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
public static class Program
{
    public static bool UiCheck;
    public static bool IntegrationTest;
    public static string? Option(string[] args, string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    [STAThread]
    public static int Main(string[] args)
    {
        Native.AttachConsole(uint.MaxValue); Console.OutputEncoding = Encoding.UTF8;
        Diagnostics.Trace = args.Contains("--trace");
        Diagnostics.TraceFile = Option(args, "--trace-file");
        UiCheck = args.Contains("--ui-check");
        IntegrationTest = args.Contains("--integration-test");
        if (Option(args, "--ipc-scope") is string scope)
        {
            if (scope.Length > 40 || scope.Any(c => !char.IsLetterOrDigit(c) && c != '-')) return 2;
            PetBridge.TestSuffix = "-" + scope;
        }
        Diagnostics.Log("startup " + string.Join(' ', args));
        var report = Option(args, "--report");
        try
        {
            if (args.Contains("--quit")) return PetBridge.Send(Profile.Role, new(Profile.Role, 0, 0, "", 0, Profile.Role) { Type = "quit" }).GetAwaiter().GetResult() ? 0 : 1;
            if (args.Contains("--dump-mode") || args.Contains("--dump-state"))
            {
                var clips = Catalog.Load(); var s = new SettingsStore().Load(clips);
                var result = $"mode={(s.RandomMode ? "random" : "fixed")} pinned={s.PinnedId} valid={clips.Any(c => c.Id == s.PinnedId)} excluded=[{string.Join(',', s.ExcludedIds)}] clips={clips.Count} pool={clips.Count - s.ExcludedIds.Count}\n" + System.Text.Json.JsonSerializer.Serialize(s, Catalog.Json);
                Console.WriteLine(result); if (report != null) File.WriteAllText(report, result); return 0;
            }
            if (Option(args, "--simulate-peer") is string role) return Simulate(role, report).GetAwaiter().GetResult();
            bool selfTest = args.Contains("--self-test");
            if (selfTest) PetBridge.TestSuffix = "-test-" + Environment.ProcessId;
            using var mutex = new Mutex(true, "Local\\" + Profile.AppId + "-" + PetBridge.Scope + (selfTest ? "-test" : PetBridge.TestSuffix), out bool created);
            if (!created)
            {
                PetBridge.Send(Profile.Role, new(Profile.Role, 0, 0, "", 0, Profile.Role) { Type = "activate" }).GetAwaiter().GetResult();
                Diagnostics.Log("duplicate instance exited"); return 0;
            }
            Diagnostics.Log("instance acquired");
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            string? testDirectory = selfTest || IntegrationTest ? Path.Combine(Path.GetTempPath(), "SunPet-test-" + Guid.NewGuid().ToString("N")) : null;
            var window = new PetWindow(selfTest, testDirectory == null ? null : new SettingsStore(Path.Combine(testDirectory, "settings.json")));
            int exitCode = 0;
            if (IntegrationTest)
            {
                window.Settings.Volume = 0;
                window.Loaded += async (_, _) => { await Task.Delay(12000); window.Close(); };
            }
            if (args.Contains("--bridge-test") || IntegrationTest && Option(args, "--play") != null)
                window.Loaded += async (_, _) => { await Task.Delay(2500); var clip = window.Clips.FirstOrDefault(c => c.Id.ToString() == Option(args, "--play")) ?? window.Clips[0]; window.Play(clip); };
            if (selfTest)
                window.Loaded += async (_, _) =>
                {
                    try
                    {
                        var lines = await Verification.Run(window);
                        var text = string.Join(Environment.NewLine, lines) + Environment.NewLine + "PASS";
                        Console.WriteLine(text); if (report != null) File.WriteAllText(report, text);
                    }
                    catch (Exception e) { exitCode = 1; var text = "FAIL " + e; Console.WriteLine(text); if (report != null) File.WriteAllText(report, text); }
                    finally { window.Close(); }
                };
            Diagnostics.Log("window constructed");
            app.Run(window); mutex.ReleaseMutex(); return exitCode;
        }
        catch (Exception e)
        {
            if (report != null) File.WriteAllText(report, "FAIL " + e);
            if (!args.Contains("--self-test") && !args.Contains("--dump-mode")) MessageBox.Show("桌宠无法启动：" + e.Message, "SunPet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 1;
        }
    }
    private static async Task<int> Simulate(string name, string? report)
    {
        var role = name == "fengge" ? Roles.Feng : name == "shaolan" ? Roles.Shao : throw new ArgumentException("peer must be fengge or shaolan");
        using var mutex = new Mutex(false, "Local\\SunPet-simulator-" + PetBridge.Scope + role);
        if (!mutex.WaitOne(0)) return 2;
        // Hold a separate process-scoped mutex via its handle; no GUI and no audio.
        using var bridge = new PetBridge(role); int received = 0;
        var log = new System.Collections.Concurrent.ConcurrentQueue<string>();
        bridge.Spoke += m =>
        {
            Interlocked.Increment(ref received); log.Enqueue($"received hop={m.Hop} root={m.Root} from={m.From}");
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(BridgeRules.Delay(m.Duration, m.Hop, 0)));
                await bridge.Announce(new(role, 1, .8, "模拟 peer 接话", m.Hop + 1, m.Root));
            });
        };
        bridge.Start(); await Task.Delay(500);
        if (role == Roles.Shao) await bridge.Announce(new(role, 1, 1.2, "模拟薄肌起句", 0, role));
        await Task.Delay(8000);
        var result = $"SIMULATOR role={role} received={received}\n" + string.Join('\n', log);
        if (report != null) File.WriteAllText(report, result); Console.WriteLine(result); return 0;
    }
}

