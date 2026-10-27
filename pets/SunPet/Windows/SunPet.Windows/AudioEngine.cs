using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace SunPet;

public sealed record DecodedClip(byte[] Pcm, WaveFormat Format, double Duration, double LeadingSilence);

public sealed class AudioEngine : IDisposable
{
    private readonly Dictionary<int, DecodedClip> cache = [];
    private WaveOutEvent? output;
    private RawSourceWaveStream? stream;
    private int generation;
    public bool IsPlaying => output?.PlaybackState == PlaybackState.Playing;
    public int ActiveOutputs => output is null ? 0 : 1;
    public event Action<Exception?>? Finished;
    public DecodedClip Decode(Clip clip)
    {
        if (cache.TryGetValue(clip.Id, out var decoded)) return decoded;
        using var reader = new MediaFoundationReader(Path.Combine(Catalog.Assets, "Clips", clip.File));
        using var memory = new MemoryStream();
        reader.CopyTo(memory);
        var pcm = memory.ToArray();
        var format = reader.WaveFormat;
        if (pcm.Length == 0 || format.BitsPerSample != 16) throw new InvalidDataException("无效 PCM 音频");
        double duration = (double)pcm.Length / format.AverageBytesPerSecond;
        int frames = Math.Min(pcm.Length / format.BlockAlign, (int)(format.SampleRate * .6));
        double peak = 0, lead = 0;
        for (int i = 0; i < frames; i++) peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(pcm, i * format.BlockAlign) / 32768.0));
        double threshold = Math.Max(.004, peak * .06);
        for (int i = 0; i < frames; i++)
            if (Math.Abs(BitConverter.ToInt16(pcm, i * format.BlockAlign) / 32768.0) > threshold) { lead = (double)i / format.SampleRate; break; }
        decoded = new(pcm, format, duration, lead); cache[clip.Id] = decoded; return decoded;
    }
    public void Preload(IEnumerable<Clip> clips)
    {
        foreach (var clip in clips.Where(c => PlaybackSelection.CompanionIds.Contains(c.Id)))
            try { Decode(clip); } catch (Exception e) { Diagnostics.Log("preload-error " + e.Message); }
    }
    public double Play(Clip clip, double volume)
    {
        Stop();
        try
        {
            var audio = Decode(clip);
            stream = new RawSourceWaveStream(new MemoryStream(audio.Pcm, false), audio.Format);
            output = new WaveOutEvent { DesiredLatency = 60, NumberOfBuffers = 3, Volume = (float)volume };
            int token = generation;
            output.PlaybackStopped += (_, e) => { if (token == generation) Finished?.Invoke(e.Exception); };
            output.Init(stream); output.Play();
            return audio.Duration;
        }
        catch { Stop(); throw; }
    }
    public void SetVolume(double volume) { if (output != null) output.Volume = (float)volume; }
    public void Stop()
    {
        generation++;
        var old = output; output = null;
        if (old != null) { old.Stop(); old.Dispose(); }
        stream?.Dispose(); stream = null;
    }
    public void Dispose() { Stop(); cache.Clear(); }
}
