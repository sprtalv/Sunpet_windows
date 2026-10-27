using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SunPet;

public sealed record Clip(int Id, string Label, string? Timestamp, string File, double Duration, bool DefaultOff = false, bool Answer = false);

public static class Catalog
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static string Assets => Path.Combine(AppContext.BaseDirectory, "Assets");
    public static List<Clip> Load(string? directory = null)
    {
        var root = directory ?? Assets;
        var clips = JsonSerializer.Deserialize<List<Clip>>(System.IO.File.ReadAllText(Path.Combine(root, "clips.json")), Json) ?? [];
        if (clips.Count == 0 || clips.Select(c => c.Id).Distinct().Count() != clips.Count ||
            clips.Any(c => string.IsNullOrWhiteSpace(c.Label) || Path.GetFileName(c.File) != c.File || !System.IO.File.Exists(Path.Combine(root, "Clips", c.File))))
            throw new InvalidDataException("语音清单或文件缺失");
        return clips;
    }
}

public sealed class Settings
{
    public double Volume { get; set; } = .65;
    public double Size { get; set; } = Profile.DefaultSize;
    // Physical screen pixels; unlike WPF DIPs these also work with negative monitor origins.
    public int? X { get; set; }
    public int? Y { get; set; }
    public bool RandomMode { get; set; } = true;
    public int? PinnedId { get; set; }
    public HashSet<int> ExcludedIds { get; set; } = [];
    public bool BridgeEnabled { get; set; } = true;
    public void Normalize(IReadOnlyList<Clip> clips)
    {
        Volume = double.IsFinite(Volume) ? Math.Clamp(Volume, 0, 1) : .65;
        Size = double.IsFinite(Size) ? Math.Clamp(Size, Profile.MinSize, Profile.MaxSize) : Profile.DefaultSize;
        var ids = clips.Select(c => c.Id).ToHashSet();
        ExcludedIds ??= [];
        ExcludedIds.IntersectWith(ids);
        if (ExcludedIds.Count >= ids.Count) ExcludedIds.Remove(clips[0].Id);
        if (PinnedId is null || !ids.Contains(PinnedId.Value)) { PinnedId = null; RandomMode = true; }
        if (X is null || Y is null) { X = null; Y = null; }
    }
}

public sealed class SettingsStore(string? file = null)
{
    public static string DirectoryPath => System.IO.File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag"))
        ? Path.Combine(AppContext.BaseDirectory, "UserData", System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Profile.SettingsFolder);
    public string FilePath { get; } = file ?? Path.Combine(DirectoryPath, "settings.json");
    public string? Warning { get; private set; }
    public Settings Load(IReadOnlyList<Clip> clips)
    {
        Settings settings;
        Settings Defaults() => new() { ExcludedIds = clips.Where(c => c.DefaultOff).Select(c => c.Id).ToHashSet() };
        try
        {
            // The delivered portable builds keep data on D: as requested. Import this
            // user's existing development settings once, without deleting the original.
            if (file == null && !System.IO.File.Exists(FilePath) && System.IO.File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag")))
            {
                var original = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Profile.SettingsFolder, "settings.json");
                if (System.IO.File.Exists(original))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    System.IO.File.Copy(original, FilePath, false);
                }
            }
            if (System.IO.File.Exists(FilePath))
            {
                var text = System.IO.File.ReadAllText(FilePath);
                settings = JsonSerializer.Deserialize<Settings>(text, Catalog.Json) ?? Defaults();
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && !doc.RootElement.EnumerateObject().Any(p => p.Name.Equals("excludedIds", StringComparison.OrdinalIgnoreCase)))
                    settings.ExcludedIds = Defaults().ExcludedIds;
            }
            else settings = Defaults();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { Warning = "配置无法读取，已恢复默认设置"; settings = Defaults(); }
        settings.Normalize(clips); return settings;
    }
    public bool Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + ".tmp";
            System.IO.File.WriteAllText(temp, JsonSerializer.Serialize(settings, Catalog.Json));
            System.IO.File.Move(temp, FilePath, true); return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Warning = "设置无法保存：" + e.Message; return false; }
    }
}

public sealed class ShuffleBag(Random? random = null)
{
    private readonly Random random = random ?? Random.Shared;
    private readonly List<int> bag = [];
    private HashSet<int> previousPool = [];
    public int? Last { get; set; }
    public void Reset() { bag.Clear(); previousPool.Clear(); }
    public int? Take(IEnumerable<int> candidates)
    {
        var pool = candidates.ToHashSet();
        if (!pool.SetEquals(previousPool)) { bag.Clear(); previousPool = pool; }
        if (pool.Count == 0) return null;
        if (bag.Count == 0)
        {
            bag.AddRange(pool);
            for (int i = bag.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (bag[i], bag[j]) = (bag[j], bag[i]); }
            if (bag.Count > 1 && bag[^1] == Last) (bag[0], bag[^1]) = (bag[^1], bag[0]);
        }
        int value = bag[^1]; bag.RemoveAt(bag.Count - 1); Last = value; return value;
    }
}

public sealed class PlaybackSelection(IReadOnlyList<Clip> clips, Settings settings)
{
    public static readonly int[] CompanionIds = Profile.CompanionIds;
    private readonly ShuffleBag bag = new();
    private readonly ShuffleBag companion = new();
    public int? LastId { get; private set; }
    public Clip Next(bool forceRandom = false)
    {
        int id = !forceRandom && !settings.RandomMode && settings.PinnedId is int pin ? pin : bag.Take(clips.Where(c => !settings.ExcludedIds.Contains(c.Id)).Select(c => c.Id))!.Value;
        return clips.First(c => c.Id == id);
    }
    public Clip? Companion()
    {
        var id = companion.Take(clips.Where(c => CompanionIds.Contains(c.Id) && !settings.ExcludedIds.Contains(c.Id)).Select(c => c.Id));
        return clips.FirstOrDefault(c => c.Id == id);
    }
    public void Played(Clip c) { LastId = c.Id; bag.Last = c.Id; }
    public bool Toggle(int id)
    {
        if (!clips.Any(c => c.Id == id)) return false;
        if (!settings.ExcludedIds.Remove(id))
        {
            if (settings.ExcludedIds.Count >= clips.Count - 1) return false;
            settings.ExcludedIds.Add(id);
        }
        bag.Reset(); companion.Reset(); return true;
    }
    public void Restore() { settings.ExcludedIds.Clear(); bag.Reset(); companion.Reset(); }
}

