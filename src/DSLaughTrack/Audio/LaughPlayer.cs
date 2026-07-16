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
    private readonly object _gateLock = new();
    private object? _currentToken;   // non-null while a sound is playing (no-overlap gate)
    private Action? _stopCurrent;    // stops the currently playing sound (for interrupts)

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
            var token = TryBeginPlayback(tc.Interrupt);
            if (token is null)
            {
                _log.Debug($"{triggerKey}: fired while another sound is playing — skipped (no-overlap)");
                return;
            }
            try
            {
                var file = ResolveSound(_soundsRoot, triggerKey, tc, _rng);
                if (file is null)
                {
                    _log.Warn($"{triggerKey}: fired, but no audio files found (looked in {Path.Combine(_soundsRoot, triggerKey)} and {Path.Combine(_soundsRoot, "default")}) — nothing played");
                    EndPlayback(token);
                    return;
                }
                var volume = (float)Math.Clamp(cfg.MasterVolume * tc.Volume, 0.0, 1.0);
                var reader = new AudioFileReader(file) { Volume = volume };
                var output = new WaveOutEvent();
                output.PlaybackStopped += (_, _) => { output.Dispose(); reader.Dispose(); EndPlayback(token); };
                RegisterStopper(token, output.Stop);
                output.Init(reader);
                output.Play();
                _log.Info($"LAUGH [{triggerKey}] {Path.GetFileName(file)} (vol {volume:0.00})");
            }
            catch
            {
                EndPlayback(token);
                throw;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"{triggerKey}: audio playback failed: {ex.Message}");
        }
    }

    /// No-overlap gate: at most one sound plays at a time. Non-interrupting fires
    /// while busy are skipped (not queued); an interrupting fire (e.g. death) stops
    /// the current sound and takes over. Returns a playback token, or null if skipped.
    internal object? TryBeginPlayback(bool interrupt)
    {
        lock (_gateLock)
        {
            if (_currentToken is not null)
            {
                if (!interrupt) return null;
                _stopCurrent?.Invoke(); // its PlaybackStopped callback disposes it; EndPlayback with the old token no-ops below
            }
            _currentToken = new object();
            _stopCurrent = null;
            return _currentToken;
        }
    }

    internal void RegisterStopper(object token, Action stop)
    {
        lock (_gateLock)
        {
            if (ReferenceEquals(_currentToken, token)) _stopCurrent = stop;
        }
    }

    internal void EndPlayback(object token)
    {
        lock (_gateLock)
        {
            if (ReferenceEquals(_currentToken, token))
            {
                _currentToken = null;
                _stopCurrent = null;
            }
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
