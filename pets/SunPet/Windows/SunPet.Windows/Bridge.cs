using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SunPet;

public static class Roles
{
    public const string Sun = "com.ganzhe2028.sunpet", Feng = "local.goodthing.desktop-pet", Shao = "local.shaolan.desktop-pet";
    public static readonly string[] All = [Sun, Feng, Shao];
    public static string Name(string role) => role == Sun ? "太对了" : role == Feng ? "峰哥" : "薄肌";
}
public sealed record BridgeMessage(string From, int ClipID, double Duration, string Label, int Hop, string Root)
{
    public string Type { get; init; } = "spoke";
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public int Version { get; init; } = 1;
}
public static class BridgeRules
{
    public static bool Valid(BridgeMessage m) => m.Version == 1 && Roles.All.Contains(m.From) && Roles.All.Contains(m.Root)
        && m.ClipID > 0 && double.IsFinite(m.Duration) && m.Duration > 0 && m.Duration <= 30
        && m.Hop >= 0 && m.Hop <= 3 && m.Label is { Length: <= 512 } && m.EventId is { Length: > 0 and <= 64 }
        && (m.Hop != 0 || m.Root == m.From);
    public static bool Respond(string me, BridgeMessage m) => m.Type == "spoke" && Valid(m) && m.From != me && m.Hop < 3 &&
        (me == Roles.Sun ? m.From == Roles.Feng || m.From == Roles.Shao : me == Roles.Feng && m.From == Roles.Sun && m.Root == Roles.Shao);
    public static double Delay(double duration, int hop, double leading) => Math.Max(.02, duration - (hop == 1 ? .2 : .3) - leading);
}

// One bounded, newline-delimited JSON request/response per connection. Local-only
// clients use "."; ACL is CurrentUserOnly, and names include both SID and session.
public sealed class PetBridge : IDisposable
{
    public static string TestSuffix = "";
    public static string Scope => WindowsIdentity.GetCurrent().User!.Value + "-" + Process.GetCurrentProcess().SessionId;
    public static string PipeName(string role) => "SunPetBridge-v1-" + Scope + "-" + role + TestSuffix;
    private readonly string me;
    private readonly CancellationTokenSource stop = new();
    private readonly HashSet<string> seen = [];
    private readonly Queue<string> order = [];
    private Task? server, presence;
    public event Action<BridgeMessage>? Spoke;
    public event Action? Activate;
    public event Action? Quit;
    public event Action<string>? Presence;
    public string Summary { get; private set; } = "都不在（Windows peers）";
    public PetBridge(string me = Roles.Sun) { this.me = me; }
    public void Start() { server = Task.Run(Serve); presence = Task.Run(Poll); }
    private async Task Serve()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName(me), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); deadline.CancelAfter(1000);
                var text = await ReadLine(pipe, deadline.Token);
                var m = JsonSerializer.Deserialize<BridgeMessage>(text, Catalog.Json);
                if (m == null) continue;
                await pipe.WriteAsync(Encoding.UTF8.GetBytes("ok\n"), deadline.Token);
                if (m.Type == "activate" && m.From == me) { Activate?.Invoke(); continue; }
                if (m.Type == "quit" && m.From == me) { Quit?.Invoke(); continue; }
                if (!BridgeRules.Respond(me, m) || !seen.Add(m.EventId)) continue;
                order.Enqueue(m.EventId);
                if (order.Count > 256) seen.Remove(order.Dequeue());
                Diagnostics.Log($"receive from={m.From} hop={m.Hop} root={m.Root}");
                Spoke?.Invoke(m);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or JsonException or UnauthorizedAccessException)
            {
                if (!stop.IsCancellationRequested)
                {
                    Diagnostics.Log("ipc " + e.Message);
                    try { await Task.Delay(100, stop.Token); } catch (OperationCanceledException) { }
                }
            }
        }
    }
    private async Task Poll()
    {
        while (!stop.IsCancellationRequested)
        {
            var alive = new List<string>();
            foreach (var role in Roles.All.Where(r => r != me))
                if (await Send(role, new(me, 0, 0, "", 0, me) { Type = "ping" }, stop.Token)) alive.Add(Roles.Name(role));
            var summary = alive.Count == 0 ? "都不在（Windows peers）" : string.Join("、", alive);
            if (summary != Summary) { Summary = summary; Presence?.Invoke(summary); }
            try { await Task.Delay(2000, stop.Token); } catch (OperationCanceledException) { break; }
        }
    }
    public async Task Announce(BridgeMessage message)
    {
        await Task.WhenAll(Roles.All.Where(r => r != me).Select(r => Send(r, message, stop.Token)));
    }
    public static async Task<bool> Send(string role, BridgeMessage message, CancellationToken token = default)
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(400);
            using var pipe = new NamedPipeClientStream(".", PipeName(role), PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(deadline.Token);
            var compact = new JsonSerializerOptions(Catalog.Json) { WriteIndented = false };
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, compact) + "\n"), deadline.Token);
            return await ReadLine(pipe, deadline.Token) == "ok";
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or UnauthorizedAccessException or TimeoutException) { return false; }
    }
    private static async Task<string> ReadLine(Stream stream, CancellationToken token)
    {
        var bytes = new List<byte>(); var one = new byte[1];
        while (bytes.Count < 8192)
        {
            if (await stream.ReadAsync(one, token) == 0) throw new IOException("Incomplete IPC frame");
            if (one[0] == 10) return Encoding.UTF8.GetString(bytes.ToArray());
            bytes.Add(one[0]);
        }
        throw new IOException("IPC frame too long");
    }
    public void Dispose()
    {
        stop.Cancel();
        try { Task.WaitAll(new[] { server, presence }.Where(t => t != null).Cast<Task>().ToArray(), 1500); } catch (AggregateException) { }
        stop.Dispose();
    }
}
