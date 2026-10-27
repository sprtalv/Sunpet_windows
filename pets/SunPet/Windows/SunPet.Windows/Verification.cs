using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SunPet;
public static class Verification
{
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
    public static List<string> Core()
    {
        var log = new List<string>(); var clips = Catalog.Load();
        Check(clips.Count == Profile.ExpectedClips, "28 metadata rows");
        Check(Directory.GetFiles(Path.Combine(Catalog.Assets, "Clips"), "*").Select(Path.GetFileName).ToHashSet().SetEquals(clips.Select(c => c.File)), "one-to-one audio files");
        using (var audio = new AudioEngine())
            foreach (var c in clips)
            {
                var decoded = audio.Decode(c);
                Check(decoded.Duration > .25 && decoded.Duration < 7 && Math.Abs(decoded.Duration - c.Duration) < .15, "decode duration " + c.Id);
                Check(decoded.LeadingSilence >= 0 && decoded.LeadingSilence < decoded.Duration * .6, "leading silence " + c.Id);
                log.Add($"decode {c.Id:00}: {decoded.Duration:F3}s lead={decoded.LeadingSilence:F3}s PCM={decoded.Pcm.Length}");
            }
        var bag = new ShuffleBag(new Random(123)); int? last = null;
        for (int round = 0; round < 100; round++)
        {
            var ids = Enumerable.Range(0, clips.Count).Select(_ => bag.Take(clips.Select(c => c.Id))!.Value).ToArray();
            Check(ids.Distinct().Count() == clips.Count && ids[0] != last, "shuffle uniqueness / round boundary"); last = ids[^1];
        }
        var settings = new Settings { RandomMode = false, PinnedId = clips[0].Id }; var selector = new PlaybackSelection(clips, settings);
        for (int i = 0; i < 100; i++) Check(selector.Next().Id == clips[0].Id, "fixed mode");
        settings.RandomMode = true;
        selector.Next(); selector.Toggle(1); selector.Toggle(2);
        for (int i = 0; i < 104; i++) Check(!settings.ExcludedIds.Contains(selector.Next().Id), "exclusion after bag populated");
        selector.Restore(); Check(Enumerable.Range(0, clips.Count).Select(_ => selector.Next().Id).Distinct().Count() == clips.Count, "restore pool");
        foreach (var c in clips.Skip(1)) Check(selector.Toggle(c.Id), "toggle exclude");
        Check(!selector.Toggle(clips[0].Id) && selector.Next().Id == clips[0].Id, "minimum one");
        selector.Restore(); foreach (var id in PlaybackSelection.CompanionIds) selector.Toggle(id);
        var candidate = selector.Companion(); Check(candidate == null || !settings.ExcludedIds.Contains(candidate.Id), "companion obeys exclusions");
        var directory = Path.Combine(Path.GetTempPath(), "SunPet-model-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        Check(store.Load(clips).ExcludedIds.SetEquals(clips.Where(c => c.DefaultOff).Select(c => c.Id)), "first-run defaultOff metadata");
        settings = new() { Size = 240, Volume = .8, X = -1000, Y = 500, RandomMode = false, PinnedId = clips[^1].Id, ExcludedIds = [1, 2, 4], BridgeEnabled = false };
        Check(store.Save(settings), "save"); var restored = store.Load(clips);
        Check(restored.Size == 240 && restored.Volume == .8 && restored.X == -1000 && restored.Y == 500 && !restored.RandomMode && restored.PinnedId == clips[^1].Id && restored.ExcludedIds.SetEquals([1, 2, 4]) && !restored.BridgeEnabled, "settings roundtrip");
        File.WriteAllText(store.FilePath, "broken{"); restored = store.Load(clips); Check(restored.Size == Profile.DefaultSize && restored.Volume == .65 && restored.RandomMode, "corrupt settings fallback");
        restored.PinnedId = 999; restored.RandomMode = false; restored.ExcludedIds = clips.Select(c => c.Id).ToHashSet(); restored.Size = 999; restored.Normalize(clips);
        Check(restored.RandomMode && restored.Size == Profile.MaxSize && restored.ExcludedIds.Count == clips.Count - 1, "invalid IDs / bounds");
        Directory.Delete(directory, true);
        var msg = new BridgeMessage(Roles.Shao, 1, 1, "test", 0, Roles.Shao);
        Check(BridgeRules.Respond(Roles.Sun, msg), "shaolan start");
        Check(!BridgeRules.Respond(Roles.Sun, msg with { From = Roles.Sun }), "self filter");
        Check(!BridgeRules.Respond(Roles.Sun, msg with { From = "unknown" }), "peer filter");
        Check(!BridgeRules.Respond(Roles.Sun, msg with { Hop = 3 }), "hop cap");
        Check(!BridgeRules.Respond(Roles.Sun, msg with { Hop = -1 }), "negative hop");
        Check(!BridgeRules.Respond(Roles.Sun, msg with { Root = Roles.Feng }), "root mismatch");
        Check(!BridgeRules.Respond(Roles.Sun, msg with { Duration = double.NaN }), "invalid duration");
        Check(BridgeRules.Respond(Roles.Feng, msg with { From = Roles.Sun, Hop = 1 }), "feng root rule positive");
        Check(!BridgeRules.Respond(Roles.Feng, msg with { From = Roles.Sun, Root = Roles.Sun }), "feng root rule negative");
        Check(Math.Abs(BridgeRules.Delay(1, 0, .1) - .6) < .001 && Math.Abs(BridgeRules.Delay(1, 1, .1) - .7) < .001, "overlap and lead");
        log.Add("PASS core: 100 shuffle rounds, fixed, exclusions, pool changes, settings/corruption, protocol, lead compensation"); return log;
    }
    public static async Task<List<string>> Run(PetWindow window)
    {
        var log = Core(); window.Settings.Volume = 0;
        var menu = window.MakeMenu();
        var choose = menu.Items.OfType<MenuItem>().Single(m => (string)m.Header == "挑一句");
        var filter = menu.Items.OfType<MenuItem>().Single(m => (string)m.Header == "轮播内容");
        Check(choose.Items.Count == window.Clips.Count && filter.Items.OfType<MenuItem>().Count(m => m.StaysOpenOnClick) == window.Clips.Count, "menu wiring");
        window.Settings.RandomMode = false; window.Settings.PinnedId = window.Clips[0].Id;
        for (int i = 0; i < 40; i++) { window.PlayNext(); Check(window.Selection.LastId == window.Clips[0].Id && window.ActiveOutputs == 1 && window.Speaking, "rapid playback"); }
        await Task.Delay(2200); Check(window.AtRest && !window.IsPlaying, "end restores animation");
        window.PlayNext(); window.StopPlayback(); Check(window.AtRest && window.ActiveOutputs == 0, "stop disposes output");
        // Test independent OS pipe handles; multi-process simulator is an additional integration test.
        using (var sun = new PetBridge())
        {
            int count = 0; sun.Spoke += _ => System.Threading.Interlocked.Increment(ref count); sun.Start(); await Task.Delay(100);
            var message = new BridgeMessage(Roles.Shao, 1, 1, "test", 0, Roles.Shao);
            Check(await PetBridge.Send(Roles.Sun, message), "pipe delivery");
            await PetBridge.Send(Roles.Sun, message); await PetBridge.Send(Roles.Sun, message with { EventId = Guid.NewGuid().ToString(), Hop = 3 });
            await Task.Delay(100); Check(count == 1, "IPC duplicate/hop filtering");
        }
        Check(!await PetBridge.Send(Roles.Sun, new(Roles.Shao, 1, 1, "test", 0, Roles.Shao)), "pipe shutdown");
        log.Add($"PASS GUI self-test: {window.Clips.Count} menu entries, 40 rapid replays / one output, natural completion/rest, stop/release, named-pipe delivery/dedup/hop/cleanup");
        return log;
    }
}

