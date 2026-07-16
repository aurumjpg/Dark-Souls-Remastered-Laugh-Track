using DSLaughTrack.Config;
using DSLaughTrack.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DSLaughTrack.Audio;

/// Audio format detected from a file's actual bytes (not its extension).
internal enum SniffedFormat { Wav, Mp3, Unknown }

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
                var reader = OpenAudioFile(file);
                var samples = new VolumeSampleProvider(reader.ToSampleProvider()) { Volume = volume };
                var output = new WaveOutEvent();
                output.PlaybackStopped += (_, _) => { output.Dispose(); reader.Dispose(); EndPlayback(token); };
                RegisterStopper(token, output.Stop);
                output.Init(samples);
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

    /// Detects the real audio format from the file's leading bytes, so mislabeled
    /// downloads (e.g. MP3 data with a .wav filename) still decode correctly.
    internal static SniffedFormat DetectFormat(string path)
    {
        var header = new byte[4];
        using var fs = File.OpenRead(path);
        if (fs.Read(header, 0, 4) < 4) return SniffedFormat.Unknown;
        if (header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F') return SniffedFormat.Wav;
        if (header[0] == 'I' && header[1] == 'D' && header[2] == '3') return SniffedFormat.Mp3;       // ID3-tagged MP3
        if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0) return SniffedFormat.Mp3;                // bare MPEG frame sync
        return SniffedFormat.Unknown;
    }

    internal static WaveStream OpenAudioFile(string path) => DetectFormat(path) switch
    {
        SniffedFormat.Wav => new WaveFileReader(path),
        SniffedFormat.Mp3 => new Mp3FileReader(path),
        // Unknown: fall back to extension-based decoding (also covers anything
        // else Windows MediaFoundation can play).
        _ => new AudioFileReader(path),
    };

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
