using DSLaughTrack.Config;
using DSLaughTrack.Logging;
using NAudio.Wave;

namespace DSLaughTrack.Audio;

public sealed class LaughPlayer
{
    private static readonly string[] Extensions = { ".wav", ".mp3" };
    private readonly string _soundsRoot;
    private readonly Func<AppConfig> _config;
    private readonly Log _log;
    private readonly Random _rng = new();

    public LaughPlayer(string soundsRoot, Func<AppConfig> config, Log log)
    {
        _soundsRoot = soundsRoot;
        _config = config;
        _log = log;
    }

    public void Play(string triggerKey)
    {
        try
        {
            var cfg = _config();
            var tc = cfg.For(triggerKey);
            var file = ResolveSound(_soundsRoot, triggerKey, tc, _rng);
            if (file is null)
            {
                _log.Warn($"{triggerKey}: fired, but no audio files found (looked in {Path.Combine(_soundsRoot, triggerKey)} and {Path.Combine(_soundsRoot, "default")}) — nothing played");
                return;
            }
            var volume = (float)Math.Clamp(cfg.MasterVolume * tc.Volume, 0.0, 1.0);
            var reader = new AudioFileReader(file) { Volume = volume };
            var output = new WaveOutEvent();
            output.PlaybackStopped += (_, _) => { output.Dispose(); reader.Dispose(); };
            output.Init(reader);
            output.Play();
            _log.Info($"LAUGH [{triggerKey}] {Path.GetFileName(file)} (vol {volume:0.00})");
        }
        catch (Exception ex)
        {
            _log.Error($"{triggerKey}: audio playback failed: {ex.Message}");
        }
    }

    internal static string? ResolveSound(string soundsRoot, string triggerKey, TriggerConfig cfg, Random rng)
    {
        List<string> candidates;
        if (cfg.Sounds.Count > 0)
        {
            candidates = cfg.Sounds
                .Select(s => Path.Combine(soundsRoot, s))
                .Where(File.Exists)
                .ToList();
        }
        else
        {
            candidates = AudioFilesIn(Path.Combine(soundsRoot, triggerKey));
        }
        if (candidates.Count == 0)
            candidates = AudioFilesIn(Path.Combine(soundsRoot, "default"));
        return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
    }

    private static List<string> AudioFilesIn(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir)
                .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToList()
            : new List<string>();
}
